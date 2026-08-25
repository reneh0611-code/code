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
                    "Character file not found.\n\nPut Ch28_nonPBR.fbx here:\n" + CharacterPath +
                    "\n\nUnity will automatically import it as a Humanoid. Then run this menu item again.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>("Assets/Models/Characters");
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
                NormalizeModelScale(model.transform);
                GroundModel(model.transform);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "CYDOY · Mixamo Character",
                    "Mixamo character installed successfully.\n\nYour Player prefab now uses the real character model while keeping the existing network controller, wallet, needs, interaction system and third-person camera.",
                    "Let's go");

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
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
            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
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

        private static void NormalizeModelScale(Transform modelRoot)
        {
            if (!TryGetRendererBounds(modelRoot.gameObject, out Bounds bounds))
                return;

            float height = bounds.size.y;
            if (height <= 0.001f)
                return;

            float factor = TargetHeight / height;
            modelRoot.localScale *= factor;
        }

        private static void GroundModel(Transform modelRoot)
        {
            if (!TryGetRendererBounds(modelRoot.gameObject, out Bounds bounds))
                return;

            float bottomY = bounds.min.y;
            Vector3 worldPosition = modelRoot.position;
            worldPosition.y -= bottomY;
            modelRoot.position = worldPosition;

            // Player root is centered around the CharacterController. Put feet near its lower end.
            modelRoot.localPosition += new Vector3(0f, -1.02f, 0f);
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
