using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class ModularStreetBuilder
    {
        private const float RoadWidth = 10f;
        private const float SegmentLength = 8f;
        private const float TotalLength = 64f;
        private const float SidewalkWidth = 3.25f;
        private const float SidewalkHeight = 0.18f;
        private const float CurbWidth = 0.30f;

        [MenuItem("Tools/CYDOY/Build Premium Modular Street")]
        public static void BuildPremiumStreet()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            GameObject visualRoot = GameObject.Find("VisualPrototype") ?? new GameObject("VisualPrototype");
            RemoveLegacyStreet(visualRoot.transform);

            Transform old = visualRoot.transform.Find("ModularStreet");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject streetRoot = new("ModularStreet");
            streetRoot.transform.SetParent(visualRoot.transform, false);

            Material asphaltA = Mat("Street_Asphalt_A", new Color(.105f,.110f,.113f), .22f);
            Material asphaltB = Mat("Street_Asphalt_B", new Color(.095f,.100f,.103f), .19f);
            Material asphaltDark = Mat("Street_Asphalt_Dark", new Color(.072f,.076f,.078f), .16f);
            Material patch = Mat("Street_Asphalt_Patch", new Color(.070f,.073f,.075f), .13f);
            Material curb = Mat("Street_Curb", new Color(.48f,.49f,.48f), .28f);
            Material curbTop = Mat("Street_Curb_Top", new Color(.57f,.58f,.56f), .31f);
            Material pavingA = Mat("Street_Paving_A", new Color(.43f,.435f,.425f), .32f);
            Material pavingB = Mat("Street_Paving_B", new Color(.385f,.39f,.385f), .29f);
            Material pavingC = Mat("Street_Paving_C", new Color(.455f,.45f,.43f), .27f);
            Material joint = Mat("Street_Paving_Joint", new Color(.18f,.185f,.185f), .16f);
            Material marking = Mat("Street_Marking", new Color(.84f,.83f,.78f), .38f);
            Material drain = Mat("Street_Drain", new Color(.065f,.07f,.075f), .12f);

            int segmentCount = Mathf.CeilToInt(TotalLength / SegmentLength);
            float startX = -segmentCount * SegmentLength * .5f + SegmentLength * .5f;

            Random.InitState(271828);
            for (int i = 0; i < segmentCount; i++)
            {
                float centerX = startX + i * SegmentLength;
                BuildStraightSegment(streetRoot.transform, i, centerX, asphaltA, asphaltB, asphaltDark, patch, curb, curbTop, pavingA, pavingB, pavingC, joint, marking, drain);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path)) EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = streetRoot;
            EditorGUIUtility.PingObject(streetRoot);
            EditorUtility.DisplayDialog("CYDOY", "Street rebuilt with profiled asphalt, chamfered curbs, sloped sidewalks and subtle wear.", "Nice");
        }

        private static void BuildStraightSegment(Transform parent,int index,float centerX,Material asphaltA,Material asphaltB,Material asphaltDark,Material patch,Material curb,Material curbTop,Material pavingA,Material pavingB,Material pavingC,Material joint,Material marking,Material drain)
        {
            GameObject module = new($"StreetModule_{index:00}");
            module.transform.SetParent(parent,false);
            module.transform.position = new Vector3(centerX,0,0);

            Material asphalt = index % 3 == 1 ? asphaltB : asphaltA;
            CreateRoadMesh(module.transform,"Asphalt_Profiled",SegmentLength + .02f,RoadWidth,asphalt);

            // Darkened gutter strips visually anchor the curb without looking painted on.
            ThinBox(module.transform,"Gutter_N",new Vector3(0,.024f,-RoadWidth*.5f+.17f),new Vector3(SegmentLength,.006f,.34f),asphaltDark);
            ThinBox(module.transform,"Gutter_S",new Vector3(0,.024f,RoadWidth*.5f-.17f),new Vector3(SegmentLength,.006f,.34f),asphaltDark);

            BuildSide(module.transform,-1f,index,curb,curbTop,pavingA,pavingB,pavingC,joint,drain);
            BuildSide(module.transform,1f,index,curb,curbTop,pavingA,pavingB,pavingC,joint,drain);

            // Less perfect centre line: slightly varied length and tiny lateral offset.
            float dashLength = 2.85f + Random.Range(-.12f,.16f);
            float dashZ = Random.Range(-.018f,.018f);
            ThinBox(module.transform,"CenterDash",new Vector3(Random.Range(-.08f,.08f),.053f,dashZ),new Vector3(dashLength,.014f,.125f),marking);

            // Construction seam between modules, intentionally subtle.
            if (index > 0)
                ThinBox(module.transform,"RoadJoint",new Vector3(-SegmentLength*.5f,.041f,0),new Vector3(.018f,.006f,RoadWidth-.7f),asphaltDark);

            // Sparse resurfacing patches. Not every module gets one.
            if (index == 2 || index == 5)
            {
                float side = index == 2 ? -1f : 1f;
                float z = side * 2.25f;
                ThinBox(module.transform,"RepairPatch",new Vector3(.7f,.049f,z),new Vector3(2.15f,.010f,1.25f),patch);
                ThinBox(module.transform,"RepairPatchSeamA",new Vector3(-.37f,.056f,z),new Vector3(.018f,.005f,1.16f),asphaltDark);
            }

            // Hairline cracks, short and restrained.
            if (index == 1 || index == 6)
            {
                float z = index == 1 ? 2.8f : -2.55f;
                MakeCrack(module.transform,new Vector3(-1.3f,.057f,z),index == 1 ? 14f : -18f,1.35f,asphaltDark);
                MakeCrack(module.transform,new Vector3(-.1f,.057f,z+.12f),index == 1 ? -9f : 11f,.72f,asphaltDark);
            }
        }

        private static void BuildSide(Transform module,float side,int segmentIndex,Material curb,Material curbTop,Material pavingA,Material pavingB,Material pavingC,Material joint,Material drain)
        {
            float roadEdge = RoadWidth*.5f;

            CreateCurbMesh(module,side < 0 ? "Curb_N" : "Curb_S",SegmentLength,side,roadEdge,curb,curbTop);
            CreateSidewalkMesh(module,side < 0 ? "Sidewalk_N" : "Sidewalk_S",SegmentLength,side,roadEdge + CurbWidth,SidewalkWidth,pavingA);

            // Unequal slabs break the procedural grid. Widths sum to roughly one module.
            float[] slabWidths = { .86f,1.04f,.92f,1.12f,.82f,1.03f,.96f,1.25f };
            float x = -SegmentLength*.5f;
            for (int i = 0; i < slabWidths.Length; i++)
            {
                float width = slabWidths[i];
                float center = x + width*.5f;
                x += width;
                if (center > SegmentLength*.5f) break;

                Material m = ((i + segmentIndex) % 5) switch { 0 => pavingC, 1 => pavingB, _ => pavingA };
                float lift = Random.Range(-.003f,.004f);
                float zJitter = Random.Range(-.012f,.012f);
                float sidewalkCenterZ = side * (roadEdge + CurbWidth + SidewalkWidth*.5f);
                ThinBox(module,$"Paver_{(side<0?"N":"S")}_{i:00}",new Vector3(center,SidewalkHeight+.025f+lift,sidewalkCenterZ+zJitter),new Vector3(width-.022f,.015f,SidewalkWidth-.075f),m);

                if (i > 0)
                    ThinBox(module,$"Joint_{(side<0?"N":"S")}_{i:00}",new Vector3(center-width*.5f,SidewalkHeight+.035f,sidewalkCenterZ),new Vector3(.015f,.004f,SidewalkWidth-.12f),joint);
            }

            // Longitudinal paving joint is offset from the exact middle, like real laid slabs.
            float longJointZ = side * (roadEdge + CurbWidth + 1.10f);
            ThinBox(module,side < 0 ? "LongJoint_N" : "LongJoint_S",new Vector3(0,SidewalkHeight+.036f,longJointZ),new Vector3(SegmentLength,.004f,.014f),joint);

            if (segmentIndex % 2 == 1)
            {
                float drainZ = side * (roadEdge-.11f);
                ThinBox(module,side < 0 ? "Drain_N" : "Drain_S",new Vector3(1.65f,.050f,drainZ),new Vector3(.72f,.014f,.29f),drain);
                for (int slot=-3;slot<=3;slot++)
                    ThinBox(module,$"DrainSlot_{(side<0?"N":"S")}_{slot+3}",new Vector3(1.65f+slot*.085f,.059f,drainZ),new Vector3(.026f,.004f,.22f),joint);
            }
        }

        private static void CreateRoadMesh(Transform parent,string name,float length,float width,Material material)
        {
            // Slight crown: centre is ~2.5 cm higher than edges.
            Vector2[] profile =
            {
                new(-width*.5f,-.12f), new(-width*.5f,.025f), new(-width*.26f,.043f),
                new(0,.052f), new(width*.26f,.043f), new(width*.5f,.025f), new(width*.5f,-.12f)
            };
            GameObject go = CreateExtrudedProfile(parent,name,length,profile,material,true);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void CreateCurbMesh(Transform parent,string name,float length,float side,float roadEdge,Material bodyMat,Material topMat)
        {
            // Profile has a proper bevel on the road-facing upper edge.
            float z0 = side * roadEdge;
            float z1 = side * (roadEdge + .08f);
            float z2 = side * (roadEdge + CurbWidth);
            Vector2[] p = side > 0
                ? new[] { new Vector2(z0,.025f), new Vector2(z0,.11f), new Vector2(z1,.175f), new Vector2(z2,.185f), new Vector2(z2,0f) }
                : new[] { new Vector2(z2,0f), new Vector2(z2,.185f), new Vector2(z1,.175f), new Vector2(z0,.11f), new Vector2(z0,.025f) };

            CreateExtrudedProfile(parent,name,length,p,bodyMat,true);
            float topCenterZ = side * (roadEdge + CurbWidth*.69f);
            ThinBox(parent,name+"_TopTone",new Vector3(0,.190f,topCenterZ),new Vector3(length,.008f,CurbWidth*.54f),topMat);
        }

        private static void CreateSidewalkMesh(Transform parent,string name,float length,float side,float innerZ,float width,Material mat)
        {
            float outerZ = innerZ + width;
            if (side < 0) { innerZ = -innerZ; outerZ = -outerZ; }

            // Tiny crossfall toward the curb for drainage (~1.2%).
            float innerY = .185f;
            float outerY = .222f;
            Vector2[] p = side > 0
                ? new[] { new Vector2(innerZ,0),new Vector2(innerZ,innerY),new Vector2(outerZ,outerY),new Vector2(outerZ,0) }
                : new[] { new Vector2(outerZ,0),new Vector2(outerZ,outerY),new Vector2(innerZ,innerY),new Vector2(innerZ,0) };
            CreateExtrudedProfile(parent,name,length,p,mat,true);
        }

        private static GameObject CreateExtrudedProfile(Transform parent,string name,float length,Vector2[] yz,Material material,bool collider)
        {
            int n = yz.Length;
            List<Vector3> verts = new();
            List<int> tris = new();
            float x0 = -length*.5f, x1 = length*.5f;

            for (int i=0;i<n;i++) { verts.Add(new Vector3(x0,yz[i].y,yz[i].x)); verts.Add(new Vector3(x1,yz[i].y,yz[i].x)); }
            for (int i=0;i<n-1;i++)
            {
                int a=i*2,b=a+1,c=a+2,d=a+3;
                tris.Add(a);tris.Add(c);tris.Add(b);tris.Add(b);tris.Add(c);tris.Add(d);
            }
            // End caps as fans.
            for (int i=1;i<n-1;i++) { tris.Add(0);tris.Add(i*2);tris.Add((i+1)*2); tris.Add(1);tris.Add((i+1)*2+1);tris.Add(i*2+1); }

            Mesh mesh = new(){ name=name+"_Mesh" };
            mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            GameObject go = new(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            if (collider) { MeshCollider mc=go.AddComponent<MeshCollider>();mc.sharedMesh=mesh; }
            return go;
        }

        private static void MakeCrack(Transform parent,Vector3 pos,float angle,float length,Material mat)
        {
            GameObject c = ThinBox(parent,"AsphaltCrack",pos,new Vector3(length,.004f,.025f),mat);
            c.transform.localRotation = Quaternion.Euler(0,angle,0);
        }

        private static GameObject ThinBox(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            Collider c=go.GetComponent<Collider>();if(c!=null)Object.DestroyImmediate(c);return go;
        }

        private static void RemoveLegacyStreet(Transform visualRoot)
        {
            for (int i=visualRoot.childCount-1;i>=0;i--)
            {
                Transform child=visualRoot.GetChild(i);string n=child.name;
                if(n=="Road"||n=="SidewalkNorth"||n=="SidewalkSouth"||n.StartsWith("LaneMark"))Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Material Mat(string name,Color color,float smoothness)
        {
            string folder="Assets/Materials/Prototype";
            if(!AssetDatabase.IsValidFolder("Assets/Materials"))AssetDatabase.CreateFolder("Assets","Materials");
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Materials","Prototype");
            string path=$"{folder}/{name}.mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");mat=new Material(shader){name=name};AssetDatabase.CreateAsset(mat,path);}
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",smoothness);if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
    }
}
