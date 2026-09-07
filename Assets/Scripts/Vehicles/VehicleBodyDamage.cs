using System.Collections.Generic;
using UnityEngine;

namespace CheatOnYourDayOnes.Vehicles
{
    // Owns per-car mesh copies. Imported assets and collision geometry never deform.
    public sealed class VehicleBodyDamage : MonoBehaviour
    {
        private sealed class Panel
        {
            public MeshFilter filter;
            public Mesh original, mesh;
            public Vector3[] rest, vertices;
            public int[] triangles;
        }
        private readonly List<Panel> panels = new();
        private readonly Queue<GameObject> marks = new();
        private Material scrapeMaterial;
        public int Revision { get; private set; }
        public int DeformedVertexCount { get; private set; }

        public void Initialize(IEnumerable<Renderer> wheels)
        {
            if(panels.Count>0)return;
            var excluded=new HashSet<Renderer>(wheels);
            foreach(MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer=filter.GetComponent<Renderer>();
                Mesh source=filter.sharedMesh;
                if(renderer==null||excluded.Contains(renderer)||source==null||!source.isReadable)continue;
                if(source.vertexCount>100000)continue;
                Mesh instance=Instantiate(source);
                instance.name=source.name+" (vehicle damage instance)";
                instance.MarkDynamic();
                panels.Add(new Panel{filter=filter,original=source,mesh=instance,rest=source.vertices,vertices=source.vertices,triangles=source.triangles});
                filter.sharedMesh=instance;
            }
        }

        public void ApplyImpact(Vector3 point,Vector3 inward,float severity)
        {
            if(severity<1f||inward.sqrMagnitude<.001f)return;
            inward.Normalize();
            float radius=Mathf.Lerp(.45f,1.1f,Mathf.Clamp01(severity/65f));
            float depth=Mathf.Lerp(.008f,.22f,Mathf.Clamp01(severity/75f));
            DeformedVertexCount=0;
            foreach(Panel panel in panels)
            {
                if(panel.filter==null)continue;
                Transform frame=panel.filter.transform;
                bool changed=false;
                for(int i=0;i<panel.vertices.Length;i++)
                {
                    Vector3 restWorld=frame.TransformPoint(panel.rest[i]);
                    float distance=Vector3.Distance(restWorld,point);
                    if(distance>=radius)continue;
                    float weight=1f-distance/radius;weight=weight*weight*(3f-2f*weight);
                    Vector3 current=frame.TransformPoint(panel.vertices[i]);
                    Vector3 displaced=current+inward*(depth*weight);
                    displaced=restWorld+Vector3.ClampMagnitude(displaced-restWorld,.30f);
                    panel.vertices[i]=frame.InverseTransformPoint(displaced);
                    changed=true;DeformedVertexCount++;
                }
                if(changed){panel.mesh.vertices=panel.vertices;panel.mesh.RecalculateNormals();panel.mesh.RecalculateBounds();}
            }
            AddScrape(point,radius);
            Revision++;
        }

        private void AddScrape(Vector3 point,float radius)
        {
            // A thin patch follows a real deformed surface triangle, rather than
            // placing a floating box at the physics collider contact point.
            Panel nearest=null;int triangle=-1;float best=radius*radius;
            foreach(Panel panel in panels)
            {
                if(panel.filter==null)continue;
                for(int i=0;i<panel.triangles.Length;i+=3)
                {
                    Vector3 triangleCenter=(panel.vertices[panel.triangles[i]]+panel.vertices[panel.triangles[i+1]]+panel.vertices[panel.triangles[i+2]])/3f;
                    float distance=(panel.filter.transform.TransformPoint(triangleCenter)-point).sqrMagnitude;
                    if(distance<best){best=distance;nearest=panel;triangle=i;}
                }
            }
            if(nearest==null)return;
            if(scrapeMaterial==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
                if(shader==null)return;
                scrapeMaterial=new Material(shader){name="Exposed vehicle metal"};
                if(scrapeMaterial.HasProperty("_BaseColor"))scrapeMaterial.SetColor("_BaseColor",new Color(.16f,.17f,.18f));
                if(scrapeMaterial.HasProperty("_Color"))scrapeMaterial.SetColor("_Color",new Color(.16f,.17f,.18f));
                if(scrapeMaterial.HasProperty("_Metallic"))scrapeMaterial.SetFloat("_Metallic",.65f);
                if(scrapeMaterial.HasProperty("_Smoothness"))scrapeMaterial.SetFloat("_Smoothness",.23f);
            }
            Transform t=nearest.filter.transform;
            Vector3 a=t.TransformPoint(nearest.vertices[nearest.triangles[triangle]]);
            Vector3 b=t.TransformPoint(nearest.vertices[nearest.triangles[triangle+1]]);
            Vector3 c=t.TransformPoint(nearest.vertices[nearest.triangles[triangle+2]]);
            Vector3 center=(a+b+c)/3f,normal=Vector3.Cross(b-a,c-a).normalized;
            GameObject mark=new GameObject("Body scrape",typeof(MeshFilter),typeof(MeshRenderer));
            mark.transform.SetParent(transform,false);
            Vector3 Patch(Vector3 p)=>transform.InverseTransformPoint(center+Vector3.ClampMagnitude((p-center)*.42f,.22f)+normal*.003f);
            Mesh mesh=new Mesh{name="Contact scrape"};
            mesh.vertices=new[]{Patch(a),Patch(b),Patch(c)};
            mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
            mark.GetComponent<MeshFilter>().sharedMesh=mesh;
            mark.GetComponent<MeshRenderer>().sharedMaterial=scrapeMaterial;
            marks.Enqueue(mark);
            if(marks.Count>12)ReleaseMark(marks.Dequeue());
        }

        private void ReleaseMark(GameObject mark)
        {
            if(mark==null)return;
            Destroy(mark.GetComponent<MeshFilter>().sharedMesh);Destroy(mark);
        }
        private void OnDestroy()
        {
            foreach(Panel panel in panels)
            {
                if(panel.filter!=null&&panel.filter.sharedMesh==panel.mesh)panel.filter.sharedMesh=panel.original;
                if(panel.mesh!=null)Destroy(panel.mesh);
            }
            while(marks.Count>0)ReleaseMark(marks.Dequeue());
            if(scrapeMaterial!=null)Destroy(scrapeMaterial);
        }
    }
}
