using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        sealed class SurfaceMesh
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> indices=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            public void Save(Transform t,string name,Material material)
            {
                if(vertices.Count==0)return;
                var mesh=new Mesh{name="V4 "+name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var g=Group(t,name,Vector3.zero);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        static void ArchitecturalFinish(Transform t,Spec s)
        {
            var stone=Mat("V4_Limestone",new Color(.73f,.67f,.55f));
            var pale=Mat("V4_CarvedStone",new Color(.86f,.8f,.67f));
            var metal=Mat("V4_PatinatedMetal",new Color(.16f,.22f,.23f));
            bool historic=s.style=="townhall"||s.style=="station"||s.style=="bar"||s.style=="cafe"||s.style=="shop_bakery"||s.style=="shop_books";
            if(s.style=="townhall")HistoricTownHall(t,s,stone,pale,metal);
            else if(s.style=="casino")CasinoPalace(t,s,metal);
            else
            {
                Cornice(t,s.w,s.d,s.h+.12f,historic?pale:metal,historic?3:1);
                if(historic)WindowDressings(t,pale,true);
                if(s.style=="warehouse"||s.style=="shop_hardware"||s.style=="shop_electronics")IndustrialServices(t,s,metal,stone);
                else if(s.style!="construction")
                {
                    foreach(int side in new[]{-1,1})
                    {
                        Box(t,"Sockelabschluss Seitenwand",new Vector3(side*(s.w*.5f+.08f),.35f,0),new Vector3(.18f,.7f,s.d),stone,false);
                        WallLantern(t,new Vector3(side*2.1f,2.5f,-s.d*.5f-.32f),metal);
                    }
                }
            }
            if(s.style!="townhall"&&s.style!="casino")IndividualBuildingFinish(t,s,stone,pale,metal);
            if(s.style!="warehouse"&&s.style!="carwash"&&s.style!="construction")Masonry(t,s);
            RoofCourses(t);
        }
        static void Cornice(Transform t,float w,float d,float y,Material mat,int layers)
        {
            for(int k=0;k<layers;k++)
            {
                float e=.08f+k*.07f,cy=y+k*.12f;
                foreach(int side in new[]{-1,1})
                {
                    Box(t,"Profiliertes Gesims",new Vector3(0,cy,side*(d*.5f+e)),new Vector3(w+2*e,.1f,.14f+e),mat,false);
                    Box(t,"Gesims Ruecklauf",new Vector3(side*(w*.5f+e),cy,0),new Vector3(.14f+e,.1f,d+2*e),mat,false);
                }
            }
        }
        static void Masonry(Transform root,Spec s)
        {
            bool brick=s.style=="bar"||s.style=="station"||s.style=="shop_bikes";
            bool panel=s.style=="casino"||s.style=="civic"||s.style=="office"||s.style=="shop_electronics";
            float cw=brick?.48f:panel?1.7f:1.1f,ch=brick?.20f:panel?.85f:.48f,gap=brick?.012f:.015f;
            var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Wandpfeiler"||r.name=="Sturz"||r.name=="Fensterbruestung").ToArray();
            var groups=new Dictionary<Material,SurfaceMesh>();
            foreach(var r in surfaces)
            {
                Transform q=r.transform;Vector3 sz=q.localScale;
                if(sz.z>.5f||sz.x<.2f||sz.y<.15f)continue;
                // Only the exterior face of each actual wall segment: doors/windows remain open.
                for(int row=0;row*ch<sz.y;row++)for(int col=-1;col*cw<sz.x;col++)
                {
                    float offset=panel?0:(row%2)*cw*.5f;
                    float x0=Mathf.Max(0,col*cw+offset)+gap*.5f,x1=Mathf.Min(sz.x,(col+1)*cw+offset)-gap*.5f;
                    float y0=row*ch+gap*.5f,y1=Mathf.Min(sz.y,(row+1)*ch)-gap*.5f;
                    if(x1<=x0||y1<=y0)continue;
                    int variant=(row*13+(col+3)*7)%3;
                    Color c=r.sharedMaterial.color*(.96f+variant*.035f);c.a=1;
                    var mat=Mat("V4_Surface_"+r.sharedMaterial.name+"_"+variant,c);
                    if(!groups.TryGetValue(mat,out var mesh)){mesh=new SurfaceMesh();groups.Add(mat,mesh);}
                    Vector3 A(float x,float y)=>root.InverseTransformPoint(q.TransformPoint(new Vector3(x/sz.x-.5f,y/sz.y-.5f,-.5f-.025f/sz.z)));
                    mesh.Quad(A(x0,y0),A(x0,y1),A(x1,y1),A(x1,y0));
                }
            }
            foreach(var pair in groups)pair.Value.Save(root,"Mauerwerk Relief "+pair.Key.name,pair.Key);
        }
        static void RoofCourses(Transform root)
        {
            var roofs=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&f.sharedMesh.vertexCount==24&&f.sharedMesh.name.Contains("dach",System.StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach(var roof in roofs)
            {
                var b=roof.sharedMesh.bounds;float w=b.size.x,d=b.size.z,rise=b.size.y;if(rise<.5f)continue;
                var mesh=new SurfaceMesh();
                for(float x=-w*.5f;x<w*.5f;x+=.38f)for(float z=-d*.5f;z<d*.5f;z+=.65f)
                {
                    float x0=x+.013f,x1=Mathf.Min(x+.38f,w*.5f)-.013f,z0=z+.012f,z1=Mathf.Min(z+.65f,d*.5f)-.012f;
                    Vector3 P(float xx,float zz)=>root.InverseTransformPoint(roof.transform.TransformPoint(new Vector3(xx,rise*(1-Mathf.Abs(xx)/(w*.5f))+.025f,zz)));
                    mesh.Quad(P(x0,z0),P(x0,z1),P(x1,z1),P(x1,z0));
                }
                var source=roof.GetComponent<Renderer>().sharedMaterial;Color c=source.color*1.14f;c.a=1;
                mesh.Save(root,"Dachdeckung "+roof.name,Mat("V4_Roof_"+source.name,c));
            }
        }
        static void WindowDressings(Transform root,Material stone,bool pediments)
        {
            foreach(var pane in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Durchsichtiges Fenster").ToArray())
            {
                var p=pane.transform;float w=p.localScale.x,h=p.localScale.y;var center=p.localPosition;var parent=p.parent;
                foreach(int side in new[]{-1,1})Box(parent,"Steinerne Fensterlaibung",center+new Vector3(side*(w*.5f+.11f),0,-.13f),new Vector3(.18f,h+.3f,.25f),stone,false);
                Box(parent,"Auskragende Fensterbank",center+new Vector3(0,-h*.5f-.13f,-.18f),new Vector3(w+.55f,.16f,.45f),stone,false);
                Box(parent,"Fenster Verdachung",center+new Vector3(0,h*.5f+.17f,-.16f),new Vector3(w+.5f,.17f,.35f),stone,false);
                if(pediments)Arch(parent,center+new Vector3(0,h*.5f+.22f,-.18f),w*.5f+.19f,.12f,.18f,stone,12);
            }
        }
        static void Arch(Transform t,Vector3 p,float radius,float thick,float depth,Material mat,int segments)
        {
            var mesh=new SurfaceMesh();
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI/segments+.008f,b=(i+1)*Mathf.PI/segments-.008f;
                Vector3 P(float r,float angle,float z)=>p+new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,z);
                mesh.Quad(P(radius,a,-depth/2),P(radius,b,-depth/2),P(radius+thick,b,-depth/2),P(radius+thick,a,-depth/2));
                mesh.Quad(P(radius+thick,a,-depth/2),P(radius+thick,b,-depth/2),P(radius+thick,b,depth/2),P(radius+thick,a,depth/2));
                mesh.Quad(P(radius,a,depth/2),P(radius,b,depth/2),P(radius,b,-depth/2),P(radius,a,-depth/2));
            }
            mesh.Save(t,"Segmentierter Steinbogen",mat);
        }
        static void Pillar(Transform t,Vector3 p,float height,float radius,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Runde Saeule";g.transform.SetParent(t,false);g.transform.localPosition=p+Vector3.up*height*.5f;g.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);g.GetComponent<Renderer>().sharedMaterial=mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var collision=Box(t,"Saeulen Kollision",p+Vector3.up*height*.5f,new Vector3(radius*1.8f,height,radius*1.8f),mat);
            Object.DestroyImmediate(collision.GetComponent<Renderer>());Object.DestroyImmediate(collision.GetComponent<MeshFilter>());
            Box(t,"Saeulenbasis",p+Vector3.up*.13f,new Vector3(radius*2.7f,.26f,radius*2.7f),mat);
            Box(t,"Saeulenkapitell",p+Vector3.up*(height-.08f),new Vector3(radius*2.8f,.2f,radius*2.8f),mat,false);
        }
        static void WallLantern(Transform t,Vector3 p,Material metal)
        {
            var light=Mat("V4_WarmGlass",new Color(1,.72f,.34f),false,true);
            Box(t,"Wandleuchte Arm",p+new Vector3(0,.27f,0),new Vector3(.06f,.07f,.4f),metal,false);
            Box(t,"Laternen Glas",p+new Vector3(0,0,-.19f),new Vector3(.19f,.32f,.19f),light,false);
            foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})Box(t,"Laternen Rahmen",p+new Vector3(a*.11f,0,-.19f+b*.11f),new Vector3(.025f,.4f,.025f),metal,false);
        }
        static void IndustrialServices(Transform t,Spec s,Material metal,Material stone)
        {
            foreach(int side in new[]{-1,1})Box(t,"Hallen Regenrinne",new Vector3(side*(s.w*.5f+.28f),s.h,0),new Vector3(.16f,.14f,s.d+.5f),metal,false);
            for(int i=0;i<2;i++)
            {
                Box(t,"Dach Klimageraet",new Vector3(s.w*.27f,s.h+.65f,s.d*.2f+i*2.4f),new Vector3(1.8f,1.2f,1.6f),stone);
                for(int l=0;l<6;l++)Box(t,"Lueftungslamelle",new Vector3(s.w*.27f,s.h+.22f+l*.16f,s.d*.2f+i*2.4f-.82f),new Vector3(1.6f,.035f,.045f),metal,false);
            }
        }
    }
}
