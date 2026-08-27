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
        private const string NpcControllerPath = "Assets/Resources/LittleGuys_Locomotion.controller";

        // Original GUIDs from the last known-good pre-merge state.
        private static readonly (string state, string guid)[] PlayerPunches =
        {
            ("Punch3", "2230324fee62f4d24bb9e869da179d34"),
            ("Punch4", "4ab6833bd527543f0ac256607ec50027"),
            ("Punch5", "69383b351bb11443d81ca96129218cd7")
        };

        private static readonly (string state, string guid)[] NpcCombatStates =
        {
            ("HeavyHit", "be60027ec4d2d45d19fd92244950e21f"),
            ("Knockdown", "9c4eade499c374d588dcd5c163c8e674"),
            ("GetUp", "6838c45d99abc4218b4bdaba54756836")
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

            bool playerOk = RepairController(PlayerControllerPath, PlayerPunches, "PLAYER");
            bool npcOk = RepairController(NpcControllerPath, NpcCombatStates, "NPC");

            if (playerOk && npcOk)
                Debug.Log("[CYDOY COMBAT REPAIR] SUCCESS: Player Punch3-5 and NPC HeavyHit/Knockdown/GetUp all have real motion clips again.");
        }

        private static bool RepairController(string controllerPath, (string state, string guid)[] entries, string label)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null || controller.layers == null || controller.layers.Length == 0)
            {
                Debug.LogError($"[CYDOY COMBAT REPAIR] {label} controller not found: {controllerPath}");
                return false;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            bool changed = false;
            bool allResolved = true;

            foreach (var entry in entries)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogError($"[CYDOY COMBAT REPAIR] {label} GUID for {entry.state} cannot be resolved: {entry.guid}");
                    allResolved = false;
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__"));

                if (clip == null)
                {
                    Debug.LogError($"[CYDOY COMBAT REPAIR] {label} no AnimationClip found inside {assetPath} for {entry.state}.");
                    allResolved = false;
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

                Debug.Log($"[CYDOY COMBAT REPAIR] {label} {entry.state} -> {assetPath} -> clip '{clip.name}'", clip);
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            bool statesOk = entries.All(entry =>
            {
                AnimatorState state = machine.states
                    .Select(s => s.state)
                    .FirstOrDefault(s => s != null && s.name == entry.state);
                return state != null && state.motion != null;
            });

            return allResolved && statesOk;
        }
    }
}
