using System;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void DetailedShop(Transform t,Spec s,Material stone,Material pale,Material metal,Material wood,Material brass,Material red,Material green)
        {
            float f=-s.d*.5f;
            switch(s.id)
            {
                case "36_Baeckerei":
                    ShopPortal(t,s,wood,pale);FramedGlazing(t,wood);
                    Ring(t,"Brezel linker Bogen",new Vector3(-3.2f,2.2f,f-.32f),.29f,.09f,brass);
                    Ring(t,"Brezel rechter Bogen",new Vector3(-2.65f,2.2f,f-.32f),.29f,.09f,brass);
                    Beam(t,"Brezel Kreuzung",new Vector3(-3.4f,2.45f,f-.32f),new Vector3(-2.6f,1.8f,f-.32f),.11f,brass);
                    Beam(t,"Brezel Kreuzung",new Vector3(-2.45f,2.45f,f-.32f),new Vector3(-3.25f,1.8f,f-.32f),.11f,brass);
                    for(int side=-1;side<=1;side+=2)Box(t,"Baeckerei Holzladen oben",new Vector3(side*3.6f,s.h+2,f+.03f),new Vector3(.48f,1.9f,.16f),green,false);
                    DisplayTrays(t,new Vector3(s.w*.34f,.85f,f+.65f),wood,brass);
                    Bench(t,new Vector3(-3,0,f-1.1f),2.3f,wood,metal);
                    break;
                case "37_Metzgerei":
                    ShopPortal(t,s,red,pale);FramedGlazing(t,red);
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)Box(t,"Emaille Sockelfeld",new Vector3(side*(2.1f+i*.55f),.43f,f-.19f),new Vector3(.50f,.75f,.09f),pale,false);
                    NoticeCase(t,new Vector3(-3.1f,1.8f,f-.35f),1.3f,red,pale);
                    Box(t,"Metzger Auslage Rost",new Vector3(s.w*.3f,1,f+.6f),new Vector3(1.5f,.06f,.6f),metal,false);
                    for(int i=0;i<5;i++)SphereDetail(t,"Metzger Auslage",new Vector3(s.w*.3f-.55f+i*.25f,1.12f,f+.6f),new Vector3(.17f,.13f,.4f),red);
                    break;
                case "38_Apotheke":
                    ShopPortal(t,s,pale,green);FramedGlazing(t,green);
                    GlassCanopy(t,new Vector3(0,3.4f,f-.5f),5.5f,2.1f,metal);
                    for(int i=0;i<5;i++)Box(t,"Apotheken Sichtschutzlamelle",new Vector3(-5.4f+i*.24f,1.2f,f-.24f),new Vector3(.075f,1.2f,.12f),pale,false);
                    NoticeCase(t,new Vector3(-3.3f,1.6f,f-.35f),1,green,pale);
                    Planter(t,new Vector3(5.7f,0,f-1),1.3f,pale,green);
                    break;
                case "39_Kiosk":
                    ShopPortal(t,s,green,brass);FramedGlazing(t,green);
                    Box(t,"Kiosk Front Dachkranz",new Vector3(0,s.h+.46f,f-.4f),new Vector3(s.w+1,.18f,.4f),brass,false);
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<5;i++)Box(t,"Kiosk Sockellamelle",new Vector3(side*(s.w+3.2f)*.25f,.2f+i*.13f,f-.2f),new Vector3((s.w-3.2f)*.5f,.055f,.08f),wood,false);
                    Box(t,"Kiosk Verkaufstresen innen",new Vector3(2.4f,.88f,f+.7f),new Vector3(1.1f,.12f,.65f),wood);
                    NoticeCase(t,new Vector3(-2.5f,2.25f,f-.3f),.7f,green,pale);
                    break;
                case "40_Blumenladen":
                    ShopPortal(t,s,green,pale);FramedGlazing(t,wood);
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side*(s.w*.5f-.35f);
                        for(int i=0;i<6;i++)Beam(t,"Rankgitter Diagonale",new Vector3(x-.25f,.6f+i*.35f,f-.2f),new Vector3(x+.25f,.95f+i*.35f,f-.2f),.025f,wood);
                        for(int i=0;i<4;i++)SphereDetail(t,"Rankende Pflanze",new Vector3(x,1+i*.45f,f-.26f),new Vector3(.22f,.35f,.16f),green);
                    }
                    DisplayTrays(t,new Vector3(3.2f,.6f,f+.6f),wood,green);
                    break;
                case "41_Friseur":
                    ShopPortal(t,s,metal,brass);FramedGlazing(t,brass);
                    for(int side=-1;side<=1;side+=2)Box(t,"Barber gerahmte Holztafel",new Vector3(side*(s.w+3.2f)*.25f,.4f,f-.2f),new Vector3((s.w-3.2f)*.5f,.7f,.18f),wood,false);
                    NoticeCase(t,new Vector3(-2.8f,1.65f,f-.35f),1,brass,pale);
                    Bench(t,new Vector3(-2.8f,0,f-1.05f),1.7f,wood,metal);
                    break;
                case "42_Modeboutique":
                    ShopPortal(t,s,stone,brass);FramedGlazing(t,brass);
                    Box(t,"Boutique Fensterpodest",new Vector3(s.w*.3f,.18f,f+.7f),new Vector3(2.7f,.36f,.95f),stone);
                    for(int i=0;i<2;i++)
                    {
                        float x=s.w*.3f-.6f+i*1.2f;
                        Profile(t,"Schaufenster Buste",new Vector3(x,.36f,f+.7f),new[]{0f,.1f,.55f,1.2f,1.55f,1.65f},new[]{.18f,.06f,.06f,.22f,.28f,.08f},16,pale);
                        SphereDetail(t,"Busten Kopf",new Vector3(x,2.15f,f+.7f),new Vector3(.21f,.28f,.23f),pale);
                    }
                    Planter(t,new Vector3(-4.5f,0,f-.9f),1.6f,stone,green);
                    break;
                case "43_Elektronik":
                    ShopPortal(t,s,metal,pale);FramedGlazing(t,metal);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(t,"Technik Portalstele",new Vector3(side*3.8f,2.5f,f-.5f),new Vector3(.65f,5,.8f),metal);
                        for(int i=0;i<8;i++)Box(t,"Technik Stele Lichtsegment",new Vector3(side*3.8f,.45f+i*.55f,f-.93f),new Vector3(.42f,.05f,.04f),Mat("TechBlue",new Color(.07f,.35f,.8f),false,true),false);
                    }
                    Box(t,"Technik Portalsturz",new Vector3(0,5,f-.5f),new Vector3(8.2f,.5f,.8f),metal);
                    NoticeCase(t,new Vector3(-6.3f,1.8f,f-.3f),1.8f,metal,pale);
                    break;
                case "44_Fahrradladen":
                    ShopPortal(t,s,metal,wood);FramedGlazing(t,wood);
                    var bike=Group(t,"Fahrrad Fassadensignet",new Vector3(-4.8f,1.7f,f-.32f));
                    Ring(bike,"Fahrrad Hinterrad",new Vector3(-.55f,0,0),.35f,.055f,metal);
                    Ring(bike,"Fahrrad Vorderrad",new Vector3(.55f,0,0),.35f,.055f,metal);
                    Beam(bike,"Rahmen",new Vector3(-.55f,0,0),new Vector3(-.2f,.55f,0),.055f,brass);
                    Beam(bike,"Rahmen",new Vector3(-.2f,.55f,0),new Vector3(.15f,0,0),.055f,brass);
                    Beam(bike,"Rahmen",new Vector3(.15f,0,0),new Vector3(-.55f,0,0),.055f,brass);
                    Beam(bike,"Lenkgabel",new Vector3(.55f,0,0),new Vector3(.27f,.7f,0),.055f,brass);
                    Beam(bike,"Oberrohr",new Vector3(-.2f,.55f,0),new Vector3(.33f,.55f,0),.055f,brass);
                    GlassCanopy(t,new Vector3(0,3.5f,f-.5f),5,2.2f,metal);
                    break;
                case "45_Tierbedarf":
                    ShopPortal(t,s,wood,green);FramedGlazing(t,green);
                    SlatScreen(t,new Vector3(-6,2,f-.25f),2.7f,3.2f,wood);
                    SphereDetail(t,"Pfotenzeichen Ballen",new Vector3(-3.8f,1.85f,f-.32f),new Vector3(.5f,.43f,.12f),pale);
                    for(int i=0;i<3;i++)SphereDetail(t,"Pfotenzeichen Zehe",new Vector3(-4.07f+i*.27f,2.25f,f-.32f),new Vector3(.21f,.25f,.1f),pale);
                    Planter(t,new Vector3(6.8f,0,f-.9f),1.8f,wood,green);
                    break;
                case "46_Baumarkt":
                    FramedGlazing(t,metal);HallFinish(t,s,metal,pale);
                    var orange=Mat("ShopOrange",new Color(.85f,.29f,.04f));
                    foreach(int side in new[]{-1,1})for(int i=0;i<4;i++)Box(t,"Baumarkt Prallschutz",new Vector3(side*(5+i*.7f),.5f,f-1.6f),new Vector3(.12f,1,.12f),orange);
                    Box(t,"Baumarkt seitliches Lade Vordach",new Vector3(-s.w*.5f-.8f,3.6f,3),new Vector3(2.3f,.2f,7),metal);
                    foreach(int end in new[]{-1,1})Box(t,"Ladevordach Stuetzen",new Vector3(-s.w*.5f-1.7f,1.8f,3+end*3),new Vector3(.15f,3.6f,.15f),metal);
                    break;
                case "47_Buchhandlung":
                    ShopPortal(t,s,wood,brass);FramedGlazing(t,wood);
                    NoticeCase(t,new Vector3(-3.1f,1.8f,f-.3f),1.1f,wood,pale);
                    Box(t,"Buchhandlung Schaufensterpodest",new Vector3(3,.4f,f+.65f),new Vector3(1.7f,.15f,.7f),wood);
                    for(int i=0;i<7;i++)Box(t,"Buchauslage",new Vector3(2.35f+i*.2f,.7f,f+.65f),new Vector3(.12f,.45f,.3f),i%2==0?red:green,false);
                    Bench(t,new Vector3(-3,0,f-1.1f),1.8f,wood,metal);
                    for(int side=-1;side<=1;side+=2)Box(t,"Buchhaus Fensterladen",new Vector3(side*3.45f,s.h+2,f),new Vector3(.48f,1.85f,.16f),wood,false);
                    break;
                default:throw new InvalidOperationException("Individuelle Architektur fehlt fuer "+s.id);
            }
        }
        static void Ring(Transform t,string name,Vector3 p,float r,float thickness,Material mat)
        {
            var m=new SurfaceMesh();
            for(int i=0;i<32;i++)
            {
                float a=i*2*Mathf.PI/32,b=(i+1)*2*Mathf.PI/32;
                Vector3 P(float radius,float angle,float z)=>p+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                m.Quad(P(r,a,-.035f),P(r,b,-.035f),P(r+thickness,b,-.035f),P(r+thickness,a,-.035f));
                m.Quad(P(r+thickness,a,-.035f),P(r+thickness,b,-.035f),P(r+thickness,b,.035f),P(r+thickness,a,.035f));
            }
            m.Save(t,name,mat);
        }
        static void DisplayTrays(Transform t,Vector3 p,Material shelf,Material product)
        {
            for(int level=0;level<2;level++)
            {
                Box(t,"Schaufenster Auslagenboden",p+Vector3.up*level*.48f,new Vector3(1.8f,.08f,.6f),shelf);
                for(int i=0;i<4;i++)SphereDetail(t,"Schaufenster Auslage",p+new Vector3(-.65f+i*.42f,.16f+level*.48f,0),new Vector3(.32f,.22f,.36f),product);
            }
        }
    }
}
