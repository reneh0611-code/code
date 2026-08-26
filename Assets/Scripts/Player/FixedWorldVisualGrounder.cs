using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    public sealed class FixedWorldVisualGrounder : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private int settleFrames = 2;
        [SerializeField] private float rayStartHeight = 2f;
        [SerializeField] private float rayDistance = 6f;
        [SerializeField] private float soleOffset = 0f;

        private IEnumerator Start()
        {
            ResolveModelRoot();
            if (modelRoot == null) yield break;
            for (int i = 0; i < settleFrames; i++) yield return null;
            AlignOnceToWorldGround();
            enabled = false;
        }

        private void ResolveModelRoot()
        {
            if (modelRoot != null) return;
            Transform visual = transform.Find("CharacterVisual");
            modelRoot = visual != null && visual.childCount > 0 ? visual.GetChild(0) : transform;
        }

        private void AlignOnceToWorldGround()
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            bool hasBounds = false;
            Bounds combined = default;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                else combined.Encapsulate(r.bounds);
            }
            if (!hasBounds) return;

            Vector3 origin = new Vector3(combined.center.x, combined.min.y + rayStartHeight, combined.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float best = float.MaxValue;
            float groundY = 0f;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                Transform ht = hit.collider.transform;
                // Ignore the whole character hierarchy, even when modelRoot == transform.
                if (ht == transform || ht.IsChildOf(transform) || transform.IsChildOf(ht) && ht.GetComponentInParent<CharacterController>() == GetComponentInParent<CharacterController>()) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[CYDOY] Grounding found no world collider below {name}.", this);
                return;
            }

            float deltaY = (groundY + soleOffset) - combined.min.y;
            modelRoot.position += Vector3.up * deltaY;
            Debug.Log($"[CYDOY] Grounded {name}: bottom {combined.min.y:F3} -> ground {groundY:F3}, delta {deltaY:F3}.", this);
        }

        public void SetModelRoot(Transform root) => modelRoot = root;
    }
}
