using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        // These objects belong only to the new in-memory catalog model, never the user's scene.
        static void RemoveFresh(Transform t,params string[] names)
        {
            foreach(var child in t.GetComponentsInChildren<Transform>().Where(x=>x!=t&&names.Contains(x.name)).ToArray())
                if(child!=null)Object.DestroyImmediate(child.gameObject);
        }
        static void HistoricTownHall(Transform t,Spec s,Material stone,Material pale,Material metal)
        {
            float front=-s.d*.5f;
            RemoveFresh(t,"Rathaus Eingangsfront","Eingangssaeule","Saeulenkapitell","3D Schrift - RATHAUS","Eingangsvordach","Fassadenband","Turmspitze");
            Cornice(t,s.w,s.d,.6f,stone,2);
            Cornice(t,s.w,s.d,s.h-.18f,pale,3);
            Cornice(t,s.w,s.d,s.h+4.12f,pale,4);
            // Pilasters rhythmically articulate the solid facade without adding more windows.
            foreach(int side in new[]{-1,1})foreach(float x in new[]{3.7f,11.8f,14.4f})
            {
                Box(t,"Historischer Pilaster",new Vector3(side*x,4.65f,front-.22f),new Vector3(.55f,8.2f,.4f),stone,false);
                for(int level=0;level<2;level++)
                {
                    Box(t,"Pilaster Kapitell",new Vector3(side*x,4.45f+level*4.2f,front-.3f),new Vector3(.86f,.25f,.55f),pale,false);
                    Box(t,"Pilaster Basis",new Vector3(side*x,.8f+level*4.2f,front-.3f),new Vector3(.76f,.25f,.55f),pale,false);
                }
            }
            WindowDressings(t,pale,true);
            foreach(int side in new[]{-1,1})
            {
                Pillar(t,new Vector3(side*2.75f,0,front-1.5f),3.65f,.32f,pale);
                Box(t,"Portalpfeiler",new Vector3(side*2.45f,1.4f,front-.5f),new Vector3(.48f,2.8f,.8f),stone);
                WallLantern(t,new Vector3(side*3.35f,2.3f,front-.65f),metal);
            }
            Arch(t,new Vector3(0,2.8f,front-.55f),2.2f,.35f,.65f,pale,20);
            Box(t,"Balkonboden ueber Portal",new Vector3(0,5.4f,front-.85f),new Vector3(7.4f,.32f,2.1f),pale);
            for(float x=-3.3f;x<=3.3f;x+=.38f)
            {
                Box(t,"Balustraden Fuss",new Vector3(x,5.7f,front-1.75f),new Vector3(.22f,.18f,.22f),pale,false);
                // Turned spindle silhouette made with octagonal profiles rather than box bars.
                Profile(t,"Steinbaluster",new Vector3(x,5.75f,front-1.75f),new[]{0f,.12f,.3f,.5f,.7f},new[]{.09f,.13f,.075f,.11f,.09f},8,pale);
            }
            Box(t,"Balustraden Handlauf",new Vector3(0,6.52f,front-1.75f),new Vector3(7.1f,.15f,.32f),pale,false);
            Box(t,"Mittleres Wappenfeld",new Vector3(0,7.75f,front-.25f),new Vector3(4.8f,2.4f,.35f),stone,false);
            CityFacadeDetails.Word(t,"RATHAUS",new Vector3(0,8.4f,front-.5f),.6f,metal);
            Profile(t,"Stadtsiegel",new Vector3(0,7.05f,front-.53f),new[]{0f,.06f},new[]{.56f,.56f},24,pale,Quaternion.Euler(90,0,0));
            // Curved copper cupola instead of a little triangular box roof.
            var tower=t.Find("Zentraler Uhrenturm");
            if(tower!=null)
            {
                var copper=Mat("V4_AgedCopper",new Color(.25f,.38f,.33f));
                Profile(tower,"Historische Turmhaube",new Vector3(0,6.35f,0),new[]{0f,.25f,.65f,1.2f,1.8f,2.3f,2.65f,3.05f,3.5f},new[]{2.75f,2.75f,2.2f,1.9f,1.3f,.75f,.5f,.52f,.18f},12,copper);
                Profile(tower,"Laterne auf Turm",new Vector3(0,9.85f,0),new[]{0f,.8f,.95f,1.65f},new[]{.3f,.3f,.5f,0f},12,metal);
                Box(tower,"Turmspitzen Fahnenmast",new Vector3(0,12,0),new Vector3(.06f,1.2f,.06f),metal,false);
            }
            for(float x=-s.w*.5f+.5f;x<s.w*.5f;x+=.7f)
                Box(t,"Zahnschnitt unter Traufe",new Vector3(x,s.h+3.98f,front-.23f),new Vector3(.22f,.22f,.35f),pale,false);
        }
        static void CasinoPalace(Transform t,Spec s,Material dark)
        {
            float front=-s.d*.5f;
            RemoveFresh(t,"3D Schrift - CASINO","Eingangsvordach","Casino Marquise","Marquise Leuchtpunkt","Fassadenband");
            var gold=Mat("V4_BrushedBrass",new Color(.71f,.49f,.19f));
            if(gold.HasProperty("_Metallic"))gold.SetFloat("_Metallic",.65f);
            gold.SetFloat("_Smoothness",.48f);
            var onyx=Mat("V4_Onyx",new Color(.075f,.085f,.11f));
            var warm=Mat("V4_CasinoWarmLight",new Color(1,.58f,.19f),false,true);
            var red=Mat("V4_WineRed",new Color(.23f,.035f,.065f));
            // Tall central landmark with flanking stepped verticals, all above a clear entry.
            Box(t,"Casino Zentralrisalit",new Vector3(0,8.4f,front-.7f),new Vector3(9,6.2f,1.8f),onyx);
            for(int i=0;i<3;i++)foreach(int side in new[]{-1,1})
            {
                float height=10.8f-i*1.15f;
                Box(t,"Gestaffelter Messingpfeiler",new Vector3(side*(4.9f+i*.55f),height*.5f,front-.8f),new Vector3(.26f,height,.7f),gold);
                Box(t,"Pfeiler Lichtfuge",new Vector3(side*(4.72f+i*.55f),height*.5f,front-1.18f),new Vector3(.04f,height-.35f,.04f),warm,false);
            }
            CityFacadeDetails.Word(t,"CASINO",new Vector3(0,8.45f,front-1.65f),1.1f,gold);
            // Fan-shaped emblem above lettering.
            for(int i=-4;i<=4;i++)
            {
                var ray=Box(t,"Art Deco Strahlenornament",new Vector3(0,10,front-1.64f),new Vector3(.06f,1.15f,.07f),gold,false);
                ray.transform.localRotation=Quaternion.Euler(0,0,i*14);
                ray.transform.localPosition+=ray.transform.up*.46f;
            }
            var canopy=new SurfaceMesh();float cy=3.65f;
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
                Vector3 P(float angle,float y)=>new Vector3(Mathf.Cos(angle)*7,y,front-Mathf.Sin(angle)*3.6f-.5f);
                canopy.Quad(P(a,cy),P(b,cy),P(b,cy+.32f),P(a,cy+.32f));
                canopy.Quad(new Vector3(0,cy+.32f,front-.5f),P(a,cy+.32f),P(b,cy+.32f),new Vector3(0,cy+.32f,front-.5f));
                SphereDetail(t,"Marquise Gluehlampe",P((a+b)*.5f,cy-.05f),Vector3.one*.11f,warm);
            }
            canopy.Save(t,"Geschwungene Casino Marquise",gold);
            foreach(int side in new[]{-1,1})
            {
                Pillar(t,new Vector3(side*6,0,front-1.2f),cy,.22f,gold);
                Box(t,"Schmuckpaneel",new Vector3(side*10.2f,3,front-.18f),new Vector3(4.1f,4.7f,.25f),red,false);
                for(int i=0;i<5;i++)
                {
                    var diamond=Box(t,"Fassaden Raute",new Vector3(side*10.2f,1.4f+i*.7f,front-.35f),new Vector3(.56f,.56f,.035f),gold,false);
                    diamond.transform.localRotation=Quaternion.Euler(0,0,45);
                }
            }
            Cornice(t,s.w,s.d,s.h+.1f,gold,3);
            // Carpet remains inside the doorway, not across the user's pavement.
            Box(t,"Bordeaux Eingangsteppich",new Vector3(0,.055f,front+2),new Vector3(2.6f,.012f,3.8f),red,false);
        }
        static void Profile(Transform t,string name,Vector3 p,float[] heights,float[] radii,int sides,Material material,Quaternion? rotation=null)
        {
            var mesh=new SurfaceMesh();var q=rotation??Quaternion.identity;
            Vector3 P(int level,int edge)=>p+q*new Vector3(Mathf.Cos(edge*2*Mathf.PI/sides)*radii[level],heights[level],Mathf.Sin(edge*2*Mathf.PI/sides)*radii[level]);
            for(int h=0;h<heights.Length-1;h++)for(int i=0;i<sides;i++)mesh.Quad(P(h,i),P(h+1,i),P(h+1,i+1),P(h,i+1));
            for(int i=0;i<sides;i++)
            {
                mesh.Quad(p+q*Vector3.up*heights[0],P(0,i),P(0,i+1),p+q*Vector3.up*heights[0]);
                int top=heights.Length-1;mesh.Quad(p+q*Vector3.up*heights[top],P(top,i+1),P(top,i),p+q*Vector3.up*heights[top]);
            }
            mesh.Save(t,name,material);
        }
    }
}
