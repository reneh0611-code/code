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

        [MenuItem("Tools/CYDOY/Remove AJ Backpack")]
        public static void RemoveBackpack()
        {
            EnsureReadableSource();
            EnsureGeneratedFolder();

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Player.prefab not found.", "OK");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int hiddenBones = 0;
            int modifiedRenderers = 0;
            int removedTriangles = 0;

            try
            {
                Transform aj = FindRecursive(root.transform, "Mixamo_AJ");
                if (aj == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Mixamo_AJ was not found inside Player.prefab.", "OK");
                    return;
                }

                foreach (Transform t in aj.GetComponentsInChildren<Transform>(true))
                {
                    if (t == aj)
                        continue;

                    if (IsBackpackToken(t.name))
                    {
                        t.gameObject.SetActive(false);
                        hiddenBones++;
                    }
                }

                foreach (SkinnedMeshRenderer renderer in aj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null)
                        continue;

                    Mesh source = renderer.sharedMesh;
                    if (!source.isReadable)
                    {
                        Debug.LogWarning($"[CYDOY] Mesh '{source.name}' is not readable; skipped backpack surgery.");
                        continue;
                    }

                    HashSet<int> backpackBoneIndices = GetBackpackBoneIndices(renderer);
                    if (backpackBoneIndices.Count == 0)
                        continue;

                    BoneWeight[] weights = source.boneWeights;
                    if (weights == null || weights.Length != source.vertexCount)
                        continue;

                    Mesh cleaned = Object.Instantiate(source);
                    cleaned.name = source.name + "_NoBackpack";

                    int removedFromRenderer = 0;
                    for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                    {
                        int[] triangles = source.GetTriangles(subMesh);
                        List<int> kept = new(triangles.Length);

                        for (int i = 0; i + 2 < triangles.Length; i += 3)
                        {
                            int a = triangles[i];
                            int b = triangles[i + 1];
                            int c = triangles[i + 2];

                            float wa = BackpackInfluence(weights[a], backpackBoneIndices);
                            float wb = BackpackInfluence(weights[b], backpackBoneIndices);
                            float wc = BackpackInfluence(weights[c], backpackBoneIndices);

                            int stronglyWeightedVertices = 0;
                            if (wa >= 0.20f) stronglyWeightedVertices++;
                            if (wb >= 0.20f) stronglyWeightedVertices++;
                            if (wc >= 0.20f) stronglyWeightedVertices++;

                            float average = (wa + wb + wc) / 3f;
                            bool backpackTriangle = stronglyWeightedVertices >= 2 || average >= 0.30f;

                            if (backpackTriangle)
                            {
                                removedFromRenderer++;
                                continue;
                            }

                            kept.Add(a);
                            kept.Add(b);
                            kept.Add(c);
                        }

                        cleaned.SetTriangles(kept, subMesh, false);
                    }

                    if (removedFromRenderer == 0)
                    {
                        Object.DestroyImmediate(cleaned);
                        continue;
                    }

                    cleaned.RecalculateBounds();

                    string safeRenderer = Sanitize(renderer.name);
                    string assetPath = $"{GeneratedFolder}/AJ_NoBackpack_{safeRenderer}.asset";
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(cleaned, assetPath);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                    Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    renderer.sharedMesh = saved;
                    EditorUtility.SetDirty(renderer);

                    modifiedRenderers++;
                    removedTriangles += removedFromRenderer;

                    Debug.Log($"[CYDOY] Backpack geometry removed from renderer '{renderer.name}'. Triangles removed: {removedFromRenderer}. Generated mesh: {assetPath}");
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
                $"Backpack mesh cleanup complete.\n\nBackpack bones hidden: {hiddenBones}\nMeshes modified: {modifiedRenderers}\nBackpack triangles removed: {removedTriangles}\n\nThe original Aj.fbx was not destructively changed. The Player prefab now points to generated no-backpack mesh copies.",
                "OK");
        }

        private static void EnsureReadableSource()
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

        private static HashSet<int> GetBackpackBoneIndices(SkinnedMeshRenderer renderer)
        {
            HashSet<int> result = new();
            Transform[] bones = renderer.bones;
            if (bones == null)
                return result;

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone != null && IsBackpackToken(bone.name))
                    result.Add(i);
            }

            return result;
        }

        private static float BackpackInfluence(BoneWeight weight, HashSet<int> backpackIndices)
        {
            float total = 0f;
            if (backpackIndices.Contains(weight.boneIndex0)) total += weight.weight0;
            if (backpackIndices.Contains(weight.boneIndex1)) total += weight.weight1;
            if (backpackIndices.Contains(weight.boneIndex2)) total += weight.weight2;
            if (backpackIndices.Contains(weight.boneIndex3)) total += weight.weight3;
            return total;
        }

        private static bool IsBackpackToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string n = value.ToLowerInvariant();
            return n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") ||
                   n.Contains("shoulderbag") || n.Contains("back bag") || n == "bag";
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

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
