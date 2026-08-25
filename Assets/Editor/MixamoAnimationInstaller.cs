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

            Avatar ajAvatar = FindAvatar(AjPath);
            if (ajAvatar == null)
            {
                EditorUtility.DisplayDialog(dialogTitle, "AJ Avatar not found. Run Install Mixamo Character first.", "OK");
                return;
            }

            AnimationClip idle = PrepareDirectHumanoidClip(IdlePath, "Idle");
            AnimationClip walk = PrepareDirectHumanoidClip(WalkPath, "Walk");
            AnimationClip run = PrepareDirectHumanoidClip(RunPath, "Run");

            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog(dialogTitle, "One or more Humanoid clips could not be prepared. Check [CYDOY] Console logs.", "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller, ajAvatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                dialogTitle,
                "AJ locomotion repaired using the original Humanoid FBX clips.\n\nIdle: " + idle.name +
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

            AnimationClip run = PrepareDirectHumanoidClip(RunPath, "Run");
            if (run == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run.fbx is not a valid Humanoid clip. Idle and Walk were untouched.", "OK");
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

            EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run updated from the original Humanoid FBX clip. Idle and Walk unchanged.", "Nice");
        }

        private static AnimationClip PrepareDirectHumanoidClip(string path, string stateName)
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
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
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
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            AnimationClip clip = FindBestHumanoidClip(path);
            if (clip == null)
            {
                Debug.LogError("[CYDOY] No Humanoid animation clip found for " + stateName + " in " + path);
                return null;
            }

            Debug.Log($"[CYDOY] READY {stateName}: direct FBX clip '{clip.name}', length={clip.length:F2}s, humanMotion={clip.humanMotion}");
            return clip;
        }

        private static AnimationClip FindBestHumanoidClip(string path)
        {
            AnimationClip best = null;
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip) continue;
                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                if (!clip.humanMotion) continue;
                if (best == null || clip.length > best.length) best = clip;
            }
            return best;
        }

        private static Avatar FindAvatar(string path)
        {
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar && avatar.isValid && avatar.isHuman)
                    return avatar;
            return null;
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

        private static void InstallOnPlayer(RuntimeAnimatorController controller, Avatar ajAvatar)
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

                if (ajRoot != null)
                {
                    Vector3 p = ajRoot.localPosition;
                    ajRoot.localPosition = new Vector3(p.x, 0f, p.z);
                }

                animator.avatar = ajAvatar;
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
                driverSO.FindProperty("crossFadeDuration").floatValue = 0.10f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

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
