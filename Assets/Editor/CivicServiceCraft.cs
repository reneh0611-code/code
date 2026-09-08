using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CheatOnYourDayOnes.EditorTools
{
    public static partial class CivicBuildingsBuilder
    {
        static void CraftService(Transform t,int n,List<Vector3> routes)
        {
            var stone=VMat("Craft_Kalkstein",new Color(.78f,.73f,.63f));var dark=VMat("Craft_Schiefer",new Color(.11f,.14f,.17f));var timber=VMat("Craft_Eiche",new Color(.28f,.18f,.105f));
            var metal=VMat("Craft_Zink",new Color(.33f,.38f,.4f));var copper=VMat("Craft_Patina",new Color(.2f,.34f,.3f));var glass=Mat("Craft_Glas",new Color(.4f,.61f,.68f,.2f),true);
            var g=Group(t,"BAUKULTUR - individuelle Ausarbeitung",Vector3.zero);
            if(n==10)CraftChurch(t,g,stone,dark,copper,glass,routes);
            else if(n==0)
            {
                // Loading shelter is separate from the customer entrance.
                ServiceSteelFrame(g,new Vector3(-11,0,2),5,10,3.6f,metal);
                Box(g,"Seitliche Paketannahme Ueberdachung",new Vector3(-11,3.75f,2),new Vector3(5.5f,.22f,10.5f),metal);
                for(int i=0;i<4;i++)ServiceCounter(g,new Vector3(-11,0,-1+i*2),2,stone,timber);
            }
            else if(n==1)
            {
                // Dormers and profiled eaves distinguish the older shop from modern retail.
                DistrictDormers(g,16,12,4.7f,stone,dark);
                for(float x=-7.5f;x<=7.5f;x+=.55f)Box(g,"Gesimskonsole",new Vector3(x,4.3f,-6.25f),new Vector3(.15f,.3f,.3f),stone,false);
                foreach(int s in new[]{-1,1}){var bay=Group(g,"Vitrinenfassade",new Vector3(s*4.6f,0,-6.4f));Box(bay,"Bruestung",new Vector3(0,.38f,0),new Vector3(4.8f,.75f,.25f),timber);for(int k=-2;k<=2;k++)Box(bay,"Holzkassette",new Vector3(k*.85f,.38f,-.16f),new Vector3(.72f,.5f,.06f),stone,false);}
            }
            else if(n==2)
            {
                foreach(int s in new[]{-1,1})for(int j=0;j<5;j++)Box(g,"Hauseingang Holzlamelle",new Vector3(s*8.3f+2+j*.15f,1.4f,-8.4f),new Vector3(.08f,2.8f,.14f),timber,false);
            }
            else if(n==3)
            {
                foreach(int s in new[]{-1,1})for(int z=-12;z<=12;z+=6){var frame=Group(g,"Tragwerk Detail",new Vector3(s*12.7f,0,z));Box(frame,"Hallenstuetze",Vector3.up*3.8f,new Vector3(.3f,7.6f,.3f),metal);Beam(frame,"Kopfstrebe",new Vector3(0,6,0),new Vector3(-s*1.5f,7.7f,0),.18f,metal);}
                for(int s=-1;s<=1;s+=2)Box(g,"Torpuffer",new Vector3(s*4,.5f,-15.35f),new Vector3(.45f,1,.2f),dark);
            }
            else if(n==4)
            {
                int number=0;foreach(var unit in t.Cast<Transform>().Where(x=>x.name.StartsWith("LAGERBOX")).ToArray())
                {var bay=unit.Find("Box");Box(bay,"Boxnummer Schild",new Vector3(2.55f,2.3f,-4.2f),new Vector3(.6f,.65f,.06f),stone,false);for(int j=0;j<=number%4;j++)Box(bay,"Box Kennzeichnung",new Vector3(2.35f+j*.12f,2.3f,-4.25f),new Vector3(.05f,.3f,.04f),dark,false);number++;}
            }
            else if(n==5)
            {
                var shed=Group(g,"Sammelplatz Dach",new Vector3(12,0,31));ServiceSteelFrame(shed,Vector3.zero,14,14,5.5f,metal);Pitched(shed,"Wellblechdach",15,15,5.5f,1.2f,dark);
            }
            else if(n==6)
            {
                // Freestanding illuminated identification, not another occupied storey.
                var sign=Group(g,"Abschlepphof Pylon",new Vector3(-19,0,-8));Box(sign,"Stele",Vector3.up*3,new Vector3(.45f,6,2),metal);Box(sign,"Schild",new Vector3(0,4.3f,-.15f),new Vector3(3.5f,2,.3f),dark);CityFacadeDetails.Word(sign,"HOF",new Vector3(0,4.1f,-.34f),.5f,stone);
            }
            else if(n==7)
            {
                foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)
                {var tr=Group(g,"Abgesicherter Trafo",new Vector3(s*12,0,12+k*10));foreach(int side in new[]{-1,1})Box(tr,"Trafo Oelwanne",new Vector3(side*3.8f,.3f,0),new Vector3(.25f,.6f,6.8f),stone);for(int j=0;j<3;j++)Box(tr,"Warnplatte",new Vector3(-1.2f+j*1.2f,2.2f,-1.85f),new Vector3(.55f,.35f,.03f),stone,false);}
            }
            else if(n==8)
            {
                foreach(int s in new[]{-1,1})for(int k=0;k<2;k++)
                {var bridge=Group(g,"Beckenbruecken Gelaender",new Vector3(s*12,0,15+k*18));foreach(int side in new[]{-1,1}){Box(bridge,"Handlauf",new Vector3(0,3.65f,side*.48f),new Vector3(13,.06f,.06f),metal,false);for(int j=-6;j<=6;j+=2)Box(bridge,"Pfosten",new Vector3(j,3.05f,side*.48f),new Vector3(.05f,1.2f,.05f),metal,false);}}
            }
            else if(n==9)
            {
                foreach(int s in new[]{-1,1})for(int k=0;k<3;k++)
                {var tank=Group(g,"Tank Armaturen",new Vector3(s*12,0,12+k*11));for(int side=-1;side<=1;side+=2)Box(tank,"Leiterholm",new Vector3(side*.4f,3.7f,-3.62f),new Vector3(.06f,7.4f,.08f),metal,false);Ring(tank,"Ventilrad",new Vector3(0,.9f,-3.8f),.35f,.06f,copper);}
            }
            else if(n==11)
            {
                // A saw-like suspended canopy makes the entrance read as a sports facility.
                for(int j=0;j<9;j++){var fin=Box(g,"Sportportal Lamelle",new Vector3(-10+j*2.5f,4.4f,-11.2f),new Vector3(.12f,.8f,2.2f),metal,false);fin.transform.localRotation=Quaternion.Euler(0,-20,0);}
                Box(g,"Vordach Traeger",new Vector3(0,4.7f,-11.2f),new Vector3(22,.15f,.18f),dark);
            }
            else if(n==12)
            {
                for(int s=-1;s<=1;s+=2)for(int j=0;j<12;j++)Box(g,"Elektronik Aluminiumlamelle",new Vector3(s*(7+j*.075f),2.2f,-7.1f),new Vector3(.035f,4.2f,.15f),metal,false);
                Box(g,"Tiefer Portalhimmel",new Vector3(0,4.05f,-7),new Vector3(15.5f,.12f,2),dark);
            }
            else if(n==13)
            {
                foreach(int s in new[]{-1,1})
                {
                    var surround=Group(g,"Fahrzeugtor Technik",new Vector3(s*11,0,-11));
                    Box(surround,"Torpaket hochgezogen",new Vector3(0,5.95f,1.2f),new Vector3(5.4f,.3f,2.8f),metal);
                    for(int j=0;j<8;j++)Box(surround,"Torlamelle",new Vector3(0,6.13f,.05f+j*.35f),new Vector3(5.4f,.035f,.05f),stone,false);
                }
            }
            TextureServiceWalls(t,n);
        }
        static void CraftChurch(Transform t,Transform detail,Material stone,Material roof,Material copper,Material glass,List<Vector3> routes)
        {
            var old=t.Find("GLOCKENTURM - nicht ausgebaut");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var tower=Group(detail,"RUNDTURM - historische Glockengalerie",new Vector3(-11,0,-9));
            Profile(tower,"Runder Turmschaft",Vector3.zero,new[]{0f,.6f,1f,17f},new[]{3.9f,3.9f,3.45f,3.45f},72,stone);
            var col=tower.gameObject.AddComponent<CapsuleCollider>();col.radius=3.45f;col.height=17;col.center=Vector3.up*8.5f;
            foreach(float y in new[]{1.1f,5.4f,10.2f,16.7f,20.8f})Profile(tower,"Steingesims",new Vector3(0,y,0),new[]{0f,.12f,.24f,.34f},new[]{3.5f,3.72f,3.72f,3.5f},72,stone);
            for(int a=0;a<12;a++)
            {
                float rad=a*Mathf.PI/6;var face=Group(tower,"Turmjoch "+a,new Vector3(3.42f*Mathf.Sin(rad),0,-3.42f*Mathf.Cos(rad)),a*30);
                if(a%2==0)for(int f=0;f<2;f++){Box(face,"Schmales Turmfenster",new Vector3(0,7+f*5,-.02f),new Vector3(.6f,2,.07f),roof,false);Arch(face,new Vector3(0,8+f*5,-.12f),.36f,.1f,.18f,stone,16);}
                Pillar(face,new Vector3(0,17.1f,0),3.6f,.14f,stone);
                Arch(face,new Vector3(.85f,19.3f,0),.8f,.16f,.22f,stone,18);
            }
            Profile(tower,"Glockengeschoss Boden",new Vector3(0,17.1f,0),new[]{0f,.2f},new[]{3.45f,3.45f},72,stone);
            Profile(tower,"Glocke",new Vector3(0,17.7f,0),new[]{0f,.2f,1f,1.3f},new[]{.85f,.85f,.48f,.18f},32,copper);
            Profile(tower,"Hohe kupferne Turmhaube",Vector3.zero,new[]{21f,21.3f,22.3f,24.8f,27.5f,30.5f},new[]{3.9f,3.9f,3.4f,1.65f,.7f,0f},72,copper);
            Box(tower,"Turmkreuz",new Vector3(0,31,0),new Vector3(.1f,1.7f,.1f),copper,false);Box(tower,"Kreuz Querarm",new Vector3(0,31.25f,0),new Vector3(.9f,.1f,.1f),copper,false);
            // Round chancel is a real accessible extension, not a closed decorative cylinder.
            var apse=Group(detail,"RUNDER CHORRAUM",new Vector3(0,0,14));
            for(int i=0;i<36;i++)
            {
                float a=(i+.5f)*Mathf.PI/36;var wall=Group(apse,"Chor Wandsegment",new Vector3(7*Mathf.Cos(a),0,7*Mathf.Sin(a)),-a*Mathf.Rad2Deg-90);
                bool window=i%6==2;
                if(!window)Box(wall,"Chorwand",Vector3.up*3.3f,new Vector3(.63f,6.6f,.3f),stone);
                else{Box(wall,"Fensterbruestung",Vector3.up*.8f,new Vector3(.63f,1.6f,.3f),stone);Box(wall,"Oberer Bogenbereich",Vector3.up*5.6f,new Vector3(.63f,2,.3f),stone);Box(wall,"Chorglas",Vector3.up*3.1f,new Vector3(.63f,3,.025f),glass);}
            }
            for(int z=0;z<28;z++){float depth=(z+.5f)*.25f,ww=2*Mathf.Sqrt(49-depth*depth);Box(apse,"Chorboden",new Vector3(0,-.1f,depth),new Vector3(ww,.2f,.25f),stone);}
            var cap=new SurfaceMesh();for(int i=0;i<36;i++){float a=i*Mathf.PI/36,b=(i+1)*Mathf.PI/36;cap.Quad(new Vector3(7.4f*Mathf.Cos(a),6.7f,7.4f*Mathf.Sin(a)),new Vector3(0,9,0),new Vector3(0,9,0),new Vector3(7.4f*Mathf.Cos(b),6.7f,7.4f*Mathf.Sin(b)));}cap.Save(apse,"Halbkegeldach",roof);
            ServiceRoute(routes,new Vector3(3,0,9),new Vector3(3,0,13));ServiceRoute(routes,new Vector3(3,0,13),new Vector3(0,0,13));ServiceRoute(routes,new Vector3(0,0,13),new Vector3(0,0,19));
            foreach(int s in new[]{-1,1})for(int z=-10;z<=10;z+=5)
            {var buttress=Group(detail,"Historischer Strebepfeilerkopf",new Vector3(s*8.4f,0,z));Pitched(buttress,"Abgeschraegter Pfeilerabschluss",1.2f,1.2f,6,.7f,stone);}
            var main=t.Find("HAUPTGEBAEUDE");Cornice(main,16.6f,28.6f,8.7f,stone,3);
            for(int s=-1;s<=1;s+=2)for(float z=-13;z<=13;z+=.7f)Box(detail,"Romanischer Konsolfries",new Vector3(s*8.22f,8.6f,z),new Vector3(.25f,.3f,.15f),stone,false);
        }
        static void TextureServiceWalls(Transform root,int style)
        {
            const string textures="Assets/GeeZyyGames/SuperMarket/Textures/";
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>().ToArray())
            {
                if(r==null)continue;
                var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null||mf.sharedMesh.vertexCount!=24)continue;
                bool wall=r.name=="Torpfeiler"||r.name=="Wandpfeiler"||r.name=="Sturz"||r.name=="Fensterbruestung"||r.name=="Chorwand"||r.name=="Wand unter Rosenfenster"||r.name=="Wand ueber Rosenfenster";
                if(!wall)continue;
                bool masonry=style==1||style==7||style==8||style==10;string key=masonry?"Craft_Fassadenstein":"Craft_Fassadenputz";
                var mat=Mat(key,masonry?new Color(.85f,.8f,.69f):new Color(.83f,.83f,.79f));
                if(mat.mainTexture==null){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+(masonry?"Modern_Brick_BaseColor.png":"Plaster.png"));EditorUtility.SetDirty(mat);}
                // Metric UVs on each independent part: neither stretched per cube nor merged by colour.
                var mesh=Object.Instantiate(mf.sharedMesh);mesh.name="Modular metric wall";var vs=mesh.vertices;var ns=mesh.normals;var uv=new Vector2[vs.Length];
                for(int i=0;i<vs.Length;i++){var p=root.InverseTransformPoint(r.transform.TransformPoint(vs[i]));var normal=root.InverseTransformDirection(r.transform.TransformDirection(ns[i]));uv[i]=Mathf.Abs(normal.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(normal.x)>.7f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);uv[i]/=masonry?1.8f:3f;}
                mesh.uv=uv;mf.sharedMesh=mesh;r.sharedMaterial=mat;
                // Relief was made for the old flat-colour material; avoid two competing wall skins.
                foreach(var child in r.transform.Cast<Transform>().Where(c=>c.name.StartsWith("Mauerwerk Relief")).ToArray())Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
