using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class CityFacadeDetails
    {
        const string Folder="Assets/Generated/NeubeckumCity";
        static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>{
            {'A',"0,0 .32,1 .64,0|.14,.4 .5,.4"},{'B',"0,0 0,1 .38,1 .60,.88 .60,.67 .38,.52 0,.52|.38,.52 .62,.4 .62,.15 .4,0 0,0"},
            {'C',".62,.88 .48,1 .16,1 0,.82 0,.18 .16,0 .48,0 .62,.12"},
            {'E',".62,1 0,1 0,0 .62,0|0,.5 .48,.5"},{'F',"0,0 0,1 .62,1|0,.52 .5,.52"},
            {'H',"0,0 0,1|.62,0 .62,1|0,.5 .62,.5"},{'I',".12,1 .5,1|.31,1 .31,0|.12,0 .5,0"},
            {'K',"0,0 0,1|.6,1 0,.45 .64,0"},{'L',"0,1 0,0 .62,0"},
            {'N',"0,0 0,1 .62,0 .62,1"},{'O',".18,0 .44,0 .62,.18 .62,.82 .44,1 .18,1 0,.82 0,.18 .18,0"},
            {'P',"0,0 0,1 .4,1 .62,.82 .62,.65 .4,.5 0,.5"},
            {'R',"0,0 0,1 .4,1 .62,.82 .62,.65 .4,.5 0,.5|.30,.5 .64,0"},
            {'S',".62,.88 .46,1 .16,1 0,.84 0,.65 .16,.52 .46,.48 .62,.34 .62,.16 .46,0 .16,0 0,.12"},
            {'T',"0,1 .64,1|.32,1 .32,0"},{'U',"0,1 0,.18 .18,0 .44,0 .62,.18 .62,1"},
            {'Z',"0,1 .62,1 0,0 .62,0"}
        };
        static CityFacadeDetails()
        {
            Glyphs['D']="0,0 0,1 .35,1 .62,.78 .62,.22 .35,0 0,0";
            Glyphs['G']=".62,.88 .48,1 .16,1 0,.82 0,.18 .16,0 .48,0 .62,.18 .62,.48 .35,.48";
            Glyphs['J']=".12,1 .62,1 .62,.18 .44,0 .18,0 0,.18";
            Glyphs['M']="0,0 0,1 .31,.52 .62,1 .62,0";
            Glyphs['V']="0,1 .31,0 .62,1";
            Glyphs['W']="0,1 .12,0 .31,.48 .5,0 .62,1";
        }
        public static GameObject Word(Transform parent,string word,Vector3 position,float height,Material material)
        {
            var g=new GameObject("3D Schrift - "+word);g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=Vector3.one*height;
            g.AddComponent<MeshFilter>().sharedMesh=LetterMesh(word);g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
        }
        public static void Apply(Transform plot,int index,float width,float depth,Material white,Material metal,Material wall,Material green)
        {
            if(plot.Find("FASSADEN UND GARTEN")!=null)return;
            var group=new GameObject("FASSADEN UND GARTEN");group.transform.SetParent(plot,false);
            Undo.RegisterCreatedObjectUndo(group,"Fassade und Garten");
            Transform old=plot.Find("Beschriftung");
            if(old!=null){Undo.RecordObject(old.gameObject,"Alte flache Beschriftung ausblenden");old.gameObject.SetActive(false);}
            string word=index==17?"AUTOHOF":index==12?"AUTOHAUS":index==11?"POLIZEI":index==10?"RESTAURANT":index==9?"BISTRO":index==8?"BACKSTUBE":null;
            if(word!=null)
            {
                Vector3 position=new Vector3(0,index==12||index==17?3.25f:2.96f,-depth*.5f-.55f);
                if(index==17)
                {
                    var shell=plot.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.name=="Plaster");
                    if(shell!=null){var b=shell.sharedMesh.bounds;position=plot.InverseTransformPoint(shell.transform.TransformPoint(new Vector3(b.center.x,b.min.y,b.min.z)));position.y+=3.25f;position.z-=.4f;}
                }
                var letters=new GameObject("3D Einzelbuchstaben - "+word);letters.transform.SetParent(group.transform,false);
                letters.transform.localPosition=position;letters.transform.localScale=Vector3.one*.36f;
                letters.AddComponent<MeshFilter>().sharedMesh=LetterMesh(word);letters.AddComponent<MeshRenderer>().sharedMaterial=white;
            }
            bool residential=index==2||index==4||index==6||index==7||index>=13&&index<=16;
            if(!residential)return;
            float x=width*.5f+1.5f,front=-depth*.5f-3.7f,rear=depth*.5f+4.3f;
            // All details remain inside the existing reserved plot envelope.
            Cube(group.transform,"Garten hinten",new Vector3(0,.04f,depth*.5f+2.2f),new Vector3(width+2,.08f,3.8f),green,false);
            Cube(group.transform,"Gartenmauer hinten",new Vector3(0,.33f,rear),new Vector3(x*2,.66f,.22f),wall,true);
            for(int side=-1;side<=1;side+=2)
            {
                Cube(group.transform,"Seitliche Gartenmauer",new Vector3(side*x,.18f,(front+rear)*.5f),new Vector3(.2f,.36f,rear-front),wall,true);
                Cube(group.transform,"Zaun-Handlauf",new Vector3(side*x,1.12f,(front+rear)*.5f),new Vector3(.08f,.08f,rear-front),metal,false);
                for(float z=front;z<rear;z+=.55f)
                    Cube(group.transform,"Zaunstab",new Vector3(side*x,.73f,z),new Vector3(.05f,.76f,.05f),metal,false);
                Cube(group.transform,"Zaun-Kollision",new Vector3(side*x,.72f,(front+rear)*.5f),new Vector3(.12f,.8f,rear-front),null,true);
                float length=x-1.7f;
                Cube(group.transform,"Vorgartenmauer - Zugang offen",new Vector3(side*(1.7f+length*.5f),.4f,front),new Vector3(length,.8f,.25f),wall,true);
                Cube(group.transform,"Torpfosten",new Vector3(side*1.7f,.6f,front),new Vector3(.3f,1.2f,.3f),wall,true);
            }
            Cube(group.transform,"Terrasse",new Vector3(0,.08f,depth*.5f+1.5f),new Vector3(3,.16f,2.5f),wall,true);
        }
        static void Cube(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collision)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
            if(mat==null)Object.DestroyImmediate(g.GetComponent<Renderer>());else g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static Mesh LetterMesh(string word)
        {
            string dir=Folder+"/Beschriftungen 3D";
            if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.CreateFolder(Folder,"Beschriftungen 3D");
            string path=dir+"/"+word+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int c=0;c<word.Length;c++)
            {
                if(word[c]==' ')continue;
                if(!Glyphs.TryGetValue(word[c],out string glyph))throw new InvalidOperationException("Unbekannter Buchstabe "+word[c]);
                float offset=c*.82f-(word.Length*.82f-.18f)*.5f;
                foreach(var stroke in glyph.Split('|'))
                {
                    var points=stroke.Split(' ').Select(p=>p.Split(',')).Select(p=>new Vector2(float.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture)+offset,float.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture)-.5f)).ToArray();
                    for(int i=1;i<points.Length;i++)AddBar(vertices,triangles,points[i-1],points[i]);
                }
            }
            var mesh=new Mesh{name=word+" - massive Buchstaben"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void AddBar(List<Vector3> vertices,List<int> triangles,Vector2 a,Vector2 b)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*.055f;
            Vector2[] p={a-n,b-n,b+n,a+n};
            // Separate vertices per face: crisp front face and solid side walls.
            Vector3[] corners=p.Select(v=>new Vector3(v.x,v.y,-.16f)).Concat(p.Select(v=>new Vector3(v.x,v.y,0))).ToArray();
            int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{1,2,6,5},new[]{2,3,7,6},new[]{3,0,4,7}};
            foreach(var face in faces){int start=vertices.Count;foreach(int i in face)vertices.Add(corners[i]);triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
        }
    }
}
