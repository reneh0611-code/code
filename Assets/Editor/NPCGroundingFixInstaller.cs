using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class NPCGroundingFixInstaller
    {
        [MenuItem("Tools/CYDOY/Tripo Test/Fix NPC Grounding Only")]
        public static void Fix()
        {
            NPCWanderer[] npcs = Object.FindObjectsByType<NPCWanderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;

            foreach (NPCWanderer wanderer in npcs)
            {
                GameObject npc = wanderer.gameObject;

                // Remove previous visual/world grounders so they cannot fight this solution.
                foreach (FixedWorldVisualGrounder old in npc.GetComponentsInChildren<FixedWorldVisualGrounder>(true))
                    Object.DestroyImmediate(old);

                Transform visualRoot = FindVisualRoot(npc.transform);
                if (visualRoot == null)
                    continue;

                NPCVisualControllerGrounder grounder = npc.GetComponent<NPCVisualControllerGrounder>();
                if (grounder == null)
                    grounder = npc.AddComponent<NPCVisualControllerGrounder>();

                grounder.Configure(visualRoot, 0f);
                grounder.enabled = true;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[CYDOY] Installed controller-bottom grounding on {count} NPCs.");
            EditorUtility.DisplayDialog(
                "CYDOY · NPC Grounding",
                $"Prepared {count} NPCs.\n\nThis fix does NOT raycast or edit animation. It waits for each CharacterController to stand on the ground, then aligns the visible shoe-bottom exactly to the controller bottom once.",
                "Play to test");
        }

        private static Transform FindVisualRoot(Transform npcRoot)
        {
            Animator animator = npcRoot.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != npcRoot)
                return animator.transform;

            SkinnedMeshRenderer smr = npcRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) return null;

            Transform current = smr.transform;
            while (current.parent != null && current.parent != npcRoot)
                current = current.parent;
            return current;
        }
    }
}
