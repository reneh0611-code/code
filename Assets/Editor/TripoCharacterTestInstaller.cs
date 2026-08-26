using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class TripoCharacterTestInstaller
    {
        private const string CharacterPath = "Assets/Models/Characters/TripoTest/TripoCharacter.fbx";
        private const string BaseColorPath = "Assets/Models/Characters/TripoTest/Textures/BaseColor.jpeg";
        private const string NormalPath = "Assets/Models/Characters/TripoTest/Textures/Normal_Bake.png";
        private const string MetallicPath = "Assets/Models/Characters/TripoTest/Textures/Metallic.PNG";
        private const string RoughnessPath = "Assets/Models/Characters/TripoTest/Textures/Roughness.PNG";
        private const string MaterialPath = "Assets/Models/Characters/TripoTest/TripoCharacter_URP.mat";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string NpcRootName = "Generated_Tripo_NPCs";
        private const float PlayerTargetHeight = 1.82f;

        [MenuItem("Tools/CYDOY/Tripo Test/Install Tripo As Player")]
        public static void InstallPlayer()
        {
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (characterAsset == null)
            {
                ShowMissingAssets();
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Tripo Test", "Player.prefab was not found.", "OK");
                return;
            }

            PrepareImportSettings();
            Material material = CreateOrUpdateMaterial();

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform oldVisual = prefabRoot.transform.Find("CharacterVisual");
                if (oldVisual != null)
                    Object.DestroyImmediate(oldVisual.gameObject);

                GameObject visualRoot = new("CharacterVisual");
                visualRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject model = PrefabUtility.InstantiatePrefab(characterAsset) as GameObject;
                if (model == null)
                    model = Object.Instantiate(characterAsset);

                model.name = "Tripo_Test_Character";
                model.transform.SetParent(visualRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.SetActive(true);

                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                ApplyMaterial(model, material);
                NormalizeHeight(model.transform, PlayerTargetHeight);

                Animator animator = model.GetComponentInChildren<Animator>(true);
                RuntimeAnimatorController humanoidController = null;
                bool humanoidReady = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;

                if (humanoidReady)
                {
                    humanoidController = LittleGuysAnimationInstaller.EnsureController();
                    if (humanoidController != null)
                    {
                        animator.runtimeAnimatorController = humanoidController;
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.updateMode = AnimatorUpdateMode.Normal;
                        animator.enabled = true;
                    }
                }

                CharacterAnimationDriver driver = prefabRoot.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject driverSO = new(driver);
                    driverSO.FindProperty("animator").objectReferenceValue = humanoidReady ? animator : null;
                    driverSO.FindProperty("fallbackController").objectReferenceValue = humanoidReady ? humanoidController : null;
                    driverSO.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = humanoidReady && humanoidController != null;
                }

                CharacterController controller = prefabRoot.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.height = 1.90f;
                    controller.radius = 0.34f;
                    controller.center = new Vector3(0f, 0.95f, 0f);
                    controller.stepOffset = 0.30f;
                }

                MixamoRuntimePoseAndGrounder grounder = prefabRoot.GetComponent<MixamoRuntimePoseAndGrounder>();
                if (grounder == null)
                    grounder = prefabRoot.AddComponent<MixamoRuntimePoseAndGrounder>();

                SerializedObject groundSO = new(grounder);
                groundSO.FindProperty("animator").objectReferenceValue = animator;
                groundSO.FindProperty("modelRoot").objectReferenceValue = model.transform;
                groundSO.FindProperty("characterController").objectReferenceValue = controller;
                groundSO.FindProperty("settleFrames").intValue = humanoidReady ? 2 : 0;
                groundSO.FindProperty("soleOffset").floatValue = 0f;
                groundSO.ApplyModifiedPropertiesWithoutUndo();
                grounder.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string animationStatus = humanoidReady && humanoidController != null
                    ? "Humanoid rig detected: Idle/Walk/Run enabled."
                    : "No usable Humanoid rig detected: visual test works, but animation is disabled.";

                EditorUtility.DisplayDialog(
                    "CYDOY · Tripo Player Test",
                    "Tripo is now the Player visual.\n\n" + animationStatus + "\n\nMovement, HUD, camera and networking were not replaced.",
                    "Test it");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Spawn Tripo NPCs")]
        public static void SpawnNpcs()
        {
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (characterAsset == null)
            {
                ShowMissingAssets();
                return;
            }

            PrepareImportSettings();
            Material material = CreateOrUpdateMaterial();
            RuntimeAnimatorController humanoidController = LittleGuysAnimationInstaller.EnsureController();

            RemoveNpcs();
            GameObject root = new(NpcRootName);

            const int count = 6;
            int created = 0;
            for (int attempt = 0; attempt < 160 && created < count; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * 28f;
                if (circle.magnitude < 8f)
                    continue;

                Vector3 origin = new(circle.x, 50f, circle.y);
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                string hitName = hit.collider != null ? hit.collider.name.ToLowerInvariant() : string.Empty;
                if (hitName.Contains("roof") || hitName.Contains("wall") || hitName.Contains("building"))
                    continue;

                GameObject npc = PrefabUtility.InstantiatePrefab(characterAsset) as GameObject;
                if (npc == null)
                    npc = Object.Instantiate(characterAsset);

                npc.name = $"Tripo_NPC_{created + 1:00}";
                npc.transform.SetParent(root.transform, true);
                npc.transform.position = hit.point + Vector3.up * 2f;
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.transform.localScale = Vector3.one;
                npc.SetActive(true);

                foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                ApplyMaterial(npc, material);
                NormalizeHeight(npc.transform, Random.Range(1.68f, 1.84f));
                SnapVisualToGround(npc, hit.point.y);

                Animator animator = npc.GetComponentInChildren<Animator>(true);
                bool humanoidReady = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                if (humanoidReady && humanoidController != null)
                {
                    animator.runtimeAnimatorController = humanoidController;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.enabled = true;
                    animator.Rebind();
                    animator.Update(0f);
                }

                Bounds bounds = GetBounds(npc);
                CharacterController cc = npc.AddComponent<CharacterController>();
                cc.height = Mathf.Max(1.0f, bounds.size.y * 0.92f);
                cc.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.23f, 0.20f, 0.38f);
                Vector3 localCenter = npc.transform.InverseTransformPoint(bounds.center);
                cc.center = localCenter;
                cc.stepOffset = Mathf.Min(0.22f, cc.height * 0.12f);
                cc.skinWidth = 0.035f;

                NPCWanderer wander = npc.AddComponent<NPCWanderer>();
                wander.Configure(Random.Range(1.05f, 1.30f), Random.Range(7f, 12f));
                wander.enabled = humanoidReady && humanoidController != null;

                SnapVisualToGround(npc, hit.point.y);
                created++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog(
                "CYDOY · Tripo NPC Test",
                $"Spawned {created} Tripo test NPCs.\n\nIf the FBX has a valid Humanoid rig they wander with Idle/Walk; otherwise they remain static for visual comparison.",
                "OK");
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Remove Tripo NPCs")]
        public static void RemoveNpcs()
        {
            GameObject root = GameObject.Find(NpcRootName);
            if (root != null)
                Object.DestroyImmediate(root);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Restore AJ Player")]
        public static void RestoreAj()
        {
            MixamoCharacterInstaller.Install();
        }

        private static void PrepareImportSettings()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer != null)
            {
                importer.importMaterials = false;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }

            TextureImporter normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.sRGBTexture = false;
                normalImporter.SaveAndReimport();
            }

            SetLinearTexture(MetallicPath);
            SetLinearTexture(RoughnessPath);
        }

        private static void SetLinearTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "TripoCharacter_URP" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            if (normal != null)
            {
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            // Tripo supplied separate roughness/metallic maps. For this visual test we keep
            // the material matte and non-metallic rather than packing maps destructively.
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.22f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.22f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            material.renderQueue = 2000;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            if (material == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                    mats = new Material[1];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = material;
                renderer.sharedMaterials = mats;
                renderer.enabled = true;
            }
        }

        private static void NormalizeHeight(Transform root, float targetHeight)
        {
            if (!TryGetBounds(root.gameObject, out Bounds bounds)) return;
            if (bounds.size.y <= 0.001f) return;
            root.localScale *= targetHeight / bounds.size.y;
        }

        private static Bounds GetBounds(GameObject root)
        {
            if (TryGetBounds(root, out Bounds b)) return b;
            return new Bounds(root.transform.position + Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static void SnapVisualToGround(GameObject root, float groundY)
        {
            if (!TryGetBounds(root, out Bounds bounds)) return;
            root.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static void ShowMissingAssets()
        {
            EditorUtility.DisplayDialog(
                "CYDOY · Tripo Test",
                "Tripo test files are missing.\n\nExtract the TripoTest Unity bundle into the PROJECT ROOT so this exists:\n\n" + CharacterPath,
                "OK");
        }
    }
}
