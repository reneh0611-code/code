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
    public static class TripoAnimationDiagnostics
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_Safe.controller";

        [MenuItem("Tools/CYDOY/Tripo Test/Diagnose Animation Setup")]
        public static void Diagnose()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Animation Diagnose", "Player.prefab not found.", "OK");
                return;
            }

            Animator animator = null;
            Transform visual = prefab.transform.Find("CharacterVisual");
            if (visual != null) animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = prefab.GetComponentInChildren<Animator>(true);

            AnimationClip idle = FindNewestClip("idle");
            AnimationClip walk = FindNewestClip("walk");
            AnimationClip run = FindNewestClip("run");

            string report = "========== TRIPO ANIMATION DIAGNOSTICS ==========\n";
            report += "Animator: " + (animator != null ? animator.name : "<NONE>") + "\n";
            report += "Controller: " + (animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<NONE>") + "\n";
            report += "Avatar: " + (animator != null && animator.avatar != null ? animator.avatar.name : "<NONE>") + "\n";
            report += "Avatar valid: " + (animator != null && animator.avatar != null && animator.avatar.isValid) + "\n";
            report += "Avatar human: " + (animator != null && animator.avatar != null && animator.avatar.isHuman) + "\n\n";
            report += DescribeClip("Idle", idle);
            report += DescribeClip("Walk", walk);
            report += DescribeClip("Run", run);
            report += "=================================================\n";

            Debug.Log(report);
            EditorUtility.DisplayDialog("CYDOY · Animation Diagnose", report, "OK");
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Install Animations Safely")]
        public static void InstallSafely()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefabAsset == null) return;

            AnimationClip idle = FindNewestClip("idle");
            AnimationClip walk = FindNewestClip("walk");
            AnimationClip run = FindNewestClip("run");
            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Safe Animation Install", "Idle/Walk/Run could not all be found. Run Diagnose Animation Setup first.", "OK");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = null;
                Transform visual = prefabRoot.transform.Find("CharacterVisual");
                if (visual != null) animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = prefabRoot.GetComponentInChildren<Animator>(true);

                if (animator == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · Safe Animation Install", "No Animator exists on the current Player visual.", "OK");
                    return;
                }

                bool humanoid = animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                if (!humanoid)
                {
                    EditorUtility.DisplayDialog(
                        "CYDOY · Safe Animation Install",
                        "STOPPED SAFELY.\n\nThe current Tripo Player still has no valid Humanoid Avatar.\n\nThat means Humanoid Idle/Walk/Run clips cannot animate this mesh yet. I have NOT replaced the controller this time.\n\nUse Mixamo Auto-Rig on the Tripo character first, then replace TripoCharacter.fbx and run this installer again.",
                        "OK");
                    return;
                }

                EnsureResourcesFolder();
                AssetDatabase.DeleteAsset(ControllerPath);
                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                AnimatorStateMachine sm = controller.layers[0].stateMachine;
                AnimatorState idleState = sm.AddState("Idle");
                AnimatorState walkState = sm.AddState("Walk");
                AnimatorState runState = sm.AddState("Run");
                idleState.motion = idle;
                walkState.motion = walk;
                runState.motion = run;
                idleState.speed = 1f;
                walkState.speed = 0.82f;
                runState.speed = 1f;
                sm.defaultState = idleState;

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                CharacterAnimationDriver driver = prefabRoot.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject so = new(driver);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("fallbackController").objectReferenceValue = controller;
                    so.FindProperty("idleWalkBlend").floatValue = 0.10f;
                    so.FindProperty("walkRunBlend").floatValue = 0.08f;
                    so.FindProperty("idleRunBlend").floatValue = 0.10f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = true;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog(
                    "CYDOY · Safe Animation Install",
                    "Installed successfully.\n\nIdle = 1.00x\nWalk = 0.82x\nRun = 1.00x\nOnly clean crossfades were added; the clips themselves were not edited.",
                    "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string DescribeClip(string label, AnimationClip clip)
        {
            if (clip == null) return label + ": <MISSING>\n";
            string path = AssetDatabase.GetAssetPath(clip);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            string rig = importer != null ? importer.animationType.ToString() : "embedded/unknown";
            return $"{label}: {clip.name}\n  Path: {path}\n  Length: {clip.length:F2}s\n  Legacy: {clip.legacy}\n  Import Rig: {rig}\n\n";
        }

        private static AnimationClip FindNewestClip(string keyword)
        {
            List<Candidate> candidates = new();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string lp = path.ToLowerInvariant();
                if (lp.Contains("tripo_locomotion_safe") || lp.Contains("tripo_locomotion_exact") || lp.Contains("littleguyshumanoid")) continue;

                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is not AnimationClip clip) continue;
                    if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                    string hay = (Path.GetFileNameWithoutExtension(path) + " " + clip.name).ToLowerInvariant();
                    if (!hay.Contains(keyword)) continue;
                    candidates.Add(new Candidate(clip, GetModifiedUtc(path), Score(hay, keyword, lp)));
                }
            }
            return candidates.OrderByDescending(c => c.Score).ThenByDescending(c => c.Modified).Select(c => c.Clip).FirstOrDefault();
        }

        private static int Score(string hay, string keyword, string path)
        {
            int s = 0;
            if (Path.GetFileNameWithoutExtension(path) == keyword) s += 100;
            if (hay.StartsWith(keyword + " ")) s += 50;
            if (path.Contains("tripo")) s += 20;
            if (path.Contains("new")) s += 10;
            return s;
        }

        private static DateTime GetModifiedUtc(string assetPath)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
                string full = Path.Combine(root, assetPath);
                return File.Exists(full) ? File.GetLastWriteTimeUtc(full) : DateTime.MinValue;
            }
            catch { return DateTime.MinValue; }
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        }

        private sealed class Candidate
        {
            public readonly AnimationClip Clip;
            public readonly DateTime Modified;
            public readonly int Score;
            public Candidate(AnimationClip clip, DateTime modified, int score) { Clip = clip; Modified = modified; Score = score; }
        }
    }
}
