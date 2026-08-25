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
            CreatePreviewCamera();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("CYDOY Phase 1", "Phase 1 scene rebuilt with the new third-person character.\n\nRun Build Visual Prototype next.", "Let's go");
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

            GameObject player = new("Player");
            player.transform.position = new Vector3(0f, 1.05f, 0f);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0f, 0f);
            cc.radius = 0.38f;
            cc.height = 2.05f;
            cc.stepOffset = 0.32f;
            cc.slopeLimit = 48f;

            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerData>();
            player.AddComponent<PlayerWallet>();
            player.AddComponent<AuraSystem>();
            player.AddComponent<NeedsSystem>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerAgent>();
            NetworkPlayerController controller = player.AddComponent<NetworkPlayerController>();
            PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
            StylizedCharacterAnimator characterAnimator = player.AddComponent<StylizedCharacterAnimator>();

            CharacterParts parts = CreateStylizedPlayerVisual(player.transform);

            SerializedObject animatorSO = new(characterAnimator);
            animatorSO.FindProperty("leftArm").objectReferenceValue = parts.LeftArm;
            animatorSO.FindProperty("rightArm").objectReferenceValue = parts.RightArm;
            animatorSO.FindProperty("leftLeg").objectReferenceValue = parts.LeftLeg;
            animatorSO.FindProperty("rightLeg").objectReferenceValue = parts.RightLeg;
            animatorSO.FindProperty("torso").objectReferenceValue = parts.Torso;
            animatorSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject cameraRoot = new("CameraRoot");
            cameraRoot.transform.SetParent(player.transform);
            cameraRoot.transform.localPosition = Vector3.zero;

            GameObject cameraObject = new("PlayerCamera");
            cameraObject.transform.SetParent(cameraRoot.transform);
            cameraObject.transform.localPosition = new Vector3(0.65f, 1.65f, -4.8f);
            cameraObject.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.fieldOfView = 62f;
            playerCamera.nearClipPlane = 0.08f;
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
            AssetDatabase.Refresh();
            return prefab;
        }

        private static CharacterParts CreateStylizedPlayerVisual(Transform parent)
        {
            Material skin = GetMaterial("PlayerSkin", new Color(0.72f, 0.50f, 0.36f));
            Material shirt = GetMaterial("PlayerShirt", new Color(0.07f, 0.09f, 0.13f));
            Material shirtAccent = GetMaterial("PlayerShirtAccent", new Color(0.82f, 0.82f, 0.78f));
            Material pants = GetMaterial("PlayerPants", new Color(0.16f, 0.19f, 0.24f));
            Material shoes = GetMaterial("PlayerShoes", new Color(0.035f, 0.035f, 0.045f));
            Material hair = GetMaterial("PlayerHair", new Color(0.08f, 0.055f, 0.04f));

            GameObject visual = new("CharacterVisual");
            visual.transform.SetParent(parent);
            visual.transform.localPosition = Vector3.zero;

            Transform torso = CreatePart(visual.transform, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.22f, 0f), new Vector3(0.68f, 0.72f, 0.44f), shirt);
            CreatePart(torso, PrimitiveType.Cube, "ShirtStripe", new Vector3(0f, 0.12f, -0.50f), new Vector3(0.48f, 0.12f, 0.035f), shirtAccent);

            Transform neck = CreatePart(visual.transform, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.83f, 0f), new Vector3(0.18f, 0.13f, 0.18f), skin);
            neck.localRotation = Quaternion.identity;

            Transform head = CreatePart(visual.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.13f, 0f), new Vector3(0.48f, 0.56f, 0.46f), skin);
            CreatePart(head, PrimitiveType.Sphere, "Hair", new Vector3(0f, 0.31f, 0.02f), new Vector3(0.94f, 0.38f, 0.93f), hair);

            CreatePart(head, PrimitiveType.Sphere, "EarL", new Vector3(-0.52f, 0f, 0f), new Vector3(0.12f, 0.18f, 0.09f), skin);
            CreatePart(head, PrimitiveType.Sphere, "EarR", new Vector3(0.52f, 0f, 0f), new Vector3(0.12f, 0.18f, 0.09f), skin);

            GameObject leftArmPivot = new("LeftArmPivot");
            leftArmPivot.transform.SetParent(visual.transform);
            leftArmPivot.transform.localPosition = new Vector3(-0.45f, 0.57f, 0f);
            CreatePart(leftArmPivot.transform, PrimitiveType.Capsule, "LeftArm", new Vector3(0f, -0.34f, 0f), new Vector3(0.18f, 0.40f, 0.18f), shirt);
            CreatePart(leftArmPivot.transform, PrimitiveType.Sphere, "LeftHand", new Vector3(0f, -0.72f, 0f), new Vector3(0.18f, 0.18f, 0.18f), skin);

            GameObject rightArmPivot = new("RightArmPivot");
            rightArmPivot.transform.SetParent(visual.transform);
            rightArmPivot.transform.localPosition = new Vector3(0.45f, 0.57f, 0f);
            CreatePart(rightArmPivot.transform, PrimitiveType.Capsule, "RightArm", new Vector3(0f, -0.34f, 0f), new Vector3(0.18f, 0.40f, 0.18f), shirt);
            CreatePart(rightArmPivot.transform, PrimitiveType.Sphere, "RightHand", new Vector3(0f, -0.72f, 0f), new Vector3(0.18f, 0.18f, 0.18f), skin);

            GameObject leftLegPivot = new("LeftLegPivot");
            leftLegPivot.transform.SetParent(visual.transform);
            leftLegPivot.transform.localPosition = new Vector3(-0.20f, -0.32f, 0f);
            CreatePart(leftLegPivot.transform, PrimitiveType.Capsule, "LeftLeg", new Vector3(0f, -0.38f, 0f), new Vector3(0.22f, 0.46f, 0.22f), pants);
            CreatePart(leftLegPivot.transform, PrimitiveType.Cube, "LeftShoe", new Vector3(0f, -0.82f, 0.10f), new Vector3(0.27f, 0.16f, 0.48f), shoes);

            GameObject rightLegPivot = new("RightLegPivot");
            rightLegPivot.transform.SetParent(visual.transform);
            rightLegPivot.transform.localPosition = new Vector3(0.20f, -0.32f, 0f);
            CreatePart(rightLegPivot.transform, PrimitiveType.Capsule, "RightLeg", new Vector3(0f, -0.38f, 0f), new Vector3(0.22f, 0.46f, 0.22f), pants);
            CreatePart(rightLegPivot.transform, PrimitiveType.Cube, "RightShoe", new Vector3(0f, -0.82f, 0.10f), new Vector3(0.27f, 0.16f, 0.48f), shoes);

            return new CharacterParts
            {
                Torso = torso,
                LeftArm = leftArmPivot.transform,
                RightArm = rightArmPivot.transform,
                LeftLeg = leftLegPivot.transform,
                RightLeg = rightLegPivot.transform
            };
        }

        private static Transform CreatePart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return part.transform;
        }

        private static Material GetMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Prototype"))
                AssetDatabase.CreateFolder("Assets/Materials", "Prototype");

            string path = $"Assets/Materials/Prototype/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
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

        private static void CreatePreviewCamera()
        {
            GameObject cameraObject = new("ScenePreviewCamera");
            cameraObject.transform.position = new Vector3(0f, 8f, -18f);
            cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            ScenePreviewCamera handoff = cameraObject.AddComponent<ScenePreviewCamera>();
            SerializedObject so = new(handoff);
            so.FindProperty("audioListener").objectReferenceValue = listener;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private sealed class CharacterParts
        {
            public Transform Torso;
            public Transform LeftArm;
            public Transform RightArm;
            public Transform LeftLeg;
            public Transform RightLeg;
        }
    }
}
