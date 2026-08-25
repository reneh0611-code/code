using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    /// <summary>
    /// Automatically configures the selected Mixamo base character as a Humanoid rig.
    /// The expected model path is Assets/Models/Characters/Ch28_nonPBR.fbx.
    /// </summary>
    public sealed class MixamoCharacterPostprocessor : AssetPostprocessor
    {
        private const string CharacterPath = "Assets/Models/Characters/Ch28_nonPBR.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.Equals(CharacterPath, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
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
                    Debug.Log("[CYDOY] Mixamo character imported and configured as Humanoid: " + CharacterPath);
            }
        }
    }
}
