using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SetupModularKitCityMaterial
{
    private const string Root = "Assets/Models/ModularKitCityBuildV2";
    private const string ModelPath = Root + "/ModularKitCityBuildV2.fbx";
    private const string MaterialPath = Root + "/Materials/ModularKitCityBuildV2.mat";
    private const string BaseColorPath = Root + "/Textures/ModularKitCityBuildV2_BaseColor.png";
    private const string NormalPath = Root + "/Textures/ModularKitCityBuildV2_Normal.png";
    private const string PackedPath = Root + "/Textures/ModularKitCityBuildV2_MetallicSmoothness.png";

    static SetupModularKitCityMaterial()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Tools/Modular Kit/Setup City Material")]
    public static void Setup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            return;

        ConfigureTexture(BaseColorPath, false, true);
        ConfigureTexture(NormalPath, true, false);
        ConfigureTexture(PackedPath, false, false);

        Directory.CreateDirectory(Root + "/Materials");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = Shader.Find("Standard");
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath));
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_GlossMapScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(material);

        RemapModelMaterials(material);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureTexture(string path, bool normalMap, bool sRgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        bool changed = importer.textureType != (normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default)
                       || importer.sRGBTexture != sRgb;
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        if (changed)
            importer.SaveAndReimport();
    }

    private static void RemapModelMaterials(Material target)
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (importer == null || model == null)
            return;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var source in renderer.sharedMaterials)
            {
                if (source != null && names.Add(source.name))
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), target);
            }
        }

        importer.SaveAndReimport();
    }
}
