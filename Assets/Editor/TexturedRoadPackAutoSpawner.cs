using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CheatOnYourDayOnes.World;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class TexturedRoadPackAutoSpawner
    {
        private const string SourcePath = "Assets/Materials/modular-kit-city-builder-starter-kit/source/FORMATS/modular kit city build v2.fbx";
        private const string TextureFolder = "Assets/Materials/modular-kit-city-builder-starter-kit/textures";
        private const string OutputFolder = "Assets/Environment/Roads/TexturedModularCityPack";
        private const string MaterialPath = OutputFolder + "/ModularCityPack_URP.mat";
        private const string PrefabPath = OutputFolder + "/TexturedRoadPack.prefab";
        private const string SceneRootName = "TexturedRoadPack_Spawned";
        private const string AutomaticRunKey = "CYDOY.TexturedRoadPackAutoSpawner.v3";

        private static bool _running;

        static TexturedRoadPackAutoSpawner()
        {
            EditorApplication.delayCall += TryAutomaticSpawn;
        }

        private static void TryAutomaticSpawn()
        {
            if (_running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticSpawn;
                return;
            }
            if (EditorPrefs.GetBool(AutomaticRunKey, false)) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) == null)
            {
                EditorApplication.delayCall += TryAutomaticSpawn;
                return;
            }

            BuildAndSpawn();
            EditorPrefs.SetBool(AutomaticRunKey, true);
        }

        [MenuItem("Tools/CYDOY/Road Pack/Spawn New Textured Road Pack")]
        public static void BuildAndSpawn()
        {
            if (_running) return;
            _running = true;
            try
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
                if (source == null)
                {
                    Debug.LogWarning("[CYDOY ROAD PACK] The modular city FBX is not imported yet.");
                    return;
                }

                EnsureFolder(OutputFolder);
                ConfigureTextureImport(TextureFolder + "/low_modular_kit_Normal.png", true, false);
                ConfigureTextureImport(TextureFolder + "/low_modular_kit_Metallic.png", false, true);
                ConfigureTextureImport(TextureFolder + "/low_modular_kit_Roughness.png", false, true);

                Material material = BuildMaterial();
                GameObject prefab = BuildPrefab(source, material);
                SpawnInActiveScene(prefab);
                AssetDatabase.SaveAssets();

                Debug.Log("[CYDOY ROAD PACK] READY: the new modular road/city pack was spawned with its color, normal and surface textures.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[CYDOY ROAD PACK] Could not build the textured pack.\n" + exception);
            }
            finally
            {
                _running = false;
            }
        }

        [MenuItem("Tools/CYDOY/Road Pack/Fix Module Pivots")]
        public static void FixModulePivots()
        {
            BuildAndSpawn();
        }

        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("CYDOY/Modular City Pack Lit") ??
                            Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "ModularCityPack_URP" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/modularkit.png");
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/low_modular_kit_Normal.png");
            Texture2D metallicMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/low_modular_kit_Metallic.png");
            Texture2D roughnessMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/low_modular_kit_Roughness.png");

            SetTexture(material, "_BaseMap", "_MainTex", baseMap);
            SetTexture(material, "_BumpMap", "_BumpMap", normalMap);
            SetTexture(material, "_MetallicMap", "_MetallicGlossMap", metallicMap);
            SetTexture(material, "_RoughnessMap", "_OcclusionMap", roughnessMap);

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPrefab(GameObject source, Material material)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            instance.name = "TexturedRoadPack";

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
                    UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach (Light light in instance.GetComponentsInChildren<Light>(true))
                    UnityEngine.Object.DestroyImmediate(light.gameObject);

                CenterModulePivots(instance.transform);

                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    Material[] materials = new Material[count];
                    for (int i = 0; i < count; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                    MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }

                return PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void SpawnInActiveScene(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || prefab == null) return;

            GameObject previous = GameObject.Find(SceneRootName);
            if (previous != null)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(previous))
                {
                    Undo.RegisterFullObjectHierarchyUndo(previous, "Fix road pack module pivots");
                    CenterModulePivots(previous.transform);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!string.IsNullOrWhiteSpace(scene.path)) EditorSceneManager.SaveScene(scene);
                Selection.activeGameObject = previous;
                EditorGUIUtility.PingObject(previous);
                return;
            }

            GameObject spawned = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (spawned == null) spawned = UnityEngine.Object.Instantiate(prefab);
            spawned.name = SceneRootName;
            Undo.RegisterCreatedObjectUndo(spawned, "Spawn textured road pack");

            Bounds bounds = CalculateBounds(spawned);
            Vector3 targetCenter = Vector3.zero;
            float groundY = FindTerrainHeight(targetCenter);
            Vector3 shift = new(targetCenter.x - bounds.center.x, groundY + .025f - bounds.min.y, targetCenter.z - bounds.center.z);
            spawned.transform.position += shift;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path)) EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = spawned;
            EditorGUIUtility.PingObject(spawned);
        }

        private static void CenterModulePivots(Transform packRoot)
        {
            Transform container = packRoot;
            while (container.childCount == 1 && container.GetComponent<Renderer>() == null)
                container = container.GetChild(0);

            List<Transform> modules = new();
            foreach (Transform child in container)
            {
                if (child.name.StartsWith("PIVOT_", StringComparison.Ordinal)) continue;
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0) modules.Add(child);
            }

            // Some exporters add collection wrappers. Descend one additional level when the
            // first level is clearly not a useful set of independently placeable modules.
            if (modules.Count <= 1)
            {
                modules.Clear();
                foreach (Transform branch in container)
                {
                    foreach (Transform child in branch)
                    {
                        if (child.name.StartsWith("PIVOT_", StringComparison.Ordinal)) continue;
                        if (child.GetComponentsInChildren<Renderer>(true).Length > 0) modules.Add(child);
                    }
                }
            }

            int index = 0;
            foreach (Transform module in modules)
            {
                Renderer[] renderers = module.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                GameObject pivotObject = new($"PIVOT_{index:000}_{Sanitize(module.name)}");
                pivotObject.AddComponent<RoadModulePivot>();
                Transform pivot = pivotObject.transform;
                pivot.SetParent(module.parent, false);
                pivot.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                pivot.rotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
                module.SetParent(pivot, true);
                index++;
            }

            Debug.Log($"[CYDOY ROAD PACK] Created centered local pivots for {index} independently rotatable modules.");
        }

        private static float FindTerrainHeight(Vector3 worldPosition)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                TerrainData data = terrain.terrainData;
                Vector3 origin = terrain.transform.position;
                Vector3 size = data.size;
                if (worldPosition.x < origin.x || worldPosition.z < origin.z ||
                    worldPosition.x > origin.x + size.x || worldPosition.z > origin.z + size.z) continue;
                return origin.y + terrain.SampleHeight(worldPosition);
            }
            return 0f;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void ConfigureTextureImport(string path, bool normalMap, bool linear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            TextureImporterType wantedType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != wantedType) { importer.textureType = wantedType; changed = true; }
            bool wantedSrgb = !linear && !normalMap;
            if (importer.sRGBTexture != wantedSrgb) { importer.sRGBTexture = wantedSrgb; changed = true; }
            if (!changed) return;
            importer.SaveAndReimport();
        }

        private static void SetTexture(Material material, string urpProperty, string standardProperty, Texture texture)
        {
            if (texture == null) return;
            if (material.HasProperty(urpProperty)) material.SetTexture(urpProperty, texture);
            else if (material.HasProperty(standardProperty)) material.SetTexture(standardProperty, texture);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Module";
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }
    }
}
