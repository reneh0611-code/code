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
        private const string ControllerPath = "Assets/Models/Animations/AJ_Locomotion.controller";
        private const string GeneratedFolder = "Assets/Models/Animations/Generated";

        [MenuItem("Tools/CYDOY/Install Mixamo Animations")]
        public static void Install()
        {
            EnsureGeneratedFolder();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Avatar ajAvatar = FindAvatar(AjPath);
            if (ajAvatar == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Animation Import", "AJ Avatar not found. Run Tools → CYDOY → Install Mixamo Character first.", "OK");
                return;
            }

            AnimationClip idle = PrepareClip(IdlePath, "Idle", ajAvatar);
            AnimationClip walk = PrepareClip(WalkPath, "Walk", ajAvatar);
            AnimationClip run = PrepareClip(RunPath, "Run", ajAvatar);

            if (idle == null || walk == null || run == null)
            {
                string missing = string.Empty;
                if (idle == null) missing += "Idle.fbx ";
                if (walk == null) missing += "Walk.fbx ";
                if (run == null) missing += "Run.fbx ";

                EditorUtility.DisplayDialog(
                    "CYDOY · Animation Import",
                    "Still no usable clip from: " + missing.Trim() +
                    "\n\nCheck Console lines beginning with [CYDOY].",
                    "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller, ajAvatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · Mixamo Animations",
                "Installed successfully.\n\nIdle: " + idle.name + "\nWalk: " + walk.name + "\nRun: " + run.name,
                "Let's go");
        }

        private static AnimationClip PrepareClip(string path, string stateName, Avatar ajAvatar)
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
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            AnimationClip genericClip = FindFirstRealClip(path);
            if (genericClip == null)
            {
                Debug.LogError("[CYDOY] GENERIC FAILED: no AnimationClip in " + path);
                return null;
            }

            Debug.Log($"[CYDOY] GENERIC OK {stateName}: '{genericClip.name}', {genericClip.length:F2}s");
            AnimationClip fallback = ExtractClipCopy(genericClip, stateName + "_GenericFallback");

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = ajAvatar;
            importer.SaveAndReimport();

            AnimationClip humanoidClip = FindFirstRealClip(path);
            if (humanoidClip != null)
            {
                ConfigureLoop(importer);
                importer.SaveAndReimport();
                humanoidClip = FindFirstRealClip(path);

                if (humanoidClip != null)
                {
                    Debug.Log($"[CYDOY] HUMANOID OK {stateName}: '{humanoidClip.name}', {humanoidClip.length:F2}s");
                    return humanoidClip;
                }
            }

            Debug.LogWarning("[CYDOY] HUMANOID FAILED for " + stateName + ". Using extracted Generic fallback.");
            return fallback;
        }

        private static void ConfigureLoop(ModelImporter importer)
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
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private static AnimationClip ExtractClipCopy(AnimationClip source, string name)
        {
            string path = GeneratedFolder + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);

            AnimationClip copy = UObject.Instantiate(source);
            copy.name = name;
            copy.wrapMode = WrapMode.Loop;
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static Avatar FindAvatar(string path)
        {
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar && avatar.isValid)
                    return avatar;
            return null;
        }

        private static AnimationClip FindFirstRealClip(string path)
        {
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
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
            idleState.speed = walkState.speed = runState.speed = 1f;
            idleState.writeDefaultValues = walkState.writeDefaultValues = runState.writeDefaultValues = true;
            machine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void InstallOnPlayer(RuntimeAnimatorController controller, Avatar ajAvatar)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null)
                throw new InvalidOperationException("Player.prefab not found. Install the Mixamo character first.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = FindAjAnimator(root.transform);
                if (animator == null)
                    throw new InvalidOperationException("No Animator found under Mixamo_AJ in Player.prefab.");

                foreach (Animator other in root.GetComponentsInChildren<Animator>(true))
                {
                    if (other != animator)
                        other.enabled = false;
                }

                animator.avatar = ajAvatar;
                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.speed = 1f;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                CharacterController characterController = root.GetComponent<CharacterController>();
                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver == null)
                    driver = root.AddComponent<CharacterAnimationDriver>();

                SerializedObject driverSO = new(driver);
                driverSO.FindProperty("animator").objectReferenceValue = animator;
                driverSO.FindProperty("characterController").objectReferenceValue = characterController;
                driverSO.FindProperty("walkThreshold").floatValue = 0.15f;
                driverSO.FindProperty("runThreshold").floatValue = 5.1f;
                driverSO.FindProperty("crossFadeDuration").floatValue = 0.10f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log(
                    $"[CYDOY] Controller '{controller.name}' wired directly to AJ Animator '{GetHierarchyPath(animator.transform)}'. Avatar='{ajAvatar.name}'.");

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Animator FindAjAnimator(Transform root)
        {
            Transform ajRoot = FindChildRecursive(root, "Mixamo_AJ");
            if (ajRoot != null)
            {
                Animator exact = ajRoot.GetComponentInChildren<Animator>(true);
                if (exact != null)
                    return exact;
            }

            return root.GetComponentInChildren<Animator>(true);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildRecursive(root.GetChild(i), targetName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Models/Animations"))
                    throw new InvalidOperationException("Assets/Models/Animations does not exist.");
                AssetDatabase.CreateFolder("Assets/Models/Animations", "Generated");
            }
        }
    }
}
