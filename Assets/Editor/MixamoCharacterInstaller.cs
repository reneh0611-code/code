using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class MixamoCharacterInstaller
    {
        private const string CharacterPath = "Assets/Models/Characters/Aj.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const float TargetHeight = 1.82f;

        [MenuItem("Tools/CYDOY/Install Mixamo Character")]
        public static void Install()
        {
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (characterAsset == null)
            {
                EnsureFolders();
                EditorUtility.DisplayDialog("CYDOY · Mixamo Character", "Character file not found.\n\nPut Aj.fbx here:\n" + CharacterPath, "OK");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Mixamo Character", "Player prefab not found. Run Tools → CYDOY → Build Phase 1 Scene first.", "OK");
                return;
            }

            ExtractEmbeddedMaterials();

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                RemoveOldVisuals(prefabRoot.transform);

                GameObject visualRoot = new("CharacterVisual");
                visualRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject model = PrefabUtility.InstantiatePrefab(characterAsset) as GameObject;
                if (model == null)
                    model = Object.Instantiate(characterAsset);

                model.name = "Mixamo_AJ";
                model.transform.SetParent(visualRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                RemoveModelColliders(model);
                Animator animator = ConfigureAnimator(model);
                NormalizeScale(model.transform);
                ApplyFallbackMaterialsIfNeeded(model);

                CharacterController controller = prefabRoot.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.height = 1.90f;
                    controller.radius = 0.34f;
                    controller.center = new Vector3(0f, 0.95f, 0f);
                    controller.stepOffset = 0.30f;
                }

                MixamoRuntimePoseAndGrounder runtimeFix = prefabRoot.GetComponent<MixamoRuntimePoseAndGrounder>();
                if (runtimeFix == null)
                    runtimeFix = prefabRoot.AddComponent<MixamoRuntimePoseAndGrounder>();

                SerializedObject runtimeSO = new(runtimeFix);
                runtimeSO.FindProperty("animator").objectReferenceValue = animator;
                runtimeSO.FindProperty("modelRoot").objectReferenceValue = model.transform;
                runtimeSO.FindProperty("characterController").objectReferenceValue = controller;
                runtimeSO.FindProperty("applyRelaxedPoseWithoutController").boolValue = true;
                runtimeSO.FindProperty("forceGrounding").boolValue = true;
                runtimeSO.FindProperty("visualGroundOffset").floatValue = 0.008f;
                runtimeSO.ApplyModifiedPropertiesWithoutUndo();

                ThirdPersonCamera camera = prefabRoot.GetComponentInChildren<ThirdPersonCamera>(true);
                if (camera != null)
                {
                    SerializedObject cameraSO = new(camera);
                    cameraSO.FindProperty("target").objectReferenceValue = prefabRoot.transform;
                    cameraSO.FindProperty("pivotOffset").vector3Value = new Vector3(0.28f, 1.48f, 0f);
                    cameraSO.FindProperty("distance").floatValue = 1.85f;
                    cameraSO.FindProperty("pitch").floatValue = 7f;
                    cameraSO.FindProperty("followSmooth").floatValue = 20f;
                    cameraSO.FindProperty("rotationSmooth").floatValue = 18f;
                    cameraSO.FindProperty("collisionRadius").floatValue = 0.18f;
                    cameraSO.FindProperty("minimumDistance").floatValue = 0.75f;
                    cameraSO.ApplyModifiedPropertiesWithoutUndo();

                    Camera unityCamera = camera.GetComponent<Camera>();
                    if (unityCamera != null)
                    {
                        unityCamera.fieldOfView = 60f;
                        unityCamera.nearClipPlane = 0.06f;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Character",
                    "AJ installed with the existing third-person framing, grounding and animation hooks.",
                    "Nice");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ExtractEmbeddedMaterials()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null) return;

            string materialFolder = "Assets/Models/Characters/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/Models/Characters")) EnsureFolders();
            if (!AssetDatabase.IsValidFolder(materialFolder))
                AssetDatabase.CreateFolder("Assets/Models/Characters", "Materials");

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
            importer.SaveAndReimport();

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CharacterPath);
            foreach (Object asset in assets)
            {
                if (asset is not Material material) continue;
                string targetPath = materialFolder + "/" + material.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(targetPath) == null)
                    AssetDatabase.ExtractAsset(material, targetPath);
            }

            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
            importer.SaveAndReimport();
        }

        private static void RemoveOldVisuals(Transform root)
        {
            string[] candidates = { "CharacterVisual", "Visual" };
            foreach (string candidate in candidates)
            {
                Transform old = root.Find(candidate);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            StylizedCharacterAnimator primitiveAnimator = root.GetComponent<StylizedCharacterAnimator>();
            if (primitiveAnimator != null) Object.DestroyImmediate(primitiveAnimator);
        }

        private static void RemoveModelColliders(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static Animator ConfigureAnimator(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            return animator;
        }

        private static void NormalizeScale(Transform modelRoot)
        {
            if (!TryGetRendererBounds(modelRoot.gameObject, out Bounds initialBounds)) return;
            float initialHeight = initialBounds.size.y;
            if (initialHeight <= 0.001f) return;
            modelRoot.localScale = Vector3.one * (TargetHeight / initialHeight);
        }

        private static void ApplyFallbackMaterialsIfNeeded(GameObject model)
        {
            Material skin = GetOrCreateFallbackMaterial("AJ_Skin_Fallback", new Color(0.58f, 0.38f, 0.26f));
            Material cloth = GetOrCreateFallbackMaterial("AJ_Clothes_Fallback", new Color(0.10f, 0.12f, 0.16f));
            Material shoes = GetOrCreateFallbackMaterial("AJ_Shoes_Fallback", new Color(0.035f, 0.035f, 0.045f));

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] current = renderer.sharedMaterials;
                for (int i = 0; i < current.Length; i++)
                {
                    Material existing = current[i];
                    if (existing != null && HasUsefulTexture(existing)) continue;

                    string key = (renderer.name + " " + (existing != null ? existing.name : string.Empty)).ToLowerInvariant();
                    if (key.Contains("skin") || key.Contains("body") || key.Contains("head") || key.Contains("face") || key.Contains("hand")) current[i] = skin;
                    else if (key.Contains("shoe") || key.Contains("sneaker") || key.Contains("foot")) current[i] = shoes;
                    else current[i] = cloth;
                }
                renderer.sharedMaterials = current;
            }
        }

        private static bool HasUsefulTexture(Material material)
        {
            if (material == null) return false;
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null) return true;
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null) return true;
            return false;
        }

        private static Material GetOrCreateFallbackMaterial(string name, Color color)
        {
            string folder = "Assets/Models/Characters/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EnsureFolders();
                AssetDatabase.CreateFolder("Assets/Models/Characters", "Materials");
            }

            string path = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Characters")) AssetDatabase.CreateFolder("Assets/Models", "Characters");
        }
    }
}
