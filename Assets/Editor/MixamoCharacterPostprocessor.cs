using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    /// <summary>
    /// AJ uses the original Mixamo transform hierarchy directly.
    /// Generic is intentional: no Humanoid retargeting or pose conversion.
    /// </summary>
    public sealed class MixamoCharacterPostprocessor : AssetPostprocessor
    {
        private const string CharacterPath = "Assets/Models/Characters/Aj.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.Equals(CharacterPath, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable = false;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.Equals(CharacterPath, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
                if (model != null)
                    Debug.Log("[CYDOY] AJ imported as Generic for direct Mixamo playback: " + CharacterPath);
            }
        }
    }
}
