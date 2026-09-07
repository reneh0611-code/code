using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void ShopIdentity(Transform t,Spec s,Material wall,Material trim,Material glass,Material dark)
        {
            var wood=Mat("ShopWalnut",new Color(.29f,.17f,.09f));
            var orange=Mat("ShopOrange",new Color(.85f,.29f,.04f));
            var green=Mat("ShopLeaf",new Color(.16f,.35f,.15f));
            float front=-s.d*.5f;
            foreach(int side in new[]{-1,1})
            {
                Box(t,"Seitlicher Sockel",new Vector3(side*(s.w*.5f+.04f),.22f,0),new Vector3(.12f,.44f,s.d),dark,false);
                Box(t,"Regenfallrohr",new Vector3(side*(s.w*.5f-.12f),s.h*.5f,s.d*.5f+.22f),new Vector3(.09f,s.h,.09f),dark,false);
            }
            switch(s.style)
            {
                case "shop_bakery":
                    Upper(t,"Baecker Wohnhaus",new Vector3(0,s.h+.28f,0),s.w,s.d,3.5f,trim,trim,glass,dark);
                    Pitched(t,"Baecker Ziegeldach",s.w+.6f,s.d+.6f,s.h+4.05f,2.8f,Mat("ShopClay",new Color(.39f,.17f,.09f)));
                    ShopAwning(t,s,new Color(.5f,.24f,.1f),trim,true);
                    DisplayShelves(t,s,wood,trim,3);
                    Box(t,"Backstuben Kamin",new Vector3(-3,s.h+5,2),new Vector3(.7f,3,.7f),wall);
                    break;
                case "shop_butcher":
                    ShopAwning(t,s,new Color(.55f,.045f,.035f),trim,true);
                    foreach(int side in new[]{-1,1})for(int row=0;row<4;row++)
                        Box(t,"Keramik Fassadenfuge",new Vector3(side*(s.w+3.2f)*.25f,.12f+row*.18f,front-.15f),new Vector3((s.w-3.2f)*.5f,.012f,.015f),trim,false);
                    Box(t,"Kuehlthekenbasis",new Vector3(s.w*.28f,.48f,1),new Vector3(2,.96f,3.6f),trim);
                    Box(t,"Kuehltheken Glasaufsatz",new Vector3(s.w*.28f,1.13f,1),new Vector3(2,.35f,3.6f),glass,false);
                    break;
                case "shop_pharmacy":
                    Upper(t,"Apotheke Obergeschoss",new Vector3(-1.5f,s.h+.3f,1),s.w-3,s.d-2,3.5f,wall,trim,glass,dark);
                    var cross=Mat("PharmacyCross",new Color(.08f,.66f,.24f),false,true);
                    Box(t,"Apothekenkreuz senkrecht",new Vector3(-s.w*.33f,2.2f,front-.3f),new Vector3(.35f,1.5f,.14f),cross,false);
                    Box(t,"Apothekenkreuz waagerecht",new Vector3(-s.w*.33f,2.2f,front-.31f),new Vector3(1.5f,.35f,.14f),cross,false);
                    DisplayShelves(t,s,trim,green,4);
                    break;
                case "shop_kiosk":
                    Box(t,"Kiosk Schutzdach",new Vector3(0,s.h+.25f,-.35f),new Vector3(s.w+1.2f,.22f,s.d+1.8f),trim);
                    Box(t,"Zeitungsstaender",new Vector3(-2.5f,.8f,front-.55f),new Vector3(.8f,1.6f,.45f),dark);
                    for(int i=0;i<3;i++)Box(t,"Zeitschriftenfach",new Vector3(-2.5f,.45f+i*.4f,front-.8f),new Vector3(.66f,.23f,.04f),trim,false);
                    DisplayShelves(t,s,wood,orange,2);
                    break;
                case "shop_florist":
                    var pergola=Group(t,"Blumen Pergola",new Vector3(-s.w*.28f,0,front-.8f));
                    foreach(int side in new[]{-1,1})Box(pergola,"Holzpfosten",new Vector3(side*1.5f,1.6f,0),new Vector3(.12f,3.2f,.12f),wood);
                    for(int i=0;i<7;i++)Box(pergola,"Pergola Lamelle",new Vector3(-1.5f+i*.5f,3.2f,0),new Vector3(.10f,.14f,1.6f),wood,false);
                    for(int i=0;i<4;i++)
                    {
                        float x=-s.w*.5f+.65f+i*.62f;
                        Box(t,"Blumentopf",new Vector3(x,.25f,front-.65f),new Vector3(.45f,.5f,.45f),wall);
                        SphereDetail(t,"Bepflanzung",new Vector3(x,.67f,front-.65f),new Vector3(.55f,.65f,.55f),green);
                        SphereDetail(t,"Bluete",new Vector3(x,.97f,front-.65f),Vector3.one*.2f,Mat("FlowerRose",new Color(.85f,.26f,.4f)));
                    }
                    ShopAwning(t,s,new Color(.31f,.46f,.27f),trim,false);
                    break;
                case "shop_barber":
                    for(int i=0;i<8;i++)Box(t,"Barbier Saeulenring",new Vector3(-s.w*.32f,1.6f+i*.1f,front-.3f),new Vector3(.2f,.09f,.22f),i%2==0?trim:Mat("BarberRed",new Color(.6f,.04f,.035f)),false);
                    for(int i=0;i<3;i++)
                    {
                        Box(t,"Friseurstuhl",new Vector3(2,.5f,-2+i*2),new Vector3(.8f,1,.8f),dark);
                        Box(t,"Wandspiegel",new Vector3(s.w*.5f-.16f,1.65f,-2+i*2),new Vector3(.04f,1.4f,.9f),Mat("MirrorSilver",new Color(.59f,.67f,.69f)),false);
                    }
                    Box(t,"Friseur Dachgesims",new Vector3(0,s.h+.3f,front),new Vector3(s.w+.5f,.4f,.7f),trim);
                    break;
                case "shop_fashion":
                    Upper(t,"Boutique Atelier",new Vector3(-2,s.h+.28f,1),s.w-4,s.d-2,4.1f,wall,trim,glass,dark);
                    for(int i=0;i<9;i++)Box(t,"Naturstein Lamelle",new Vector3(-s.w*.5f+.45f+i*.3f,s.h*.5f,front-.23f),new Vector3(.08f,s.h,.28f),trim,false);
                    ShopAwning(t,s,new Color(.12f,.12f,.12f),trim,false);
                    DisplayShelves(t,s,wood,wall,2);
                    break;
                case "shop_electronics":
                    Box(t,"Technik Dachblende",new Vector3(0,s.h+.4f,0),new Vector3(s.w+.6f,.65f,s.d+.6f),dark);
                    var blue=Mat("TechBlue",new Color(.07f,.35f,.8f),false,true);
                    foreach(int side in new[]{-1,1})Box(t,"Elektronik Lichtkante",new Vector3(side*(s.w*.5f-.2f),s.h*.5f,front-.3f),new Vector3(.08f,s.h,.08f),blue,false);
                    for(int i=0;i<4;i++)Box(t,"Bildschirm Ausstellung",new Vector3(s.w*.5f-.2f,1.8f,-4+i*3),new Vector3(.09f,1.1f,1.8f),blue,false);
                    DisplayShelves(t,s,dark,trim,4);
                    break;
                case "shop_bikes":
                    Pitched(t,"Werkstatt Satteldach",s.w+.8f,s.d+.8f,s.h+.28f,2.5f,dark);
                    for(int i=0;i<3;i++)
                    {
                        var rack=Group(t,"Fahrradbuegel",new Vector3(-s.w*.32f+i*.8f,0,front-1));
                        foreach(int side in new[]{-1,1})Box(rack,"Buegelpfosten",new Vector3(0,.45f,side*.45f),new Vector3(.05f,.9f,.05f),dark);
                        Box(rack,"Buegelholm",new Vector3(0,.9f,0),new Vector3(.05f,.05f,.95f),dark);
                    }
                    Box(t,"Werkbank",new Vector3(s.w*.3f,.5f,s.d*.3f),new Vector3(3,1,1.2f),wood);
                    break;
                case "shop_pets":
                    ShopAwning(t,s,new Color(.42f,.5f,.16f),trim,false);
                    DisplayShelves(t,s,wood,green,4);
                    Box(t,"Gruene Dachblende",new Vector3(-s.w*.3f,s.h+.8f,front+.5f),new Vector3(4,1.6f,1),green);
                    break;
                case "shop_hardware":
                    Pitched(t,"Baumarkt Hallendach",s.w+.5f,s.d+.5f,s.h+.28f,2,dark);
                    foreach(int side in new[]{-1,1})Box(t,"Orange Eingangspylon",new Vector3(side*4,2.6f,front-.55f),new Vector3(.5f,5.2f,.6f),orange);
                    Box(t,"Baumarkt Portal",new Vector3(0,5.2f,front-.55f),new Vector3(8.5f,.6f,.6f),orange);
                    for(float z=-s.d*.5f;z<s.d*.5f;z+=.8f)foreach(int side in new[]{-1,1})Box(t,"Baumarkt Blechfalz",new Vector3(side*(s.w*.5f+.15f),s.h*.5f,z),new Vector3(.06f,s.h,.04f),trim,false);
                    DisplayShelves(t,s,dark,orange,6);
                    break;
                case "shop_books":
                    Upper(t,"Buchhaus Obergeschoss",new Vector3(0,s.h+.28f,0),s.w,s.d,3.5f,wall,trim,glass,dark);
                    Pitched(t,"Buchhaus Altstadtdach",s.w+.5f,s.d+.5f,s.h+4.05f,3.3f,dark);
                    Box(t,"Buchhaus Holzgesims",new Vector3(0,s.h+.1f,front-.12f),new Vector3(s.w+.3f,.22f,.35f),wood,false);
                    DisplayShelves(t,s,wood,wall,5);
                    break;
            }
        }
        static void ShopAwning(Transform t,Spec s,Color color,Material trim,bool striped)
        {
            var mat=Mat(s.id+"Canopy",color);
            Box(t,"Ladenmarkise",new Vector3(0,3.2f,-s.d*.5f-.65f),new Vector3(s.w-.5f,.12f,1.4f),mat,false);
            if(striped)for(float x=-s.w*.5f+.4f;x<s.w*.5f-.3f;x+=.65f)
                Box(t,"Markisenstreifen",new Vector3(x,3.27f,-s.d*.5f-.65f),new Vector3(.27f,.015f,1.4f),trim,false);
        }
        static void DisplayShelves(Transform t,Spec s,Material frame,Material goods,int count)
        {
            for(int i=0;i<count;i++)
            {
                float z=-s.d*.22f+i*s.d*.58f/count;
                Box(t,"Warenregal Ruecken",new Vector3(s.w*.5f-.35f,1.1f,z),new Vector3(.12f,2.2f,1.4f),frame);
                for(int level=0;level<3;level++)
                {
                    Box(t,"Regalboden",new Vector3(s.w*.5f-.65f,.35f+level*.65f,z),new Vector3(.7f,.07f,1.4f),frame);
                    for(int item=0;item<3;item++)Box(t,"Warenattrappe",new Vector3(s.w*.5f-.65f,.55f+level*.65f,z-.45f+item*.45f),new Vector3(.3f,.32f,.25f),goods,false);
                }
            }
        }
        static void SphereDetail(Transform t,string name,Vector3 pos,Vector3 size,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(t,false);g.transform.localPosition=pos;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());
        }
    }
}
