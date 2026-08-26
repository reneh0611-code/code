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
        private const string CharacterPath = "Assets/Models/Characters/Aj.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string GeneratedFolder = "Assets/Models/Characters/Generated";

        // Confirmed manually on the ORIGINAL AJ mesh:
        // Renderer 1 (zero-based 0), displayed Island 12 (zero-based 11) = backpack body.
        // Island 13 contains cap/hood geometry and must never be removed.
        private const int BackpackRendererIndex = 0;
        private const int BackpackIslandDisplayNumber = 12;

        private sealed class Island
        {
            public int subMesh;
            public List<int> triangles = new();
            public HashSet<int> vertices = new();
        }

        [MenuItem("Tools/CYDOY/Remove AJ Backpack")]
        public static void RemoveBackpack()
        {
            EnsureGeneratedFolder();
            EnsureAjReadable();

            GameObject rawAj = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (rawAj == null || playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Aj.fbx or Player.prefab is missing.", "OK");
                return;
            }

            SkinnedMeshRenderer[] rawRenderers = rawAj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (rawRenderers.Length <= BackpackRendererIndex)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Renderer 1 was not found in the original Aj.fbx.", "OK");
                return;
            }

            SkinnedMeshRenderer rawRenderer = rawRenderers[BackpackRendererIndex];
            Mesh source = rawRenderer.sharedMesh;
            if (source == null || !source.isReadable)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "The original AJ mesh is not readable.", "OK");
                return;
            }

            List<Island> islands = BuildIslands(source);
            int islandIndex = BackpackIslandDisplayNumber - 1;
            if (islandIndex < 0 || islandIndex >= islands.Count)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Backpack",
                    $"Original Renderer 1 has {islands.Count} islands; confirmed Island 12 could not be resolved.",
                    "OK");
                return;
            }

            Island backpackBody = islands[islandIndex];
            HashSet<int> backpackBoneIndices = GetBackpackBoneIndices(rawRenderer);

            int bodyTrianglesRemoved;
            int leftoverTrianglesRemoved;
            Mesh cleaned = CreateCleanMesh(
                source,
                backpackBody,
                backpackBoneIndices,
                out bodyTrianglesRemoved,
                out leftoverTrianglesRemoved);

            cleaned.name = source.name + "_NoBackpack_Complete";

            string assetPath = $"{GeneratedFolder}/AJ_NoBackpack_Complete.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(cleaned, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform playerAj = FindRecursive(root.transform, "Mixamo_AJ");
                if (playerAj == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Mixamo_AJ was not found inside Player.prefab.", "OK");
                    return;
                }

                SkinnedMeshRenderer[] playerRenderers = playerAj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (playerRenderers.Length <= BackpackRendererIndex)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Renderer 1 was not found inside Player.prefab.", "OK");
                    return;
                }

                playerRenderers[BackpackRendererIndex].sharedMesh = savedMesh;
                playerRenderers[BackpackRendererIndex].enabled = true;
                EditorUtility.SetDirty(playerRenderers[BackpackRendererIndex]);

                // Helper bones are no longer needed once their geometry is removed.
                foreach (Transform t in playerAj.GetComponentsInChildren<Transform>(true))
                {
                    if (IsBackpackBoneName(t.name))
                        t.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · AJ Backpack",
                "Complete backpack cleanup finished.\n\n" +
                $"Backpack body triangles removed: {bodyTrianglesRemoved}\n" +
                $"Strap/patch triangles removed: {leftoverTrianglesRemoved}\n" +
                $"Backpack bones detected: {backpackBoneIndices.Count}\n\n" +
                "Island 13 was never touched. Cap/hood geometry remains intact.\n\n" +
                "Recreate the NPCs once so they clone the cleaned AJ.",
                "Perfekt");
        }

        private static Mesh CreateCleanMesh(
            Mesh source,
            Island backpackBody,
            HashSet<int> backpackBoneIndices,
            out int bodyTrianglesRemoved,
            out int leftoverTrianglesRemoved)
        {
            Mesh cleaned = UnityEngine.Object.Instantiate(source);
            BoneWeight[] weights = source.boneWeights;

            HashSet<string> backpackBodyKeys = new();
            for (int i = 0; i < backpackBody.triangles.Count; i += 3)
            {
                backpackBodyKeys.Add(TriangleKey(
                    backpackBody.triangles[i],
                    backpackBody.triangles[i + 1],
                    backpackBody.triangles[i + 2]));
            }

            bodyTrianglesRemoved = 0;
            leftoverTrianglesRemoved = 0;

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                List<int> kept = new(tris.Length);

                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i];
                    int b = tris[i + 1];
                    int c = tris[i + 2];

                    if (sub == backpackBody.subMesh && backpackBodyKeys.Contains(TriangleKey(a, b, c)))
                    {
                        bodyTrianglesRemoved++;
                        continue;
                    }

                    if (weights != null && weights.Length == source.vertexCount && backpackBoneIndices.Count > 0)
                    {
                        float wa = BackpackInfluence(weights[a], backpackBoneIndices);
                        float wb = BackpackInfluence(weights[b], backpackBoneIndices);
                        float wc = BackpackInfluence(weights[c], backpackBoneIndices);

                        int influencedVertices = 0;
                        if (wa >= 0.025f) influencedVertices++;
                        if (wb >= 0.025f) influencedVertices++;
                        if (wc >= 0.025f) influencedVertices++;

                        float average = (wa + wb + wc) / 3f;

                        // Dedicated straps/patches are normally strongly tied to the custom backpack bones.
                        // Require either two influenced vertices or a meaningful average, which protects
                        // ordinary torso/clothing geometry from tiny incidental weights.
                        bool backpackLeftover = influencedVertices >= 2 || average >= 0.08f;
                        if (backpackLeftover)
                        {
                            leftoverTrianglesRemoved++;
                            continue;
                        }
                    }

                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }

                cleaned.SetTriangles(kept, sub, false);
            }

            cleaned.RecalculateBounds();
            return cleaned;
        }

        private static HashSet<int> GetBackpackBoneIndices(SkinnedMeshRenderer renderer)
        {
            HashSet<int> indices = new();
            Transform[] bones = renderer.bones;
            if (bones == null)
                return indices;

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone != null && IsBackpackBoneName(bone.name))
                    indices.Add(i);
            }

            return indices;
        }

        private static float BackpackInfluence(BoneWeight weight, HashSet<int> indices)
        {
            float total = 0f;
            if (indices.Contains(weight.boneIndex0)) total += weight.weight0;
            if (indices.Contains(weight.boneIndex1)) total += weight.weight1;
            if (indices.Contains(weight.boneIndex2)) total += weight.weight2;
            if (indices.Contains(weight.boneIndex3)) total += weight.weight3;
            return total;
        }

        private static bool IsBackpackBoneName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string n = value.ToLowerInvariant();
            return n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack");
        }

        private static List<Island> BuildIslands(Mesh mesh)
        {
            List<Island> islands = new();

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

                        int[] vertices = { a, b, c };
                        foreach (int vertex in vertices)
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

                    islands.Add(island);
                }
            }

            // Same ordering used by the live island tester that identified Island 12.
            return islands.OrderByDescending(i => i.triangles.Count).ToList();
        }

        private static void EnsureAjReadable()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null)
                return;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
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
    }
}
