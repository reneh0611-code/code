using System;
using System.Collections.Generic;
using System.Linq;
using CheatOnYourDayOnes.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class LowPolyCarPackBuilder
    {
        private const string SourcePath="Assets/Models/Vehicles/LowPoly Car Pack/Source/LowPoly Car Pack.fbx";
        private const string OutputFolder="Assets/Models/Vehicles/LowPoly Car Pack/READY - DRIVEABLE";
        private const string ScenePath="Assets/zzz.unity";
        private const string RootName="LowPoly Cars - PARKING SLOTS";
        private const float ParkingYaw=130f;
        private const float SlotSpacing=2.65f;

        private struct WheelRig{public Transform steering,spin;public float radius;}

        static LowPolyCarPackBuilder(){EditorApplication.delayCall+=TryAutomaticBuild;}

        private static void TryAutomaticBuild()
        {
            if(EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged-=AfterPlayMode;
                EditorApplication.playModeStateChanged+=AfterPlayMode;
                return;
            }
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)
            {
                EditorApplication.delayCall+=TryAutomaticBuild;
                return;
            }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath)==null)return;
            if(AllPrefabsExist())return;
            BuildAndPlace(false);
        }

        private static void AfterPlayMode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.playModeStateChanged-=AfterPlayMode;
            EditorApplication.delayCall+=TryAutomaticBuild;
        }

        [MenuItem("Tools/CYDOY/Vehicles/Build + Place LowPoly Cars In Parking Slots")]
        public static void BuildAndPlaceFromMenu(){BuildAndPlace(true);}

        private static void BuildAndPlace(bool showDialog)
        {
            GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if(source==null){Debug.LogWarning("[CYDOY] LowPoly car FBX is not imported yet.");return;}
            EnsureFolder(OutputFolder);

            List<List<string>> clusters=FindVehicleClusters(source);
            if(clusters.Count!=4){Debug.LogError($"[CYDOY] Expected four car groups, found {clusters.Count}.");return;}
            var paths=new List<string>();
            for(int i=0;i<clusters.Count;i++)
            {
                string path=$"{OutputFolder}/LowPoly Car {i+1:00} - DRIVEABLE.prefab";
                if(BuildVehicle(source,clusters[i],i,path)!=null)paths.Add(path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var prefabs=paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p=>p!=null).ToArray();
            bool placed=PlaceCarsInSlots(prefabs);
            if(showDialog)
                EditorUtility.DisplayDialog("CYDOY · LowPoly Cars",placed
                    ?"Die vier neuen Autos stehen exakt in den weißen Parkplatz-Slots.\n\nE = ein-/aussteigen\nWASD = fahren\nLeertaste = stark bremsen"
                    :"Die fahrbaren Prefabs sind fertig. Öffne zzz.unity und starte diesen Menüpunkt erneut.","Fertig");
        }

        private static List<List<string>> FindVehicleClusters(GameObject source)
        {
            GameObject probe=PrefabUtility.InstantiatePrefab(source) as GameObject;
            if(probe==null)return new List<List<string>>();
            try
            {
                Renderer[] renderers=probe.GetComponentsInChildren<Renderer>(true);
                float[] centers={renderers.Min(r=>r.bounds.center.x),-4f,-2f,renderers.Max(r=>r.bounds.center.x)};
                float min=centers[0],max=centers[3];
                for(int i=0;i<4;i++)centers[i]=Mathf.Lerp(min,max,i/3f);
                int[] assignment=new int[renderers.Length];
                for(int pass=0;pass<20;pass++)
                {
                    for(int r=0;r<renderers.Length;r++)
                    {
                        float best=float.PositiveInfinity;int index=0;
                        for(int c=0;c<4;c++){float d=Mathf.Abs(renderers[r].bounds.center.x-centers[c]);if(d<best){best=d;index=c;}}
                        assignment[r]=index;
                    }
                    for(int c=0;c<4;c++)
                    {
                        var members=Enumerable.Range(0,renderers.Length).Where(r=>assignment[r]==c).ToArray();
                        if(members.Length>0)centers[c]=members.Average(r=>renderers[r].bounds.center.x);
                    }
                }
                int[] order=Enumerable.Range(0,4).OrderBy(c=>centers[c]).ToArray();
                var result=new List<List<string>>();
                foreach(int c in order)result.Add(Enumerable.Range(0,renderers.Length).Where(r=>assignment[r]==c).Select(r=>HierarchyPath(probe.transform,renderers[r].transform)).ToList());
                return result;
            }
            finally{Object.DestroyImmediate(probe);}
        }

        private static GameObject BuildVehicle(GameObject source,List<string> keepPaths,int carIndex,string outputPath)
        {
            GameObject imported=PrefabUtility.InstantiatePrefab(source) as GameObject;
            if(imported==null)return null;
            try
            {
                PrefabUtility.UnpackPrefabInstance(imported,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                imported.name="Visual";
                var keep=new HashSet<string>(keepPaths);
                foreach(Renderer renderer in imported.GetComponentsInChildren<Renderer>(true))
                    if(!keep.Contains(HierarchyPath(imported.transform,renderer.transform)))Object.DestroyImmediate(renderer.gameObject);
                foreach(Collider collider in imported.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);

                GameObject car=new($"LowPoly Car {carIndex+1:00} - DRIVEABLE");
                imported.transform.SetParent(car.transform,false);
                imported.transform.localPosition=Vector3.zero;
                imported.transform.localRotation=Quaternion.identity;

                Bounds before=WorldBounds(car);
                float scale=4.15f/Mathf.Max(.1f,before.size.z);
                imported.transform.localScale*=Mathf.Clamp(scale,.25f,4f);
                Bounds scaled=WorldBounds(car);
                imported.transform.position+=new Vector3(-scaled.center.x,-scaled.min.y,-scaled.center.z);

                Renderer[] vehicleRenderers=car.GetComponentsInChildren<Renderer>(true);
                bool cubeWheelNaming=vehicleRenderers.Any(r=>r.name.StartsWith("Cube.",StringComparison.OrdinalIgnoreCase));
                Renderer[] candidates=vehicleRenderers.Where(r=>IsWheelMesh(r.name,cubeWheelNaming)).ToArray();
                Bounds bodyBounds=WorldBounds(car);
                var groups=GroupWheels(candidates,bodyBounds.center);
                WheelRig fl=RigWheel(imported.transform,groups[0],"FrontLeft");
                WheelRig fr=RigWheel(imported.transform,groups[1],"FrontRight");
                WheelRig rl=RigWheel(imported.transform,groups[2],"RearLeft");
                WheelRig rr=RigWheel(imported.transform,groups[3],"RearRight");

                Bounds b=LocalBounds(car.transform);
                Transform seat=NewMarker(car.transform,"DriverSeat",new Vector3(-b.extents.x*.22f,b.min.y+b.size.y*.58f,b.center.z-.12f));
                Transform exit=NewMarker(car.transform,"ExitPoint",new Vector3(-b.extents.x-.85f,b.min.y+.15f,b.center.z));
                Transform com=NewMarker(car.transform,"CenterOfMass",new Vector3(0f,b.min.y+b.size.y*.24f,b.center.z+.08f));
                Rigidbody rb=car.AddComponent<Rigidbody>();rb.mass=1280f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                DriveableCar driveable=car.AddComponent<DriveableCar>();
                SerializedObject data=new(driveable);
                data.FindProperty("driverSeat").objectReferenceValue=seat;
                data.FindProperty("exitPoint").objectReferenceValue=exit;
                data.FindProperty("centerOfMass").objectReferenceValue=com;
                data.ApplyModifiedPropertiesWithoutUndo();
                VehicleWheelVisuals visuals=car.AddComponent<VehicleWheelVisuals>();
                visuals.Configure(fl.steering,fl.spin,fr.steering,fr.spin,rl.steering,rl.spin,rr.steering,rr.spin,Average(fl.radius,fr.radius,rl.radius,rr.radius));
                return PrefabUtility.SaveAsPrefabAsset(car,outputPath);
            }
            finally{Object.DestroyImmediate(imported.transform.parent!=null?imported.transform.parent.gameObject:imported);}
        }

        private static List<Renderer>[] GroupWheels(Renderer[] wheels,Vector3 center)
        {
            var groups=new[]{new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>()};
            foreach(Renderer wheel in wheels)
            {
                bool front=wheel.bounds.center.z>center.z;
                bool left=wheel.bounds.center.x<center.x;
                int index=front?(left?0:1):(left?2:3);
                groups[index].Add(wheel);
            }
            return groups;
        }

        private static WheelRig RigWheel(Transform visual,List<Renderer> pieces,string cleanName)
        {
            if(pieces.Count==0){Debug.LogWarning($"[CYDOY] Missing wheel group {cleanName}");return default;}
            Bounds bounds=pieces[0].bounds;foreach(Renderer piece in pieces)bounds.Encapsulate(piece.bounds);
            Transform steering=new GameObject($"Wheel_{cleanName}_SteeringPivot").transform;
            steering.SetParent(visual,true);steering.position=bounds.center;steering.rotation=visual.rotation;
            Transform spin=new GameObject($"Wheel_{cleanName}_SpinPivot").transform;
            spin.SetParent(steering,false);spin.localPosition=Vector3.zero;spin.localRotation=Quaternion.identity;
            foreach(Renderer piece in pieces)piece.transform.SetParent(spin,true);
            return new WheelRig{steering=steering,spin=spin,radius=bounds.extents.y};
        }

        private static bool PlaceCarsInSlots(GameObject[] prefabs)
        {
            if(prefabs.Length==0)return false;
            Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=false;
            if(!scene.IsValid()||!scene.isLoaded){scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);opened=true;}
            if(!scene.IsValid()||!scene.isLoaded)return false;
            foreach(DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(car.gameObject.scene==scene)Object.DestroyImmediate(car.gameObject);
            DestroyNamedRoot(scene,"Cartoon City Cars - DRIVEABLE");
            DestroyNamedRoot(scene,RootName);

            GameObject root=new(RootName);SceneManager.MoveGameObjectToScene(root,scene);
            GameObject playerSpawn=FindInScene(scene,"PlayerSpawn");
            Vector3 anchor=playerSpawn!=null?playerSpawn.transform.position:new Vector3(258.51f,.06f,676.44f);
            Quaternion rotation=Quaternion.Euler(0f,ParkingYaw,0f);
            int[] slotIndices={-2,-1,1,2};
            var markers=new ParkingSlotMarker[slotIndices.Length];
            for(int i=0;i<slotIndices.Length;i++)
            {
                GameObject slot=new($"Parking Slot {i+1:00}");slot.transform.SetParent(root.transform,false);
                slot.transform.SetPositionAndRotation(anchor+rotation*(Vector3.right*(slotIndices[i]*SlotSpacing)),rotation);
                markers[i]=slot.AddComponent<ParkingSlotMarker>();
                GameObject car=PrefabUtility.InstantiatePrefab(prefabs[i%prefabs.Length],scene) as GameObject;
                if(car==null)continue;
                car.transform.SetParent(slot.transform,true);car.transform.SetPositionAndRotation(slot.transform.position,slot.transform.rotation);
                ParkedVehicleSpawner.GroundCar(car,anchor.y);
            }
            ParkedVehicleSpawner spawner=root.AddComponent<ParkedVehicleSpawner>();spawner.Configure(prefabs,markers);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[CYDOY] Four LowPoly cars placed precisely in marked parking slots.",root);
            if(opened)EditorSceneManager.CloseScene(scene,true);else{Selection.activeGameObject=root;SceneView.lastActiveSceneView?.FrameSelected();}
            return true;
        }

        private static bool IsWheelMesh(string name,bool cubeWheelNaming)=>cubeWheelNaming?(name.StartsWith("Cube.",StringComparison.OrdinalIgnoreCase)&&!name.Equals("Cube.010",StringComparison.OrdinalIgnoreCase)):name.StartsWith("Circle",StringComparison.OrdinalIgnoreCase);
        private static string HierarchyPath(Transform root,Transform child){string path=child.name;for(Transform p=child.parent;p!=null&&p!=root;p=p.parent)path=p.name+"/"+path;return path;}
        private static Transform NewMarker(Transform parent,string name,Vector3 position){Transform t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        private static float Average(params float[] values){float sum=0;int n=0;foreach(float v in values)if(v>.01f){sum+=v;n++;}return n>0?sum/n:.32f;}
        private static Bounds WorldBounds(GameObject root){Renderer[] rs=root.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(root.transform.position,new Vector3(2,1.5f,4));Bounds b=rs[0].bounds;foreach(Renderer r in rs)b.Encapsulate(r.bounds);return b;}
        private static Bounds LocalBounds(Transform root){Bounds w=WorldBounds(root.gameObject);Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=new(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);foreach(Vector3 c in Corners(w)){Vector3 p=root.InverseTransformPoint(c);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}return new Bounds((min+max)*.5f,max-min);}
        private static Vector3[] Corners(Bounds b){Vector3 n=b.min,x=b.max;return new[]{new Vector3(n.x,n.y,n.z),new Vector3(x.x,n.y,n.z),new Vector3(n.x,x.y,n.z),new Vector3(x.x,x.y,n.z),new Vector3(n.x,n.y,x.z),new Vector3(x.x,n.y,x.z),new Vector3(n.x,x.y,x.z),new Vector3(x.x,x.y,x.z)};}
        private static GameObject FindInScene(Scene scene,string name){foreach(GameObject root in scene.GetRootGameObjects())foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t.gameObject;return null;}
        private static void DestroyNamedRoot(Scene scene,string name){GameObject found=FindInScene(scene,name);if(found!=null)Object.DestroyImmediate(found);}
        private static bool AllPrefabsExist()
        {
            for(int i=1;i<=4;i++)
            {
                GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/LowPoly Car {i:00} - DRIVEABLE.prefab");
                if(prefab==null)return false;
                int spinPivots=prefab.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Wheel_")&&t.name.EndsWith("_SpinPivot"));
                if(spinPivots!=4)return false;
            }
            return true;
        }
        private static void EnsureFolder(string path){string[] parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
    }
}
