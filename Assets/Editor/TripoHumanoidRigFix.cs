using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class TripoHumanoidRigFix
    {
        private const string CharacterPath = "Assets/Models/Characters/TripoTest/TripoCharacter.fbx";

        [MenuItem("Tools/CYDOY/Tripo Test/Enable Humanoid Animations")]
        public static void EnableHumanoidAnimations()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Tripo Humanoid", "TripoCharacter.fbx was not found at:\n\n" + CharacterPath, "OK");
                return;
            }

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();

            AssetDatabase.Refresh();

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Animator animator = asset != null ? asset.GetComponentInChildren<Animator>(true) : null;
            Avatar avatar = animator != null ? animator.avatar : null;

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[CYDOY] Tripo Humanoid setup failed. Unity could not create a valid Human Avatar from the FBX skeleton.");
                EditorUtility.DisplayDialog(
                    "CYDOY · Tripo Humanoid",
                    "Unity could not automatically map this Tripo skeleton to a Humanoid Avatar.\n\nOpen TripoCharacter.fbx → Rig → Configure to see which bones are missing, or send me the Console output and I will adapt the mapping.",
                    "OK");
                return;
            }

            RuntimeAnimatorController controller = LittleGuysAnimationInstaller.EnsureController(true);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Tripo Humanoid", "Humanoid avatar is valid, but the Idle/Walk/Run controller could not be built.", "OK");
                return;
            }

            Debug.Log($"[CYDOY] Tripo Humanoid ready. Avatar='{avatar.name}', valid={avatar.isValid}, human={avatar.isHuman}. Reinstalling Player visual now.");
            TripoCharacterTestInstaller.InstallPlayer();
        }
    }
}
