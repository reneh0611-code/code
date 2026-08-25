using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.DebugTools;
using CheatOnYourDayOnes.Economy;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Inventory;
using CheatOnYourDayOnes.Multiplayer;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class Phase1SceneBuilder
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string PrefabFolder = "Assets/Prefabs/Player";
        private const string ScenePath = SceneFolder + "/Prototype_Street.unity";
        private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

        [MenuItem("Tools/CYDOY/Build Phase 1 Scene")]
        public static void BuildPhase1Scene()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Player");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            CreateGround();
            GameObject playerPrefab = CreateOrReplacePlayerPrefab();
            CreateNetworkRoot(playerPrefab);
            CreateTestJob();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("CYDOY Phase 1", "Phase 1 scene created.\n\nPress Play and click Start Host.", "Let's go");
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
        }

        private static GameObject CreateOrReplacePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
                AssetDatabase.DeleteAsset(PlayerPrefabPath);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null) Object.DestroyImmediate(capsuleCollider);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = Vector3.zero;
            cc.radius = 0.4f;
            cc.height = 2f;

            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerData>();
            player.AddComponent<PlayerWallet>();
            player.AddComponent<AuraSystem>();
            player.AddComponent<NeedsSystem>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerAgent>();
            NetworkPlayerController controller = player.AddComponent<NetworkPlayerController>();
            PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();

            GameObject cameraRoot = new("CameraRoot");
            cameraRoot.transform.SetParent(player.transform);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            GameObject cameraObject = new("PlayerCamera");
            cameraObject.transform.SetParent(cameraRoot.transform);
            Camera playerCamera = cameraObject.AddComponent<Camera>();
            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            ThirdPersonCamera thirdPersonCamera = cameraObject.AddComponent<ThirdPersonCamera>();

            SerializedObject cameraSO = new(thirdPersonCamera);
            cameraSO.FindProperty("target").objectReferenceValue = player.transform;
            cameraSO.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject controllerSO = new(controller);
            controllerSO.FindProperty("cameraTarget").objectReferenceValue = cameraRoot.transform;
            controllerSO.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            controllerSO.FindProperty("audioListener").objectReferenceValue = listener;
            controllerSO.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject interactorSO = new(interactor);
            interactorSO.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            interactorSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void CreateNetworkRoot(GameObject playerPrefab)
        {
            GameObject networkRoot = new("NetworkRoot");
            NetworkManager networkManager = networkRoot.AddComponent<NetworkManager>();
            UnityTransport transport = networkRoot.AddComponent<UnityTransport>();
            networkRoot.AddComponent<DevNetworkLauncher>();

            // NGO does not automatically choose a transport merely because one is on the GameObject.
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            EditorUtility.SetDirty(networkManager);
        }

        private static void CreateTestJob()
        {
            GameObject testJob = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testJob.name = "TestJob";
            testJob.transform.position = new Vector3(3f, 0.5f, 2f);
            testJob.AddComponent<NetworkObject>();
            testJob.AddComponent<TestCashInteractable>();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
