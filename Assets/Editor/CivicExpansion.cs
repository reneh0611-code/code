using System;
using System.Linq;
using CheatOnYourDayOnes.World;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static bool CreateExpansion(GameObject root,Spec s)
        {
            if(int.Parse(s.id.Substring(0,2))<48)return false;
            var t=root.transform;
            var wall=Mat(s.id,s.color);var trim=Mat("ResidentialTrim",new Color(.83f,.8f,.7f));
            var dark=Mat("ResidentialMetal",new Color(.055f,.065f,.073f));var roof=Mat("ResidentialRoof",new Color(.115f,.13f,.145f));
            var glass=Mat("ClearGlass",new Color(.63f,.8f,.87f,.19f),true);
            var wood=Mat("ResidentialWood",new Color(.29f,.19f,.11f));
            if(s.style=="exterior")ExteriorVariant(t,s,wall,trim,dark,roof);
            else if(s.style=="apartments")ApartmentBuilding(t,s,wall,trim,glass,dark,roof,wood);
            else if(s.style=="bus")BusTerminal(t,s,wall,trim,glass,dark);
            else
            {
                ExpansionShell(t,s,wall,trim,glass,dark,roof);
                ExpansionIdentity(t,s,wall,trim,glass,dark,wood);
                FramedGlazing(t,s.style=="mafia"?trim:dark);
                Masonry(t,s);RoofCourses(t);
            }
            bool residential=s.style=="apartments"||s.style=="exterior";
            root.AddComponent<CityBuilding>().Configure(s.id,s.label,residential?CityDistrict.Residential:CityDistrict.Commercial,s.type,false,s.style!="exterior");
            Group(t,"EINGANG - FREI",new Vector3(0,0,-s.d*.5f));
            Group(t,"LOGIK ANKER - SPAETER",Vector3.zero);
            return true;
        }
        static void ExpansionShell(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark,Material roof)
        {
            Box(t,"Innenboden",new Vector3(0,-.07f,0),new Vector3(s.w,.2f,s.d),Mat("ResidentialFloor",new Color(.46f,.42f,.35f)));
            float f=-s.d*.5f;
            if(s.style=="garage")
            {
                float edge=-s.w*.5f;
                foreach(float x in new[]{-8f,0,8f})
                {
                    float left=x-2.8f,right=x+2.8f;
                    Box(t,"Garagen Torpfeiler",new Vector3((edge+left)*.5f,s.h*.5f,f),new Vector3(left-edge,s.h,.35f),wall);
                    Box(t,"Garagen Torsturz",new Vector3(x,(4.7f+s.h)*.5f,f),new Vector3(5.6f,s.h-4.7f,.35f),wall);
                    Box(t,"Hochgezogenes Garagentor",new Vector3(x,4.85f,f+.5f),new Vector3(5.6f,.25f,1.2f),trim);
                    foreach(int side in new[]{-1,1})Box(t,"Torfuehrung",new Vector3(x+side*2.87f,2.35f,f-.25f),new Vector3(.1f,4.7f,.15f),dark,false);
                    Group(t,"FAHRSPUR GARAGE",new Vector3(x,0,f));edge=right;
                }
                Box(t,"Garagen Eckpfeiler",new Vector3((edge+s.w*.5f)*.5f,s.h*.5f,f),new Vector3(s.w*.5f-edge,s.h,.35f),wall);
            }
            else if(s.style=="showroom")CurtainFront(Group(t,"Showroom Glasfront",new Vector3(0,0,f)),s.w,s.h,5.6f,glass,dark);
            else Facade(Group(t,"Front",new Vector3(0,0,f)),s.w,s.h,3.2f,wall,trim,glass,dark,false,false,s.style=="nightclub"?0:2,2,.55f,3.1f);
            foreach(int side in new[]{-1,1})Facade(Group(t,"Seitenwand",new Vector3(side*s.w*.5f,0,0),side*90),s.d,s.h,0,wall,trim,glass,dark,false,false,s.style=="nightclub"?0:1,1.5f,1.2f,3.1f);
            Facade(Group(t,"Rueckwand",new Vector3(0,0,s.d*.5f),180),s.w,s.h,0,wall,trim,glass,dark,false,false,0);
            if(s.style=="italian"||s.style=="grill"||s.style=="mafia")Pitched(t,"Dach",s.w+.7f,s.d+.7f,s.h,2.8f,roof);
            else Box(t,"Dach",new Vector3(0,s.h+.15f,0),new Vector3(s.w+.6f,.3f,s.d+.6f),roof);
            Cornice(t,s.w,s.d,s.h,trim,2);
            CityFacadeDetails.Word(t,s.sign,new Vector3(0,s.h-.6f,f-.27f),s.style=="nightclub"?.7f:.5f,trim);
            var lamp=new GameObject("Innenraumlicht");lamp.transform.SetParent(t,false);lamp.transform.localPosition=new Vector3(0,s.h-.5f,0);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=Mathf.Max(s.w,s.d)*.8f;light.intensity=1.5f;light.shadows=LightShadows.None;
        }
        static void ExpansionIdentity(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark,Material wood)
        {
            float f=-s.d*.5f;var green=Mat("V6_Green",new Color(.2f,.32f,.23f));
            switch(s.style)
            {
                case "showroom":
                    GlassCanopy(t,new Vector3(0,4.7f,f-.5f),8,3,dark);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(t,"Showroom Empfang",new Vector3(side*9,.6f,s.d*.3f),new Vector3(3,1.2f,1),wood);
                        for(int i=0;i<2;i++)Group(t,"AUTO AUSSTELLUNGSPLATZ",new Vector3(side*8,0,-3+i*7));
                    }
                    for(int i=0;i<4;i++)Box(t,"Showroom Deckenlicht",new Vector3(-9+i*6,s.h-.2f,0),new Vector3(.3f,.1f,s.d-2),Mat("ShowroomLight",new Color(.9f,.93f,1),false,true),false);
                    break;
                case "garage":
                    var blue=Mat("GarageBlue",new Color(.06f,.2f,.36f));
                    foreach(float x in new[]{-8f,0,8f})
                    {
                        foreach(int side in new[]{-1,1})
                        {
                            Box(t,"Hebebuehnen Saeule",new Vector3(x+side*2,1.9f,1.8f),new Vector3(.35f,3.8f,.45f),blue);
                            Box(t,"Werkstatt Bodenlinie",new Vector3(x+side*1.7f,.045f,-2),new Vector3(.06f,.012f,9),trim,false);
                        }
                        Box(t,"Werkbank",new Vector3(x,.6f,s.d*.5f-1),new Vector3(4,1.2f,.8f),dark);
                        NoticeCase(t,new Vector3(x,2.4f,s.d*.5f-.2f),2,blue,trim);
                    }
                    break;
                case "skate":
                    ShopPortal(t,s,wood,trim);
                    for(int i=0;i<5;i++)
                    {
                        var board=Box(t,"Skateboard Deck",new Vector3(-4.7f+i*.7f,2.1f,f-.25f),new Vector3(.23f,1.2f,.08f),i%2==0?wood:green,false);
                        board.transform.localRotation=Quaternion.Euler(0,0,12);
                        foreach(int end in new[]{-1,1})SphereDetail(t,"Skateboard Rollen",new Vector3(-4.7f+i*.7f,2.1f+end*.4f,f-.33f),new Vector3(.3f,.12f,.12f),trim);
                    }
                    DisplayShelves(t,s,wood,green,4);break;
                case "nightclub":
                    var neon=Mat("NightclubNeon",new Color(.47f,.1f,.8f),false,true);
                    foreach(int side in new[]{-1,1})
                    {
                        Box(t,"Nachtclub Lichtportal",new Vector3(side*2.1f,1.7f,f-.4f),new Vector3(.08f,3.4f,.15f),neon,false);
                        Box(t,"Lounge Sitzbank",new Vector3(side*8,.55f,2),new Vector3(3,1.1f,4),dark);
                    }
                    Box(t,"Lichtportal Sturz",new Vector3(0,3.4f,f-.4f),new Vector3(4.3f,.08f,.15f),neon,false);
                    Box(t,"Tanzflaeche",new Vector3(0,.045f,2),new Vector3(10,.025f,9),dark,false);
                    Box(t,"DJ Pult",new Vector3(0,.65f,8),new Vector3(4,1.3f,1.4f),dark);
                    foreach(int side in new[]{-1,1})Box(t,"Lautsprecher",new Vector3(side*4,1.5f,8),new Vector3(1,3,.8f),dark);
                    break;
                case "mafia":
                    foreach(int side in new[]{-1,1})Pillar(t,new Vector3(side*3,0,f-1),4,.3f,trim);
                    Arch(t,new Vector3(0,3.2f,f-.45f),2.2f,.25f,.3f,trim,18);
                    WindowDressings(t,trim,true);
                    InteriorDivider(t,0,-s.w*.5f,s.w*.5f,3,2.4f,s.h,wall,false);
                    Box(t,"Besprechungstisch",new Vector3(0,.95f,6.5f),new Vector3(6,.15f,2.2f),wood);
                    for(int i=0;i<5;i++)foreach(int side in new[]{-1,1})Box(t,"Konferenzstuhl",new Vector3(-2.4f+i*1.2f,.5f,6.5f+side*1.7f),new Vector3(.65f,1,.65f),dark);
                    Planter(t,new Vector3(-7,0,f-1),3,trim,green);Planter(t,new Vector3(7,0,f-1),3,trim,green);
                    break;
                case "fashion_new":
                case "streetwear":
                    ShopPortal(t,s,s.style=="fashion_new"?trim:dark,wood);
                    SlatScreen(t,new Vector3(-s.w*.35f,2,f-.25f),2.2f,3.5f,wood);
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
                    {
                        float x=side*s.w*.3f,z=-2+i*3;
                        Beam(t,"Kleiderstange",new Vector3(x-1,2.2f,z),new Vector3(x+1,2.2f,z),.04f,dark);
                        foreach(int edge in new[]{-1,1})Box(t,"Kleiderstaender",new Vector3(x+edge,1.1f,z),new Vector3(.06f,2.2f,.06f),dark);
                        for(int item=0;item<4;item++)Box(t,"Kleidungsattrappe",new Vector3(x-.7f+item*.45f,1.55f,z),new Vector3(.3f,.9f,.15f),item%2==0?green:trim,false);
                    }
                    break;
                case "estate":
                    GlassCanopy(t,new Vector3(0,3.4f,f-.3f),5,2,dark);
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)NoticeCase(t,new Vector3(side*(3.2f+i*1.6f),1.8f,f-.3f),1.2f,dark,trim);
                    for(int side=-1;side<=1;side+=2){Box(t,"Beratertisch",new Vector3(side*4,.95f,2),new Vector3(2,.12f,1.2f),wood);Box(t,"Besucherstuhl",new Vector3(side*4,.5f,.7f),new Vector3(.7f,1,.7f),dark);}
                    break;
                case "italian":
                case "asian":
                case "grill":
                    ShopPortal(t,s,wood,trim);
                    InteriorDivider(t,0,-s.w*.5f,s.w*.5f,s.d*.22f,2.2f,s.h,wall,false);
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)OutdoorTable(t,new Vector3(side*s.w*.28f,0,-s.d*.25f+i*3),wood,dark);
                    Box(t,"Kuechenarbeitszeile",new Vector3(-s.w*.25f,.6f,s.d*.38f),new Vector3(s.w*.35f,1.2f,1),trim);
                    Box(t,"Dunstabzug",new Vector3(-s.w*.25f,3.1f,s.d*.38f),new Vector3(s.w*.35f,.5f,1.2f),dark,false);
                    if(s.style=="italian"){ShopAwning(t,s,new Color(.3f,.4f,.2f),trim,true);Profile(t,"Pizzaofen Kuppel",new Vector3(s.w*.3f,1,s.d*.35f),new[]{0f,.3f,.8f,1.1f},new[]{.9f,.9f,.6f,0f},20,wall);}
                    if(s.style=="asian")for(int i=0;i<6;i++)SphereDetail(t,"Laterne",new Vector3(-s.w*.4f+i*s.w*.16f,3.3f,f-.6f),new Vector3(.4f,.65f,.4f),Mat("AsiaLantern",new Color(.64f,.06f,.04f),false,true));
                    if(s.style=="grill"){Box(t,"Grill Schornstein",new Vector3(s.w*.3f,s.h+1,3),new Vector3(.8f,3,.8f),dark);Barrel(t,new Vector3(-s.w*.35f,0,f-1),wood,dark);}
                    break;
            }
        }
        // A complete wall with one true open doorway. Horizontal means X span at fixed Z.
        static void InteriorDivider(Transform t,float y,float min,float max,float fixedAxis,float door,float height,Material mat,bool alongZ,float doorCenter=0)
        {
            float a=doorCenter-door*.5f,b=doorCenter+door*.5f;
            void Segment(float left,float right,float bottom,float h)
            {
                if(right<=left||h<=0)return;
                Box(t,"Innenwand mit freier Tuer",alongZ?new Vector3(fixedAxis,y+bottom+h*.5f,(left+right)*.5f):new Vector3((left+right)*.5f,y+bottom+h*.5f,fixedAxis),alongZ?new Vector3(.18f,h,right-left):new Vector3(right-left,h,.18f),mat);
            }
            Segment(min,a,0,height);Segment(b,max,0,height);Segment(a,b,3,height-3);
        }
    }
}
