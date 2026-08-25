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
        private const string GeneratedFolder = "Assets/Models/Animations/Generated";

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
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Avatar ajAvatar = FindAvatar(AjPath);
            if (ajAvatar == null)
            {
                EditorUtility.DisplayDialog(dialogTitle, "AJ Avatar not found. Run Tools → CYDOY → Install Mixamo Character first.", "OK");
                return;
            }

            AnimationClip idle = PrepareHumanoidClip(IdlePath, "Idle");
            AnimationClip walk = PrepareHumanoidClip(WalkPath, "Walk");
            AnimationClip run = PrepareHumanoidClip(RunPath, "Run");

            if (idle == null || walk == null || run == null)
            {
                string missing = string.Empty;
                if (idle == null) missing += "Idle.fbx ";
                if (walk == null) missing += "Walk.fbx ";
                if (run == null) missing += "Run.fbx ";
                EditorUtility.DisplayDialog(dialogTitle, "Could not prepare: " + missing.Trim(), "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller, ajAvatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                dialogTitle,
                "AJ locomotion repaired.\n\nIdle: " + idle.name + "\nWalk: " + walk.name + "\nRun: " + run.name +
                "\n\nRuntime fallback: Resources/AJ_Locomotion",
                "Let's go");
        }

        [MenuItem("Tools/CYDOY/Refresh Run Only")]
        public static void RefreshRunOnly()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Controller missing. Use Tools → CYDOY → Repair AJ Locomotion first.", "OK");
                return;
            }

            AnimationClip run = PrepareHumanoidClip(RunPath, "Run");
            if (run == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run.fbx could not be imported. Idle and Walk were untouched.", "OK");
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

            EditorUtility.DisplayDialog("CYDOY · Run Refresh", "Run updated. Idle and Walk unchanged.", "Nice");
        }

        private static AnimationClip PrepareHumanoidClip(string path, string stateName)
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

            AnimationClip clip = FindBestHumanoidClip(path);
            if (clip == null)
            {
                Debug.LogError("[CYDOY] No Humanoid clip found for " + stateName + " in " + path);
                return null;
            }

            ConfigureLoopOnly(importer);
            importer.SaveAndReimport();
            clip = FindBestHumanoidClip(path);
            if (clip == null)
                return null;

            AnimationClip stable = ExtractStableClip(clip, stateName + "_Humanoid");
            if (stable != null)
                Debug.Log($"[CYDOY] READY {stateName}: '{clip.name}', {clip.length:F2}s, humanMotion={clip.humanMotion}");

            return stable;
        }

        private static void ConfigureLoopOnly(ModelImporter importer)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                return;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            importer.clipAnimations = clips;
        }

        private static AnimationClip ExtractStableClip(AnimationClip source, string name)
        {
            if (source == null)
                return null;

            string path = GeneratedFolder + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);

            AnimationClip copy = new AnimationClip();
            EditorUtility.CopySerialized(source, copy);
            copy.name = name;
            copy.wrapMode = WrapMode.Loop;
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
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

        private static void EnsureControllerStillOnPlayer(RuntimeAnimatorController controller)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = FindAjAnimator(root.transform);
                if (animator == null) return;

                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.applyRootMotion = false;

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

        private static void InstallOnPlayer(RuntimeAnimatorController controller, Avatar ajAvatar)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null)
                throw new InvalidOperationException("Player.prefab not found. Run the Phase 1 player builder first.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = FindAjAnimator(root.transform);
                if (animator == null)
                    throw new InvalidOperationException("No Animator found under Mixamo_AJ in Player.prefab.");

                foreach (Animator other in root.GetComponentsInChildren<Animator>(true))
                    if (other != animator) other.enabled = false;

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
                driverSO.FindProperty("fallbackController").objectReferenceValue = controller;
                driverSO.FindProperty("walkThreshold").floatValue = 0.35f;
                driverSO.FindProperty("runThreshold").floatValue = 5.1f;
                driverSO.FindProperty("crossFadeDuration").floatValue = 0.10f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

                MixamoRuntimePoseAndGrounder grounder = root.GetComponent<MixamoRuntimePoseAndGrounder>();
                if (grounder != null)
                {
                    SerializedObject groundSO = new(grounder);
                    groundSO.FindProperty("animator").objectReferenceValue = animator;
                    groundSO.FindProperty("modelRoot").objectReferenceValue = FindAjRoot(root.transform);
                    groundSO.FindProperty("characterController").objectReferenceValue = characterController;
                    groundSO.FindProperty("forceGrounding").boolValue = true;
                    groundSO.FindProperty("visualGroundOffset").floatValue = 0.01f;
                    groundSO.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Animator FindAjAnimator(Transform root)
        {
            Transform ajRoot = FindAjRoot(root);
            if (ajRoot != null)
            {
                Animator exact = ajRoot.GetComponentInChildren<Animator>(true);
                if (exact != null) return exact;
            }
            return root.GetComponentInChildren<Animator>(true);
        }

        private static Transform FindAjRoot(Transform root)
        {
            return FindChildRecursive(root, "Mixamo_AJ");
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root.name == targetName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildRecursive(root.GetChild(i), targetName);
                if (result != null) return result;
            }
            return null;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Animations"))
                AssetDatabase.CreateFolder("Assets/Models", "Animations");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Models/Animations", "Generated");
        }
    }
}
