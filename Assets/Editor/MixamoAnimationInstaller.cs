using System;
using System.Collections.Generic;
using System.IO;
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            List<string> fbxPaths = GetPhysicalFbxAssetPaths();
            if (fbxPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Animations",
                    "Unity can see no .fbx files inside:\n" + AnimationFolder +
                    "\n\nPhysical folder checked:\n" + GetPhysicalAnimationFolder(),
                    "OK");
                return;
            }

            Debug.Log("[CYDOY] FBX files physically found:\n" + string.Join("\n", fbxPaths));

            foreach (string path in fbxPaths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AnimationClip idle = FindClip(fbxPaths, "idle", "standing idle", "breathing idle");
            AnimationClip walk = FindClip(fbxPaths, "walk", "walking");
            AnimationClip run = FindClip(fbxPaths, "run", "running", "jog", "jogging");

            if (idle == null || walk == null || run == null)
            {
                string report = BuildClipReport(fbxPaths);
                string missing = string.Empty;
                if (idle == null) missing += "Idle ";
                if (walk == null) missing += "Walk ";
                if (run == null) missing += "Run ";

                Debug.LogError("[CYDOY] Animation detection failed.\n" + report);
                EditorUtility.DisplayDialog(
                    "CYDOY · Animation Detection",
                    "Missing: " + missing.Trim() +
                    "\n\nUnity DID find your FBX files. It could not classify the internal animation clips.\n\nLook at the Console for a complete list of FBX filenames and internal clip names.",
                    "OK");
                return;
            }

            Debug.Log($"[CYDOY] Using exact clips: Idle='{idle.name}', Walk='{walk.name}', Run='{run.name}'");

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

        private static List<string> GetPhysicalFbxAssetPaths()
        {
            string physicalFolder = GetPhysicalAnimationFolder();
            var result = new List<string>();

            if (!Directory.Exists(physicalFolder))
                return result;

            string[] files = Directory.GetFiles(physicalFolder, "*.fbx", SearchOption.AllDirectories);
            foreach (string physicalPath in files)
            {
                string normalized = physicalPath.Replace('\\', '/');
                string dataPath = Application.dataPath.Replace('\\', '/');
                if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string assetPath = "Assets" + normalized.Substring(dataPath.Length);
                result.Add(assetPath);
            }

            return result;
        }

        private static string GetPhysicalAnimationFolder()
        {
            return Path.Combine(Application.dataPath, "Models", "Animations");
        }

        private static AnimationClip FindClip(List<string> fbxPaths, params string[] keywords)
        {
            foreach (string path in fbxPaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                foreach (Object asset in assets)
                {
                    if (asset is not AnimationClip clip || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string clipName = clip.name.ToLowerInvariant();
                    foreach (string keyword in keywords)
                    {
                        string key = keyword.ToLowerInvariant();
                        if (fileName.Contains(key) || clipName.Contains(key))
                        {
                            Debug.Log($"[CYDOY] Matched '{keyword}' → file '{path}', clip '{clip.name}'");
                            return clip;
                        }
                    }
                }
            }

            return null;
        }

        private static string BuildClipReport(List<string> fbxPaths)
        {
            var lines = new List<string>();
            foreach (string path in fbxPaths)
            {
                var clipNames = new List<string>();
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        clipNames.Add(clip.name);
                }

                lines.Add(path + " → [" + string.Join(", ", clipNames) + "]");
            }

            return string.Join("\n", lines);
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

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                AssetDatabase.CreateFolder("Assets/Models", "Animations");
        }
    }
}
