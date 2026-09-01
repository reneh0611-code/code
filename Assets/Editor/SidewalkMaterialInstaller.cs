#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class SidewalkMaterialInstaller
    {
        private const string Root = "Assets/Materials/Sidewalk/ConcretePavement02";
        private const string BaseColorPath = Root + "/Textures/ConcretePavement02_BaseColor_2K.jpg";
        private const string NormalPath = Root + "/Textures/ConcretePavement02_NormalGL_2K.jpg";
        private const string OcclusionPath = Root + "/Textures/ConcretePavement02_AO_2K.jpg";
        private const string MaterialPath = Root + "/Concrete Pavement - Large Slabs.mat";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticSetup()
        {
            EditorApplication.delayCall += CreateOrUpdateMaterial;
        }

        [MenuItem("Tools/CYDOY/Materials/Rebuild Large Sidewalk Material")]
        public static void CreateOrUpdateMaterial()
        {
            ConfigureTexture(BaseColorPath, false, true);
            ConfigureTexture(NormalPath, true, false);
            ConfigureTexture(OcclusionPath, false, false);

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath);
            if (baseColor == null || normal == null || occlusion == null) return;

            Shader shader = Shader.Find("CYDOY/World Aligned Sidewalk");
            bool scriptablePipeline = GraphicsSettings.currentRenderPipeline != null;
            if (shader == null)
                shader = Shader.Find(scriptablePipeline
                    ? "Universal Render Pipeline/Lit"
                    : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Concrete Pavement - Large Slabs" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // Less than 1 enlarges the source slabs. Keep this in sync with the
            // curve-aligned material so both sidewalk workflows have the same scale.
            Vector2 largeSlabTiling = new(.5f, .5f);
            SetTexture(material, "_BaseMap", baseColor, largeSlabTiling);
            SetTexture(material, "_MainTex", baseColor, largeSlabTiling);
            SetTexture(material, "_BumpMap", normal, largeSlabTiling);
            SetTexture(material, "_OcclusionMap", occlusion, largeSlabTiling);

            SetFloat(material, "_BumpScale", .72f);
            SetFloat(material, "_OcclusionStrength", .85f);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_Smoothness", .16f);
            SetFloat(material, "_Glossiness", .16f);
            SetFloat(material, "_WorldTiling", .18f);
            SetFloat(material, "_WorldRotation", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CYDOY MATERIAL] Large sidewalk slabs are ready at {MaterialPath}");
        }

        private static void ConfigureTexture(string path, bool normalMap, bool sRgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            TextureImporterType wantedType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (importer.textureType != wantedType)
            {
                importer.textureType = wantedType;
                changed = true;
            }
            if (importer.sRGBTexture != sRgb)
            {
                importer.sRGBTexture = sRgb;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }
            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }
            if (importer.maxTextureSize != 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }
            if (normalMap && importer.flipGreenChannel)
            {
                // Poly Haven's GL normal map already uses the expected Y direction.
                importer.flipGreenChannel = false;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        private static void SetTexture(Material material, string property, Texture texture, Vector2 scale)
        {
            if (!material.HasProperty(property)) return;
            material.SetTexture(property, texture);
            material.SetTextureScale(property, scale);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
#endif
