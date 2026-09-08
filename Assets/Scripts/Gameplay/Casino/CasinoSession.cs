using System;
using System.Collections.Generic;
using System.Linq;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Casino
{
    // Client presentation; authoritative results and wallet settlement live on the player.
    public sealed class CasinoSession : MonoBehaviour
    {
        public CasinoStation[] stations;
        public bool IsOpen=>station!=null;
        public long Chips => player!=null&&player.Wallet!=null?player.Wallet.Cash.Value:0;
        CasinoPlayerGames games; int lastRound=-1;
        readonly List<CasinoRules.Bet> bets=CasinoRules.Bets();
        readonly Dictionary<int,int> placed=new Dictionary<int,int>();
        readonly List<Behaviour> suspended=new List<Behaviour>();
        readonly List<int> hand=new List<int>(),dealer=new List<int>();
        readonly bool[] holds=new bool[5];
        readonly List<int> history=new List<int>();
        PlayerAgent player;
        NetworkPlayerController movement;
        Camera cameraView;
        CasinoStation station,nearby;
        CursorLockMode oldCursor;
        bool oldCursorVisible,oldMovementLock;
        Vector3 oldCameraPosition;
        Quaternion oldCameraRotation;
        bool oldOrthographic;
        float oldOrthographicSize;
        int chip=10,selectedBet=49,roundStake,winningNumber;
        int[] reels=new int[3];
        float spinTime,scanAt;
        bool spinning,cardRound;
        string status="Willkommen. Nur Spielgeld – Einsätze werden vom Bargeld abgezogen.";
        Vector2 betScroll;
        Vector2 guiOffset;
        GUIStyle title,label,button,small;

        
        void Update()
        {
            if(station!=null)
            {
                if(player==null){Close();return;}
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                if(spinning)AnimateSpin();
                if(Keyboard.current?.escapeKey.wasPressedThisFrame==true&&!spinning)Close();
                return;
            }
            if(Time.unscaledTime>=scanAt)
            {
                scanAt=Time.unscaledTime+.2f;nearby=null;
                var manager=NetworkManager.Singleton;
                player=manager!=null&&manager.IsListening?manager.LocalClient?.PlayerObject?.GetComponent<PlayerAgent>():null;
                if(player==null||!player.IsOwner||Cursor.lockState!=CursorLockMode.Locked)return;
                float best=3.6f;
                foreach(var s in stations)
                {
                    if(s==null)continue;float d=Vector3.Distance(player.transform.position,s.transform.position);
                    if(d>=best||Mathf.Abs(player.transform.position.y-s.transform.position.y)>1.5f)continue;
                    Vector3 a=player.transform.position+Vector3.up*1.35f,b=s.transform.position+Vector3.up*1.3f;
                    if(Physics.Linecast(a,b,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(s.transform)&&!hit.transform.IsChildOf(player.transform))continue;
                    nearby=s;best=d;
                }
            }
            if(nearby!=null&&Keyboard.current?.eKey.wasPressedThisFrame==true)Open(nearby,player);
        }
        public void Open(CasinoStation next,PlayerAgent agent)
        {
            if(IsOpen||next==null||agent==null||!agent.IsOwner)return;
            cameraView=agent.GetComponentInChildren<Camera>();if(cameraView==null)cameraView=Camera.main;
            if(cameraView==null)return;
            games=agent.GetComponent<CasinoPlayerGames>();
            if(games==null||!games.IsSpawned)return;
            if(games.State.phase=="spin"&&games.State.station!=Array.IndexOf(stations,next))return;
            player=agent;station=next;placed.Clear();cardRound=false;spinning=false;
            games.Changed+=Receive;
            games.Request(Array.IndexOf(stations,next),"open");
            if(games.State.station==Array.IndexOf(stations,next))Receive(games.State);
            status="Spielgeld. Einsatz wählen und Spiel starten.";
            oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;
            oldCameraPosition=cameraView.transform.position;oldCameraRotation=cameraView.transform.rotation;
            oldOrthographic=cameraView.orthographic;oldOrthographicSize=cameraView.orthographicSize;
            movement=agent.GetComponent<NetworkPlayerController>();oldMovementLock=movement!=null&&movement.IsCombatMovementLocked;
            movement?.SetCombatMovementLocked(true);
            Suspend(agent.GetComponent<PlayerInteractor>());Suspend(agent.GetComponent<PlayerMeleeCombat>());
            Suspend(agent.GetComponent<MeleeAnimationBridge>());Suspend(agent.GetComponent<VehicleInteractor>());
            Suspend(cameraView.GetComponent<ThirdPersonCamera>());
            if(next.view!=null)cameraView.transform.SetPositionAndRotation(next.view.position,next.view.rotation);
            if(next.game==CasinoGame.Roulette||next.game==CasinoGame.Blackjack)
            {
                cameraView.transform.SetPositionAndRotation(next.transform.TransformPoint(new Vector3(0,5.5f,-.5f)),next.transform.rotation*Quaternion.Euler(90,0,0));
                cameraView.orthographic=true;cameraView.orthographicSize=next.game==CasinoGame.Roulette?2.9f:2.4f;
            }
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Suspend(Behaviour b){if(b!=null&&b.enabled){suspended.Add(b);b.enabled=false;}}
        public void Close()
        {
            if(!IsOpen)return;
            // Server resolves committed rounds even when the panel closes.
            if(games!=null){games.Request(Array.IndexOf(stations,station),"leave");games.Changed-=Receive;}
            
            foreach(var b in suspended)if(b!=null)b.enabled=true;suspended.Clear();
            if(movement!=null)movement.SetCombatMovementLocked(oldMovementLock);
            if(cameraView!=null){cameraView.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);cameraView.orthographic=oldOrthographic;cameraView.orthographicSize=oldOrthographicSize;}
            Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
            station=null;nearby=null;placed.Clear();
        }
        void OnDisable(){Close();}
        void Receive(CasinoSnapshot snapshot)
        {
            if(!IsOpen||snapshot.station!=Array.IndexOf(stations,station))return;
            status=snapshot.message;roundStake=snapshot.stake;
            hand.Clear();hand.AddRange(snapshot.hand);dealer.Clear();dealer.AddRange(snapshot.dealer);
            cardRound=snapshot.phase=="cards";
            winningNumber=snapshot.winner;
            if(snapshot.reels.Length==3)reels=snapshot.reels;
            if(snapshot.phase=="spin") { if(lastRound!=snapshot.round){spinTime=0;lastRound=snapshot.round;}spinning=true; }
            else if(snapshot.phase=="settled")
            {
                spinTime=6;AnimateSpin();spinning=false;
                if(station.game==CasinoGame.Roulette&&!history.Any()||station.game==CasinoGame.Roulette&&lastRound==snapshot.round)
                {history.Insert(0,winningNumber);if(history.Count>10)history.RemoveAt(10);lastRound=-1;}
                placed.Clear();
            }
        }
        int TotalBet()=>placed.Values.Sum();
        void AddBet(int index)
        {
            if(spinning)return;if(TotalBet()+chip>Chips){status="Nicht genügend Spielgeld.";return;}
            placed.TryGetValue(index,out int amount);if(amount+chip>1000||TotalBet()+chip>10000)return;placed[index]=amount+chip;
        }
        void Spin()
        {
            if(spinning)return;
            games.Request(Array.IndexOf(stations,station),station.game==CasinoGame.Roulette?"roulette":"slots",chip,placed.Keys.ToArray(),placed.Values.ToArray());
        }
        void AnimateSpin()
        {
            spinTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(spinTime/6),ease=1-Mathf.Pow(1-t,3);
            if(station.game==CasinoGame.Roulette&&station.wheel!=null&&station.ball!=null)
            {
                float wheelAngle=1440*ease+137*ease;
                station.wheel.localRotation=Quaternion.Euler(0,wheelAngle,0);
                int index=Array.IndexOf(CasinoRules.Wheel,winningNumber);
                float end=137+index*360f/37;
                float angle=Mathf.Lerp(1800,end,ease)*Mathf.Deg2Rad;
                float radius=Mathf.Lerp(.96f,.72f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,t)));
                station.ball.localPosition=new Vector3(Mathf.Sin(angle)*radius,1.34f+Mathf.Sin(t*Mathf.PI)*.08f,Mathf.Cos(angle)*radius);
            }
            if(station.game==CasinoGame.Slots&&station.displays!=null)
                for(int i=0;i<station.displays.Length;i++)station.displays[i].text=t<.72f+i*.09f?Symbols[(int)(spinTime*15+i)%6]:Symbols[reels[i]];
        }
        static readonly string[] Symbols={"7","BAR","★","K","Q","A"};
        void Deal()=>games.Request(Array.IndexOf(stations,station),"deal",chip);
        void Hit()=>games.Request(Array.IndexOf(stations,station),"hit");
        void Stand()=>games.Request(Array.IndexOf(stations,station),"stand");
        void Double()=>games.Request(Array.IndexOf(stations,station),"double");
        void DrawPoker(){}
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold};title.normal.textColor=new Color(1,.81f,.42f);
            label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};
            small=new GUIStyle(label){fontSize=13};button=new GUIStyle(GUI.skin.button){fontSize=16};
        }
        void OnGUI()
        {
            Styles();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);var prior=GUI.matrix;
            guiOffset=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f);
            GUI.matrix=Matrix4x4.TRS(guiOffset,Quaternion.identity,Vector3.one*scale);
            if(!IsOpen){if(nearby!=null)GUI.Box(new Rect(370,720,540,45),"[E] "+nearby.game+" spielen · Spielgeld",button);GUI.matrix=prior;return;}
            if(station.game==CasinoGame.Roulette){PhysicalRouletteGUI(scale);GUI.matrix=prior;return;}
            if(station.game==CasinoGame.Blackjack){BlackjackView(scale);GUI.matrix=prior;return;}
            // Keep upper third clear for the animated physical table/wheel.
            DrawPanel(new Rect(15,260,1250,525));
            GUILayout.BeginArea(new Rect(30,270,1220,505));
            GUILayout.BeginHorizontal();GUILayout.Label("CASINO / "+station.game.ToString().ToUpper(),title);
            GUILayout.FlexibleSpace();GUILayout.Label($"{Chips} €",title);
            GUI.enabled=!spinning;if(GUILayout.Button("Verlassen [Esc]",button,GUILayout.Width(160)))Close();GUI.enabled=true;GUILayout.EndHorizontal();
            if(!IsOpen){GUILayout.EndArea();GUI.matrix=prior;return;}
            GUILayout.Label(status,label,GUILayout.Height(45));
            GUILayout.BeginHorizontal();GUILayout.Label("Einsatz",label,GUILayout.Width(80));
            GUI.enabled=!spinning&&!cardRound;
            foreach(int value in new[]{10,20,50,100})if(GUILayout.Button((value==chip?"● ":"")+value,button,GUILayout.Width(85)))chip=value;
            GUI.enabled=true;GUILayout.EndHorizontal();
            if(station.game==CasinoGame.Roulette)RouletteGUI();
            else if(station.game==CasinoGame.Slots)SlotsGUI();
            else CardsGUI();
            GUILayout.EndArea();GUI.matrix=prior;
        }
        void RouletteGUI()
        {
            GUI.enabled=!spinning;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(780));
            for(int row=2;row>=0;row--)
            {
                GUILayout.BeginHorizontal();
                if(row==2)NumberButton(0,46);else GUILayout.Space(50);
                for(int col=0;col<12;col++)NumberButton(col*3+row+1,55);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();for(int i=37;i<43;i++)if(GUILayout.Button(bets[i].Name,button,GUILayout.Width(117),GUILayout.Height(32)))AddBet(i);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();for(int i=43;i<49;i++)if(GUILayout.Button(bets[i].Name,button,GUILayout.Width(117),GUILayout.Height(32)))AddBet(i);GUILayout.EndHorizontal();
            GUILayout.Label("Klick setzt Chips. 0 verliert bei Rot/Schwarz, Gerade/Ungerade und 1–18/19–36.\nEinfachnull · kein La Partage · Zahl 35:1 · Split 17:1 · Straße 11:1 · Carré 8:1 · Sechs 5:1",small);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(410));
            GUILayout.Label("Weitere Innenwetten",label);
            betScroll=GUILayout.BeginScrollView(betScroll,GUILayout.Height(120));
            for(int i=49;i<bets.Count;i++)if(GUILayout.Button((selectedBet==i?"● ":"")+bets[i].Name,small))selectedBet=i;
            GUILayout.EndScrollView();
            if(GUILayout.Button("Ausgewählte Wette setzen",button)&&selectedBet>=49)AddBet(selectedBet);
            GUILayout.EndVertical();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.Label($"Gesetzt: {TotalBet()}  ·  Wetten: {placed.Count}",label);
            if(GUILayout.Button("Zurücksetzen",button,GUILayout.Width(155)))placed.Clear();
            GUI.enabled=!spinning&&TotalBet()>0;if(GUILayout.Button("RAD DREHEN",button,GUILayout.Width(210),GUILayout.Height(38)))Spin();GUI.enabled=true;GUILayout.EndHorizontal();
            GUILayout.Label("Letzte Zahlen: "+string.Join("  ·  ",history),small);
        }
        void PhysicalRouletteGUI(float scale)
        {
            GUI.enabled=!spinning;
            // Thin split/corner hit areas take precedence over number interiors.
            for(int col=0;col<12;col++)for(int row=0;row<3;row++)
            {
                int n=col*3+row+1;float x=-.58f+col*.272f,z=-.25f+row*.38f;
                if(col<11&&row<2)CoverageButton(new[]{n,n+1,n+3,n+4},new Vector3(x+.136f,1.22f,z+.19f),.065f,.07f,scale);
                if(row<2)CoverageButton(new[]{n,n+1},new Vector3(x,1.22f,z+.19f),.20f,.065f,scale);
                if(col<11)CoverageButton(new[]{n,n+3},new Vector3(x+.136f,1.22f,z),.06f,.30f,scale);
            }
            for(int i=0;i<37;i++)
            {
                float angle=(station.wheel.localEulerAngles.y+i*360f/37)*Mathf.Deg2Rad;
                var center=station.wheel.parent.localPosition+new Vector3(Mathf.Sin(angle)*.79f,1.34f,Mathf.Cos(angle)*.79f);
                FeltButton(CasinoRules.Wheel[i],center,.105f,.105f,scale);
            }
            for(int col=0;col<12;col++)for(int row=0;row<3;row++)
                FeltButton(col*3+row+1,new Vector3(-.58f+col*.272f,1.22f,-.25f+row*.38f),.26f,.365f,scale);
            FeltButton(0,new Vector3(-.88f,1.22f,.1f),.27f,1.14f,scale);
            for(int i=0;i<3;i++)
            {
                FeltButton(43+i*2,new Vector3(-.18f+i*1.08f,1.22f,-.65f),1.05f,.30f,scale);
                FeltButton(44+i*2,new Vector3(2.77f,1.22f,-.25f+i*.38f),.28f,.365f,scale);
            }
            int[] outside={41,39,37,38,40,42};
            for(int i=0;i<6;i++)FeltButton(outside[i],new Vector3(-.4f+i*.54f,1.22f,-.96f),.51f,.25f,scale);
            GUI.enabled=true;
            DrawPanel(new Rect(12,598,1256,190));
            GUILayout.BeginArea(new Rect(26,605,1228,175));
            GUILayout.BeginHorizontal();GUILayout.Label(status,label);GUILayout.FlexibleSpace();GUILayout.Label(Chips+" €",title);
            if(GUILayout.Button("Verlassen [Esc]",button,GUILayout.Width(165)))Close();GUILayout.EndHorizontal();
            if(!IsOpen){GUILayout.EndArea();return;}
            GUI.enabled=!spinning;
            GUILayout.BeginHorizontal();foreach(int value in new[]{10,20,50,100})if(GUILayout.Button((chip==value?"● ":"")+value+" €",button,GUILayout.Width(85)))chip=value;
            GUILayout.Label("Auf dem Tisch klicken · Gesetzt: "+TotalBet()+" €",label);
            if(GUILayout.Button("Leeren",button,GUILayout.Width(85)))placed.Clear();
            GUI.enabled=!spinning&&TotalBet()>0;
            if(GUILayout.Button(spinning?"Kugel rollt …":"DREHEN",button,GUILayout.Width(180),GUILayout.Height(40)))Spin();
            GUILayout.EndHorizontal();GUI.enabled=!spinning;
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("◀",button,GUILayout.Width(40)))selectedBet=selectedBet<=49?bets.Count-1:selectedBet-1;
            GUILayout.Label(bets[selectedBet].Name,small,GUILayout.Width(200));
            if(GUILayout.Button("▶",button,GUILayout.Width(40)))selectedBet=selectedBet>=bets.Count-1?49:selectedBet+1;
            if(GUILayout.Button("Innenwette setzen",button,GUILayout.Width(175)))AddBet(selectedBet);
            GUILayout.Label("Letzte Zahlen: "+string.Join(" · ",history),small);
            GUILayout.EndHorizontal();GUI.enabled=true;
            GUILayout.Label("Zahl 35:1 · Split 17:1 · Straße 11:1 · Carré 8:1 · Sechs 5:1 · Dutzend/Kolonne 2:1 · Außen 1:1 · 0 verliert Außenwetten",small);
            GUILayout.EndArea();
        }
        void CoverageButton(int[] numbers,Vector3 center,float width,float depth,float scale)
        {
            int id=bets.FindIndex(b=>b.Numbers.Length==numbers.Length&&numbers.All(n=>b.Numbers.Contains(n)));
            if(id>=0)FeltButton(id,center,width,depth,scale);
        }
        void DrawPanel(Rect rect)
        {
            var c=GUI.color;GUI.color=new Color(.015f,.23f,.16f,.88f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=new Color(.86f,.63f,.25f,.95f);GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,3),Texture2D.whiteTexture);
            GUI.color=new Color(.35f,.045f,.11f,.88f);GUI.DrawTexture(new Rect(rect.x,rect.y+3,rect.width,42),Texture2D.whiteTexture);GUI.color=c;
        }
        void BlackjackView(float scale)
        {
            DrawCardRow(dealer,.52f,scale);
            DrawCardRow(hand,-.65f,scale);
            DrawPanel(new Rect(120,570,1040,210));
            GUILayout.BeginArea(new Rect(140,580,1000,190));
            GUILayout.BeginHorizontal();GUILayout.Label(status,label);GUILayout.FlexibleSpace();GUILayout.Label(Chips+" €",title);
            if(GUILayout.Button("Verlassen [Esc]",button,GUILayout.Width(160)))Close();GUILayout.EndHorizontal();
            if(!IsOpen){GUILayout.EndArea();return;}
            GUILayout.Label("Blackjack 3:2 · Dealer steht auf 17 · Ass zählt 1 oder 11",small);
            GUILayout.BeginHorizontal();GUI.enabled=!cardRound;
            foreach(int value in new[]{10,20,50,100})if(GUILayout.Button((chip==value?"● ":"")+value+" €",button,GUILayout.Width(90)))chip=value;
            GUI.enabled=true;GUILayout.Label(hand.Count>0?"Deine Punkte: "+CasinoRules.BlackjackTotal(hand):"Einsatz wählen",label);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(!cardRound){GUI.enabled=Chips>=chip;if(GUILayout.Button("KARTEN GEBEN",button,GUILayout.Height(44)))Deal();}
            else
            {
                if(GUILayout.Button("KARTE",button,GUILayout.Height(44)))Hit();
                if(GUILayout.Button("STEHEN",button,GUILayout.Height(44)))Stand();
                GUI.enabled=cardRound&&hand.Count==2&&Chips>=roundStake;
                if(GUILayout.Button("VERDOPPELN",button,GUILayout.Height(44)))Double();
            }
            GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.EndArea();
        }
        void DrawCardRow(List<int> cards,float z,float scale)
        {
            for(int i=0;i<cards.Count;i++)
            {
                var p=cameraView.WorldToScreenPoint(station.transform.TransformPoint(new Vector3((i-(cards.Count-1)*.5f)*.42f,1.2f,z)));
                var rect=new Rect((p.x-guiOffset.x)/scale-25,(Screen.height-p.y-guiOffset.y)/scale-36,50,72);
                var c=GUI.color;GUI.color=cards[i]<0?new Color(.45f,.035f,.11f):new Color(1,.97f,.86f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
                GUI.color=cards[i]>=0&&(cards[i]/13==1||cards[i]/13==2)?new Color(.75f,.02f,.04f):Color.black;
                GUI.Label(new Rect(rect.x+5,rect.y+6,48,58),cards[i]<0?"◆":CasinoRules.Card(cards[i]),label);GUI.color=c;
            }
        }
        void FeltButton(int id,Vector3 center,float width,float depth,float scale)
        {
            var a=cameraView.WorldToScreenPoint(station.transform.TransformPoint(center+new Vector3(-width*.5f,0,-depth*.5f)));
            var b=cameraView.WorldToScreenPoint(station.transform.TransformPoint(center+new Vector3(width*.5f,0,depth*.5f)));
            var rect=new Rect((Mathf.Min(a.x,b.x)-guiOffset.x)/scale,(Screen.height-Mathf.Max(a.y,b.y)-guiOffset.y)/scale,Mathf.Abs(a.x-b.x)/scale,Mathf.Abs(a.y-b.y)/scale);
            if(rect.Contains(Event.current.mousePosition)&&GUI.enabled)
            {
                var c=GUI.color;GUI.color=new Color(1,.8f,.2f,.35f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=c;
                GUI.Label(new Rect(25,20,500,30),bets[id].Name+" · "+chip+" € setzen",label);
            }
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))AddBet(id);
            if(placed.TryGetValue(id,out int amount))
            {
                var c=GUI.color;GUI.color=new Color(.85f,.61f,.16f);GUI.DrawTexture(new Rect(rect.center.x-15,rect.center.y-12,30,24),Texture2D.whiteTexture);GUI.color=Color.black;
                GUI.Label(new Rect(rect.center.x-15,rect.center.y-12,40,24),amount.ToString(),small);GUI.color=c;
            }
        }
        void NumberButton(int n,int width)
        {
            Color old=GUI.backgroundColor;GUI.backgroundColor=n==0?new Color(.1f,.7f,.4f):CasinoRules.IsRed(n)?new Color(.85f,.2f,.2f):new Color(.28f,.3f,.35f);
            string suffix=placed.TryGetValue(n,out int amount)?"\n"+amount:"";
            if(GUILayout.Button(n+suffix,button,GUILayout.Width(width),GUILayout.Height(42)))AddBet(n);GUI.backgroundColor=old;
        }
        void SlotsGUI()
        {
            GUILayout.Space(20);GUILayout.Label(string.Join("     |     ",reels.Select(i=>Symbols[i])),title,GUILayout.Height(55));
            GUILayout.Label("3 × 7: 20× Einsatz · andere Drillinge: 5× · genau ein Paar: 1×\nAlle Auszahlungen inklusive Einsatz. Sechs gleich wahrscheinliche Symbole pro Walze.",label);
            GUI.enabled=!spinning;if(GUILayout.Button(spinning?"Walzen laufen …":"HEBEL ZIEHEN / SPIN",button,GUILayout.Height(60)))Spin();GUI.enabled=true;
        }
        void CardsGUI()
        {
            bool blackjack=station.game==CasinoGame.Blackjack;
            GUILayout.Label(blackjack?"Blackjack: Dealer steht auf allen 17 · Blackjack 3:2 · Verdoppeln auf den ersten zwei Karten · kein Split / keine Versicherung.":"Five Card Draw gegen den Dealer: fünf Karten, einmal tauschen. Gewinn 1:1, Gleichstand gibt den Einsatz zurück.",small);
            GUILayout.Label("DEALER: "+(hand.Count==0?"—":cardRound?(blackjack?CasinoRules.Card(dealer[0])+"   [verdeckt]":"[5 verdeckte Karten]"):string.Join("   ",dealer.Select(CasinoRules.Card))),title);
            GUILayout.BeginHorizontal();
            for(int i=0;i<hand.Count;i++)
            {
                if(!blackjack&&cardRound){if(GUILayout.Button(CasinoRules.Card(hand[i])+"\n"+(holds[i]?"HALTEN":"TAUSCHEN"),button,GUILayout.Width(130),GUILayout.Height(75)))holds[i]=!holds[i];}
                else GUILayout.Label(CasinoRules.Card(hand[i]),title,GUILayout.Width(95));
            }
            GUILayout.EndHorizontal();
            if(blackjack&&hand.Count>0)GUILayout.Label("Deine Punkte: "+CasinoRules.BlackjackTotal(hand),label);
            GUILayout.Space(15);GUILayout.BeginHorizontal();
            if(!cardRound){if(GUILayout.Button("NEUE RUNDE / GEBEN",button,GUILayout.Height(45)))Deal();}
            else if(blackjack)
            {
                if(GUILayout.Button("KARTE",button,GUILayout.Height(45)))Hit();
                if(GUILayout.Button("STEHEN",button,GUILayout.Height(45)))Stand();
                GUI.enabled=cardRound&&hand.Count==2&&Chips>=roundStake;
                if(GUILayout.Button("VERDOPPELN",button,GUILayout.Height(45)))Double();GUI.enabled=true;
            }
            else if(GUILayout.Button("TAUSCHEN & AUFDECKEN",button,GUILayout.Height(45)))DrawPoker();
            GUILayout.EndHorizontal();
        }
    }
}
