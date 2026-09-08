using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CheatOnYourDayOnes.World;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        const string Villas=Folder+"/VILLEN - 10 EINZELNE GRUNDSTUECKE";
        const string VillaRequest=Folder+"/Villas.request";
        [InitializeOnLoadMethod] static void WatchVillas(){EditorApplication.delayCall+=PendingVillas;}
        static void PendingVillas()
        {
            if(!File.Exists(VillaRequest))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=PendingVillas;return;}
            File.Move(VillaRequest,VillaRequest+"."+DateTime.UtcNow.Ticks+".consumed");
            try{BuildVillas();}catch(Exception e){File.WriteAllText("Library/VillasResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Villen-Kollektion oeffnen")]
        static void OpenVillas(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(Villas);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Villen-Kollektion erstellen")]
        public static void BuildVillas()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            if(!AssetDatabase.IsValidFolder(Villas))AssetDatabase.CreateFolder(Folder,"VILLEN - 10 EINZELNE GRUNDSTUECKE");
            foreach(var dir in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(Villas+"/"+dir))AssetDatabase.CreateFolder(Villas,dir);
            var previous=AssetOutput;AssetOutput=Villas;Mats.Clear();
            string[] names={"01_Palais_Rondell","02_Turmvilla_Rosenhain","03_Klinker_Landgut","04_Mediterraner_Patio","05_Art_Deco_Residenz","06_Modernes_Atrium","07_Poolvilla_Auskragung","08_Gartenpavillon","09_Terrassenresidenz","10_Skulpturvilla_Rondell"};
            try
            {
                File.WriteAllText("Library/VillasResult.txt","Building ten villa prefabs. Existing scene untouched.");
                for(int i=0;i<names.Length;i++)
                {
                    var root=new GameObject(names[i]);
                    try
                    {
                        MakeVilla(root,i);
                        foreach(var c in root.GetComponentsInChildren<BoxCollider>())if(c.size.x<=0||c.size.y<=0||c.size.z<=0)throw new InvalidOperationException("Invalid collider: "+c.name);
                        BatchStaticVisuals(root,names[i]);
                        if(PrefabUtility.SaveAsPrefabAsset(root,Villas+"/"+names[i]+".prefab")==null)throw new InvalidOperationException("Save failed: "+names[i]);
                        RenderPreview(root,"VILLA_"+names[i]);
                        File.WriteAllText("Library/VillasResult.txt","Built "+(i+1)+"/10: "+names[i]);
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                AssetDatabase.SaveAssets();OpenVillas();
                File.WriteAllText("Library/VillasResult.txt","SUCCESS: 10 exterior villa prefabs, with colliders and individual grounds. No scene objects placed or changed. Previews in Library/CivicPreviewsV6/VILLA_*.png. Play Mode test pending.");
                Debug.Log("[VILLEN] 10 Villen fertig. Day Ones > Stadt > Villen-Kollektion oeffnen.");
            }
            finally{AssetOutput=previous;Mats.Clear();}
        }
        static Material VMat(string key,Color c){return Mat("Villa_"+key,c);}
        static void MakeVilla(GameObject root,int n)
        {
            var t=root.transform;
            var stone=VMat("Kalkstein",new Color(.78f,.73f,.62f));var white=VMat("Elfenbein",new Color(.91f,.89f,.82f));
            var dark=VMat("Graphit",new Color(.085f,.105f,.12f));var leaf=VMat("Laub",new Color(.16f,.28f,.13f));
            var roof=VMat(n==3?"Terrakotta":"Schiefer",n==3?new Color(.48f,.22f,.12f):new Color(.16f,.18f,.20f));
            var wall=n==2?VMat("Klinker",new Color(.39f,.19f,.13f)):n==3?VMat("Ockerputz",new Color(.82f,.68f,.46f)):n==7?VMat("Salbei",new Color(.48f,.52f,.43f)):white;
            bool circle=n==0||n==4||n==9;
            VillaGround(t,circle,n>=5,stone,dark,leaf);
            var house=Group(t,"VILLA - Architektur",new Vector3(0,0,5));
            switch(n)
            {
                case 0:
                    VillaVolume(house,"Corps de Logis",Vector3.zero,22,13,2,wall,stone,roof,true);
                    VillaVolume(house,"Westfluegel",new Vector3(-14,0,2),6,11,1,wall,stone,roof,true);
                    VillaVolume(house,"Ostfluegel",new Vector3(14,0,2),6,11,1,wall,stone,roof,true);
                    for(int x=-4;x<=4;x+=2)Pillar(house,new Vector3(x,0,-8),6.6f,.25f,stone);
                    Box(house,"Portikus Gebaelk",new Vector3(0,6.7f,-7.5f),new Vector3(10,.5f,3.3f),stone);
                    Pitched(Group(house,"Tempelgiebel",new Vector3(0,0,-7.5f)),"Frontondach",10.4f,3.5f,7,2,stone);
                    VillaBalustrade(house,new Vector3(0,3.6f,-7),10,stone);break;
                case 1:
                    VillaVolume(house,"Hauptfluegel",new Vector3(2,0,0),17,13,2,wall,stone,roof,true);
                    VillaVolume(house,"Querfluegel",new Vector3(7,0,6),9,13,2,wall,stone,roof,true);
                    var tower=Group(house,"Achteckiger Wohnturm",new Vector3(-8,0,-5));
                    Profile(tower,"Turmfassade",Vector3.zero,new[]{0f,10f},new[]{3.3f,3.3f},8,wall);
                    var tc=Box(tower,"Turmkollision",new Vector3(0,5,0),new Vector3(4.5f,10,4.5f),wall);Object.DestroyImmediate(tc.GetComponent<Renderer>());Object.DestroyImmediate(tc.GetComponent<MeshFilter>());
                    for(int a=0;a<8;a++){var side=Group(tower,"Turmfenster",Vector3.zero,a*45);for(int f=0;f<3;f++)VillaWindow(side,new Vector3(0,1.8f+f*3,-3.1f),1.15f,1.7f,stone,true);}
                    Profile(tower,"Turmhaube",Vector3.zero,new[]{10f,10.3f,14f,14.4f},new[]{3.7f,3.7f,.12f,0f},8,roof);
                    VillaBalustrade(house,new Vector3(3,3.6f,-7.4f),8,stone);break;
                case 2:
                    VillaVolume(house,"Landhaus",Vector3.zero,21,12,2,wall,stone,roof,true);
                    foreach(int s in new[]{-1,1}){var wing=Group(house,"Quergiebel",new Vector3(s*8,0,-4),90);VillaVolume(wing,"Risalit",Vector3.zero,10,6,2,wall,stone,roof,true);}
                    for(int x=-7;x<=7;x+=7){Box(house,"Kaminschaft",new Vector3(x,8,2),new Vector3(1.1f,4,1.2f),wall);Box(house,"Kaminkrone",new Vector3(x,10.1f,2),new Vector3(1.4f,.25f,1.5f),stone);}
                    VillaPergola(house,new Vector3(0,0,9),12,5,stone);break;
                case 3:
                    VillaVolume(house,"Patio Ruecken",new Vector3(0,0,7),23,8,2,wall,stone,roof,true);
                    foreach(int s in new[]{-1,1})VillaVolume(house,"Patio Seitenfluegel",new Vector3(s*8.5f,0,-1),6,11,1,wall,stone,roof,true);
                    for(int x=-5;x<=5;x+=5){Pillar(house,new Vector3(x,0,2.5f),3.2f,.2f,stone);Arch(house,new Vector3(x,2.15f,2.5f),1.2f,.18f,.3f,stone,16);}
                    break;
                case 4:
                    VillaVolume(house,"Symmetrischer Sockel",Vector3.zero,24,14,1,wall,stone,roof,false);
                    VillaVolume(house,"Staffelgeschoss",new Vector3(0,3.6f,1),18,11,1,wall,stone,roof,false);
                    VillaVolume(house,"Treppenturm",new Vector3(0,0,-4),5,6,3,stone,white,roof,false);
                    foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)Box(house,"Art Deco Lisenen",new Vector3(s*(3.5f+k*.4f),4,-7.15f),new Vector3(.18f,7.5f,.25f),stone,false);
                    VillaBalustrade(house,new Vector3(0,3.6f,-7),22,stone);break;
                case 5:
                    VillaVolume(house,"Atrium Ruecken",new Vector3(0,0,8),26,8,2,wall,dark,roof,false);
                    foreach(int s in new[]{-1,1})VillaVolume(house,"Atrium Fluegel",new Vector3(s*10,0,-2),6,12,1,wall,dark,roof,false);
                    VillaPergola(house,new Vector3(-10,3.6f,-2),6,11,dark);break;
                case 6:
                    VillaVolume(house,"Steinsockel",new Vector3(-4,0,2),15,14,1,stone,dark,roof,false);
                    VillaVolume(house,"Schwebender Oberbau",new Vector3(3,3.6f,0),24,11,1,wall,dark,roof,false);
                    foreach(float z in new[]{-4f,4f})Pillar(house,new Vector3(13,0,z),3.6f,.16f,dark);
                    VillaBalustrade(house,new Vector3(0,3.6f,-7),17,dark);break;
                case 7:
                    VillaVolume(house,"Flacher Gartenpavillon",new Vector3(-4,0,2),24,12,1,wall,dark,roof,false);
                    VillaVolume(house,"Schlaftrakt",new Vector3(9,0,7),8,18,1,stone,dark,roof,false);
                    VillaPergola(house,new Vector3(-4,0,-7),23,5,dark);break;
                case 8:
                    for(int f=0;f<3;f++)VillaVolume(house,"Zurueckgesetzte Ebene "+f,new Vector3(f*2,f*3.6f,f*2),24-f*5,16-f*3,1,f==1?stone:wall,dark,roof,false);
                    for(int f=1;f<3;f++)VillaBalustrade(house,new Vector3(f*2,f*3.6f,-8+(f-1)*3.5f),22-f*4,dark);break;
                case 9:
                    VillaVolume(house,"Naturstein Galerie",new Vector3(-7,0,2),9,17,2,stone,dark,roof,false);
                    var angled=Group(house,"Gedrehter Wohnfluegel",new Vector3(5,0,4),-18);VillaVolume(angled,"Weisser Wohnkubus",Vector3.zero,16,11,2,wall,dark,roof,false);
                    VillaVolume(house,"Verbindung",new Vector3(0,0,-1),8,7,1,dark,stone,roof,false);
                    VillaPergola(house,new Vector3(7,0,13),14,5,dark);break;
            }
            root.AddComponent<CityBuilding>().Configure("villa_"+n,root.name,CityDistrict.Residential,CityBuildingType.Residential,false,false);
            Group(t,"Eingangsseite - Richtung minus Z",new Vector3(0,0,-26));
        }
        static void VillaVolume(Transform parent,string name,Vector3 p,float w,float d,int floors,Material wall,Material trim,Material roof,bool historic,bool residentialWindows=false)
        {
            var t=Group(parent,name,p);float h=floors*3.6f;
            Box(t,"Massiver Baukoerper - nicht begehbar",new Vector3(0,h*.5f,0),new Vector3(w,h,d),wall);
            Cornice(t,w+.2f,d+.2f,.2f,trim,2);Cornice(t,w+.25f,d+.25f,h,trim,historic?3:1);
            for(int f=1;f<floors;f++)Cornice(t,w+.12f,d+.12f,f*3.6f,trim,historic?2:1);
            for(int side=0;side<4;side++)
            {
                float width=side%2==0?w:d,depth=side%2==0?d:w;
                var face=Group(t,"Fassadenseite "+side,Vector3.zero,side*90);
                int bays=Mathf.Max(2,Mathf.FloorToInt(width/(historic||residentialWindows?3.1f:4.8f)));
                for(int f=0;f<floors;f++)for(int b=0;b<bays;b++)
                {
                    float x=(b+.5f)*width/bays-width*.5f;
                    if(side==0&&f==0&&p.y==0&&Mathf.Abs(x)<1.6f)continue;
                    VillaWindow(face,new Vector3(x,f*3.6f+1.95f,-depth*.5f-.025f),historic?1.35f:residentialWindows?1.65f:width/bays-.75f,historic||residentialWindows?1.95f:2.5f,trim,historic);
                }
                if(historic)for(int s=-1;s<=1;s+=2)for(float y=.45f;y<h-.4f;y+=.45f)Box(face,"Eckquader",new Vector3(s*(width*.5f-.22f),y,-depth*.5f-.045f),new Vector3(.44f,.32f,.12f),trim,false);
                if(!historic)for(float y=.4f;y<h;y+=.65f)Box(face,"Fassaden Schattenfuge",new Vector3(0,y,-depth*.5f-.015f),new Vector3(width,.018f,.018f),roof,false);
            }
            if(p.y==0)
            {
                var door=Group(t,"Haupteingang - dekorativ geschlossen",new Vector3(0,0,-d*.5f-.06f));
                Box(door,"Doppeltuer",new Vector3(0,1.3f,0),new Vector3(1.9f,2.6f,.1f),VMat("Tueren",new Color(.16f,.12f,.08f)),false);
                foreach(int s in new[]{-1,1})
                {
                    Box(door,"Tuerlaibung",new Vector3(s*1.05f,1.35f,-.04f),new Vector3(.18f,2.7f,.2f),trim,false);
                    Box(door,"Tuerfuellung",new Vector3(s*.48f,1.35f,-.07f),new Vector3(.72f,1.9f,.08f),roof,false);
                    Box(door,"Tuergriff",new Vector3(s*.12f,1.15f,-.15f),new Vector3(.035f,.35f,.06f),trim,false);
                }
                Box(door,"Tuersturz",new Vector3(0,2.75f,-.04f),new Vector3(2.3f,.2f,.24f),trim,false);
            }
            if(historic){Pitched(t,"Villendach",w+.9f,d+.9f,h+.18f,Mathf.Min(3.6f,w*.23f),roof);RoofCourses(t);}
            else{Box(t,"Attika Dachplatte",new Vector3(0,h+.13f,0),new Vector3(w+.5f,.26f,d+.5f),trim);Box(t,"Dach Kiesbett",new Vector3(0,h+.28f,0),new Vector3(w-.5f,.05f,d-.5f),roof,false);}
        }
        static void VillaWindow(Transform t,Vector3 p,float w,float h,Material trim,bool classic)
        {
            var g=Group(t,"Fenster mit Laibung",p);var glass=VMat("Rauchglas",new Color(.10f,.19f,.23f));
            Box(g,"Verglasung",Vector3.zero,new Vector3(w,h,.035f),glass,false);
            foreach(int s in new[]{-1,1}){Box(g,"Seitlicher Rahmen",new Vector3(s*(w*.5f+.05f),0,-.05f),new Vector3(.1f,h+.2f,.14f),trim,false);Box(g,"Horizontaler Rahmen",new Vector3(0,s*(h*.5f+.05f),-.05f),new Vector3(w+.2f,.1f,.14f),trim,false);}
            Box(g,"Mittelpfosten",new Vector3(0,0,-.045f),new Vector3(.055f,h,.08f),trim,false);
            if(classic){Box(g,"Sprosse",new Vector3(0,.15f,-.045f),new Vector3(w,.05f,.08f),trim,false);Box(g,"Profilierte Fensterbank",new Vector3(0,-h*.5f-.12f,-.12f),new Vector3(w+.45f,.12f,.35f),trim,false);Box(g,"Verdachung",new Vector3(0,h*.5f+.17f,-.08f),new Vector3(w+.4f,.15f,.23f),trim,false);}
        }
        static void VillaGround(Transform t,bool circle,bool modern,Material stone,Material dark,Material leaf)
        {
            var grounds=Group(t,"GRUNDSTUECK - Garten und Vorfahrt",Vector3.zero);
            Box(grounds,"Grundplatte 42 x 54 m",new Vector3(0,-.13f,0),new Vector3(42,.25f,54),VMat("Rasen",new Color(.25f,.34f,.18f)));
            Box(grounds,"Terrasse",new Vector3(0,.015f,4),new Vector3(36,.05f,33),stone);
            var paving=VMat("Pflaster",new Color(.39f,.40f,.38f));
            Box(grounds,"Zufahrt",new Vector3(0,.01f,-22),new Vector3(circle?18:7,.06f,10),paving);
            if(circle){VillaDisk(grounds,"Rondell Fahrbahn",new Vector3(0,.055f,-16),9.5f,paving);VillaDisk(grounds,"Rondell Insel - frei gestaltbar",new Vector3(0,.09f,-16),4.6f,stone);}
            else Box(grounds,"Vorplatz",new Vector3(0,.02f,-16),new Vector3(27,.06f,9),paving);
            foreach(int s in new[]{-1,1})
            {
                Box(grounds,"Grundstuecksmauer",new Vector3(s*20.7f,.55f,0),new Vector3(.35f,1.1f,53),stone);
                for(int z=-24;z<=24;z+=4){Box(grounds,"Zaunpfeiler",new Vector3(s*20.7f,.95f,z),new Vector3(.6f,1.9f,.6f),stone);Box(grounds,"Pfeilerabdeckung",new Vector3(s*20.7f,1.92f,z),new Vector3(.75f,.15f,.75f),dark,false);}
                for(int z=-25;z<=25;z++)Box(grounds,"Zaunstab",new Vector3(s*20.7f,1.35f,z),new Vector3(.05f,.6f,.05f),dark,false);
                Box(grounds,"Eingangsmauer",new Vector3(s*15,.55f,-26.7f),new Vector3(11,1.1f,.35f),stone);
                Pillar(grounds,new Vector3(s*9.6f,0,-26.7f),2.1f,.32f,stone);
            }
            Box(grounds,"Rueckmauer",new Vector3(0,.55f,26.7f),new Vector3(42,1.1f,.35f),stone);
        }
        static void VillaDisk(Transform t,string name,Vector3 p,float radius,Material mat)
        {Profile(t,name,p,new[]{0f,.035f},new[]{radius,radius},64,mat);}
        static void VillaFountain(Transform t,Vector3 p,float r,Material stone)
        {
            var g=Group(t,"Brunnen",p);Profile(g,"Brunnenbecken",Vector3.zero,new[]{0f,.18f,.45f,.5f},new[]{r,r,r,r-.12f},48,stone);
            VillaDisk(g,"Wasserspiegel",new Vector3(0,.51f,0),r-.18f,VMat("Wasser",new Color(.14f,.42f,.44f)));
            Profile(g,"Brunnenschale",Vector3.zero,new[]{.5f,1.3f,1.45f,1.65f},new[]{.22f,.18f,.7f,.8f},32,stone);
            SphereDetail(g,"Brunnenabschluss",new Vector3(0,1.9f,0),Vector3.one*.3f,stone);
        }
        static void VillaPool(Transform t,Vector3 p,float w,float d,Material stone)
        {
            var g=Group(t,"Pool mit umlaufendem Rand",p);Box(g,"Becken",new Vector3(0,.12f,0),new Vector3(w,.24f,d),stone);
            Box(g,"Wasser",new Vector3(0,.255f,0),new Vector3(w-.6f,.025f,d-.6f),VMat("Wasser",new Color(.14f,.42f,.44f)),false);
            for(int s=-1;s<=1;s+=2){Box(g,"Randstein",new Vector3(s*(w*.5f-.15f),.32f,0),new Vector3(.3f,.16f,d),stone);Box(g,"Randstein",new Vector3(0,.32f,s*(d*.5f-.15f)),new Vector3(w,.16f,.3f),stone);}
        }
        static void VillaPergola(Transform t,Vector3 p,float w,float d,Material mat)
        {var g=Group(t,"Pergola",p);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box(g,"Pfosten",new Vector3(x*(w*.5f-.15f),1.65f,z*(d*.5f-.15f)),new Vector3(.2f,3.3f,.2f),mat);for(float x=-w*.5f;x<=w*.5f;x+=.6f)Box(g,"Dachlamelle",new Vector3(x,3.4f,0),new Vector3(.12f,.22f,d+.4f),mat,false);}
        static void VillaBalustrade(Transform t,Vector3 p,float w,Material mat)
        {var g=Group(t,"Balkon mit Gelaender",p);Box(g,"Balkonplatte",Vector3.zero,new Vector3(w,.22f,1.5f),mat);Box(g,"Handlauf",new Vector3(0,1.05f,-.65f),new Vector3(w,.1f,.14f),mat);for(float x=-w*.5f;x<=w*.5f;x+=.3f)Box(g,"Gelaenderstab",new Vector3(x,.5f,-.65f),new Vector3(.065f,1,.065f),mat,false);foreach(int s in new[]{-1,1})Box(g,"Seitengelaender",new Vector3(s*w*.5f,.5f,0),new Vector3(.08f,1,1.4f),mat);}
    }
}
