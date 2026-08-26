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
            if (characterAsset == null) { ShowMissingAssets(); return; }
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) { EditorUtility.DisplayDialog("CYDOY · Tripo Test", "Player.prefab was not found.", "OK"); return; }

            PrepareImportSettings();
            characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Material material = CreateOrUpdateMaterial();
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform oldVisual = prefabRoot.transform.Find("CharacterVisual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                GameObject visualRoot = new("CharacterVisual");
                visualRoot.transform.SetParent(prefabRoot.transform, false);
                GameObject model = PrefabUtility.InstantiatePrefab(characterAsset) as GameObject ?? Object.Instantiate(characterAsset);
                model.name = "Tripo_Test_Character";
                model.transform.SetParent(visualRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.SetActive(true);
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                ApplyMaterial(model, material);
                NormalizeHeight(model.transform, PlayerTargetHeight);

                Animator animator = model.GetComponentInChildren<Animator>(true);
                RuntimeAnimatorController controllerAsset = null;
                bool humanoidReady = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                if (humanoidReady)
                {
                    controllerAsset = LittleGuysAnimationInstaller.EnsureController();
                    if (controllerAsset != null)
                    {
                        animator.runtimeAnimatorController = controllerAsset;
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.enabled = true;
                    }
                }

                CharacterAnimationDriver driver = prefabRoot.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject so = new(driver);
                    so.FindProperty("animator").objectReferenceValue = humanoidReady ? animator : null;
                    so.FindProperty("fallbackController").objectReferenceValue = humanoidReady ? controllerAsset : null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = humanoidReady && controllerAsset != null;
                }

                CharacterController cc = prefabRoot.GetComponent<CharacterController>();
                if (cc != null) { cc.height = 1.90f; cc.radius = 0.34f; cc.center = new Vector3(0f, .95f, 0f); cc.stepOffset = .30f; }
                MixamoRuntimePoseAndGrounder grounder = prefabRoot.GetComponent<MixamoRuntimePoseAndGrounder>() ?? prefabRoot.AddComponent<MixamoRuntimePoseAndGrounder>();
                SerializedObject gso = new(grounder);
                gso.FindProperty("animator").objectReferenceValue = animator;
                gso.FindProperty("modelRoot").objectReferenceValue = model.transform;
                gso.FindProperty("characterController").objectReferenceValue = cc;
                gso.FindProperty("settleFrames").intValue = humanoidReady ? 2 : 0;
                gso.FindProperty("soleOffset").floatValue = 0f;
                gso.ApplyModifiedPropertiesWithoutUndo();
                grounder.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("CYDOY · Tripo Player Test", "Tripo is now the Player visual.\n\n" + (humanoidReady && controllerAsset != null ? "Humanoid rig detected: animations enabled." : "Visual installed. No usable Humanoid rig detected yet."), "Test it");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Spawn Tripo NPCs")]
        public static void SpawnNpcs()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (asset == null) { ShowMissingAssets(); return; }
            PrepareImportSettings();
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Material material = CreateOrUpdateMaterial();
            RuntimeAnimatorController animController = LittleGuysAnimationInstaller.EnsureController();
            RemoveNpcs();
            GameObject root = new(NpcRootName);
            int created = 0;
            for (int attempt = 0; attempt < 160 && created < 6; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * 28f;
                if (circle.magnitude < 8f) continue;
                if (!Physics.Raycast(new Vector3(circle.x, 50f, circle.y), Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore)) continue;
                string hn = hit.collider ? hit.collider.name.ToLowerInvariant() : "";
                if (hn.Contains("roof") || hn.Contains("wall") || hn.Contains("building")) continue;
                GameObject npc = PrefabUtility.InstantiatePrefab(asset) as GameObject ?? Object.Instantiate(asset);
                npc.name = $"Tripo_NPC_{created + 1:00}";
                npc.transform.SetParent(root.transform, true);
                npc.transform.position = hit.point + Vector3.up * 2f;
                npc.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                foreach (Collider c in npc.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                ApplyMaterial(npc, material);
                NormalizeHeight(npc.transform, Random.Range(1.68f, 1.84f));
                SnapVisualToGround(npc, hit.point.y);
                Animator animator = npc.GetComponentInChildren<Animator>(true);
                bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                if (human && animController != null) { animator.runtimeAnimatorController = animController; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true; animator.Rebind(); animator.Update(0); }
                Bounds b = GetBounds(npc);
                CharacterController cc = npc.AddComponent<CharacterController>();
                cc.height = Mathf.Max(1f, b.size.y * .92f); cc.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * .23f, .2f, .38f); cc.center = npc.transform.InverseTransformPoint(b.center); cc.stepOffset = Mathf.Min(.22f, cc.height * .12f); cc.skinWidth = .035f;
                NPCWanderer wander = npc.AddComponent<NPCWanderer>();
                wander.Configure(Random.Range(1.05f, 1.30f), Random.Range(7f, 12f)); wander.enabled = human && animController != null;
                SnapVisualToGround(npc, hit.point.y);
                created++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("CYDOY · Tripo NPC Test", $"Spawned {created} Tripo test NPCs.", "OK");
        }

        [MenuItem("Tools/CYDOY/Tripo Test/Remove Tripo NPCs")]
        public static void RemoveNpcs() { GameObject r = GameObject.Find(NpcRootName); if (r) Object.DestroyImmediate(r); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); }
        [MenuItem("Tools/CYDOY/Tripo Test/Restore AJ Player")]
        public static void RestoreAj() => MixamoCharacterInstaller.Install();

        private static void PrepareImportSettings()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer != null)
            {
                // Unity 6 removed ModelImporter.importMaterials. We assign our own PBR material,
                // so no material-import API call is needed here.
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            TextureImporter normal = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normal != null) { normal.textureType = TextureImporterType.NormalMap; normal.sRGBTexture = false; normal.SaveAndReimport(); }
            SetLinearTexture(MetallicPath); SetLinearTexture(RoughnessPath);
        }

        private static void SetLinearTexture(string path) { TextureImporter i = AssetImporter.GetAtPath(path) as TextureImporter; if (i == null) return; i.sRGBTexture = false; i.SaveAndReimport(); }
        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null) { m = new Material(shader) { name = "TripoCharacter_URP" }; AssetDatabase.CreateAsset(m, MaterialPath); } else if (m.shader != shader) m.shader = shader;
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath), normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", color); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", color); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white); if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (normal != null) { if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", normal); if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .22f); if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .22f); m.renderQueue = 2000;
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); return m;
        }
        private static void ApplyMaterial(GameObject root, Material m) { if (!m) return; foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) { Material[] a = r.sharedMaterials; if (a == null || a.Length == 0) a = new Material[1]; for (int i = 0; i < a.Length; i++) a[i] = m; r.sharedMaterials = a; r.enabled = true; } }
        private static void NormalizeHeight(Transform root, float h) { if (!TryGetBounds(root.gameObject, out Bounds b) || b.size.y <= .001f) return; root.localScale *= h / b.size.y; }
        private static Bounds GetBounds(GameObject root) { return TryGetBounds(root, out Bounds b) ? b : new Bounds(root.transform.position + Vector3.up, new Vector3(.6f, 1.8f, .6f)); }
        private static bool TryGetBounds(GameObject root, out Bounds b) { Renderer[] rs = root.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) { b = default; return false; } b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return true; }
        private static void SnapVisualToGround(GameObject root, float y) { if (TryGetBounds(root, out Bounds b)) root.transform.position += Vector3.up * (y - b.min.y); }
        private static void ShowMissingAssets() => EditorUtility.DisplayDialog("CYDOY · Tripo Test", "Tripo test files are missing. Expected:\n\n" + CharacterPath, "OK");
    }
}
