using System.Collections.Generic;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class PolicePatrolDirector : MonoBehaviour
    {
        private enum PoliceVariant { Chief, Policeman1, Policeman2, Policewoman }

        [SerializeField, Range(1, 8)] private int patrolGroupCount = 1;
        [SerializeField] private float minimumSpawnRadius = 18f;
        [SerializeField] private float maximumSpawnRadius = 92f;
        [SerializeField] private float inactiveDespawnRadius = 108f;
        [SerializeField] private float policeCallRadius = 100f;
        [SerializeField] private float wantedSightRadius = 20f;
        [SerializeField] private float routineControlRadius = 2.4f;
        [SerializeField, Range(0f, 1f)] private float routineControlChance = .18f;
        [SerializeField] private float routineControlCooldown = 55f;
        [SerializeField] private float populationCheckInterval = 2f;

        private readonly List<PoliceOfficerAI> _officers = new();
        private readonly GameObject[] _prefabs = new GameObject[4];
        private RuntimeAnimatorController _controller;
        private Transform _policeRoot;
        private float _nextPopulationCheck;
        private int _nextGroupIndex;
        private int _spawnSerial;
        private int _chiefSlot;
        private float _nextRoutineControl;
        private bool _resourcesReady;
        private bool _loggedMissingResources;

        private static readonly PoliceVariant[] RegularRotation =
        {
            PoliceVariant.Policeman1,
            PoliceVariant.Policewoman,
            PoliceVariant.Policeman2,
            PoliceVariant.Policeman1,
            PoliceVariant.Policewoman,
            PoliceVariant.Policeman2
        };

        private void Awake()
        {
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            {
                enabled = false;
                return;
            }

            _chiefSlot = Random.Range(0, 10);
            LoadResources();
        }

        private void OnEnable() => NPCWitnessCoordinator.PoliceReportCompleted += OnPoliceReportCompleted;
        private void OnDisable() => NPCWitnessCoordinator.PoliceReportCompleted -= OnPoliceReportCompleted;

        private void Start()
        {
            GameObject existing = GameObject.Find("Generated_Police");
            _policeRoot = existing != null ? existing.transform : new GameObject("Generated_Police").transform;
            EnsurePopulation();
        }

        private void Update()
        {
            if (Time.time < _nextPopulationCheck) return;
            _nextPopulationCheck = Time.time + populationCheckInterval;

            for (int i = _officers.Count - 1; i >= 0; i--)
            {
                PoliceOfficerAI officer = _officers[i];
                if (officer == null)
                {
                    _officers.RemoveAt(i);
                    continue;
                }

                Vector3 delta = officer.transform.position - transform.position;
                delta.y = 0f;
                if (!officer.IsResponding && delta.sqrMagnitude > inactiveDespawnRadius * inactiveDespawnRadius)
                {
                    Destroy(officer.gameObject);
                    _officers.RemoveAt(i);
                }
            }

            EnsurePopulation();
            AlertOfficersWhoSeeWantedPlayer();
            TryStartRoutineControl();
        }

        private void AlertOfficersWhoSeeWantedPlayer()
        {
            PlayerPoliceStatus policeStatus = GetComponent<PlayerPoliceStatus>();
            if (policeStatus == null || policeStatus.WantedStars <= 0) return;
            float sightRadiusSqr = wantedSightRadius * wantedSightRadius;
            foreach (PoliceOfficerAI officer in _officers)
            {
                if (officer == null) continue;
                Vector3 toPlayer = transform.position - officer.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude <= sightRadiusSqr)
                    officer.RespondToPoliceCall(transform.position, transform);
            }
        }

        private void TryStartRoutineControl()
        {
            PlayerPoliceStatus policeStatus = GetComponent<PlayerPoliceStatus>();
            if (policeStatus == null || policeStatus.WantedStars > 0 || policeStatus.IsInPoliceControl) return;
            if (Time.time < _nextRoutineControl) return;

            float rangeSqr = routineControlRadius * routineControlRadius;
            foreach (PoliceOfficerAI officer in _officers)
            {
                if (officer == null || officer.IsResponding) continue;
                Vector3 toPlayer = transform.position - officer.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > rangeSqr) continue;
                if (Random.value > routineControlChance) return;

                _nextRoutineControl = Time.time + routineControlCooldown;
                policeStatus.BeginPoliceControl(officer, false);
                return;
            }
        }

        private void LoadResources()
        {
            _prefabs[(int)PoliceVariant.Chief] = Resources.Load<GameObject>("Police/Chief");
            _prefabs[(int)PoliceVariant.Policeman1] = Resources.Load<GameObject>("Police/Policeman1");
            _prefabs[(int)PoliceVariant.Policeman2] = Resources.Load<GameObject>("Police/Policeman2");
            _prefabs[(int)PoliceVariant.Policewoman] = Resources.Load<GameObject>("Police/Policewoman");
            _controller = Resources.Load<RuntimeAnimatorController>("Tripo_Locomotion_ExactGeneric");
            _resourcesReady = true;
            foreach (GameObject prefab in _prefabs)
                if (prefab == null) _resourcesReady = false;
        }

        private void EnsurePopulation()
        {
            if (!_resourcesReady)
            {
                LoadResources();
                if (!_resourcesReady && !_loggedMissingResources)
                {
                    _loggedMissingResources = true;
                    Debug.LogWarning("[CYDOY POLICE] Police prefabs are still being built. Stop Play Mode once and let Unity finish importing them.", this);
                }
                return;
            }

            int guard = 0;
            while (_officers.Count < patrolGroupCount * 2 && guard++ < patrolGroupCount * 5)
            {
                if (!TryFindPatrolSpawn(out Vector3 spawnPoint)) continue;
                SpawnPair(spawnPoint, _nextGroupIndex++);
            }
        }

        private void SpawnPair(Vector3 center, int groupIndex)
        {
            Vector3 right = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right;
            PoliceOfficerAI first = SpawnOfficer(center - right * .52f, groupIndex, true);
            PoliceOfficerAI second = SpawnOfficer(center + right * .52f, groupIndex, false);
            if (first != null && second != null)
            {
                first.SetPartner(second);
                second.SetPartner(first);
            }
        }

        private PoliceOfficerAI SpawnOfficer(Vector3 point, int groupIndex, bool leader)
        {
            PoliceVariant variant = NextVariant();
            GameObject modelPrefab = _prefabs[(int)variant];
            if (modelPrefab == null) return null;

            GameObject officerObject = new($"Police_{groupIndex:00}_{variant}");
            officerObject.transform.SetParent(_policeRoot, true);
            officerObject.transform.position = point;
            officerObject.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject groundAnchorObject = new("PoliceGroundAnchor");
            Transform groundAnchor = groundAnchorObject.transform;
            groundAnchor.SetParent(officerObject.transform, false);

            GameObject visual = Instantiate(modelPrefab, groundAnchor);
            visual.name = "PoliceVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            Bounds bounds = CalculateBounds(visual);
            if (bounds.size.y > .1f)
            {
                float targetHeight = variant == PoliceVariant.Policewoman ? 1.70f : 1.80f;
                float scale = Mathf.Clamp(targetHeight / bounds.size.y, .45f, 2.2f);
                visual.transform.localScale *= scale;
                bounds = CalculateBounds(visual);
            }
            groundAnchor.position += Vector3.up * (point.y - bounds.min.y + .01f);
            bounds = CalculateBounds(visual);

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (_controller != null) animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.enabled = true;
            }

            CharacterController characterController = officerObject.AddComponent<CharacterController>();
            characterController.height = Mathf.Clamp(bounds.size.y * .94f, 1.55f, 1.92f);
            characterController.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * .23f, .23f, .34f);
            characterController.center = new Vector3(0f, characterController.height * .5f, 0f);
            characterController.stepOffset = .22f;
            characterController.slopeLimit = 46f;

            PoliceOfficerAI officer = officerObject.AddComponent<PoliceOfficerAI>();
            officer.Configure(groupIndex, leader, point);
            _officers.Add(officer);
            return officer;
        }

        private PoliceVariant NextVariant()
        {
            int serial = _spawnSerial++;
            if (serial % 10 == _chiefSlot) return PoliceVariant.Chief;
            return RegularRotation[serial % RegularRotation.Length];
        }

        private bool TryFindPatrolSpawn(out Vector3 point)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector2.right;
            float radius = Random.Range(minimumSpawnRadius, maximumSpawnRadius);
            Vector3 desired = transform.position + new Vector3(direction.x, 0f, direction.y) * radius;
            Vector3 origin = desired + Vector3.up * 80f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 180f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < .72f) continue;
                if (hit.collider.GetComponentInParent<NetworkObject>() != null) continue;
                if (hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (hit.collider.GetComponentInParent<PoliceOfficerAI>() != null) continue;
                if (!found || hit.point.y > bestY)
                {
                    found = true;
                    bestY = hit.point.y;
                }
            }
            point = new Vector3(desired.x, bestY + .01f, desired.z);
            return found;
        }

        private void OnPoliceReportCompleted(Vector3 incidentPosition, Transform suspect)
        {
            float radiusSqr = policeCallRadius * policeCallRadius;
            foreach (PoliceOfficerAI officer in _officers)
            {
                if (officer == null) continue;
                Vector3 delta = officer.transform.position - incidentPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                    officer.RespondToPoliceCall(incidentPosition, suspect);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position + Vector3.up * .9f, new Vector3(.6f, 1.8f, .6f));
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
