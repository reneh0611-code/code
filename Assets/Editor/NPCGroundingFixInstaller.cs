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

                foreach (FixedWorldVisualGrounder old in npc.GetComponentsInChildren<FixedWorldVisualGrounder>(true))
                    Object.DestroyImmediate(old);

                NPCRootGroundSnapper rootSnapper = npc.GetComponent<NPCRootGroundSnapper>();
                if (rootSnapper == null)
                    rootSnapper = npc.AddComponent<NPCRootGroundSnapper>();
                rootSnapper.enabled = true;

                Transform visualRoot = FindVisualRoot(npc.transform);
                if (visualRoot == null)
                    continue;

                NPCVisualControllerGrounder visualGrounder = npc.GetComponent<NPCVisualControllerGrounder>();
                if (visualGrounder == null)
                    visualGrounder = npc.AddComponent<NPCVisualControllerGrounder>();

                // Runs after the root snapper settles; visual is then aligned to the correctly grounded capsule.
                visualGrounder.Configure(visualRoot, 0f);
                visualGrounder.enabled = true;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[CYDOY] Installed ROOT + VISUAL grounding on {count} NPCs.");
            EditorUtility.DisplayDialog(
                "CYDOY · NPC Grounding",
                $"Prepared {count} NPCs.\n\nEach NPC now does two steps at runtime:\n1) snap the CharacterController root to the real horizontal road/sidewalk surface\n2) align the visible shoe-bottom to that grounded controller\n\nWalls/roofs/building surfaces are ignored. Animations are untouched.",
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
