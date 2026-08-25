using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.DebugTools;
using CheatOnYourDayOnes.Economy;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Inventory;
using CheatOnYourDayOnes.Multiplayer;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.UI;
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
            CreateHUD();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("CYDOY Phase 1", "Phase 1 scene created.\n\nNow run Build Visual Prototype, then press Play and Start Host.", "Let's go");
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
            MeshRenderer rootRenderer = player.GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;

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

            CreateStylizedPlayerVisual(player.transform);

            GameObject cameraRoot = new("CameraRoot");
            cameraRoot.transform.SetParent(player.transform);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            GameObject cameraObject = new("PlayerCamera");
            cameraObject.transform.SetParent(cameraRoot.transform);
            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.fieldOfView = 65f;
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

        private static void CreateStylizedPlayerVisual(Transform parent)
        {
            Material skin = GetMaterial("PlayerSkin", new Color(0.72f, 0.50f, 0.36f));
            Material shirt = GetMaterial("PlayerShirt", new Color(0.10f, 0.12f, 0.16f));
            Material pants = GetMaterial("PlayerPants", new Color(0.18f, 0.20f, 0.24f));
            Material shoes = GetMaterial("PlayerShoes", new Color(0.04f, 0.04f, 0.05f));

            GameObject visual = new("Visual");
            visual.transform.SetParent(parent);
            CreatePrimitivePart(visual.transform, PrimitiveType.Capsule, "Torso", new Vector3(0, 0.2f, 0), new Vector3(0.72f, 0.85f, 0.45f), shirt);
            CreatePrimitivePart(visual.transform, PrimitiveType.Sphere, "Head", new Vector3(0, 1.03f, 0), new Vector3(0.52f, 0.58f, 0.52f), skin);
            CreatePrimitivePart(visual.transform, PrimitiveType.Capsule, "LegL", new Vector3(-0.19f, -0.58f, 0), new Vector3(0.22f, 0.52f, 0.22f), pants);
            CreatePrimitivePart(visual.transform, PrimitiveType.Capsule, "LegR", new Vector3(0.19f, -0.58f, 0), new Vector3(0.22f, 0.52f, 0.22f), pants);
            CreatePrimitivePart(visual.transform, PrimitiveType.Cube, "ShoeL", new Vector3(-0.19f, -1.0f, 0.08f), new Vector3(0.28f, 0.16f, 0.45f), shoes);
            CreatePrimitivePart(visual.transform, PrimitiveType.Cube, "ShoeR", new Vector3(0.19f, -1.0f, 0.08f), new Vector3(0.28f, 0.16f, 0.45f), shoes);
        }

        private static void CreatePrimitivePart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            Collider c = part.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            part.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material GetMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Prototype")) AssetDatabase.CreateFolder("Assets/Materials", "Prototype");
            string path = $"Assets/Materials/Prototype/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateNetworkRoot(GameObject playerPrefab)
        {
            GameObject networkRoot = new("NetworkRoot");
            NetworkManager networkManager = networkRoot.AddComponent<NetworkManager>();
            UnityTransport transport = networkRoot.AddComponent<UnityTransport>();
            networkRoot.AddComponent<DevNetworkLauncher>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            EditorUtility.SetDirty(networkManager);
        }

        private static void CreateHUD()
        {
            GameObject hud = new("PrototypeHUD");
            hud.AddComponent<PrototypeHUD>();
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
