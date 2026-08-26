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
        private const string ControllerPath = "Assets/Resources/AJ_Locomotion.controller";
        private const string RootName = "Generated_NPCs";

        private static readonly string[] CharacterPrefabPaths =
        {
            "Assets/LuceedStudio/Character Lab/Little Guys/Little Guys - Free Sample/Woman/Free Woman/Free Woman.prefab",
            "Assets/LuceedStudio/Character Lab/Little Guys/Little Guys - Free Sample/Woman/Free Woman/Free Woman Tall.prefab",
            "Assets/LuceedStudio/Character Lab/Little Guys/Little Guys - Free Sample/Man/Free Man/Free Man.prefab",
            "Assets/LuceedStudio/Character Lab/Little Guys/Little Guys - Free Sample/Man/Free Man/Free Man Tall.prefab"
        };

        [MenuItem("Tools/CYDOY/Populate World With NPCs")]
        public static void Populate()
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            List<GameObject> characterPrefabs = new();

            foreach (string path in CharacterPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    characterPrefabs.Add(prefab);
                else
                    Debug.LogWarning("[CYDOY] NPC prefab missing: " + path);
            }

            if (characterPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · NPCs",
                    "None of the Little Guys character prefabs could be found. Make sure the Free Sample pack is imported.",
                    "OK");
                return;
            }

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · NPCs",
                    "AJ_Locomotion.controller is missing.",
                    "OK");
                return;
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Populate NPCs");

            const int targetCount = 8;
            List<Vector3> used = new();
            int created = 0;

            for (int attempt = 0; attempt < 200 && created < targetCount; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * 30f;
                if (circle.magnitude < 8f)
                    continue;

                Vector3 rayOrigin = new(circle.x, 50f, circle.y);
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                string hitName = hit.collider != null ? hit.collider.name.ToLowerInvariant() : string.Empty;
                if (hitName.Contains("roof") || hitName.Contains("wall") || hitName.Contains("building"))
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

                GameObject sourcePrefab = characterPrefabs[Random.Range(0, characterPrefabs.Count)];
                GameObject npc = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
                if (npc == null)
                    npc = Object.Instantiate(sourcePrefab);

                npc.name = $"NPC_{sourcePrefab.name.Replace(" ", "_")}_{created + 1:00}";
                npc.transform.SetParent(root.transform, true);
                npc.transform.position = hit.point;
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.SetActive(true);

                foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

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

                Bounds bounds = GetRendererBounds(npc);
                float visualHeight = Mathf.Max(1.2f, bounds.size.y);
                cc.height = visualHeight * 0.92f;
                cc.radius = Mathf.Clamp(bounds.size.x * 0.32f, 0.22f, 0.38f);
                cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
                cc.stepOffset = Mathf.Min(0.25f, cc.height * 0.15f);
                cc.skinWidth = 0.04f;

                NPCWanderer wander = npc.GetComponent<NPCWanderer>();
                if (wander == null)
                    wander = npc.AddComponent<NPCWanderer>();

                bool tall = sourcePrefab.name.ToLowerInvariant().Contains("tall");
                float speed = tall ? Random.Range(1.25f, 1.50f) : Random.Range(1.15f, 1.40f);
                wander.Configure(speed, Random.Range(7f, 12f));

                // Do NOT add NPCAppearanceRandomizer here. The new character pack owns its
                // materials/textures, so skin and clothing remain exactly as authored.
                SnapToGround(npc, hit.point.y);

                used.Add(npc.transform.position);
                created++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog(
                "CYDOY · NPCs",
                $"Placed {created} Little Guys NPCs using Free Woman, Free Woman Tall, Free Man and Free Man Tall.\n\nPlayer, hub, camera and network objects were not modified.",
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

        private static Bounds GetRendererBounds(GameObject npc)
        {
            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(npc.transform.position + Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
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
