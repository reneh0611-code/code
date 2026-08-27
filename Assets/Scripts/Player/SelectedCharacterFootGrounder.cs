using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Keeps the selected playable visual planted on the real world surface.
    /// The stable CharacterVisual wrapper is corrected, never the animated FBX root.
    /// A small negative sink is intentional: a couple of centimetres into the ground is
    /// visually preferable to any visible floating gap.
    /// </summary>
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3.0f;
        [SerializeField] private float rayDistance = 12.0f;
        [SerializeField] private float forcedSink = 0.035f;
        [SerializeField] private float refreshInterval = 0.10f;
        [SerializeField] private float maxCorrectionPerPass = 1.0f;

        private Transform _visualRoot;
        private Transform _animatedVisual;
        private float _nextGroundCheck;
        private bool _prepared;

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
            if (_animatedVisual != current || _visualRoot != visualRoot)
            {
                _visualRoot = visualRoot;
                _animatedVisual = current;
                _prepared = false;
                _nextGroundCheck = 0f;
            }

            if (!_prepared)
            {
                DisableCompetingGrounders();
                foreach (SkinnedMeshRenderer skin in _animatedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.updateWhenOffscreen = false;
                _prepared = true;
            }

            if (Time.unscaledTime < _nextGroundCheck) return;
            _nextGroundCheck = Time.unscaledTime + refreshInterval;
            GroundNow();
        }

        private void DisableCompetingGrounders()
        {
            FixedWorldVisualGrounder oldWorldGrounder = GetComponent<FixedWorldVisualGrounder>();
            if (oldWorldGrounder != null) oldWorldGrounder.enabled = false;

            MixamoRuntimePoseAndGrounder oldMixamoGrounder = GetComponent<MixamoRuntimePoseAndGrounder>();
            if (oldMixamoGrounder != null) oldMixamoGrounder.enabled = false;
        }

        private void GroundNow()
        {
            if (_visualRoot == null || _animatedVisual == null) return;
            if (!TryGetBounds(_animatedVisual, out Bounds bounds)) return;
            if (!TryFindWorldGround(bounds, out float groundY)) return;

            float desiredBottom = groundY - forcedSink;
            float correction = desiredBottom - bounds.min.y;
            correction = Mathf.Clamp(correction, -maxCorrectionPerPass, maxCorrectionPerPass);

            // CharacterVisual is outside the animated hierarchy, so animation/root curves cannot
            // overwrite this offset. Repeating the correction also follows hills and road height.
            _visualRoot.position += Vector3.up * correction;
        }

        private bool TryFindWorldGround(Bounds bounds, out float groundY)
        {
            // Cast through the player's X/Z position instead of relying on a potentially odd FBX
            // renderer centre. This keeps the query directly underneath the gameplay capsule.
            Vector3 origin = new(transform.position.x, Mathf.Max(bounds.max.y, transform.position.y) + rayStartHeight, transform.position.z);
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

                if (ht == transform || ht.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<NPCWanderer>() != null) continue;
                if (c.GetComponentInParent<DriveableCar>() != null) continue;
                if (c.GetComponentInParent<CharacterController>() != null) continue;

                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;

                bool worldSurface = c is TerrainCollider || c.gameObject.isStatic || rb == null;
                if (!worldSurface) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

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
                else
                {
                    combined.Encapsulate(r.bounds);
                }
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
