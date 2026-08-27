using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class MeleeAnimationAutoSetup
    {
        private const string AnimationFolder = "Assets/Models/Animations";
        private const string PlayerControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";
        private const string NpcControllerPath = "Assets/Resources/LittleGuys_Locomotion.controller";

        private static readonly (string state, string file)[] PlayerStates =
        {
            ("Punch1", "Punch1.fbx"),
            ("Punch2", "Punch2.fbx"),
            ("Punch3", "Punch3.fbx"),
            ("Punch4", "Punch4.fbx"),
            ("Punch5", "Punch5.fbx")
        };

        private static readonly (string state, string file)[] NpcStates =
        {
            ("Hit1", "Hit1.fbx"),
            ("Hit2", "Hit2.fbx"),
            ("HeavyHit", "HeavyHit.fbx"),
            ("Knockdown", "Knockdown.fbx"),
            ("GetUp", "GetUp.fbx")
        };

        private static double nextAttempt;
        private static int attempts;
        private static bool installed;

        static MeleeAnimationAutoSetup()
        {
            EditorApplication.update += RetryUntilInstalled;
            EditorApplication.delayCall += ForceAttemptSoon;
        }

        [DidReloadScripts]
        private static void AfterScriptsReload()
        {
            installed = false;
            attempts = 0;
            EditorApplication.delayCall += ForceAttemptSoon;
        }

        private static void ForceAttemptSoon()
        {
            nextAttempt = 0;
        }

        private static void RetryUntilInstalled()
        {
            if (installed) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextAttempt) return;

            nextAttempt = EditorApplication.timeSinceStartup + 1.0;
            attempts++;
            installed = InstallNow();

            if (!installed && attempts == 1)
                Debug.Log("[CYDOY MELEE AUTO] Waiting for Unity to finish importing combat FBX files; setup will retry automatically.");
        }

        private static bool InstallNow()
        {
            AnimatorController player = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            AnimatorController npc = AssetDatabase.LoadAssetAtPath<AnimatorController>(NpcControllerPath);
            if (player == null || npc == null) return false;

            bool allReady = true;
            bool changed = false;

            foreach (var entry in PlayerStates)
            {
                AnimationClip clip = ResolveClip(entry.file);
                if (clip == null) { allReady = false; continue; }
                changed |= EnsureState(player, entry.state, clip);
            }

            foreach (var entry in NpcStates)
            {
                AnimationClip clip = ResolveClip(entry.file);
                if (clip == null) { allReady = false; continue; }
                changed |= EnsureState(npc, entry.state, clip);
            }

            // Keep hit states available on the player controller too because ambient NPCs may clone
            // the player's visual/controller at runtime before SharedWorldBootstrap swaps controllers.
            foreach (var entry in NpcStates)
            {
                AnimationClip clip = ResolveClip(entry.file);
                if (clip != null) changed |= EnsureState(player, entry.state, clip);
            }

            if (changed)
            {
                EditorUtility.SetDirty(player);
                EditorUtility.SetDirty(npc);
                AssetDatabase.SaveAssets();
            }

            bool playerVerified = PlayerStates.All(e => HasState(player, e.state));
            bool npcVerified = NpcStates.All(e => HasState(npc, e.state));

            if (allReady && playerVerified && npcVerified)
            {
                Debug.Log("[CYDOY MELEE AUTO] READY: Player Punch1-5 and NPC Hit1/Hit2/HeavyHit/Knockdown/GetUp are permanently wired.");
                return true;
            }

            return false;
        }

        private static AnimationClip ResolveClip(string expectedFile)
        {
            // First: exact deterministic path.
            string exactPath = AnimationFolder + "/" + expectedFile;
            AnimationClip clip = FirstRealClip(exactPath, Path.GetFileNameWithoutExtension(expectedFile));
            if (clip != null) return clip;

            // Fallback: locate the FBX anywhere under Models/Animations, case-insensitively.
            string stem = Path.GetFileNameWithoutExtension(expectedFile);
            string[] guids = AssetDatabase.FindAssets(stem + " t:Model", new[] { AnimationFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), stem, StringComparison.OrdinalIgnoreCase)) continue;
                clip = FirstRealClip(path, stem);
                if (clip != null) return clip;
            }

            return null;
        }

        private static AnimationClip FirstRealClip(string path, string preferredName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) return null;

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (clips.Length == 0) return null;
            AnimationClip exact = clips.FirstOrDefault(c => Normalize(c.name) == Normalize(preferredName));
            return exact ?? clips[0];
        }

        private static bool EnsureState(AnimatorController controller, string stateName, AnimationClip clip)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == stateName);
            bool changed = false;

            if (state == null)
            {
                state = machine.AddState(stateName);
                changed = true;
            }

            if (state.motion != clip)
            {
                state.motion = clip;
                changed = true;
            }

            if (!Mathf.Approximately(state.speed, 1f)) { state.speed = 1f; changed = true; }
            state.writeDefaultValues = true;
            return changed;
        }

        private static bool HasState(AnimatorController controller, string stateName)
        {
            return controller != null && controller.layers.Length > 0 &&
                   controller.layers[0].stateMachine.states.Any(s => s.state != null && s.state.name == stateName && s.state.motion != null);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        public static bool IsCombatFile(string file)
        {
            return PlayerStates.Concat(NpcStates).Any(e => string.Equals(e.file, file, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class MeleeAnimationImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            string file = Path.GetFileName(assetPath);
            if (!MeleeAnimationAutoSetup.IsCombatFile(file)) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            // Do not force a rig conversion here. Existing imported rig settings are preserved.
        }

        private void OnPostprocessModel(GameObject go)
        {
            string file = Path.GetFileName(assetPath);
            if (!MeleeAnimationAutoSetup.IsCombatFile(file)) return;
            // InitializeOnLoad update loop will retry after importing finishes.
        }
    }
}
