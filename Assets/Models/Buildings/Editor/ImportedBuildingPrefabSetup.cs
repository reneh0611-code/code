#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts the two downloaded building models into grounded, material-safe prefabs.
/// It runs once after Unity has imported the source assets and can also be run manually.
/// </summary>
public static class ImportedBuildingPrefabSetup
{
    private static int retryCount;
    private const string GasModel = "Assets/Models/Buildings/SmallRedGasStation/Source/stacja.fbx";
    private const string GasRoot = "Assets/Models/Buildings/SmallRedGasStation";
    private const string GasPrefab = GasRoot + "/Prefabs/Small Red Gas Station.prefab";
    private const string StoreModel = "Assets/Models/Buildings/ConvenienceStore/Source/8 16 20 conveniance_store.glb";
    private const string StoreRoot = "Assets/Models/Buildings/ConvenienceStore";
    private const string StorePrefab = StoreRoot + "/Prefabs/Convenience Store.prefab";
    private const string EasyFolder = "Assets/_READY BUILDINGS - DRAG INTO SCENE";
    private const string EasyGasPrefab = EasyFolder + "/GAS STATION - READY.prefab";
    private const string EasyStorePrefab = EasyFolder + "/CONVENIENCE STORE - READY.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleMissingPrefabBuild()
    {
        EditorApplication.delayCall += TryBuildMissingPrefabs;
    }

    private static void TryBuildMissingPrefabs()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryBuildMissingPrefabs;
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(GasPrefab) != null
            && AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefab) != null
            && AssetDatabase.LoadAssetAtPath<GameObject>(EasyGasPrefab) != null
            && AssetDatabase.LoadAssetAtPath<GameObject>(EasyStorePrefab) != null)
            return;

        BuildPrefabs();
        if (retryCount++ < 20 && (AssetDatabase.LoadAssetAtPath<GameObject>(GasPrefab) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefab) == null))
            EditorApplication.delayCall += TryBuildMissingPrefabs;
    }

    [MenuItem("Tools/CYDOY/Buildings/Rebuild Imported Building Prefabs")]
    public static void BuildPrefabs()
    {
        PrepareTextures(GasRoot + "/Textures");
        PrepareTextures(StoreRoot + "/Textures");
        PrepareGasModelImporter();

        bool gasBuilt = BuildPrefab(GasModel, GasRoot, GasPrefab, "Small Red Gas Station", true);
        bool storeBuilt = BuildPrefab(StoreModel, StoreRoot, StorePrefab, "Convenience Store", false);
        if (gasBuilt && storeBuilt) CreateEasyToFindCopies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (gasBuilt && storeBuilt)
            Debug.Log("[CYDOY Buildings] Gas station and convenience store are ready in their Prefabs folders.");
        else
            Debug.LogWarning("[CYDOY Buildings] A source model is still importing. The missing prefab will be created automatically after import.");
    }

    private static void CreateEasyToFindCopies()
    {
        if (!AssetDatabase.IsValidFolder(EasyFolder))
            AssetDatabase.CreateFolder("Assets", "_READY BUILDINGS - DRAG INTO SCENE");

        if (AssetDatabase.LoadAssetAtPath<Object>(EasyGasPrefab) != null) AssetDatabase.DeleteAsset(EasyGasPrefab);
        if (AssetDatabase.LoadAssetAtPath<Object>(EasyStorePrefab) != null) AssetDatabase.DeleteAsset(EasyStorePrefab);
        AssetDatabase.CopyAsset(GasPrefab, EasyGasPrefab);
        AssetDatabase.CopyAsset(StorePrefab, EasyStorePrefab);
        AssetDatabase.ImportAsset(EasyGasPrefab, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(EasyStorePrefab, ImportAssetOptions.ForceSynchronousImport);

        Object gas = AssetDatabase.LoadAssetAtPath<Object>(EasyGasPrefab);
        Selection.activeObject = gas;
        EditorGUIUtility.PingObject(gas);
    }

    private static void PrepareGasModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(GasModel) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            changed = true;
        }
        if (importer.materialName != ModelImporterMaterialName.BasedOnMaterialName)
        {
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            changed = true;
        }
        if (importer.materialSearch != ModelImporterMaterialSearch.Everywhere)
        {
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static void PrepareTextures(string textureFolder)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool changed = false;
            bool isNormal = path.ToLowerInvariant().Contains("normal");
            TextureImporterType desiredType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desiredType) { importer.textureType = desiredType; changed = true; }
            if (importer.maxTextureSize > 2048) { importer.maxTextureSize = 2048; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }

    private static bool BuildPrefab(string modelPath, string assetRoot, string prefabPath,
        string displayName, bool convertToUrp)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) return false;

        GameObject wrapper = new GameObject(displayName);
        GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (instance == null) instance = Object.Instantiate(model);
        instance.name = displayName + " Model";
        instance.transform.SetParent(wrapper.transform, true);

        ReplaceMaterials(instance, assetRoot + "/Materials", assetRoot + "/Textures", convertToUrp);
        CenterAndGround(wrapper, instance);
        SetEnvironmentStatic(wrapper);
        PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
        Object.DestroyImmediate(wrapper);
        return true;
    }

    private static void ReplaceMaterials(GameObject instance, string materialFolder,
        string textureFolder, bool convertToUrp)
    {
        Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
        Texture2D[] textures = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(texture => texture != null)
            .ToArray();

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null) continue;
                if (!replacements.TryGetValue(source, out Material replacement))
                {
                    replacement = CreateOrUpdateMaterial(source, materialFolder, textures, convertToUrp);
                    replacements.Add(source, replacement);
                }
                materials[index] = replacement;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Material CreateOrUpdateMaterial(Material source, string materialFolder,
        IReadOnlyList<Texture2D> textures, bool convertToUrp)
    {
        string safeName = Sanitize(source.name);
        string path = materialFolder + "/" + safeName + ".mat";
        Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
        Texture mainTexture = source.mainTexture;
        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
            : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;

        if (target == null)
        {
            Shader shader = convertToUrp
                ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                : source.shader;
            target = shader != null ? new Material(shader) : new Material(source);
            target.name = safeName;
            AssetDatabase.CreateAsset(target, path);
        }

        if (!convertToUrp)
        {
            if (source.shader != null) target.shader = source.shader;
            target.CopyPropertiesFromMaterial(source);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null) target.shader = shader;
            if (target.HasProperty("_BaseColor")) target.SetColor("_BaseColor", color);
            if (target.HasProperty("_Color")) target.SetColor("_Color", color);
            if (target.HasProperty("_Metallic")) target.SetFloat("_Metallic", 0f);
            if (target.HasProperty("_Smoothness")) target.SetFloat("_Smoothness", 0.28f);
        }

        if (mainTexture == null) mainTexture = FindMatchingTexture(source.name, textures);
        if (mainTexture != null)
        {
            if (target.HasProperty("_BaseMap")) target.SetTexture("_BaseMap", mainTexture);
            if (target.HasProperty("_MainTex")) target.SetTexture("_MainTex", mainTexture);
        }

        Texture2D normal = textures.FirstOrDefault(texture => Normalize(texture.name).Contains("normal"));
        if (normal != null && Normalize(source.name).Contains("asphalt") && target.HasProperty("_BumpMap"))
        {
            target.SetTexture("_BumpMap", normal);
            target.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(target);
        return target;
    }

    private static Texture2D FindMatchingTexture(string materialName, IReadOnlyList<Texture2D> textures)
    {
        string material = Normalize(materialName);
        Texture2D best = null;
        int bestScore = 0;
        foreach (Texture2D texture in textures)
        {
            string candidate = Normalize(texture.name);
            if (candidate.Contains("normal") || candidate.Contains("roughness")) continue;
            int score = CommonTokenScore(material, candidate);
            if (score > bestScore) { best = texture; bestScore = score; }
        }
        return bestScore >= 3 ? best : null;
    }

    private static int CommonTokenScore(string left, string right)
    {
        int score = 0;
        for (int length = 3; length <= Mathf.Min(left.Length, right.Length); length++)
        {
            bool found = false;
            for (int start = 0; start + length <= left.Length; start++)
            {
                if (!right.Contains(left.Substring(start, length))) continue;
                found = true;
                break;
            }
            if (found) score = length;
        }
        return score;
    }

    private static string Normalize(string value)
    {
        StringBuilder builder = new StringBuilder();
        foreach (char character in value.ToLowerInvariant())
            if (char.IsLetterOrDigit(character)) builder.Append(character);
        return builder.ToString();
    }

    private static string Sanitize(string value)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Building Material" : safe;
    }

    private static void CenterAndGround(GameObject wrapper, GameObject model)
    {
        Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    private static void SetEnvironmentStatic(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
    }
}
#endif
