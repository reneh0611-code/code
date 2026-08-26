using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class ExactLocomotionInstaller
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_Exact.controller";

        [MenuItem("Tools/CYDOY/Tripo Test/Install Latest Idle Walk Run")]
        public static void Install()
        {
            EnsureResourcesFolder();

            AnimationClip idle = FindNewestClip("idle");
            AnimationClip walk = FindNewestClip("walk");
            AnimationClip run = FindNewestClip("run");

            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · Exact Locomotion",
                    "I could not find all three local clips.\n\n" +
                    $"Idle: {(idle != null ? AssetDatabase.GetAssetPath(idle) : "MISSING")}\n" +
                    $"Walk: {(walk != null ? AssetDatabase.GetAssetPath(walk) : "MISSING")}\n" +
                    $"Run: {(run != null ? AssetDatabase.GetAssetPath(run) : "MISSING")}\n\n" +
                    "The files only need Idle, Walk and Run somewhere in their file/clip names.",
                    "OK");
                return;
            }

            AnimatorController controller = BuildController(idle, walk, run);
            if (controller == null)
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = FindVisibleAnimator(prefabRoot);
                if (animator == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · Exact Locomotion", "No Animator was found under Player/CharacterVisual.", "OK");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.enabled = true;

                CharacterAnimationDriver driver = prefabRoot.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject so = new(driver);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("fallbackController").objectReferenceValue = controller;
                    so.FindProperty("idleWalkBlend").floatValue = 0.12f;
                    so.FindProperty("walkRunBlend").floatValue = 0.10f;
                    so.FindProperty("idleRunBlend").floatValue = 0.12f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = true;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string avatarStatus = animator.avatar != null
                    ? $"Avatar: {animator.avatar.name} | valid={animator.avatar.isValid} | human={animator.avatar.isHuman}"
                    : "Avatar: NONE";

                Debug.Log(
                    "[CYDOY] EXACT locomotion installed.\n" +
                    $"Idle = {AssetDatabase.GetAssetPath(idle)}\n" +
                    $"Walk = {AssetDatabase.GetAssetPath(walk)}\n" +
                    $"Run  = {AssetDatabase.GetAssetPath(run)}\n" +
                    $"{avatarStatus}\n" +
                    "Clip curves/root data were not edited. Walk state speed = 0.82."
                );

                EditorUtility.DisplayDialog(
                    "CYDOY · Exact Locomotion",
                    "Installed the newest local Idle / Walk / Run clips.\n\n" +
                    "• Idle: unchanged\n" +
                    "• Run: unchanged\n" +
                    "• Walk animation itself: unchanged\n" +
                    "• Walk playback speed: 0.82x\n" +
                    "• Clean crossfades only\n\n" +
                    avatarStatus,
                    "Test it");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
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
            walkState.speed = 0.82f;
            runState.speed = 1f;

            machine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip FindNewestClip(string keyword)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            List<Candidate> candidates = new();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string lowerPath = path.ToLowerInvariant();
                if (lowerPath.Contains("littleguyshumanoid") ||
                    lowerPath.Contains("tripo_locomotion_exact") ||
                    lowerPath.Contains("resources/aj_locomotion"))
                    continue;

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset is not AnimationClip clip)
                        continue;
                    if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string haystack = (Path.GetFileNameWithoutExtension(path) + " " + clip.name).ToLowerInvariant();
                    if (!haystack.Contains(keyword))
                        continue;

                    DateTime modified = GetModifiedUtc(path);
                    int score = Score(haystack, keyword, lowerPath);
                    candidates.Add(new Candidate(clip, path, modified, score));
                }
            }

            Candidate best = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.ModifiedUtc)
                .FirstOrDefault();

            if (best != null)
            {
                Debug.Log($"[CYDOY] Selected {keyword.ToUpperInvariant()}: '{best.Clip.name}' from '{best.Path}', modified {best.ModifiedUtc:u}, score={best.Score}");
                return best.Clip;
            }

            return null;
        }

        private static int Score(string haystack, string keyword, string lowerPath)
        {
            int score = 0;
            if (haystack == keyword) score += 100;
            if (haystack.StartsWith(keyword + " ") || haystack.EndsWith(" " + keyword)) score += 60;
            if (Path.GetFileNameWithoutExtension(lowerPath) == keyword) score += 80;
            if (lowerPath.Contains("tripo")) score += 20;
            if (lowerPath.Contains("new")) score += 10;
            if (lowerPath.Contains("animation")) score += 5;
            return score;
        }

        private static DateTime GetModifiedUtc(string assetPath)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
                string fullPath = Path.Combine(projectRoot, assetPath);
                return File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static Animator FindVisibleAnimator(GameObject prefabRoot)
        {
            Transform visual = prefabRoot.transform.Find("CharacterVisual");
            if (visual != null)
            {
                Animator a = visual.GetComponentInChildren<Animator>(true);
                if (a != null) return a;
            }
            return prefabRoot.GetComponentInChildren<Animator>(true);
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        private sealed class Candidate
        {
            public readonly AnimationClip Clip;
            public readonly string Path;
            public readonly DateTime ModifiedUtc;
            public readonly int Score;

            public Candidate(AnimationClip clip, string path, DateTime modifiedUtc, int score)
            {
                Clip = clip;
                Path = path;
                ModifiedUtc = modifiedUtc;
                Score = score;
            }
        }
    }
}
