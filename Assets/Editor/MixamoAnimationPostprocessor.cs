using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public sealed class MixamoAnimationPostprocessor : AssetPostprocessor
    {
        private const string AnimationFolder = "Assets/Models/Animations/";
        private const string CharacterPath = "Assets/Models/Characters/Ch28_nonPBR.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            Avatar avatar = LoadCharacterAvatar();
            if (avatar != null)
                importer.sourceAvatar = avatar;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        private static Avatar LoadCharacterAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CharacterPath);
            foreach (Object asset in assets)
            {
                if (asset is Avatar avatar)
                    return avatar;
            }
            return null;
        }
    }
}
