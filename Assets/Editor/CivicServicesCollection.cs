using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CheatOnYourDayOnes.World;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        const string ServicesFolder=Folder+"/MODULAR - STADTDIENSTE UND INDUSTRIE";
        const string ServicesRequest=Folder+"/ServicesCollection.request";
        [InitializeOnLoadMethod] static void WatchServices(){EditorApplication.delayCall+=PendingServices;}
        static void PendingServices()
        {
            if(!File.Exists(ServicesRequest))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=PendingServices;return;}
            File.Move(ServicesRequest,ServicesRequest+"."+DateTime.UtcNow.Ticks+".consumed");
            try{BuildServices();}catch(Exception e){File.WriteAllText("Library/ServicesCollectionResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Stadtdienste und Industrie oeffnen")]
        static void OpenServices(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(ServicesFolder);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Stadtdienste und Industrie erstellen")]
        public static void BuildServices()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            if(!AssetDatabase.IsValidFolder(ServicesFolder))AssetDatabase.CreateFolder(Folder,"MODULAR - STADTDIENSTE UND INDUSTRIE");
            foreach(var d in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(ServicesFolder+"/"+d))AssetDatabase.CreateFolder(ServicesFolder,d);
            string[] names={"01_Post_Paketshop","02_Pfandhaus","03_Duplex_Reihenhaus","04_Lagerhalle","05_Self_Storage","06_Schrottplatz","07_Abschlepphof","08_Stadtwerke_Umspannwerk","09_Wasserwerk","10_Tanklager","11_Kirche","12_Fitnessstudio","13_Elektro_Handyladen","14_Feuerwehr"};
            string old=AssetOutput;AssetOutput=ServicesFolder;Mats.Clear();
            try
            {
                File.WriteAllText("Library/ServicesCollectionResult.txt","BUILDING: individual service architecture; checks pending.");
                for(int n=0;n<names.Length;n++)
                {
                    var root=new GameObject(names[n]);
                    try
                    {
                        var routes=new List<Vector3>();MakeService(root,n,routes);CraftService(root.transform,n,routes);root.transform.localScale=new Vector3(1,.8f,1);
                        CheckServiceRoutes(root,routes);
                        BatchStaticVisuals(root,names[n]);
                        if(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.StartsWith("Fassade -")))throw new InvalidOperationException("Nonmodular renderer found");
                        if(PrefabUtility.SaveAsPrefabAsset(root,ServicesFolder+"/"+names[n]+".prefab")==null)throw new InvalidOperationException(names[n]);
                        RenderPreview(root,"SERVICE_"+names[n]);
                        File.WriteAllText("Library/ServicesCollectionResult.txt","Built "+(n+1)+"/14: "+names[n]+". Modular structure and route checks passed.");
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                AssetDatabase.SaveAssets();OpenServices();File.WriteAllText("Library/ServicesCollectionResult.txt","SUCCESS: 14 modular enterable service properties. Y=0.8. Pedestrian clearance and floor checks passed. No scene edits. Equipment is decorative; no gameplay, live water or electrical simulation. Play Mode test pending.");
            }
            finally{AssetOutput=old;Mats.Clear();}
        }
        static void ServiceRoute(List<Vector3> points,Vector3 a,Vector3 b)
        {int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.3f);for(int i=0;i<=steps;i++)points.Add(Vector3.Lerp(a,b,steps==0?0:(float)i/steps));}
        static void MakeService(GameObject root,int n,List<Vector3> routes)
        {
            var t=root.transform;var white=VMat("Service_Putz",new Color(.82f,.81f,.75f));var dark=VMat("Service_Stahl",new Color(.095f,.12f,.14f));var brick=VMat("Service_Klinker",new Color(.41f,.22f,.15f));
            var trim=VMat("Service_Stein",new Color(.70f,.66f,.57f));var blue=VMat("Service_Blau",new Color(.10f,.24f,.37f));var yellow=VMat("Service_Gelb",new Color(.87f,.62f,.08f));var red=VMat("Service_Rot",new Color(.52f,.10f,.075f));var glass=Mat("Service_Glas",new Color(.54f,.73f,.8f,.17f),true);var wood=VMat("Service_Holz",new Color(.32f,.22f,.14f));
            string[] signs={"POST","PFANDHAUS","","LAGERHALLE","SELF STORAGE","SCHROTTPLATZ","ABSCHLEPPHOF","STADTWERKE","WASSERWERK","TANKLAGER","","FITNESS","ELEKTRO","FEUERWEHR"};
            bool site=n>=4&&n<=9;float w=n==3?26:n==10?16:n==11?24:n==13?8:16,d=n==3?30:n==10?28:n==11?20:n==13?22:12,h=n==3?8:n==10?9:n==13?7:4.5f;
            var accent=n==0?yellow:n==1?red:n==13?red:blue;
            if(n==2)
            {
                foreach(int s in new[]{-1,1})
                {
                    var home=Group(t,"HAUS "+(s<0?"A":"B")+" - komplett modular",new Vector3(s*8.3f,0,0));MakeWalkableHome(home.gameObject,s<0?5:6,16,2,false);
                    // Use the existing detailed residential validation at the final world scale.
                    home.localScale=new Vector3(1,.8f,1);ValidateHomeRoutes(home.gameObject,16,2,false);home.localScale=Vector3.one;
                }
            }
            else
            {
                ServiceShell(t,"HAUPTGEBAEUDE",Vector3.zero,w,d,h,n==3||n==13?6:2.6f,n==10?brick:white,trim,glass,dark,n==3||n==10,signs[n],accent);
                ServiceRoute(routes,new Vector3(0,0,-d*.5f),new Vector3(0,0,d*.5f));
            }
            if(site)
            {
                Box(t,"Hofboden",new Vector3(0,-.11f,17),new Vector3(44,.2f,58),VMat("Service_Asphalt",new Color(.25f,.26f,.25f)));
                foreach(int s in new[]{-1,1})Fence(t,new Vector3(s*22,0,17),58,90,trim,dark);
                Fence(t,new Vector3(0,0,46),44,0,trim,dark);
                foreach(int s in new[]{-1,1})Fence(t,new Vector3(s*13,0,-12),18,0,trim,dark);
                ServiceRoute(routes,new Vector3(0,0,6),new Vector3(0,0,43));
                ServiceCounter(t,new Vector3(-4,0,2.5f),3,blue,dark);
                NoticeCase(t,new Vector3(5,2.2f,5.8f),2,dark,trim);
            }
            switch(n)
            {
                case 0:
                    ServiceCounter(t,new Vector3(-4,0,2),4,yellow,dark);ServiceRack(t,new Vector3(5.5f,0,1),3,wood,trim,true);
                    var lockers=Group(t,"PAKETSTATION",new Vector3(-5,0,-6.5f));for(int x=0;x<4;x++)for(int y=0;y<4;y++){var box=Box(lockers,"Paketfach",new Vector3(x*.7f,y*.6f+.3f,0),new Vector3(.66f,.56f,.5f),yellow);Box(box.transform,"Griff",new Vector3(.25f,0,-.55f),new Vector3(.07f,.12f,.08f),dark,false);}break;
                case 1:
                    ServiceCounter(t,new Vector3(-4,0,1.5f),4,wood,dark);ServiceRack(t,new Vector3(5.5f,0,1),3,wood,trim,false);
                    for(int s=-1;s<=1;s+=2)NoticeCase(t,new Vector3(s*4,2.5f,-6.25f),2,trim,dark);break;
                case 3:
                    foreach(int s in new[]{-1,1})for(int z=-8;z<=8;z+=8)ServiceRack(t,new Vector3(s*9,0,z),6,dark,wood,true);
                    for(int z=-12;z<=12;z+=6){Beam(t,"Dachbinder",new Vector3(-12.5f,7.5f,z),new Vector3(12.5f,7.5f,z),.18f,dark);}
                    break;
                case 4:
                    for(int s=-1;s<=1;s+=2)for(int k=0;k<4;k++)
                    {
                        // Units face the centre aisle, and each has its own open entrance.
                        var unit=Group(t,"LAGERBOX "+s+"-"+k,new Vector3(s*13,0,11+k*8),s<0?-90:90);
                        ServiceShell(unit,"Box",Vector3.zero,7,8,4,3.4f,white,trim,glass,dark,false,"",blue);
                        ServiceRoute(routes,new Vector3(0,0,11+k*8),new Vector3(s*13,0,11+k*8));
                    }break;
                case 5:
                    foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)
                    {
                        var bin=Group(t,"SCHROTTCONTAINER",new Vector3(s*13,0,12+k*10));Box(bin,"Boden",new Vector3(0,.15f,0),new Vector3(6,.3f,7),dark);
                        foreach(int side in new[]{-1,1})Box(bin,"Seitenwand",new Vector3(side*3,1,0),new Vector3(.15f,2,7),blue);
                        for(int j=0;j<5;j++){var scrap=Box(bin,"Metallrest",new Vector3(-2+j,.65f+j*.1f,0),new Vector3(.35f,1.2f,5),trim);scrap.transform.localRotation=Quaternion.Euler(j*7,0,j*15);}
                    }break;
                case 6:
                    for(int s=-1;s<=1;s+=2)for(int k=0;k<4;k++)
                    {
                        var slot=Group(t,"SICHERSTELLPLATZ",new Vector3(s*13,0,10+k*8));foreach(int side in new[]{-1,1})Box(slot,"Stellplatzlinie",new Vector3(side*2,.01f,0),new Vector3(.09f,.015f,6),yellow,false);Group(slot,"FAHRZEUG ANKER",Vector3.zero);
                    }
                    ServiceCounter(t,new Vector3(-4,0,2),4,blue,dark);break;
                case 7:
                    for(int s=-1;s<=1;s+=2)for(int k=0;k<3;k++)
                    {
                        var transformer=Group(t,"TRANSFORMATOR - Kulisse",new Vector3(s*12,0,12+k*10));Box(transformer,"Fundament",new Vector3(0,.2f,0),new Vector3(7,.4f,6),trim);Box(transformer,"Gehaeuse",new Vector3(0,1.8f,0),new Vector3(4,3,3),blue);
                        for(int j=-3;j<=3;j++)Box(transformer,"Kuehlrippe",new Vector3(j*.48f,1.8f,-1.75f),new Vector3(.1f,2.4f,.7f),dark,false);
                        foreach(int side in new[]{-1,1})Profile(transformer,"Isolator",new Vector3(side,3.3f,0),new[]{0f,.2f,.3f,.5f,.6f,.9f},new[]{.28f,.28f,.15f,.28f,.15f,.18f},12,trim);
                    }break;
                case 8:
                    foreach(int s in new[]{-1,1})for(int k=0;k<2;k++)
                    {
                        var tank=Group(t,"TROCKENES KLAERBECKEN",new Vector3(s*12,0,15+k*18));Box(tank,"Beckenboden",new Vector3(0,-.02f,0),new Vector3(13,.16f,13),trim);
                        foreach(int side in new[]{-1,1}){Box(tank,"Beckenwand",new Vector3(side*6.5f,1.1f,0),new Vector3(.3f,2.2f,13),trim);Box(tank,"Beckenwand",new Vector3(0,1.1f,side*6.5f),new Vector3(13,2.2f,.3f),trim);}
                        Box(tank,"Raeumerbruecke",new Vector3(0,2.4f,0),new Vector3(13.2f,.18f,1),dark);Box(tank,"Antrieb",new Vector3(0,2.9f,0),new Vector3(1,.8f,1),blue);
                    }break;
                case 9:
                    foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)
                    {
                        var tank=Group(t,"LAGERTANK - geschlossen",new Vector3(s*12,0,12+k*11));Profile(tank,"Tankhuelle",Vector3.zero,new[]{0f,.3f,7f,7.5f},new[]{3.3f,3.5f,3.5f,3.1f},32,white);
                        var col=tank.gameObject.AddComponent<CapsuleCollider>();col.radius=3.5f;col.height=7.5f;col.center=Vector3.up*3.75f;
                        foreach(float y in new[]{.4f,6.8f})Profile(tank,"Spannring",new Vector3(0,y,0),new[]{0f,.16f},new[]{3.56f,3.56f},32,dark);
                        for(int j=0;j<14;j++)Box(tank,"Leitersprosse",new Vector3(0,.4f+j*.48f,-3.6f),new Vector3(.7f,.05f,.12f),dark,false);
                    }break;
                case 10:
                    for(int s=-1;s<=1;s+=2)for(float z=-7;z<=7;z+=2.8f)Bench(t,new Vector3(s*4,0,z),4,wood,dark);
                    Box(t,"ALTARTISCH",new Vector3(0,1,11.5f),new Vector3(4,2,1.4f),trim);
                    // Keep the central nave route clear up to the altar, not through it.
                    routes.Clear();ServiceRoute(routes,new Vector3(0,0,-14),new Vector3(0,0,9.5f));
                    var tower=Group(t,"GLOCKENTURM - nicht ausgebaut",new Vector3(-10,0,-9));Box(tower,"Turmschaft",new Vector3(0,8,0),new Vector3(4,16,5),brick);Profile(tower,"Turmspitze",new Vector3(0,16,0),new[]{0f,5f},new[]{3f,0f},4,dark,Quaternion.Euler(0,45,0));
                    Box(t,"Kreuz vertikal",new Vector3(0,5,13.7f),new Vector3(.22f,3,.16f),wood,false);Box(t,"Kreuz horizontal",new Vector3(0,5.5f,13.7f),new Vector3(1.6f,.2f,.16f),wood,false);
                    for(int s=-1;s<=1;s+=2)for(int z=-10;z<=10;z+=5)Box(t,"Strebepfeiler",new Vector3(s*8.4f,3,z),new Vector3(.8f,6,.8f),trim);
                    break;
                case 11:
                    for(int s=-1;s<=1;s+=2)for(int k=0;k<4;k++)
                    {
                        var machine=Group(t,"FITNESSGERAET",new Vector3(s*8,0,-6+k*4));Box(machine,"Laufband",new Vector3(0,.18f,0),new Vector3(1.4f,.36f,2.4f),dark);foreach(int side in new[]{-1,1})Box(machine,"Haltegriff",new Vector3(side*.65f,.85f,-.9f),new Vector3(.08f,1.6f,.08f),trim);Box(machine,"Display",new Vector3(0,1.7f,-.9f),new Vector3(1,.45f,.12f),blue,false);
                    }break;
                case 12:
                    ServiceCounter(t,new Vector3(-4,0,2),4,blue,dark);
                    for(int k=0;k<3;k++){var display=Group(t,"HANDYVITRINE",new Vector3(5,0,-3+k*3));Box(display,"Sockel",new Vector3(0,.6f,0),new Vector3(2,1.2f,1),white);Box(display,"Glashaube",new Vector3(0,1.5f,0),new Vector3(2,.6f,1),glass);for(int j=-1;j<=1;j++)Box(display,"Telefon",new Vector3(j*.5f,1.25f,-.1f),new Vector3(.23f,.035f,.4f),dark,false);}break;
                case 13:
                    Box(t,"Feuerwehr Vorfeld",new Vector3(0,-.1f,-15),new Vector3(34,.2f,8),trim);
                    var hose=Group(t,"SCHLAUCHTURM - technische Kulisse",new Vector3(18,0,7));Box(hose,"Turm",new Vector3(0,6,0),new Vector3(4,12,6),brick);Box(hose,"Turmabschluss",new Vector3(0,12.2f,0),new Vector3(4.6f,.4f,6.6f),dark);
                    for(int j=0;j<4;j++)Box(hose,"Lueftungslamelle",new Vector3(0,9+j*.5f,-3.1f),new Vector3(2.6f,.16f,.25f),trim,false);
                    foreach(int s in new[]{-1,1})
                    {
                        ServiceShell(t,"FAHRZEUGBAY",new Vector3(s*11,0,0),8,22,7,5.5f,white,trim,glass,dark,false,"",red);
                        ServiceRoute(routes,new Vector3(s*11,0,-11),new Vector3(s*11,0,7));
                        for(int k=0;k<6;k++)Box(t,"Spind",new Vector3(s*11-2.5f+k,1.4f,9.6f),new Vector3(.9f,2.8f,.7f),red);
                    }
                    break;
            }
            IndividualServiceArchitecture(t,n,w,d,h,white,trim,dark,brick,blue,yellow,red,glass,wood);
            if(n!=2)root.AddComponent<CityBuilding>().Configure(root.name,root.name,site?CityDistrict.Industrial:CityDistrict.Commercial,n==13?CityBuildingType.FireStation:n==11?CityBuildingType.Gym:n==3?CityBuildingType.Warehouse:CityBuildingType.GenericShop,false,true);
        }
        static void ServiceShell(Transform t,string name,Vector3 pos,float w,float d,float h,float door,Material wall,Material trim,Material glass,Material dark,bool pitched,string sign,Material accent)
        {
            var g=Group(t,name,pos);Box(g,"Fussboden",new Vector3(0,-.1f,0),new Vector3(w,.2f,d),trim);
            foreach(int side in new[]{-1,1})
            {
                var f=Group(g,side<0?"EINGANG":"HINTERER AUSGANG",new Vector3(0,0,side*d*.5f),side<0?0:180);
                float openingHeight=door>4?5.5f:3;
                foreach(int s in new[]{-1,1})Box(f,"Torpfeiler",new Vector3(s*(w+door)*.25f,h*.5f,0),new Vector3((w-door)*.5f,h,.25f),wall);
                Box(f,"Sturz",new Vector3(0,(h+openingHeight)*.5f,0),new Vector3(door,h-openingHeight,.25f),wall);
                foreach(int s in new[]{-1,1})Box(f,"Portalprofil",new Vector3(s*(door*.5f+.1f),openingHeight*.5f,-.18f),new Vector3(.15f,openingHeight,.15f),accent,false);
                Box(f,"Portalabschluss",new Vector3(0,openingHeight+.08f,-.18f),new Vector3(door+.35f,.15f,.15f),accent,false);
            }
            foreach(int s in new[]{-1,1})Facade(Group(g,"Fensterseite",new Vector3(s*w*.5f,0,0),s*90),d,h,0,wall,trim,glass,dark,false,false,2,2,1.1f,h==9?7:3.3f);
            if(pitched)Pitched(g,"Satteldach",w+.7f,d+.7f,h,2.6f,dark);else Box(g,"Dachplatte",new Vector3(0,h+.15f,0),new Vector3(w+.6f,.3f,d+.6f),dark);
            Cornice(g,w,d,h-.2f,trim,2);FramedGlazing(g,dark);
            var spec=new Spec("90_Service","Service","","office",w,d,h,wall.color,CityBuildingType.GenericShop);Masonry(g,spec);RoofCourses(g);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Box(g,"Regenfallrohr",new Vector3(sx*(w*.5f-.12f),h*.5f,sz*(d*.5f+.18f)),new Vector3(.07f,h,.07f),dark,false);
            if(h==9){Arch(g,new Vector3(0,3,-d*.5f-.25f),door*.5f+.2f,.2f,.3f,trim,24);WindowDressings(g,trim,true);}
            for(int s=-1;s<=1;s+=2)Box(g,"Eckprofil",new Vector3(s*(w*.5f-.12f),h*.5f,-d*.5f-.13f),new Vector3(.15f,h,.15f),accent,false);
            if(!string.IsNullOrEmpty(sign))CityFacadeDetails.Word(g,sign,new Vector3(0,h-.5f,-d*.5f-.3f),Mathf.Min(.55f,w/(sign.Length*.85f)),accent);
            var light=Group(g,"Innenleuchte",new Vector3(0,h-.5f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=Mathf.Max(w,d);light.intensity=1.3f;light.shadows=LightShadows.None;
        }
        static void ServiceCounter(Transform t,Vector3 p,float w,Material mat,Material dark)
        {var g=Group(t,"THEKE - komplett verschiebbar",p);Box(g,"Korpus",Vector3.up*.65f,new Vector3(w,1.3f,1),mat);Box(g,"Arbeitsplatte",Vector3.up*1.33f,new Vector3(w+.15f,.08f,1.15f),dark);Box(g,"Kassendisplay",new Vector3(-w*.3f,1.6f,0),new Vector3(.5f,.45f,.12f),dark,false);}
        static void ServiceRack(Transform t,Vector3 p,float width,Material frame,Material shelf,bool packages)
        {var g=Group(t,"REGAL - komplett verschiebbar",p);foreach(int s in new[]{-1,1})Box(g,"Stuetzen",new Vector3(s*width*.5f,1.6f,0),new Vector3(.1f,3.2f,1.1f),frame);for(int y=0;y<4;y++){Box(g,"Fachboden",new Vector3(0,.2f+y*.8f,0),new Vector3(width,.08f,1.1f),shelf);if(packages)for(int j=-1;j<=1;j++)Box(g,"Paket",new Vector3(j*width*.28f,.48f+y*.8f,0),new Vector3(width*.22f,.45f,.8f),shelf);}}
        static void CheckServiceRoutes(GameObject root,List<Vector3> routes)
        {
            var boxes=root.GetComponentsInChildren<BoxCollider>();float sy=root.transform.lossyScale.y;
            foreach(var point in routes)
            {
                foreach(float dx in new[]{-.32f,0,.32f})foreach(float y in new[]{.15f,.5f,1f,1.5f,2f})
                {
                    var world=root.transform.TransformPoint(point+new Vector3(dx,y/sy,0));
                    foreach(var c in boxes){if(c.isTrigger)continue;var p=c.transform.InverseTransformPoint(world)-c.center;var h=c.size*.5f;if(Mathf.Abs(p.x)<h.x-.001f&&Mathf.Abs(p.y)<h.y-.001f&&Mathf.Abs(p.z)<h.z-.001f)throw new InvalidOperationException(root.name+": blocked route at "+point+" by "+c.name);}
                }
                bool support=false;var foot=root.transform.TransformPoint(point-Vector3.up*.05f);
                foreach(var c in boxes){var p=c.transform.InverseTransformPoint(foot)-c.center;var h=c.size*.5f;if(Mathf.Abs(p.x)<=h.x+.001f&&Mathf.Abs(p.y)<=h.y+.001f&&Mathf.Abs(p.z)<=h.z+.001f){support=true;break;}}
                if(!support)throw new InvalidOperationException(root.name+": no floor at "+point);
            }
        }
    }
}
