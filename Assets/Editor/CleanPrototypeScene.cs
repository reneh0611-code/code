using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class CleanPrototypeScene
    {
        [MenuItem("Tools/CYDOY/Scene/Reset Scene - Keep Lights Only")]
        public static void ResetSceneKeepLightsOnly()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("CYDOY", "No active scene found.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Reset prototype scene",
                "This removes the visible prototype content we built (roads, sidewalks, buildings, props, NPCs, car, HUD/catalog/test objects) while preserving all Light objects.\n\nCore managers/network objects are also preserved so the project still runs.\n\nContinue?",
                "Reset scene",
                "Cancel");

            if (!confirm) return;

            List<GameObject> destroy = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (ShouldPreserve(root)) continue;
                if (IsGeneratedPrototypeRoot(root) || IsVisiblePrototypeObject(root))
                    destroy.Add(root);
            }

            int removed = 0;
            foreach (GameObject go in destroy)
            {
                if (go == null) continue;
                Undo.DestroyObjectImmediate(go);
                removed++;
            }

            // Also clean generated prototype children that may live below a preserved manager/root.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                RemoveGeneratedChildren(root.transform, ref removed);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
                EditorSceneManager.SaveScene(scene);

            Debug.Log($"[CYDOY] Scene reset complete. Removed {removed} prototype objects/roots. All Light components were preserved.");
            EditorUtility.DisplayDialog("CYDOY", $"Scene cleaned.\n\nRemoved: {removed}\nLights preserved.\n\nYou can now build the road layout first.", "Done");
        }

        private static bool ShouldPreserve(GameObject go)
        {
            if (go == null) return true;

            // Preserve any object that is itself a light or contains the scene's lighting rig.
            if (go.GetComponent<Light>() != null) return true;
            if (go.GetComponentsInChildren<Light>(true).Length > 0) return true;

            string n = go.name.ToLowerInvariant();

            // Preserve core systems/managers required for play mode.
            if (n.Contains("network") || n.Contains("manager") || n.Contains("bootstrap") ||
                n.Contains("eventsystem") || n.Contains("systems") || n.Contains("runtime"))
                return true;

            return false;
        }

        private static bool IsGeneratedPrototypeRoot(GameObject go)
        {
            string n = go.name.ToLowerInvariant();
            return
                n == "visualprototype" ||
                n == "modularstreet" ||
                n == "roadpack_catalog" ||
                n.StartsWith("generated_tripo") ||
                n.StartsWith("tripo_npc") ||
                n == "car" ||
                n.StartsWith("car_") ||
                n.Contains("prototypehud") ||
                n.Contains("premiumhud") ||
                n.Contains("scenepreview") ||
                n.Contains("testjob") ||
                n == "ground" ||
                n == "environment";
        }

        private static bool IsVisiblePrototypeObject(GameObject go)
        {
            string n = go.name.ToLowerInvariant();
            return
                n == "road" ||
                n.StartsWith("streetmodule_") ||
                n.StartsWith("lanemark") ||
                n.Contains("sidewalk") ||
                n.Contains("curb") ||
                n.Contains("streetlamp") ||
                n.Contains("bench") ||
                n.Contains("trashcan") ||
                n.Contains("building") ||
                n.Contains("gasstation") ||
                n.Contains("shop") ||
                n.Contains("store") ||
                n.Contains("house") ||
                n.Contains("canopy") ||
                n.Contains("pump");
        }

        private static void RemoveGeneratedChildren(Transform parent, ref int removed)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                // Never delete a branch that contains a light.
                if (child.GetComponent<Light>() != null || child.GetComponentsInChildren<Light>(true).Length > 0)
                    continue;

                GameObject go = child.gameObject;
                if (IsGeneratedPrototypeRoot(go) || IsVisiblePrototypeObject(go))
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                    continue;
                }

                RemoveGeneratedChildren(child, ref removed);
            }
        }
    }
}
