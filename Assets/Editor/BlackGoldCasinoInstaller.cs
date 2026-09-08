#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CheatOnYourDayOnes.Casino;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BlackGoldCasinoInstaller
{
    const string Folder="Assets/Buildings/DayOnesCasino/Games";
    static Transform root,parent;
    static Material plaster,stone,red,gold,wood,green,black,ivory,glow,glass;
    static CasinoSession session;
    static readonly List<CasinoStation> stations=new List<CasinoStation>();
    static readonly List<Renderer> bulbs=new List<Renderer>();

    [MenuItem("Day Ones/Casino/Install Black Gold Games")]
    public static void Build()
    {
        if(Application.isPlaying)return;
        var target=GameObject.Find("27_Casino (1)");
        if(target==null||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target)!="Assets/Generated/NeubeckumCity/ARCHITEKTUR V4 - DETAILLIERT/27_Casino.prefab")
        {Debug.LogError("Expected black-gold casino not found. Nothing changed.");return;}
        if(target.transform.Find("Casino_Games")!=null){Debug.Log("Casino games already installed.");return;}
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Buildings/DayOnesCasino","Games");
        root=new GameObject("Casino_Games").transform;root.SetParent(target.transform,false);root.localPosition=Vector3.up*.08f;parent=root;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Install casino games");
        session=root.gameObject.AddComponent<CasinoSession>();stations.Clear();
        red=Mat("Burgundy",new Color(.45f,.02f,.045f));gold=Mat("Brass",new Color(.72f,.46f,.14f),.72f);
        wood=Mat("Walnut",new Color(.16f,.055f,.02f),.15f);green=Mat("Felt",new Color(.016f,.19f,.10f));
        black=Mat("Obsidian",new Color(.016f,.022f,.026f));ivory=Mat("Ivory",new Color(.96f,.91f,.77f));
        glow=Mat("Amber",new Color(1,.65f,.22f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.58f,.15f)*2);
        glass=Mat("Display",new Color(.08f,.15f,.19f),.65f);
        Roulette();CardTable(CasinoGame.Blackjack,new Vector3(4.5f,0,5));
        for(int i=0;i<3;i++)Slot(new Vector3(-10,0,-4+i*3),-90);
        session.stations=stations.ToArray();
        var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/Player.prefab");
        try{if(player.GetComponent<CasinoPlayerGames>()==null)player.AddComponent<CasinoPlayerGames>();PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/Player/Player.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(player);}
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,Folder+"/Casino_Games.prefab",InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject=root.gameObject;
        Debug.Log("Casino installed: European roulette, blackjack and 3 slots. "+VerifyCasinoRules.Run());
    }
    [MenuItem("Day Ones/Casino/Focus Roulette")]
    public static void Focus(){var s=UnityEngine.Object.FindFirstObjectByType<CasinoSession>();if(s!=null)SceneView.lastActiveSceneView?.LookAt(s.stations[0].transform.TransformPoint(new Vector3(0,1.2f,0)),s.transform.rotation*Quaternion.Euler(55,0,0),5);}
    static CasinoStation Station(string name,CasinoGame game,Vector3 position,float yaw=0)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);parent=go.transform;
        var s=go.AddComponent<CasinoStation>();s.session=session;s.game=game;stations.Add(s);
        var view=new GameObject("Player table view").transform;view.SetParent(parent,false);view.localPosition=new Vector3(0,3.2f,-3.1f);view.LookAt(parent.TransformPoint(new Vector3(0,1.05f,0)));s.view=view;
        return s;
    }
    static void Roulette()
    {
        var s=Station("European roulette • single zero",CasinoGame.Roulette,new Vector3(0,0,0));
        Box("Walnut base",new Vector3(0,.5f,0),new Vector3(4.6f,1,1.25f),wood);
        Box("Polished rail",new Vector3(0,1.05f,0),new Vector3(6.5f,.24f,2.65f),wood);
        Box("Felt",new Vector3(.95f,1.18f,0),new Vector3(3.95f,.03f,2.3f),green,false);
        Transform table=parent;
        var assembly=new GameObject("Roulette wheel assembly").transform;assembly.SetParent(table,false);assembly.localPosition=new Vector3(-1.85f,0,0);parent=assembly;
        Ring("Mahogany bowl",Vector3.up*1.23f,1.12f,.13f,wood);
        Ring("Brass rim",Vector3.up*1.33f,1.03f,.025f,gold);
        Ring("Ball race",Vector3.up*1.28f,.94f,.07f,black);
        var rotor=new GameObject("Rotor 37 European pockets").transform;rotor.SetParent(assembly,false);s.wheel=rotor;parent=rotor;
        Part(PrimitiveType.Cylinder,"Rotor base",new Vector3(0,1.25f,0),new Vector3(1.76f,.04f,1.76f),wood,false);
        for(int i=0;i<37;i++)
        {
            int n=CasinoRules.Wheel[i];float angle=i*360f/37;
            Sector("Pocket "+n,.59f,.86f,angle-180f/37,angle+180f/37,1.31f,n==0?green:CasinoRules.IsRed(n)?red:black);
            float rad=angle*Mathf.Deg2Rad;
            var lab=Label(n.ToString(),new Vector3(Mathf.Sin(rad)*.79f,1.322f,Mathf.Cos(rad)*.79f),Quaternion.Euler(90,angle+180,0),.019f,ivory.color);
            float border=(angle-180f/37)*Mathf.Deg2Rad;
            var divider=Box("Pocket divider",new Vector3(Mathf.Sin(border)*.725f,1.337f,Mathf.Cos(border)*.725f),new Vector3(.012f,.025f,.27f),gold,false);divider.transform.localRotation=Quaternion.Euler(0,angle-180f/37,0);
        }
        Part(PrimitiveType.Cylinder,"Conical hub",new Vector3(0,1.36f,0),new Vector3(.87f,.10f,.87f),wood,false);
        Part(PrimitiveType.Cylinder,"Spindle",new Vector3(0,1.52f,0),new Vector3(.10f,.14f,.10f),gold,false);
        Box("Spindle cross",new Vector3(0,1.63f,0),new Vector3(.5f,.05f,.07f),gold,false);Box("Spindle cross",new Vector3(0,1.63f,0),new Vector3(.07f,.05f,.5f),gold,false);
        parent=assembly;s.ball=Part(PrimitiveType.Sphere,"Ivory ball",new Vector3(0,1.34f,.72f),Vector3.one*.06f,ivory,false).transform;
        parent=table;
        for(int c=0;c<12;c++)for(int r=0;r<3;r++)
        {
            int n=c*3+r+1;float x=-.58f+c*.272f,z=-.25f+r*.38f;
            Box("Felt number "+n,new Vector3(x,1.201f,z),new Vector3(.26f,.007f,.365f),CasinoRules.IsRed(n)?red:black,false);
            Label(n.ToString(),new Vector3(x,1.213f,z),Quaternion.Euler(90,0,0),.024f,ivory.color);
        }
        Label("0",new Vector3(-.88f,1.215f,.1f),Quaternion.Euler(90,0,0),.04f,ivory.color);
        for(int i=0;i<3;i++)Label((1+i*12)+"–"+(12+i*12),new Vector3(-.18f+i*1.08f,1.215f,-.65f),Quaternion.Euler(90,0,0),.029f,ivory.color);
        Label("1–18     EVEN     RED     BLACK     ODD     19–36",new Vector3(.95f,1.215f,-.96f),Quaternion.Euler(90,0,0),.021f,ivory.color);
        for(int i=0;i<3;i++)Label("2:1",new Vector3(2.77f,1.215f,-.25f+i*.38f),Quaternion.Euler(90,0,0),.02f,ivory.color);
        Chips(new Vector3(.8f,1.24f,.97f));
        for(int i=0;i<3;i++)Stool(new Vector3(-.3f+i*1.1f,0,-2.2f));
        Label("EUROPEAN ROULETTE",new Vector3(.4f,.8f,-1.34f),Quaternion.identity,.075f,gold.color);
        parent=root;
    }
    static void CardTable(CasinoGame game,Vector3 position)
    {
        Station(game+" table",game,position);bool poker=game==CasinoGame.Poker;
        Part(PrimitiveType.Cylinder,"Pedestal",new Vector3(0,.48f,0),new Vector3(1.2f,.48f,1.2f),wood);
        Part(PrimitiveType.Cylinder,"Oval padded rail",new Vector3(0,1.02f,0),new Vector3(4.3f,.12f,2.8f),wood);
        Part(PrimitiveType.Cylinder,"Oval green felt",new Vector3(0,1.145f,0),new Vector3(3.98f,.02f,2.5f),green,false);
        Label(poker?"FIVE CARD DRAW":"BLACKJACK PAYS 3 : 2",new Vector3(0,1.177f,.15f),Quaternion.Euler(90,0,0),.055f,gold.color);
        for(int i=-1;i<=1;i++)
        {
            Stool(new Vector3(i*1.35f,0,-1.9f));
            for(int k=0;k<2;k++){var card=Box("Playing card",new Vector3(i*1.12f+k*.16f,1.18f,-.65f),new Vector3(.23f,.008f,.33f),ivory,false);card.transform.localRotation=Quaternion.Euler(0,k*15,0);}
        }
        Chips(new Vector3(-.8f,1.18f,.72f));
        Box("Card shoe",new Vector3(1.2f,1.27f,.65f),new Vector3(.35f,.18f,.48f),black,false);parent=root;
    }
    static void Slot(Vector3 p,float yaw)
    {
        var s=Station("Slot machine "+stations.Count,CasinoGame.Slots,p,yaw);
        Box("Cabinet",new Vector3(0,1.1f,0),new Vector3(1.2f,2.2f,.75f),black);
        Box("Brass bezel",new Vector3(0,1.6f,-.4f),new Vector3(1.1f,.92f,.09f),gold,false);
        Box("Display",new Vector3(0,1.6f,-.455f),new Vector3(.97f,.65f,.045f),glass,false);
        s.displays=new TextMesh[3];for(int i=0;i<3;i++)s.displays[i]=Label("7",new Vector3((i-1)*.3f,1.62f,-.49f),Quaternion.identity,.065f,ivory.color).GetComponent<TextMesh>();
        Box("Control deck",new Vector3(0,1.06f,-.56f),new Vector3(1.15f,.15f,.44f),red);
        Part(PrimitiveType.Sphere,"Spin button",new Vector3(.35f,1.17f,-.64f),new Vector3(.16f,.07f,.16f),glow,false);
        Beam("Pull lever",new Vector3(.7f,1.1f,0),new Vector3(.7f,1.75f,-.2f),.07f,gold);
        Part(PrimitiveType.Sphere,"Lever grip",new Vector3(.7f,1.75f,-.2f),Vector3.one*.19f,red,false);
        Label("LUCKY SEVEN",new Vector3(0,2.06f,-.43f),Quaternion.identity,.038f,gold.color);
        Box("Top light",new Vector3(0,2.26f,0),new Vector3(1.23f,.10f,.79f),glow,false);
        Stool(new Vector3(0,0,-1.4f));parent=root;
    }
    static void Desk(Vector3 p)
    {
        Box("Cashier desk",p+Vector3.up*.65f,new Vector3(3.8f,1.3f,1.2f),wood);
        Box("Cashier brass rail",p+Vector3.up*1.33f,new Vector3(3.85f,.1f,1.25f),gold,false);
        Label("KASSE / TESTCHIPS",p+new Vector3(0,.85f,-.62f),Quaternion.identity,.065f,ivory.color);
    }
    static void Chips(Vector3 p){for(int j=0;j<4;j++)for(int k=0;k<6;k++)Part(PrimitiveType.Cylinder,"Casino chip",p+new Vector3(j*.16f,k*.025f,0),new Vector3(.13f,.012f,.13f),j%2==0?red:ivory,false);}
    static void Stool(Vector3 p)
    {
        Part(PrimitiveType.Cylinder,"Stool foot",p+Vector3.up*.07f,new Vector3(.55f,.07f,.55f),black);
        Part(PrimitiveType.Cylinder,"Stool stem",p+Vector3.up*.38f,new Vector3(.09f,.29f,.09f),gold);
        Part(PrimitiveType.Cylinder,"Stool cushion",p+Vector3.up*.73f,new Vector3(.62f,.10f,.62f),red);
    }
    static void Bulb(Vector3 p){bulbs.Add(Part(PrimitiveType.Sphere,"Marquee bulb",p,Vector3.one*.105f,glow,false).GetComponent<Renderer>());}
    static void Light(Vector3 p)
    {
        Box("Ceiling light frame",p+Vector3.up*.27f,new Vector3(1.9f,.07f,1.6f),gold,false);
        Box("Ceiling light diffuser",p+Vector3.up*.22f,new Vector3(1.75f,.05f,1.45f),glow,false);
        var go=new GameObject("Warm salon light");go.transform.SetParent(root,false);go.transform.localPosition=p;
        var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.8f,.56f);l.intensity=1.35f;l.range=9;l.shadows=LightShadows.None;
    }
    static Material Mat(string name,Color color,float metallic=0)
    {
        var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",metallic>0?.65f:.22f);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material m,bool collider=true)=>Part(PrimitiveType.Cube,name,p,size,m,collider);
    static GameObject Part(PrimitiveType type,string name,Vector3 p,Vector3 size,Material m,bool collider=true)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
        if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static GameObject Label(string text,Vector3 p,Quaternion rotation,float size,Color color)
    {
        var go=new GameObject(text);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=rotation;
        var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=90;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;return go;
    }
    static void Beam(string name,Vector3 a,Vector3 b,float thickness,Material m)
    {var g=Box(name,(a+b)*.5f,new Vector3(thickness,(b-a).magnitude,thickness),m);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Ring(string name,Vector3 p,float radius,float tube,Material mat)
    {
        int segments=96,sides=10;var v=new Vector3[(segments+1)*(sides+1)];var triangles=new List<int>();
        for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++)
        {
            float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;int idx=i*(sides+1)+j;
            v[idx]=new Vector3(Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube,Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube));
            if(i<segments&&j<sides){int n=idx+sides+1;triangles.AddRange(new[]{idx,n,idx+1,idx+1,n,n+1});}
        }
        MeshObject(name,v,triangles.ToArray(),p,mat);
    }
    static void Sector(string name,float inner,float outer,float start,float end,float y,Material mat)
    {
        var vertices=new List<Vector3>();for(int i=0;i<=4;i++){float a=Mathf.Lerp(start,end,i/4f)*Mathf.Deg2Rad;vertices.Add(new Vector3(Mathf.Sin(a)*inner,y,Mathf.Cos(a)*inner));vertices.Add(new Vector3(Mathf.Sin(a)*outer,y,Mathf.Cos(a)*outer));}
        var tri=new List<int>();for(int i=0;i<4;i++){int k=i*2;tri.AddRange(new[]{k,k+1,k+2,k+2,k+1,k+3});}MeshObject(name,vertices.ToArray(),tri.ToArray(),Vector3.zero,mat);
    }
    static void MeshObject(string name,Vector3[] vertices,int[] triangles,Vector3 p,Material mat)
    {
        var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
}
#endif
