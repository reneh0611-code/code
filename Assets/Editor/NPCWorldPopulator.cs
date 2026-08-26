using System.Collections.Generic;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class NPCWorldPopulator
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/AJ_Locomotion.controller";
        private const string RootName = "Generated_NPCs";

        [MenuItem("Tools/CYDOY/Populate World With NPCs")]
        public static void Populate()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            Transform sourceAj = playerPrefab != null ? FindRecursive(playerPrefab.transform, "Mixamo_AJ") : null;

            if (sourceAj == null || controller == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · NPCs",
                    "The textured Mixamo_AJ inside Player.prefab or AJ_Locomotion.controller is missing.",
                    "OK");
                return;
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Populate NPCs");

            const int targetCount = 7;
            List<Vector3> used = new();
            int created = 0;
            int children = 0;

            for (int attempt = 0; attempt < 160 && created < targetCount; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * 30f;
                if (circle.magnitude < 8f)
                    continue;

                Vector3 rayOrigin = new(circle.x, 50f, circle.y);
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                string n = hit.collider != null ? hit.collider.name.ToLowerInvariant() : string.Empty;
                if (n.Contains("roof") || n.Contains("wall") || n.Contains("building"))
                    continue;

                bool tooClose = false;
                foreach (Vector3 p in used)
                {
                    if ((p - hit.point).sqrMagnitude < 5.5f * 5.5f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                bool isChild = children < 2 && created >= 2 && Random.value < 0.25f;
                float scale = isChild ? Random.Range(0.60f, 0.72f) : Random.Range(0.92f, 1.07f);

                // Clone ONLY the already-working AJ visual child from Player.prefab.
                // This carries AJ's real materials/textures but none of the Player root's
                // network, camera, UI or movement components.
                GameObject npc = Object.Instantiate(sourceAj.gameObject);
                npc.name = isChild ? $"NPC_Child_{created + 1:00}" : $"NPC_Adult_{created + 1:00}";
                npc.transform.SetParent(root.transform, true);
                npc.transform.position = hit.point;
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.transform.localScale = sourceAj.localScale * scale;
                npc.SetActive(true);

                foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                // The source visual may contain renderer states inherited from the prefab.
                // NPCs should always render every AJ mesh; we never change the Player prefab itself.
                foreach (SkinnedMeshRenderer renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer != null)
                        renderer.enabled = true;
                }

                Animator animator = npc.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = npc.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                CharacterController cc = npc.GetComponent<CharacterController>();
                if (cc == null)
                    cc = npc.AddComponent<CharacterController>();
                cc.height = 1.82f;
                cc.radius = 0.30f;
                cc.center = new Vector3(0f, 0.91f, 0f);
                cc.stepOffset = 0.25f;
                cc.skinWidth = 0.04f;

                NPCWanderer wander = npc.GetComponent<NPCWanderer>();
                if (wander == null)
                    wander = npc.AddComponent<NPCWanderer>();
                wander.Configure(isChild ? Random.Range(0.95f, 1.15f) : Random.Range(1.20f, 1.50f), Random.Range(7f, 12f));

                if (npc.GetComponent<NPCAppearanceRandomizer>() == null)
                    npc.AddComponent<NPCAppearanceRandomizer>();

                SnapToGround(npc, hit.point.y);

                used.Add(npc.transform.position);
                created++;
                if (isChild) children++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog(
                "CYDOY · NPCs",
                $"Placed {created} isolated NPCs using the Player AJ's working textures.\n\nPlayer.prefab, hub, camera and network objects were not modified.",
                "OK");
        }

        [MenuItem("Tools/CYDOY/Remove Generated NPCs")]
        public static void RemoveGenerated()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing == null)
                return;

            Object.DestroyImmediate(existing);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static Transform FindRecursive(Transform root, string targetName)
        {
            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void SnapToGround(GameObject npc, float groundY)
        {
            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            npc.transform.position += Vector3.up * (groundY - bounds.min.y);
        }
    }
}
