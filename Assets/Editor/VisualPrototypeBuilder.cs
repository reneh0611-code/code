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

            GameObject existing = GameObject.Find("VisualPrototype");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject root = new("VisualPrototype");

            Material road = Mat("Road", new Color(0.10f, 0.11f, 0.13f));
            Material pavement = Mat("Pavement", new Color(0.42f, 0.44f, 0.46f));
            Material grass = Mat("Grass", new Color(0.19f, 0.32f, 0.22f));
            Material cream = Mat("Cream", new Color(0.78f, 0.71f, 0.59f));
            Material brick = Mat("Brick", new Color(0.40f, 0.18f, 0.13f));
            Material blue = Mat("Blue", new Color(0.13f, 0.28f, 0.43f));
            Material dark = Mat("Dark", new Color(0.055f, 0.065f, 0.08f));
            Material red = Mat("Red", new Color(0.57f, 0.10f, 0.09f));
            Material white = Mat("White", new Color(0.89f, 0.88f, 0.83f));
            Material glass = Mat("Glass", new Color(0.11f, 0.19f, 0.24f));
            Material wood = Mat("Wood", new Color(0.36f, 0.21f, 0.12f));
            Material yellow = Mat("Yellow", new Color(0.91f, 0.63f, 0.09f));

            Box(root.transform, "Ground", new Vector3(0, -0.4f, 0), new Vector3(58, 0.6f, 40), grass);
            Box(root.transform, "Road", new Vector3(0, 0, 0), new Vector3(58, 0.12f, 10), road);
            Box(root.transform, "SidewalkNorth", new Vector3(0, 0.10f, -7), new Vector3(58, 0.24f, 4), pavement);
            Box(root.transform, "SidewalkSouth", new Vector3(0, 0.10f, 7), new Vector3(58, 0.24f, 4), pavement);

            for (int x = -26; x <= 26; x += 4)
                Box(root.transform, "LaneMark", new Vector3(x, 0.08f, 0), new Vector3(2f, 0.025f, 0.12f), white);

            // Walkable interiors: front wall has a real doorway gap.
            WalkableBuilding(root.transform, "APARTMENTS", new Vector3(-18, 0.2f, -12), new Vector3(8, 5.6f, 6), cream, dark, glass, false);
            WalkableBuilding(root.transform, "DINER", new Vector3(-7.5f, 0.2f, -12), new Vector3(8, 4.6f, 6), brick, red, glass, true);
            WalkableBuilding(root.transform, "CITY BANK", new Vector3(3, 0.2f, -12), new Vector3(9, 5.5f, 6), blue, white, glass, true);
            WalkableBuilding(root.transform, "PUB", new Vector3(15, 0.2f, -12), new Vector3(9, 4.8f, 6), dark, red, glass, true);

            WalkableBuilding(root.transform, "MARKET", new Vector3(-19, 0.2f, 12), new Vector3(9, 4.7f, 6), brick, cream, glass, true);
            WalkableBuilding(root.transform, "KIOSK", new Vector3(-7, 0.2f, 12), new Vector3(7, 4.1f, 6), cream, red, glass, true);
            GasStation(root.transform, new Vector3(7, 0.2f, 12), white, red, dark, yellow, glass);
            WalkableBuilding(root.transform, "LOFTS", new Vector3(20, 0.2f, 12), new Vector3(8, 5.8f, 6), cream, blue, glass, false);

            for (int x = -24; x <= 24; x += 8)
            {
                Lamp(root.transform, new Vector3(x, 0, -5.5f), dark, yellow);
                Lamp(root.transform, new Vector3(x + 4, 0, 5.5f), dark, yellow);
            }

            Bench(root.transform, new Vector3(-12, 0.15f, -5.7f), dark, wood);
            Bench(root.transform, new Vector3(1, 0.15f, 5.7f), dark, wood);
            Trash(root.transform, new Vector3(-2, 0.55f, -5.7f), dark);
            Trash(root.transform, new Vector3(16, 0.55f, 5.7f), dark);

            ImproveLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            if (scene.path == ScenePath) EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("CYDOY", "Visual Prototype V2 built. Buildings are now walkable through the front entrances.", "Nice");
        }

        private static void WalkableBuilding(Transform parent, string name, Vector3 origin, Vector3 size, Material wall, Material accent, Material glass, bool furnished)
        {
            GameObject b = new(name); b.transform.SetParent(parent);
            float w = size.x; float h = size.y; float d = size.z; float t = 0.22f; float doorW = 1.7f; float doorH = 2.5f;

            Box(b.transform, "Floor", origin + new Vector3(0, 0.08f, 0), new Vector3(w, 0.16f, d), Mat("InteriorFloor", new Color(0.24f,0.25f,0.26f)));
            Box(b.transform, "BackWall", origin + new Vector3(0, h/2f, d/2f), new Vector3(w, h, t), wall);
            Box(b.transform, "LeftWall", origin + new Vector3(-w/2f, h/2f, 0), new Vector3(t, h, d), wall);
            Box(b.transform, "RightWall", origin + new Vector3(w/2f, h/2f, 0), new Vector3(t, h, d), wall);
            Box(b.transform, "Roof", origin + new Vector3(0, h, 0), new Vector3(w + .25f, .22f, d + .25f), accent);

            float sideWidth = (w - doorW) / 2f;
            Box(b.transform, "FrontLeft", origin + new Vector3(-(doorW/2f + sideWidth/2f), h/2f, -d/2f), new Vector3(sideWidth, h, t), wall);
            Box(b.transform, "FrontRight", origin + new Vector3((doorW/2f + sideWidth/2f), h/2f, -d/2f), new Vector3(sideWidth, h, t), wall);
            Box(b.transform, "DoorHeader", origin + new Vector3(0, doorH + (h-doorH)/2f, -d/2f), new Vector3(doorW, h-doorH, t), wall);

            Box(b.transform, "Sign", origin + new Vector3(0, h - 0.65f, -d/2f - 0.15f), new Vector3(Mathf.Min(w*0.55f, 4.2f), 0.55f, 0.15f), accent);
            Box(b.transform, "WindowL", origin + new Vector3(-w*0.28f, 1.65f, -d/2f - 0.13f), new Vector3(1.5f, 1.25f, 0.08f), glass);
            Box(b.transform, "WindowR", origin + new Vector3(w*0.28f, 1.65f, -d/2f - 0.13f), new Vector3(1.5f, 1.25f, 0.08f), glass);

            // Small entrance step makes the threshold readable.
            Box(b.transform, "Step", origin + new Vector3(0, 0.12f, -d/2f - 0.6f), new Vector3(2.3f, 0.18f, 1f), accent);

            if (furnished)
            {
                Box(b.transform, "Counter", origin + new Vector3(0, 0.65f, d*0.23f), new Vector3(w*0.55f, 1.1f, 0.7f), accent);
                Box(b.transform, "ShelfL", origin + new Vector3(-w*0.3f, 0.8f, 0.2f), new Vector3(0.45f, 1.6f, 2.2f), wall);
                Box(b.transform, "ShelfR", origin + new Vector3(w*0.3f, 0.8f, 0.2f), new Vector3(0.45f, 1.6f, 2.2f), wall);
            }
        }

        private static void GasStation(Transform parent, Vector3 origin, Material body, Material accent, Material dark, Material yellow, Material glass)
        {
            GameObject g = new("GAS STATION"); g.transform.SetParent(parent);
            WalkableBuilding(g.transform, "SHOP", origin + new Vector3(4,0,0), new Vector3(6,4.2f,6), body, accent, glass, true);
            Box(g.transform, "Canopy", origin + new Vector3(-2.8f, 3.2f, 0), new Vector3(9, 0.3f, 5.6f), accent);
            Box(g.transform, "PillarL", origin + new Vector3(-6.2f, 1.6f, 1.8f), new Vector3(0.32f, 3.2f, 0.32f), dark);
            Box(g.transform, "PillarR", origin + new Vector3(0.6f, 1.6f, 1.8f), new Vector3(0.32f, 3.2f, 0.32f), dark);
            Pump(g.transform, origin + new Vector3(-4.2f, 0.8f, 0), dark, yellow);
            Pump(g.transform, origin + new Vector3(-1.2f, 0.8f, 0), dark, yellow);
        }

        private static void Pump(Transform parent, Vector3 pos, Material dark, Material accent)
        {
            Box(parent, "Pump", pos, new Vector3(0.8f, 1.6f, 0.85f), dark);
            Box(parent, "Display", pos + new Vector3(0,0.3f,-0.45f), new Vector3(0.45f,0.32f,0.06f), accent);
        }

        private static void Lamp(Transform parent, Vector3 p, Material dark, Material glow)
        {
            GameObject l = new("StreetLamp"); l.transform.SetParent(parent);
            Box(l.transform, "Pole", p + new Vector3(0,2f,0), new Vector3(.12f,4f,.12f), dark);
            Box(l.transform, "Arm", p + new Vector3(.35f,3.9f,0), new Vector3(.7f,.1f,.1f), dark);
            Box(l.transform, "Lamp", p + new Vector3(.7f,3.75f,0), new Vector3(.35f,.18f,.28f), glow);
        }

        private static void Bench(Transform parent, Vector3 p, Material dark, Material wood)
        {
            GameObject b = new("Bench"); b.transform.SetParent(parent);
            Box(b.transform, "Seat", p + new Vector3(0,.55f,0), new Vector3(2.2f,.18f,.7f), wood);
            Box(b.transform, "Back", p + new Vector3(0,1.05f,.28f), new Vector3(2.2f,.8f,.14f), wood);
            Box(b.transform, "LegL", p + new Vector3(-.75f,.25f,0), new Vector3(.12f,.5f,.12f), dark);
            Box(b.transform, "LegR", p + new Vector3(.75f,.25f,0), new Vector3(.12f,.5f,.12f), dark);
        }

        private static void Trash(Transform parent, Vector3 p, Material mat)
        {
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "TrashCan"; t.transform.SetParent(parent); t.transform.position = p; t.transform.localScale = new Vector3(.45f,.55f,.45f); t.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent); go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
        }

        private static Material Mat(string name, Color color)
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

        private static void ImproveLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.56f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.78f);
            RenderSettings.fogDensity = 0.0028f;

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.91f, 0.78f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }
        }
    }
}
