using System;
using System.Collections.Generic;
using CheatOnYourDayOnes.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class CartoonCityDriveableCarsBuilder
    {
        private const string SourceFolder = "Assets/ithappy/Cartoon_City_Free/Prefabs/Cars";
        private const string OutputFolder = "Assets/Models/Vehicles/Cartoon City - READY";
        private const string ScenePath = "Assets/zzz.unity";
        private const string SceneRootName = "Cartoon City Cars - DRIVEABLE";
        private const string BuildVersionKey = "CYDOY.CartoonCityDriveableCars.v3";

        private static readonly string[] VehicleNames =
        {
            "Car_06", "Car_13", "Car_16", "Car_19", "Futuristic_Car_1", "Van"
        };

        private struct WheelRig
        {
            public Transform steering;
            public Transform spin;
            public float radius;
        }

        static CartoonCityDriveableCarsBuilder()
        {
            EditorApplication.delayCall += TryAutomaticBuild;
        }

        private static void TryAutomaticBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Vehicles/LowPoly Car Pack/Source/LowPoly Car Pack.fbx") != null)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutomaticBuild;
                return;
            }

            if (AllPrefabsExist()) return;
            BuildAndPlace(false);
        }

        [MenuItem("Tools/CYDOY/Vehicles/Build + Place Cartoon City Cars")]
        public static void BuildAndPlaceFromMenu()
        {
            BuildAndPlace(true);
        }

        private static void BuildAndPlace(bool showDialog)
        {
            EnsureFolder(OutputFolder);
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string vehicleName in VehicleNames)
                    BuildVehicle(vehicleName);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // SaveAsPrefabAsset can return null while StartAssetEditing is active even though
            // the prefab was written successfully. Load the completed assets after the refresh.
            var readyPrefabs = new List<GameObject>();
            foreach (string vehicleName in VehicleNames)
            {
                GameObject ready = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{OutputFolder}/{vehicleName} - DRIVEABLE.prefab");
                if (ready != null) readyPrefabs.Add(ready);
            }

            bool placed = PlaceInOpenWorldScene(readyPrefabs);
            EditorPrefs.SetBool(BuildVersionKey, true);

            if (showDialog)
            {
                string placement = placed
                    ? "Die sechs Autos stehen fahrbereit auf dem Parkplatz."
                    : "Die sechs fahrbereiten Prefabs sind fertig. Öffne zzz.unity und führe den Menüpunkt erneut aus, um sie dort zu platzieren.";
                EditorUtility.DisplayDialog("CYDOY · Cartoon City Cars",
                    placement + "\n\nE = ein-/aussteigen\nWASD = fahren und lenken\nLeertaste = bremsen\n\nDie Vorderräder lenken sichtbar, alle vier Räder drehen sich passend zur Geschwindigkeit.",
                    "Fertig");
            }
        }

        private static GameObject BuildVehicle(string vehicleName)
        {
            string sourcePath = $"{SourceFolder}/{vehicleName}.prefab";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"[CYDOY] Cartoon City car source missing: {sourcePath}");
                return null;
            }

            GameObject car = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (car == null) return null;

            try
            {
                PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                car.name = vehicleName + " - DRIVEABLE";
                car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                car.transform.localScale = Vector3.one;

                foreach (Collider collider in car.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                WheelRig frontLeft = RigWheel(car.transform, "Wheel_Front_Left", "FrontLeft");
                WheelRig frontRight = RigWheel(car.transform, "Wheel_Front_Right", "FrontRight");
                WheelRig rearLeft = RigWheel(car.transform, "Wheel_Rear_Left", "RearLeft");
                WheelRig rearRight = RigWheel(car.transform, "Wheel_Rear_Right", "RearRight");

                Bounds bounds = CalculateLocalBounds(car.transform);
                float modelHeight = Mathf.Max(.5f, bounds.size.y);
                float modelHalfWidth = Mathf.Max(.7f, bounds.extents.x);

                Transform seat = NewMarker(car.transform, "DriverSeat",
                    new Vector3(-modelHalfWidth * .24f, bounds.min.y + modelHeight * .58f, bounds.center.z - bounds.extents.z * .08f));
                Transform exit = NewMarker(car.transform, "ExitPoint",
                    new Vector3(-modelHalfWidth - .9f, bounds.min.y + .15f, bounds.center.z));
                Transform centerOfMass = NewMarker(car.transform, "CenterOfMass",
                    new Vector3(0f, bounds.min.y + modelHeight * .27f, bounds.center.z + bounds.extents.z * .04f));

                Rigidbody rb = car.AddComponent<Rigidbody>();
                rb.mass = 1350f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                DriveableCar driveable = car.AddComponent<DriveableCar>();
                SerializedObject driveableData = new SerializedObject(driveable);
                driveableData.FindProperty("driverSeat").objectReferenceValue = seat;
                driveableData.FindProperty("exitPoint").objectReferenceValue = exit;
                driveableData.FindProperty("centerOfMass").objectReferenceValue = centerOfMass;
                driveableData.ApplyModifiedPropertiesWithoutUndo();

                VehicleWheelVisuals wheelVisuals = car.AddComponent<VehicleWheelVisuals>();
                float radius = AveragePositive(frontLeft.radius, frontRight.radius, rearLeft.radius, rearRight.radius);
                wheelVisuals.Configure(
                    frontLeft.steering, frontLeft.spin,
                    frontRight.steering, frontRight.spin,
                    rearLeft.steering, rearLeft.spin,
                    rearRight.steering, rearRight.spin,
                    radius);

                string outputPath = $"{OutputFolder}/{vehicleName} - DRIVEABLE.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(car, outputPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(car);
            }
        }

        private static WheelRig RigWheel(Transform root, string namePart, string cleanName)
        {
            Transform wheel = FindTransformContaining(root, namePart);
            if (wheel == null)
            {
                Debug.LogWarning($"[CYDOY] Wheel {namePart} was not found below {root.name}.");
                return default;
            }

            Transform oldParent = wheel.parent;
            Vector3 oldPosition = wheel.localPosition;
            Quaternion oldRotation = wheel.localRotation;
            Vector3 oldScale = wheel.localScale;

            Transform steering = new GameObject($"Wheel_{cleanName}_SteeringPivot").transform;
            steering.SetParent(oldParent, false);
            steering.localPosition = oldPosition;
            steering.localRotation = Quaternion.identity;

            Transform spin = new GameObject($"Wheel_{cleanName}_SpinPivot").transform;
            spin.SetParent(steering, false);
            spin.localPosition = Vector3.zero;
            spin.localRotation = Quaternion.identity;

            wheel.SetParent(spin, false);
            wheel.localPosition = Vector3.zero;
            wheel.localRotation = oldRotation;
            wheel.localScale = oldScale;

            Renderer[] renderers = wheel.GetComponentsInChildren<Renderer>(true);
            float radius = 0f;
            foreach (Renderer renderer in renderers)
                radius = Mathf.Max(radius, renderer.bounds.extents.y);

            return new WheelRig { steering = steering, spin = spin, radius = radius };
        }

        private static Transform FindTransformContaining(Transform root, string namePart)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            return null;
        }

        private static Transform NewMarker(Transform parent, string name, Vector3 localPosition)
        {
            Transform marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            return marker;
        }

        private static Bounds CalculateLocalBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(2f, 1.5f, 4f));

            Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Renderer renderer in renderers)
            {
                Bounds b = renderer.bounds;
                foreach (Vector3 corner in BoundsCorners(b))
                {
                    Vector3 local = root.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            return new Bounds((min + max) * .5f, max - min);
        }

        private static Vector3[] BoundsCorners(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            return new[]
            {
                new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
                new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
                new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
            };
        }

        private static float AveragePositive(params float[] values)
        {
            float sum = 0f;
            int count = 0;
            foreach (float value in values)
            {
                if (value <= .01f) continue;
                sum += value;
                count++;
            }
            return count > 0 ? sum / count : .38f;
        }

        private static bool PlaceInOpenWorldScene(IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs.Count == 0) return false;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedAdditively = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }
            if (!scene.IsValid() || !scene.isLoaded) return false;

            GameObject oldRoot = FindInScene(scene, SceneRootName);
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(SceneRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject spawn = FindInScene(scene, "PlayerSpawn");
            Vector3 anchor = spawn != null ? spawn.transform.position : new Vector3(258.51f, .06f, 676.44f);
            Quaternion parkingRotation = spawn != null ? spawn.transform.rotation : Quaternion.Euler(0f, 130f, 0f);

            Vector2[] offsets =
            {
                new(-10.5f, -7.5f), new(-6.3f, -7.5f), new(-2.1f, -7.5f),
                new(2.1f, -7.5f), new(6.3f, -7.5f), new(10.5f, -7.5f)
            };

            for (int i = 0; i < prefabs.Count && i < offsets.Length; i++)
            {
                GameObject car = PrefabUtility.InstantiatePrefab(prefabs[i], scene) as GameObject;
                if (car == null) continue;
                car.transform.SetParent(root.transform);
                Vector3 worldOffset = parkingRotation * new Vector3(offsets[i].x, 0f, offsets[i].y);
                car.transform.SetPositionAndRotation(anchor + worldOffset, parkingRotation);

                Bounds bounds = CalculateLocalBounds(car.transform);
                float currentBottom = car.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
                car.transform.position += Vector3.up * (anchor.y + .025f - currentBottom);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CYDOY] Built and placed {prefabs.Count} driveable Cartoon City cars at the parking lot.", root);
            if (openedAdditively)
                EditorSceneManager.CloseScene(scene, true);
            else
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            return true;
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == objectName) return child.gameObject;
            }
            return null;
        }

        private static bool AllPrefabsExist()
        {
            foreach (string vehicleName in VehicleNames)
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/{vehicleName} - DRIVEABLE.prefab") == null)
                    return false;
            return true;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
