using System;
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

            // IMPORTANT: Mixamo/FBX sub-clips are often NOT named like the FBX file.
            // Therefore resolve the FBX by filename, then take its actual AnimationClip sub-asset.
            AnimationClip punch1 = LoadClipFromFbx("Punch1.fbx");
            AnimationClip punch2 = LoadClipFromFbx("Punch2.fbx");
            AnimationClip hit1 = LoadClipFromFbx("Hit1.fbx");
            AnimationClip hit2 = LoadClipFromFbx("Hit2.fbx");

            if (punch1 == null || punch2 == null || hit1 == null || hit2 == null)
            {
                Debug.LogError($"[CYDOY MELEE AUTO] Combat FBX resolution failed. Punch1={punch1 != null}, Punch2={punch2 != null}, Hit1={hit1 != null}, Hit2={hit2 != null}. Expected files directly in {AnimationFolder}.");
                return;
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
                changed |= EnsureState(controller, "Punch1", punch1);
                changed |= EnsureState(controller, "Punch2", punch2);
                changed |= EnsureState(controller, "Hit1", hit1);
                changed |= EnsureState(controller, "Hit2", hit2);

                if (changed)
                {
                    EditorUtility.SetDirty(controller);
                    changedAny = true;
                }

                Debug.Log($"[CYDOY MELEE AUTO] VERIFIED {controller.name}: Punch1={HasState(controller, "Punch1")}, Punch2={HasState(controller, "Punch2")}, Hit1={HasState(controller, "Hit1")}, Hit2={HasState(controller, "Hit2")}.");
            }

            if (changedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[CYDOY MELEE AUTO] Combat animation states saved permanently into the Animator Controllers.");
            }
        }

        private static AnimationClip LoadClipFromFbx(string fileName)
        {
            string path = AnimationFolder + "/" + fileName;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                return null;

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // FBX normally contains one usable animation clip. Prefer a clip matching the file name,
            // otherwise deliberately use the first real clip instead of failing because Mixamo renamed it.
            string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
            AnimationClip exact = clips.FirstOrDefault(c => Normalize(c.name) == Normalize(stem));
            return exact != null ? exact : clips.FirstOrDefault();
        }

        private static bool EnsureState(AnimatorController controller, string stateName, AnimationClip clip)
        {
            if (controller.layers == null || controller.layers.Length == 0)
                return false;

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states
                .Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name == stateName);

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
            if (controller == null || controller.layers == null || controller.layers.Length == 0) return false;
            return controller.layers[0].stateMachine.states.Any(s => s.state != null && s.state.name == name);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }
    }

    // Ensures combat clips behave like attacks/reactions instead of locomotion loops.
    public sealed class MeleeAnimationImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            string file = System.IO.Path.GetFileName(assetPath);
            if (file != "Punch1.fbx" && file != "Punch2.fbx" && file != "Hit1.fbx" && file != "Hit2.fbx")
                return;

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
        }

        private void OnPostprocessModel(GameObject go)
        {
            string file = System.IO.Path.GetFileName(assetPath);
            if (file != "Punch1.fbx" && file != "Punch2.fbx" && file != "Hit1.fbx" && file != "Hit2.fbx")
                return;

            EditorApplication.delayCall += InstallAfterImport;
        }

        private static void InstallAfterImport()
        {
            // Re-trigger script reload style initialization indirectly by touching the controller assets.
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
