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

            Transform sourceAjTransform = playerPrefab != null ? FindRecursive(playerPrefab.transform, "Mixamo_AJ") : null;
            GameObject sourceAj = sourceAjTransform != null ? sourceAjTransform.gameObject : null;

            if (sourceAj == null || controller == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · NPC Population",
                    "The textured Mixamo_AJ inside Player.prefab or AJ_Locomotion.controller is missing.\n\nMake sure AJ is installed and textured first.",
                    "OK");
                return;
            }

            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Populate NPCs");

            const int targetCount = 8;
            List<Vector3> used = new();
            int created = 0;
            int childCount = 0;
            int cappedCount = 0;

            for (int attempt = 0; attempt < 120 && created < targetCount; attempt++)
            {
                Vector2 planar = Random.insideUnitCircle * 32f;
                if (planar.magnitude < 8f)
                    continue;

                Vector3 castOrigin = new(planar.x, 40f, planar.y);
                if (!Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                if (hit.collider == null)
                    continue;

                string colliderName = hit.collider.name.ToLowerInvariant();
                if (colliderName.Contains("roof") || colliderName.Contains("wall") || colliderName.Contains("building"))
                    continue;

                Vector3 position = hit.point;
                bool tooClose = false;
                foreach (Vector3 existing in used)
                {
                    if ((existing - position).sqrMagnitude < 5.5f * 5.5f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                bool isChild = created >= 2 && childCount < 2 && Random.value < 0.28f;
                bool hasCap = !isChild && cappedCount < 2 && Random.value < 0.30f;

                float scale = isChild
                    ? Random.Range(0.58f, 0.72f)
                    : Random.Range(0.90f, 1.08f);

                GameObject npc = Object.Instantiate(sourceAj);
                npc.name = isChild ? $"NPC_Child_{created + 1:00}" : $"NPC_Adult_{created + 1:00}";
                npc.transform.SetParent(root.transform);
                npc.transform.position = position;
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.transform.localScale = sourceAj.transform.localScale * scale;
                npc.SetActive(true);

                RemoveModelColliders(npc);
                ForceCharacterVisible(npc);

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

                NPCWanderer wanderer = npc.GetComponent<NPCWanderer>();
                if (wanderer == null)
                    wanderer = npc.AddComponent<NPCWanderer>();
                float speed = isChild ? Random.Range(1.0f, 1.25f) : Random.Range(1.25f, 1.65f);
                float radius = Random.Range(7f, 13f);
                wanderer.Configure(speed, radius);

                NPCAppearanceRandomizer appearance = npc.GetComponent<NPCAppearanceRandomizer>();
                if (appearance == null)
                    appearance = npc.AddComponent<NPCAppearanceRandomizer>();
                appearance.Configure(isChild, hasCap);

                SnapVisualBottomToGround(npc, hit.point.y);

                used.Add(npc.transform.position);
                created++;
                if (isChild) childCount++;
                if (hasCap) cappedCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog(
                "CYDOY · NPC Population",
                $"Placed {created} visible NPCs from the textured Player AJ.\n\nNo renderer or rig bone is disabled during NPC creation.",
                "Nice");
        }

        [MenuItem("Tools/CYDOY/Remove Generated NPCs")]
        public static void RemoveGenerated()
        {
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
        }

        private static void RemoveModelColliders(GameObject npc)
        {
            foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void ForceCharacterVisible(GameObject npc)
        {
            foreach (SkinnedMeshRenderer renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
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

        private static void SnapVisualBottomToGround(GameObject npc, float groundY)
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
