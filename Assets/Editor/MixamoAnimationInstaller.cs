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
                EditorUtility.DisplayDialog(
                    "CYDOY · Animation Import",
                    "AJ Avatar not found. Run Tools → CYDOY → Install Mixamo Character first.",
                    "OK");
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

                EditorUtility.DisplayDialog(
                    "CYDOY · Animation Import",
                    "Could not create a Humanoid animation from: " + missing.Trim() +
                    "\n\nThe installer now lets each Mixamo FBX create its own Humanoid avatar. It no longer copies AJ's rig configuration onto the animation FBXs.",
                    "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller, ajAvatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · Mixamo Animations",
                "Installed successfully.\n\nIdle: " + idle.name +
                "\nWalk: " + walk.name +
                "\nRun: " + run.name,
                "Let's go");
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
            {
                Debug.LogError("[CYDOY] No ModelImporter for: " + path);
                return null;
            }

            // Important: every Mixamo animation FBX owns its own skeleton hierarchy.
            // Let Unity build a Humanoid Avatar FROM THAT FBX. Humanoid animation clips
            // can then retarget automatically to AJ's different Humanoid Avatar at runtime.
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            // Never retain an AnimationClip reference across SaveAndReimport.
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return null;

            AnimationClip firstHumanoidClip = FindBestHumanoidClip(path);
            if (firstHumanoidClip == null)
            {
                Debug.LogError(
                    $"[CYDOY] HUMANOID FAILED {stateName}: Unity created no human-motion clip in {path}. " +
                    "Open the FBX Rig tab and verify Avatar Definition = Create From This Model and the humanoid mapping is valid.");
                return null;
            }

            ConfigureLoop(importer);
            importer.SaveAndReimport();

            // Reacquire after the reimport because all FBX sub-assets were recreated.
            AnimationClip finalHumanoidClip = FindBestHumanoidClip(path);
            if (finalHumanoidClip == null)
            {
                Debug.LogError("[CYDOY] Humanoid clip disappeared after loop configuration: " + path);
                return null;
            }

            string stableName = stateName + "_Humanoid";
            AnimationClip stableClip = ExtractStableClip(finalHumanoidClip, stableName);
            if (stableClip == null)
            {
                Debug.LogError("[CYDOY] Could not create stable .anim copy for " + stateName);
                return null;
            }

            Debug.Log(
                $"[CYDOY] READY {stateName}: source='{finalHumanoidClip.name}', " +
                $"length={finalHumanoidClip.length:F2}s, humanMotion={finalHumanoidClip.humanMotion} -> {stableName}.anim");

            return stableClip;
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
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
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
                if (asset is not AnimationClip clip)
                    continue;
                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!clip.humanMotion)
                    continue;

                if (best == null || clip.length > best.length)
                    best = clip;
            }

            return best;
        }

        private static Avatar FindAvatar(string path)
        {
            foreach (UObject asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar avatar && avatar.isValid && avatar.isHuman)
                    return avatar;
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
                driverSO.FindProperty("walkThreshold").floatValue = 0.35f;
                driverSO.FindProperty("runThreshold").floatValue = 5.1f;
                driverSO.FindProperty("crossFadeDuration").floatValue = 0.10f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log(
                    $"[CYDOY] Controller '{controller.name}' wired to AJ Animator " +
                    $"'{GetHierarchyPath(animator.transform)}' with Avatar '{ajAvatar.name}'.");

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
