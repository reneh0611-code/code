using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class RoadPackCatalogBuilder
    {
        [MenuItem("Tools/CYDOY/Road Pack/Build road_modular Catalog")]
        public static void BuildCatalog()
        {
            string path = FindRoadPackPath();
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("CYDOY Road Pack", "road_modular.glb was not found anywhere inside Assets.\n\nMake sure the GLB is inside the Unity project and its import has finished.", "OK");
                return;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
            {
                EditorUtility.DisplayDialog("CYDOY Road Pack", "Unity found the GLB, but it is not currently imported as a GameObject.\n\nPath: " + path + "\n\nIf you use glTFast or another GLB importer, let its import finish and try again.", "OK");
                return;
            }

            GameObject old = GameObject.Find("RoadPack_Catalog");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new("RoadPack_Catalog");
            root.transform.position = new Vector3(0f, 0f, 22f);

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (temp == null) temp = Object.Instantiate(source);
            temp.name = "__RoadPack_Temp";
            temp.hideFlags = HideFlags.HideAndDontSave;

            List<Transform> modules = FindModuleRoots(temp.transform);
            if (modules.Count == 0)
            {
                Object.DestroyImmediate(temp);
                Object.DestroyImmediate(root);
                EditorUtility.DisplayDialog("CYDOY Road Pack", "The GLB was found but no renderer modules could be detected.", "OK");
                return;
            }

            modules = modules.OrderByDescending(m => GetWorldBounds(m).size.x * GetWorldBounds(m).size.z).ToList();

            const float spacingX = 18f;
            const float spacingZ = 18f;
            const int columns = 5;

            int built = 0;
            foreach (Transform module in modules)
            {
                Bounds sourceBounds = GetWorldBounds(module);
                if (sourceBounds.size.sqrMagnitude < .0001f) continue;

                GameObject holder = new($"ROAD_{built:00}_{Sanitize(module.name)}");
                holder.transform.SetParent(root.transform, false);
                int col = built % columns;
                int row = built / columns;
                holder.transform.localPosition = new Vector3(col * spacingX, 0f, row * spacingZ);

                GameObject clone = Object.Instantiate(module.gameObject, holder.transform, true);
                clone.name = "Visual";

                // Normalize the visual so every detected piece is centered on its catalog slot and rests on Y=0.
                Bounds b = GetWorldBounds(clone.transform);
                Vector3 delta = holder.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                clone.transform.position += delta;

                AddMeshColliders(clone.transform);
                AddCatalogLabel(holder.transform, built, module.name, b.size);

                Debug.Log($"[CYDOY ROAD PACK] #{built:00} name='{module.name}' size=({b.size.x:F2}, {b.size.y:F2}, {b.size.z:F2}) source='{GetHierarchyPath(module, temp.transform)}'", holder);
                built++;
            }

            Object.DestroyImmediate(temp);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog("CYDOY Road Pack", $"Found road_modular.glb and built a catalog with {built} detected modules.\n\nThe catalog is placed at Z = 22 m.\n\nRun the scene or inspect it in Scene view and send me a screenshot showing which module numbers you want for straight roads, curves and intersections.", "Done");
        }

        [MenuItem("Tools/CYDOY/Road Pack/Select road_modular Asset")]
        public static void SelectRoadPack()
        {
            string path = FindRoadPackPath();
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("CYDOY Road Pack", "road_modular.glb was not found inside Assets.", "OK");
                return;
            }
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[CYDOY ROAD PACK] road_modular asset path: " + path, asset);
        }

        private static string FindRoadPackPath()
        {
            string[] guids = AssetDatabase.FindAssets("road_modular");
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string lower = p.ToLowerInvariant();
                if (lower.EndsWith("road_modular.glb") || lower.EndsWith("road_modular.gltf")) return p;
            }

            foreach (string guid in AssetDatabase.FindAssets("road modular"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string lower = p.ToLowerInvariant();
                if (lower.EndsWith(".glb") || lower.EndsWith(".gltf") || lower.EndsWith(".fbx")) return p;
            }
            return null;
        }

        private static List<Transform> FindModuleRoots(Transform sourceRoot)
        {
            List<Transform> result = new();

            // Prefer top-level children that already represent complete modules.
            foreach (Transform child in sourceRoot)
            {
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                    result.Add(child);
            }

            if (result.Count > 1) return result;
            result.Clear();

            // Many Sketchfab GLBs add one wrapper node. In that case inspect the next level.
            Transform wrapper = sourceRoot.childCount == 1 ? sourceRoot.GetChild(0) : sourceRoot;
            foreach (Transform child in wrapper)
            {
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                    result.Add(child);
            }

            if (result.Count > 1) return result;
            result.Clear();

            // Last resort: each renderer-bearing transform becomes a catalog item.
            foreach (Renderer r in sourceRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform == sourceRoot) continue;
                if (!result.Contains(r.transform)) result.Add(r.transform);
            }
            return result;
        }

        private static Bounds GetWorldBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void AddMeshColliders(Transform root)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        private static void AddCatalogLabel(Transform parent, int index, string sourceName, Vector3 size)
        {
            GameObject label = new("CatalogLabel");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, .35f, -5.5f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"#{index:00}  {sourceName}\n{size.x:F1} x {size.z:F1} m";
            text.fontSize = 48;
            text.characterSize = .08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static string GetHierarchyPath(Transform t, Transform stop)
        {
            string path = t.name;
            Transform p = t.parent;
            while (p != null && p != stop)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
