using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// One-shot visual grounding for runtime-selected playable characters.
    /// Uses the actual world surface below the character instead of the player root/controller.
    /// </summary>
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3.0f;
        [SerializeField] private float rayDistance = 10.0f;
        [SerializeField] private float soleOffset = 0.005f;

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
            _snapRoutine = StartCoroutine(SnapAfterRender(current));
        }

        private IEnumerator SnapAfterRender(Transform visual)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (visual == null) yield break;

            // Disable older one-shot grounders on the selected playable character so they cannot
            // overwrite the world-ground result afterwards.
            FixedWorldVisualGrounder oldWorldGrounder = GetComponent<FixedWorldVisualGrounder>();
            if (oldWorldGrounder != null) oldWorldGrounder.enabled = false;
            MixamoRuntimePoseAndGrounder oldMixamoGrounder = GetComponent<MixamoRuntimePoseAndGrounder>();
            if (oldMixamoGrounder != null) oldMixamoGrounder.enabled = false;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.Update(0f);

            foreach (SkinnedMeshRenderer skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

            if (!TryGetBounds(visual, out Bounds bounds)) yield break;

            // Ray from above the visual center straight down through the player and onto the real
            // terrain/road. Ignore every collider belonging to this player hierarchy.
            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + rayStartHeight, bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance + bounds.size.y + rayStartHeight, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearestDistance = float.MaxValue;
            float groundY = 0f;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                Transform ht = hit.collider.transform;
                if (ht == transform || ht.IsChildOf(transform)) continue;
                if (hit.normal.y < 0.45f) continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("[CYDOY GROUND] No world surface found below selected character.", visual.gameObject);
                yield break;
            }

            float correction = (groundY + soleOffset) - bounds.min.y;
            visual.position += Vector3.up * correction;

            if (animator != null) animator.Update(0f);

            float remaining = 0f;
            if (TryGetBounds(visual, out Bounds check))
                remaining = check.min.y - (groundY + soleOffset);

            Debug.Log($"[CYDOY GROUND] '{visual.name}' WORLD grounded by {correction:F3}m. GroundY={groundY:F3}, remaining sole offset={remaining:F3}m.", visual.gameObject);
            _snapRoutine = null;
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
