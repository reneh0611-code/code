using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class ForceSnapNPCsToGroundNow
    {
        // Renderer bounds on skinned characters often include a small invisible margin below the actual shoe sole.
        // Sink the visible character slightly so the shoes visually contact the road instead of hovering.
        private const float VisualSoleSink = 0.055f;

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
                    if (hit.distance < bestDistance) { bestDistance = hit.distance; best = hit; found = true; }
                }
                if (!found) continue;

                // Intentionally put the renderer bounds slightly below the mathematical surface.
                // This compensates for invisible skinned-mesh bounds padding and gives visible shoe contact.
                float targetBottom = best.point.y - VisualSoleSink;
                float deltaY = targetBottom - bounds.min.y;

                CharacterController cc = npc.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                Undo.RecordObject(npc.transform, "Snap NPC to Ground");
                npc.transform.position += Vector3.up * deltaY;

                if (cc != null)
                {
                    if (TryGetBounds(npc, out Bounds newBounds))
                    {
                        float sy = Mathf.Max(0.0001f, Mathf.Abs(npc.transform.lossyScale.y));
                        cc.height = Mathf.Max(0.5f, newBounds.size.y / sy);
                        cc.center = npc.transform.InverseTransformPoint(newBounds.center);
                        cc.radius = Mathf.Clamp(Mathf.Min(newBounds.size.x, newBounds.size.z) * 0.28f / sy, 0.18f, 0.45f);
                    }
                    cc.enabled = true;
                }

                foreach (NPCRootGroundSnapper g in npc.GetComponents<NPCRootGroundSnapper>()) Object.DestroyImmediate(g);
                foreach (NPCVisualControllerGrounder g in npc.GetComponents<NPCVisualControllerGrounder>()) Object.DestroyImmediate(g);

                Debug.Log($"[CYDOY] FORCE SNAP {npc.name}: rendererBottom={bounds.min.y:F3} -> visualTarget={targetBottom:F3} (ground={best.point.y:F3}, soleSink={VisualSoleSink:F3}), delta={deltaY:F3}", npc);
                snapped++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("CYDOY · Force NPC Snap", $"Snapped {snapped} NPCs with a {VisualSoleSink * 100f:F1} cm visual sole-contact correction.", "OK");
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
