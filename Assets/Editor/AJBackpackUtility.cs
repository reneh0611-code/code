using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class AJBackpackUtility
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string GeneratedFolder = "Assets/Models/Characters/Generated";

        // Confirmed manually with the live NPC island tester:
        // Renderer 1 (zero-based index 0), displayed Island 12 (zero-based index 11)
        // is the backpack. Island 13 contains cap/hood geometry and must remain untouched.
        private const int BackpackRendererIndex = 0;
        private const int BackpackIslandDisplayNumber = 12;

        private sealed class Island
        {
            public int subMesh;
            public List<int> triangles = new();
            public HashSet<int> vertices = new();
            public Bounds localBounds;
        }

        [MenuItem("Tools/CYDOY/Remove AJ Backpack")]
        public static void RemoveBackpack()
        {
            EnsureGeneratedFolder();

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Player.prefab not found.", "OK");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform aj = FindRecursive(root.transform, "Mixamo_AJ");
                if (aj == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Mixamo_AJ was not found inside Player.prefab.", "OK");
                    return;
                }

                SkinnedMeshRenderer[] renderers = aj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length <= BackpackRendererIndex)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Renderer 1 no longer exists on Mixamo_AJ.", "OK");
                    return;
                }

                SkinnedMeshRenderer renderer = renderers[BackpackRendererIndex];
                Mesh source = renderer.sharedMesh;
                if (source == null || !source.isReadable)
                {
                    EditorUtility.DisplayDialog(
                        "CYDOY · AJ Backpack",
                        "Renderer 1 mesh is missing or not readable. Reimport Aj.fbx once so Read/Write is enabled.",
                        "OK");
                    return;
                }

                List<Island> islands = BuildIslands(source);
                int islandIndex = BackpackIslandDisplayNumber - 1;
                if (islandIndex < 0 || islandIndex >= islands.Count)
                {
                    EditorUtility.DisplayDialog(
                        "CYDOY · AJ Backpack",
                        $"Renderer 1 currently has only {islands.Count} mesh islands, so confirmed Island 12 could not be resolved.",
                        "OK");
                    return;
                }

                Island backpack = islands[islandIndex];
                Mesh cleaned = CreateMeshWithoutIsland(source, backpack);
                cleaned.name = source.name + "_NoBackpack_Exact";

                string safeRenderer = Sanitize(renderer.name);
                string assetPath = $"{GeneratedFolder}/AJ_NoBackpack_Exact_{safeRenderer}.asset";
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(cleaned, assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                renderer.sharedMesh = saved;
                EditorUtility.SetDirty(renderer);

                // These are only backpack helper bones. Hiding them is harmless and keeps
                // the hierarchy clean, but geometry removal above is what actually removes the bag.
                foreach (Transform t in aj.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack"))
                        t.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Backpack",
                    $"Exact backpack removed.\n\nRenderer: 1\nIsland removed: {BackpackIslandDisplayNumber}\nTriangles removed: {backpack.triangles.Count / 3}\n\nIsland 13 was not touched, so cap/hood geometry remains intact.\n\nRecreate the NPCs once so they clone this updated AJ.",
                    "Perfekt");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static List<Island> BuildIslands(Mesh mesh)
        {
            List<Island> islands = new();
            Vector3[] verts = mesh.vertices;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                int triCount = tris.Length / 3;
                Dictionary<int, List<int>> vertexToTriangles = new();

                for (int t = 0; t < triCount; t++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t * 3 + k];
                        if (!vertexToTriangles.TryGetValue(v, out List<int> list))
                        {
                            list = new List<int>();
                            vertexToTriangles[v] = list;
                        }
                        list.Add(t);
                    }
                }

                bool[] visited = new bool[triCount];
                Queue<int> queue = new();

                for (int start = 0; start < triCount; start++)
                {
                    if (visited[start])
                        continue;

                    Island island = new() { subMesh = sub };
                    visited[start] = true;
                    queue.Enqueue(start);

                    while (queue.Count > 0)
                    {
                        int tri = queue.Dequeue();
                        int a = tris[tri * 3];
                        int b = tris[tri * 3 + 1];
                        int c = tris[tri * 3 + 2];

                        island.triangles.Add(a);
                        island.triangles.Add(b);
                        island.triangles.Add(c);
                        island.vertices.Add(a);
                        island.vertices.Add(b);
                        island.vertices.Add(c);

                        int[] triVertices = { a, b, c };
                        foreach (int vertex in triVertices)
                        {
                            foreach (int neighbor in vertexToTriangles[vertex])
                            {
                                if (visited[neighbor])
                                    continue;
                                visited[neighbor] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    if (island.vertices.Count > 0)
                    {
                        int first = island.vertices.First();
                        Bounds bounds = new(verts[first], Vector3.zero);
                        foreach (int v in island.vertices)
                            bounds.Encapsulate(verts[v]);
                        island.localBounds = bounds;
                    }

                    islands.Add(island);
                }
            }

            // Must use the exact same ordering as the tester the user used.
            return islands.OrderByDescending(i => i.triangles.Count).ToList();
        }

        private static Mesh CreateMeshWithoutIsland(Mesh source, Island remove)
        {
            Mesh cleaned = UnityEngine.Object.Instantiate(source);

            HashSet<string> removeKeys = new();
            for (int i = 0; i < remove.triangles.Count; i += 3)
                removeKeys.Add(TriangleKey(remove.triangles[i], remove.triangles[i + 1], remove.triangles[i + 2]));

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                if (sub != remove.subMesh)
                {
                    cleaned.SetTriangles(tris, sub, false);
                    continue;
                }

                List<int> kept = new(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (removeKeys.Contains(TriangleKey(tris[i], tris[i + 1], tris[i + 2])))
                        continue;

                    kept.Add(tris[i]);
                    kept.Add(tris[i + 1]);
                    kept.Add(tris[i + 2]);
                }

                cleaned.SetTriangles(kept, sub, false);
            }

            cleaned.RecalculateBounds();
            return cleaned;
        }

        private static string TriangleKey(int a, int b, int c)
        {
            int[] values = { a, b, c };
            Array.Sort(values);
            return $"{values[0]}_{values[1]}_{values[2]}";
        }

        private static Transform FindRecursive(Transform root, string targetName)
        {
            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindRecursive(root.GetChild(i), targetName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Characters"))
                AssetDatabase.CreateFolder("Assets/Models", "Characters");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Models/Characters", "Generated");
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
