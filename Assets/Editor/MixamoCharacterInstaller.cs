using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class MixamoCharacterInstaller
    {
        private const string CharacterPath = "Assets/Models/Characters/Ch28_nonPBR.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const float TargetHeight = 1.82f;

        [MenuItem("Tools/CYDOY/Install Mixamo Character")]
        public static void Install()
        {
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (characterAsset == null)
            {
                EnsureFolders();
                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Character",
                    "Character file not found.\n\nPut Ch28_nonPBR.fbx here:\n" + CharacterPath,
                    "OK");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Character",
                    "Player prefab not found. Run Tools → CYDOY → Build Phase 1 Scene first.",
                    "OK");
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

                model.name = "Mixamo_David";
                model.transform.SetParent(visualRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                RemoveModelColliders(model);
                ConfigureAnimator(model);
                NormalizeAndGround(model.transform);

                CharacterController controller = prefabRoot.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.height = 1.9f;
                    controller.radius = 0.34f;
                    controller.center = new Vector3(0f, 0.95f, 0f);
                    controller.stepOffset = 0.30f;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Character",
                    "Character reinstalled with corrected scale, grounding and material extraction.",
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
            if (importer == null)
                return;

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
                if (asset is not Material material)
                    continue;

                string targetPath = materialFolder + "/" + material.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(targetPath) != null)
                    continue;

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
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            var primitiveAnimator = root.GetComponent<CheatOnYourDayOnes.Player.StylizedCharacterAnimator>();
            if (primitiveAnimator != null)
                Object.DestroyImmediate(primitiveAnimator);
        }

        private static void RemoveModelColliders(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void ConfigureAnimator(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = model.AddComponent<Animator>();

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        private static void NormalizeAndGround(Transform modelRoot)
        {
            if (!TryGetRendererBounds(modelRoot.gameObject, out Bounds initialBounds))
                return;

            float initialHeight = initialBounds.size.y;
            if (initialHeight <= 0.001f)
                return;

            float scaleFactor = TargetHeight / initialHeight;
            modelRoot.localScale = Vector3.one * scaleFactor;

            // Force Unity to update renderer bounds after scaling.
            Physics.SyncTransforms();
            if (!TryGetRendererBounds(modelRoot.gameObject, out Bounds scaledBounds))
                return;

            float feetWorldY = scaledBounds.min.y;
            float desiredFeetWorldY = modelRoot.parent != null ? modelRoot.parent.position.y : 0f;
            float deltaY = desiredFeetWorldY - feetWorldY;
            modelRoot.position += Vector3.up * deltaY;
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
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Characters"))
                AssetDatabase.CreateFolder("Assets/Models", "Characters");
        }
    }
}
