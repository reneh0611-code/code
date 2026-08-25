using UnityEditor;

namespace CheatOnYourDayOnes.EditorTools
{
    public sealed class MixamoAnimationPostprocessor : AssetPostprocessor
    {
        private const string AnimationFolder = "Assets/Models/Animations/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            // Keep the Mixamo animation itself untouched. We only tell Unity
            // to import it as a Humanoid animation. Loop/root settings are
            // applied later by the installer after the real clip exists.
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
