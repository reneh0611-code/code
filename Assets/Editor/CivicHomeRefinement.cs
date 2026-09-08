using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void RefineResidentialArchitecture(Transform t,int style,float w,int levels,Material wall,Material trim,Material dark,Material roof)
        {
            float h=levels*4,half=w*.5f;
            var brass=VMat("Patinierte_Bronze",new Color(.43f,.34f,.19f));
            var wood=VMat("Thermo_Eiche",new Color(.32f,.22f,.14f));
            var green=VMat("Fensterlaeden",new Color(.19f,.27f,.24f));
            bool historic=style==0||style==1||style==4||style==5;
            var detail=Group(t,"ARCHITEKTUR - Fassadendetails",Vector3.zero);
            if(historic)
            {
                // Replace only this freshly generated roof, never an object from the user's scene.
                var original=t.Find("DACH - Schnitt ausblendbar");
                if(original!=null)Object.DestroyImmediate(original.gameObject);
                foreach(var r in t.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("Dachdeckung ")).ToArray())Object.DestroyImmediate(r.gameObject);
                var rg=Group(t,"DACH - Schnitt ausblendbar",Vector3.zero);
                bool mansard=style==0||style==4;
                ResidentialRoof(rg,w+1,17,h, mansard?3.6f:3,roof,mansard);
                foreach(int side in new[]{-1,1})
                {
                    var face=Group(rg,"Dachgauben",new Vector3(0,0,side*6.4f),side<0?0:180);
                    for(int b=-1;b<=1;b++)
                    {
                        var dormer=Group(face,"Geschlossene Ziergaube",new Vector3(b*w*.29f,h+.65f,0));
                        Box(dormer,"Gaubenkoerper",new Vector3(0,.72f,0),new Vector3(1.8f,1.44f,2.5f),trim,false);
                        VillaWindow(dormer,new Vector3(0,.8f,-1.28f),1.15f,1.1f,dark,true);
                        Pitched(dormer,"Gaubendach",2.15f,2.8f,1.5f,.75f,roof);
                    }
                }
                Cornice(detail,w+.5f,16.5f,h-.2f,trim,3);
                foreach(int side in new[]{-1,1})for(float x=-half+.4f;x<half;x+=.65f)
                    Box(detail,"Konsolfries",new Vector3(x,h-.43f,side*8.23f),new Vector3(.18f,.32f,.32f),trim,false);
                // Real relief follows the existing openings, with no panels closing the glass.
                foreach(var r in t.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Durchsichtiges Fenster").ToArray())
                {
                    var win=r.transform;float ww=win.localScale.x,wh=win.localScale.y;
                    var g=Group(win.parent,"Fensterzier",win.localPosition);
                    if(style==1||style==5)
                    {
                        foreach(int s in new[]{-1,1})
                        {
                            var shutter=Group(g,"Klappladen",new Vector3(s*(ww*.5f+.42f),0,-.19f));
                            Box(shutter,"Ladenrahmen",Vector3.zero,new Vector3(.64f,wh+.2f,.08f),green,false);
                            for(float y=-wh*.5f+.1f;y<wh*.5f;y+=.16f)Box(shutter,"Lamelle",new Vector3(0,y,-.055f),new Vector3(.52f,.06f,.045f),trim,false);
                        }
                    }
                    else
                    {
                        Box(g,"Bruestungsrelief",new Vector3(0,-wh*.5f-.35f,-.19f),new Vector3(ww+.12f,.4f,.12f),trim,false);
                        var crest=Group(g,"Rosette",new Vector3(0,-wh*.5f-.35f,-.28f));
                        Ring(crest,"Steinrosette",Vector3.zero,.12f,.035f,brass);
                    }
                }
            }
            // Four-sided downpipes and gutter brackets give each silhouette a finished edge.
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                var pipe=Group(detail,"Regenfallrohr",new Vector3(sx*(half-.12f),0,sz*8.25f));
                Box(pipe,"Rohr",new Vector3(0,h*.5f,0),new Vector3(.075f,h-.15f,.075f),dark,false);
                for(float y=.4f;y<h;y+=1.5f)Box(pipe,"Rohrschelle",new Vector3(0,y,0),new Vector3(.13f,.055f,.13f),dark,false);
            }
            switch(style)
            {
                case 0:
                    // Corner pilasters, plinths and capitals form a coherent palatial facade.
                    foreach(int s in new[]{-1,1})
                    {
                        Box(detail,"Eckpilaster",new Vector3(s*(half-.4f),h*.5f,-8.25f),new Vector3(.65f,h,.3f),trim,false);
                        Box(detail,"Pilasterkapitell",new Vector3(s*(half-.4f),h-.6f,-8.3f),new Vector3(1,.35f,.45f),trim,false);
                    }
                    for(float x=-4.5f;x<=4.5f;x+=.5f)Profile(detail,"Portikusbaluster",new Vector3(x,7.5f,-10.9f),new[]{0f,.1f,.22f,.5f,.7f,.82f},new[]{.12f,.12f,.08f,.1f,.065f,.12f},8,trim);
                    Box(detail,"Portikus Balustradenabschluss",new Vector3(0,8.4f,-10.9f),new Vector3(9.5f,.14f,.3f),trim,false);
                    break;
                case 1:
                    // A projecting gabled entrance breaks the broad roof line without blocking the doorway.
                    foreach(int s in new[]{-1,1})Pillar(detail,new Vector3(s*1.8f,0,-9.7f),3.25f,.15f,trim);
                    Pitched(Group(detail,"Landhaus Windfang",new Vector3(0,0,-9.3f)),"Windfangdach",4.5f,3.2f,3.4f,1.5f,roof);
                    foreach(int s in new[]{-1,1})for(int j=0;j<5;j++)Box(detail,"Klinker Zierband",new Vector3(s*(half-.4f),.55f+j*1.6f,-8.2f),new Vector3(.7f,.2f,.22f),trim,false);
                    break;
                case 2:
                    // Floating facade frame with recessed glazing, not a fake occupied room.
                    Box(detail,"Auskragender Fassadenrahmen oben",new Vector3(2,h+.35f,-8.85f),new Vector3(w-2,.55f,2),trim);
                    Box(detail,"Auskragender Fassadenrahmen unten",new Vector3(2,4.02f,-8.85f),new Vector3(w-2,.18f,2),trim);
                    Box(detail,"Asymmetrischer Rahmenpfeiler",new Vector3(half+.8f,6,-8.85f),new Vector3(.4f,4.3f,2),trim);
                    for(float z=-6;z<7;z+=.38f)Box(detail,"Seitliche Holzlamellen",new Vector3(-half-.18f,6,z),new Vector3(.18f,3.5f,.08f),wood,false);
                    Box(detail,"Dachrand Schattenfuge",new Vector3(0,h+.05f,-8.35f),new Vector3(w,.08f,.1f),dark,false);
                    break;
                case 3:
                    for(int tier=0;tier<4;tier++)Box(detail,"Art Deco Dachstaffel",new Vector3(0,h+.4f+tier*.35f,0),new Vector3(w-tier*2,.25f,15-tier*2),trim,false);
                    for(int k=0;k<3;k++)foreach(int s in new[]{-1,1})Box(detail,"Bronze Eingangsprofil",new Vector3(s*(1.4f+k*.3f),4,-8.4f),new Vector3(.06f,7.8f,.08f),brass,false);
                    foreach(int s in new[]{-1,1})for(float y=.5f;y<h;y+=.35f)Box(detail,"Horizontale Eckrippe",new Vector3(s*(half-.4f),y,-8.25f),new Vector3(.8f,.08f,.3f),trim,false);
                    break;
                case 4:
                    foreach(int s in new[]{-1,1})
                    {
                        var g=Group(detail,"Historischer Zwerchgiebel",new Vector3(s*w*.27f,0,-7.1f));
                        Pitched(g,"Ziergiebel",4.8f,2.8f,h-.15f,2.3f,trim);
                        Arch(g,new Vector3(0,h+.45f,-1.48f),.48f,.10f,.15f,dark,20);
                    }
                    for(int f=1;f<levels;f++)Cornice(detail,w+.45f,16.45f,f*4-.2f,trim,3);
                    break;
                case 5:
                    for(int s=-1;s<=1;s+=2)Pillar(detail,new Vector3(s*1.55f,0,-9.1f),3.2f,.12f,wood);
                    Pitched(Group(detail,"Haustuer Satteldach",new Vector3(0,0,-8.8f)),"Eingangsdach",3.7f,2.3f,3.3f,.8f,roof);
                    break;
                case 6:
                    foreach(int s in new[]{-1,1})for(int j=0;j<5;j++)Box(detail,"Vertikale Fassadenlatte",new Vector3(s*(half-.7f+j*.12f),4,-8.2f),new Vector3(.05f,7.7f,.10f),wood,false);
                    Box(detail,"Seitlich auskragendes Dach",new Vector3(-1,h+.25f,0),new Vector3(w+2,.22f,17.3f),trim);
                    break;
                case 7:
                    // A low asymmetric roof rather than another two-storey cube.
                    var bg=t.Find("DACH - Schnitt ausblendbar");if(bg!=null)Object.DestroyImmediate(bg.gameObject);
                    var bRoof=Group(t,"DACH - Schnitt ausblendbar",Vector3.zero);
                    ResidentialRoof(bRoof,w+1.7f,17.7f,4,.95f,roof,false);
                    for(float x=-half+.5f;x<half;x+=.55f)Box(detail,"Sichtbare Sparrenkoepfe",new Vector3(x,3.9f,-8.55f),new Vector3(.12f,.18f,.9f),wood,false);
                    break;
            }
        }
        static void ResidentialRoof(Transform t,float w,float d,float y,float rise,Material mat,bool mansard)
        {
            // Closed, rectangular ring roof; the structural ceiling is retained above the last storey.
            float[] heights=mansard?new[]{y,y+rise*.73f,y+rise}:new[]{y,y+rise};
            float[] widths=mansard?new[]{w,w-3,w-5}:new[]{w,Mathf.Max(1,w-8)};
            float[] depths=mansard?new[]{d,d-3,d-5}:new[]{d,1f};
            var mesh=new SurfaceMesh();
            for(int r=0;r<heights.Length-1;r++)
            {
                Vector3[] a={new Vector3(-widths[r]/2,heights[r],-depths[r]/2),new Vector3(widths[r]/2,heights[r],-depths[r]/2),new Vector3(widths[r]/2,heights[r],depths[r]/2),new Vector3(-widths[r]/2,heights[r],depths[r]/2)};
                Vector3[] b={new Vector3(-widths[r+1]/2,heights[r+1],-depths[r+1]/2),new Vector3(widths[r+1]/2,heights[r+1],-depths[r+1]/2),new Vector3(widths[r+1]/2,heights[r+1],depths[r+1]/2),new Vector3(-widths[r+1]/2,heights[r+1],depths[r+1]/2)};
                for(int s=0;s<4;s++)mesh.Quad(a[s],b[s],b[(s+1)%4],a[(s+1)%4]);
            }
            int last=heights.Length-1;float xx=widths[last]/2,zz=depths[last]/2,yy=heights[last];mesh.Quad(new Vector3(-xx,yy,-zz),new Vector3(-xx,yy,zz),new Vector3(xx,yy,zz),new Vector3(xx,yy,-zz));
            mesh.Save(t,"Profiliertes Dach",mat);
            Box(t,"Geschlossene Dachdecke",new Vector3(0,y-.01f,0),new Vector3(w,.12f,d),mat);
        }
    }
}
