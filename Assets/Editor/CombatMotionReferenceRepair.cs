using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class CombatMotionReferenceRepair
    {
        private const string PlayerControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";

        // These are the original GUIDs from the last known-good pre-merge state.
        private static readonly (string state, string guid)[] PlayerPunches =
        {
            ("Punch3", "2230324fee62f4d24bb9e869da179d34"),
            ("Punch4", "4ab6833bd527543f0ac256607ec50027"),
            ("Punch5", "69383b351bb11443d81ca96129218cd7")
        };

        static CombatMotionReferenceRepair()
        {
            EditorApplication.delayCall += Repair;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += Repair;
        }

        private static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Repair;
                return;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null || controller.layers == null || controller.layers.Length == 0)
                return;

            bool changed = false;
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            foreach (var entry in PlayerPunches)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogError($"[CYDOY COMBAT REPAIR] GUID for {entry.state} cannot be resolved: {entry.guid}");
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__"));

                if (clip == null)
                {
                    Debug.LogError($"[CYDOY COMBAT REPAIR] No AnimationClip found inside {assetPath} for {entry.state}.");
                    continue;
                }

                AnimatorState state = machine.states
                    .Select(s => s.state)
                    .FirstOrDefault(s => s != null && s.name == entry.state);

                if (state == null)
                {
                    state = machine.AddState(entry.state);
                    changed = true;
                }

                if (state.motion != clip)
                {
                    state.motion = clip;
                    changed = true;
                }

                state.speed = 1f;
                state.writeDefaultValues = true;

                Debug.Log($"[CYDOY COMBAT REPAIR] {entry.state} -> {assetPath} -> clip '{clip.name}'", clip);
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            bool ok = PlayerPunches.All(entry =>
            {
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == entry.state);
                return state != null && state.motion != null;
            });

            if (ok)
                Debug.Log("[CYDOY COMBAT REPAIR] SUCCESS: Punch3, Punch4 and Punch5 all have real motion clips again.");
        }
    }
}
