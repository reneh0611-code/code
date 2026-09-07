using System;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void BusTerminal(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark)
        {
            var asphalt=Mat("BusAsphalt",new Color(.19f,.205f,.22f));var yellow=Mat("BusStopYellow",new Color(.95f,.8f,.14f));var green=Mat("BusStopGreen",new Color(.04f,.35f,.16f));
            Box(t,"Busbahnhof Fahrflaeche",new Vector3(0,-.08f,0),new Vector3(36,.2f,32),asphalt);
            for(int bay=0;bay<4;bay++)
            {
                float x=-13.5f+bay*9,platform=x+3;
                Group(t,"BUS HALTEPLATZ "+(bay+1),new Vector3(x,0,-2));
                // Flat boarding strip: accessible without a collision step after Y scaling.
                Box(t,"Haltestellen Insel",new Vector3(platform,.035f,-2),new Vector3(1.8f,.03f,17),trim,false);
                for(int i=0;i<12;i++)Box(t,"Taktiler Leitstreifen",new Vector3(platform-.7f,.065f,-9.5f+i*1.3f),new Vector3(.25f,.015f,.9f),yellow,false);
                foreach(int side in new[]{-1,1})Box(t,"Haltebucht Markierung",new Vector3(x+side*1.7f,.04f,-2),new Vector3(.09f,.012f,14),trim,false);
                Box(t,"Wartehallendach",new Vector3(platform,4.7f,-1),new Vector3(2.5f,.2f,8),dark);
                foreach(int end in new[]{-1,1})Box(t,"Wartehallenstuetze",new Vector3(platform+.55f,2.35f,-1+end*3.7f),new Vector3(.12f,4.7f,.12f),dark);
                Box(t,"Wartehalle Glasrueckwand",new Vector3(platform+.65f,2,-1),new Vector3(.035f,3.6f,7.5f),glass);
                Bench(t,new Vector3(platform,0,-2),2,trim,dark);
                Box(t,"Haltestellenmast",new Vector3(platform,2.3f,5.5f),new Vector3(.07f,4.6f,.07f),dark);
                Profile(t,"Haltestellenschild gelb",new Vector3(platform,4.3f,5.5f),new[]{0f,.04f},new[]{.4f,.4f},32,yellow,Quaternion.Euler(90,0,0));
                CityFacadeDetails.Word(t,"H",new Vector3(platform,4.1f,5.43f),.5f,green);
                CityFacadeDetails.Word(t,((char)('A'+bay)).ToString(),new Vector3(platform,3.35f,5.43f),.32f,dark);
                NoticeCase(t,new Vector3(platform,2.2f,5.48f),.65f,dark,trim);
            }
            var terminal=Group(t,"Begehbare Wartehalle",new Vector3(0,0,12.5f));
            Facade(Group(terminal,"Wartehalle Front",new Vector3(0,0,-3.5f)),12,4.5f,3.2f,wall,trim,glass,dark,false,false,2,2.2f,.5f,3.1f);
            Box(terminal,"Wartehalle Rueckwand",new Vector3(0,2.25f,3.5f),new Vector3(12,4.5f,.2f),wall);
            foreach(int side in new[]{-1,1})Box(terminal,"Wartehalle Seitenwand",new Vector3(side*6,2.25f,0),new Vector3(.2f,4.5f,7),wall);
            Box(terminal,"Wartehalle Dach",new Vector3(0,4.65f,0),new Vector3(12.5f,.3f,7.5f),dark);
            Bench(terminal,new Vector3(-3.5f,0,1),3,trim,dark);
            NoticeCase(terminal,new Vector3(3.7f,2,3.35f),2,dark,trim);
            CityFacadeDetails.Word(t,s.sign,new Vector3(0,3.75f,8.8f),.5f,dark);
        }
        static void ValidateExpansion(GameObject root,Spec s)
        {
            if(int.Parse(s.id.Substring(0,2))<48||s.style=="exterior")return;
            float sy=root.transform.lossyScale.y;
            var boxes=root.GetComponentsInChildren<BoxCollider>();
            void Clear(float x,float groundY,float z,float halfWidth=.3f,float head=2)
            {
                foreach(float dx in new[]{-halfWidth,0,halfWidth})foreach(float dy in new[]{.12f,1f,head})
                {
                    var world=root.transform.TransformPoint(new Vector3(x+dx,groundY+dy/sy,z));
                    foreach(var c in boxes)
                    {
                        if(c.isTrigger)continue;var p=c.transform.InverseTransformPoint(world)-c.center;var h=c.size*.5f;
                        if(Mathf.Abs(p.x)<h.x-.001f&&Mathf.Abs(p.y)<h.y-.001f&&Mathf.Abs(p.z)<h.z-.001f)
                            throw new InvalidOperationException(s.id+": Lauf-/Fahrweg blockiert durch "+c.name+" bei "+new Vector3(x,groundY,z));
                    }
                }
            }
            if(s.style=="apartments")
            {
                for(int level=0;level<3;level++)
                {
                    float y=level*4;
                    foreach(int side in new[]{-1,1})
                    {
                        for(float x=0;x<=7;x+=.25f)Clear(side*x,y,-4);
                        for(float z=-4;z<=4;z+=.25f)Clear(side*6,y,z);
                        Clear(side*4.5f,y,4);Clear(side*7,y,4);
                    }
                    for(float z=-4;z<7.2f;z+=.25f)Clear(2,y,z);
                    Clear(0,y,7);Clear(0,y,-2);
                    if(level<2)for(int step=0;step<24;step++)Clear(0,y+(step+1)*4f/24,-1+(step+.5f)*.3f);
                }
            }
            else if(s.style=="garage"||s.style=="showroom")
            {
                foreach(float lane in s.style=="garage"?new[]{-8f,0,8f}:new[]{0f})
                    for(float z=-s.d*.5f-1;z<=4;z+=.25f)Clear(lane,0,z,1.4f,2.4f);
            }
            else if(s.style=="bus")
            {
                for(float z=-15;z<=14;z+=.3f)Clear(0,0,z);
            }
            else for(float z=-s.d*.5f;z<s.d*.3f;z+=.25f)Clear(0,0,z);
        }
    }
}
