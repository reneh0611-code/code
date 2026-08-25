using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class VisualPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/Prototype_Street.unity";

        [MenuItem("Tools/CYDOY/Build Visual Prototype")]
        public static void BuildVisualPrototype()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            ClearGeneratedRoot();
            GameObject root = new("VisualPrototype");

            Material roadMat = MakeMaterial("RoadMat", new Color(0.13f, 0.14f, 0.16f));
            Material sidewalkMat = MakeMaterial("SidewalkMat", new Color(0.42f, 0.43f, 0.45f));
            Material grassMat = MakeMaterial("GrassMat", new Color(0.22f, 0.36f, 0.24f));
            Material creamMat = MakeMaterial("CreamMat", new Color(0.78f, 0.73f, 0.63f));
            Material darkMat = MakeMaterial("DarkMat", new Color(0.08f, 0.09f, 0.11f));
            Material brickMat = MakeMaterial("BrickMat", new Color(0.42f, 0.19f, 0.14f));
            Material blueMat = MakeMaterial("BlueMat", new Color(0.15f, 0.32f, 0.50f));
            Material redMat = MakeMaterial("RedMat", new Color(0.58f, 0.12f, 0.10f));
            Material whiteMat = MakeMaterial("WhiteMat", new Color(0.90f, 0.90f, 0.86f));
            Material glassMat = MakeMaterial("GlassMat", new Color(0.12f, 0.18f, 0.22f));
            Material yellowMat = MakeMaterial("YellowMat", new Color(0.88f, 0.63f, 0.10f));

            CreateBox(root.transform, "GroundBase", new Vector3(0, -0.35f, 0), new Vector3(50, 0.5f, 36), grassMat);
            CreateBox(root.transform, "Road", new Vector3(0, 0, 0), new Vector3(50, 0.12f, 10), roadMat);
            CreateBox(root.transform, "SidewalkLeft", new Vector3(0, 0.08f, -7), new Vector3(50, 0.22f, 4), sidewalkMat);
            CreateBox(root.transform, "SidewalkRight", new Vector3(0, 0.08f, 7), new Vector3(50, 0.22f, 4), sidewalkMat);

            for (int x = -22; x <= 22; x += 4)
                CreateBox(root.transform, "RoadDash", new Vector3(x, 0.08f, 0), new Vector3(2f, 0.03f, 0.12f), whiteMat);

            CreateBuilding(root.transform, new Vector3(-15, 3, -11), new Vector3(8, 6, 5), creamMat, darkMat, "APARTMENTS");
            CreateBuilding(root.transform, new Vector3(-5, 2.6f, -11), new Vector3(7, 5.2f, 5), brickMat, darkMat, "DINER");
            CreateBuilding(root.transform, new Vector3(6, 3.5f, -11), new Vector3(9, 7, 5), blueMat, darkMat, "CITY BANK");
            CreateBuilding(root.transform, new Vector3(17, 2.8f, -11), new Vector3(8, 5.6f, 5), darkMat, redMat, "PUB");

            CreateBuilding(root.transform, new Vector3(-17, 2.6f, 11), new Vector3(8, 5.2f, 5), brickMat, creamMat, "MARKET");
            CreateKiosk(root.transform, new Vector3(-6, 0, 10.5f), creamMat, redMat, darkMat, glassMat);
            CreateGasStation(root.transform, new Vector3(8, 0, 10.5f), whiteMat, redMat, darkMat, yellowMat);
            CreateBuilding(root.transform, new Vector3(18, 3.2f, 11), new Vector3(8, 6.4f, 5), creamMat, blueMat, "LOFTS");

            for (int x = -20; x <= 20; x += 8)
            {
                CreateStreetLamp(root.transform, new Vector3(x, 0, -5.4f), darkMat, yellowMat);
                CreateStreetLamp(root.transform, new Vector3(x + 4, 0, 5.4f), darkMat, yellowMat);
            }

            CreateParking(root.transform, new Vector3(12, 0.14f, 7.1f), whiteMat);
            CreateBench(root.transform, new Vector3(-10, 0.2f, -5.8f), darkMat, creamMat);
            CreateBench(root.transform, new Vector3(2, 0.2f, 5.8f), darkMat, creamMat);
            CreateTrashCan(root.transform, new Vector3(-3, 0.35f, -5.7f), darkMat);
            CreateTrashCan(root.transform, new Vector3(15, 0.35f, 5.7f), darkMat);

            ImproveLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            if (scene.path == ScenePath) EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("CYDOY", "Visual Prototype built.\n\nOpen Scene view or press Play + Start Host.", "Nice");
        }

        private static void ClearGeneratedRoot()
        {
            GameObject existing = GameObject.Find("VisualPrototype");
            if (existing != null) Object.DestroyImmediate(existing);
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string folder = "Assets/Materials/Prototype";
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "Prototype");
            string path = folder + "/" + name + ".mat";
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

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void CreateBuilding(Transform parent, Vector3 pos, Vector3 size, Material wall, Material accent, string sign)
        {
            GameObject b = new(sign);
            b.transform.SetParent(parent);
            CreateBox(b.transform, "Body", pos, size, wall);
            CreateBox(b.transform, "Roof", pos + new Vector3(0, size.y / 2f + 0.2f, 0), new Vector3(size.x + 0.4f, 0.3f, size.z + 0.4f), accent);
            CreateBox(b.transform, "Door", pos + new Vector3(0, -size.y / 2f + 1.2f, -size.z / 2f - 0.05f), new Vector3(1.4f, 2.4f, 0.15f), accent);
            for (int i = -1; i <= 1; i += 2)
                CreateBox(b.transform, "Window", pos + new Vector3(i * size.x * 0.25f, 0.4f, -size.z / 2f - 0.06f), new Vector3(1.5f, 1.3f, 0.12f), accent);
            CreateBox(b.transform, "Sign_" + sign, pos + new Vector3(0, size.y * 0.18f, -size.z / 2f - 0.12f), new Vector3(size.x * 0.55f, 0.65f, 0.18f), accent);
        }

        private static void CreateKiosk(Transform parent, Vector3 origin, Material body, Material accent, Material dark, Material glass)
        {
            GameObject k = new("KIOSK"); k.transform.SetParent(parent);
            CreateBox(k.transform, "KioskBody", origin + new Vector3(0, 1.5f, 0), new Vector3(5.5f, 3f, 4f), body);
            CreateBox(k.transform, "KioskRoof", origin + new Vector3(0, 3.15f, 0), new Vector3(6.2f, 0.3f, 4.6f), accent);
            CreateBox(k.transform, "FrontWindow", origin + new Vector3(0, 1.65f, -2.06f), new Vector3(3.3f, 1.35f, 0.12f), glass);
            CreateBox(k.transform, "Door", origin + new Vector3(2f, 1.1f, -2.07f), new Vector3(1.1f, 2.2f, 0.14f), dark);
            CreateBox(k.transform, "Sign", origin + new Vector3(0, 2.75f, -2.2f), new Vector3(3.4f, 0.6f, 0.2f), accent);
        }

        private static void CreateGasStation(Transform parent, Vector3 origin, Material body, Material accent, Material dark, Material yellow)
        {
            GameObject g = new("GAS STATION"); g.transform.SetParent(parent);
            CreateBox(g.transform, "Shop", origin + new Vector3(3.5f, 1.6f, 0), new Vector3(5, 3.2f, 4), body);
            CreateBox(g.transform, "ShopBand", origin + new Vector3(3.5f, 2.8f, -2.08f), new Vector3(5.2f, 0.45f, 0.15f), accent);
            CreateBox(g.transform, "Canopy", origin + new Vector3(-2f, 3.2f, 0), new Vector3(8, 0.35f, 5), accent);
            CreateBox(g.transform, "Pillar1", origin + new Vector3(-5f, 1.6f, 1.6f), new Vector3(0.35f, 3.2f, 0.35f), dark);
            CreateBox(g.transform, "Pillar2", origin + new Vector3(1f, 1.6f, 1.6f), new Vector3(0.35f, 3.2f, 0.35f), dark);
            CreateBox(g.transform, "Pump1", origin + new Vector3(-3.5f, 0.8f, 0), new Vector3(0.8f, 1.6f, 0.8f), dark);
            CreateBox(g.transform, "Pump2", origin + new Vector3(-0.5f, 0.8f, 0), new Vector3(0.8f, 1.6f, 0.8f), dark);
            CreateBox(g.transform, "PumpAccent1", origin + new Vector3(-3.5f, 1.05f, -0.42f), new Vector3(0.45f, 0.35f, 0.08f), yellow);
            CreateBox(g.transform, "PumpAccent2", origin + new Vector3(-0.5f, 1.05f, -0.42f), new Vector3(0.45f, 0.35f, 0.08f), yellow);
        }

        private static void CreateStreetLamp(Transform parent, Vector3 origin, Material pole, Material lightMat)
        {
            GameObject l = new("StreetLamp"); l.transform.SetParent(parent);
            CreateBox(l.transform, "Pole", origin + new Vector3(0, 2f, 0), new Vector3(0.12f, 4f, 0.12f), pole);
            CreateBox(l.transform, "Arm", origin + new Vector3(0.35f, 3.9f, 0), new Vector3(0.7f, 0.1f, 0.1f), pole);
            CreateBox(l.transform, "Lamp", origin + new Vector3(0.7f, 3.75f, 0), new Vector3(0.35f, 0.18f, 0.28f), lightMat);
        }

        private static void CreateParking(Transform parent, Vector3 center, Material white)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = center.x - 4.5f + i * 3f;
                CreateBox(parent, "ParkingLine", new Vector3(x, center.y, center.z), new Vector3(0.08f, 0.03f, 2.4f), white);
            }
        }

        private static void CreateBench(Transform parent, Vector3 origin, Material dark, Material seat)
        {
            GameObject b = new("Bench"); b.transform.SetParent(parent);
            CreateBox(b.transform, "Seat", origin + new Vector3(0, 0.55f, 0), new Vector3(2.2f, 0.18f, 0.7f), seat);
            CreateBox(b.transform, "Back", origin + new Vector3(0, 1.05f, 0.28f), new Vector3(2.2f, 0.8f, 0.14f), seat);
            CreateBox(b.transform, "LegL", origin + new Vector3(-0.75f, 0.25f, 0), new Vector3(0.12f, 0.5f, 0.12f), dark);
            CreateBox(b.transform, "LegR", origin + new Vector3(0.75f, 0.25f, 0), new Vector3(0.12f, 0.5f, 0.12f), dark);
        }

        private static void CreateTrashCan(Transform parent, Vector3 origin, Material mat)
        {
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "TrashCan"; t.transform.SetParent(parent); t.transform.position = origin; t.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f); t.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void ImproveLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.78f);
            RenderSettings.fogDensity = 0.0035f;

            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.91f, 0.78f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }
        }
    }
}
