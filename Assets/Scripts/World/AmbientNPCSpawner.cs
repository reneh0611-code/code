using System.Collections.Generic;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class AmbientNPCSpawner : MonoBehaviour
    {
        [SerializeField] private int targetCount = 7;
        [SerializeField] private float minSpawnRadius = 8f;
        [SerializeField] private float maxSpawnRadius = 20f;
        [SerializeField] private float despawnRadius = 30f;
        [SerializeField] private float respawnCheckInterval = 2.0f;
        [SerializeField] private float rayHeight = 80f;
        [SerializeField] private float maxGroundSlope = 0.72f;

        private readonly List<NPCWanderer> _spawned = new();
        private Transform _visualTemplate;
        private GameObject _frozenNpcTemplate;
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

            _frozenNpcTemplate = Instantiate(_visualTemplate.gameObject);
            _frozenNpcTemplate.name = "OriginalCharacter_NPCTemplate";
            _frozenNpcTemplate.SetActive(false);
            DontDestroyOnLoad(_frozenNpcTemplate);

            GameObject existing = GameObject.Find("Generated_NPCs");
            _root = existing != null ? existing.transform : new GameObject("Generated_NPCs").transform;

            // Remove only auto-generated ambient NPCs from an older/runtime setup, never authored NPCs.
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                Transform child = _root.GetChild(i);
                if (child != null && child.name.StartsWith("AmbientNPC_"))
                    Destroy(child.gameObject);
            }

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

                // A corpse is now a persistent gameplay object. Remove it from the ambient
                // population bookkeeping without destroying it, otherwise a body being dragged
                // away from its original root position vanishes at the despawn radius.
                if (npc.IsDead)
                {
                    _spawned.RemoveAt(i);
                    continue;
                }

                if (npc.IsCarried) continue;
                Vector3 d = npc.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > despawnRadius * despawnRadius)
                {
                    Destroy(npc.gameObject);
                    _spawned.RemoveAt(i);
                }
            }

            while (_spawned.Count > targetCount)
            {
                int farthest = -1;
                float farthestSqr = -1f;
                for (int i = 0; i < _spawned.Count; i++)
                {
                    if (_spawned[i] == null) continue;
                    Vector3 d = _spawned[i].transform.position - transform.position;
                    d.y = 0f;
                    float sqr = d.sqrMagnitude;
                    if (sqr > farthestSqr) { farthestSqr = sqr; farthest = i; }
                }
                if (farthest < 0) break;
                Destroy(_spawned[farthest].gameObject);
                _spawned.RemoveAt(farthest);
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

            point = new Vector3(xz.x, bestY + .01f, xz.z);
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

            GameObject source = _frozenNpcTemplate != null ? _frozenNpcTemplate : _visualTemplate.gameObject;
            GameObject visual = Instantiate(source, npc.transform);
            visual.SetActive(true);
            visual.name = "CharacterVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (NetworkBehaviour n in visual.GetComponentsInChildren<NetworkBehaviour>(true)) n.enabled = false;
            foreach (PlayerMeleeCombat combat in visual.GetComponentsInChildren<PlayerMeleeCombat>(true)) combat.enabled = false;
            foreach (CharacterAnimationDriver driver in visual.GetComponentsInChildren<CharacterAnimationDriver>(true)) driver.enabled = false;
            foreach (SkinnedMeshRenderer skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = false;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (_controller != null) animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.enabled = true;
            }

            NPCWanderer wanderer = npc.AddComponent<NPCWanderer>();
            wanderer.Configure(Random.Range(1.15f, 1.40f), Random.Range(5f, 10f));
            _spawned.Add(wanderer);
        }

        private void OnDestroy()
        {
            if (_frozenNpcTemplate != null) Destroy(_frozenNpcTemplate);
        }
    }
}
