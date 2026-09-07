#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only authoring. The resulting prefab has no runtime builder dependency.
public static class CasinoBuildingInstaller
{
    const string Folder = "Assets/Buildings/DayOnesCasino";
    static Material plaster, stone, red, gold, wood, felt, black, white, glow;
    static Transform root;

    [MenuItem("Day Ones/Casino/Build Beside Selected Road")]
    public static void Build()
    {
        var selected = Selection.activeGameObject;
        if (selected == null || !selected.scene.IsValid() || selected.name != "modular kit04 (6)")
        { Debug.LogError("[Casino] Select the requested road: modular kit04 (6). No scene changes made."); return; }
        if (GameObject.Find("Casino_DayOnes") != null)
        { Debug.LogWarning("[Casino] Casino already exists. Select it in Hierarchy to edit it."); return; }
        var mf = selected.GetComponent<MeshFilter>();
        var rr = selected.GetComponent<Renderer>();
        if (mf == null || rr == null) return;
        var bounds = mf.sharedMesh.bounds;
        Vector3[] axes = { selected.transform.TransformVector(Vector3.right * bounds.size.x), selected.transform.TransformVector(Vector3.up * bounds.size.y), selected.transform.TransformVector(Vector3.forward * bounds.size.z) };
        var horizontal = axes.OrderByDescending(v => new Vector2(v.x,v.z).sqrMagnitude).ToArray();
        Vector3 along = horizontal[0]; along.y = 0; along.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, along).normalized;
        float roadWidth = new Vector2(horizontal[1].x,horizontal[1].z).magnitude;
        Vector3 roadCenter = rr.bounds.center;
        var rotation = Quaternion.LookRotation(side, Vector3.up);
        float offset = roadWidth * .5f + 14f;
        Vector3 a = roadCenter + side * offset, b = roadCenter - side * offset;
        if (Obstacles(b) < Obstacles(a)) { side = -side; rotation = Quaternion.LookRotation(side,Vector3.up); }
        Vector3 position = roadCenter + side * offset;
        position.y = rr.bounds.max.y + .03f;
        // Refuse a tree-covered site instead of removing or hiding existing trees.
        if (Obstacles(position) > 0) { Debug.LogError("[Casino] Both roadside plots contain trees or buildings. No scene changes made."); return; }
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        plaster = Mat("Warm plaster", new Color(.73f,.70f,.62f));
        stone = Mat("Dark stone", new Color(.20f,.23f,.25f));
        red = Mat("Burgundy",new Color(.30f,.035f,.055f));
        gold = Mat("Brass",new Color(.70f,.48f,.19f),.55f);
        wood = Mat("Walnut",new Color(.18f,.08f,.035f));
        felt = Mat("Roulette green",new Color(.025f,.24f,.12f));
        black = Mat("Charcoal",new Color(.035f,.04f,.045f));
        white = Mat("Ivory",new Color(.91f,.87f,.72f));
        glow = Mat("Warm light",new Color(1f,.77f,.38f));
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(1f,.64f,.22f)*2f); EditorUtility.SetDirty(glow);
        int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build roadside casino");
        root = new GameObject("Casino_DayOnes").transform;
        root.SetPositionAndRotation(position,rotation);
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Build roadside casino");
        Box("Foundation",new Vector3(0,-.32f,0),new Vector3(24,.64f,18),stone);
        Box("Carpet floor",new Vector3(0,.015f,0),new Vector3(23.2f,.03f,17.2f),red,false);
        Box("Back wall",new Vector3(0,2.8f,8.8f),new Vector3(24,5.6f,.4f),plaster);
        Box("Left wall",new Vector3(-11.8f,2.8f,0),new Vector3(.4f,5.6f,18),plaster);
        Box("Right wall",new Vector3(11.8f,2.8f,0),new Vector3(.4f,5.6f,18),plaster);
        // Two front wall halves leave a genuine 3.6 m entrance, with no invisible collider.
        foreach(int sign in new[]{-1,1})
        {
            Box("Front wall",new Vector3(sign*6.9f,2.8f,-8.8f),new Vector3(10.2f,5.6f,.4f),plaster);
            Box("Front plinth",new Vector3(sign*6.9f,.45f,-9.04f),new Vector3(10.2f,.9f,.15f),stone);
            Box("Entrance jamb",new Vector3(sign*1.95f,1.7f,-9.12f),new Vector3(.3f,3.4f,.45f),gold);
            Box("Facade inset",new Vector3(sign*6.8f,2.5f,-9.05f),new Vector3(4.8f,2.6f,.1f),red,false);
            foreach(float x in new[]{4.5f,9.1f}) Box("Facade pilaster",new Vector3(sign*x,2.7f,-9.16f),new Vector3(.35f,5.4f,.32f),stone);
            Box("Entrance sconce",new Vector3(sign*2.6f,2.55f,-9.25f),new Vector3(.25f,.8f,.25f),glow,false);
        }
        Box("Entrance lintel",new Vector3(0,4.5f,-8.8f),new Vector3(3.6f,2.2f,.4f),plaster);
        Box("Roof",new Vector3(0,5.8f,0),new Vector3(24.6f,.4f,18.6f),stone);
        Box("Interior ceiling",new Vector3(0,5.56f,0),new Vector3(23.2f,.06f,17.2f),plaster,false);
        Box("Sign fascia",new Vector3(0,4.65f,-9.16f),new Vector3(13,1.35f,.25f),red,false);
        Label("CASINO",new Vector3(0,4.68f,-9.32f),Quaternion.identity,.58f,white.color);
        Box("Entrance canopy",new Vector3(0,3.5f,-10.3f),new Vector3(7,.24f,3.5f),stone);
        Box("Canopy light",new Vector3(0,3.36f,-11.7f),new Vector3(6.4f,.06f,.09f),glow,false);
        Box("Walkway",new Vector3(0,-.06f,-11.5f),new Vector3(7,.12f,5),stone);
        Box("Runner",new Vector3(0,.04f,-5.6f),new Vector3(2.6f,.02f,6),red,false);
        for(int i=-1;i<=1;i++)
        {
            Box("Ceiling light",new Vector3(i*6,5.46f,0),new Vector3(2,.08f,3),glow,false);
            var lamp = new GameObject("Warm interior light"); lamp.transform.SetParent(root,false); lamp.transform.localPosition=new Vector3(i*6,4.7f,0);
            var light=lamp.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.82f,.60f); light.intensity=1.6f; light.range=13; light.shadows=LightShadows.None;
        }
        Table();
        // Wall seating leaves wide circulation around the centre table.
        foreach(int sign in new[]{-1,1})
        {
            Box("Lounge seat",new Vector3(sign*9,.45f,3),new Vector3(1.1f,.45f,4.6f),red);
            Box("Lounge back",new Vector3(sign*9.5f,.95f,3),new Vector3(.25f,1.15f,4.6f),red);
            Box("Console",new Vector3(sign*8,.7f,-5),new Vector3(2,1.4f,.65f),wood);
        }
        Box("Cashier desk",new Vector3(0,.7f,6.5f),new Vector3(4,1.4f,1.2f),wood);
        Label("KASSE",new Vector3(0,2.6f,8.55f),Quaternion.identity,.32f,gold.color);
        AssetDatabase.SaveAssets();
        string prefab=Folder+"/Casino_DayOnes.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,prefab,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject=root.gameObject;
        Undo.CollapseUndoOperations(undo);
        Physics.SyncTransforms();
        var entrance=Physics.OverlapBox(root.TransformPoint(new Vector3(0,1.5f,-8.8f)),new Vector3(1.4f,1.1f,.3f),root.rotation).Where(c=>c.transform.IsChildOf(root)).ToArray();
        Debug.Log($"[Casino] Created at {position}, road {selected.name}, road width {roadWidth:F2}m; entrance blockers={entrance.Length}. Interior clear height 5.5m. Decorative roulette table centred. Prefab: {prefab}");
        SceneView.lastActiveSceneView?.LookAt(root.TransformPoint(new Vector3(0,2,0)),root.rotation*Quaternion.Euler(22,155,0),32f);
    }

    static int Obstacles(Vector3 center)
    {
        int score=0;
        foreach(var terrain in Terrain.activeTerrains)
        foreach(var tree in terrain.terrainData.treeInstances)
        {
            Vector3 p=terrain.transform.position+Vector3.Scale(tree.position,terrain.terrainData.size);
            if(new Vector2(p.x-center.x,p.z-center.z).sqrMagnitude<19*19) score++;
        }
        return score;
    }
    static Material Mat(string name,Color color,float metallic=0)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",.24f);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool collider=true)
    {return Part(PrimitiveType.Cube,name,p,size,mat,collider);}
    static GameObject Part(PrimitiveType type,string name,Vector3 p,Vector3 size,Material mat,bool collider=true)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Label(string text,Vector3 p,Quaternion q,float size,Color color)
    {
        var go=new GameObject(text);go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localRotation=q;
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.fontSize=80;tm.characterSize=size/7;tm.color=color;
    }
    static void Table()
    {
        Box("Roulette table pedestal",new Vector3(0,.48f,0),new Vector3(4.2f,.96f,1.45f),wood);
        Box("Roulette padded rail",new Vector3(0,1.02f,0),new Vector3(6.6f,.22f,2.65f),wood);
        Box("Green betting felt",new Vector3(1,1.145f,0),new Vector3(3.95f,.03f,2.2f),felt,false);
        Part(PrimitiveType.Cylinder,"Wheel bowl",new Vector3(-2,1.2f,0),new Vector3(1.85f,.1f,1.85f),gold,false);
        Part(PrimitiveType.Cylinder,"Wheel track",new Vector3(-2,1.31f,0),new Vector3(1.65f,.025f,1.65f),black,false);
        for(int i=0;i<37;i++)
        {
            float angle=i*360f/37; float rad=angle*Mathf.Deg2Rad;
            var pocket=Box("Pocket "+i,new Vector3(-2+Mathf.Sin(rad)*.65f,1.345f,Mathf.Cos(rad)*.65f),new Vector3(.10f,.02f,.25f),i==0?felt:(i%2==0?red:black),false);
            pocket.transform.localRotation=Quaternion.Euler(0,angle,0);
        }
        Part(PrimitiveType.Cylinder,"Wheel centre",new Vector3(-2,1.36f,0),new Vector3(.72f,.06f,.72f),wood,false);
        Part(PrimitiveType.Cylinder,"Spindle",new Vector3(-2,1.5f,0),new Vector3(.12f,.12f,.12f),gold,false);
        Part(PrimitiveType.Sphere,"Roulette ball",new Vector3(-2.74f,1.37f,.1f),Vector3.one*.065f,white,false);
        for(int col=0;col<12;col++)for(int row=0;row<3;row++)
        {
            int n=col*3+row+1; float x=-.7f+col*.295f,z=-.53f+row*.44f;
            Box("Bet "+n,new Vector3(x,1.169f,z),new Vector3(.28f,.008f,.425f),n%2==0?black:red,false);
            Label(n.ToString(),new Vector3(x,1.178f,z),Quaternion.Euler(90,0,0),.105f,Color.white);
        }
        Label("ROULETTE",new Vector3(1,1.18f,.9f),Quaternion.Euler(90,0,0),.16f,white.color);
        foreach(float x in new[]{-1f,.5f,2f})foreach(int side in new[]{-1,1})
        {
            Part(PrimitiveType.Cylinder,"Stool base",new Vector3(x,.08f,side*2.2f),new Vector3(.6f,.08f,.6f),black);
            Part(PrimitiveType.Cylinder,"Stool stem",new Vector3(x,.38f,side*2.2f),new Vector3(.12f,.3f,.12f),gold);
            Part(PrimitiveType.Cylinder,"Stool cushion",new Vector3(x,.7f,side*2.2f),new Vector3(.62f,.1f,.62f),red);
        }
    }

    [MenuItem("Day Ones/Casino/Finish Placement And Validate")]
    public static void Finish()
    {
        root=GameObject.Find("Casino_DayOnes")?.transform; if(root==null)return;
        var road=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(m=>m.name=="modular kit04 (6)").OrderBy(m=>Vector3.Distance(m.GetComponent<Renderer>().bounds.center,root.position)).First();
        var center=road.GetComponent<Renderer>().bounds.center;
        Vector3 originalSide=root.forward;
        var candidates=new System.Collections.Generic.List<Vector3>();
        float distance=new Vector2(root.position.x-center.x,root.position.z-center.z).magnitude;
        foreach(int sign in new[]{-1,1})foreach(float shift in new[]{0f,-8f,8f,-16f,16f})
            candidates.Add(center+originalSide*sign*distance+root.right*shift);
        var oldPosition=root.position;float best=float.PositiveInfinity;Vector3 chosen=oldPosition;Quaternion chosenRotation=root.rotation;
        foreach(var p0 in candidates)
        {
            var p=p0;p.y=oldPosition.y;
            var side=Vector3.Dot(p-center,originalSide)>0?originalSide:-originalSide;
            var rot=Quaternion.LookRotation(side);
            int overlaps=Physics.OverlapBox(p+Vector3.up*.5f,new Vector3(12.4f,1.5f,9.4f),rot).Count(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(root));
            float score=overlaps*1000+Obstacles(p)*10000+Vector3.Distance(p,center);
            if(score<best){best=score;chosen=p;chosenRotation=rot;}
        }
        Undo.RecordObject(root,"Place casino beside road");root.SetPositionAndRotation(chosen,chosenRotation);Physics.SyncTransforms();
        int blockers=Physics.OverlapBox(root.TransformPoint(new Vector3(0,1.5f,-8.8f)),new Vector3(1.3f,1.1f,.3f),root.rotation).Count(c=>c.transform.IsChildOf(root));
        int external=Physics.OverlapBox(root.TransformPoint(new Vector3(0,1,0)),new Vector3(11.5f,.85f,8.5f),root.rotation).Count(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(root));
        int groundHits=0;float worstTerrain=0;
        for(float x=-10;x<=10;x+=5)for(float z=-7;z<=7;z+=3.5f)
        {
            var p=root.TransformPoint(new Vector3(x,0,z));
            if(Physics.Raycast(p+Vector3.up*.4f,Vector3.down,out var hit,1)&&hit.transform.IsChildOf(root))groundHits++;
            foreach(var t in Terrain.activeTerrains){var b=t.transform.position;var s=t.terrainData.size;if(p.x>=b.x&&p.x<=b.x+s.x&&p.z>=b.z&&p.z<=b.z+s.z)worstTerrain=Mathf.Max(worstTerrain,t.SampleHeight(p)+b.y-p.y);}
        }
        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/Casino-Build.md",$"# Roadside casino\n\nScene: Assets/zzz.unity\nPrefab: {Folder}/Casino_DayOnes.prefab\nRoad: modular kit04 (6)\nPosition: {root.position}\n\n24 x 18 m shell, open 3.6 m entrance, 5.5 m interior clearance, solid floor and individual wall colliders, burgundy/plaster/stone exterior. Central decorative roulette table, six stools, lounge seats, cashier counter and warm lighting. No wagering logic.\n\nValidation: entrance blockers {blockers}; external interior colliders {external}; floor ray hits {groundHits}/25; maximum terrain protrusion {worstTerrain:F3} m. Visual Editor checks; no player Play Mode test.\n");
        PrefabUtility.ApplyPrefabInstance(root.gameObject,InteractionMode.AutomatedAction);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log($"[Casino] Validated: entrance blockers {blockers}, external colliders {external}, floor rays {groundHits}/25, terrain protrusion {worstTerrain:F3}m; position {root.position}.");
        Selection.activeGameObject=null;SceneView.lastActiveSceneView?.LookAt(root.TransformPoint(new Vector3(0,2,0)),root.rotation*Quaternion.Euler(16,25,0),27f);
    }
    [MenuItem("Day Ones/Casino/View Interior")]
    public static void Interior()
    {
        root=GameObject.Find("Casino_DayOnes")?.transform;if(root==null)return;
        Selection.activeGameObject=null;
        // Camera sits inside the open entrance, looking at the centre table.
        SceneView.lastActiveSceneView?.LookAt(root.TransformPoint(new Vector3(0,1.65f,-2)),root.rotation*Quaternion.Euler(8,0,0),4f);
    }
}
#endif
