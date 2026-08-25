using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class AJTextureAndTuningUtility
    {
        private const string CharacterPath = "Assets/Models/Characters/Aj.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string CharacterFolder = "Assets/Models/Characters";
        private const string TextureFolder = CharacterFolder + "/Textures";
        private const string MaterialFolder = CharacterFolder + "/Materials";
        private const float WalkSpeed = 3.7f;

        [MenuItem("Tools/CYDOY/Apply AJ Textures & Tuning")]
        public static void Apply()
        {
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (characterAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Textures",
                    "Aj.fbx was not found at:\n" + CharacterPath,
                    "OK");
                return;
            }

            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Textures",
                    "Player.prefab was not found at:\n" + PlayerPrefabPath,
                    "OK");
                return;
            }

            EnsureFolders();
            int extractedTextures = ExtractEmbeddedTextures();
            int extractedMaterials = ExtractAndRemapMaterials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            int rendererCount = ApplySourceMaterialsToPlayer(characterAsset);
            ApplyWalkSpeed();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · AJ Textures & Tuning",
                "Done.\n\n" +
                "Walk speed: " + WalkSpeed.ToString("0.0") + " m/s\n" +
                "Extracted textures: " + extractedTextures + "\n" +
                "Extracted materials: " + extractedMaterials + "\n" +
                "AJ renderers updated: " + rendererCount +
                "\n\nAnimations, character height and camera were not changed.",
                "Nice");
        }

        private static int ExtractEmbeddedTextures()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null)
                return 0;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            int before = CountAssetsAtPath<Texture>(TextureFolder);

            importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer != null)
                importer.ExtractTextures(TextureFolder);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int after = CountAssetsAtPath<Texture>(TextureFolder);
            return Mathf.Max(0, after - before);
        }

        private static int ExtractAndRemapMaterials()
        {
            int extracted = 0;
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(CharacterPath);

            foreach (Object asset in subAssets)
            {
                if (asset is not Material material)
                    continue;

                string safeName = string.IsNullOrWhiteSpace(material.name) ? "AJ_Material" : material.name;
                string targetPath = MaterialFolder + "/" + safeName + ".mat";

                if (AssetDatabase.LoadAssetAtPath<Material>(targetPath) != null)
                    continue;

                string error = AssetDatabase.ExtractAsset(material, targetPath);
                if (string.IsNullOrEmpty(error))
                    extracted++;
                else
                    Debug.LogWarning("[CYDOY] Material extraction: " + error);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer != null)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnMaterialName,
                    ModelImporterMaterialSearch.Everywhere);
                importer.SaveAndReimport();
            }

            return extracted;
        }

        private static int ApplySourceMaterialsToPlayer(GameObject characterAsset)
        {
            if (characterAsset == null)
                return 0;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int updated = 0;

            try
            {
                Transform targetAj = FindRecursive(root.transform, "Mixamo_AJ");
                if (targetAj == null)
                {
                    Debug.LogError("[CYDOY] Mixamo_AJ was not found inside Player.prefab.");
                    return 0;
                }

                Renderer[] sourceRenderers = characterAsset.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer sourceRenderer in sourceRenderers)
                {
                    string relativePath = GetRelativePath(characterAsset.transform, sourceRenderer.transform);
                    Transform targetTransform = string.IsNullOrEmpty(relativePath)
                        ? targetAj
                        : targetAj.Find(relativePath);

                    if (targetTransform == null)
                        continue;

                    Renderer targetRenderer = targetTransform.GetComponent<Renderer>();
                    if (targetRenderer == null)
                        continue;

                    Material[] sourceMaterials = sourceRenderer.sharedMaterials;
                    if (sourceMaterials == null || sourceMaterials.Length == 0)
                        continue;

                    targetRenderer.sharedMaterials = sourceMaterials;
                    updated++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return updated;
        }

        private static void ApplyWalkSpeed()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                NetworkPlayerController controller = root.GetComponent<NetworkPlayerController>();
                if (controller == null)
                {
                    Debug.LogWarning("[CYDOY] NetworkPlayerController not found; walk speed was not changed on prefab.");
                    return;
                }

                SerializedObject so = new(controller);
                SerializedProperty walkSpeed = so.FindProperty("walkSpeed");
                if (walkSpeed != null)
                    walkSpeed.floatValue = WalkSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindRecursive(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (child == root)
                return string.Empty;

            string path = child.name;
            Transform current = child.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return current == root ? path : string.Empty;
        }

        private static int CountAssetsAtPath<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return 0;

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            return guids.Length;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(CharacterFolder))
                AssetDatabase.CreateFolder("Assets/Models", "Characters");
            if (!AssetDatabase.IsValidFolder(TextureFolder))
                AssetDatabase.CreateFolder(CharacterFolder, "Textures");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(CharacterFolder, "Materials");
        }
    }
}
