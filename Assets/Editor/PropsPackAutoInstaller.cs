using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class PropsPackAutoInstaller
    {
        private const string Root = "Assets/Props";
        private const string ReadyFolder = Root + "/READY - DRAG INTO SCENE";
        private const string MarkerPath = Root + "/.props-import-v2-complete.txt";

        private sealed class Pack
        {
            public string Name;
            public string Folder;
            public string ModelPath;
            public bool SplitChildren;
        }

        private static readonly Pack[] Packs =
        {
            new()
            {
                Name = "Street Asset Pack 01",
                Folder = Root + "/Street Asset Pack 01",
                ModelPath = Root + "/Street Asset Pack 01/Models/Street_Scene.fbx",
                SplitChildren = true
            },
            new()
            {
                Name = "Plastic Toilet Cabin",
                Folder = Root + "/Plastic Toilet Cabin",
                ModelPath = Root + "/Plastic Toilet Cabin/Models/Plastic Toilet Cabin LP.fbx",
                SplitChildren = false
            },
            new()
            {
                Name = "Clothes Donation Box",
                Folder = Root + "/Clothes Donation Box",
                ModelPath = Root + "/Clothes Donation Box/Source/Kleiderspende.obj",
                SplitChildren = false
            },
            new()
            {
                Name = "City Props Collection Volume 1",
                Folder = Root + "/City Props Collection Volume 1",
                ModelPath = Root + "/City Props Collection Volume 1/Source/CityPropsCollection.fbx",
                SplitChildren = true
            },
            new()
            {
                Name = "Supermarket Checkout",
                Folder = Root + "/Supermarket Checkout",
                ModelPath = Root + "/Supermarket Checkout/Models/supermarket_checkout.glb",
                SplitChildren = false
            },
            new()
            {
                Name = "Multi Supermarket Asset Pack Vol 1",
                Folder = Root + "/Multi Supermarket Asset Pack Vol 1",
                ModelPath = Root + "/Multi Supermarket Asset Pack Vol 1/Models/supermarket_assets.fbx",
                SplitChildren = true
            }
        };

        static PropsPackAutoInstaller()
        {
            EditorApplication.delayCall += TryAutomaticBuild;
        }

        [MenuItem("Tools/CYDOY/Props/Rebuild All Ready Props With Hitboxes")]
        private static void RebuildFromMenu()
        {
            if (File.Exists(MarkerPath))
                File.Delete(MarkerPath);

            AssetDatabase.Refresh();
            BuildAll(true);
        }

        // Used by the isolated verification/import pass. Keeping this public also
        // makes the importer deterministic for automated project setup.
        public static void BuildAllBatch()
        {
            if (File.Exists(MarkerPath))
                return;

            ConfigureNormalMaps();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildAll(false);
            AssetDatabase.SaveAssets();
        }

        private static void TryAutomaticBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutomaticBuild;
                return;
            }

            if (File.Exists(MarkerPath))
                return;

            foreach (Pack pack in Packs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pack.ModelPath) == null)
                {
                    EditorApplication.delayCall += TryAutomaticBuild;
                    return;
                }
            }

            if (ConfigureNormalMaps())
            {
                EditorApplication.delayCall += TryAutomaticBuild;
                return;
            }

            BuildAll(false);
        }

        private static bool ConfigureNormalMaps()
        {
            bool changed = false;
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });

            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!filename.Contains("normal") && !filename.Contains("_norma"))
                    continue;

                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    changed = true;
                }
            }

            return changed;
        }

        private static void BuildAll(bool manual)
        {
            Directory.CreateDirectory(ReadyFolder);

            int prefabCount = 0;
            int colliderCount = 0;
            foreach (Pack pack in Packs)
                prefabCount += BuildPack(pack, ref colliderCount);

            // Material properties are changed after CreateAsset. Save them before
            // the following refresh, otherwise Unity reloads the just-created
            // material files with their default (untextured) values.
            AssetDatabase.SaveAssets();

            File.WriteAllText(
                MarkerPath,
                $"Generated {prefabCount} ready-to-place prefabs with {colliderCount} fitted hitboxes on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.\n");
            AssetDatabase.Refresh();

            Debug.Log($"[CYDOY PROPS] READY: {prefabCount} draggable prefabs and {colliderCount} fitted hitboxes were created in '{ReadyFolder}'.");

            if (manual)
                EditorUtility.DisplayDialog("Props ready", $"{prefabCount} prefabs with {colliderCount} hitboxes are ready in:\n{ReadyFolder}", "OK");
        }

        private static int BuildPack(Pack pack, ref int colliderCount)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pack.ModelPath);
            if (modelAsset == null)
                return 0;

            string materialFolder = pack.Folder + "/Generated Materials";
            string prefabFolder = ReadyFolder + "/" + SanitizeName(pack.Name);
            Directory.CreateDirectory(materialFolder);
            Directory.CreateDirectory(prefabFolder);
            AssetDatabase.Refresh();

            List<Texture2D> textures = LoadTextures(pack.Folder);
            Dictionary<string, Material> materialCache = new(StringComparer.OrdinalIgnoreCase);
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            int created = 0;

            try
            {
                GameObject fullInstance = PrefabUtility.InstantiatePrefab(modelAsset, previewScene) as GameObject;
                if (fullInstance == null)
                    return 0;

                fullInstance.name = pack.Name + " - Full Set";
                ReplaceMaterials(fullInstance, pack, textures, materialCache);
                colliderCount += AddFittedHitboxes(fullInstance);
                string fullPath = prefabFolder + "/" + SanitizeName(fullInstance.name) + ".prefab";
                SaveReadyPrefab(fullInstance, fullPath, pack.Name);
                created++;

                if (pack.SplitChildren)
                {
                    List<Transform> candidates = FindPropRoots(fullInstance.transform);
                    Dictionary<string, int> usedNames = new(StringComparer.OrdinalIgnoreCase);

                    foreach (Transform candidate in candidates)
                    {
                        if (candidate.GetComponentsInChildren<Renderer>(true).Length == 0)
                            continue;

                        string cleanName = SanitizeName(candidate.name);
                        if (string.IsNullOrWhiteSpace(cleanName))
                            cleanName = "Prop";

                        if (!usedNames.TryAdd(cleanName, 1))
                        {
                            usedNames[cleanName]++;
                            cleanName += " " + usedNames[cleanName];
                        }

                        GameObject wrapper = new(cleanName);
                        SceneManager.MoveGameObjectToScene(wrapper, previewScene);
                        GameObject clone = UnityEngine.Object.Instantiate(candidate.gameObject, wrapper.transform);
                        clone.name = "Model";
                        clone.transform.localPosition = Vector3.zero;

                        colliderCount += AddFittedHitboxes(wrapper);
                        string propPath = prefabFolder + "/" + cleanName + ".prefab";
                        SaveReadyPrefab(wrapper, propPath, pack.Name);
                        UnityEngine.Object.DestroyImmediate(wrapper);
                        created++;
                    }
                }

                UnityEngine.Object.DestroyImmediate(fullInstance);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            return created;
        }

        private static List<Transform> FindPropRoots(Transform modelRoot)
        {
            List<Transform> candidates = new();
            for (int i = 0; i < modelRoot.childCount; i++)
                candidates.Add(modelRoot.GetChild(i));

            while (candidates.Count == 1 &&
                   candidates[0].GetComponent<Renderer>() == null &&
                   candidates[0].childCount > 1)
            {
                Transform container = candidates[0];
                candidates.Clear();
                for (int i = 0; i < container.childCount; i++)
                    candidates.Add(container.GetChild(i));
            }

            return candidates;
        }

        private static int AddFittedHitboxes(GameObject root)
        {
            int count = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.GetComponent<Collider>() != null)
                    continue;

                Bounds localBounds = renderer.localBounds;
                if (localBounds.size.sqrMagnitude < 0.000001f)
                    continue;

                BoxCollider box = renderer.gameObject.AddComponent<BoxCollider>();
                box.center = localBounds.center;
                box.size = localBounds.size;
                count++;
            }

            return count;
        }

        private static void ReplaceMaterials(
            GameObject root,
            Pack pack,
            List<Texture2D> textures,
            Dictionary<string, Material> cache)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] replacements = renderer.sharedMaterials;
                for (int i = 0; i < replacements.Length; i++)
                {
                    Material original = replacements[i];
                    if (original == null)
                        continue;

                    if (!cache.TryGetValue(original.name, out Material replacement))
                    {
                        replacement = CreateOrUpdateMaterial(pack, original, textures);
                        cache[original.name] = replacement;
                    }

                    replacements[i] = replacement;
                }

                renderer.sharedMaterials = replacements;
            }
        }

        private static Material CreateOrUpdateMaterial(Pack pack, Material original, List<Texture2D> textures)
        {
            string folder = pack.Folder + "/Generated Materials";
            string path = folder + "/" + SanitizeName(original.name) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                material = new Material(shader) { name = original.name };
                AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
            }

            material.color = original.HasProperty("_Color") ? original.color : Color.white;
            material.SetFloat("_Glossiness", 0.32f);

            Texture2D albedo = FindTexture(textures, original.name, "basecolor", "basec", "diffuse");
            Texture2D normal = FindTexture(textures, original.name, "normal", "norma");
            Texture2D metallic = FindTexture(textures, original.name, "metallic", "metal");
            Texture2D occlusion = FindTexture(textures, original.name, "ambientocclusion", "occlusion", "_ao");
            Texture2D emission = FindTexture(textures, original.name, "emissive", "emission");

            if (albedo != null)
            {
                material.mainTexture = albedo;
                material.color = Color.white;
            }
            else if (original.mainTexture != null)
            {
                material.mainTexture = original.mainTexture;
            }

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_Metallic", 0.6f);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                string lowerName = original.name.ToLowerInvariant();
                material.SetFloat("_Metallic", lowerName.Contains("metal") || lowerName.Contains("steel") ? 0.65f : 0f);
            }

            if (occlusion != null)
            {
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", 1f);
            }

            if (emission != null && emission.width > 4 && emission.height > 4)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static List<Texture2D> LoadTextures(string folder)
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null)
                .ToList();
        }

        private static Texture2D FindTexture(List<Texture2D> textures, string materialName, params string[] roles)
        {
            string materialKey = Normalize(materialName);
            Texture2D best = null;
            int bestScore = 0;

            foreach (Texture2D texture in textures)
            {
                string filename = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(texture)).ToLowerInvariant();
                bool roleMatches = roles.Any(filename.Contains);
                bool albedoSearch = roles.Contains("basecolor") || roles.Contains("diffuse");
                bool looksLikeOtherMap = filename.Contains("normal") || filename.Contains("_norma") ||
                                         filename.Contains("metal") || filename.Contains("rough") ||
                                         filename.Contains("occlusion") || filename.EndsWith("_ao") ||
                                         filename.Contains("emiss") || filename.Contains("height");

                // Some OBJ packs provide one plain JPG without a BaseColor suffix.
                // It is still a valid albedo when its name matches the material.
                if (!roleMatches && (!albedoSearch || looksLikeOtherMap))
                    continue;

                string candidateKey = Normalize(filename);
                int score = 0;
                if (materialKey.Length >= 3 && candidateKey.Contains(materialKey))
                    score += 100 + materialKey.Length;

                string[] materialTokens = Tokenize(materialName);
                string[] candidateTokens = Tokenize(filename);
                score += materialTokens.Count(token => candidateTokens.Contains(token)) * 12;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = texture;
                }
            }

            return bestScore >= 12 ? best : null;
        }

        private static string[] Tokenize(string value)
        {
            string[] ignored = { "mat", "material", "shader", "lp", "basecolor", "basec", "diffuse", "normal", "metallic", "metal", "roughness", "ambientocclusion", "occlusion", "emissive", "emission", "ao" };
            return value.ToLowerInvariant()
                .Split(new[] { ' ', '_', '-', '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Length >= 3 && !ignored.Contains(token))
                .Distinct()
                .ToArray();
        }

        private static string Normalize(string value)
        {
            string normalized = new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            string[] remove = { "material", "shader", "basecolor", "diffuse", "normaldirectx", "normal", "metallic", "roughness", "ambientocclusion", "occlusion", "emissive", "emission" };
            foreach (string word in remove)
                normalized = normalized.Replace(word, string.Empty);
            return normalized;
        }

        private static void SaveReadyPrefab(GameObject instance, string path, string packName)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            if (prefab != null)
                AssetDatabase.SetLabels(prefab, new[] { "Prop", "ReadyToPlace", packName });
        }

        private static string SanitizeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Trim().Replace("  ", " ");
        }
    }
}
