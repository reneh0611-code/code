using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        // Architecture is deliberately separated from interior/gameplay fixtures.
        // Upper storeys are exterior volumes; the ground-floor route stays open.
        static void BuildArchitecturalShell(Transform t,Spec s,float door,Material wall,Material trim,Material glass,Material dark)
        {
            if(s.style=="warehouse")
            {
                // Industrial portal opening, no domestic waist-height windows.
                float opening=6,head=5.4f;
                foreach(int side in new[]{-1,1})
                    Box(t,"Hallenfront Sandwichpaneel",new Vector3(side*(s.w+opening)*.25f,s.h*.5f,-s.d*.5f),new Vector3((s.w-opening)*.5f,s.h,.28f),wall);
                Box(t,"Torsturz",new Vector3(0,(s.h+head)*.5f,-s.d*.5f),new Vector3(opening,s.h-head,.3f),wall);
                Box(t,"Hallenrueckwand",new Vector3(0,s.h*.5f,s.d*.5f),new Vector3(s.w,s.h,.28f),wall);
                foreach(int side in new[]{-1,1})
                {
                    var f=Group(t,"Hallenlaengswand",new Vector3(side*s.w*.5f,0,0));
                    Box(f,"Sockelbeton",new Vector3(0,.65f,0),new Vector3(.32f,1.3f,s.d),trim);
                    Box(f,"Blechwand",new Vector3(0,3.1f,0),new Vector3(.25f,3.6f,s.d),wall);
                    // Mostly closed industrial elevations, just three short clerestory panes.
                    for(int bay=0;bay<3;bay++)
                    {
                        var strip=Group(f,"Oberlicht Abschnitt",new Vector3(0,4.9f,-s.d/3+bay*s.d/3),side*90);
                        Facade(strip,s.d/3,1.2f,0,wall,trim,glass,dark,false,false,1,2.4f,.3f,.9f);
                    }
                    Box(f,"Traufwand",new Vector3(0,(6.1f+s.h)*.5f,0),new Vector3(.25f,s.h-6.1f,s.d),wall);
                    for(float z=-s.d*.5f;z<=s.d*.5f;z+=1.1f)
                        Box(f,"Vertikale Trapezblechfalte",new Vector3(side*.17f,3.1f,z),new Vector3(.06f,3.6f,.055f),trim,false);
                    for(float z=-s.d*.5f+.2f;z<s.d*.5f;z+=5)
                        Box(f,"Stahlportalstuetze",new Vector3(-side*.22f,s.h*.5f,z),new Vector3(.22f,s.h,.28f),dark);
                }
                Pitched(t,"Hallen Satteldach",s.w+.8f,s.d+.8f,s.h,2.8f,dark);
                for(float z=-s.d*.4f;z<s.d*.5f;z+=6)
                    Box(t,"First Entluefter",new Vector3(0,s.h+2.95f,z),new Vector3(1.1f,.35f,2.4f),trim,false);
                return;
            }
            bool wash=s.style=="carwash",police=s.style=="police";
            bool shop=s.style.StartsWith("shop_",System.StringComparison.Ordinal);
            bool curtain=s.style=="civic"; // Only the bank uses a full curtain wall.
            if(curtain)CurtainFront(Group(t,"Glasfront",new Vector3(0,0,-s.d*.5f)),s.w,s.h,door,glass,dark);
            else Facade(Group(t,"Front",new Vector3(0,0,-s.d*.5f)),s.w,s.h,door,wall,trim,glass,dark,false,wash,
                s.style=="casino"?0:shop&&s.w<16?1:2,shop?Mathf.Min(3.4f,s.w*.25f):1.6f,shop?.4f:.95f,shop?2.85f:2.65f);
            Facade(Group(t,"Rueckwand",new Vector3(0,0,s.d*.5f),180),s.w,s.h,wash?door:police?1.8f:0,wall,trim,glass,dark,police,wash,police?1:0);
            foreach(int side in new[]{-1,1})
                Facade(Group(t,"Seitenwand",new Vector3(side*s.w*.5f,0,0),side*90),s.d,s.h,0,wall,trim,glass,dark,police,false,
                    !shop&&side==1&&s.style!="casino"&&s.style!="carwash"?1:0,1.1f,1.25f,2.6f);
            bool pitched=s.style=="townhall"||s.style=="station"||s.style=="bar"||s.style=="cafe";
            if(!pitched)Box(t,"Flachdach",new Vector3(0,s.h+.14f,0),new Vector3(s.w+.6f,.28f,s.d+.6f),dark);
            if(s.style=="bar"||s.style=="cafe")Pitched(t,"Steiles Ziegeldach",s.w+.6f,s.d+.6f,s.h,s.style=="bar"?3.6f:2.6f,Mat("TerracottaRoof",new Color(.32f,.16f,.12f)));
            if(s.style=="station")Pitched(t,"Bahnhof Langdach",s.w+1,s.d+1,s.h,3.6f,Mat("StationSlate",new Color(.2f,.25f,.28f)));
        }

        static void CurtainFront(Transform t,float width,float height,float door,Material glass,Material metal)
        {
            foreach(int side in new[]{-1,1})
            {
                float span=(width-door)*.5f,center=side*(width+door)*.25f;
                Box(t,"Bodentiefe Verglasung",new Vector3(center,height*.5f,0),new Vector3(span,height,.035f),glass);
                for(float x=door*.5f;x<=width*.5f+.01f;x+=1.5f)
                    Box(t,"Schlanker Glasfassadenpfosten",new Vector3(side*x,height*.5f,-.05f),new Vector3(.07f,height,.13f),metal,false);
            }
            Box(t,"Glas ueber Eingang",new Vector3(0,(height+3)*.5f,0),new Vector3(door,height-3,.035f),glass);
            Box(t,"Fassadenriegel",new Vector3(0,3,-.04f),new Vector3(width,.08f,.14f),metal,false);
        }

        static void Upper(Transform t,string name,Vector3 pos,float w,float d,float h,Material wall,Material trim,Material glass,Material dark)
        {
            var g=Group(t,name,pos);
            foreach(int side in new[]{-1,1})
            {
                Facade(Group(g,"Obergeschoss Laengsfassade",new Vector3(0,0,side*d*.5f),side<0?0:180),w,h,0,wall,trim,glass,dark,false,false,side<0?2:1,1.3f,1.05f,2.6f);
                Facade(Group(g,"Obergeschoss Stirnwand",new Vector3(side*w*.5f,0,0),side*90),d,h,0,wall,trim,glass,dark,false,false,0);
            }
            Box(g,"Geschossdecke",new Vector3(0,.06f,0),new Vector3(w,.12f,d),trim);
            Box(g,"Dachkante",new Vector3(0,h+.12f,0),new Vector3(w+.3f,.24f,d+.3f),dark);
        }

        // Closed triangular prism, including both gables. No open roof end faces.
        static void Pitched(Transform t,string name,float w,float d,float y,float rise,Material material)
        {
            var g=Group(t,name,new Vector3(0,y,0));
            Vector3[] p={new Vector3(-w/2,0,-d/2),new Vector3(w/2,0,-d/2),new Vector3(0,rise,-d/2),new Vector3(-w/2,0,d/2),new Vector3(w/2,0,d/2),new Vector3(0,rise,d/2)};
            int[] order={0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3};
            var vertices=new Vector3[order.Length];var indices=new int[order.Length];var uv=new Vector2[order.Length];
            for(int i=0;i<order.Length;i++){vertices[i]=p[order[i]];indices[i]=i;uv[i]=new Vector2(vertices[i].x+vertices[i].y,vertices[i].z)*.25f;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=indices;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
            g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            // Ground-floor ceiling provides physical roof separation without a saved transient mesh collider.
            Box(g,"Geschlossene Decke",new Vector3(0,.02f,0),new Vector3(w,.12f,d),material);
        }

        static void AddIdentity(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark,Material gold)
        {
            switch(s.style)
            {
                case "civic":
                    Upper(t,"Schwebender Bank Oberbau",new Vector3(-3,s.h+.28f,1),s.w-6,s.d-3,4,trim,trim,glass,dark);
                    for(float x=-s.w*.5f+.6f;x<3;x+=.65f)
                        Box(t,"Bronzene Sonnenschutzlamelle",new Vector3(x,s.h+2.3f,-s.d*.5f+2.4f),new Vector3(.12f,3.7f,.7f),gold,false);
                    Box(t,"Asymmetrisches Eingangsportal",new Vector3(5.2f,2.8f,-s.d*.5f-.55f),new Vector3(.6f,5.6f,1.1f),dark);
                    Box(t,"Auskragendes Portalband",new Vector3(0,5.55f,-s.d*.5f-.55f),new Vector3(11,.32f,1.1f),dark);
                    for(int i=0;i<2;i++){Box(t,"Geldautomat",new Vector3(-7+i*1.5f,1,-s.d*.5f-.4f),new Vector3(.9f,2,.6f),dark);Box(t,"ATM Bildschirm",new Vector3(-7+i*1.5f,1.4f,-s.d*.5f-.72f),new Vector3(.6f,.4f,.025f),Mat("ATMLight",new Color(.2f,.65f,.7f),false,true),false);}
                    break;
                case "townhall":
                    Upper(t,"Rathaus Beletage",new Vector3(0,s.h,0),s.w,s.d,4.2f,wall,trim,glass,dark);
                    Pitched(t,"Rathaus Schieferdach",s.w+.8f,s.d+.8f,s.h+4.5f,4,dark);
                    var tower=Group(t,"Zentraler Uhrenturm",new Vector3(0,s.h+4.5f,-2));
                    Box(tower,"Turmschaft",new Vector3(0,3.2f,0),new Vector3(4.6f,6.4f,4.6f),trim);
                    Pitched(tower,"Turmspitze",5.2f,5.2f,6.4f,3.8f,dark);
                    var dial=GameObject.CreatePrimitive(PrimitiveType.Cylinder);dial.name="Runde Turmuhr";dial.transform.SetParent(tower,false);dial.transform.localPosition=new Vector3(0,4.7f,-2.34f);dial.transform.localRotation=Quaternion.Euler(90,0,0);dial.transform.localScale=new Vector3(2.5f,.045f,2.5f);dial.GetComponent<Renderer>().sharedMaterial=gold;Object.DestroyImmediate(dial.GetComponent<Collider>());
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var tick=Box(tower,"Stundenindex",new Vector3(Mathf.Sin(a)*1.02f,4.7f+Mathf.Cos(a)*1.02f,-2.4f),new Vector3(.07f,.18f,.04f),dark,false);tick.transform.localRotation=Quaternion.Euler(0,0,-i*30);}
                    Box(tower,"Minutenzeiger",new Vector3(0,5.08f,-2.44f),new Vector3(.08f,.8f,.04f),dark,false);
                    Box(tower,"Stundenzeiger",new Vector3(.27f,4.7f,-2.44f),new Vector3(.6f,.09f,.04f),dark,false);
                    CivicFront(t,new Spec(s.id,s.label,s.sign,"portico",s.w,s.d,s.h,s.color,s.type),trim,gold);
                    Pitched(Group(t,"Rathaus Eingangsfront",new Vector3(0,0,-s.d*.5f-1)),"Klassischer Dreiecksgiebel",10,2.2f,4,2,trim);
                    foreach(int side in new[]{-1,1})for(int level=0;level<9;level++)Box(t,"Bossierte Ecksteine",new Vector3(side*(s.w*.5f-.25f),.5f+level,-s.d*.5f-.18f),new Vector3(.65f,.55f,.35f),trim,false);
                    break;
                case "office":
                    int floors=s.type==CheatOnYourDayOnes.World.CityBuildingType.Office?3:1;
                    for(int i=0;i<floors;i++)Upper(t,"Verwaltungs Obergeschoss "+i,new Vector3(i==floors-1?1.2f:0,s.h+i*3.6f,0),i==floors-1?s.w-2.4f:s.w,s.d,3.6f,wall,trim,glass,dark);
                    Box(t,"Vertikaler Treppenhaus Akzent",new Vector3(s.w*.34f,(s.h+floors*3.6f)*.5f,-s.d*.5f-.25f),new Vector3(1,s.h+floors*3.6f,.6f),Mat(s.id+"Accent",s.type==CheatOnYourDayOnes.World.CityBuildingType.Office?new Color(.25f,.32f,.34f):new Color(.5f,.24f,.12f)),false);
                    break;
                case "warehouse":
                    for(int i=0;i<(s.w>30?4:2);i++)
                    {
                        float z=-s.d*.3f+i*5.5f;
                        Box(t,"Geschlossenes Sektionaltor",new Vector3(s.w*.5f+.2f,2.4f,z),new Vector3(.12f,4.8f,3.9f),dark);
                        for(int r=0;r<8;r++)Box(t,"Torsegment",new Vector3(s.w*.5f+.28f,.4f+r*.58f,z),new Vector3(.04f,.055f,3.7f),trim,false);
                        foreach(int sign in new[]{-1,1})Box(t,"Gelber Rammschutz",new Vector3(s.w*.5f+.7f,.6f,z+sign*2.2f),new Vector3(.2f,1.2f,.2f),Mat("SafetyYellow",new Color(.95f,.65f,.06f)));
                    }
                    break;
                case "casino":
                    for(int i=0;i<3;i++)Box(t,"Art Deco Dachstaffel",new Vector3(0,s.h+.5f+i*.8f,1),new Vector3(s.w-5-i*5,.8f,s.d-4-i*3),i==1?gold:dark);
                    foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)Box(t,"Art Deco Pylon",new Vector3(side*(s.w*.5f-1-i*.6f),s.h*.5f+.8f,-s.d*.5f-.32f),new Vector3(.28f,s.h+1.6f-i*.4f,.5f),gold);
                    break;
                case "police":
                    Upper(t,"Polizei Einsatzleitung",new Vector3(-3,s.h+.28f,1),12,11,3.8f,trim,trim,glass,dark);
                    Box(t,"Blauer Treppenhausturm",new Vector3(-7,4.6f,4),new Vector3(3,9.2f,3),Mat("PoliceTower",new Color(.04f,.15f,.25f)));
                    Box(t,"Funkmast",new Vector3(-7,11.4f,4),new Vector3(.09f,4.4f,.09f),dark,false);
                    break;
                case "motel":
                    Upper(t,"Motel Zimmergeschoss",new Vector3(0,s.h+.3f,0),s.w,s.d,3.5f,wall,trim,glass,dark);
                    Box(t,"Laubengang",new Vector3(0,s.h+.2f,-s.d*.5f-.9f),new Vector3(s.w, .2f,1.8f),trim);
                    for(float x=-s.w*.5f;x<=s.w*.5f;x+=1.6f)Box(t,"Laubengang Gelanderstab",new Vector3(x,s.h+.8f,-s.d*.5f-1.7f),new Vector3(.06f,1.2f,.06f),dark,false);
                    Box(t,"Laubengang Handlauf",new Vector3(0,s.h+1.4f,-s.d*.5f-1.7f),new Vector3(s.w,.07f,.08f),dark,false);
                    break;
                case "fastfood":
                    Box(t,"Rotes schwebendes Diner Dach",new Vector3(0,s.h+.4f,0),new Vector3(s.w+2,.35f,s.d+2),Mat("DinerRed",new Color(.65f,.08f,.035f)));
                    var fin=Box(t,"Schraeges Markenzeichen",new Vector3(s.w*.36f,s.h+1,-s.d*.5f),new Vector3(1.3f,3.8f,.8f),trim);fin.transform.localRotation=Quaternion.Euler(0,0,-15);
                    break;
                case "carwash":
                    foreach(int side in new[]{-1,1})Box(t,"Waschtunnel Dachkante",new Vector3(side*3.1f,s.h+.4f,0),new Vector3(.4f,.6f,s.d+1),Mat("WashCyan",new Color(.05f,.52f,.65f)));
                    break;
                case "station":
                    Upper(t,"Bahnhof Mittelrisalit",new Vector3(0,s.h,0),8,9,3.6f,wall,trim,glass,dark);
                    Pitched(t,"Bahnhof Mittelgiebel",9,10,s.h+3.9f,2.8f,dark);
                    break;
                case "icecream":
                    Box(t,"Pastell Pavillon Dach",new Vector3(0,s.h+.25f,0),new Vector3(s.w+1,.25f,s.d+1),Mat("MintRoof",new Color(.4f,.65f,.55f)));
                    for(int i=0;i<10;i++)Box(t,"Gestreifte Eisdielenmarkise",new Vector3(-4.5f+i,3.3f,-s.d*.5f-.7f),new Vector3(.48f,.05f,1.4f),trim,false);
                    break;
                case "laundry":
                    for(int i=0;i<3;i++)Box(t,"Dach Abluft",new Vector3(-3+i*3,s.h+.6f,2),new Vector3(.5f,1.1f,.5f),dark,false);
                    foreach(int side in new[]{-1,1})Box(t,"Waschsalon Keramiksockel",new Vector3(side*(s.w+3.2f)*.25f,.35f,-s.d*.5f-.17f),new Vector3((s.w-3.2f)*.5f,.6f,.05f),Mat("LaundryTeal",new Color(.12f,.38f,.4f)),false);
                    // Split the trim at the entrance rather than placing a physical barrier there.
                    for(int side=-1;side<=1;side+=2)Box(t,"Waschsalon Leuchtstreifen",new Vector3(side*3.8f,3.1f,-s.d*.5f-.3f),new Vector3(4,.06f,.08f),Mat("LaundryLight",new Color(.4f,.8f,.82f),false,true),false);
                    break;
                case "bar":
                    Box(t,"Backsteinschornstein",new Vector3(-s.w*.3f,s.h+2,2),new Vector3(1,4,1),wall);
                    Box(t,"Schornsteinkrone",new Vector3(-s.w*.3f,s.h+4.05f,2),new Vector3(1.3f,.2f,1.3f),trim);
                    Box(t,"Ausleger Schildarm",new Vector3(s.w*.5f-.6f,3,-s.d*.5f-.8f),new Vector3(.09f,.09f,1.4f),dark,false);
                    Box(t,"Pub Haengeschild",new Vector3(s.w*.5f-.6f,2.55f,-s.d*.5f-1.1f),new Vector3(.1f,.7f,.9f),gold,false);
                    break;
                case "cafe":
                    foreach(int side in new[]{-1,1})
                    {
                        var dormer=Group(t,"Dachgaube",new Vector3(side*3,s.h+.4f,-s.d*.2f));
                        Box(dormer,"Gaubenkorpus",new Vector3(0,.8f,0),new Vector3(1.8f,1.6f,2.4f),trim);
                        Box(dormer,"Gaubenfenster",new Vector3(0,.8f,-1.22f),new Vector3(1.2f,1.1f,.03f),glass,false);
                        Pitched(dormer,"Gaubendach",2.1f,2.7f,1.6f,.8f,dark);
                    }
                    break;
            }
        }
    }
}
