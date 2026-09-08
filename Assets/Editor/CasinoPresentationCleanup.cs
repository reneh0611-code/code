#if UNITY_EDITOR
using CheatOnYourDayOnes.Casino;
using UnityEditor;
using UnityEngine;
public static class CasinoPresentationCleanup
{
    [MenuItem("Day Ones/Casino/Clean Decorative Text")]
    public static void Clean()
    {
        const string path="Assets/Buildings/DayOnesCasino/Games/Casino_Games.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            const string matPath="Assets/Buildings/DayOnesCasino/Games/InlaidLettering.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(Shader.Find("DayOnes/Casino Inlaid Lettering"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.mainTexture=font.material.mainTexture;EditorUtility.SetDirty(mat);
            foreach(var t in root.GetComponentsInChildren<TextMesh>(true))
            {
                if(t.text=="LUCKY SEVEN"||t.text=="EUROPEAN ROULETTE"||t.text=="BLACKJACK PAYS 3 : 2")t.gameObject.SetActive(false);
                t.font=font;t.GetComponent<MeshRenderer>().sharedMaterial=mat;
            }
            foreach(var station in root.GetComponentsInChildren<CasinoStation>())
            {
                if(station.game==CasinoGame.Roulette)
                {
                    station.wheel.parent.localPosition=new Vector3(-2.3f,0,0);
                    var rail=station.transform.Find("Polished rail");rail.localPosition=new Vector3(-.2f,1.05f,0);rail.localScale=new Vector3(7.4f,.24f,2.65f);
                }
                if(station.game==CasinoGame.Blackjack)
                    foreach(var t in station.GetComponentsInChildren<Transform>())if(t.name=="Playing card")t.gameObject.SetActive(false);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Debug.Log("Casino decorative names hidden. Functional numbers and reel symbols retained.");
    }
}
#endif
