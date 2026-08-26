using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class LittleGuysAnimationInstaller
    {
        private const string SourceIdle = "Assets/Models/Animations/Idle.fbx";
        private const string SourceWalk = "Assets/Models/Animations/Walk.fbx";
        private const string SourceRun = "Assets/Models/Animations/Run.fbx";
        private const string GeneratedFolder = "Assets/Models/Animations/LittleGuysHumanoid";
        private const string IdleCopy = GeneratedFolder + "/Idle_Humanoid.fbx";
        private const string WalkCopy = GeneratedFolder + "/Walk_Humanoid.fbx";
        private const string RunCopy = GeneratedFolder + "/Run_Humanoid.fbx";
        public const string ControllerPath = "Assets/Resources/LittleGuys_Locomotion.controller";

        [MenuItem("Tools/CYDOY/Build Little Guys Humanoid Animations")]
        public static void BuildMenu()
        {
            RuntimeAnimatorController controller = EnsureController(true);
            EditorUtility.DisplayDialog(
                "CYDOY · Little Guys Animations",
                controller != null
                    ? "Humanoid Idle/Walk/Run controller created for the Little Guys. AJ's original Generic FBX files were not changed."
                    : "Could not build the Little Guys Humanoid controller. Check the Console.",
                "OK");
        }

        public static RuntimeAnimatorController EnsureController(bool forceRebuild = false)
        {
            EnsureFolders();

            if (!forceRebuild)
            {
                RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                if (existing != null)
                    return existing;
            }

            AnimationClip idle = PrepareHumanoidCopy(SourceIdle, IdleCopy, "Idle");
            AnimationClip walk = PrepareHumanoidCopy(SourceWalk, WalkCopy, "Walk");
            AnimationClip run = PrepareHumanoidCopy(SourceRun, RunCopy, "Run");

            if (idle == null || walk == null)
            {
                Debug.LogError("[CYDOY] Little Guys Humanoid setup failed: Idle or Walk could not be imported as Humanoid.");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle");
            AnimatorState walkState = machine.AddState("Walk");
            idleState.motion = idle;
            walkState.motion = walk;
            idleState.speed = 1f;
            walkState.speed = 1f;
            machine.defaultState = idleState;

            if (run != null)
            {
                AnimatorState runState = machine.AddState("Run");
                runState.motion = run;
                runState.speed = 1f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CYDOY] Little Guys Humanoid controller ready: {ControllerPath}. Idle human={idle.humanMotion}, Walk human={walk.humanMotion}, Run={(run != null ? run.humanMotion.ToString() : "missing")}");
            return controller;
        }

        private static AnimationClip PrepareHumanoidCopy(string sourcePath, string copyPath, string stateName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                Debug.LogError("[CYDOY] Missing source animation: " + sourcePath);
                return null;
            }

            if (AssetDatabase.LoadMainAssetAtPath(copyPath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, copyPath))
                {
                    Debug.LogError("[CYDOY] Could not copy " + sourcePath + " -> " + copyPath);
                    return null;
                }
            }

            AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceSynchronousImport);
            ModelImporter importer = AssetImporter.GetAtPath(copyPath) as ModelImporter;
            if (importer == null)
                return null;

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(copyPath) as ModelImporter;
            if (importer == null)
                return null;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = true;
                    clips[i].loopPose = false;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalPositionXZ = true;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            AnimationClip best = null;
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(copyPath))
            {
                if (asset is not AnimationClip clip) continue;
                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null || clip.length > best.length) best = clip;
            }

            if (best == null)
                Debug.LogError("[CYDOY] No readable Humanoid clip found for " + stateName + " in " + copyPath);
            else
                Debug.Log($"[CYDOY] Little Guys {stateName}: '{best.name}', humanMotion={best.humanMotion}, length={best.length:F2}s");

            return best;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Animations")) AssetDatabase.CreateFolder("Assets/Models", "Animations");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/Models/Animations", "LittleGuysHumanoid");
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
