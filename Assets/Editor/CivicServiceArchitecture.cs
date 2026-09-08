using System;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void IndividualServiceArchitecture(Transform t,int n,float w,float d,float h,Material white,Material stone,Material dark,Material brick,Material blue,Material yellow,Material red,Material glass,Material wood)
        {
            var main=t.Find("HAUPTGEBAEUDE");var details=Group(t,"INDIVIDUELLE ARCHITEKTUR",Vector3.zero);
            var copper=VMat("Service_Kupfer",new Color(.23f,.35f,.3f));var cyan=Mat("Service_Cyan",new Color(.12f,.65f,.8f),false,true);
            void ReplaceFront(Material frame)
            {
                var front=main.Find("EINGANG");if(front!=null)Object.DestroyImmediate(front.gameObject);
                CurtainFront(Group(main,"Offene Schaufensterfront",new Vector3(0,0,-d*.5f)),w,h,2.6f,glass,frame);
            }
            void RemoveRoof()
            {
                foreach(var c in main.Cast<Transform>().Where(c=>c.name=="Satteldach"||c.name=="Dachplatte").ToArray())Object.DestroyImmediate(c.gameObject);
            }
            void Roof(float rise,Material mat,bool mansard=false)
            {
                RemoveRoof();ResidentialRoof(Group(main,"Eigenstaendige Dachform",Vector3.zero),w+1,d+1,h,rise,mat,mansard);
            }
            void Strip(Transform p,string name,Vector3 a,Vector3 b,float size,Material mat)
            {Beam(p,name,a,b,size,mat);}
            switch(n)
            {
                case 0:
                    ReplaceFront(dark);
                    Box(details,"Post gelbe Dachscheibe",new Vector3(-2,h+.35f,-1),new Vector3(w+2,.5f,d+2),yellow);
                    Box(details,"Asymmetrisches Postvordach",new Vector3(-3,3.35f,-7.5f),new Vector3(12,.22f,3.5f),dark);
                    foreach(int s in new[]{-1,1})Box(details,"Stahlstuetze",new Vector3(-3+s*5.5f,1.6f,-8.8f),new Vector3(.13f,3.2f,.13f),dark);
                    for(int k=0;k<7;k++)Box(details,"Vordachlamelle",new Vector3(-8+k*1.5f,3.55f,-7.5f),new Vector3(.09f,.35f,3.5f),yellow,false);
                    Box(details,"Paketshop Signalturm",new Vector3(9,2.4f,-5),new Vector3(.55f,4.8f,1.6f),yellow);
                    CityFacadeDetails.Word(details,"POST",new Vector3(0,3.85f,-6.42f),.55f,dark);break;
                case 1:
                    Roof(2.8f,dark,true);ReplaceFront(wood);
                    Box(details,"Historische Ladenschuerze",new Vector3(0,3.45f,-6.45f),new Vector3(16.5f,.45f,.65f),red);
                    foreach(int s in new[]{-1,1}){Pillar(details,new Vector3(s*7.55f,0,-6.45f),3.3f,.19f,stone);Box(details,"Fenstersockel",new Vector3(s*4.5f,.4f,-6.1f),new Vector3(5.5f,.8f,.2f),wood);}
                    var emblem=Group(details,"Pfandhaus Drei Kugeln",new Vector3(6,4.8f,-7));
                    Strip(emblem,"Ausleger",new Vector3(0,0,1),new Vector3(0,0,0),.08f,dark);
                    foreach(float x in new[]{-.45f,0,.45f}){Strip(emblem,"Kette",new Vector3(x,0,0),new Vector3(x,-.4f,0),.035f,stone);SphereDetail(emblem,"Messingkugel",new Vector3(x,-.55f,0),Vector3.one*.32f,yellow);}
                    break;
                case 2:
                    foreach(int s in new[]{-1,1})
                    {
                        var porch=Group(details,"Eigenstaendiger Hauseingang",new Vector3(s*8.3f,0,-8));
                        Box(porch,"Vordach",new Vector3(0,3.1f,-1),new Vector3(3.5f,.16f,2.3f),s<0?wood:dark);
                        foreach(int side in new[]{-1,1})Box(porch,"Seitlicher Sichtschutz",new Vector3(side*1.6f,1.3f,-.75f),new Vector3(.09f,2.6f,1.6f),s<0?wood:white);
                    }break;
                case 3:
                    RemoveRoof();
                    var saw=Group(main,"Drei Shed-Dachsegmente",Vector3.zero);
                    Box(saw,"Hallendecke",new Vector3(0,h,0),new Vector3(w,.16f,d),dark);
                    for(int k=0;k<3;k++)
                    {
                        float z=-15+k*10;var panel=new SurfaceMesh();
                        panel.Quad(new Vector3(-13.5f,h,z),new Vector3(-13.5f,h+2.7f,z+10),new Vector3(13.5f,h+2.7f,z+10),new Vector3(13.5f,h,z));panel.Save(saw,"Shed Dachflaeche "+k,dark);
                        Box(saw,"Shed Lichtband",new Vector3(0,h+1.35f,z+10),new Vector3(27,2.7f,.08f),glass,false);
                        foreach(int s in new[]{-1,1}){var end=new SurfaceMesh();Vector3 a=new Vector3(s*13.5f,h,z),b=new Vector3(s*13.5f,h+2.7f,z+10),c=new Vector3(s*13.5f,h,z+10);if(s<0)end.Quad(a,b,c,c);else end.Quad(c,b,a,a);end.Save(saw,"Shed Abschluss",white);}
                    }
                    foreach(int side in new[]{-1,1})for(int z=-13;z<=13;z+=2)Box(details,"Hallen Fassadenrippe",new Vector3(side*13.2f,3.8f,z),new Vector3(.14f,7.6f,.16f),blue,false);
                    for(int s=-1;s<=1;s+=2)Box(details,"Rammschutz",new Vector3(s*3.4f,.55f,-15.6f),new Vector3(.22f,1.1f,.22f),yellow);
                    break;
                case 4:
                    Roof(1.1f,blue);
                    int number=0;
                    foreach(var unit in t.Cast<Transform>().Where(x=>x.name.StartsWith("LAGERBOX")).ToArray())
                    {
                        var bay=unit.Find("Box");Box(bay,"Farbiger Boxabschluss",new Vector3(0,4.15f,0),new Vector3(7.4f,.3f,8.4f),number++%2==0?blue:yellow);
                        foreach(int s in new[]{-1,1})Box(bay,"Torlaibung",new Vector3(s*1.85f,1.5f,-4.22f),new Vector3(.18f,3,.18f),blue,false);
                        Box(bay,"Hochgezogenes Rolltor",new Vector3(0,3.5f,-3.4f),new Vector3(3.3f,.35f,1.4f),dark,false);
                    }
                    break;
                case 5:
                    Roof(1.4f,red);ServiceSteelFrame(details,new Vector3(-12,0,32),15,10,8,dark);
                    Box(details,"Kranlaufbahn",new Vector3(-12,8.3f,32),new Vector3(16,.35f,.4f),yellow);
                    Strip(details,"Kranseil",new Vector3(-10,8.2f,32),new Vector3(-10,4.2f,32),.055f,dark);Ring(details,"Kranhaken",new Vector3(-10,4,32),.23f,.065f,dark);
                    for(int side=-1;side<=1;side+=2)for(int k=0;k<12;k++)Box(details,"Wellblech Buero",new Vector3(side*8.16f,2.1f,-5+k*.9f),new Vector3(.12f,4,.08f),red,false);
                    break;
                case 6:
                    ReplaceFront(blue);Box(details,"Breites Kontrollvordach",new Vector3(0,4.8f,-2),new Vector3(20,.25f,15),blue);
                    ServiceSteelFrame(details,new Vector3(12,0,24),12,30,5.8f,dark);
                    Box(details,"Abschlepphof Unterstand",new Vector3(12,6,24),new Vector3(13,.2f,31),white);
                    break;
                case 7:
                    Roof(1.6f,brick);foreach(int s in new[]{-1,1})for(int k=0;k<2;k++)
                    {
                        var gantry=Group(details,"Sammelschienen Portal",new Vector3(s*12,0,17+k*20));ServiceSteelFrame(gantry,Vector3.zero,8,3,7,dark);
                        for(int wire=-1;wire<=1;wire++)Strip(gantry,"Sammelschiene",new Vector3(wire*2,7.4f,-5),new Vector3(wire*2,7.4f,5),.06f,copper);
                    }break;
                case 8:
                    Roof(2.5f,copper);foreach(int s in new[]{-1,1})Pillar(details,new Vector3(s*2,0,-7.4f),4.2f,.18f,stone);
                    Pitched(Group(details,"Pumpenhaus Eingangsgiebel",new Vector3(0,0,-7)),"Steinfronton",5.4f,2.8f,4.3f,1.5f,stone);
                    foreach(int s in new[]{-1,1}){Strip(details,"Hauptleitung",new Vector3(s*20,.6f,7),new Vector3(s*20,.6f,42),.65f,blue);for(int z=10;z<42;z+=8)Box(details,"Rohrsattel",new Vector3(s*20,.25f,z),new Vector3(1,.5f,.6f),stone);}
                    break;
                case 9:
                    Box(details,"Flaches Technikdach",new Vector3(0,h+.3f,0),new Vector3(18,.5f,13),blue);
                    ServiceSteelFrame(details,new Vector3(0,0,29),38,4,8,dark);
                    foreach(float z in new[]{28f,29f,30f})Strip(details,"Rohrbruecke Leitung",new Vector3(-19,8.4f,z),new Vector3(19,8.4f,z),.23f,z==29?red:stone);
                    break;
                case 10:
                    var front=main.Find("EINGANG");var lintel=front.Find("Sturz");if(lintel!=null)Object.DestroyImmediate(lintel.gameObject);
                    Box(front,"Wand unter Rosenfenster",new Vector3(0,4.1f,0),new Vector3(2.6f,2.2f,.25f),brick);
                    Box(front,"Wand ueber Rosenfenster",new Vector3(0,8.45f,0),new Vector3(2.6f,1.1f,.25f),brick);
                    Box(front,"Rosenfenster Glas",new Vector3(0,6.55f,0),new Vector3(2.6f,2.7f,.035f),glass);
                    Ring(front,"Steinrosette",new Vector3(0,6.55f,-.18f),1.2f,.15f,stone);Ring(front,"Inneres Masswerk",new Vector3(0,6.55f,-.21f),.5f,.06f,stone);
                    for(int spoke=0;spoke<12;spoke++){float a=spoke*Mathf.PI/6;Strip(front,"Masswerk Speiche",new Vector3(.5f*Mathf.Cos(a),6.55f+.5f*Mathf.Sin(a),-.21f),new Vector3(1.1f*Mathf.Cos(a),6.55f+1.1f*Mathf.Sin(a),-.21f),.055f,stone);}
                    foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)Pillar(details,new Vector3(s*(1.45f+k*.22f),0,-14.35f-k*.16f),3.1f,.085f,stone);
                    for(int k=0;k<3;k++)Arch(details,new Vector3(0,3.1f,-14.4f-k*.16f),1.5f+k*.22f,.13f,.2f,stone,28);
                    var bell=t.Find("GLOCKENTURM - nicht ausgebaut");foreach(int s in new[]{-1,1}){var face=Group(bell,"Glockenfenster",Vector3.zero,s<0?0:180);Box(face,"Schalloeffnung",new Vector3(0,13.4f,-2.55f),new Vector3(2.3f,3,.1f),dark,false);for(int j=0;j<7;j++)Box(face,"Schalllamelle",new Vector3(0,12.2f+j*.36f,-2.65f),new Vector3(2.15f,.12f,.15f),stone,false);}
                    Cornice(bell,4.6f,5.6f,15.6f,stone,3);break;
                case 11:
                    ReplaceFront(dark);Box(details,"Sportliche Dachauskragung",new Vector3(-2,h+.3f,-1),new Vector3(w+3,.55f,d+2),dark);
                    for(int s=-1;s<=1;s+=2)Strip(details,"Diagonalstrebe",new Vector3(s*11.8f,0,-10.4f),new Vector3(s*8,h,-10.4f),.18f,yellow);
                    Box(details,"Fitness Leuchtband",new Vector3(0,3.7f,-10.3f),new Vector3(22,.16f,.12f),yellow,false);break;
                case 12:
                    ReplaceFront(dark);Box(details,"Technikportal oben",new Vector3(0,4.85f,-6.5f),new Vector3(17,.55f,1.4f),dark);
                    foreach(int s in new[]{-1,1}){Box(details,"Technikportal Seite",new Vector3(s*8.2f,2.4f,-6.5f),new Vector3(.45f,4.8f,1.4f),dark);Box(details,"Lichtkante",new Vector3(s*7.92f,2.4f,-7.23f),new Vector3(.045f,4.5f,.04f),cyan,false);}
                    Box(details,"Leuchtlinie",new Vector3(0,4.52f,-7.23f),new Vector3(16,.045f,.04f),cyan,false);break;
                case 13:
                    ReplaceFront(dark);
                    Box(details,"Verbindende Dachkante",new Vector3(0,7.4f,0),new Vector3(31,.6f,22.5f),red);
                    foreach(int s in new[]{-1,1})
                    {
                        Box(details,"Torrahmen links",new Vector3(s*11-3,2.75f,-11.3f),new Vector3(.3f,5.5f,.4f),red);
                        Box(details,"Torrahmen rechts",new Vector3(s*11+3,2.75f,-11.3f),new Vector3(.3f,5.5f,.4f),red);
                        for(int z=-9;z<=9;z+=2)Box(details,"Hallen Fassadenrippe",new Vector3(s*15.15f,3.3f,z),new Vector3(.12f,6.6f,.12f),red,false);
                    }
                    var hose=t.Find("SCHLAUCHTURM - technische Kulisse");if(hose!=null){Box(hose,"Rote Turmblende",new Vector3(0,6,-3.14f),new Vector3(.9f,12,.15f),red,false);Cornice(hose,4.4f,6.4f,11.8f,white,2);}
                    break;
            }
        }
        static void ServiceSteelFrame(Transform t,Vector3 p,float width,float depth,float height,Material metal)
        {
            var frame=Group(t,"STAHLRAHMEN",p);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box(frame,"Stuetze",new Vector3(x*width*.5f,height*.5f,z*depth*.5f),new Vector3(.2f,height,.2f),metal);
            foreach(int z in new[]{-1,1})Box(frame,"Quertraeger",new Vector3(0,height,z*depth*.5f),new Vector3(width+.2f,.25f,.2f),metal);
            foreach(int x in new[]{-1,1})Box(frame,"Laengstraeger",new Vector3(x*width*.5f,height,0),new Vector3(.2f,.25f,depth),metal);
        }
    }
}
