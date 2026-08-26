using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class ForceSnapNPCsToGroundNow
    {
        [MenuItem("Tools/CYDOY/Tripo Test/Force Snap NPCs To Ground Now")]
        public static void Snap()
        {
            NPCWanderer[] npcs = Object.FindObjectsByType<NPCWanderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int snapped = 0;

            foreach (NPCWanderer wanderer in npcs)
            {
                GameObject npc = wanderer.gameObject;
                if (!TryGetBounds(npc, out Bounds bounds)) continue;

                Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 5f, bounds.center.z);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);

                bool found = false;
                RaycastHit best = default;
                float bestDistance = float.MaxValue;

                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null) continue;
                    Transform ht = hit.collider.transform;
                    if (ht == npc.transform || ht.IsChildOf(npc.transform)) continue;
                    if (hit.normal.y < 0.7f) continue;

                    string name = hit.collider.name.ToLowerInvariant();
                    if (name.Contains("wall") || name.Contains("roof") || name.Contains("building")) continue;

                    if (hit.distance < bestDistance)
                    {
                        bestDistance = hit.distance;
                        best = hit;
                        found = true;
                    }
                }

                if (!found) continue;

                float deltaY = best.point.y - bounds.min.y;

                CharacterController cc = npc.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                Undo.RecordObject(npc.transform, "Snap NPC to Ground");
                npc.transform.position += Vector3.up * deltaY;
                if (cc != null)
                {
                    // Refit capsule around the now-grounded visible model.
                    if (TryGetBounds(npc, out Bounds newBounds))
                    {
                        float worldScaleY = Mathf.Max(0.0001f, Mathf.Abs(npc.transform.lossyScale.y));
                        cc.height = Mathf.Max(0.5f, newBounds.size.y / worldScaleY);
                        Vector3 localCenter = npc.transform.InverseTransformPoint(newBounds.center);
                        cc.center = localCenter;
                        cc.radius = Mathf.Clamp(Mathf.Min(newBounds.size.x, newBounds.size.z) * 0.28f / worldScaleY, 0.18f, 0.45f);
                    }
                    cc.enabled = true;
                }

                // Remove all old runtime grounding components from the NPC so nothing moves it again.
                foreach (NPCRootGroundSnapper g in npc.GetComponents<NPCRootGroundSnapper>()) Object.DestroyImmediate(g);
                foreach (NPCVisualControllerGrounder g in npc.GetComponents<NPCVisualControllerGrounder>()) Object.DestroyImmediate(g);

                Debug.Log($"[CYDOY] FORCE SNAP {npc.name}: rendererBottom={bounds.min.y:F3} -> ground={best.point.y:F3}, delta={deltaY:F3}, groundCollider={best.collider.name}", npc);
                snapped++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("CYDOY · Force NPC Snap", $"Snapped {snapped} NPCs directly in the scene. Their renderer bottoms now sit on the detected horizontal ground surface. Old runtime grounding components were removed.", "OK");
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = default;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
    }
}
