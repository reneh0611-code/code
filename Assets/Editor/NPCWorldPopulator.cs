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
            List<GameObject> characterPrefabs = new();
            foreach (string path in CharacterPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) characterPrefabs.Add(prefab);
            }

            if (characterPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("CYDOY · NPCs", "Little Guys prefabs were not found.", "OK");
                return;
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Populate NPCs");

            const int targetCount = 8;
            List<Vector3> used = new();
            int created = 0;

            for (int attempt = 0; attempt < 220 && created < targetCount; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * 30f;
                if (circle.magnitude < 8f) continue;

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
                if (tooClose) continue;

                GameObject sourcePrefab = characterPrefabs[Random.Range(0, characterPrefabs.Count)];
                GameObject npc = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
                if (npc == null) npc = Object.Instantiate(sourcePrefab);

                npc.name = $"NPC_{sourcePrefab.name.Replace(" ", "_")}_{created + 1:00}";
                npc.transform.SetParent(root.transform, true);
                npc.transform.position = hit.point + Vector3.up * 3f;
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.SetActive(true);

                foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                foreach (SkinnedMeshRenderer renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null) continue;
                    renderer.enabled = true;
                    ForceOpaqueUrpMaterial(renderer);
                }

                Animator animator = npc.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.enabled = true;
                    if (animator.runtimeAnimatorController != null)
                    {
                        animator.Rebind();
                        animator.Update(0f);
                    }
                }

                // Scale the actual rendered body to a realistic human height.
                Bounds preScaleBounds = GetRendererBounds(npc);
                bool tall = sourcePrefab.name.ToLowerInvariant().Contains("tall");
                float targetHeight = tall ? Random.Range(1.78f, 1.86f) : Random.Range(1.64f, 1.76f);
                if (preScaleBounds.size.y > 0.05f)
                {
                    float scaleMultiplier = targetHeight / preScaleBounds.size.y;
                    npc.transform.localScale *= scaleMultiplier;
                }

                // Put visible feet exactly on ground after scaling.
                SnapVisualToGround(npc, hit.point.y);

                Bounds bounds = GetRendererBounds(npc);
                float visualHeight = Mathf.Max(1.0f, bounds.size.y);
                float visualWidth = Mathf.Max(0.35f, Mathf.Max(bounds.size.x, bounds.size.z));
                Vector3 localCenter = npc.transform.InverseTransformPoint(bounds.center);

                CharacterController cc = npc.GetComponent<CharacterController>();
                if (cc == null) cc = npc.AddComponent<CharacterController>();
                cc.height = visualHeight * 0.92f;
                cc.radius = Mathf.Clamp(visualWidth * 0.27f, 0.20f, 0.40f);
                cc.center = localCenter;
                cc.stepOffset = Mathf.Min(0.24f, cc.height * 0.14f);
                cc.skinWidth = 0.035f;
                cc.minMoveDistance = 0.001f;

                NPCWanderer wander = npc.GetComponent<NPCWanderer>();
                if (wander == null) wander = npc.AddComponent<NPCWanderer>();
                wander.Configure(tall ? Random.Range(1.20f, 1.42f) : Random.Range(1.08f, 1.32f), Random.Range(7f, 12f));

                SnapVisualToGround(npc, hit.point.y);
                used.Add(npc.transform.position);
                created++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog(
                "CYDOY · NPCs",
                $"Placed {created} Little Guys NPCs with opaque URP materials, realistic height and ground snapping.\n\nPlayer/hub/camera/network were not modified.",
                "OK");
        }

        [MenuItem("Tools/CYDOY/Remove Generated NPCs")]
        public static void RemoveGenerated()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing == null) return;
            Object.DestroyImmediate(existing);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static void ForceOpaqueUrpMaterial(SkinnedMeshRenderer renderer)
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] replacements = new Material[sourceMaterials.Length];
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader fallback = Shader.Find("Standard");

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                if (source == null)
                {
                    replacements[i] = null;
                    continue;
                }

                Texture baseTexture = null;
                if (source.HasProperty("_BaseMap")) baseTexture = source.GetTexture("_BaseMap");
                if (baseTexture == null && source.HasProperty("_MainTex")) baseTexture = source.GetTexture("_MainTex");

                Material mat = new(urpLit != null ? urpLit : fallback)
                {
                    name = source.name + "_CYDOY_Opaque"
                };

                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTexture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTexture);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = 2000;
                replacements[i] = mat;
            }

            renderer.sharedMaterials = replacements;
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

        private static void SnapVisualToGround(GameObject npc, float groundY)
        {
            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            npc.transform.position += Vector3.up * (groundY - bounds.min.y);
        }
    }
}
