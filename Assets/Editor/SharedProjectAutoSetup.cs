using System.Linq;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class SharedProjectAutoSetup
    {
        private const string ScenePath = "Assets/Scenes/Prototype_Street.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string PlayerControllerPath = "Assets/Resources/AJ_Locomotion.controller";
        private const string NpcControllerPath = "Assets/Resources/LittleGuys_Locomotion.controller";

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
            EnsureOpenSceneNetworkPlayerReference();
            ValidateSharedAssets();
        }

        private static void EnsureMainSceneInBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == ScenePath && s.enabled)) return;

            var list = scenes.ToList();
            int existing = list.FindIndex(s => s.path == ScenePath);
            if (existing >= 0) list[existing] = new EditorBuildSettingsScene(ScenePath, true);
            else list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[CYDOY AUTO] Main scene added/enabled in Build Settings.");
        }

        private static void EnsurePlayerPrefabRuntimeParts()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
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

                Animator animator = root.GetComponentInChildren<Animator>(true);
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
                if (animator != null && animator.runtimeAnimatorController == null && controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log("[CYDOY AUTO] Player prefab repaired automatically.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureOpenSceneNetworkPlayerReference()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath) return;

            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (manager == null || playerPrefab == null) return;

            if (manager.NetworkConfig.PlayerPrefab != playerPrefab)
            {
                manager.NetworkConfig.PlayerPrefab = playerPrefab;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[CYDOY AUTO] NetworkManager linked to the shared Player prefab.");
            }
        }

        private static void ValidateSharedAssets()
        {
            string[] required =
            {
                PlayerPrefabPath,
                "Assets/Models/Animations/Idle.fbx",
                "Assets/Models/Animations/Walk.fbx",
                "Assets/Models/Animations/Run.fbx",
                PlayerControllerPath,
                NpcControllerPath
            };

            foreach (string path in required)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    Debug.LogError("[CYDOY AUTO] Required shared asset is missing: " + path + ". Make sure it is tracked in Git and pulled on this machine.");

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null && playerPrefab.GetComponentInChildren<Animator>(true) == null)
                Debug.LogWarning("[CYDOY AUTO] Player.prefab currently contains no Animator/character visual. The deleted AJ source FBX is no longer required; only the character actually embedded/referenced by Player.prefab matters.");
        }
    }
}
