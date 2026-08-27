using System.Collections.Generic;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class AmbientNPCSpawner : MonoBehaviour
    {
        [SerializeField] private int targetCount = 12;
        [SerializeField] private float minSpawnRadius = 10f;
        [SerializeField] private float maxSpawnRadius = 28f;
        [SerializeField] private float despawnRadius = 55f;
        [SerializeField] private float respawnCheckInterval = 1.25f;
        [SerializeField] private float rayHeight = 120f;
        [SerializeField] private float maxGroundSlope = 0.72f;

        private readonly List<NPCWanderer> _spawned = new();
        private Transform _visualTemplate;
        private RuntimeAnimatorController _controller;
        private Transform _root;
        private float _nextCheck;

        private void Start()
        {
            NetworkObject net = GetComponent<NetworkObject>();
            if (net != null && !net.IsOwner) { enabled = false; return; }

            _visualTemplate = transform.Find("CharacterVisual");
            if (_visualTemplate == null)
            {
                Debug.LogWarning("[CYDOY NPC] No CharacterVisual found on player; ambient NPC spawning disabled.", this);
                enabled = false;
                return;
            }

            Animator a = _visualTemplate.GetComponentInChildren<Animator>(true);
            _controller = a != null ? a.runtimeAnimatorController : Resources.Load<RuntimeAnimatorController>("Tripo_Locomotion_ExactGeneric");

            GameObject existing = GameObject.Find("Generated_NPCs");
            _root = existing != null ? existing.transform : new GameObject("Generated_NPCs").transform;
            SpawnUntilFull();
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + respawnCheckInterval;

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                NPCWanderer npc = _spawned[i];
                if (npc == null) { _spawned.RemoveAt(i); continue; }
                Vector3 d = npc.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > despawnRadius * despawnRadius)
                {
                    Destroy(npc.gameObject);
                    _spawned.RemoveAt(i);
                }
            }

            SpawnUntilFull();
        }

        private void SpawnUntilFull()
        {
            int guard = 0;
            while (_spawned.Count < targetCount && guard++ < targetCount * 8)
            {
                if (TryFindSpawnPoint(out Vector3 point))
                    SpawnNpc(point);
            }
        }

        private bool TryFindSpawnPoint(out Vector3 point)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < .1f) dir = Vector2.right;
            float radius = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 xz = transform.position + new Vector3(dir.x, 0f, dir.y) * radius;
            Vector3 origin = new(xz.x, transform.position.y + rayHeight, xz.z);

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < maxGroundSlope) continue;
                if (hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (hit.collider.GetComponentInParent<NetworkObject>() != null) continue;
                if (!found || hit.point.y > bestY) { bestY = hit.point.y; found = true; }
            }

            point = new Vector3(xz.x, bestY + .05f, xz.z);
            return found;
        }

        private void SpawnNpc(Vector3 point)
        {
            GameObject npc = new($"AmbientNPC_{_spawned.Count + 1:00}");
            npc.transform.SetParent(_root, true);
            npc.transform.position = point;
            npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            CharacterController cc = npc.AddComponent<CharacterController>();
            cc.height = 1.78f;
            cc.radius = .31f;
            cc.center = new Vector3(0f, .89f, 0f);
            cc.stepOffset = .22f;
            cc.slopeLimit = 45f;

            GameObject visual = Instantiate(_visualTemplate.gameObject, npc.transform);
            visual.name = "CharacterVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (NetworkBehaviour n in visual.GetComponentsInChildren<NetworkBehaviour>(true)) n.enabled = false;
            foreach (PlayerMeleeCombat combat in visual.GetComponentsInChildren<PlayerMeleeCombat>(true)) combat.enabled = false;
            foreach (CharacterAnimationDriver driver in visual.GetComponentsInChildren<CharacterAnimationDriver>(true)) driver.enabled = false;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (_controller != null) animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
            }

            NPCWanderer wanderer = npc.AddComponent<NPCWanderer>();
            wanderer.Configure(Random.Range(1.15f, 1.45f), Random.Range(7f, 14f));
            _spawned.Add(wanderer);
        }
    }
}
