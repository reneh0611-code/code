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

            // IMPORTANT: Do not force Generic/Humanoid/avatar settings here.
            // The installer controls that explicitly in multiple passes.
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
