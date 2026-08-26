using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    /// <summary>
    /// Grounds the visible NPC model against the CharacterController bottom once,
    /// after the controller has settled on the world. Bones/animation are untouched.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCVisualControllerGrounder : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private int settleFrames = 6;
        [SerializeField] private float soleOffset = 0f;

        private IEnumerator Start()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller == null) yield break;

            ResolveVisualRoot();
            if (visualRoot == null) yield break;

            for (int i = 0; i < settleFrames; i++)
                yield return null;

            // Give gravity a chance to settle the controller on the road.
            float timeout = 1.0f;
            while (!controller.isGrounded && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) yield break;

            bool hasBounds = false;
            Bounds b = default;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!hasBounds) yield break;

            float controllerBottomY = transform.TransformPoint(controller.center).y
                                      - controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
            float desiredVisualBottomY = controllerBottomY + soleOffset;
            float deltaY = desiredVisualBottomY - b.min.y;

            visualRoot.position += Vector3.up * deltaY;

            Debug.Log($"[CYDOY] NPC grounded from controller: {name} | VisualBottom={b.min.y:F4} | ControllerBottom={controllerBottomY:F4} | Delta={deltaY:F4}", this);
            enabled = false;
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null) return;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visualRoot = animator.transform;
                return;
            }

            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                Transform current = smr.transform;
                while (current.parent != null && current.parent != transform)
                    current = current.parent;
                visualRoot = current;
            }
        }

        public void Configure(Transform root, float offset = 0f)
        {
            visualRoot = root;
            soleOffset = offset;
        }
    }
}
