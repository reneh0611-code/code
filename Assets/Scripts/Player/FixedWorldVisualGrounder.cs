using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Grounds only the rendered model once against the real world surface.
    /// No bones, animation curves, animator parameters or controller motion are modified.
    /// The resulting local visual offset then stays fixed so locomotion animation remains untouched.
    /// </summary>
    public sealed class FixedWorldVisualGrounder : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private int settleFrames = 2;
        [SerializeField] private float rayStartHeight = 1.5f;
        [SerializeField] private float rayDistance = 4f;
        [SerializeField] private float soleOffset = 0f;

        private IEnumerator Start()
        {
            ResolveModelRoot();
            if (modelRoot == null)
                yield break;

            for (int i = 0; i < settleFrames; i++)
                yield return null;

            AlignOnceToWorldGround();
            enabled = false;
        }

        private void ResolveModelRoot()
        {
            if (modelRoot != null) return;
            Transform visual = transform.Find("CharacterVisual");
            if (visual != null && visual.childCount > 0)
                modelRoot = visual.GetChild(0);
        }

        private void AlignOnceToWorldGround()
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            bool hasBounds = false;
            Bounds combined = default;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                else combined.Encapsulate(r.bounds);
            }
            if (!hasBounds) return;

            Vector3 rayOrigin = new Vector3(combined.center.x, combined.min.y + rayStartHeight, combined.center.z);
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            float groundY = combined.min.y;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                Transform ht = hit.collider.transform;
                if (ht == transform || ht.IsChildOf(transform)) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found) return;

            float deltaY = (groundY + soleOffset) - combined.min.y;
            modelRoot.position += Vector3.up * deltaY;

            Debug.Log($"[CYDOY] Fixed visual grounding applied once. VisualBottom={combined.min.y:F4}, Ground={groundY:F4}, DeltaY={deltaY:F4}. Animation untouched.", this);
        }
    }
}
