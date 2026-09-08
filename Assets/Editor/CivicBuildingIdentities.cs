using System;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void IndividualBuildingFinish(Transform t,Spec s,Material stone,Material pale,Material metal)
        {
            float f=-s.d*.5f;
            var wood=Mat("V5_OiledOak",new Color(.39f,.25f,.13f));
            var brass=Mat("V5_Brass",new Color(.64f,.45f,.2f));
            var blue=Mat("V5_DeepBlue",new Color(.035f,.12f,.2f));
            var red=Mat("V5_Oxblood",new Color(.29f,.035f,.03f));
            var green=Mat("V5_Sage",new Color(.2f,.32f,.25f));
            // Each catalog ID has a deliberate treatment; new types cannot silently fall back.
            switch(s.id)
            {
                case "19_Bank":
                    FramedGlazing(t,brass);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(t,"Bank Naturstein Portalwange",new Vector3(side*4,2.65f,f-.6f),new Vector3(.8f,5.3f,1.1f),stone);
                        for(int i=0;i<9;i++)Box(t,"Portal horizontale Schattenfuge",new Vector3(side*4,.35f+i*.55f,f-1.17f),new Vector3(.82f,.018f,.025f),metal,false);
                    }
                    Box(t,"Bank Portalsturz",new Vector3(0,5.3f,f-.6f),new Vector3(8.8f,.65f,1.1f),stone);
                    GlassCanopy(t,new Vector3(0,3.5f,f-.5f),6,2.8f,brass);
                    Planter(t,new Vector3(-8,0,f-.9f),2.5f,stone,green);
                    break;
                case "20_Jobcenter":
                    FramedGlazing(t,pale);
                    SlatScreen(t,new Vector3(-6,2,f-.28f),3.4f,3.6f,wood);
                    GlassCanopy(t,new Vector3(0,3.3f,f-.5f),6,2.5f,metal);
                    Bench(t,new Vector3(-6,0,f-1.3f),3,wood,metal);
                    NoticeCase(t,new Vector3(5,1.7f,f-.3f),1.4f,blue,pale);
                    Planter(t,new Vector3(7.7f,0,f-.7f),1.5f,stone,green);
                    break;
                case "21_Waschsalon":
                    ShopPortal(t,s,green,pale);
                    for(int i=0;i<3;i++)
                    {
                        Profile(t,"Runde Waschsalon Fassadenrosette",new Vector3(-4.3f+i*.75f,2,f-.26f),new[]{0f,.035f,.06f},new[]{.27f,.27f,.21f},24,pale,Quaternion.Euler(90,0,0));
                        Profile(t,"Blauer Rosettenkern",new Vector3(-4.3f+i*.75f,2,f-.32f),new[]{0f,.02f},new[]{.18f,.18f},24,blue,Quaternion.Euler(90,0,0));
                    }
                    for(int i=0;i<4;i++)Profile(t,"Waschmaschinen Bullauge",new Vector3(s.w*.5f-1.5f,.55f,-s.d*.2f+i*1.5f),new[]{0f,.05f,.07f},new[]{.32f,.32f,.25f},24,metal,Quaternion.Euler(0,0,90));
                    Bench(t,new Vector3(-3.8f,0,f-1),2.4f,wood,metal);
                    break;
                case "22_Lagerhalle":
                case "35_Logistikhalle":
                    HallFinish(t,s,metal,pale);
                    if(s.id=="35_Logistikhalle")
                    {
                        Box(t,"Logistik Buero Dachaufsatz",new Vector3(-s.w*.3f,s.h+.6f,-s.d*.22f),new Vector3(6,1.2f,5),blue);
                        for(int i=0;i<4;i++)Box(t,"Laderampe Gummipuffer",new Vector3(s.w*.5f+.45f,.45f,-s.d*.3f+i*5.5f),new Vector3(.5f,.9f,3.7f),metal);
                    }
                    break;
                case "23_FastFood":
                    ShopPortal(t,s,red,pale);FramedGlazing(t,metal);
                    for(int side=-1;side<=1;side+=2)for(int line=0;line<3;line++)Box(t,"Diner Chromband",new Vector3(side*(s.w+3.2f)*.25f,.35f+line*.18f,f-.2f),new Vector3((s.w-3.2f)*.5f,.055f,.09f),pale,false);
                    for(int i=0;i<2;i++)OutdoorTable(t,new Vector3(-6+i*3,0,f-2),red,metal);
                    Box(t,"Diner Vordach Untersicht",new Vector3(0,s.h+.16f,f-.8f),new Vector3(s.w+1,.08f,1.2f),Mat("V5_DinerLight",new Color(1,.72f,.4f),false,true),false);
                    break;
                case "24_Waschstrasse":
                    for(int end=-1;end<=1;end+=2)
                    {
                        foreach(int side in new[]{-1,1})Box(t,"Waschstrasse Eingangsportal",new Vector3(side*3.5f,2.25f,end*s.d*.5f),new Vector3(.65f,4.5f,.9f),blue);
                        Box(t,"Waschportal Beschriftungsblende",new Vector3(0,4.65f,end*s.d*.5f),new Vector3(7.7f,.65f,.9f),pale);
                    }
                    for(float z=-s.d*.4f;z<s.d*.45f;z+=3.5f)foreach(int side in new[]{-1,1})Box(t,"Waschtunnel Aussenrippe",new Vector3(side*(s.w*.5f+.18f),2.2f,z),new Vector3(.24f,4.4f,.22f),pale,false);
                    NoticeCase(t,new Vector3(-4.2f,1.6f,f-.6f),.8f,blue,pale);
                    break;
                case "25_Bahnhof":
                    FramedGlazing(t,pale);
                    for(int side=-1;side<=1;side+=2)for(int n=0;n<3;n++)
                    {
                        float x=side*(4+n*3.7f);Pillar(t,new Vector3(x,0,f-2),3,.1f,metal);
                        Beam(t,"Bahnhof Konsolenstrebe",new Vector3(x,2.4f,f-2),new Vector3(x+side*.65f,3,f-2),.065f,metal);
                    }
                    for(int side=-1;side<=1;side+=2)Bench(t,new Vector3(side*8,0,f-1.1f),3.5f,wood,metal);
                    NoticeCase(t,new Vector3(-4.8f,1.8f,f-.3f),2,metal,pale);
                    ClockFace(t,new Vector3(0,s.h+2,f-.45f),.75f,metal,pale);
                    break;
                case "28_Finanzamt":
                    FramedGlazing(t,metal);
                    for(int floor=0;floor<4;floor++)Cornice(t,s.w,s.d,3.7f+floor*3.6f,pale,1);
                    for(int i=0;i<7;i++)Box(t,"Amtsgebaeude vertikale Steinrippe",new Vector3(-s.w*.5f+.8f+i*.3f,7,f-.25f),new Vector3(.10f,13.5f,.35f),stone,false);
                    GlassCanopy(t,new Vector3(0,3.4f,f-.5f),7,2.4f,metal);
                    Planter(t,new Vector3(8,0,f-1),3,stone,green);
                    break;
                case "29_Baustelle":
                    for(int side=-1;side<=1;side+=2)
                    {
                        for(float z=-s.d*.5f;z<s.d*.5f;z+=3)
                        {
                            Box(t,"Geruest Vertikalrohr",new Vector3(side*(s.w*.5f+.7f),3,z),new Vector3(.065f,6,.065f),metal,false);
                            Beam(t,"Geruest Diagonalverband",new Vector3(side*(s.w*.5f+.7f),.5f,z),new Vector3(side*(s.w*.5f+.7f),5.5f,Mathf.Min(z+3,s.d*.5f)),.045f,metal);
                        }
                        for(int level=1;level<=2;level++)Box(t,"Geruest Laufbohle",new Vector3(side*(s.w*.5f+.7f),level*2.5f,0),new Vector3(1,.12f,s.d),wood,false);
                    }
                    for(int i=0;i<5;i++)Box(t,"Schalungsbrettstapel",new Vector3(-4,.15f+i*.15f,3),new Vector3(4,.12f,.8f),wood);
                    break;
                case "30_Bar":
                    ShopPortal(t,s,wood,brass);
                    FramedGlazing(t,wood);
                    for(int side=-1;side<=1;side+=2)Box(t,"Pub Holzsockel Front",new Vector3(side*(s.w+3.2f)*.25f,.4f,f-.15f),new Vector3((s.w-3.2f)*.5f,.8f,.2f),wood,false);
                    NoticeCase(t,new Vector3(-3.1f,1.9f,f-.38f),1,wood,pale);
                    Barrel(t,new Vector3(-3.1f,0,f-1.1f),wood,metal);
                    break;
                case "31_Motel":
                    FramedGlazing(t,pale);
                    for(float x=-s.w*.5f+.5f;x<s.w*.5f;x+=3.2f)
                    {
                        // Supports avoid the middle public doorway.
                        if(Mathf.Abs(x)<2)continue;
                        Pillar(t,new Vector3(x,0,f-1.5f),s.h,.1f,metal);
                        WallLantern(t,new Vector3(x,s.h+2,f-.2f),metal);
                    }
                    Box(t,"Motel seitlicher Signalmast",new Vector3(-s.w*.5f-.8f,3.5f,f),new Vector3(.25f,7,.25f),metal);
                    Box(t,"Motel Leuchtkasten",new Vector3(-s.w*.5f-.8f,5.5f,f),new Vector3(1.8f,2.5f,.3f),red,false);
                    Planter(t,new Vector3(6,0,f-1),2,stone,green);
                    break;
                case "32_Cafe":
                    ShopPortal(t,s,wood,pale);FramedGlazing(t,wood);
                    OutdoorTable(t,new Vector3(-4,0,f-1.8f),wood,metal);
                    OutdoorTable(t,new Vector3(4,0,f-1.8f),wood,metal);
                    Planter(t,new Vector3(-5.2f,0,f-.6f),1,pale,green);
                    NoticeCase(t,new Vector3(-3.8f,1.75f,f-.3f),1,wood,pale);
                    break;
                case "33_Eisdiele":
                    ShopPortal(t,s,green,pale);FramedGlazing(t,green);
                    for(int i=0;i<3;i++)Profile(t,"Eisdielen runder Hocker",new Vector3(-3.5f+i*.7f,0,f-1),new[]{0f,.1f,.6f,.7f},new[]{.22f,.13f,.1f,.26f},16,pale);
                    for(int i=0;i<3;i++)SphereDetail(t,"Eiskugel Fassadensignet",new Vector3(-3.4f+i*.27f,2.25f+(i%2)*.2f,f-.28f),Vector3.one*.35f,i%2==0?pale:red);
                    break;
                case "34_PolizeiCampus":
                    FramedGlazing(t,metal);
                    GlassCanopy(t,new Vector3(0,3.5f,f-.6f),6.5f,2.7f,blue);
                    foreach(int side in new[]{-1,1})
                    {
                        Box(t,"Polizei Portalstele",new Vector3(side*3.8f,2.5f,f-.45f),new Vector3(.45f,5,.6f),blue);
                        for(int i=0;i<3;i++)Pillar(t,new Vector3(side*(3.1f+i*1.2f),0,f-2.7f),.8f,.09f,metal);
                    }
                    NoticeCase(t,new Vector3(-6,1.75f,f-.4f),1.5f,blue,pale);
                    break;
                default:
                    DetailedShop(t,s,stone,pale,metal,wood,brass,red,green);
                    break;
            }
        }
        static void FramedGlazing(Transform t,Material mat)
        {
            foreach(var r in t.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Durchsichtiges Fenster").ToArray())
            {
                var p=r.transform;Vector3 c=p.localPosition;float w=p.localScale.x,h=p.localScale.y;
                foreach(int side in new[]{-1,1})Box(p.parent,"Profilierte Fensterwange",c+new Vector3(side*(w*.5f+.075f),0,-.12f),new Vector3(.12f,h+.18f,.24f),mat,false);
                Box(p.parent,"Fensterprofil oben",c+new Vector3(0,h*.5f+.08f,-.12f),new Vector3(w+.27f,.12f,.24f),mat,false);
                Box(p.parent,"Fensterbank mit Tropfkante",c+new Vector3(0,-h*.5f-.1f,-.2f),new Vector3(w+.35f,.12f,.4f),mat,false);
            }
        }
        static void Beam(Transform t,string name,Vector3 a,Vector3 b,float width,Material mat)
        {var g=Box(t,name,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),mat,false);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void GlassCanopy(Transform t,Vector3 p,float width,float depth,Material metal)
        {
            var g=Group(t,"Abgehaengtes Glasvordach",p);
            Box(g,"Vordach Verbundglas",new Vector3(0,0,-depth*.5f),new Vector3(width,.06f,depth),Mat("ClearGlass",new Color(.63f,.8f,.87f,.19f),true),false);
            foreach(int side in new[]{-1,1})
            {
                Box(g,"Vordach Randtraeger",new Vector3(side*width*.5f,0,-depth*.5f),new Vector3(.08f,.12f,depth),metal,false);
                Beam(g,"Zugstange",new Vector3(side*width*.4f,.9f,0),new Vector3(side*width*.4f,0,-depth*.9f),.035f,metal);
            }
        }
        static void ShopPortal(Transform t,Spec s,Material mat,Material detail)
        {
            float f=-s.d*.5f-.2f;
            foreach(int side in new[]{-1,1})
            {
                Box(t,"Ladenportal profilierter Pfosten",new Vector3(side*1.75f,1.5f,f),new Vector3(.18f,3,.35f),mat,false);
                Box(t,"Portalpfosten Fuss",new Vector3(side*1.75f,.2f,f-.03f),new Vector3(.3f,.4f,.42f),detail,false);
            }
            Box(t,"Profilierter Ladensturz",new Vector3(0,3.06f,f),new Vector3(3.75f,.16f,.38f),mat,false);
        }
        static void SlatScreen(Transform t,Vector3 p,float width,float height,Material mat)
        {for(float x=-width*.5f;x<width*.5f;x+=.19f)Box(t,"Fassaden Holzlamelle",p+Vector3.right*x,new Vector3(.065f,height,.17f),mat,false);}
        static void Planter(Transform t,Vector3 p,float width,Material mat,Material leaf)
        {
            Box(t,"Pflanzkuebel",p+Vector3.up*.3f,new Vector3(width,.6f,.7f),mat);
            for(float x=-width*.4f;x<=width*.4f;x+=.5f)SphereDetail(t,"Pflanzung",p+new Vector3(x,.8f,0),new Vector3(.6f,.65f,.6f),leaf);
        }
        static void Bench(Transform t,Vector3 p,float width,Material wood,Material metal)
        {
            t=Group(t,"SITZBANK - komplett verschiebbar",p);p=Vector3.zero;
            for(int i=0;i<4;i++)Box(t,"Sitzbank Holzlatte",p+new Vector3(0,.48f,-.22f+i*.14f),new Vector3(width,.065f,.11f),wood,false);
            foreach(int side in new[]{-1,1})Box(t,"Bank Stahlfuss",p+new Vector3(side*width*.35f,.24f,0),new Vector3(.10f,.48f,.5f),metal);
            var c=Box(t,"Sitzbank Kollisionsvolumen",p+Vector3.up*.24f,new Vector3(width,.48f,.5f),wood);
            Object.DestroyImmediate(c.GetComponent<Renderer>());Object.DestroyImmediate(c.GetComponent<MeshFilter>());
        }
        static void NoticeCase(Transform t,Vector3 p,float w,Material frame,Material paper)
        {
            Box(t,"Gerahmter Aushang",p,new Vector3(w,1.2f,.16f),frame,false);
            Box(t,"Aushangtafel",p+new Vector3(0,0,-.09f),new Vector3(w-.15f,1.05f,.025f),paper,false);
            for(int i=0;i<5;i++)Box(t,"Aushang Druckzeile",p+new Vector3(0,.3f-i*.13f,-.108f),new Vector3(w*.65f,.018f,.008f),frame,false);
        }
        static void OutdoorTable(Transform t,Vector3 p,Material mat,Material metal)
        {
            Profile(t,"Bistrotisch",p,new[]{0f,.08f,.7f,.78f},new[]{.27f,.1f,.06f,.5f},20,metal);
            Box(t,"Tischplatte",p+Vector3.up*.8f,new Vector3(1,.08f,.75f),mat);
            foreach(int side in new[]{-1,1})Box(t,"Bistro Sitzhocker",p+new Vector3(side*.85f,.23f,0),new Vector3(.4f,.46f,.4f),mat);
        }
        static void Barrel(Transform t,Vector3 p,Material wood,Material metal)
        {
            Profile(t,"Holzfass",p,new[]{0f,.15f,.5f,.9f,1},new[]{.33f,.39f,.43f,.39f,.33f},16,wood);
            foreach(float y in new[]{.15f,.8f})Profile(t,"Fassreifen",p+Vector3.up*y,new[]{0f,.06f},new[]{.4f,.4f},16,metal);
        }
        static void ClockFace(Transform t,Vector3 p,float r,Material frame,Material face)
        {
            Profile(t,"Bahnhofsuhr",p,new[]{0f,.1f},new[]{r,r},32,frame,Quaternion.Euler(90,0,0));
            Profile(t,"Zifferblatt",p+Vector3.back*.11f,new[]{0f,.025f},new[]{r*.86f,r*.86f},32,face,Quaternion.Euler(90,0,0));
            Box(t,"Minutenzeiger",p+new Vector3(0,r*.23f,-.15f),new Vector3(.045f,r*.52f,.025f),frame,false);
            Box(t,"Stundenzeiger",p+new Vector3(r*.17f,0,-.15f),new Vector3(r*.4f,.045f,.025f),frame,false);
        }
        static void HallFinish(Transform t,Spec s,Material metal,Material pale)
        {
            for(float z=-s.d*.4f;z<s.d*.5f;z+=6)
            {
                Beam(t,"Hallen Dachfachwerk links",new Vector3(-s.w*.5f,s.h-.2f,z),new Vector3(0,s.h+2.4f,z),.15f,metal);
                Beam(t,"Hallen Dachfachwerk rechts",new Vector3(0,s.h+2.4f,z),new Vector3(s.w*.5f,s.h-.2f,z),.15f,metal);
                Beam(t,"Hallen Untergurt",new Vector3(-s.w*.5f,s.h-.2f,z),new Vector3(s.w*.5f,s.h-.2f,z),.12f,metal);
            }
            foreach(int side in new[]{-1,1})Box(t,"Tor Fuehrungsschiene",new Vector3(side*3.15f,2.7f,-s.d*.5f-.2f),new Vector3(.12f,5.4f,.17f),metal,false);
            Box(t,"Tor Rollkasten",new Vector3(0,5.55f,-s.d*.5f-.35f),new Vector3(6.5f,.5f,.6f),pale);
            for(int i=0;i<3;i++)Box(t,"Palettenstapel",new Vector3(-s.w*.32f,.15f+i*.23f,-s.d*.2f),new Vector3(1.2f,.2f,.8f),pale);
        }
    }
}
