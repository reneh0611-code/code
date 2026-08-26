using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCRootGroundSnapper : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3f;
        [SerializeField] private float rayDistance = 10f;
        [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.65f;
        [SerializeField] private int settleFrames = 1;

        private IEnumerator Start()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller == null) yield break;

            for (int i = 0; i < settleFrames; i++) yield return null;

            Vector3 origin = transform.position + Vector3.up * rayStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            RaycastHit bestHit = default;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                Transform ht = hit.collider.transform;
                if (ht == transform || ht.IsChildOf(transform)) continue;
                if (hit.normal.y < minGroundNormalY) continue;

                string n = hit.collider.name.ToLowerInvariant();
                if (n.Contains("wall") || n.Contains("roof") || n.Contains("building")) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[CYDOY] NPC root snap found no valid ground below {name}.", this);
                yield break;
            }

            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float controllerBottomLocal = controller.center.y - controller.height * 0.5f;
            float currentBottomWorld = transform.position.y + controllerBottomLocal * scaleY;
            float deltaY = bestHit.point.y - currentBottomWorld;

            controller.enabled = false;
            transform.position += Vector3.up * deltaY;
            controller.enabled = true;

            Debug.Log($"[CYDOY] NPC ROOT snapped {name}: ground={bestHit.point.y:F3}, oldBottom={currentBottomWorld:F3}, delta={deltaY:F3}, collider={bestHit.collider.name}", this);
            enabled = false;
        }
    }
}
