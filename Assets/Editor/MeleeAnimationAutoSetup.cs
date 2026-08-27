using System;
using System.Collections.Generic;
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

        private static readonly string[] Controllers =
        {
            "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller",
            "Assets/Resources/LittleGuys_Locomotion.controller"
        };

        private static readonly (string state, string file)[] CombatClips =
        {
            ("Punch1", "Punch1.fbx"),
            ("Punch2", "Punch2.fbx"),
            ("Punch3", "Punch3.fbx"),
            ("Punch4", "Punch4.fbx"),
            ("Punch5", "Punch5.fbx"),
            ("Hit1", "Hit1.fbx"),
            ("Hit2", "Hit2.fbx"),
            ("HeavyHit", "HeavyHit.fbx"),
            ("Knockdown", "Knockdown.fbx"),
            ("GetUp", "GetUp.fbx")
        };

        static MeleeAnimationAutoSetup()
        {
            EditorApplication.delayCall += InstallIfPossible;
        }

        [DidReloadScripts]
        private static void AfterScriptsReload()
        {
            EditorApplication.delayCall += InstallIfPossible;
        }

        private static void InstallIfPossible()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var resolved = new Dictionary<string, AnimationClip>();
            foreach (var entry in CombatClips)
            {
                AnimationClip clip = LoadClipFromFbx(entry.file);
                if (clip == null)
                {
                    Debug.LogError($"[CYDOY MELEE AUTO] Missing/invalid combat animation: {AnimationFolder}/{entry.file}");
                    return;
                }
                resolved[entry.state] = clip;
            }

            bool changedAny = false;
            foreach (string path in Controllers)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    Debug.LogError("[CYDOY MELEE AUTO] AnimatorController not found: " + path);
                    continue;
                }

                bool changed = false;
                foreach (var entry in CombatClips)
                    changed |= EnsureState(controller, entry.state, resolved[entry.state]);

                if (changed)
                {
                    EditorUtility.SetDirty(controller);
                    changedAny = true;
                }

                bool verified = CombatClips.All(e => HasState(controller, e.state));
                Debug.Log($"[CYDOY MELEE AUTO] VERIFIED {controller.name}: extended combat states={verified} (Punch1-5, Hit1-2, HeavyHit, Knockdown, GetUp).");
            }

            if (changedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[CYDOY MELEE AUTO] Extended combat animation states saved permanently.");
            }
        }

        private static AnimationClip LoadClipFromFbx(string fileName)
        {
            string path = AnimationFolder + "/" + fileName;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) return null;

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
            AnimationClip exact = clips.FirstOrDefault(c => Normalize(c.name) == Normalize(stem));
            return exact != null ? exact : clips.FirstOrDefault();
        }

        private static bool EnsureState(AnimatorController controller, string stateName, AnimationClip clip)
        {
            if (controller.layers == null || controller.layers.Length == 0) return false;
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

            state.speed = 1f;
            state.writeDefaultValues = true;
            return changed;
        }

        private static bool HasState(AnimatorController controller, string name)
        {
            return controller != null && controller.layers != null && controller.layers.Length > 0 &&
                   controller.layers[0].stateMachine.states.Any(s => s.state != null && s.state.name == name);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        public static bool IsCombatFile(string file)
        {
            return CombatClips.Any(e => string.Equals(e.file, file, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class MeleeAnimationImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            string file = System.IO.Path.GetFileName(assetPath);
            if (!MeleeAnimationAutoSetup.IsCombatFile(file)) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
        }

        private void OnPostprocessModel(GameObject go)
        {
            string file = System.IO.Path.GetFileName(assetPath);
            if (!MeleeAnimationAutoSetup.IsCombatFile(file)) return;
            EditorApplication.delayCall += TouchControllers;
        }

        private static void TouchControllers()
        {
            foreach (string path in new[]
            {
                "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller",
                "Assets/Resources/LittleGuys_Locomotion.controller"
            })
            {
                UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj != null) EditorUtility.SetDirty(obj);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
