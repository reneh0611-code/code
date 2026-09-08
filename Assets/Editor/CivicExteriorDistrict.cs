using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using CheatOnYourDayOnes.World;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        const string DistrictCatalog=Folder+"/WOHNHAEUSER BLOCKBAUTEN HISTORISCH - AUSSEN";
        const string DistrictRequest=Folder+"/ExteriorDistrict.request";
        [InitializeOnLoadMethod] static void WatchDistrict(){EditorApplication.delayCall+=PendingDistrict;}
        static void PendingDistrict()
        {
            if(!File.Exists(DistrictRequest))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=PendingDistrict;return;}
            File.Move(DistrictRequest,DistrictRequest+"."+DateTime.UtcNow.Ticks+".consumed");
            try{BuildExteriorDistrict();}catch(Exception e){File.WriteAllText("Library/ExteriorDistrictResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Wohnhaeuser Blocks Historisch oeffnen")]
        static void OpenDistrict(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(DistrictCatalog);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Day Ones/Stadt/Wohnhaeuser Blocks Historisch erstellen")]
        public static void BuildExteriorDistrict()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            if(!AssetDatabase.IsValidFolder(DistrictCatalog))AssetDatabase.CreateFolder(Folder,"WOHNHAEUSER BLOCKBAUTEN HISTORISCH - AUSSEN");
            foreach(var d in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(DistrictCatalog+"/"+d))AssetDatabase.CreateFolder(DistrictCatalog,d);
            string[] names={"01_Wohnhaus_Klinker_Garten","02_Wohnhaus_Modern_L_Form","03_Wohnhaus_Doppelhaus","04_Wohnhaus_Stadthaus","05_Block_Zeilenbau","06_Block_Hofensemble","07_Block_Terrassenturm","08_Block_Klinkerquartier","09_Historisch_Gruenderzeit","10_Historisch_Fachwerk","11_Historisch_Kontorhaus","12_Historisch_Eckpalais"};
            string old=AssetOutput;AssetOutput=DistrictCatalog;Mats.Clear();
            try
            {
                for(int n=0;n<names.Length;n++)
                {
                    var root=new GameObject(names[n]);
                    try
                    {
                        DistrictModel(root,n);
                        if(root.GetComponentsInChildren<BoxCollider>().Length==0)throw new InvalidOperationException("Missing shell collider");
                        RenderPreview(root,"DISTRICT_RAW_"+names[n]);
                        BatchStaticVisuals(root,names[n]);
                        if(PrefabUtility.SaveAsPrefabAsset(root,DistrictCatalog+"/"+names[n]+".prefab")==null)throw new InvalidOperationException("Prefab save failed");
                        RenderPreview(root,"DISTRICT_"+names[n]);
                        File.WriteAllText("Library/ExteriorDistrictResult.txt","Built "+(n+1)+"/12: "+names[n]);
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                AssetDatabase.SaveAssets();OpenDistrict();
                File.WriteAllText("Library/ExteriorDistrictResult.txt","SUCCESS: 12 exterior prefabs (4 homes, 4 blocks, 4 historical). Solid building colliders; no scene placement, plants or water. Previews generated. Play Mode test pending.");
            }
            finally{AssetOutput=old;Mats.Clear();}
        }
        static void DistrictModel(GameObject root,int n)
        {
            var t=root.transform;var trim=VMat("Quartier_Kalkstein",new Color(.82f,.79f,.7f));var dark=VMat("Quartier_Anthrazit",new Color(.075f,.095f,.11f));
            var brick=VMat("Quartier_Klinker",new Color(.40f,.215f,.15f));var white=VMat("Quartier_Elfenbein",new Color(.86f,.85f,.79f));
            var sage=VMat("Quartier_Salbei",new Color(.47f,.52f,.46f));var sand=VMat("Quartier_Sandstein",new Color(.71f,.61f,.46f));
            var roof=VMat("Quartier_Schiefer",new Color(.125f,.15f,.17f));var wood=VMat("Quartier_Eiche",new Color(.24f,.16f,.105f));
            void Volume(string name,float x,float z,float w,float d,int f,Material wall,bool historic,float y=0,float yaw=0)
            {
                var p=Group(t,name,new Vector3(x,y,z),yaw);VillaVolume(p,"Baukoerper",Vector3.zero,w,d,f,wall,historic?trim:dark,roof,historic,true);
                DistrictEdges(p,w,d,f,historic?trim:dark);
            }
            switch(n)
            {
                case 0:
                    Volume("Klinkerhaus",0,0,12,11,2,brick,true);DistrictGarden(t,16,11,12,trim);
                    DistrictShutters(t,12,11,2,trim,wood);DistrictPorch(t,-5.5f,trim,roof);break;
                case 1:
                    Volume("Hauptfluegel",-3,0,14,11,2,white,false);Volume("Gartenfluegel",7,4,6,16,1,sage,false);
                    VillaPergola(t,new Vector3(-3,0,8),13,4,wood);DistrictGarden(t,24,17,8,trim);break;
                case 2:
                    foreach(int s in new[]{-1,1}){Volume("Doppelhaus Haelfte",s*5.2f,0,10.4f,12,2,s<0?white:brick,true);DistrictPorch(Group(t,"Haustuer",new Vector3(s*5.2f,0,0)),-6,trim,roof);}
                    DistrictGarden(t,24,12,10,trim);Box(t,"Gartentrennung",new Vector3(0,.55f,11),new Vector3(.16f,1.1f,10),trim);break;
                case 3:
                    Volume("Schmales Stadthaus",0,0,9,12,3,sage,true);for(int f=1;f<3;f++)VillaBalustrade(t,new Vector3(0,f*3.6f,-6.7f),5,trim);DistrictDormers(t,9,12,10.8f,trim,roof);break;
                case 4:
                    foreach(int s in new[]{-1,0,1}){var g=Group(t,"Zeilenbau Segment",new Vector3(s*14,0,0));VillaVolume(g,"Wohnriegel",Vector3.zero,14,14,s==0?6:5,s==0?sage:white,dark,roof,false,true);DistrictBalconies(g,14,14,s==0?6:5,dark);DistrictEdges(g,14,14,s==0?6:5,dark);}
                    break;
                case 5:
                    Volume("Hof Ruecken",0,12,42,12,5,white,false);
                    foreach(int s in new[]{-1,1}){Volume("Hof Seitenfluegel",s*15,-5,12,22,4,s<0?sage:sand,false);DistrictBalconies(Group(t,"Hof Balkone",new Vector3(s*15,0,-5)),12,22,4,dark);}
                    Box(t,"Freier Innenhof",new Vector3(0,-.08f,-3),new Vector3(18,.16f,28),trim);break;
                case 6:
                    Volume("Sockel",0,0,24,20,3,sand,false);
                    Volume("Turm",-3,2,18,16,4,white,false,10.8f);
                    Volume("Staffelgeschoss",-5,4,14,12,1,sage,false,25.2f);
                    DistrictBalconies(t,24,20,3,dark);DistrictBalconies(Group(t,"Turmbalkone",new Vector3(-3,10.8f,2)),18,16,4,dark);break;
                case 7:
                    foreach(int s in new[]{-1,1}){Volume("Klinker Wohntrakt",s*12,0,24,15,s<0?4:5,brick,true);DistrictBalconies(Group(t,"Balkongruppe",new Vector3(s*12,0,0)),24,15,s<0?4:5,dark);}
                    Volume("Treppenturm",0,-1,4,14,6,sand,false);break;
                case 8:
                    Volume("Gruenderzeithaus",0,0,18,14,4,sand,true);DistrictDormers(t,18,14,14.4f,trim,roof);
                    for(int f=1;f<4;f++){Cornice(t,18.6f,14.6f,f*3.6f,trim,3);VillaBalustrade(t,new Vector3(0,f*3.6f,-7.7f),5,trim);}
                    DistrictPilasters(t,18,14,14.4f,trim);break;
                case 9:
                    Volume("Fachwerkhaus",0,0,10,11,3,white,true);
                    foreach(int s in new[]{-1,1})
                    {
                        var g=Group(t,"Fachwerkfassade",Vector3.zero,s<0?0:180);
                        for(int f=0;f<=3;f++)Box(g,"Schwellenbalken",new Vector3(0,f*3.6f,-5.66f),new Vector3(10,.18f,.17f),wood,false);
                        foreach(float x in new[]{-4.8f,0,4.8f})Box(g,"Fachwerkstaender",new Vector3(x,5.4f,-5.66f),new Vector3(.18f,10.8f,.17f),wood,false);
                        for(int f=0;f<3;f++)foreach(int side in new[]{-1,1})Beam(g,"Strebe",new Vector3(side*.2f,f*3.6f+.3f,-5.7f),new Vector3(side*1.1f,f*3.6f+3.3f,-5.7f),.13f,wood);
                    }
                    DistrictPorch(t,-5.5f,wood,roof);break;
                case 10:
                    Volume("Historisches Kontor",0,0,24,16,4,brick,true);DistrictPilasters(t,24,16,14.4f,trim);
                    foreach(int s in new[]{-1,1}){var g=Group(t,"Zwerchgiebel",new Vector3(s*7,0,-6.5f));Pitched(g,"Ziergiebel",6,3.6f,14.4f,3.3f,trim);Arch(g,new Vector3(0,15.2f,-1.85f),.7f,.15f,.2f,dark,24);}
                    break;
                case 11:
                    Volume("Palais Hauptfluegel",0,0,22,14,3,white,true);Volume("Palais Seitenfluegel",8,9,6,10,3,sand,true);
                    DistrictPilasters(t,22,14,10.8f,trim);DistrictDormers(t,22,14,10.8f,trim,roof);
                    var tower=Group(t,"Eckturm",new Vector3(-10,0,-6));Profile(tower,"Oktogonaler Turm",Vector3.zero,new[]{0f,13f},new[]{2.8f,2.8f},8,trim);
                    var box=Box(tower,"Turmkollision",new Vector3(0,6.5f,0),new Vector3(3.8f,13,3.8f),trim);Object.DestroyImmediate(box.GetComponent<MeshRenderer>());Object.DestroyImmediate(box.GetComponent<MeshFilter>());
                    for(int a=0;a<8;a++)for(int f=0;f<3;f++)VillaWindow(Group(tower,"Turmfenster",Vector3.zero,a*45),new Vector3(0,1.9f+f*3.6f,-2.63f),1.1f,1.8f,dark,true);
                    Profile(tower,"Kupferhaube",Vector3.zero,new[]{13f,13.3f,15.5f,17f},new[]{3f,3f,1.1f,0f},16,VMat("Patiniertes_Kupfer",new Color(.2f,.34f,.3f)));break;
            }
            root.AddComponent<CityBuilding>().Configure(root.name,root.name,CityDistrict.Residential,CityBuildingType.Residential,false,false);
            Group(t,"AUSSENMODELL - geschlossene Tueren",Vector3.zero);
        }
        static void DistrictEdges(Transform t,float w,float d,int floors,Material mat)
        {foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Box(t,"Fallrohr",new Vector3(sx*(w*.5f-.12f),floors*1.8f,sz*(d*.5f+.16f)),new Vector3(.075f,floors*3.6f,.075f),mat,false);}
        static void DistrictGarden(Transform t,float w,float d,float length,Material mat)
        {var g=Group(t,"GARTEN - frei gestaltbar",new Vector3(0,0,d*.5f));Box(g,"Gartenflaeche",new Vector3(0,-.1f,length*.5f),new Vector3(w,.2f,length),VMat("Unbepflanzte_Flaeche",new Color(.3f,.35f,.23f)));Box(g,"Terrasse",new Vector3(0,.015f,1.5f),new Vector3(w-2,.03f,3),mat);foreach(int s in new[]{-1,1})Box(g,"Gartenmauer",new Vector3(s*w*.5f,.5f,length*.5f),new Vector3(.18f,1,length),mat);Box(g,"Rueckmauer",new Vector3(0,.5f,length),new Vector3(w,1,.18f),mat);}
        static void DistrictBalconies(Transform t,float w,float d,int floors,Material mat)
        {for(int f=1;f<floors;f++)foreach(int s in new[]{-1,1})VillaBalustrade(t,new Vector3(s*w*.27f,f*3.6f,-d*.5f-.7f),Mathf.Min(4,w*.3f),mat);}
        static void DistrictPorch(Transform t,float z,Material trim,Material roof)
        {foreach(int s in new[]{-1,1})Pillar(t,new Vector3(s*1.5f,0,z-1.3f),2.9f,.12f,trim);Pitched(Group(t,"Eingangsportal",new Vector3(0,0,z-.8f)),"Portaldach",3.5f,2.1f,3,.8f,roof);}
        static void DistrictDormers(Transform t,float w,float d,float y,Material trim,Material roof)
        {foreach(int s in new[]{-1,1}){var g=Group(t,"Ziergaube",new Vector3(s*w*.25f,y+.5f,-d*.5f+.6f));Box(g,"Gaubenkasten",new Vector3(0,.65f,0),new Vector3(1.8f,1.3f,2),trim,false);VillaWindow(g,new Vector3(0,.75f,-1.02f),1.15f,1.05f,trim,true);Pitched(g,"Gaubendach",2.1f,2.3f,1.4f,.7f,roof);}}
        static void DistrictPilasters(Transform t,float w,float d,float h,Material mat)
        {foreach(int s in new[]{-1,1}){Box(t,"Eckpilaster",new Vector3(s*(w*.5f-.4f),h*.5f,-d*.5f-.17f),new Vector3(.65f,h,.25f),mat,false);Box(t,"Kapitell",new Vector3(s*(w*.5f-.4f),h-.3f,-d*.5f-.2f),new Vector3(1,.4f,.4f),mat,false);}for(float x=-w*.5f+.4f;x<w*.5f;x+=.6f)Box(t,"Konsolfries",new Vector3(x,h-.3f,-d*.5f-.2f),new Vector3(.15f,.28f,.3f),mat,false);}
        static void DistrictShutters(Transform t,float w,float d,int levels,Material trim,Material wood)
        {for(int f=0;f<levels;f++)foreach(int s in new[]{-1,1})foreach(int side in new[]{-1,1}){float x=s*w*.375f+side*.96f;Box(t,"Fensterladen",new Vector3(x,f*3.6f+1.95f,-d*.5f-.12f),new Vector3(.5f,2.05f,.09f),wood,false);for(int k=0;k<9;k++)Box(t,"Ladenlamelle",new Vector3(x,f*3.6f+1.1f+k*.2f,-d*.5f-.18f),new Vector3(.4f,.045f,.04f),trim,false);}}
    }
}
