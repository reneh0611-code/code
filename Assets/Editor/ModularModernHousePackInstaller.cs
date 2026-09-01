#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    /// <summary>
    /// Turns the source FBX from the Modular Modern House Pack into grounded,
    /// categorised drag-and-drop prefabs with project-ready materials.
    /// </summary>
    public static class ModularModernHousePackInstaller
    {
        private const string Root = "Assets/Models/Buildings/ModularModernHousePack";
        private const string SourcePath = Root + "/Source/Modular Modern House Pack.fbx";
        private const string MaterialRoot = Root + "/Materials";
        private const string PrefabRoot = Root + "/Prefabs";
        private const string ReadyPrefabPath =
            "Assets/_READY BUILDINGS - DRAG INTO SCENE/MODERN MODULAR HOUSE - COMPLETE.prefab";
        private const string SentinelPrefabPath = PrefabRoot + "/Walls/Wall 5x5.prefab";

        private const string ConcreteAlbedo = Root + "/Textures/Concrete/vlklbgd_4K_Albedo.jpg";
        private const string ConcreteNormal = Root + "/Textures/Concrete/vlklbgd_4K_Normal.jpg";
        private const string ConcreteAo = Root + "/Textures/Concrete/vlklbgd_4K_AO.jpg";
        private const string ConcreteRoughness = Root + "/Textures/Concrete/vlklbgd_4K_Roughness.jpg";
        private const string ConcreteDisplacement = Root + "/Textures/Concrete/vlklbgd_4K_Displacement.jpg";
        private const string MetalAlbedo = Root + "/Textures/BlackMetal/shrcaefc_4K_Albedo.jpg";
        private const string MetalNormal = Root + "/Textures/BlackMetal/shrcaefc_4K_Normal.jpg";
        private const string Metalness = Root + "/Textures/BlackMetal/shrcaefc_4K_Metalness.jpg";
        private const string MetalRoughness = Root + "/Textures/BlackMetal/shrcaefc_4K_Roughness.jpg";

        private static readonly Dictionary<string, Material> Materials =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool isInstalling;

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticSetup()
        {
            EditorApplication.delayCall += AutomaticSetup;
        }

        private static void AutomaticSetup()
        {
            if (isInstalling || !File.Exists(SourcePath)) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) == null)
            {
                AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SentinelPrefabPath) == null)
            {
                Install();
            }
        }

        [MenuItem("Tools/CYDOY/Buildings/Rebuild Modular Modern House Pack")]
        public static void Install()
        {
            if (isInstalling) return;
            isInstalling = true;

            try
            {
                EnsureFolders();
                ConfigureTextures();
                CreateMaterials();
                ConfigureModelAndMaterialRemaps();
                GeneratePrefabs();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[CYDOY BUILDINGS] Modular Modern House Pack is ready in {PrefabRoot}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                isInstalling = false;
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Models");
            EnsureFolder("Assets/Models/Buildings");
            EnsureFolder(Root);
            EnsureFolder(MaterialRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(PrefabRoot + "/Walls");
            EnsureFolder(PrefabRoot + "/Windows");
            EnsureFolder(PrefabRoot + "/Doors");
            EnsureFolder(PrefabRoot + "/Roofs");
            EnsureFolder(PrefabRoot + "/Props");
            EnsureFolder(PrefabRoot + "/Complete");
            EnsureFolder("Assets/_READY BUILDINGS - DRAG INTO SCENE");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(ConcreteAlbedo, false, true);
            ConfigureTexture(ConcreteNormal, true, false);
            ConfigureTexture(ConcreteAo, false, false);
            ConfigureTexture(ConcreteRoughness, false, false);
            ConfigureTexture(ConcreteDisplacement, false, false);
            ConfigureTexture(MetalAlbedo, false, true);
            ConfigureTexture(MetalNormal, true, false);
            ConfigureTexture(Metalness, false, false);
            ConfigureTexture(MetalRoughness, false, false);
        }

        private static void ConfigureTexture(string path, bool normalMap, bool sRgb)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            bool changed = false;
            TextureImporterType wantedType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            SetIfDifferent(ref changed, importer.textureType, wantedType, value => importer.textureType = value);
            SetIfDifferent(ref changed, importer.sRGBTexture, sRgb, value => importer.sRGBTexture = value);
            SetIfDifferent(ref changed, importer.wrapMode, TextureWrapMode.Repeat, value => importer.wrapMode = value);
            SetIfDifferent(ref changed, importer.mipmapEnabled, true, value => importer.mipmapEnabled = value);
            SetIfDifferent(ref changed, importer.maxTextureSize, 2048, value => importer.maxTextureSize = value);
            SetIfDifferent(
                ref changed,
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);

            if (changed) importer.SaveAndReimport();
        }

        private static void SetIfDifferent<T>(
            ref bool changed,
            T current,
            T wanted,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
            setter(wanted);
            changed = true;
        }

        private static void CreateMaterials()
        {
            Materials.Clear();

            Material concrete = GetOrCreateMaterial("Concrete");
            ResetOpaqueMaterial(concrete, new Color(.9f, .9f, .88f));
            SetTexture(concrete, "_MainTex", ConcreteAlbedo);
            SetTexture(concrete, "_BumpMap", ConcreteNormal);
            SetTexture(concrete, "_OcclusionMap", ConcreteAo);
            SetFloat(concrete, "_BumpScale", .75f);
            SetFloat(concrete, "_OcclusionStrength", .8f);
            SetFloat(concrete, "_Metallic", 0f);
            SetFloat(concrete, "_Glossiness", .16f);
            concrete.EnableKeyword("_NORMALMAP");
            Materials["Concrete"] = concrete;

            Material blackMetal = GetOrCreateMaterial("BlackMetal");
            ResetOpaqueMaterial(blackMetal, Color.white);
            SetTexture(blackMetal, "_MainTex", MetalAlbedo);
            SetTexture(blackMetal, "_BumpMap", MetalNormal);
            SetTexture(blackMetal, "_MetallicGlossMap", Metalness);
            SetFloat(blackMetal, "_BumpScale", .8f);
            SetFloat(blackMetal, "_Metallic", .9f);
            SetFloat(blackMetal, "_GlossMapScale", .32f);
            SetFloat(blackMetal, "_Glossiness", .32f);
            blackMetal.EnableKeyword("_NORMALMAP");
            blackMetal.EnableKeyword("_METALLICGLOSSMAP");
            Materials["BlackMetal"] = blackMetal;

            Material wall = GetOrCreateMaterial("Wall");
            ResetOpaqueMaterial(wall, new Color(.82f, .83f, .81f));
            SetFloat(wall, "_Metallic", 0f);
            SetFloat(wall, "_Glossiness", .2f);
            Materials["Wall"] = wall;

            Material glass = GetOrCreateMaterial("Glass");
            ConfigureTransparentMaterial(glass, new Color(.42f, .66f, .72f, .24f), .9f);
            Materials["Glass"] = glass;

            Material behindGlass = GetOrCreateMaterial("BehindGlass");
            ResetOpaqueMaterial(behindGlass, new Color(.07f, .085f, .09f));
            SetFloat(behindGlass, "_Glossiness", .42f);
            Materials["BehindGlass"] = behindGlass;

            Material black = GetOrCreateMaterial("Black");
            ResetOpaqueMaterial(black, new Color(.025f, .027f, .03f));
            SetFloat(black, "_Glossiness", .28f);
            Materials["Black"] = black;

            Material darkGrey = GetOrCreateMaterial("DarkGrey");
            ResetOpaqueMaterial(darkGrey, new Color(.18f, .19f, .2f));
            SetFloat(darkGrey, "_Glossiness", .24f);
            Materials["DarkGrey"] = darkGrey;

            Material steel = GetOrCreateMaterial("Steel");
            ResetOpaqueMaterial(steel, new Color(.48f, .5f, .52f));
            SetFloat(steel, "_Metallic", .8f);
            SetFloat(steel, "_Glossiness", .5f);
            Materials["Steel"] = steel;

            Material white = GetOrCreateMaterial("White");
            ResetOpaqueMaterial(white, new Color(.94f, .94f, .91f));
            SetFloat(white, "_Glossiness", .18f);
            Materials["White"] = white;

            Material grass = GetOrCreateMaterial("Grass");
            ResetOpaqueMaterial(grass, new Color(.14f, .32f, .08f));
            SetFloat(grass, "_Glossiness", .08f);
            Materials["Grass"] = grass;

            Material light = GetOrCreateMaterial("Light");
            ResetOpaqueMaterial(light, new Color(1f, .86f, .58f));
            SetColor(light, "_EmissionColor", new Color(2.2f, 1.45f, .55f));
            light.EnableKeyword("_EMISSION");
            Materials["Light"] = light;

            foreach (Material material in Materials.Values)
            {
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
            }
        }

        private static Material GetOrCreateMaterial(string name)
        {
            string path = $"{MaterialRoot}/MMH {name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("The Standard shader could not be found.");

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void ResetOpaqueMaterial(Material material, Color color)
        {
            material.SetOverrideTag("RenderType", "Opaque");
            SetFloat(material, "_Mode", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            SetColor(material, "_Color", color);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        private static void ConfigureTransparentMaterial(Material material, Color color, float smoothness)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloat(material, "_Mode", 3f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_Glossiness", smoothness);
            SetColor(material, "_Color", color);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ConfigureModelAndMaterialRemaps()
        {
            if (AssetImporter.GetAtPath(SourcePath) is not ModelImporter importer)
            {
                AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            }

            if (importer == null) throw new InvalidOperationException($"Could not import {SourcePath}");

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            // The original showcase FBX is large and Unity's automatic unwrap can
            // stall the editor for several minutes. Runtime prefabs use probes and
            // batching, so keep the authored UVs and skip that expensive re-unwrap.
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            // Keep the author's MainBuiding group so the assembled example can
            // also be exported as one ready-to-drag prefab.
            importer.preserveHierarchy = true;

            foreach ((string sourceName, Material material) in Materials)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
                importer.AddRemap(identifier, material);
            }

            importer.SaveAndReimport();
        }

        private static void GeneratePrefabs()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidOperationException($"No model found at {SourcePath}");

            MeshFilter[] sourceMeshes = source.GetComponentsInChildren<MeshFilter>(true);
            if (sourceMeshes.Length == 0) throw new InvalidOperationException("The house FBX contains no mesh objects.");

            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;

            foreach (MeshFilter sourceMeshFilter in sourceMeshes)
            {
                if (sourceMeshFilter.sharedMesh == null) continue;
                MeshRenderer sourceRenderer = sourceMeshFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer == null) continue;

                string originalName = sourceMeshFilter.gameObject.name;
                string friendlyName = FriendlyName(originalName);
                string category = CategoryFor(originalName);
                string path = UniquePrefabPath($"{PrefabRoot}/{category}/{friendlyName}.prefab", usedPaths);

                GameObject prefabRoot = BuildStandalonePart(
                    friendlyName,
                    originalName,
                    sourceMeshFilter,
                    sourceRenderer);

                try
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    created++;

                    if (category == "Complete" &&
                        originalName.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        prefabRoot.name = "MODERN MODULAR HOUSE - COMPLETE";
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, ReadyPrefabPath);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(prefabRoot);
                }
            }

            CreateCompleteBuildingPrefab(source);

            Debug.Log($"[CYDOY BUILDINGS] Generated {created} modular building prefabs.");
        }

        private static void CreateCompleteBuildingPrefab(GameObject source)
        {
            Transform completeSource = null;
            foreach (Transform candidate in source.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.IndexOf("MainBuiding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidate.name.IndexOf("MainBuilding", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    completeSource = candidate;
                    break;
                }
            }

            if (completeSource == null) return;

            GameObject complete = Object.Instantiate(completeSource.gameObject);
            complete.name = "MODERN MODULAR HOUSE - COMPLETE";
            complete.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                Renderer[] renderers = complete.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return;

                foreach (MeshFilter meshFilter in complete.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh == null) continue;
                    MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterials = ResolveMaterials(
                            renderer.sharedMaterials,
                            meshFilter.gameObject.name);
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }

                    if (meshFilter.GetComponent<Collider>() == null)
                    {
                        BoxCollider collider = meshFilter.gameObject.AddComponent<BoxCollider>();
                        collider.center = meshFilter.sharedMesh.bounds.center;
                        collider.size = meshFilter.sharedMesh.bounds.size;
                    }
                }

                Bounds bounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                complete.transform.position += Vector3.up * -bounds.min.y;

                SetStaticRecursively(complete);
                PrefabUtility.SaveAsPrefabAsset(
                    complete,
                    PrefabRoot + "/Complete/Modern Modular House - Complete.prefab");
                PrefabUtility.SaveAsPrefabAsset(complete, ReadyPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(complete);
            }
        }

        private static GameObject BuildStandalonePart(
            string friendlyName,
            string originalName,
            MeshFilter sourceMeshFilter,
            MeshRenderer sourceRenderer)
        {
            var root = new GameObject(friendlyName);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            // Use the accumulated source transform so standalone parts remain
            // correct even when they live below an imported collection/group.
            model.transform.localRotation = sourceMeshFilter.transform.rotation;
            model.transform.localScale = sourceMeshFilter.transform.lossyScale;

            MeshFilter meshFilter = model.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = ResolveMaterials(sourceRenderer.sharedMaterials, originalName);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

            // Every part rests exactly on y=0 when dragged into a scene, while its
            // original X/Z pivot stays intact for 2.5 m and 5 m modular snapping.
            float bottom = renderer.bounds.min.y;
            model.transform.position += Vector3.up * -bottom;

            BoxCollider collider = model.AddComponent<BoxCollider>();
            collider.center = sourceMeshFilter.sharedMesh.bounds.center;
            collider.size = sourceMeshFilter.sharedMesh.bounds.size;

            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(root, flags);
            GameObjectUtility.SetStaticEditorFlags(model, flags);

            return root;
        }

        private static Material[] ResolveMaterials(Material[] sourceMaterials, string objectName)
        {
            int count = Mathf.Max(1, sourceMaterials?.Length ?? 0);
            var result = new Material[count];

            for (int index = 0; index < count; index++)
            {
                Material sourceMaterial = sourceMaterials != null && index < sourceMaterials.Length
                    ? sourceMaterials[index]
                    : null;
                string sourceName = sourceMaterial != null ? sourceMaterial.name : string.Empty;
                result[index] = ResolveMaterial(sourceName, objectName);
            }

            return result;
        }

        private static Material ResolveMaterial(string sourceName, string objectName)
        {
            string cleanName = sourceName
                .Replace(" (Instance)", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("MMH", string.Empty);

            foreach ((string name, Material material) in Materials)
            {
                string cleanKnownName = name.Replace(" ", string.Empty);
                if (cleanName.Equals(cleanKnownName, StringComparison.OrdinalIgnoreCase)) return material;
            }

            if (objectName.StartsWith("Window", StringComparison.OrdinalIgnoreCase)) return Materials["Glass"];
            if (ContainsAny(objectName, "Railing", "Bike", "BlackSteel")) return Materials["BlackMetal"];
            if (ContainsAny(objectName, "Lamp")) return Materials["Steel"];
            if (ContainsAny(objectName, "Hedge")) return Materials["Grass"];
            if (ContainsAny(objectName, "Stone", "Balcon")) return Materials["Concrete"];
            if (ContainsAny(objectName, "Door")) return Materials["DarkGrey"];
            return Materials["Wall"];
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            foreach (string fragment in fragments)
            {
                if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string CategoryFor(string objectName)
        {
            if (objectName.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0) return "Complete";
            if (objectName.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0) return "Props";
            if (objectName.StartsWith("Window", StringComparison.OrdinalIgnoreCase)) return "Windows";
            if (objectName.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0) return "Doors";
            if (objectName.IndexOf("Roof", StringComparison.OrdinalIgnoreCase) >= 0) return "Roofs";
            if (objectName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0) return "Walls";
            return "Props";
        }

        private static string FriendlyName(string originalName)
        {
            string name = originalName
                .Replace("Buiding", "Building")
                .Replace("Balconny", "Balcony")
                .Replace("RIght", "Right")
                .Replace("slightly slopedRoof", "Slightly Sloped Roof")
                .Replace("slightly sloped Roof", "Slightly Sloped Roof")
                .Replace("heavily sloped roof", "Heavily Sloped Roof")
                .Replace("sloping roof", "Sloping Roof")
                .Replace("corner wall", "Corner Wall")
                .Trim();

            if (name.EndsWith(".001", StringComparison.Ordinal))
                name = name[..^4] + " 2";

            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '-');
            return name;
        }

        private static string UniquePrefabPath(string wantedPath, HashSet<string> usedPaths)
        {
            string path = wantedPath;
            string directory = Path.GetDirectoryName(wantedPath)?.Replace('\\', '/') ?? PrefabRoot;
            string filename = Path.GetFileNameWithoutExtension(wantedPath);
            int suffix = 2;

            while (!usedPaths.Add(path))
            {
                path = $"{directory}/{filename} {suffix}.prefab";
                suffix++;
            }

            return path;
        }

        private static void SetStaticRecursively(GameObject root)
        {
            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
        }

        private static void SetTexture(Material material, string property, string path)
        {
            if (!material.HasProperty(property)) return;
            material.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }
    }
}
#endif
