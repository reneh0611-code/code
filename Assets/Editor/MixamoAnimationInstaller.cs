using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class MixamoAnimationInstaller
    {
        private const string AnimationFolder = "Assets/Models/Animations";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Models/Animations/AJ_Locomotion.controller";

        [MenuItem("Tools/CYDOY/Install Mixamo Animations")]
        public static void Install()
        {
            EnsureFolder();
            ForceAnimationReimport();

            AnimationClip idle = FindClipFromFbx("idle");
            AnimationClip walk = FindClipFromFbx("walk", "walking");
            AnimationClip run = FindClipFromFbx("run", "running");

            if (idle == null || walk == null || run == null)
            {
                string missing = string.Empty;
                if (idle == null) missing += "Idle ";
                if (walk == null) missing += "Walk ";
                if (run == null) missing += "Run ";

                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Animations",
                    "Could not read these animation clips: " + missing.Trim() +
                    "\n\nExpected FBX files somewhere inside:\n" + AnimationFolder +
                    "\n\nAccepted filename examples:\nIdle.fbx\nWalk.fbx / Walking.fbx\nRun.fbx / Running.fbx\n\nThe installer now reads animation sub-assets directly from each FBX.",
                    "OK");
                return;
            }

            Debug.Log($"[CYDOY] Animation clips found: Idle={idle.name}, Walk={walk.name}, Run={run.name}");

            AnimatorController controller = BuildDirectController(idle, walk, run);
            InstallOnPlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "CYDOY · Mixamo Animations",
                "Done. AJ now uses the original Mixamo Idle, Walk and Run clips directly and loops them continuously.",
                "Let's go");
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
                throw new System.InvalidOperationException("Player.prefab not found. Install the Mixamo character first.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    throw new System.InvalidOperationException("No Animator found below Player.prefab.");

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

        private static void ForceAnimationReimport()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { AnimationFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static AnimationClip FindClipFromFbx(params string[] keywords)
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { AnimationFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string lowerPath = path.ToLowerInvariant();
                bool filenameMatches = false;
                foreach (string keyword in keywords)
                {
                    if (lowerPath.Contains(keyword.ToLowerInvariant()))
                    {
                        filenameMatches = true;
                        break;
                    }
                }

                if (!filenameMatches)
                    continue;

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[CYDOY] Found clip '{clip.name}' inside '{path}'.");
                        return clip;
                    }
                }

                Debug.LogWarning($"[CYDOY] FBX matched by filename but contained no readable AnimationClip: {path}");
            }

            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                AssetDatabase.CreateFolder("Assets/Models", "Animations");
        }
    }
}
