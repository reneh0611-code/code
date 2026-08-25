using System;
using System.Collections.Generic;
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
                EditorUtility.DisplayDialog("CYDOY · AJ Textures", "Aj.fbx was not found at:\n" + CharacterPath, "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Textures", "Player.prefab was not found at:\n" + PlayerPrefabPath, "OK");
                return;
            }

            EnsureFolders();

            int extractedTextures = ExtractEmbeddedTexturesSafely();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            List<Texture2D> availableTextures = LoadAvailableTextures();
            MaterialApplyResult result = ApplySafeMaterialsToPlayer(characterAsset, availableTextures);
            ApplyWalkSpeed();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · AJ Textures & Tuning",
                "Done.\n\n" +
                "Walk speed: " + WalkSpeed.ToString("0.0") + " m/s\n" +
                "Textures found: " + availableTextures.Count + "\n" +
                "New textures extracted: " + extractedTextures + "\n" +
                "Textured material slots: " + result.texturedSlots + "\n" +
                "Safe fallback slots: " + result.fallbackSlots +
                "\n\nWhite/textureless source materials are no longer copied onto AJ.\nAnimations, height and camera were not changed.",
                "Nice");
        }

        private static int ExtractEmbeddedTexturesSafely()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null)
                return 0;

            int before = LoadAvailableTextures().Count;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer != null)
            {
                try
                {
                    importer.ExtractTextures(TextureFolder);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CYDOY] AJ texture extraction was not available: " + ex.Message);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int after = LoadAvailableTextures().Count;
            return Mathf.Max(0, after - before);
        }

        private static MaterialApplyResult ApplySafeMaterialsToPlayer(GameObject characterAsset, List<Texture2D> textures)
        {
            MaterialApplyResult result = default;
            if (characterAsset == null)
                return result;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform targetAj = FindRecursive(root.transform, "Mixamo_AJ");
                if (targetAj == null)
                {
                    Debug.LogError("[CYDOY] Mixamo_AJ was not found inside Player.prefab.");
                    return result;
                }

                Renderer[] sourceRenderers = characterAsset.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer sourceRenderer in sourceRenderers)
                {
                    string relativePath = GetRelativePath(characterAsset.transform, sourceRenderer.transform);
                    Transform targetTransform = string.IsNullOrEmpty(relativePath) ? targetAj : targetAj.Find(relativePath);
                    if (targetTransform == null)
                        continue;

                    Renderer targetRenderer = targetTransform.GetComponent<Renderer>();
                    if (targetRenderer == null)
                        continue;

                    Material[] sourceMaterials = sourceRenderer.sharedMaterials;
                    int slotCount = Mathf.Max(1, sourceMaterials != null ? sourceMaterials.Length : 0);
                    Material[] finalMaterials = new Material[slotCount];

                    for (int i = 0; i < slotCount; i++)
                    {
                        Material source = sourceMaterials != null && i < sourceMaterials.Length ? sourceMaterials[i] : null;
                        string key = BuildMaterialKey(sourceRenderer.name, source != null ? source.name : string.Empty, i);

                        if (HasUsefulTexture(source))
                        {
                            finalMaterials[i] = source;
                            result.texturedSlots++;
                            continue;
                        }

                        Texture2D texture = FindBestTexture(textures, key);
                        if (texture != null)
                        {
                            finalMaterials[i] = GetOrCreateTexturedMaterial(key, texture);
                            result.texturedSlots++;
                        }
                        else
                        {
                            finalMaterials[i] = GetSafeFallbackMaterial(key);
                            result.fallbackSlots++;
                        }
                    }

                    targetRenderer.sharedMaterials = finalMaterials;
                    EditorUtility.SetDirty(targetRenderer);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return result;
        }

        private static Texture2D FindBestTexture(List<Texture2D> textures, string key)
        {
            if (textures == null || textures.Count == 0)
                return null;

            string normalizedKey = Normalize(key);
            Texture2D best = null;
            int bestScore = 0;

            foreach (Texture2D texture in textures)
            {
                if (texture == null)
                    continue;

                string name = Normalize(texture.name);
                int score = 0;

                string[] importantTokens = { "body", "skin", "head", "face", "shirt", "cloth", "clothes", "pants", "trouser", "shoe", "shoes", "sneaker", "hair", "diffuse", "albedo", "basecolor", "base" };
                foreach (string token in importantTokens)
                {
                    if (normalizedKey.Contains(token) && name.Contains(token))
                        score += 5;
                }

                string[] keyParts = normalizedKey.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in keyParts)
                {
                    if (part.Length >= 3 && name.Contains(part))
                        score++;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = texture;
                }
            }

            if (best != null && bestScore > 0)
                return best;

            // If the FBX contains exactly one usable texture, that is normally the character atlas.
            return textures.Count == 1 ? textures[0] : null;
        }

        private static Material GetOrCreateTexturedMaterial(string key, Texture2D texture)
        {
            string safeName = SanitizeFileName("AJ_" + key + "_Textured");
            string path = MaterialFolder + "/" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetSafeFallbackMaterial(string key)
        {
            string normalized = Normalize(key);

            if (normalized.Contains("skin") || normalized.Contains("body") || normalized.Contains("head") || normalized.Contains("face") || normalized.Contains("hand"))
                return GetOrCreateSolidMaterial("AJ_Skin_Safe", new Color(0.56f, 0.36f, 0.24f));

            if (normalized.Contains("shoe") || normalized.Contains("sneaker") || normalized.Contains("foot"))
                return GetOrCreateSolidMaterial("AJ_Shoes_Safe", new Color(0.035f, 0.04f, 0.05f));

            if (normalized.Contains("hair"))
                return GetOrCreateSolidMaterial("AJ_Hair_Safe", new Color(0.06f, 0.045f, 0.035f));

            return GetOrCreateSolidMaterial("AJ_Clothes_Safe", new Color(0.10f, 0.13f, 0.18f));
        }

        private static Material GetOrCreateSolidMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool HasUsefulTexture(Material material)
        {
            if (material == null)
                return false;

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                return true;
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
                return true;

            return false;
        }

        private static List<Texture2D> LoadAvailableTextures()
        {
            List<Texture2D> textures = new();
            if (!AssetDatabase.IsValidFolder(TextureFolder))
                return textures;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            foreach (string guid in guids)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (texture != null)
                    textures.Add(texture);
            }

            return textures;
        }

        private static void ApplyWalkSpeed()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                NetworkPlayerController controller = root.GetComponent<NetworkPlayerController>();
                if (controller == null)
                    return;

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

        private static string BuildMaterialKey(string rendererName, string materialName, int slot)
        {
            return rendererName + "_" + materialName + "_" + slot;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            value = value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return value;
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

        private struct MaterialApplyResult
        {
            public int texturedSlots;
            public int fallbackSlots;
        }
    }
}
