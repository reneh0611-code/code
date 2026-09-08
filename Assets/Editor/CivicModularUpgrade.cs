using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        const string ModularRequest=Folder+"/ModularBuildings.request";
        [InitializeOnLoadMethod] static void WatchModular(){EditorApplication.delayCall+=PendingModular;}
        static void PendingModular()
        {
            if(!File.Exists(ModularRequest))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=PendingModular;return;}
            // Do not discard user's in-scene edits or instance overrides through a catalog rebuild.
            File.Move(ModularRequest,ModularRequest+"."+DateTime.UtcNow.Ticks+".consumed");
            try{BuildModularCopies();}catch(Exception e){File.WriteAllText("Library/ModularBuildingsResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Day Ones/Stadt/Modulare Einzelteile erstellen")]
        public static void BuildModularCopies()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
            // Existing catalogs and their instance overrides stay intact. New modular catalog is separate.
            string output=Folder+"/MODULAR - GEBAEUDE MIT EINZELTEILEN";
            if(!AssetDatabase.IsValidFolder(output))AssetDatabase.CreateFolder(Folder,"MODULAR - GEBAEUDE MIT EINZELTEILEN");
            foreach(var dir in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(output+"/"+dir))AssetDatabase.CreateFolder(output,dir);
            string previous=AssetOutput;AssetOutput=output;Mats.Clear();int count=0;
            try
            {
                foreach(var s in Specs)
                {
                    var root=new GameObject(s.label);
                    try
                    {
                        Create(root,s);root.transform.localScale=new Vector3(1,s.style=="exterior"?1:.8f,1);
                        if(s.style!="exterior")ValidateDoorway(root,s);ValidateExpansion(root,s);
                        BatchStaticVisuals(root,s.id);
                        if(PrefabUtility.SaveAsPrefabAsset(root,output+"/"+s.id+".prefab")==null)throw new InvalidOperationException("Could not save "+s.id);
                        count++;File.WriteAllText("Library/ModularBuildingsResult.txt","Built "+count+"/49 modular buildings. Existing catalogs unchanged.");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
                void SaveExtra(string id,Action<GameObject> create)
                {
                    var root=new GameObject(id);
                    try{create(root);BatchStaticVisuals(root,id);if(PrefabUtility.SaveAsPrefabAsset(root,output+"/"+id+".prefab")==null)throw new InvalidOperationException(id);count++;File.WriteAllText("Library/ModularBuildingsResult.txt","Built "+count+"/87 modular models.");}
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
                string[] vn={"Palais","Turmvilla","Landgut","Patio","ArtDeco","Atrium","Auskragung","Pavillon","Terrassen","Skulpturvilla"};
                for(int n=0;n<10;n++){int k=n;SaveExtra("V_"+(n+1).ToString("00")+"_"+vn[n],r=>MakeVilla(r,k));}
                string[] hn={"Villa_Klassik","Villa_Klinker","Villa_Modern","Villa_ArtDeco","Wohnhaus_Altbau","Wohnhaus_Klinker","Wohnhaus_Modern","Wohnhaus_Bungalow"};
                for(int n=0;n<8;n++)foreach(bool garden in new[]{false,true})
                {
                    int k=n;bool g=garden;SaveExtra("H_"+(n+1).ToString("00")+"_"+hn[n]+(g?"_MIT_GARTEN":"_OHNE_GARTEN"),r=>{float w=k==0?22:k==1?20:k==2?24:k==3?18:k==7?18:16;int f=k==7?1:k==4?3:2;MakeWalkableHome(r,k,w,f,g);r.transform.localScale=new Vector3(1,.8f,1);ValidateHomeRoutes(r,w,f,g);});
                }
                string[] dn={"Klinkerhaus","L_Haus","Doppelhaus","Stadthaus","Zeilenblock","Hofblock","Terrassenturm","Klinkerquartier","Gruenderzeit","Fachwerk","Kontor","Eckpalais"};
                for(int n=0;n<12;n++){int k=n;SaveExtra("Q_"+(n+1).ToString("00")+"_"+dn[n],r=>DistrictModel(r,k));}
                AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(output);EditorGUIUtility.PingObject(Selection.activeObject);
                File.WriteAllText("Library/ModularBuildingsResult.txt","SUCCESS: 87 modular buildings. Renderers remain on individual part transforms, benches and ATMs grouped. Existing catalogs and scenes unchanged. Old combined instances need deliberate replacement; automatic reversal is not lossless.");
            }
            finally{AssetOutput=previous;Mats.Clear();}
        }
        [MenuItem("Day Ones/Stadt/Modulare Gebaeude oeffnen")]
        static void OpenModular(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Folder+"/MODULAR - GEBAEUDE MIT EINZELTEILEN");EditorGUIUtility.PingObject(Selection.activeObject);}
    }
}
