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
        const string Homes=Folder+"/BEGEHBARE VILLEN UND WOHNHAEUSER";
        const string HomesRequest=Folder+"/WalkableHomes.request";
        [InitializeOnLoadMethod] static void WatchHomes(){EditorApplication.delayCall+=PendingHomes;}
        static void PendingHomes()
        {
            if(!File.Exists(HomesRequest))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=PendingHomes;return;}
            bool homesOnly=File.ReadAllText(HomesRequest).Contains("homes-only");
            File.Move(HomesRequest,HomesRequest+"."+DateTime.UtcNow.Ticks+".consumed");
            try{if(!homesOnly)BuildVillas();BuildWalkableHomes();}catch(Exception e){File.WriteAllText("Library/WalkableHomesResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Begehbare Villen und Wohnhaeuser oeffnen")]
        static void OpenHomes(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(Homes);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Begehbare Villen und Wohnhaeuser erstellen")]
        public static void BuildWalkableHomes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            if(!AssetDatabase.IsValidFolder(Homes))AssetDatabase.CreateFolder(Folder,"BEGEHBARE VILLEN UND WOHNHAEUSER");
            foreach(var dir in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(Homes+"/"+dir))AssetDatabase.CreateFolder(Homes,dir);
            string old=AssetOutput;AssetOutput=Homes;Mats.Clear();int count=0;
            string[] names={"Villa_Klassik","Villa_Klinker","Villa_Modern","Villa_ArtDeco","Wohnhaus_Altbau","Wohnhaus_Klinker","Wohnhaus_Modern","Wohnhaus_Bungalow"};
            try
            {
                for(int n=0;n<8;n++)foreach(bool garden in new[]{false,true})
                {
                    string id=(n+1).ToString("00")+"_"+names[n]+(garden?"_MIT_GARTEN":"_OHNE_GARTEN");
                    var root=new GameObject(id);
                    try
                    {
                        float width=n==0?22:n==1?20:n==2?24:n==3?18:n==7?18:16;int levels=n==7?1:n==4?3:2;
                        MakeWalkableHome(root,n,width,levels,garden);
                        root.transform.localScale=new Vector3(1,.8f,1);
                        ValidateHomeRoutes(root,width,levels,garden);
                        RenderHomeCutaway(root,id);
                        RenderPreview(root,"HOME_DETAIL_"+id);
                        BatchStaticVisuals(root,id);
                        if(PrefabUtility.SaveAsPrefabAsset(root,Homes+"/"+id+".prefab")==null)throw new InvalidOperationException("Save failed: "+id);
                        RenderPreview(root,"HOME_"+id);count++;
                        File.WriteAllText("Library/WalkableHomesResult.txt","Built "+count+"/16. Routes checked: "+id);
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                AssetDatabase.SaveAssets();OpenHomes();
                File.WriteAllText("Library/WalkableHomesResult.txt","SUCCESS: 16 enterable prefabs, 4 villa designs and 4 house designs, each with/without garden. Root Y=0.8. Entrance, room, stair, landing and garden routes checked for clearance and floor support. No plants or water. Scene unchanged. Actual player Play Mode test pending.");
            }
            finally{AssetOutput=old;Mats.Clear();}
        }
        static void MakeWalkableHome(GameObject root,int style,float width,int levels,bool garden)
        {
            var t=root.transform;bool historic=style==0||style==1||style==4||style==5;
            var trim=VMat("Haus_Kalkstein",new Color(.83f,.80f,.72f));var dark=VMat("Haus_Metall",new Color(.075f,.09f,.1f));
            var wall=VMat("Hausputz_"+style,style==1||style==5?new Color(.42f,.23f,.16f):style==6?new Color(.43f,.48f,.46f):style==7?new Color(.74f,.70f,.59f):new Color(.88f,.85f,.78f));
            var glass=Mat("HomeTransparentGlass",new Color(.57f,.74f,.81f,.17f),true);
            var floor=VMat("Eichenparkett",new Color(.43f,.33f,.23f));var roof=VMat("Haus_Schiefer",new Color(.12f,.145f,.17f));
            const float storey=4;float half=width*.5f;
            for(int level=0;level<levels;level++)
            {
                var g=Group(t,"WOHNEBENE "+(level+1),new Vector3(0,level*storey,0));
                if(level==0)Box(g,"Boden",new Vector3(0,-.1f,0),new Vector3(width,.2f,16),floor);
                else
                {
                    foreach(int s in new[]{-1,1})Box(g,"Seitlicher Geschossboden",new Vector3(s*(half+1.25f)*.5f,-.1f,0),new Vector3(half-1.25f,.2f,16),floor);
                    Box(g,"Vorderes Podest",new Vector3(0,-.1f,-4.5f),new Vector3(2.5f,.2f,7),floor);
                    Box(g,"Hinteres Podest",new Vector3(0,-.1f,7.1f),new Vector3(2.5f,.2f,1.8f),floor);
                }
                Facade(Group(g,"Front - Schnitt ausblendbar",new Vector3(0,0,-8)),width,4,level==0?2.4f:0,wall,trim,glass,dark,false,false,2,historic?1.6f:3.2f,.9f,3.2f);
                Facade(Group(g,"Rueckfassade",new Vector3(0,0,8),180),width,4,level==0||style==2?2.4f:0,wall,trim,glass,dark,false,false,2,historic?1.6f:3.2f,.9f,3.2f);
                if(style==2&&level>0)
                {
                    Box(g,"Begehbare Loggia",new Vector3(0,-.1f,9.6f),new Vector3(width,.2f,3.2f),trim);
                    Box(g,"Loggia Frontgelaender",new Vector3(0,.7f,11.15f),new Vector3(width,1.4f,.08f),glass);
                    foreach(int s in new[]{-1,1})Box(g,"Loggia Seitengelaender",new Vector3(s*(half-.05f),.7f,9.6f),new Vector3(.08f,1.4f,3.2f),glass);
                    Box(g,"Loggia Handlauf",new Vector3(0,1.43f,11.15f),new Vector3(width,.06f,.1f),dark,false);
                }
                foreach(int side in new[]{-1,1})
                {
                    Facade(Group(g,"Seitenfassade",new Vector3(side*half,0,0),side*90),16,4,0,wall,trim,glass,dark,false,false,2,historic?1.5f:2.8f,.95f,3.2f);
                    InteriorDivider(g,0,-8,8,side*3,2.2f,4,trim,true,-4);
                    float roomX=side*(half+3)*.5f;
                    InteriorDivider(g,0,side<0?-half:3,side<0?-3:half,0,2.2f,4,trim,false,roomX);
                    Group(g,level==0?(side<0?"WOHNEN UND ESSEN":"KUECHE UND ARBEITEN"):(side<0?"SCHLAFEN":"GASTZIMMER"),new Vector3(roomX,0,-4));
                    Group(g,"ZIMMER HINTEN",new Vector3(roomX,0,4));
                    // Furniture stays near the outer walls; all connecting door routes stay free.
                    if(level==0)
                    {
                        Box(g,"Kuechenunterschraenke",new Vector3(side*(half-1.6f),.63f,-6.7f),new Vector3(2.4f,1.26f,.7f),trim);
                        Box(g,"Arbeitsplatte",new Vector3(side*(half-1.6f),1.28f,-6.7f),new Vector3(2.55f,.08f,.78f),dark);
                        for(int i=0;i<3;i++)Box(g,"Schrankfuge",new Vector3(side*(half-2.35f+i*.75f),.65f,-7.06f),new Vector3(.02f,1.1f,.015f),dark,false);
                    }
                    else
                    {
                        Box(g,"Bettgestell",new Vector3(side*(half-1.5f),.35f,-6),new Vector3(1.9f,.7f,2.2f),floor);
                        Box(g,"Matratze",new Vector3(side*(half-1.5f),.77f,-6),new Vector3(1.85f,.18f,2.1f),trim);
                        Box(g,"Kopfteil",new Vector3(side*(half-1.5f),.9f,-7.1f),new Vector3(2,1.5f,.12f),floor);
                    }
                    Box(g,"Wandschrank",new Vector3(side*(half-.6f),1.25f,5.7f),new Vector3(.8f,2.5f,2.6f),floor);
                    // Rear room remains openly usable for later furnishing / gameplay.
                }
                if(level<levels-1)
                {
                    for(int step=0;step<24;step++)Box(g,"Treppe "+step,new Vector3(0,(step+1)*4f/24-.09f,-1+(step+.5f)*.3f),new Vector3(2,.18f,.3f),trim);
                    foreach(int side in new[]{-1,1})
                    {
                        Beam(g,"Treppenhandlauf",new Vector3(side*1.08f,1.3f,-1),new Vector3(side*1.08f,5.3f,6.2f),.075f,dark);
                        for(int k=0;k<24;k+=2)Box(g,"Treppenpfosten",new Vector3(side*1.08f,(k+1)*4f/24+.65f,-1+(k+.5f)*.3f),new Vector3(.06f,1.3f,.06f),dark);
                    }
                }
                if(level>0)foreach(int side in new[]{-1,1})
                {
                    Box(g,"Treppenauge Absturzschutz",new Vector3(side*1.28f,.65f,2.6f),new Vector3(.08f,1.3f,7.2f),glass);
                    Box(g,"Podesthandlauf",new Vector3(side*1.28f,1.33f,2.6f),new Vector3(.09f,.07f,7.2f),dark,false);
                }
                // Wall-side trim never spans a doorway.
                Cornice(g,width+.15f,16.15f,3.92f,trim,historic?2:1);
                var lamp=Group(g,"Deckenleuchte",new Vector3(0,3.6f,-4));Box(lamp,"Leuchtenkoerper",Vector3.zero,new Vector3(.65f,.08f,.65f),trim,false);
                var light=lamp.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=14;light.intensity=1.1f;light.shadows=LightShadows.None;
            }
            var roofGroup=Group(t,"DACH - Schnitt ausblendbar",Vector3.zero);
            if(historic)Pitched(roofGroup,"Geschlossenes Satteldach",width+.8f,16.8f,levels*4,style==4?3.4f:2.6f,roof);
            else
            {
                Box(roofGroup,"Dachdecke",new Vector3(0,levels*4+.12f,0),new Vector3(width+.5f,.24f,16.5f),trim);
                foreach(int s in new[]{-1,1}){Box(roofGroup,"Attika",new Vector3(s*(half+.1f),levels*4+.45f,0),new Vector3(.22f,.5f,16.5f),trim);Box(roofGroup,"Attika",new Vector3(0,levels*4+.45f,s*8.1f),new Vector3(width,.5f,.22f),trim);}
            }
            FramedGlazing(t,historic?trim:dark);
            if(historic)WindowDressings(t,trim,style==0||style==4);
            var spec=new Spec("80_Home","Home","","residential",width,16,levels*4,wall.color,CityBuildingType.Residential);
            Masonry(t,spec);RoofCourses(t);
            HomeIdentity(t,style,width,levels,trim,dark,roof);
            RefineResidentialArchitecture(t,style,width,levels,wall,trim,dark,roof);
            if(garden)HomeGarden(t,width,trim,dark);
            root.AddComponent<CityBuilding>().Configure(root.name,root.name,CityDistrict.Residential,CityBuildingType.Residential,false,true);
            Group(t,"EINGANG - OFFEN",new Vector3(0,0,-8));Group(t,"GARTENTUER - OFFEN",new Vector3(0,0,8));
        }
        static void HomeIdentity(Transform t,int style,float w,int levels,Material trim,Material dark,Material roof)
        {
            float half=w*.5f;
            if(style==0)
            {
                foreach(float x in new[]{-4f,-2f,2f,4f})Pillar(t,new Vector3(x,0,-9.7f),6.9f,.23f,trim);
                Box(t,"Portikus Gebaelk",new Vector3(0,7.1f,-9.5f),new Vector3(10,.35f,3.4f),trim);
                Pitched(Group(t,"Portikus Fronton",new Vector3(0,0,-9.5f)),"Tempelgiebel",10.5f,3.5f,7.3f,1.6f,trim);
            }
            else if(style==1||style==5)
            {
                foreach(int s in new[]{-1,1})
                {
                    Box(t,"Kaminschacht",new Vector3(s*(half-2),levels*4+1,4),new Vector3(.9f,3.5f,1.1f),trim);
                    Box(t,"Kaminabdeckung",new Vector3(s*(half-2),levels*4+2.8f,4),new Vector3(1.2f,.18f,1.4f),dark);
                }
                VillaPergola(t,new Vector3(0,0,10),w-2,3,trim);
            }
            else if(style==2)
            {
                GlassCanopy(t,new Vector3(0,3.35f,-8.5f),w-2,2.8f,dark);
                foreach(int s in new[]{-1,1})SlatScreen(t,new Vector3(s*(half-1),0,-8.25f),1.2f,7.8f,dark);
            }
            else if(style==3)
            {
                for(int k=0;k<3;k++)foreach(int s in new[]{-1,1})Box(t,"Art Deco Vertikalprofil",new Vector3(s*(1.55f+k*.27f),4,-8.23f),new Vector3(.12f,8.5f-k*.25f,.24f),trim,false);
                Box(t,"Gestufte Eingangskrone",new Vector3(0,8.45f,-7.5f),new Vector3(5,.4f,1.5f),trim);
                Box(t,"Eingangsvordach",new Vector3(0,3.2f,-9),new Vector3(5,.2f,2),dark);
            }
            else if(style==4)
            {
                for(int s=-1;s<=1;s+=2)for(float y=.4f;y<levels*4;y+=.5f)Box(t,"Rustizierte Ecke",new Vector3(s*(half-.22f),y,-8.16f),new Vector3(.45f,.32f,.18f),trim,false);
                Arch(t,new Vector3(0,3.2f,-8.3f),1.4f,.18f,.3f,trim,20);
            }
            else if(style==6)GlassCanopy(t,new Vector3(0,3.2f,-8.4f),4,2,dark);
            else VillaPergola(t,new Vector3(0,0,10),w-1,3,trim);
            // Small wall lanterns flank, never obstruct, the front opening.
            WallLantern(t,new Vector3(-1.6f,2.3f,-8.3f),dark);WallLantern(t,new Vector3(1.6f,2.3f,-8.3f),dark);
        }
        static void HomeGarden(Transform t,float w,Material stone,Material dark)
        {
            var g=Group(t,"GARTEN - UNBEPFLANZT",Vector3.zero);float gw=w+4;
            Box(g,"Gartenboden",new Vector3(0,-.08f,16),new Vector3(gw,.16f,16),VMat("Garten_Unbepflanzt",new Color(.32f,.37f,.24f)));
            Box(g,"Terrasse",new Vector3(0,-.02f,10),new Vector3(w,.04f,4),stone);
            foreach(int s in new[]{-1,1})Box(g,"Gartenmauer",new Vector3(s*gw*.5f,.65f,16),new Vector3(.25f,1.3f,16),stone);
            foreach(int s in new[]{-1,1})Box(g,"Rueckmauer mit Durchgang",new Vector3(s*(gw*.25f+.6f),.65f,24),new Vector3(gw*.5f-1.2f,1.3f,.25f),stone);
        }
        static void ValidateHomeRoutes(GameObject root,float width,int levels,bool garden)
        {
            var boxes=root.GetComponentsInChildren<BoxCollider>().Where(c=>!c.isTrigger).ToArray();float sy=root.transform.lossyScale.y;
            void Check(float x,float y,float z,bool support=true)
            {
                foreach(float dx in new[]{-.32f,0,.32f})foreach(float dz in new[]{-.12f,0,.12f})foreach(float dy in new[]{.15f,.45f,.8f,1.2f,1.6f,2f})
                {
                    var p=root.transform.TransformPoint(new Vector3(x+dx,y+dy/sy,z+dz));
                    foreach(var c in boxes){var q=c.transform.InverseTransformPoint(p)-c.center;var h=c.size*.5f;if(Mathf.Abs(q.x)<h.x-.001f&&Mathf.Abs(q.y)<h.y-.001f&&Mathf.Abs(q.z)<h.z-.001f)throw new InvalidOperationException(root.name+": blocked by "+c.name+" at "+new Vector3(x,y,z));}
                }
                if(support)
                {
                    bool found=false;var p=root.transform.TransformPoint(new Vector3(x,y-.05f,z));
                    foreach(var c in boxes){var q=c.transform.InverseTransformPoint(p)-c.center;var h=c.size*.5f;if(Mathf.Abs(q.x)<=h.x+.001f&&Mathf.Abs(q.y)<=h.y+.001f&&Mathf.Abs(q.z)<=h.z+.001f){found=true;break;}}
                    if(!found)throw new InvalidOperationException(root.name+": missing floor at "+new Vector3(x,y,z));
                }
            }
            for(float z=-8;z<=-2;z+=.25f)Check(0,0,z);
            for(int f=0;f<levels;f++)
            {
                float y=f*4,room=(width*.5f+3)*.5f;
                foreach(int s in new[]{-1,1})
                {
                    for(float x=0;x<=room;x+=.25f)Check(s*x,y,-4);
                    for(float z=-4;z<=4;z+=.25f)Check(s*room,y,z);
                }
                for(float z=-4;z<=7;z+=.25f)Check(2.1f,y,z);
                for(float x=0;x<=2.1f;x+=.2f)Check(x,y,7);
                if(root.name.Contains("Villa_Modern")&&f>0)for(float z=7;z<=10.5f;z+=.25f)Check(0,y,z);
                if(f<levels-1)for(int k=0;k<24;k++)Check(0,y+(k+1)*4f/24,-1+(k+.5f)*.3f);
            }
            for(float z=7;z<=8;z+=.25f)Check(0,0,z);
            if(garden)for(float z=8.25f;z<24;z+=.25f)Check(0,0,z);
        }
        static void RenderHomeCutaway(GameObject root,string id)
        {
            var copy=Object.Instantiate(root);
            try
            {
                foreach(var child in copy.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Schnitt ausblendbar")||t.name.StartsWith("Dachdeckung ")||t.name.StartsWith("Mauerwerk Relief ")).ToArray())if(child!=null)Object.DestroyImmediate(child.gameObject);
                RenderPreview(copy,"HOME_SCHNITT_"+id);
            }
            finally{Object.DestroyImmediate(copy);}
        }
    }
}
