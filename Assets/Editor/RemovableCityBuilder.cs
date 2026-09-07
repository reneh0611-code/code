using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    // Own assets and own root only. Never edits terrain, roads, existing buildings,
    // lighting, navigation data, or the user's scene file on disk.
    [InitializeOnLoad]
    public static class RemovableCityBuilder
    {
        const string RootName="NEUBECKUM - STADT ERWEITERUNG";
        const string Folder="Assets/Generated/NeubeckumCity";
        const string Request=Folder+"/BuildOnce.request";
        const string Result="Library/NeubeckumCityResult.json";
        static readonly Vector3 KnownShop=new Vector3(268.67844f,.047f,673.929f);
        static readonly string[] Models={"01_MarketRow","02_BrickApartments","03_GardenHouse","04_SageCorner","05_Townhouse","06_ModernCourtyard","07_Bungalow","08_GardenBungalow","09_Bakery","10_Bistro","11_Restaurant","12_PoliceStation","13_Dealership","14_WhiteVilla","15_SandTownhouse","16_WhiteBungalow","17_SlateApartments","18_Autohof"};
        static readonly string[] Labels={"Ladenzeile","Klinkerwohnungen","Gartenhaus","Eckhaus","Stadthaus","Wohnhof","Bungalow","Gartenbungalow","Baeckerei","Bistro","Restaurant","Polizeistation","Autohandel","Weisse Villa","Sandfarbenes Stadthaus","Weisser Bungalow","Schiefergraues Wohnhaus","Autohof"};
        static readonly float[] Widths={16,13,10,14,11,16,9,11,9,10,13,16,20,12,10,12,14,40};
        static readonly float[] Depths={11,11,9,12,10,12,8,8,8,9,11,12,14,10,9,9,11,42};
        static readonly float[] Heights={9.3f,9.3f,6.2f,9.3f,6.2f,9.3f,3.1f,3.1f,3.1f,3.1f,6.2f,6.2f,3.1f,6.2f,6.2f,3.1f,9.3f,3.1f};
        static bool extending;
        static readonly Dictionary<string,Material> Palette=new Dictionary<string,Material>();
        class Road { public Vector3 center,along,across; public float length,width; public bool squareStraight; public Vector3[] corners; }
        class Obstacle { public Bounds bounds; public Vector2[] outline; }
        [Serializable] class Summary { public string status,scene,prefab; public int roads,buildings,blocked,nearShop,uneven,trees; public List<Vector3> positions=new List<Vector3>(); }
        static RemovableCityBuilder(){EditorApplication.delayCall+=Pending;}
        static void Pending()
        {
            bool upgrade=File.Exists(Folder+"/UpgradeBuildings.request");
            if(!File.Exists(Request)&&!upgrade)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="zzz")return;
            if(upgrade)
            {
                File.Move(Folder+"/UpgradeBuildings.request",Folder+"/UpgradeBuildings."+DateTime.UtcNow.Ticks+".consumed");
                try{UpgradeBuildings();}catch(Exception e){Debug.LogException(e);}
                return;
            }
            // Consume before attempting. A deleted/hidden city must never regenerate.
            File.Move(Request,Folder+"/BuildOnce."+DateTime.UtcNow.Ticks+".consumed");
            try{Build();}catch(Exception e){Debug.LogException(e);}
        }
        static GameObject FindRoot()=>SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);
        [MenuItem("Day Ones/Stadt/Stadt-Erweiterung bauen")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            var existingRoot=FindRoot();
            if(existingRoot!=null&&!extending){Selection.activeGameObject=existingRoot;return;}
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="zzz")throw new InvalidOperationException("Bitte die Szene zzz öffnen.");
            var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(false)).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
            var entrance=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Where(t=>t.name=="SlidingDoorAssembly"||t.name=="Window2")
                .OrderBy(t=>(t.position-KnownShop).sqrMagnitude).FirstOrDefault();
            Vector3 anchor=entrance!=null&&Vector3.Distance(entrance.position,KnownShop)<100?entrance.position:KnownShop;
            var roads=new List<Road>();
            var occupied=new List<Obstacle>();
            foreach(var r in renderers)
            {
                Bounds b=r.bounds;float ground=CitySiteSurvey.Ground(b.center);
                if(!float.IsNaN(ground)&&b.min.y<ground+2&&b.max.y>ground-.5f&&b.size.x>.3f&&b.size.z>.3f)
                {
                    var mf=r.GetComponent<MeshFilter>();
                    // Keep the footprint aligned with the actual object, not with
                    // world axes. Otherwise every diagonal road blocks its own lots.
                    if(mf!=null&&mf.sharedMesh!=null)
                    {
                        var mesh=mf.sharedMesh;
                        if(mesh.isReadable&&b.size.y<.5f&&Mathf.Max(b.size.x,b.size.z)>30)
                        {
                            // Long curved sidewalks are concave: protect their
                            // real surface triangles, not the empty interior.
                            var vertices=mesh.vertices;var triangles=mesh.triangles;
                            for(int j=0;j<triangles.Length;j+=3)
                                AddObstacle(occupied,new[]{mf.transform.TransformPoint(vertices[triangles[j]]),mf.transform.TransformPoint(vertices[triangles[j+1]]),mf.transform.TransformPoint(vertices[triangles[j+2]])});
                        }
                        else AddObstacle(occupied,Corners(mesh.bounds).Select(c=>mf.transform.TransformPoint(c)));
                    }
                    else AddObstacle(occupied,Corners(b));
                }
            }
            foreach(var pivot in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RoadModulePivot>(false)))
            {
                var rs=pivot.GetComponentsInChildren<MeshFilter>(false);if(rs.Length==0)continue;
                Bounds local=new Bounds();bool first=true;
                foreach(var mf in rs)
                {
                    if(mf.sharedMesh==null)continue;
                    var b=mf.sharedMesh.bounds;
                    foreach(var corner in Corners(b))
                    {
                        Vector3 p=pivot.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if(first){local=new Bounds(p,Vector3.zero);first=false;}else local.Encapsulate(p);
                    }
                }
                if(first)continue;
                Vector3 center=pivot.transform.TransformPoint(local.center);
                float ground=CitySiteSurvey.Ground(center);
                Vector3 scale=pivot.transform.lossyScale;
                float sx=local.size.x*Mathf.Abs(scale.x),sz=local.size.z*Mathf.Abs(scale.z),sy=local.size.y*Mathf.Abs(scale.y);
                float len=Mathf.Max(sx,sz),width=Mathf.Min(sx,sz);
                // Only grounded, long, nearly horizontal modules seed lots.
                // Curves, junctions and parking remain protected by occupied bounds.
                bool squareStraight=pivot.name.Contains("modular_kit03_")||pivot.name.Contains("modular_kit03 ")||pivot.name.EndsWith("modular_kit03");
                if(float.IsNaN(ground)||Mathf.Abs(center.y-ground)>1.5f||sy>1.8f||width<4||width>28||(!squareStraight&&len<width*1.5f)||len>180||
                   Vector3.Distance(new Vector3(center.x,anchor.y,center.z),anchor)>700)continue;
                Vector3 along=sx>sz?pivot.transform.right:pivot.transform.forward;
                if(Mathf.Abs(along.y)>.06f)continue;
                along=Vector3.ProjectOnPlane(along,Vector3.up).normalized;
                roads.Add(new Road{center=center,along=along,across=Vector3.Cross(Vector3.up,along),length=len,width=width,squareStraight=squareStraight,corners=Corners(local).Select(c=>pivot.transform.TransformPoint(c)).ToArray()});
            }
            // Square straight tiles have no longest axis. Infer the continuation
            // from a neighbouring straight tile, never from world north.
            foreach(var road in roads.Where(r=>r.squareStraight))
            {
                var neighbour=roads.Where(r=>r!=road&&r.squareStraight&&Vector3.Distance(r.center,road.center)>road.length*.55f&&Vector3.Distance(r.center,road.center)<road.length*1.6f)
                    .OrderBy(r=>(r.center-road.center).sqrMagnitude).FirstOrDefault();
                if(neighbour==null){road.length=0;continue;}
                road.along=Vector3.ProjectOnPlane(neighbour.center-road.center,Vector3.up).normalized;
                road.across=Vector3.Cross(Vector3.up,road.along);
            }
            roads.RemoveAll(r=>r.length<=0);
            foreach(var road in roads)
            {
                // These straight tiles are wider across the carriageway (8 m)
                // than along it (about 5.7 m). Do not swap length and width.
                road.width=road.corners.Max(c=>Vector3.Dot(c-road.center,road.across))-road.corners.Min(c=>Vector3.Dot(c-road.center,road.across));
                road.length=road.corners.Max(c=>Vector3.Dot(c-road.center,road.along))-road.corners.Min(c=>Vector3.Dot(c-road.center,road.along));
            }
            roads=roads.OrderBy(r=>(r.center-anchor).sqrMagnitude).ThenBy(r=>r.center.x).ThenBy(r=>r.center.z).ToList();
            if(roads.Count==0)throw new InvalidOperationException("Keine sicher erkannten Straßenabschnitte: keine Stadt platziert.");
            foreach(var n in Models)if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/"+n+".obj")==null)
                throw new InvalidOperationException("Gebäudemodell noch nicht importiert: "+n);
            EnsurePalette();
            var root=existingRoot!=null?existingRoot:new GameObject(RootName);
            var added=new List<GameObject>();int attempted=0;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Stadt-Erweiterung");
            if(existingRoot==null)Undo.RegisterCreatedObjectUndo(root,"Stadt-Erweiterung");
            var summary=new Summary{scene=scene.path,roads=roads.Count,prefab=Folder+"/Neubeckum - Stadt.prefab"};
            try
            {
                foreach(var road in roads)
                {
                    int samples=Mathf.Max(1,Mathf.FloorToInt(road.length/23));
                    for(int s=0;s<samples;s++)for(int side=-1;side<=1;side+=2)
                    {
                        if(summary.buildings>=96)break;
                        int index=extending?6+(attempted++%(Models.Length-6)):(attempted++*5)%Models.Length;
                        float width=Widths[index],depth=Depths[index];
                        var away=road.across*side;
                        var pos=road.center+road.along*((s+.5f)/samples-.5f)*road.length+away*(road.width*.5f+7+depth*.5f);
                        if(Vector2.Distance(new Vector2(pos.x,pos.z),new Vector2(anchor.x,anchor.z))<72){summary.nearShop++;continue;}
                        var rotation=Quaternion.LookRotation(away,Vector3.up);
                        Bounds footprint=Footprint(pos,rotation,width+5,depth+11);
                        Vector2[] outline=Hull(Corners(new Bounds(Vector3.zero,new Vector3(width+5,0,depth+11))).Select(c=>pos+rotation*c));
                        if(occupied.Any(b=>OverlapXZ(footprint,b.bounds)&&PolygonsOverlap(outline,b.outline,.35f))){summary.blocked++;continue;}
                        if(!GroundLot(footprint,out float height)){summary.uneven++;continue;}
                        if(ContainsTerrainTree(footprint)){summary.trees++;continue;}
                        pos.y=height;
                        var plot=new GameObject("Quartier "+(summary.buildings+1).ToString("00")+" - "+Models[index]);
                        added.Add(plot);Undo.RegisterCreatedObjectUndo(plot,"Gebäude hinzufügen");
                        plot.transform.SetParent(root.transform,false);plot.transform.SetPositionAndRotation(pos,rotation);
                        var building=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/"+Models[index]+".obj"));
                        building.transform.SetParent(plot.transform,false);
                        Normalize(building,width);
                        foreach(var renderer in building.GetComponentsInChildren<Renderer>())
                        {
                            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>GetMaterial(m!=null?m.name:"Plaster")).ToArray();
                            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
                        }
                        var collider=plot.AddComponent<BoxCollider>();
                        collider.center=new Vector3(0,Heights[index]*.5f,0);
                        collider.size=new Vector3(width,Heights[index],depth);
                        MakeSelectable(plot,index);
                        AddSign(plot.transform,index);
                        FixForecourtCollision(plot,index);
                        if(index!=17)
                        {
                        Box(plot.transform,"Sockel",new Vector3(0,-.12f,0),new Vector3(width+.25f,.24f,depth+.25f),"Base",true);
                        Box(plot.transform,"Eingangsweg",new Vector3(0,.025f,-depth*.5f-2.4f),new Vector3(2.4f,.05f,4.8f),"Trim",true);
                        Box(plot.transform,"Beet hinten",new Vector3(width*.25f,.15f,depth*.5f+2),new Vector3(width*.4f,.3f,1.5f),"Base",false);
                        Box(plot.transform,"Hecke",new Vector3(width*.25f,.6f,depth*.5f+2),new Vector3(width*.4f-.2f,.75f,1.3f),"Sage",false);
                        if(summary.buildings%3==0)
                            Prop(plot.transform,"Assets/Props/READY - DRAG INTO SCENE/City Props Collection Volume 1/Bench.prefab",
                                new Vector3(width*.33f,0,-depth*.5f-2),1.8f);
                        if(summary.buildings%4==0)
                            Prop(plot.transform,"Assets/Models/Buildings/ModularModernHousePack/Prefabs/Props/Bike Stand.prefab",
                                new Vector3(-width*.33f,0,-depth*.5f-2),1.7f);
                        }
                        var lod=plot.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,plot.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                        occupied.Add(new Obstacle{bounds=footprint,outline=outline});summary.positions.Add(pos);summary.buildings++;
                    }
                }
                if(summary.buildings==0&&!extending){summary.status="No sites: see rejection counts";File.WriteAllText(Result,JsonUtility.ToJson(summary,true));throw new InvalidOperationException("Keine sicheren Bauplätze. Details: Library/NeubeckumCityResult.json");}
                PrefabUtility.SaveAsPrefabAsset(root,summary.prefab);
                EditorSceneManager.MarkSceneDirty(scene); // Deliberately no SaveScene.
                summary.status="built";File.WriteAllText(Result,JsonUtility.ToJson(summary,true));
                Selection.activeGameObject=root;EditorGUIUtility.PingObject(root);
                Show();
                Debug.Log("[CITY] "+summary.buildings+" Gebäude. Straßen unverändert. Entfernen: Day Ones > Stadt > Stadt-Erweiterung entfernen.");
            }
            catch(Exception e) {summary.status=e.Message;File.WriteAllText(Result,JsonUtility.ToJson(summary,true));if(existingRoot==null)Undo.DestroyObjectImmediate(root);else foreach(var g in added)if(g!=null)Undo.DestroyObjectImmediate(g);throw;}
            finally {Undo.CollapseUndoOperations(undo);}
        }
        [MenuItem("Day Ones/Stadt/Gebaeude aktualisieren und Varianten hinzufuegen")]
        public static void UpgradeBuildings()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            foreach(var model in Models)if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/"+model+".obj")==null)
                throw new InvalidOperationException("Bitte zuerst alle Gebaeudemodelle importieren lassen: "+model);
            EnsurePalette();
            string ready=Folder+"/EINZELNE GEBAEUDE";
            if(!AssetDatabase.IsValidFolder(ready))AssetDatabase.CreateFolder(Folder,"EINZELNE GEBAEUDE");
            for(int i=0;i<Models.Length;i++)
            {
                var g=new GameObject(Labels[i]);
                try
                {
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/"+Models[i]+".obj"));
                    model.transform.SetParent(g.transform,false);Normalize(model,Widths[i]);
                    foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>GetMaterial(m!=null?m.name:"Plaster")).ToArray();
                    var collider=g.AddComponent<BoxCollider>();collider.center=Vector3.up*Heights[i]*.5f;collider.size=new Vector3(Widths[i],Heights[i],Depths[i]);
                    MakeSelectable(g,i);AddSign(g.transform,i);FixForecourtCollision(g,i);
                    PrefabUtility.SaveAsPrefabAsset(g,ready+"/"+(i+1).ToString("00")+" - "+Labels[i]+".prefab");
                }
                finally{Object.DestroyImmediate(g);}
            }
            var root=FindRoot();int existing=0;
            if(root!=null)
            {
                // Only our generated city can be unpacked; never touch other prefabs.
                if(PrefabUtility.IsOutermostPrefabInstanceRoot(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.OutermostRoot,InteractionMode.UserAction);
                string placed=ready+"/BEREITS PLATZIERT";
                if(!AssetDatabase.IsValidFolder(placed))AssetDatabase.CreateFolder(ready,"BEREITS PLATZIERT");
                foreach(Transform child in root.transform)
                {
                    int index=Array.FindIndex(Models,n=>child.name.Contains(n));if(index<0)continue;
                    MakeSelectable(child.gameObject,index,true);AddSign(child,index);RefreshLod(child);existing++;
                    var copy=Object.Instantiate(child.gameObject);
                    try{copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(copy,placed+"/"+child.name.Replace('/','-')+".prefab");}
                    finally{Object.DestroyImmediate(copy);}
                }
                // Add only to currently free lots; retain every existing transform.
                extending=true;try{Build();}finally{extending=false;}
                EditorSceneManager.MarkSceneDirty(root.scene);
            }
            File.WriteAllText("Library/NeubeckumBuildingUpgrade.txt","Catalog: "+Models.Length+" prefabs. Existing individual buildings: "+existing+". Placement results: NeubeckumCityResult.json");
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(ready);EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[CITY] "+Models.Length+" einzelne Gebaeude-Prefabs unter "+ready+". Bestehende Positionen behalten.");
        }
        static void MakeSelectable(GameObject g,int index,bool undo=false)
        {
            var info=g.GetComponent<CityBuilding>();
            if(info!=null)return;
            info=undo?Undo.AddComponent<CityBuilding>(g):g.AddComponent<CityBuilding>();
            CityBuildingType type=index==11?CityBuildingType.PoliceStation:index==12||index==17?CityBuildingType.CarDealer:index==9||index==10?CityBuildingType.Restaurant:index==8||index==0?CityBuildingType.GenericShop:CityBuildingType.Residential;
            info.Configure(Guid.NewGuid().ToString("N"),Labels[index],index>=8?CityDistrict.Commercial:CityDistrict.Residential,type,false,false);
        }
        static void AddSign(Transform parent,int index)
        {
            if(index<6)
            {
                int seed=parent.name.Sum(c=>(int)c)%3;
                if(seed!=0)foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials=renderer.sharedMaterials;
                    if(!materials.Any(m=>m!=null&&m.name=="Plaster"))continue;
                    Undo.RecordObject(renderer,"Fassadenfarbe variieren");
                    renderer.sharedMaterials=materials.Select(m=>m!=null&&m.name=="Plaster"?GetMaterial(seed==1?"White":"Sand"):m).ToArray();
                }
            }
            CityFacadeDetails.Apply(parent,index,Widths[index],Depths[index],GetMaterial("White"),GetMaterial("Metal"),GetMaterial("Trim"),GetMaterial("Sage"));
        }

        public static int PlaceCivicBuildings(GameObject[] prefabs)
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="zzz")throw new InvalidOperationException("Bitte die Szene zzz öffnen.");
            var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(false)).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
            var entrance=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Where(t=>t.name=="SlidingDoorAssembly"||t.name=="Window2")
                .OrderBy(t=>(t.position-KnownShop).sqrMagnitude).FirstOrDefault();
            Vector3 anchor=entrance!=null&&Vector3.Distance(entrance.position,KnownShop)<100?entrance.position:KnownShop;
            var roads=new List<Road>();
            var occupied=new List<Obstacle>();
            foreach(var r in renderers)
            {
                Bounds b=r.bounds;float ground=CitySiteSurvey.Ground(b.center);
                if(!float.IsNaN(ground)&&b.min.y<ground+2&&b.max.y>ground-.5f&&b.size.x>.3f&&b.size.z>.3f)
                {
                    var mf=r.GetComponent<MeshFilter>();
                    // Keep the footprint aligned with the actual object, not with
                    // world axes. Otherwise every diagonal road blocks its own lots.
                    if(mf!=null&&mf.sharedMesh!=null)
                    {
                        var mesh=mf.sharedMesh;
                        if(mesh.isReadable&&b.size.y<.5f&&Mathf.Max(b.size.x,b.size.z)>30)
                        {
                            // Long curved sidewalks are concave: protect their
                            // real surface triangles, not the empty interior.
                            var vertices=mesh.vertices;var triangles=mesh.triangles;
                            for(int j=0;j<triangles.Length;j+=3)
                                AddObstacle(occupied,new[]{mf.transform.TransformPoint(vertices[triangles[j]]),mf.transform.TransformPoint(vertices[triangles[j+1]]),mf.transform.TransformPoint(vertices[triangles[j+2]])});
                        }
                        else AddObstacle(occupied,Corners(mesh.bounds).Select(c=>mf.transform.TransformPoint(c)));
                    }
                    else AddObstacle(occupied,Corners(b));
                }
            }
            foreach(var pivot in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RoadModulePivot>(false)))
            {
                var rs=pivot.GetComponentsInChildren<MeshFilter>(false);if(rs.Length==0)continue;
                Bounds local=new Bounds();bool first=true;
                foreach(var mf in rs)
                {
                    if(mf.sharedMesh==null)continue;
                    var b=mf.sharedMesh.bounds;
                    foreach(var corner in Corners(b))
                    {
                        Vector3 p=pivot.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if(first){local=new Bounds(p,Vector3.zero);first=false;}else local.Encapsulate(p);
                    }
                }
                if(first)continue;
                Vector3 center=pivot.transform.TransformPoint(local.center);
                float ground=CitySiteSurvey.Ground(center);
                Vector3 scale=pivot.transform.lossyScale;
                float sx=local.size.x*Mathf.Abs(scale.x),sz=local.size.z*Mathf.Abs(scale.z),sy=local.size.y*Mathf.Abs(scale.y);
                float len=Mathf.Max(sx,sz),width=Mathf.Min(sx,sz);
                // Only grounded, long, nearly horizontal modules seed lots.
                // Curves, junctions and parking remain protected by occupied bounds.
                bool squareStraight=pivot.name.Contains("modular_kit03_")||pivot.name.Contains("modular_kit03 ")||pivot.name.EndsWith("modular_kit03");
                if(float.IsNaN(ground)||Mathf.Abs(center.y-ground)>1.5f||sy>1.8f||width<4||width>28||(!squareStraight&&len<width*1.5f)||len>180||
                   Vector3.Distance(new Vector3(center.x,anchor.y,center.z),anchor)>700)continue;
                Vector3 along=sx>sz?pivot.transform.right:pivot.transform.forward;
                if(Mathf.Abs(along.y)>.06f)continue;
                along=Vector3.ProjectOnPlane(along,Vector3.up).normalized;
                roads.Add(new Road{center=center,along=along,across=Vector3.Cross(Vector3.up,along),length=len,width=width,squareStraight=squareStraight,corners=Corners(local).Select(c=>pivot.transform.TransformPoint(c)).ToArray()});
            }
            // Square straight tiles have no longest axis. Infer the continuation
            // from a neighbouring straight tile, never from world north.
            foreach(var road in roads.Where(r=>r.squareStraight))
            {
                var neighbour=roads.Where(r=>r!=road&&r.squareStraight&&Vector3.Distance(r.center,road.center)>road.length*.55f&&Vector3.Distance(r.center,road.center)<road.length*1.6f)
                    .OrderBy(r=>(r.center-road.center).sqrMagnitude).FirstOrDefault();
                if(neighbour==null){road.length=0;continue;}
                road.along=Vector3.ProjectOnPlane(neighbour.center-road.center,Vector3.up).normalized;
                road.across=Vector3.Cross(Vector3.up,road.along);
            }
            roads.RemoveAll(r=>r.length<=0);
            foreach(var road in roads)
            {
                // These straight tiles are wider across the carriageway (8 m)
                // than along it (about 5.7 m). Do not swap length and width.
                road.width=road.corners.Max(c=>Vector3.Dot(c-road.center,road.across))-road.corners.Min(c=>Vector3.Dot(c-road.center,road.across));
                road.length=road.corners.Max(c=>Vector3.Dot(c-road.center,road.along))-road.corners.Min(c=>Vector3.Dot(c-road.center,road.along));
            }
            roads=roads.OrderBy(r=>(r.center-anchor).sqrMagnitude).ThenBy(r=>r.center.x).ThenBy(r=>r.center.z).ToList();
            if(roads.Count==0)throw new InvalidOperationException("Keine sicher erkannten Straßenabschnitte: keine Stadt platziert.");

            var cityRoot=FindRoot();
            if(cityRoot==null)throw new InvalidOperationException("Bitte zuerst die Stadtgruppe laden. Der Gebäudekatalog wurde erstellt.");
            var district=cityRoot.transform.Find("LOKALE UND OEFFENTLICHE GEBAEUDE");
            if(district==null){var group=new GameObject("LOKALE UND OEFFENTLICHE GEBAEUDE");group.transform.SetParent(cityRoot.transform,false);Undo.RegisterCreatedObjectUndo(group,"Lokale hinzufügen");district=group.transform;}
            int placed=0;
            foreach(var prefab in prefabs)
            {
                if(district.Find(prefab.name)!=null)continue;
                Bounds modelBounds=new Bounds();bool first=true;
                foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(mf.sharedMesh==null)continue;
                    foreach(var c in Corners(mf.sharedMesh.bounds))
                    {var p=prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(c));if(first){modelBounds=new Bounds(p,Vector3.zero);first=false;}else modelBounds.Encapsulate(p);}
                }
                if(first)continue;
                float width=modelBounds.size.x,depth=modelBounds.size.z;
                bool done=false;
                foreach(var road in roads)
                {
                    foreach(int side in new[]{-1,1})
                    {
                        Vector3 away=road.across*side;
                        var rotation=Quaternion.LookRotation(away,Vector3.up);
                        var pos=road.center+away*(road.width*.5f+7+depth*.5f);
                        if(Vector2.Distance(new Vector2(pos.x,pos.z),new Vector2(anchor.x,anchor.z))<72)continue;
                        Bounds footprint=Footprint(pos,rotation,width+4,depth+4);
                        var outline=Hull(Corners(new Bounds(Vector3.zero,new Vector3(width+4,0,depth+4))).Select(c=>pos+rotation*c));
                        if(occupied.Any(b=>OverlapXZ(footprint,b.bounds)&&PolygonsOverlap(outline,b.outline,.35f)))continue;
                        if(!GroundLot(footprint,out float height)||ContainsTerrainTree(footprint))continue;
                        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        Undo.RegisterCreatedObjectUndo(g,"Begehbares Gebäude hinzufügen");g.name=prefab.name;g.transform.SetParent(district,false);
                        Vector3 origin=pos-rotation*new Vector3(modelBounds.center.x,0,modelBounds.center.z);origin.y=height;
                        g.transform.SetPositionAndRotation(origin,rotation);
                        occupied.Add(new Obstacle{bounds=footprint,outline=outline});placed++;done=true;break;
                    }
                    if(done)break;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return placed;
        }

        static void RefreshLod(Transform plot)
        {
            var lod=plot.GetComponent<LODGroup>();if(lod==null)return;
            Undo.RecordObject(lod,"Gebaeudedetails in LOD aufnehmen");lod.SetLODs(new[]{new LOD(.025f,plot.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        }
        static void FixForecourtCollision(GameObject plot,int index)
        {
            if(index!=17)return;
            // The courtyard must remain driveable: never a building-sized solid box.
            Object.DestroyImmediate(plot.GetComponent<BoxCollider>());
            foreach(var mf in plot.GetComponentsInChildren<MeshFilter>())
            {
                if(mf.name!="Plaster"&&mf.name!="Asphalt")continue;
                Bounds bounds=new Bounds();bool first=true;
                foreach(var c in Corners(mf.sharedMesh.bounds))
                {var p=plot.transform.InverseTransformPoint(mf.transform.TransformPoint(c));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                var collider=plot.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
            }
        }
        [MenuItem("Day Ones/Stadt/Einzelne Gebaeude oeffnen")]
        static void OpenCatalog(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(Folder+"/EINZELNE GEBAEUDE");EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Stadt ein-ausblenden")]
        static void Toggle(){var r=FindRoot();if(r==null)return;Undo.RecordObject(r,"Stadt ein-ausblenden");r.SetActive(!r.activeSelf);}
        [MenuItem("Day Ones/Stadt/Stadt anzeigen")]
        static void Show()
        {
            var r=FindRoot();if(r==null){Debug.LogWarning("Noch keine Stadt vorhanden: Day Ones > Stadt > Stadt-Erweiterung bauen.");return;}
            if(!r.activeSelf){Undo.RecordObject(r,"Stadt anzeigen");r.SetActive(true);}
            Selection.activeGameObject=r;EditorGUIUtility.PingObject(r);
            if(r.transform.childCount==0||SceneView.lastActiveSceneView==null)return;
            var rs=r.transform.GetChild(0).GetComponentsInChildren<Renderer>();if(rs.Length==0)return;
            var b=rs[0].bounds;foreach(var renderer in rs)b.Encapsulate(renderer.bounds);b.Expand(40);
            SceneView.lastActiveSceneView.Frame(b,false);
        }
        [MenuItem("Day Ones/Stadt/Stadt-Erweiterung entfernen")]
        static void Remove(){var r=FindRoot();if(r!=null)Undo.DestroyObjectImmediate(r);}
        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                yield return b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
        }
        static Bounds Footprint(Vector3 p,Quaternion q,float w,float d)
        {
            var b=new Bounds(p,Vector3.zero);
            foreach(var c in Corners(new Bounds(Vector3.zero,new Vector3(w,0,d))))b.Encapsulate(p+q*c);
            return b;
        }
        static bool OverlapXZ(Bounds a,Bounds b)=>a.min.x<b.max.x&&a.max.x>b.min.x&&a.min.z<b.max.z&&a.max.z>b.min.z;
        static void AddObstacle(List<Obstacle> obstacles,IEnumerable<Vector3> points)
        {
            var hull=Hull(points);if(hull.Length<3)return;
            var b=new Bounds(new Vector3(hull[0].x,0,hull[0].y),Vector3.zero);
            foreach(var p in hull)b.Encapsulate(new Vector3(p.x,0,p.y));
            b.Expand(new Vector3(.7f,0,.7f));obstacles.Add(new Obstacle{bounds=b,outline=hull});
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static Vector2[] Hull(IEnumerable<Vector3> points)
        {
            var p=points.Select(v=>new Vector2(v.x,v.z)).Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();
            if(p.Length<3)return p;
            var h=new List<Vector2>();
            foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],v)<=.00001f)h.RemoveAt(h.Count-1);h.Add(v);}
            int lower=h.Count;
            for(int i=p.Length-2;i>=0;i--){while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p[i])<=.00001f)h.RemoveAt(h.Count-1);h.Add(p[i]);}
            h.RemoveAt(h.Count-1);return h.ToArray();
        }
        static bool PolygonsOverlap(Vector2[] a,Vector2[] b,float clearance)
        {
            foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++)
            {
                Vector2 edge=polygon[(i+1)%polygon.Length]-polygon[i];if(edge.sqrMagnitude<.000001f)continue;
                Vector2 axis=new Vector2(-edge.y,edge.x).normalized;
                float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;
                foreach(var p in a){float v=Vector2.Dot(p,axis);amin=Mathf.Min(amin,v);amax=Mathf.Max(amax,v);}
                foreach(var p in b){float v=Vector2.Dot(p,axis);bmin=Mathf.Min(bmin,v);bmax=Mathf.Max(bmax,v);}
                if(amax+clearance<=bmin||bmax+clearance<=amin)return false;
            }
            return true;
        }
        static bool GroundLot(Bounds b,out float height)
        {
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach(var c in Corners(b)){float h=CitySiteSurvey.Ground(c);if(float.IsNaN(h)){height=0;return false;}min=Mathf.Min(min,h);max=Mathf.Max(max,h);}
            height=max;return max-min<.3f;
        }
        static bool ContainsTerrainTree(Bounds b)
        {
            foreach(var t in Terrain.activeTerrains)
            {
                if(t.terrainData==null)continue;
                foreach(var tree in t.terrainData.treeInstances)
                {
                    var p=t.transform.position+Vector3.Scale(tree.position,t.terrainData.size);
                    if(p.x>b.min.x-2&&p.x<b.max.x+2&&p.z>b.min.z-2&&p.z<b.max.z+2)return true;
                }
            }
            return false;
        }
        static void Normalize(GameObject g,float width)
        {
            var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new InvalidOperationException("Modell ohne Mesh");
            // Temporarily normalize under identity so no world-axis distortion occurs.
            var parent=g.transform.parent;g.transform.SetParent(null,false);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            g.transform.localScale*=width/Mathf.Max(.01f,b.size.x);
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            var offset=new Vector3(-b.center.x,-b.min.y,-b.center.z);
            g.transform.SetParent(parent,false);g.transform.localPosition=offset;
        }
        static void Box(Transform parent,string name,Vector3 pos,Vector3 size,string material,bool collision)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=GetMaterial(material);
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static void Prop(Transform parent,string path,Vector3 pos,float width)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)return;
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(parent,false);Normalize(g,width);g.transform.localPosition+=pos;
        }
        static Material GetMaterial(string name)
        {
            name=name.Replace(" (Instance)","");return Palette.TryGetValue(name,out var m)?m:Palette["Plaster"];
        }
        static void EnsurePalette()
        {
            if(!AssetDatabase.IsValidFolder(Folder+"/Materials"))AssetDatabase.CreateFolder(Folder,"Materials");
            string[] keys={"Plaster","Brick","Sage","Trim","Roof","Metal","Glass","Wood","Awning","Base","PoliceBlue","White","Sand","Slate","Asphalt"};
            Color[] colors={new Color(.72f,.69f,.61f),new Color(.37f,.17f,.105f),new Color(.43f,.49f,.43f),new Color(.83f,.80f,.70f),new Color(.115f,.13f,.145f),new Color(.055f,.065f,.073f),new Color(.09f,.16f,.20f),new Color(.29f,.19f,.11f),new Color(.2f,.32f,.29f),new Color(.28f,.29f,.28f),new Color(.055f,.19f,.35f),new Color(.9f,.91f,.87f),new Color(.76f,.68f,.56f),new Color(.32f,.39f,.43f),new Color(.19f,.205f,.22f)};
            for(int i=0;i<keys.Length;i++)
            {
                string path=Folder+"/Materials/"+keys[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));m.color=colors[i];m.enableInstancing=true;
                    if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",keys[i]=="Glass"?.55f:.18f);
                    if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",keys[i]=="Glass"?.25f:0);
                    if(keys[i]=="Plaster"||keys[i]=="Sage"||keys[i]=="Base"||keys[i]=="Trim")
                    {
                        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Buildings/ModularModernHousePack/Textures/Concrete/vlklbgd_4K_Albedo.jpg");
                        if(texture!=null)m.mainTexture=texture;
                    }
                    AssetDatabase.CreateAsset(m,path);}
                Palette[keys[i]]=m;
            }
        }
    }
}
