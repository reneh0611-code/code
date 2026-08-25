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

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPostprocessModel(UnityEngine.GameObject root)
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return;

            string lower = assetPath.ToLowerInvariant();
            bool shouldLoop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run");

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }
    }
}
