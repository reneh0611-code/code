using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class CitySiteSurvey
    {
        [Serializable] public class Item { public string name,path,asset; public Vector3 center,size,position,right,forward; public float ground; }
        [Serializable] public class Report { public string scene; public bool dirty; public Item[] objects; }
        static CitySiteSurvey() { EditorApplication.delayCall += Once; }
        static void Once()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Once; return; }
            if(SceneManager.GetActiveScene().name!="zzz" || File.Exists("Library/CitySiteSurvey.json")) return;
            Survey();
        }
        [MenuItem("Day Ones/Stadt/Standort analysieren")]
        public static void Survey()
        {
            var scene=SceneManager.GetActiveScene();
            var items=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(false))
                .Where(r=>!(r is ParticleSystemRenderer)).Select(r=>new Item{
                    name=r.name,path=PathOf(r.transform),asset=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject),
                    center=r.bounds.center,size=r.bounds.size,position=r.transform.position,
                    right=r.transform.right,forward=r.transform.forward,ground=Ground(r.bounds.center)
                }).ToArray();
            File.WriteAllText("Library/CitySiteSurvey.json",JsonUtility.ToJson(new Report{scene=scene.path,dirty=scene.isDirty,objects=items},true));
            Debug.Log("[CITY SURVEY] Read-only survey: "+items.Length+" renderers.");
        }
        static string PathOf(Transform t) { return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name; }
        public static float Ground(Vector3 p)
        {
            foreach(var t in Terrain.activeTerrains) {
                var d=t.terrainData; var q=p-t.transform.position;
                if(d!=null&&q.x>=0&&q.z>=0&&q.x<=d.size.x&&q.z<=d.size.z) return t.SampleHeight(p)+t.transform.position.y;
            }
            return float.NaN;
        }
    }
}

