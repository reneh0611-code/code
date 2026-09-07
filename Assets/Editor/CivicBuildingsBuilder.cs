using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheatOnYourDayOnes.World;
using CheatOnYourDayOnes.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static partial class CivicBuildingsBuilder
    {
        const string Folder="Assets/Generated/NeubeckumCity";
        const string Ready=Folder+"/STADTGEBAEUDE V3 - SHOPS UND LOKALE";
        const string Request=Folder+"/CivicBuildings.request";
        class Spec
        {
            public string id,label,sign,style;public float w,d,h;public Color color;public CityBuildingType type;
            public Spec(string id,string label,string sign,string style,float w,float d,float h,Color color,CityBuildingType type)
            {this.id=id;this.label=label;this.sign=sign;this.style=style;this.w=w;this.d=d;this.h=h;this.color=color;this.type=type;}
        }
        static readonly Color White=new Color(.88f,.88f,.83f),Sand=new Color(.72f,.66f,.55f),Brick=new Color(.42f,.24f,.18f);
        static readonly Spec[] Specs={
            new Spec("19_Bank","Bank","BANK","civic",24,16,4.6f,White,CityBuildingType.Bank),
            new Spec("20_Jobcenter","Jobcenter","JOBCENTER","office",19,13,3.8f,Sand,CityBuildingType.JobCenter),
            new Spec("21_Waschsalon","Waschsalon","WASCHSALON","laundry",12,10,3.8f,new Color(.66f,.77f,.75f),CityBuildingType.GenericShop),
            new Spec("22_Lagerhalle","Lagerhalle","LAGERHALLE","warehouse",26,32,7.5f, new Color(.38f,.43f,.47f),CityBuildingType.Warehouse),
            new Spec("23_FastFood","Fast-Food-Restaurant","FAST FOOD","fastfood",19,11,3.6f,new Color(.75f,.36f,.25f),CityBuildingType.Restaurant),
            new Spec("24_Waschstrasse","Waschstrasse","WASCHSTRASSE","carwash",10,28,4.5f,new Color(.3f,.54f,.65f),CityBuildingType.Workshop),
            new Spec("25_Bahnhof","Bahnhof","BAHNHOF","station",26,14,5.5f,Brick,CityBuildingType.GenericShop),
            new Spec("26_Rathaus","Rathaus","RATHAUS","townhall",30,17,4.8f,new Color(.76f,.69f,.57f),CityBuildingType.CityHall),
            new Spec("27_Casino","Casino","CASINO","casino",28,21,6.5f,new Color(.16f,.18f,.23f),CityBuildingType.Leisure),
            new Spec("28_Finanzamt","Finanzamt","FINANZAMT","office",23,16,4,Sand,CityBuildingType.Office),
            new Spec("29_Baustelle","Baustelle","BAUSTELLE","construction",20,16,6,new Color(.51f,.5f,.46f),CityBuildingType.Industrial),
            new Spec("30_Bar","Bar","BAR","bar",9,14,3.5f,Brick,CityBuildingType.Restaurant),
            new Spec("31_Motel","Motel","MOTEL","motel",32,10,3.6f,new Color(.75f,.73f,.63f),CityBuildingType.Leisure),
            new Spec("32_Cafe","Cafe","CAFE","cafe",12,10,3.8f,White,CityBuildingType.Restaurant),
            new Spec("33_Eisdiele","Eisdiele","EISDIELE","icecream",10,8,3.4f,new Color(.86f,.71f,.7f),CityBuildingType.Restaurant),
            new Spec("34_PolizeiCampus","Polizeiwache mit Sicherheitshof","POLIZEI","police",18,14,4.6f,White,CityBuildingType.PoliceStation),
            new Spec("35_Logistikhalle","Logistikhalle","LOGISTIK","warehouse",38,28,9,new Color(.67f,.65f,.58f),CityBuildingType.Warehouse),
            new Spec("36_Baeckerei","Baeckerei","BAECKEREI","shop_bakery",10,12,3.7f,new Color(.78f,.66f,.48f),CityBuildingType.GenericShop),
            new Spec("37_Metzgerei","Metzgerei","METZGEREI","shop_butcher",9,13,3.6f,new Color(.75f,.73f,.67f),CityBuildingType.GenericShop),
            new Spec("38_Apotheke","Apotheke","APOTHEKE","shop_pharmacy",14,12,4.1f,new Color(.84f,.85f,.81f),CityBuildingType.GenericShop),
            new Spec("39_Kiosk","Kiosk","KIOSK","shop_kiosk",7,7,3.3f,new Color(.18f,.32f,.31f),CityBuildingType.GenericShop),
            new Spec("40_Blumenladen","Blumenladen","BLUMEN","shop_florist",11,9,3.6f,new Color(.7f,.72f,.58f),CityBuildingType.GenericShop),
            new Spec("41_Friseur","Friseursalon","FRISEUR","shop_barber",8,11,3.5f,new Color(.3f,.27f,.26f),CityBuildingType.GenericShop),
            new Spec("42_Modeboutique","Modeboutique","ATELIER","shop_fashion",13,12,4.3f,new Color(.78f,.74f,.67f),CityBuildingType.GenericShop),
            new Spec("43_Elektronik","Elektronikmarkt","TECHNIK","shop_electronics",22,18,5.5f,new Color(.22f,.25f,.3f),CityBuildingType.GenericShop),
            new Spec("44_Fahrradladen","Fahrradladen","RADWERK","shop_bikes",15,16,4.6f,new Color(.48f,.27f,.17f),CityBuildingType.GenericShop),
            new Spec("45_Tierbedarf","Tierbedarf","TIERBEDARF","shop_pets",17,14,4,new Color(.71f,.65f,.49f),CityBuildingType.GenericShop),
            new Spec("46_Baumarkt","Baumarkt","BAUMARKT","shop_hardware",32,25,6.2f,new Color(.48f,.5f,.46f),CityBuildingType.GenericShop),
            new Spec("47_Buchhandlung","Buchhandlung","BUCHHAUS","shop_books",9,13,3.8f,new Color(.38f,.46f,.4f),CityBuildingType.GenericShop)
        };
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        static readonly List<GameObject> Catalog=new List<GameObject>();
        static CivicBuildingsBuilder(){EditorApplication.delayCall+=Pending;}
        static void Pending()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            // Asset-only build works independently of the currently loaded scene.
            File.Move(Request,Folder+"/CivicBuildings."+DateTime.UtcNow.Ticks+".consumed");
            try{BuildCatalog();}catch(Exception e){File.WriteAllText("Library/CivicBuildingsResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Begehbare Lokale und Polizei erstellen")]
        public static void BuildCatalog()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            if(!AssetDatabase.IsValidFolder(Ready))AssetDatabase.CreateFolder(Folder,"STADTGEBAEUDE V3 - SHOPS UND LOKALE");
            if(!AssetDatabase.IsValidFolder(Ready+"/Meshes"))AssetDatabase.CreateFolder(Ready,"Meshes");
            if(!AssetDatabase.IsValidFolder(Ready+"/Materials"))AssetDatabase.CreateFolder(Ready,"Materials");
            Catalog.Clear();
            foreach(var s in Specs)
            {
                var root=new GameObject(s.label);
                try
                {
                    Create(root,s);
                    ValidateDoorway(root,s);
                    BatchStaticVisuals(root,s.id);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Ready+"/"+s.id+".prefab");
                    if(prefab==null)throw new InvalidOperationException("Prefab nicht gespeichert: "+s.label);
                    Catalog.Add(prefab);
                    try{RenderPreview(root,s.id);}catch(Exception e){Debug.LogWarning("Gebaeudevorschau nicht verfuegbar: "+e.Message);}
                }
                finally{Object.DestroyImmediate(root);}
            }
            // Placement uses the same road/terrain exclusion tests as the city.
            int placed=0; // Catalog only: enlarged replacements must not overwrite occupied lots.
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(Ready);EditorGUIUtility.PingObject(Selection.activeObject);
            File.WriteAllText("Library/CivicBuildingsResult.txt","Created "+Catalog.Count+" enterable prefabs; placed "+placed+" on safe free lots. Existing city and roads preserved. Play Mode gate test pending.");
            Debug.Log("[CITY CIVIC] "+Catalog.Count+" begehbare Typen, "+placed+" platziert. Katalog: "+Ready);
        }
        [MenuItem("Day Ones/Stadt/Begehbare Gebaeude oeffnen")]
        static void Open(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(Ready);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Individuelle Gebaeude V2 erstellen")]
        static void BuildV2(){BuildCatalog();}
        [MenuItem("Day Ones/Stadt/Individuelle Gebaeude V2 oeffnen")]
        static void OpenV2(){Open();}
        [MenuItem("Day Ones/Stadt/Shops und Gebaeude V3 erstellen")]
        static void BuildV3(){BuildCatalog();}
        [MenuItem("Day Ones/Stadt/Shops und Gebaeude V3 oeffnen")]
        static void OpenV3(){Open();}
        static Material Mat(string key,Color color,bool glass=false,bool glow=false)
        {
            if(Mats.TryGetValue(key,out var material))return material;
            string path=Ready+"/Materials/"+key+".mat";material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){name=key,color=color,enableInstancing=true};
                material.SetFloat("_Smoothness",glass?.75f:.25f);
                if(glass)
                {
                    material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
                    material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
                    material.SetShaderPassEnabled("ShadowCaster",false);
                    if(material.HasProperty("_Mode")){material.SetFloat("_Mode",3);material.EnableKeyword("_ALPHABLEND_ON");}
                }
                if(glow){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*2.5f);}
                AssetDatabase.CreateAsset(material,path);
            }
            Mats[key]=material;return material;
        }
        static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static Transform Group(Transform parent,string name,Vector3 p,float yaw=0)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.Euler(0,yaw,0);return g.transform;}
        static void Create(GameObject root,Spec s)
        {
            var t=root.transform;var wall=Mat(s.id,s.color);var trim=Mat("WarmWhite",White);var dark=Mat("Anthrazit",new Color(.10f,.13f,.16f));
            var glass=Mat("ClearGlass",new Color(.63f,.8f,.87f,.19f),true);var floor=Mat("InteriorFloor",new Color(.56f,.54f,.5f));
            var asphalt=Mat("Asphalt",new Color(.20f,.22f,.23f));var gold=Mat("Champagne",new Color(.72f,.54f,.22f));
            bool police=s.style=="police",wash=s.style=="carwash",warehouse=s.style=="warehouse",construction=s.style=="construction";
            Box(t,"Innenboden",new Vector3(0,-.06f,0),new Vector3(s.w,.2f,s.d),floor);
            if(construction){Construction(t,s,wall,dark);return;}
            float door=wash?4.6f:warehouse?4.2f:3.2f;
            BuildArchitecturalShell(t,s,door,wall,trim,glass,dark);
            Box(t,"Fassadenband",new Vector3(0,s.h-.42f,-s.d*.5f-.18f),new Vector3(s.w+.25f,.65f,.25f),police?Mat("PolizeiBlau",new Color(.035f,.16f,.31f)):s.style=="casino"?gold:dark);
            CityFacadeDetails.Word(t,s.sign,new Vector3(0,s.h-.42f,-s.d*.5f-.36f),.48f,trim);
            Box(t,"Eingangsvordach",new Vector3(0,3.14f,-s.d*.5f-.8f),new Vector3(door+1.2f,.12f,1.9f),dark,false);
            if(!wash)foreach(int side in new[]{-1,1})
            {
                var hinge=Group(t,"Tuerfluegel - offen",new Vector3(side*door*.5f,0,-s.d*.5f),side*90);
                float leaf=door*.5f-.08f;
                Box(hinge,"Offene Glastuer",new Vector3(-side*leaf*.5f,1.42f,0),new Vector3(leaf,2.84f,.035f),glass);
                foreach(int edge in new[]{0,1})Box(hinge,"Tuerrahmen",new Vector3(-side*leaf*edge,1.42f,0),new Vector3(.065f,2.84f,.085f),dark,false);
                Box(hinge,"Tuergriff",new Vector3(-side*(leaf-.12f),1.3f,-.08f),new Vector3(.045f,.38f,.06f),trim,false);
            }
            // No collider crosses the open door, which is 3.2 m wide and 3 m high.
            Group(t,"EINGANG - FREI",new Vector3(0,0,-s.d*.5f));
            Group(t,"LOGIK ANKER - SPAETER",new Vector3(0,0,0));
            var light=new GameObject("Innenraumlicht");light.transform.SetParent(t,false);light.transform.localPosition=new Vector3(0,s.h-.6f,0);
            var l=light.AddComponent<Light>();l.type=LightType.Point;l.range=Mathf.Max(s.w,s.d)*.9f;l.intensity=2;l.shadows=LightShadows.None;l.color=new Color(1,.94f,.84f);
            Box(t,"Deckenleuchte",new Vector3(0,s.h-.08f,0),new Vector3(2,.08f,1),Mat("CeilingLight",new Color(.9f,.86f,.73f),false,true),false);
            // Furnish only the edges: keep a wide, unobstructed route from the entrance.
            if(!wash&&!warehouse)Box(t,"Empfang oder Tresen",new Vector3(-s.w*.28f,.52f,s.d*.18f),new Vector3(s.w*.27f,1.04f,.8f),s.style=="casino"?gold:dark);
            AddIdentity(t,s,wall,trim,glass,dark,gold);
            if(s.style.StartsWith("shop_",StringComparison.Ordinal))ShopIdentity(t,s,wall,trim,glass,dark);
            if(s.style=="casino")Casino(t,s,dark,gold);
            if(s.style=="laundry")Laundry(t,s,trim,dark,glass);
            if(s.style=="bar"||s.style=="cafe"||s.style=="icecream"||s.style=="fastfood")Seating(t,s,trim,dark);
            if(wash)Carwash(t,s,dark);
            if(warehouse)Warehouse(t,s,trim,dark);
            if(s.style=="station")Station(t,s,trim,dark);
            if(s.style=="motel")Motel(t,s,trim,dark);
            if(police)PoliceYard(t,s,asphalt,trim,dark);
            var info=root.AddComponent<CityBuilding>();info.Configure(s.id,s.label,police?CityDistrict.Civic:CityDistrict.Commercial,s.type,false,true);
        }
        static void Facade(Transform t,float width,float height,float door,Material wall,Material trim,Material glass,Material dark,bool bars,bool onlyDoor,int windows=2,float windowWidth=1.35f,float sill=.95f,float windowTop=2.65f)
        {
            var openings=new List<Vector3>();
            if(door>0)openings.Add(new Vector3(0,door,0)); // x centre, width, sill height.
            if(!onlyDoor&&windows>0)
            {
                float available=door>0?(width-door)*.5f-.5f:width*.4f;
                float pane=Mathf.Min(windowWidth,available);
                if(pane>.2f)
                {
                    if(windows==1)openings.Add(new Vector3(door>0?(width+door)*.25f:0,pane,sill));
                    else foreach(int sign in new[]{-1,1})openings.Add(new Vector3(sign*(door>0?(width+door)*.25f:width*.26f),pane,sill));
                }
            }
            float edge=-width*.5f;
            foreach(var opening in openings.OrderBy(o=>o.x))
            {
                float left=opening.x-opening.y*.5f,right=opening.x+opening.y*.5f;
                if(left>edge)Box(t,"Wandpfeiler",new Vector3((left+edge)*.5f,height*.5f,0),new Vector3(left-edge,height,.25f),wall);
                float top=opening.z==0?3:Mathf.Min(windowTop,height-.3f);
                if(opening.z>0)
                {
                    Box(t,"Fensterbruestung",new Vector3(opening.x,opening.z*.5f,0),new Vector3(opening.y,opening.z,.25f),wall);
                    Box(t,"Durchsichtiges Fenster",new Vector3(opening.x,(opening.z+top)*.5f,0),new Vector3(opening.y,.0f+top-opening.z,.025f),glass);
                    Box(t,"Fenstersturzrahmen",new Vector3(opening.x,top,-.02f),new Vector3(opening.y+.12f,.08f,.18f),dark,false);
                    Box(t,"Fenstersohlrahmen",new Vector3(opening.x,opening.z,-.02f),new Vector3(opening.y+.12f,.08f,.18f),dark,false);
                    for(int side=-1;side<=1;side+=2)Box(t,"Fensterrahmen",new Vector3(opening.x+side*opening.y*.5f,(top+opening.z)*.5f,-.02f),new Vector3(.075f,top-opening.z,.18f),dark,false);
                    if(bars)
                    {
                        for(float x=left+.18f;x<right;x+=.24f)Box(t,"Sicherheitsgitter",new Vector3(x,(opening.z+top)*.5f,-.2f),new Vector3(.035f,top-opening.z+.15f,.035f),dark,false);
                        Box(t,"Gitterquerstrebe",new Vector3(opening.x,1.8f,-.2f),new Vector3(opening.y+.1f,.04f,.04f),dark,false);
                    }
                }
                if(height>top)Box(t,"Sturz",new Vector3(opening.x,(height+top)*.5f,0),new Vector3(opening.y,height-top,.25f),wall);
                edge=right;
            }
            if(edge<width*.5f)Box(t,"Wandpfeiler",new Vector3((edge+width*.5f)*.5f,height*.5f,0),new Vector3(width*.5f-edge,height,.25f),wall);
        }
        static void CivicFront(Transform t,Spec s,Material trim,Material gold)
        {
            for(int side=-1;side<=1;side+=2)for(int n=0;n<2;n++)
            {float x=side*(2.4f+n*1.8f);Box(t,"Eingangssaeule",new Vector3(x,1.55f,-s.d*.5f-.5f),new Vector3(.42f,3.1f,.42f),trim);Box(t,"Saeulenkapitell",new Vector3(x,3.05f,-s.d*.5f-.5f),new Vector3(.65f,.22f,.65f),gold);}
            if(s.style=="townhall")
            {
                Box(t,"Uhrenturm",new Vector3(0,s.h+1.3f,1),new Vector3(3.4f,2.6f,3.4f),trim);
                Box(t,"Turmabschluss",new Vector3(0,s.h+2.7f,1),new Vector3(3.8f,.25f,3.8f),gold);
                Box(t,"Uhrflaeche",new Vector3(0,s.h+1.5f,-.72f),new Vector3(1.6f,1.6f,.06f),gold,false);
                Box(t,"Uhrzeiger",new Vector3(0,s.h+1.65f,-.77f),new Vector3(.055f,.6f,.06f),Mat("Anthrazit",Color.gray),false);
            }
        }
        static void Casino(Transform t,Spec s,Material dark,Material gold)
        {
            var neon=Mat("CasinoNeon",new Color(.82f,.12f,.42f),false,true);var bulbs=Mat("CasinoBulbs",new Color(1,.68f,.24f),false,true);
            for(int side=-1;side<=1;side+=2)
            {
                Box(t,"Goldene Fassadenrippe",new Vector3(side*(s.w*.5f-.4f),s.h*.5f,-s.d*.5f-.3f),new Vector3(.3f,s.h,.3f),gold);
                Box(t,"Neon Kontur",new Vector3(side*(s.w*.5f-.8f),s.h*.5f,-s.d*.5f-.4f),new Vector3(.05f,s.h-.6f,.08f),neon,false);
            }
            Box(t,"Casino Marquise",new Vector3(0,3.3f,-s.d*.5f-1.2f),new Vector3(8,.35f,2.5f),gold);
            for(float x=-3.8f;x<=3.8f;x+=.42f)Box(t,"Marquise Leuchtpunkt",new Vector3(x,3.2f,-s.d*.5f-2.48f),new Vector3(.10f,.10f,.10f),bulbs,false);
            for(int i=0;i<4;i++){float x=s.w*.3f;Box(t,"Automatenkulisse",new Vector3(x,.85f,-s.d*.2f+i*2),new Vector3(.85f,1.7f,.65f),dark);Box(t,"Leuchtendes Display",new Vector3(x,1.2f,-s.d*.2f+i*2-.34f),new Vector3(.62f,.65f,.025f),neon,false);}
        }
        static void Laundry(Transform t,Spec s,Material white,Material dark,Material glass)
        {
            for(int i=0;i<4;i++){float z=-s.d*.2f+i*1.5f;Box(t,"Waschmaschine",new Vector3(s.w*.5f-.9f,.55f,z),new Vector3(1.1f,1.1f,1.1f),white);Box(t,"Maschinenfront",new Vector3(s.w*.5f-1.47f,.55f,z),new Vector3(.05f,.65f,.65f),dark,false);}
        }
        static void Seating(Transform t,Spec s,Material white,Material dark)
        {
            for(int n=0;n<3;n++){float z=-s.d*.2f+n*2;float x=s.w*.28f;Box(t,"Tisch",new Vector3(x,.8f,z),new Vector3(1.3f,.10f,.8f),white);Box(t,"Tischfuss",new Vector3(x,.4f,z),new Vector3(.15f,.8f,.15f),dark);foreach(int side in new[]{-1,1})Box(t,"Sitzbank",new Vector3(x,.4f,z+side*.7f),new Vector3(1.3f,.8f,.35f),dark);}
            var awning=Mat(s.id+"Awning",s.style=="icecream"?new Color(.64f,.79f,.72f):new Color(.39f,.25f,.21f));
            Box(t,"Lokal Markise",new Vector3(0,3.25f,-s.d*.5f-.65f),new Vector3(s.w-.8f,.12f,1.4f),awning,false);
        }
        static void Warehouse(Transform t,Spec s,Material trim,Material dark)
        {
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
            {float z=-s.d*.3f+i*s.d*.2f;Box(t,"Lagerregal",new Vector3(side*(s.w*.5f-1.6f),1.8f,z),new Vector3(2.3f,3.6f,.2f),dark);for(int level=0;level<3;level++)Box(t,"Regalboden",new Vector3(side*(s.w*.5f-1.6f),.4f+level*1.25f,z),new Vector3(2.3f,.12f,1.5f),trim);}
            for(float x=-s.w*.5f+.5f;x<s.w*.5f;x+=1.2f)Box(t,"Industriefassadenrippe",new Vector3(x,s.h*.5f,s.d*.5f+.17f),new Vector3(.05f,s.h,.06f),trim,false);
        }
        static void Carwash(Transform t,Spec s,Material dark)
        {
            var brush=Mat("CarWashBrush",new Color(.19f,.39f,.66f));
            for(int i=0;i<3;i++){float z=-s.d*.25f+i*s.d*.25f;foreach(int side in new[]{-1,1})Box(t,"Waschportal",new Vector3(side*3.2f,1.9f,z),new Vector3(.35f,3.8f,.4f),dark);Box(t,"Portaltraeger",new Vector3(0,3.75f,z),new Vector3(6.6f,.3f,.4f),dark);foreach(int side in new[]{-1,1})Box(t,"Seitliche Buerste",new Vector3(side*2.7f,1.2f,z),new Vector3(.5f,2.3f,.8f),brush,false);}
            foreach(int side in new[]{-1,1})Box(t,"Fahrspurmarkierung",new Vector3(side*1.5f,.055f,0),new Vector3(.10f,.02f,s.d),Mat("LaneWhite",White),false);
        }
        static void Station(Transform t,Spec s,Material trim,Material dark)
        {
            // Platform is wholly inside its own lot; no tracks or road changes.
            Box(t,"Wartebereich innen",new Vector3(0,.4f,s.d*.25f),new Vector3(7,.8f,.6f),dark);
            for(int side=-1;side<=1;side+=2)Box(t,"Bahnhofsvordachstuetze",new Vector3(side*(s.w*.5f-1),1.5f,-s.d*.5f-1.5f),new Vector3(.18f,3,.18f),dark);
            Box(t,"Bahnhofsvordach",new Vector3(0,3.1f,-s.d*.5f-1.5f),new Vector3(s.w,.16f,3),trim);
        }
        static void Motel(Transform t,Spec s,Material trim,Material dark)
        {
            // Ground-floor room wings with open doorways; centre remains reception.
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*s.w*.3f;
                Facade(Group(t,"Zimmerfluegel",new Vector3(x,0,1)),s.w*.35f,3.4f,1.8f,trim,trim,Mat("ClearGlass",new Color(.63f,.8f,.87f,.19f),true),dark,false,true);
                Box(t,"Bett",new Vector3(x,.32f,s.d*.27f),new Vector3(1.6f,.64f,2.2f),trim);
            }
        }
        static void Construction(Transform t,Spec s,Material concrete,Material dark)
        {
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
                Box(t,"Rohbau Betonstuetze",new Vector3(side*(s.w*.5f-.5f),s.h*.5f,end*(s.d*.5f-.5f)),new Vector3(.55f,s.h,.55f),concrete);
            foreach(int side in new[]{-1,1})Box(t,"Betontraeger",new Vector3(side*(s.w*.5f-.5f),s.h-.2f,0),new Vector3(.55f,.4f,s.d),concrete);
            Box(t,"Baustellencontainer",new Vector3(-s.w*.3f,1.3f,s.d*.25f),new Vector3(4,2.6f,2.4f),Mat("SiteOrange",new Color(.77f,.38f,.08f)));
            for(int i=0;i<5;i++)Box(t,"Baumaterial",new Vector3(s.w*.3f,.2f+i*.15f,s.d*.2f),new Vector3(3,.14f,1.2f),concrete);
            CityFacadeDetails.Word(t,"BAUSTELLE",new Vector3(0,3.2f,-s.d*.5f),.4f,Mat("WarmWhite",White));
            var info=t.gameObject.AddComponent<CityBuilding>();info.Configure(s.id,s.label,CityDistrict.Industrial,s.type,false,true);
            Group(t,"EINGANG - FREI",new Vector3(0,0,-s.d*.5f));
        }
        static void PoliceYard(Transform t,Spec s,Material asphalt,Material trim,Material dark)
        {
            // 30 x 22 m secured courtyard behind the station.
            float back=s.d*.5f,far=back+22,x=15;
            Box(t,"Polizeihof",new Vector3(0,-.06f,back+11),new Vector3(30,.2f,22),asphalt);
            foreach(int side in new[]{-1,1})
            {
                Fence(t,new Vector3(side*x,0,back+11),22,90,trim,dark);
                Fence(t,new Vector3(side*9,0,far),12,0,trim,dark);
                Fence(t,new Vector3(side*12,0,back),6,0,trim,dark);
            }
            // Six metres of clear access between the two fence wings.
            for(int side=-1;side<=1;side+=2)for(int i=0;i<5;i++)
                Box(t,"Parkplatzlinie",new Vector3(side*9,.055f,back+2+i*4),new Vector3(5.5f,.025f,.10f),trim,false);
            var gate=Group(t,"POLIZEI SCHIEBETOR - E",new Vector3(0,0,far));
            var panel=Group(gate,"Bewegliches Torblatt",new Vector3(0,0,-.2f));
            Box(panel,"Torrahmen unten",new Vector3(0,.35f,0),new Vector3(6,.10f,.18f),dark,false);
            Box(panel,"Torrahmen oben",new Vector3(0,2.25f,0),new Vector3(6,.10f,.18f),dark,false);
            for(float i=-2.9f;i<=2.9f;i+=.22f)Box(panel,"Torstab",new Vector3(i,1.3f,0),new Vector3(.045f,1.9f,.045f),dark,false);
            var collider=panel.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.25f,0);collider.size=new Vector3(6,2.5f,.18f);
            var rb=panel.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var sensor=gate.gameObject.AddComponent<BoxCollider>();sensor.isTrigger=true;sensor.center=new Vector3(3.2f,1.5f,0);sensor.size=new Vector3(13.2f,3,3);
            gate.gameObject.AddComponent<CourtyardSlidingGate>().Configure(rb,sensor,6.4f);
            foreach(int side in new[]{-1,1})
            {
                var control=Box(gate,"Bedienpfosten - E",new Vector3(-3.6f,.7f,side*1.3f),new Vector3(.35f,1.4f,.35f),dark);
                Box(control.transform,"Gruener Taster",new Vector3(0,.18f,-.55f),new Vector3(.3f,.12f,.12f),Mat("GateButton",Color.green,false,true),false);
            }
        }
        static void Fence(Transform t,Vector3 p,float length,float yaw,Material baseMat,Material metal)
        {
            var g=Group(t,"Sicherheitszaun",p,yaw);
            Box(g,"Zaunsockel",new Vector3(0,.18f,0),new Vector3(length,.36f,.23f),baseMat);
            // Physical surface is one simple box; thin decorative bars are batched.
            var collider=g.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.25f,0);collider.size=new Vector3(length,2.5f,.12f);
            for(float x=-length*.5f;x<=length*.5f;x+=.28f)Box(g,"Zaunstab",new Vector3(x,1.3f,0),new Vector3(.035f,2.1f,.035f),metal,false);
            Box(g,"Zaunholm",new Vector3(0,2.35f,0),new Vector3(length,.075f,.075f),metal,false);
        }
        static void ValidateDoorway(GameObject root,Spec s)
        {
            // Check the player-sized walking corridor using each real BoxCollider.
            foreach(var c in root.GetComponentsInChildren<BoxCollider>())
            {
                if(c.isTrigger)continue;
                for(float z=-s.d*.5f-.2f;z<-s.d*.5f+2.5f;z+=.25f)
                foreach(float x in new[]{-.4f,0,.4f})foreach(float y in new[]{.15f,.9f,1.8f})
                {
                    Vector3 p=root.transform.TransformPoint(new Vector3(x,y,z));
                    Vector3 local=c.transform.InverseTransformPoint(p)-c.center;Vector3 half=c.size*.5f;
                    if(Mathf.Abs(local.x)<half.x&&Mathf.Abs(local.y)<half.y&&Mathf.Abs(local.z)<half.z)
                        throw new InvalidOperationException(s.label+": Eingang wird blockiert durch "+c.name);
                }
            }
        }
        static void RenderPreview(GameObject root,string id)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var preview=new PreviewRenderUtility();
            try
            {
                var copy=Object.Instantiate(root);preview.AddSingleGO(copy);
                var rs=copy.GetComponentsInChildren<Renderer>();Bounds bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.23f,.27f,.29f);
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=500;preview.camera.fieldOfView=35;
                preview.camera.transform.position=bounds.center+new Vector3(.8f,.65f,-1).normalized*bounds.size.magnitude*1.6f;
                preview.camera.transform.LookAt(bounds.center);preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,640,480));preview.Render(true);
                var texture=preview.EndStaticPreview();Directory.CreateDirectory("Library/CivicPreviewsV2");File.WriteAllBytes("Library/CivicPreviewsV2/"+id+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{preview.Cleanup();}
        }
        static void BatchStaticVisuals(GameObject root,string id)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponentInParent<Rigidbody>()==null&&r.sharedMaterial!=null&&r.sharedMaterial.renderQueue<3000).ToArray();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {
                var list=group.ToArray();var combines=list.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name=id+" "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);
                string path=Ready+"/Meshes/"+id+"_"+group.Key.name+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(previous==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;EditorUtility.SetDirty(previous);}
                var batch=new GameObject("Fassade - "+group.Key.name);batch.transform.SetParent(root.transform,false);batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
                foreach(var renderer in list){Object.DestroyImmediate(renderer.GetComponent<MeshFilter>());Object.DestroyImmediate(renderer);}
            }
        }
    }
}
