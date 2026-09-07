using System;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void ApartmentBuilding(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark,Material roof,Material wood)
        {
            var floor=Mat("ApartmentFloor",new Color(.46f,.39f,.3f));
            const float storey=4;
            for(int level=0;level<3;level++)
            {
                float y=level*storey;var g=Group(t,"ETAGE "+(level+1)+" - ZWEI WOHNUNGEN",new Vector3(0,y,0));
                if(level==0)Box(g,"Erdgeschossboden",new Vector3(0,-.08f,0),new Vector3(18,.2f,16),floor);
                else
                {
                    // Two side slabs, front hall and rear landing leave a real stairwell void.
                    foreach(int side in new[]{-1,1})Box(g,"Geschossboden neben Treppenauge",new Vector3(side*5.125f,-.1f,0),new Vector3(7.75f,.2f,16),floor);
                    Box(g,"Vorderes Treppenpodest",new Vector3(0,-.1f,-4.5f),new Vector3(2.5f,.2f,7),floor);
                    Box(g,"Hinteres Treppenpodest",new Vector3(0,-.1f,7.1f),new Vector3(2.5f,.2f,1.8f),floor);
                }
                Facade(Group(g,"Hausfront",new Vector3(0,0,-8)),18,storey,level==0?2.4f:0,wall,trim,glass,dark,false,false,2,1.6f,1.05f,3.05f);
                Facade(Group(g,"Rueckfassade",new Vector3(0,0,8),180),18,storey,0,wall,trim,glass,dark,false,false,2,1.6f,1.05f,3.05f);
                foreach(int side in new[]{-1,1})
                {
                    Facade(Group(g,"Wohnhaus Giebelseite",new Vector3(side*9,0,0),side*90),16,storey,0,wall,trim,glass,dark,false,false,2,1.4f,1.05f,3.05f);
                    InteriorDivider(g,0,-8,8,side*3,2.2f,storey,trim,true,-4);
                    InteriorDivider(g,0,side<0?-9:3,side<0?-3:9,0,1.6f,storey,trim,false,side*6);
                    InteriorDivider(g,0,0,8,side*6,1.6f,storey,trim,true,4);
                    Group(g,"WOHNUNG "+(level*2+(side<0?1:2))+" - EINGANG",new Vector3(side*3,0,-4));
                    Box(g,"Kuechenzeile",new Vector3(side*7,.65f,-6.8f),new Vector3(2.5f,1.3f,.7f),wood);
                    Box(g,"Wohnzimmer Sofa",new Vector3(side*7,.5f,-1.2f),new Vector3(2.2f,1,.75f),dark);
                    Box(g,"Bett",new Vector3(side*7.6f,.4f,6.2f),new Vector3(1.5f,.8f,2.2f),trim);
                    Box(g,"Waschbecken",new Vector3(side*4.4f,.9f,6.8f),new Vector3(.8f,.25f,.65f),trim);
                    Box(g,"Duschwanne",new Vector3(side*4.3f,.12f,1.3f),new Vector3(1.1f,.24f,1.1f),trim);
                    if(level>0)
                    {
                        Beam(g,"Treppenauge Handlauf",new Vector3(side*1.3f,1.3f,-.9f),new Vector3(side*1.3f,1.3f,6.15f),.08f,dark);
                        g.GetChild(g.childCount-1).gameObject.AddComponent<BoxCollider>();
                        for(int post=0;post<10;post++)Box(g,"Treppengelaender Pfosten",new Vector3(side*1.3f,.65f,-.8f+post*.75f),new Vector3(.07f,1.3f,.07f),dark);
                    }
                }
                if(level<2)
                {
                    for(int step=0;step<24;step++)
                    {
                        float top=(step+1)*storey/24,z=-1+(step+.5f)*.3f;
                        Box(g,"Treppe Stufe "+(step+1),new Vector3(0,top-.09f,z),new Vector3(2,.18f,.3f),trim);
                    }
                    foreach(int side in new[]{-1,1})
                    {
                        Beam(g,"Treppenhandlauf",new Vector3(side*1.07f,1.3f,-1),new Vector3(side*1.07f,5.3f,6.2f),.09f,dark);
                        g.GetChild(g.childCount-1).gameObject.AddComponent<BoxCollider>();
                        for(int step=0;step<24;step+=3)Box(g,"Treppenpfosten",new Vector3(side*1.07f,(step+1)*storey/24+.65f,-1+(step+.5f)*.3f),new Vector3(.06f,1.3f,.06f),dark);
                    }
                }
                Cornice(g,18,16,3.95f,trim,1);
            }
            Pitched(t,"Wohnhaus Schieferdach",18.6f,16.6f,12,2.7f,roof);
            FramedGlazing(t,trim);Masonry(t,s);RoofCourses(t);
            GlassCanopy(t,new Vector3(0,3.25f,-8.35f),3.5f,1.6f,dark);
        }
        static void ExteriorVariant(Transform t,Spec s,Material wall,Material trim,Material dark,Material roof)
        {
            // The same palette, sill proportions and balconies as the established exterior homes.
            int floors=s.id=="65_HofBungalow"?1:s.id=="61_Klinkerblock"||s.id=="66_Eckwohnhaus"?3:2;
            float h=floors*3.1f;var glass=Mat("ExteriorResidentialGlass",new Color(.09f,.16f,.20f));
            Box(t,"Nicht begehbarer Wohnbau",new Vector3(0,h*.5f,0),new Vector3(s.w,h,s.d),wall);
            for(int level=0;level<floors;level++)
            {
                foreach(int face in new[]{-1,1})foreach(float frac in new[]{-.32f,0,.32f})
                {
                    if(level==0&&face==-1&&frac==0)continue;
                    var q=Group(t,"Wohnfenster",new Vector3(s.w*frac,level*3.1f+1.8f,face*(s.d*.5f+.04f)),face<0?0:180);
                    Box(q,"Fensterrahmen",Vector3.zero,new Vector3(1.62f,1.92f,.16f),trim,false);
                    Box(q,"Fensterglas",new Vector3(0,0,-.1f),new Vector3(1.4f,1.7f,.04f),glass,false);
                    Box(q,"Mittelpfosten",new Vector3(0,0,-.14f),new Vector3(.055f,1.7f,.05f),dark,false);
                    Box(q,"Fensterbank",new Vector3(0,-.94f,-.12f),new Vector3(1.75f,.1f,.35f),trim,false);
                }
                if(level>0)Cornice(t,s.w,s.d,level*3.1f,trim,1);
            }
            Box(t,"Geschlossene Haustuer",new Vector3(0,1.25f,-s.d*.5f-.1f),new Vector3(1.4f,2.5f,.18f),dark);
            Pitched(t,"Wohnhaus Dach",s.w+.7f,s.d+.7f,h,2.2f,roof);
            if(floors>1)foreach(int side in new[]{-1,1})
            {
                float x=side*s.w*.32f;
                Box(t,"Balkonplatte",new Vector3(x,3.2f,-s.d*.5f-.7f),new Vector3(2.5f,.16f,1.4f),trim);
                Box(t,"Balkongelaender",new Vector3(x,4.25f,-s.d*.5f-1.35f),new Vector3(2.5f,.06f,.06f),dark,false);
                for(int i=0;i<9;i++)Box(t,"Balkonstab",new Vector3(x-1.2f+i*.3f,3.75f,-s.d*.5f-1.35f),new Vector3(.035f,1,.035f),dark,false);
            }
            float garden=s.id=="63_Doppelhaus"?18:s.id=="64_GartenVilla"?22:s.id=="65_HofBungalow"?7:s.id=="67_Stadtvilla"?5:0;
            if(garden>0)
            {
                float width=s.w+4,back=s.d*.5f;
                Box(t,"Gartenflaeche",new Vector3(0,-.055f,back+garden*.5f),new Vector3(width,.12f,garden),Mat("ResidentialGrass",new Color(.27f,.4f,.23f)));
                Fence(t,new Vector3(0,0,back+garden),width,0,trim,dark);
                foreach(int side in new[]{-1,1})Fence(t,new Vector3(side*width*.5f,0,back+garden*.5f),garden,90,trim,dark);
                Box(t,"Terrasse",new Vector3(0,.03f,back+1.6f),new Vector3(5,.08f,3.2f),trim);
                if(garden>10)for(int i=0;i<3;i++)Planter(t,new Vector3(-width*.3f+i*width*.3f,0,back+garden-1),1.8f,trim,Mat("ResidentialHedge",new Color(.16f,.29f,.17f)));
            }
            RoofCourses(t);
        }
    }
}
