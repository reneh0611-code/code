using System.Linq;
using CheatOnYourDayOnes.Core;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class SharedProjectAutoSetup
    {
        private const string ScenePath = "Assets/zzz.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string CharacterPath = "Assets/Models/Characters/TripoTest/TripoCharacter.fbx";
        private const string CharacterMaterialPath = "Assets/Models/Characters/TripoTest/TripoCharacter_URP.mat";
        private const string PlayerControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";
        private const string NpcControllerPath = "Assets/Resources/LittleGuys_Locomotion.controller";
        private const float TargetCharacterHeight = 1.82f;

        static SharedProjectAutoSetup()
        {
            EditorApplication.delayCall += RunSilentSetup;
            EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += RunSilentSetup;
        }

        private static void RunSilentSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EnsureMainSceneInBuild();
            EnsurePlayerPrefabRuntimeParts();
            EnsureSharedMapGameplayRoot();
            ValidateSharedAssets();
        }

        private static void EnsureMainSceneInBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var list = scenes.ToList();
            int existing = list.FindIndex(s => s.path == ScenePath);
            if (existing >= 0) list[existing] = new EditorBuildSettingsScene(ScenePath, true);
            else list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureSharedMapGameplayRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath) return;

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) return;

            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            bool changed = false;

            if (manager == null)
            {
                GameObject networkRoot = new("NetworkManager");
                manager = networkRoot.AddComponent<NetworkManager>();
                UnityTransport transport = networkRoot.AddComponent<UnityTransport>();
                networkRoot.AddComponent<AutoLocalHost>();
                manager.NetworkConfig.NetworkTransport = transport;
                changed = true;
                Debug.Log("[CYDOY AUTO] Added NetworkManager + UnityTransport + AutoLocalHost to zzz.unity.");
            }
            else
            {
                UnityTransport transport = manager.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    transport = manager.gameObject.AddComponent<UnityTransport>();
                    manager.NetworkConfig.NetworkTransport = transport;
                    changed = true;
                }
                if (manager.GetComponent<AutoLocalHost>() == null)
                {
                    manager.gameObject.AddComponent<AutoLocalHost>();
                    changed = true;
                }
            }

            if (manager.NetworkConfig.PlayerPrefab != playerPrefab)
            {
                manager.NetworkConfig.PlayerPrefab = playerPrefab;
                changed = true;
            }

            // The player prefab owns its third-person camera. Keep the terrain preview camera only for edit mode.
            Camera sceneCamera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (sceneCamera != null && sceneCamera.transform.root.GetComponent<NetworkObject>() == null)
            {
                sceneCamera.gameObject.name = "Map Preview Camera";
            }

            if (changed)
            {
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[CYDOY AUTO] zzz.unity is now the shared gameplay map and is linked to Player.prefab.");
            }
        }

        private static void EnsurePlayerPrefabRuntimeParts()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (asset == null) return;

            bool changed = false;
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<VehicleInteractor>() == null)
                {
                    root.AddComponent<VehicleInteractor>();
                    changed = true;
                }

                Transform visualRoot = root.transform.Find("CharacterVisual");
                Animator animator = visualRoot != null ? visualRoot.GetComponentInChildren<Animator>(true) : null;
                Renderer[] currentRenderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();

                if ((animator == null || currentRenderers.Length == 0) && characterAsset != null)
                {
                    if (visualRoot != null) Object.DestroyImmediate(visualRoot.gameObject);
                    GameObject holder = new("CharacterVisual");
                    holder.transform.SetParent(root.transform, false);

                    GameObject model = PrefabUtility.InstantiatePrefab(characterAsset) as GameObject;
                    if (model == null) model = Object.Instantiate(characterAsset);
                    model.name = "TripoCharacter";
                    model.transform.SetParent(holder.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    NormalizeCharacterHeight(model.transform, TargetCharacterHeight);
                    ApplySharedMaterial(model);
                    SnapVisualFeetToPlayerGround(model.transform);
                    animator = model.GetComponentInChildren<Animator>(true);
                    changed = true;
                }

                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
                if (animator != null)
                {
                    if (controller != null && animator.runtimeAnimatorController != controller)
                    {
                        animator.runtimeAnimatorController = controller;
                        changed = true;
                    }
                    animator.avatar = null;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.enabled = true;

                    CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                    if (driver == null)
                    {
                        driver = root.AddComponent<CharacterAnimationDriver>();
                        changed = true;
                    }
                    SerializedObject so = new(driver);
                    SerializedProperty animatorProp = so.FindProperty("animator");
                    if (animatorProp != null) animatorProp.objectReferenceValue = animator;
                    SerializedProperty fallbackProp = so.FindProperty("fallbackController");
                    if (fallbackProp != null) fallbackProp.objectReferenceValue = controller;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[CYDOY AUTO] Player prefab repaired automatically.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void NormalizeCharacterHeight(Transform modelRoot, float targetHeight)
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.001f) return;
            modelRoot.localScale *= targetHeight / b.size.y;
        }

        private static void SnapVisualFeetToPlayerGround(Transform modelRoot)
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float localBottom = modelRoot.parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            modelRoot.parent.localPosition += Vector3.up * (-localBottom);
        }

        private static void ApplySharedMaterial(GameObject model)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            if (mat == null) return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void ValidateSharedAssets()
        {
            string[] required = { PlayerPrefabPath, CharacterPath, "Assets/Models/Animations/Idle.fbx", "Assets/Models/Animations/Walk.fbx", "Assets/Models/Animations/Run.fbx", PlayerControllerPath, NpcControllerPath };
            foreach (string path in required)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    Debug.LogError("[CYDOY AUTO] Required shared asset is missing: " + path);
        }
    }
}
