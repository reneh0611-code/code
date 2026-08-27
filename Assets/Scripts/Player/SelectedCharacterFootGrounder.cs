using System.Collections;
using CheatOnYourDayOnes.UI;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// One-shot visual grounding for runtime-selected playable characters.
    /// Waits until the skinned mesh has evaluated its idle pose, then aligns the actual
    /// renderer sole to the CharacterController bottom. It never adjusts continuously,
    /// so locomotion animation cannot create vertical bobbing.
    /// </summary>
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
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
            // Let Instantiate -> controller assignment -> Animator.Rebind -> Idle evaluation finish.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (visual == null) yield break;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.Update(0f);
            }

            foreach (SkinnedMeshRenderer skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) yield break;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            CharacterController cc = GetComponent<CharacterController>();

            // The player's transform is not necessarily the floor. The gameplay capsule bottom is.
            float targetGroundY;
            if (cc != null)
            {
                Vector3 centerWorld = transform.TransformPoint(cc.center);
                float worldHalfHeight = cc.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
                targetGroundY = centerWorld.y - worldHalfHeight;
            }
            else
            {
                targetGroundY = transform.position.y;
            }

            float correction = targetGroundY - bounds.min.y;
            visual.position += Vector3.up * correction;

            // Re-evaluate once after moving the wrapper and log the remaining error for diagnostics.
            if (animator != null) animator.Update(0f);
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds check = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) check.Encapsulate(renderers[i].bounds);
                float remaining = check.min.y - targetGroundY;
                Debug.Log($"[CYDOY GROUND] '{visual.name}' snapped by {correction:F3}m. Remaining sole offset: {remaining:F3}m.", visual.gameObject);
            }

            _snapRoutine = null;
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
