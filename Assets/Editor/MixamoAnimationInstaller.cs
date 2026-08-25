using System;
using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class MixamoAnimationInstaller
    {
        private const string IdlePath = "Assets/Models/Animations/Idle.fbx";
        private const string WalkPath = "Assets/Models/Animations/Walk.fbx";
        private const string RunPath = "Assets/Models/Animations/Run.fbx";
        private const string AjPath = "Assets/Models/Characters/Aj.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/AJ_Locomotion.controller";

        [MenuItem("Tools/CYDOY/Repair AJ Locomotion")]
        public static void RepairAjLocomotion()
        {
            InstallInternal("CYDOY · AJ Repair");
        }

        [MenuItem("Tools/CYDOY/Install Mixamo Animations")]
        public static void Install()
        {
            InstallInternal("CYDOY · Mixamo Animations");
        }

        private static void InstallInternal(string dialogTitle)
        {
            EnsureResourcesFolder();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!PrepareAjForDirectPlayback())
            {
                EditorUtility.DisplayDialog(dialogTitle, "AJ could not be configured as Generic. Check Assets/Models/Characters/Aj.fbx.", "OK");
                return;
            }

            AnimationClip idle = PrepareOriginalGenericClip(IdlePath, "Idle");
            AnimationClip walk = PrepareOriginalGenericClip(WalkPath, "Walk");
            AnimationClip run = PrepareOriginalGenericClip(RunPath, "Run");

            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog(dialogTitle, "One or more original Mixamo clips could not be prepared. Check [CYDOY] Console logs.", "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                dialogTitle,
                "AJ now uses the original Generic Mixamo transform animation.\n\nNo Humanoid retargeting.\nNo root baking.\nNo loop-pose modification.\nNo animation blending.\n\nIdle: " + idle.name +
                "\nWalk: " + walk.name +
                "\nRun: " + run.name,
                "Let's go");
        }

        [MenuItem("Tools/CYDOY/Refresh Run Only")]
        public static void RefreshRunOnly()
        {
            EnsureResourcesFolder();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Controller missing. Use Repair AJ Locomotion first.", "OK");
                return;
            }

            AnimationClip run = PrepareOriginalGenericClip(RunPath, "Run");
            if (run == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run.fbx could not be loaded as its original Generic animation.", "OK");
                return;
            }

            AnimatorState runState = FindState(controller.layers[0].stateMachine, "Run");
            if (runState == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run state missing. Use Repair AJ Locomotion first.", "OK");
                return;
            }

            runState.motion = run;
            runState.speed = 1f;
            EditorUtility.SetDirty(runState);
            EditorUtility.SetDirty(controller);
            EnsureControllerStillOnPlayer(controller);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run updated directly from Run.fbx with no retargeting or pose modification.", "Nice");
        }

        private static bool PrepareAjForDirectPlayback()
        {
            ModelImporter importer = AssetImporter.GetAtPath(AjPath) as ModelImporter;
            if (importer == null)
                return false;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(AjPath) != null;
        }

        private static AnimationClip PrepareOriginalGenericClip(string path, string stateName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                Debug.LogError("[CYDOY] Missing FBX: " + path);
                return null;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return null;

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return null;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    // Repeat the exact source clip. Do not alter its pose at the seam.
                    clips[i].loopTime = true;
                    clips[i].loopPose = false;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            AnimationClip clip = FindBestOriginalClip(path);
            if (clip == null)
            {
                Debug.LogError("[CYDOY] No animation clip found for " + stateName + " in " + path);
                return null;
            }

            Debug.Log($"[CYDOY] ORIGINAL {stateName}: '{clip.name}', length={clip.length:F2}s, humanMotion={clip.humanMotion}");
            return clip;
        }

        private static AnimationClip FindBestOriginalClip(string path)
        {
            AnimationClip best = null;
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip) continue;
                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null || clip.length > best.length) best = clip;
            }
            return best;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
                if (child.state != null && child.state.name == stateName)
                    return child.state;
            return null;
        }

        private static AnimatorController BuildDirectController(AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle");
            AnimatorState walkState = machine.AddState("Walk");
            AnimatorState runState = machine.AddState("Run");

            idleState.motion = idle;
            walkState.motion = walk;
            runState.motion = run;
            idleState.speed = 1f;
            walkState.speed = 1f;
            runState.speed = 1f;
            idleState.writeDefaultValues = true;
            walkState.writeDefaultValues = true;
            runState.writeDefaultValues = true;
            machine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void InstallOnPlayer(RuntimeAnimatorController controller)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null)
                throw new InvalidOperationException("Player.prefab not found.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform ajRoot = FindAjRoot(root.transform);
                Animator animator = ajRoot != null ? ajRoot.GetComponentInChildren<Animator>(true) : root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    throw new InvalidOperationException("AJ Animator not found in Player.prefab.");

                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.speed = 1f;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                CharacterController characterController = root.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    characterController.height = 1.90f;
                    characterController.radius = 0.34f;
                    characterController.center = new Vector3(0f, 0.95f, 0f);
                    characterController.stepOffset = 0.30f;
                }

                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver == null) driver = root.AddComponent<CharacterAnimationDriver>();

                SerializedObject driverSO = new(driver);
                driverSO.FindProperty("animator").objectReferenceValue = animator;
                driverSO.FindProperty("characterController").objectReferenceValue = characterController;
                SerializedProperty fallback = driverSO.FindProperty("fallbackController");
                if (fallback != null) fallback.objectReferenceValue = controller;
                driverSO.FindProperty("walkThreshold").floatValue = 0.35f;
                driverSO.FindProperty("runThreshold").floatValue = 5.1f;
                SerializedProperty crossFade = driverSO.FindProperty("crossFadeDuration");
                if (crossFade != null) crossFade.floatValue = 0f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

                MixamoRuntimePoseAndGrounder grounder = root.GetComponent<MixamoRuntimePoseAndGrounder>();
                if (grounder == null) grounder = root.AddComponent<MixamoRuntimePoseAndGrounder>();
                SerializedObject groundSO = new(grounder);
                SerializedProperty groundAnimator = groundSO.FindProperty("animator");
                if (groundAnimator != null) groundAnimator.objectReferenceValue = animator;
                SerializedProperty modelRoot = groundSO.FindProperty("modelRoot");
                if (modelRoot != null && ajRoot != null) modelRoot.objectReferenceValue = ajRoot;
                SerializedProperty cc = groundSO.FindProperty("characterController");
                if (cc != null) cc.objectReferenceValue = characterController;
                SerializedProperty settle = groundSO.FindProperty("settleFrames");
                if (settle != null) settle.intValue = 2;
                SerializedProperty sole = groundSO.FindProperty("soleOffset");
                if (sole != null) sole.floatValue = 0f;
                groundSO.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureControllerStillOnPlayer(RuntimeAnimatorController controller)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform ajRoot = FindAjRoot(root.transform);
                Animator animator = ajRoot != null ? ajRoot.GetComponentInChildren<Animator>(true) : root.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }

                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject driverSO = new(driver);
                    SerializedProperty fallback = driverSO.FindProperty("fallbackController");
                    if (fallback != null) fallback.objectReferenceValue = controller;
                    driverSO.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindAjRoot(Transform root)
        {
            if (root.name == "Mixamo_AJ") return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindAjRoot(root.GetChild(i));
                if (result != null) return result;
            }
            return null;
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
