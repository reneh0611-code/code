using System;
using System.Collections.Generic;
using System.Linq;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class CitySimulationBuilder
    {
        private const string QuaterniusRoot = "Assets/Environment/City/Quaternius";
        private const string CityRootName = "SimulationCity";
        private const float BlockSize = 30f;
        private const float RoadWidth = 9f;
        private const float SidewalkWidth = 2.8f;
        private const float LotInset = 2.5f;

        private sealed class Candidate
        {
            public GameObject Asset;
            public string Path;
            public string Name;
            public Vector3 Size;
            public float Score;
        }

        private readonly struct BuildingSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly CityDistrict District;
            public readonly CityBuildingType Type;
            public readonly bool Jobs;
            public BuildingSpec(string id,string name,CityDistrict district,CityBuildingType type,bool jobs)
            { Id=id;Name=name;District=district;Type=type;Jobs=jobs; }
        }

        [MenuItem("Tools/CYDOY/City/Build Simulation City")]
        public static void BuildCity()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(!scene.IsValid()){EditorUtility.DisplayDialog("CYDOY City","No active scene found.","OK");return;}

            List<Candidate> candidates=FindBuildingCandidates();
            if(candidates.Count<4)
            {
                EditorUtility.DisplayDialog("CYDOY City",$"Only {candidates.Count} suitable Quaternius building assets were detected.\n\nLet Unity finish importing the FBX/PNG files and try again.","OK");
                return;
            }

            GameObject old=GameObject.Find(CityRootName);
            if(old!=null)Undo.DestroyObjectImmediate(old);

            GameObject root=new(CityRootName);
            Undo.RegisterCreatedObjectUndo(root,"Build Simulation City");

            Transform roads=Child(root.transform,"Roads");
            Transform downtown=Child(root.transform,"Downtown");
            Transform residential=Child(root.transform,"Residential");
            Transform commercial=Child(root.transform,"Commercial");
            Transform industrial=Child(root.transform,"Industrial");
            Transform civic=Child(root.transform,"Civic");
            Transform props=Child(root.transform,"StreetLife");

            Material roadMat=MaterialAsset("City_Road",new Color(.105f,.112f,.118f),.20f);
            Material walkMat=MaterialAsset("City_Sidewalk",new Color(.40f,.405f,.395f),.30f);
            Material curbMat=MaterialAsset("City_Curb",new Color(.54f,.545f,.53f),.32f);
            Material stripeMat=MaterialAsset("City_RoadMark",new Color(.88f,.86f,.78f),.35f);
            Material grassMat=MaterialAsset("City_Grass",new Color(.19f,.31f,.19f),.14f);

            BuildRoadGrid(roads,roadMat,walkMat,curbMat,stripeMat,grassMat);

            BuildingSpec[] specs={
                new("bank_01","Eastwood Bank",CityDistrict.Downtown,CityBuildingType.Bank,true),
                new("jobcenter_01","Eastwood Job Center",CityDistrict.Downtown,CityBuildingType.JobCenter,false),
                new("restaurant_01","Downtown Diner",CityDistrict.Downtown,CityBuildingType.Restaurant,true),
                new("office_01","Mercer Offices",CityDistrict.Downtown,CityBuildingType.Office,true),
                new("market_01","Fresh Market",CityDistrict.Commercial,CityBuildingType.Supermarket,true),
                new("clothes_01","District Clothing",CityDistrict.Commercial,CityBuildingType.ClothingStore,true),
                new("gym_01","Eastwood Fitness",CityDistrict.Commercial,CityBuildingType.Gym,true),
                new("dealer_01","Eastwood Motors",CityDistrict.Commercial,CityBuildingType.CarDealer,true),
                new("apartment_01","Maple Apartments",CityDistrict.Residential,CityBuildingType.Apartment,false),
                new("apartment_02","Oak Apartments",CityDistrict.Residential,CityBuildingType.Apartment,false),
                new("residential_01","Pine Residence",CityDistrict.Residential,CityBuildingType.Residential,false),
                new("residential_02","River Residence",CityDistrict.Residential,CityBuildingType.Residential,false),
                new("warehouse_01","Eastwood Logistics",CityDistrict.Industrial,CityBuildingType.Warehouse,true),
                new("workshop_01","Auto Works",CityDistrict.Industrial,CityBuildingType.Workshop,true),
                new("industrial_01","Northside Industry",CityDistrict.Industrial,CityBuildingType.Industrial,true),
                new("gas_01","Eastwood Fuel",CityDistrict.Industrial,CityBuildingType.GasStation,true),
                new("cityhall_01","Eastwood City Hall",CityDistrict.Civic,CityBuildingType.CityHall,true),
                new("hospital_01","Eastwood Medical",CityDistrict.Civic,CityBuildingType.Hospital,true),
                new("police_01","Eastwood Police",CityDistrict.Civic,CityBuildingType.PoliceStation,true),
                new("fire_01","Eastwood Fire & Rescue",CityDistrict.Civic,CityBuildingType.FireStation,true)
            };

            Vector3[] positions={
                new(-21,0,-21),new(21,0,-21),new(-21,0,21),new(21,0,21),
                new(-51,0,-21),new(-51,0,21),new(-51,0,51),new(-51,0,-51),
                new(51,0,-21),new(51,0,21),new(51,0,51),new(51,0,-51),
                new(-21,0,51),new(21,0,51),new(-21,0,81),new(21,0,81),
                new(-21,0,-51),new(21,0,-51),new(-21,0,-81),new(21,0,-81)
            };

            Quaternion[] rotations={
                Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0),Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0),
                Quaternion.Euler(0,90,0),Quaternion.Euler(0,90,0),Quaternion.Euler(0,90,0),Quaternion.Euler(0,90,0),
                Quaternion.Euler(0,-90,0),Quaternion.Euler(0,-90,0),Quaternion.Euler(0,-90,0),Quaternion.Euler(0,-90,0),
                Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0),Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0),
                Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0),Quaternion.Euler(0,0,0),Quaternion.Euler(0,180,0)
            };

            Dictionary<CityDistrict,Transform> districts=new(){
                {CityDistrict.Downtown,downtown},{CityDistrict.Residential,residential},{CityDistrict.Commercial,commercial},{CityDistrict.Industrial,industrial},{CityDistrict.Civic,civic}
            };

            System.Random rng=new(64021);
            List<Candidate> pool=candidates.Take(Mathf.Min(50,candidates.Count)).ToList();
            for(int i=0;i<specs.Length;i++)
            {
                Candidate pick=PickCandidate(pool,specs[i].District,rng,i);
                PlaceBuilding(districts[specs[i].District],pick,specs[i],positions[i],rotations[i]);
            }

            BuildStreetLife(props,walkMat,curbMat,grassMat);

            EditorSceneManager.MarkSceneDirty(scene);
            if(!string.IsNullOrWhiteSpace(scene.path))EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root;
            EditorGUIUtility.PingObject(root);
            Debug.Log($"[CYDOY CITY] Built simulation city using {candidates.Count} detected Quaternius candidates. Gameplay building metadata is active.",root);
            EditorUtility.DisplayDialog("CYDOY City",$"Simulation city built.\n\nDetected Quaternius candidates: {candidates.Count}\nGameplay buildings: {specs.Length}\n\nEvery important building already has an ID, district and gameplay type for future jobs/economy.","Done");
        }

        [MenuItem("Tools/CYDOY/City/Analyze Quaternius Buildings")]
        public static void Analyze()
        {
            var list=FindBuildingCandidates();
            foreach(var c in list.Take(80))Debug.Log($"[CYDOY CITY ASSET] {c.Name} size={c.Size.x:F1}x{c.Size.y:F1}x{c.Size.z:F1} score={c.Score:F1} path={c.Path}",c.Asset);
            EditorUtility.DisplayDialog("CYDOY City",$"Detected {list.Count} usable building candidates. The best 80 were written to the Console.","OK");
        }

        private static List<Candidate> FindBuildingCandidates()
        {
            string[] guids=AssetDatabase.FindAssets("t:GameObject",new[]{QuaterniusRoot});
            List<Candidate> result=new();
            foreach(string guid in guids)
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                string lower=path.ToLowerInvariant();
                if(!lower.EndsWith(".fbx"))continue;
                if(!lower.Contains("building_"))continue;
                if(lower.Contains("_lod0")||lower.Contains("_lod1")||lower.Contains("_lod2")||lower.Contains("_collider")||lower.Contains("fakebevel")||lower.Contains("_gap"))continue;

                GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(asset==null)continue;
                Bounds b=GetLocalAssetBounds(asset);
                Vector3 size=b.size;
                if(size.y<1.5f||Mathf.Max(size.x,size.z)<1.5f)continue;
                float footprint=size.x*size.z;
                float score=footprint+size.y*2f;
                if(lower.Contains("corner"))score+=8f;
                if(lower.Contains("window"))score-=7f;
                if(lower.Contains("trim")||lower.Contains("pillar")||lower.Contains("arch"))score-=5f;
                result.Add(new Candidate{Asset=asset,Path=path,Name=asset.name,Size=size,Score=score});
            }
            return result.OrderByDescending(c=>c.Score).ToList();
        }

        private static Candidate PickCandidate(List<Candidate> pool,CityDistrict district,System.Random rng,int index)
        {
            if(pool.Count==0)return null;
            int range=Mathf.Min(18,pool.Count);
            int offset=(index*3+(int)district*5+rng.Next(0,5))%range;
            return pool[offset];
        }

        private static void PlaceBuilding(Transform parent,Candidate candidate,BuildingSpec spec,Vector3 position,Quaternion rotation)
        {
            if(candidate==null)return;
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(candidate.Asset);
            if(instance==null)instance=UnityEngine.Object.Instantiate(candidate.Asset);
            instance.name=$"{spec.Id}__{spec.Name}";
            instance.transform.SetParent(parent,true);
            instance.transform.position=position;
            instance.transform.rotation=rotation;

            Bounds b=WorldBounds(instance.transform);
            instance.transform.position+=Vector3.up*(-b.min.y);

            // Keep the pack's original proportions. Only fix obviously tiny/huge import scales.
            b=WorldBounds(instance.transform);
            float maxHorizontal=Mathf.Max(b.size.x,b.size.z);
            if(maxHorizontal<3f||maxHorizontal>24f)
            {
                float target=Mathf.Clamp(maxHorizontal,7f,15f);
                float factor=target/Mathf.Max(.01f,maxHorizontal);
                instance.transform.localScale*=factor;
                b=WorldBounds(instance.transform);
                instance.transform.position+=Vector3.up*(-b.min.y);
            }

            AddColliders(instance.transform);
            CityBuilding meta=instance.GetComponent<CityBuilding>()??instance.AddComponent<CityBuilding>();
            meta.Configure(spec.Id,spec.Name,spec.District,spec.Type,spec.Jobs,true);
        }

        private static void BuildRoadGrid(Transform parent,Material road,Material sidewalk,Material curb,Material mark,Material grass)
        {
            // Large grass base beneath the compact simulation city.
            Cube(parent,"CityGround",new Vector3(0,-.12f,0),new Vector3(210,.20f,210),grass,true);

            float[] lines={-60f,-30f,0f,30f,60f};
            foreach(float z in lines)BuildHorizontalStreet(parent,z,road,sidewalk,curb,mark);
            foreach(float x in lines)BuildVerticalStreet(parent,x,road,sidewalk,curb,mark);
        }

        private static void BuildHorizontalStreet(Transform parent,float z,Material road,Material sidewalk,Material curb,Material mark)
        {
            Cube(parent,$"Road_H_{z}",new Vector3(0,.005f,z),new Vector3(190,.08f,RoadWidth),road,true);
            float edge=RoadWidth*.5f;
            Cube(parent,$"Walk_H_A_{z}",new Vector3(0,.12f,z-edge-SidewalkWidth*.5f),new Vector3(190,.20f,SidewalkWidth),sidewalk,true);
            Cube(parent,$"Walk_H_B_{z}",new Vector3(0,.12f,z+edge+SidewalkWidth*.5f),new Vector3(190,.20f,SidewalkWidth),sidewalk,true);
            Cube(parent,$"Curb_H_A_{z}",new Vector3(0,.11f,z-edge-.12f),new Vector3(190,.20f,.24f),curb,true);
            Cube(parent,$"Curb_H_B_{z}",new Vector3(0,.11f,z+edge+.12f),new Vector3(190,.20f,.24f),curb,true);
            for(float x=-84;x<=84;x+=8)Cube(parent,"Dash",new Vector3(x,.055f,z),new Vector3(3,.012f,.12f),mark,false);
        }

        private static void BuildVerticalStreet(Transform parent,float x,Material road,Material sidewalk,Material curb,Material mark)
        {
            Cube(parent,$"Road_V_{x}",new Vector3(x,.006f,0),new Vector3(RoadWidth,.08f,190),road,true);
            float edge=RoadWidth*.5f;
            Cube(parent,$"Walk_V_A_{x}",new Vector3(x-edge-SidewalkWidth*.5f,.121f,0),new Vector3(SidewalkWidth,.20f,190),sidewalk,true);
            Cube(parent,$"Walk_V_B_{x}",new Vector3(x+edge+SidewalkWidth*.5f,.121f,0),new Vector3(SidewalkWidth,.20f,190),sidewalk,true);
            Cube(parent,$"Curb_V_A_{x}",new Vector3(x-edge-.12f,.111f,0),new Vector3(.24f,.20f,190),curb,true);
            Cube(parent,$"Curb_V_B_{x}",new Vector3(x+edge+.12f,.111f,0),new Vector3(.24f,.20f,190),curb,true);
            for(float z=-84;z<=84;z+=8)Cube(parent,"Dash",new Vector3(x,.056f,z),new Vector3(.12f,.012f,3),mark,false);
        }

        private static void BuildStreetLife(Transform parent,Material sidewalk,Material curb,Material grass)
        {
            // Simple shapes are intentionally used only for small urban props; buildings themselves come from Quaternius.
            Material dark=MaterialAsset("City_StreetMetal",new Color(.08f,.09f,.10f),.42f);
            Material tree=MaterialAsset("City_Tree",new Color(.16f,.35f,.17f),.18f);
            System.Random rng=new(8128);
            for(int i=0;i<26;i++)
            {
                float x=(float)(rng.NextDouble()*160-80),z=(float)(rng.NextDouble()*160-80);
                // Snap props away from drive lanes toward block edges.
                x=SnapToBlockEdge(x);z=SnapToBlockEdge(z);
                Transform lamp=Child(parent,$"StreetLamp_{i:00}");lamp.position=new Vector3(x,0,z);
                Cube(lamp,"Pole",new Vector3(0,1.8f,0),new Vector3(.09f,3.6f,.09f),dark,false);
                Cube(lamp,"Head",new Vector3(0,3.55f,.12f),new Vector3(.28f,.13f,.55f),dark,false);
            }
            for(int i=0;i<18;i++)
            {
                float x=(i%6)*30-75,z=(i/6)*60-60;
                Transform t=Child(parent,$"Tree_{i:00}");t.position=new Vector3(x+11,0,z+11);
                Cube(t,"Trunk",new Vector3(0,1,0),new Vector3(.35f,2,.35f),curb,false);
                GameObject crown=GameObject.CreatePrimitive(PrimitiveType.Icosphere);crown.name="Crown";crown.transform.SetParent(t,false);crown.transform.localPosition=new Vector3(0,2.75f,0);crown.transform.localScale=new Vector3(2.1f,2.7f,2.1f);crown.GetComponent<Renderer>().sharedMaterial=tree;UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
        }

        private static float SnapToBlockEdge(float value)
        {
            float nearest=Mathf.Round(value/30f)*30f;
            return nearest+(value>=nearest?8.3f:-8.3f);
        }

        private static void AddColliders(Transform root)
        {
            foreach(MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mf.sharedMesh==null||mf.GetComponent<Collider>()!=null)continue;
                MeshCollider mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;
            }
        }

        private static Bounds GetLocalAssetBounds(GameObject asset)
        {
            GameObject temp=UnityEngine.Object.Instantiate(asset);temp.hideFlags=HideFlags.HideAndDontSave;Bounds b=WorldBounds(temp.transform);Vector3 size=b.size;UnityEngine.Object.DestroyImmediate(temp);return new Bounds(Vector3.zero,size);
        }

        private static Bounds WorldBounds(Transform root)
        {
            Renderer[] renderers=root.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return new Bounds(root.position,Vector3.zero);
            Bounds b=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);return b;
        }

        private static Transform Child(Transform parent,string name){GameObject g=new(name);g.transform.SetParent(parent,false);return g.transform;}

        private static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collider)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }

        private static Material MaterialAsset(string name,Color color,float smoothness)
        {
            const string root="Assets/Materials";const string folder="Assets/Materials/City";
            if(!AssetDatabase.IsValidFolder(root))AssetDatabase.CreateFolder("Assets","Materials");if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(root,"City");
            string path=$"{folder}/{name}.mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Color"))m.SetColor("_Color",color);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
    }
}
