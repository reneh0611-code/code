using System.Collections;
using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Grounds the stable CharacterVisual wrapper, never the animated model root itself.
    /// This prevents Animator root curves from undoing the visual Y correction.
    /// </summary>
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3.0f;
        [SerializeField] private float rayDistance = 12.0f;
        [SerializeField] private float soleOffset = 0.002f;
        [SerializeField] private int stabilizationFrames = 12;

        private Transform _lastVisual;
        private Coroutine _snapRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnLocalPlayer()
        {
            RuntimeInstaller.EnsureExists();
        }

        private void Update()
        {
            Transform visualRoot = transform.Find("CharacterVisual");
            if (visualRoot == null || visualRoot.childCount == 0) return;

            Transform current = visualRoot.GetChild(0);
            if (current == _lastVisual) return;

            _lastVisual = current;
            if (_snapRoutine != null) StopCoroutine(_snapRoutine);
            _snapRoutine = StartCoroutine(StabilizeAndGround(visualRoot, current));
        }

        private IEnumerator StabilizeAndGround(Transform visualRoot, Transform animatedVisual)
        {
            FixedWorldVisualGrounder oldWorldGrounder = GetComponent<FixedWorldVisualGrounder>();
            if (oldWorldGrounder != null) oldWorldGrounder.enabled = false;
            MixamoRuntimePoseAndGrounder oldMixamoGrounder = GetComponent<MixamoRuntimePoseAndGrounder>();
            if (oldMixamoGrounder != null) oldMixamoGrounder.enabled = false;

            Animator animator = animatedVisual.GetComponentInChildren<Animator>(true);
            foreach (SkinnedMeshRenderer skin in animatedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

            // Repeat for several rendered frames so Animator.Rebind/Idle evaluation cannot restore
            // an old root offset after the first correction.
            for (int frame = 0; frame < stabilizationFrames; frame++)
            {
                yield return new WaitForEndOfFrame();
                if (visualRoot == null || animatedVisual == null) yield break;

                if (animator != null) animator.Update(0f);
                GroundOnce(visualRoot, animatedVisual, frame == stabilizationFrames - 1);
            }

            _snapRoutine = null;
        }

        private void GroundOnce(Transform visualRoot, Transform animatedVisual, bool logResult)
        {
            if (!TryGetBounds(animatedVisual, out Bounds bounds)) return;
            if (!TryFindWorldGround(bounds, out float groundY)) return;

            float correction = (groundY + soleOffset) - bounds.min.y;

            // CRITICAL: move CharacterVisual, not SelectedCharacterVisual. The Animator can own the
            // selected model's root transform but does not own this wrapper.
            visualRoot.position += Vector3.up * correction;

            if (logResult && TryGetBounds(animatedVisual, out Bounds check))
            {
                float remaining = check.min.y - (groundY + soleOffset);
                Debug.Log($"[CYDOY GROUND] Stable wrapper grounded by {correction:F3}m. Ground={groundY:F3}, remaining={remaining:F3}m, wrapperLocalY={visualRoot.localPosition.y:F3}.", animatedVisual.gameObject);
            }
        }

        private bool TryFindWorldGround(Bounds bounds, out float groundY)
        {
            Vector3 origin = new(bounds.center.x, bounds.max.y + rayStartHeight, bounds.center.z);
            float distance = rayDistance + bounds.size.y + rayStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            groundY = 0f;

            foreach (RaycastHit hit in hits)
            {
                Collider c = hit.collider;
                if (c == null || hit.normal.y < .45f) continue;
                Transform ht = c.transform;

                // Never use any part of this player as ground.
                if (ht == transform || ht.IsChildOf(transform)) continue;

                // Never stand visually on NPCs, vehicles or other character capsules.
                if (c.GetComponentInParent<NPCWanderer>() != null) continue;
                if (c.GetComponentInParent<DriveableCar>() != null) continue;
                if (c.GetComponentInParent<CharacterController>() != null) continue;

                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;

                // Terrain is always valid. Static/non-character environment colliders are valid too,
                // which keeps roads, sidewalks and building floors usable even if they are not tagged.
                bool worldSurface = c is TerrainCollider || c.gameObject.isStatic || rb == null;
                if (!worldSurface) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                Debug.LogWarning("[CYDOY GROUND] No valid terrain/road surface found below selected character.", this);

            return found;
        }

        private static bool TryGetBounds(Transform visual, out Bounds combined)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            combined = default;

            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!has)
                {
                    combined = r.bounds;
                    has = true;
                }
                else combined.Encapsulate(r.bounds);
            }
            return has;
        }

        private sealed class RuntimeInstaller : MonoBehaviour
        {
            private static RuntimeInstaller _instance;

            public static void EnsureExists()
            {
                if (_instance != null) return;
                GameObject go = new("CYDOY_SelectedCharacterGroundInstaller");
                _instance = go.AddComponent<RuntimeInstaller>();
                DontDestroyOnLoad(go);
            }

            private void Update()
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
                NetworkObject player = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (player == null) return;

                if (player.GetComponent<SelectedCharacterFootGrounder>() == null)
                    player.gameObject.AddComponent<SelectedCharacterFootGrounder>();

                enabled = false;
            }
        }
    }
}
