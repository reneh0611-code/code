using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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

        private static void InstallIfPossible()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            AnimationClip punch1 = FindClip("Punch1");
            AnimationClip punch2 = FindClip("Punch2");
            AnimationClip hit1 = FindClip("Hit1");
            AnimationClip hit2 = FindClip("Hit2");

            if (punch1 == null || punch2 == null || hit1 == null || hit2 == null)
            {
                Debug.LogWarning($"[CYDOY MELEE AUTO] Waiting for combat clips. Punch1={punch1 != null}, Punch2={punch2 != null}, Hit1={hit1 != null}, Hit2={hit2 != null}");
                return;
            }

            bool changedAny = false;
            foreach (string path in Controllers)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;

                bool changed = false;
                changed |= EnsureState(controller, "Punch1", punch1);
                changed |= EnsureState(controller, "Punch2", punch2);
                changed |= EnsureState(controller, "Hit1", hit1);
                changed |= EnsureState(controller, "Hit2", hit2);

                if (changed)
                {
                    EditorUtility.SetDirty(controller);
                    changedAny = true;
                    Debug.Log($"[CYDOY MELEE AUTO] Wired Punch1/Punch2/Hit1/Hit2 into {controller.name}.");
                }
            }

            if (changedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static AnimationClip FindClip(string wanted)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationFolder });
            var clips = guids
                .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<AnimationClip>())
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            AnimationClip exact = clips.FirstOrDefault(c => string.Equals(Normalize(c.name), Normalize(wanted), StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            return clips.FirstOrDefault(c => Normalize(c.name).Contains(Normalize(wanted), StringComparison.OrdinalIgnoreCase));
        }

        private static bool EnsureState(AnimatorController controller, string stateName, AnimationClip clip)
        {
            if (controller.layers == null || controller.layers.Length == 0) return false;
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState existing = machine.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == stateName);

            if (existing == null)
            {
                existing = machine.AddState(stateName);
                existing.motion = clip;
                existing.speed = 1f;
                existing.writeDefaultValues = true;
                return true;
            }

            if (existing.motion != clip)
            {
                existing.motion = clip;
                existing.speed = 1f;
                return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }
    }
}
