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
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Models/Animations/AJ_Locomotion.controller";

        [MenuItem("Tools/CYDOY/Install Mixamo Animations")]
        public static void Install()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimationClip idle = PrepareAndLoadClip(IdlePath, "Idle");
            AnimationClip walk = PrepareAndLoadClip(WalkPath, "Walk");
            AnimationClip run = PrepareAndLoadClip(RunPath, "Run");

            if (idle == null || walk == null || run == null)
            {
                string missing = string.Empty;
                if (idle == null) missing += "Idle.fbx ";
                if (walk == null) missing += "Walk.fbx ";
                if (run == null) missing += "Run.fbx ";

                EditorUtility.DisplayDialog(
                    "CYDOY · Animation Import",
                    "Could not create a readable AnimationClip from: " + missing.Trim() +
                    "\n\nThe installer now uses the exact three file paths and rebuilds their clip settings directly from the FBX take data.\n\nCheck the Console for the exact importer report.",
                    "OK");
                return;
            }

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · Mixamo Animations",
                "Installed successfully.\n\nIdle: " + idle.name +
                "\nWalk: " + walk.name +
                "\nRun: " + run.name,
                "Let's go");
        }

        private static AnimationClip PrepareAndLoadClip(string path, string forcedName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                Debug.LogError("[CYDOY] Missing FBX at exact path: " + path);
                return null;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[CYDOY] No ModelImporter available for: " + path);
                return null;
            }

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
            if (sourceClips == null || sourceClips.Length == 0)
                sourceClips = importer.clipAnimations;

            if (sourceClips == null || sourceClips.Length == 0)
            {
                Debug.LogError("[CYDOY] FBX contains no importable animation take data: " + path);
                importer.SaveAndReimport();
                return null;
            }

            ModelImporterClipAnimation source = sourceClips[0];
            ModelImporterClipAnimation clip = new()
            {
                name = forcedName,
                takeName = source.takeName,
                firstFrame = source.firstFrame,
                lastFrame = source.lastFrame,
                loopTime = true,
                loopPose = true,
                lockRootRotation = true,
                lockRootHeightY = true,
                lockRootPositionXZ = true,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true
            };

            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            UObject[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (UObject asset in assets)
            {
                if (asset is AnimationClip animationClip &&
                    !animationClip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[CYDOY] {path} → clip '{animationClip.name}', length={animationClip.length:F2}s, frames={clip.firstFrame:F0}-{clip.lastFrame:F0}, take='{clip.takeName}'");
                    return animationClip;
                }
            }

            Debug.LogError("[CYDOY] Import completed but Unity still exposed no AnimationClip sub-asset for: " + path);
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
                throw new InvalidOperationException("Player.prefab not found. Install the Mixamo character first.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    throw new InvalidOperationException("No Animator found below Player.prefab.");

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
                driverSO.FindProperty("crossFadeDuration").floatValue = 0.12f;
                driverSO.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
