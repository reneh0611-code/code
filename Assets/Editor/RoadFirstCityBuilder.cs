using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class RoadFirstCityBuilder
    {
        private const string RoadAssetPath = "Assets/Environment/Roads/RoadModular/road_modular.glb";
        private const string RootName = "RoadFirst_CityLayout";

        private sealed class Module
        {
            public Transform Source;
            public string Label;
            public Bounds Bounds;
            public int Vertices;
            public string Materials;
            public float StraightScore;
            public float CurveScore;
            public float CrossScore;
        }

        [MenuItem("Tools/CYDOY/City/1 - Build Clean Road Network From Pack")]
        public static void BuildCleanRoadNetwork()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(RoadAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("CYDOY Roads", "road_modular.glb is not imported as a GameObject yet. Let Unity/glTFast finish importing it, then try again.", "OK");
                return;
            }

            RemoveOldGeneratedWorld();

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (temp == null) temp = UnityEngine.Object.Instantiate(asset);
            temp.name = "__ROAD_ANALYSIS_TEMP";
            temp.hideFlags = HideFlags.HideAndDontSave;

            List<Module> modules = DiscoverModules(temp.transform);
            if (modules.Count < 3)
            {
                UnityEngine.Object.DestroyImmediate(temp);
                EditorUtility.DisplayDialog("CYDOY Roads", $"Only {modules.Count} usable road modules were detected in the GLB.", "OK");
                return;
            }

            Module straight = modules.OrderByDescending(m => m.StraightScore).FirstOrDefault();
            Module cross = modules.Where(m => m != straight).OrderByDescending(m => m.CrossScore).FirstOrDefault();
            Module curve = modules.Where(m => m != straight && m != cross).OrderByDescending(m => m.CurveScore).FirstOrDefault();

            if (straight == null || curve == null || cross == null)
            {
                UnityEngine.Object.DestroyImmediate(temp);
                EditorUtility.DisplayDialog("CYDOY Roads", "Could not classify straight, curve and intersection modules.", "OK");
                return;
            }

            Debug.Log($"[CYDOY ROAD PICK] STRAIGHT: {straight.Label} size={XZ(straight.Bounds.size)} score={straight.StraightScore:F1}");
            Debug.Log($"[CYDOY ROAD PICK] CURVE: {curve.Label} size={XZ(curve.Bounds.size)} score={curve.CurveScore:F1}");
            Debug.Log($"[CYDOY ROAD PICK] CROSS: {cross.Label} size={XZ(cross.Bounds.size)} score={cross.CrossScore:F1}");

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build clean road network");
            Transform roadRoot = NewChild(root.transform, "RoadNetwork");

            float straightLength = Mathf.Max(straight.Bounds.size.x, straight.Bounds.size.z);
            float crossSpan = Mathf.Max(cross.Bounds.size.x, cross.Bounds.size.z);
            float curveSpan = Mathf.Max(curve.Bounds.size.x, curve.Bounds.size.z);

            // Modular packs normally use the same square footprint for curve/intersection tiles.
            // Use their mean so tiny exporter/bounds differences don't accumulate into visible seams.
            float nodeSpan = (crossSpan + curveSpan) * 0.5f;
            float radius = nodeSpan * 0.5f + straightLength + nodeSpan * 0.5f;

            // Central intersection.
            Place(roadRoot, cross, "Intersection_Center", Vector3.zero, 0f, false);

            // Midpoint intersections of the outer loop.
            Place(roadRoot, cross, "Intersection_North", new Vector3(0,0,radius), 0f, false);
            Place(roadRoot, cross, "Intersection_South", new Vector3(0,0,-radius), 0f, false);
            Place(roadRoot, cross, "Intersection_East", new Vector3(radius,0,0), 0f, false);
            Place(roadRoot, cross, "Intersection_West", new Vector3(-radius,0,0), 0f, false);

            // Four real curved corner pieces from the pack.
            Place(roadRoot, curve, "Curve_NE", new Vector3(radius,0,radius), 0f, false);
            Place(roadRoot, curve, "Curve_SE", new Vector3(radius,0,-radius), 90f, false);
            Place(roadRoot, curve, "Curve_SW", new Vector3(-radius,0,-radius), 180f, false);
            Place(roadRoot, curve, "Curve_NW", new Vector3(-radius,0,radius), 270f, false);

            float halfGap = nodeSpan * .5f + straightLength * .5f;

            // Outer loop: exact one-module gaps between square node tiles.
            PlaceStraight(roadRoot, straight, "Top_W", new Vector3(-halfGap,0,radius), true);
            PlaceStraight(roadRoot, straight, "Top_E", new Vector3(halfGap,0,radius), true);
            PlaceStraight(roadRoot, straight, "Bottom_W", new Vector3(-halfGap,0,-radius), true);
            PlaceStraight(roadRoot, straight, "Bottom_E", new Vector3(halfGap,0,-radius), true);
            PlaceStraight(roadRoot, straight, "Left_N", new Vector3(-radius,0,halfGap), false);
            PlaceStraight(roadRoot, straight, "Left_S", new Vector3(-radius,0,-halfGap), false);
            PlaceStraight(roadRoot, straight, "Right_N", new Vector3(radius,0,halfGap), false);
            PlaceStraight(roadRoot, straight, "Right_S", new Vector3(radius,0,-halfGap), false);

            // Four spokes connecting the center intersection to the loop intersections.
            PlaceStraight(roadRoot, straight, "Center_N", new Vector3(0,0,halfGap), false);
            PlaceStraight(roadRoot, straight, "Center_S", new Vector3(0,0,-halfGap), false);
            PlaceStraight(roadRoot, straight, "Center_E", new Vector3(halfGap,0,0), true);
            PlaceStraight(roadRoot, straight, "Center_W", new Vector3(-halfGap,0,0), true);

            UnityEngine.Object.DestroyImmediate(temp);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog("CYDOY Roads",
                "Road-first layout built from the real road_modular.glb assets.\n\nNo buildings were generated. No extra sidewalks were added. The pack's own road/curb/sidewalk geometry is used throughout.", "Done");
        }

        [MenuItem("Tools/CYDOY/City/0 - Analyze Road Pack Modules")]
        public static void AnalyzeRoadPack()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(RoadAssetPath);
            if (asset == null) { EditorUtility.DisplayDialog("CYDOY Roads", "road_modular.glb is not imported as a GameObject.", "OK"); return; }
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (temp == null) temp = UnityEngine.Object.Instantiate(asset);
            temp.hideFlags = HideFlags.HideAndDontSave;
            List<Module> modules = DiscoverModules(temp.transform);
            foreach (Module m in modules.OrderByDescending(m => Mathf.Max(m.StraightScore, Mathf.Max(m.CurveScore,m.CrossScore))))
                Debug.Log($"[CYDOY ROAD MODULE] {m.Label} | size={XZ(m.Bounds.size)} | verts={m.Vertices} | mats={m.Materials} | S={m.StraightScore:F1} C={m.CurveScore:F1} X={m.CrossScore:F1}");
            UnityEngine.Object.DestroyImmediate(temp);
            EditorUtility.DisplayDialog("CYDOY Roads", $"Analyzed {modules.Count} road modules. Results are in the Console.", "OK");
        }

        private static List<Module> DiscoverModules(Transform root)
        {
            Transform container = root;
            while (container.childCount == 1 && container.GetComponents<Renderer>().Length == 0)
                container = container.GetChild(0);

            List<Transform> roots = new();
            foreach (Transform child in container)
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0) roots.Add(child);

            // Some GLBs have another wrapper level per collection.
            if (roots.Count <= 2)
            {
                roots.Clear();
                foreach (Renderer r in container.GetComponentsInChildren<Renderer>(true))
                {
                    Transform t = r.transform;
                    while (t.parent != null && t.parent != container && t.parent.parent != container) t = t.parent;
                    if (!roots.Contains(t)) roots.Add(t);
                }
            }

            List<Module> result = new();
            foreach (Transform t in roots)
            {
                Bounds b = BoundsOf(t);
                float maxXZ = Mathf.Max(b.size.x,b.size.z), minXZ = Mathf.Min(b.size.x,b.size.z);
                if (maxXZ < 1f || b.size.y > maxXZ * 1.25f) continue; // filters tiny props / tall lamps

                Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] meshes = t.GetComponentsInChildren<MeshFilter>(true);
                int verts = meshes.Sum(m => m.sharedMesh != null ? m.sharedMesh.vertexCount : 0);
                string label = FullLabel(t, container).ToLowerInvariant();
                string mats = string.Join(" ", renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name)).ToLowerInvariant();
                string all = label + " " + mats;
                float ratio = maxXZ / Mathf.Max(.01f,minXZ);
                float flatness = maxXZ / Mathf.Max(.05f,b.size.y);
                float roadMaterial = ContainsAny(all,"road","street","asphalt","lane","pavement","sidewalk") ? 25f : 0f;
                float reject = ContainsAny(all,"lamp","light","pole","sign","tree","bench","barrier") ? 80f : 0f;

                Module m = new(){Source=t,Label=FullLabel(t,container),Bounds=b,Vertices=verts,Materials=mats};
                m.StraightScore = roadMaterial + Mathf.Clamp((ratio-1f)*22f,0,60) + Mathf.Clamp(flatness,0,20) + Keyword(all,45,"straight","long","road_straight","street_straight") - Keyword(all,50,"curve","corner","bend","intersection","junction","cross") - reject;
                m.CurveScore = roadMaterial + Mathf.Clamp(24f-Mathf.Abs(ratio-1f)*22f,0,24) + Keyword(all,75,"curve","curved","corner","bend","turn","round") - Keyword(all,45,"intersection","junction","cross","straight") - reject;
                m.CrossScore = roadMaterial + Mathf.Clamp(26f-Mathf.Abs(ratio-1f)*24f,0,26) + Mathf.Clamp(verts/300f,0,25) + Keyword(all,85,"intersection","junction","crossroad","cross","4way","fourway") - Keyword(all,40,"curve","bend") - reject;
                result.Add(m);
            }
            return result;
        }

        private static void PlaceStraight(Transform parent, Module module, string name, Vector3 center, bool horizontal)
        {
            bool sourceLongX = module.Bounds.size.x >= module.Bounds.size.z;
            float yaw = horizontal ? (sourceLongX ? 0f : 90f) : (sourceLongX ? 90f : 0f);
            Place(parent,module,name,center,yaw,true);
        }

        private static GameObject Place(Transform parent, Module module, string name, Vector3 targetCenter, float yaw, bool exactLongAxis)
        {
            GameObject holder = new(name);
            holder.transform.SetParent(parent,false);
            holder.transform.position = targetCenter;
            holder.transform.rotation = Quaternion.Euler(0,yaw,0);

            GameObject visual = UnityEngine.Object.Instantiate(module.Source.gameObject);
            visual.name = "RoadAsset";
            visual.transform.SetParent(holder.transform,true);

            // Recenter source-layout offsets first, then ground the exact lowest mesh point to y=0.
            Bounds b = BoundsOf(visual.transform);
            Vector3 desired = holder.transform.position;
            visual.transform.position += new Vector3(desired.x-b.center.x, -b.min.y, desired.z-b.center.z);

            AddMeshColliders(visual.transform);
            return holder;
        }

        private static void AddMeshColliders(Transform root)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        private static void RemoveOldGeneratedWorld()
        {
            string[] names = {"SimulationCity","RoadFirst_CityLayout","ModularStreet","RoadPack_Catalog"};
            foreach (string n in names)
            {
                GameObject go = GameObject.Find(n);
                if (go != null) Undo.DestroyObjectImmediate(go);
            }
        }

        private static Bounds BoundsOf(Transform root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(root.position,Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i=1;i<rs.Length;i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static string FullLabel(Transform t, Transform stop)
        {
            string s=t.name; Transform p=t.parent;
            while(p!=null && p!=stop){s=p.name+"/"+s;p=p.parent;}
            return s;
        }

        private static bool ContainsAny(string text, params string[] words) => words.Any(text.Contains);
        private static float Keyword(string text,float score,params string[] words) => ContainsAny(text,words) ? score : 0f;
        private static string XZ(Vector3 s) => $"{s.x:F2} x {s.z:F2}";
        private static Transform NewChild(Transform p,string n){GameObject g=new(n);g.transform.SetParent(p,false);return g.transform;}
    }
}
