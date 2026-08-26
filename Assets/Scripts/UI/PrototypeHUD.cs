using System;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private static readonly Color Panel=new(.025f,.026f,.03f,.48f),PanelSoft=new(.025f,.026f,.03f,.34f),Text=new(.98f,.98f,.96f,1f),Muted=new(.72f,.72f,.70f,1f),Track=new(.035f,.035f,.04f,.92f);
        private static readonly Color Health=new(.93f,.20f,.28f,1f),Hunger=new(1f,.66f,.19f,1f),Energy=new(.16f,.65f,.94f,1f),Stamina=new(.36f,.82f,.39f,1f),Gold=new(.94f,.80f,.47f,1f),Green=new(.50f,.96f,.55f,1f),Purple=new(.65f,.35f,.92f,1f);
        private PlayerAgent _player; private PlayerInteractor _interactor; private VehicleInteractor _vehicleInteractor; private NetworkPlayerController _movement; private Texture2D _round; private float _staminaVisibleUntil;
        private GUIStyle _tiny,_small,_medium,_big,_prompt,_key,_speed,_unit,_dial;

        private void Awake(){_round=CreateRoundedMask(96,18f);_round.hideFlags=HideFlags.HideAndDontSave;}
        private void OnDestroy(){if(_round!=null)Destroy(_round);}
        private void TryBind(){if(_player!=null||NetworkManager.Singleton==null||!NetworkManager.Singleton.IsListening)return;var l=NetworkManager.Singleton.LocalClient?.PlayerObject;if(l==null)return;_player=l.GetComponent<PlayerAgent>();_interactor=l.GetComponent<PlayerInteractor>();_vehicleInteractor=l.GetComponent<VehicleInteractor>();_movement=l.GetComponent<NetworkPlayerController>();}
        private static GUIStyle Style(float z,float s,FontStyle f,Color c,TextAnchor a)=>new(GUI.skin.label){fontSize=Mathf.RoundToInt(z*s),fontStyle=f,alignment=a,padding=new RectOffset(0,0,0,0),normal={textColor=c}};
        private void Styles(float s){_tiny=Style(10,s,FontStyle.Normal,Muted,TextAnchor.MiddleLeft);_small=Style(13,s,FontStyle.Bold,Text,TextAnchor.MiddleLeft);_medium=Style(18,s,FontStyle.Bold,Text,TextAnchor.MiddleLeft);_big=Style(34,s,FontStyle.Bold,Text,TextAnchor.MiddleLeft);_prompt=Style(15,s,FontStyle.Bold,Text,TextAnchor.MiddleLeft);_key=Style(14,s,FontStyle.Bold,Text,TextAnchor.MiddleCenter);_speed=Style(44,s,FontStyle.Bold,Text,TextAnchor.MiddleCenter);_unit=Style(11,s,FontStyle.Bold,Muted,TextAnchor.MiddleCenter);_dial=Style(9,s,FontStyle.Bold,Text,TextAnchor.MiddleCenter);}

        private void OnGUI(){TryBind();if(_player==null)return;float s=Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.78f,1.22f);Styles(s);DriveableCar car=FindOccupiedCar();DrawWallet(s);DrawMissionBlock(s);DrawClock(s);DrawNeeds(s);DrawLocation(s);DrawStamina(s,car==null);DrawInteraction(s,car==null);if(car!=null)DrawTacho(s,car);}

        private void DrawWallet(float s)
        {
            float x=24*s,y=26*s,w=332*s,h=100*s;
            DrawCard(new Rect(x,y,w,h),Panel,true);
            Rect badge=new(x+14*s,y+14*s,52*s,52*s);DrawCard(badge,new Color(.055f,.10f,.07f,.76f),true);DrawBorder(badge,new Color(.65f,1f,.70f,.60f),1.4f*s);GUI.Label(badge,"$",Style(26,s,FontStyle.Bold,Green,TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x+82*s,y+12*s,90*s,15*s),"CASH",_tiny);Color old=_big.normal.textColor;_big.normal.textColor=Green;GUI.Label(new Rect(x+82*s,y+27*s,226*s,40*s),$"$ {_player.Wallet.Cash.Value:N0}",_big);_big.normal.textColor=old;
            Rect track=new(x+82*s,y+76*s,228*s,7*s);DrawRounded(track,new Color(0,0,0,.75f));DrawRounded(new Rect(track.x,track.y,track.width*.52f,track.height),Green);

            float by=y+h+5*s,bh=58*s;DrawCard(new Rect(x,by,w,bh),PanelSoft,true);DrawLine(new Rect(x+w*.5f,by+9*s,1*s,bh-18*s),new Color(1,1,1,.18f));
            GUI.Label(new Rect(x+15*s,by+7*s,80*s,15*s),"▣  BANK",Style(10,s,FontStyle.Normal,new Color(.65f,.82f,.68f,1f),TextAnchor.MiddleLeft));GUI.Label(new Rect(x+15*s,by+24*s,140*s,24*s),$"$ {_player.Wallet.Bank.Value:N0}",_medium);
            GUI.Label(new Rect(x+w*.5f+15*s,by+7*s,80*s,15*s),"♜  AURA",Style(10,s,FontStyle.Normal,new Color(.76f,.54f,.96f,1f),TextAnchor.MiddleLeft));GUI.Label(new Rect(x+w*.5f+15*s,by+24*s,140*s,24*s),$"{_player.Aura.Aura.Value:+0;-0;0}",_medium);
        }

        private void DrawMissionBlock(float s)
        {
            float x=24*s,y=214*s,w=332*s;GUI.Label(new Rect(x,y,w,18*s),"AKTIVE MISSIONEN",_tiny);
            DrawCard(new Rect(x,y+25*s,w,78*s),PanelSoft,true);DrawRounded(new Rect(x+13*s,y+40*s,8*s,8*s),Purple);GUI.Label(new Rect(x+32*s,y+31*s,w-44*s,24*s),"Keine aktive Mission",_small);GUI.Label(new Rect(x+32*s,y+56*s,w-44*s,19*s),"Missionen erscheinen hier automatisch",Style(11,s,FontStyle.Normal,Muted,TextAnchor.MiddleLeft));
        }

        private void DrawClock(float s){DateTime n=DateTime.Now;float w=170*s,h=72*s,x=Screen.width-25*s-w,y=22*s;GUI.Label(new Rect(x,y,w,38*s),"☀  "+n.ToString("HH:mm"),Style(30,s,FontStyle.Bold,Text,TextAnchor.MiddleRight));GUI.Label(new Rect(x,y+37*s,w,20*s),GermanDay(n.DayOfWeek),Style(11,s,FontStyle.Normal,Muted,TextAnchor.MiddleRight));}
        private static string GermanDay(DayOfWeek d)=>d switch{DayOfWeek.Monday=>"MONTAG",DayOfWeek.Tuesday=>"DIENSTAG",DayOfWeek.Wednesday=>"MITTWOCH",DayOfWeek.Thursday=>"DONNERSTAG",DayOfWeek.Friday=>"FREITAG",DayOfWeek.Saturday=>"SAMSTAG",_=>"SONNTAG"};

        private void DrawNeeds(float s)
        {
            float x=24*s,w=356*s,row=54*s,g=4*s,y=Screen.height-28*s-(row*3+g*2)-42*s;
            DrawNeed(x,y,w,row,"♥","HEALTH",_player.Needs.Health.Value,Health,s);DrawNeed(x,y+row+g,w,row,"●","HUNGER",_player.Needs.Hunger.Value,Hunger,s);DrawNeed(x,y+(row+g)*2,w,row,"▰","ENERGY",_player.Needs.Energy.Value,Energy,s);
        }
        private void DrawNeed(float x,float y,float w,float h,string icon,string label,float raw,Color col,float s)
        {
            float v=Mathf.Clamp(raw,0,100);DrawCard(new Rect(x,y,w,h),Panel,true);GUI.Label(new Rect(x+10*s,y+7*s,40*s,40*s),icon,Style(24,s,FontStyle.Bold,col,TextAnchor.MiddleCenter));GUI.Label(new Rect(x+56*s,y+8*s,120*s,18*s),label,_small);GUI.Label(new Rect(x+w-62*s,y+8*s,48*s,18*s),$"{Mathf.RoundToInt(v)}%",Style(12,s,FontStyle.Bold,Text,TextAnchor.MiddleRight));Rect tr=new(x+56*s,y+31*s,w-76*s,8*s);DrawRounded(tr,new Color(0,0,0,.82f));if(v>0)DrawRounded(new Rect(tr.x,tr.y,tr.width*v/100f,tr.height),col);
        }

        private void DrawStamina(float s,bool foot)
        {
            if(!foot||_movement==null)return;if(_movement.IsSprinting||_movement.Stamina01<.995f)_staminaVisibleUntil=Time.unscaledTime+1.2f;if(Time.unscaledTime>_staminaVisibleUntil)return;
            float x=24*s,w=356*s,h=54*s,y=Screen.height-28*s-h;DrawCard(new Rect(x,y,w,h),Panel,true);GUI.Label(new Rect(x+10*s,y+7*s,40*s,40*s),"ϟ",Style(26,s,FontStyle.Bold,Stamina,TextAnchor.MiddleCenter));GUI.Label(new Rect(x+56*s,y+8*s,120*s,18*s),"STAMINA",_small);GUI.Label(new Rect(x+w-62*s,y+8*s,48*s,18*s),$"{Mathf.RoundToInt(_movement.Stamina)}%",Style(12,s,FontStyle.Bold,Text,TextAnchor.MiddleRight));Rect tr=new(x+56*s,y+31*s,w-76*s,8*s);DrawRounded(tr,new Color(0,0,0,.82f));DrawRounded(new Rect(tr.x,tr.y,tr.width*_movement.Stamina01,tr.height),Stamina);
        }

        private void DrawLocation(float s){GUI.Label(new Rect(24*s,Screen.height-48*s,190*s,22*s),"◆  Eastwood",Style(12,s,FontStyle.Normal,Text,TextAnchor.MiddleLeft));}
        private void DrawInteraction(float s,bool foot){if(!foot)return;string p=null;if(_vehicleInteractor!=null&&_vehicleInteractor.enabled&&_vehicleInteractor.CanEnterVehicle)p="Fahren";else if(_interactor!=null&&_interactor.enabled&&!string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))p=CleanPrompt(_interactor.CurrentPrompt);if(string.IsNullOrWhiteSpace(p))return;float w=Mathf.Clamp(175+p.Length*8,220,380)*s,h=56*s,x=Screen.width*.5f-w*.5f,y=Screen.height-88*s;DrawCard(new Rect(x,y,w,h),new Color(.025f,.026f,.03f,.58f),true);Rect key=new(x+12*s,y+9*s,38*s,38*s);DrawBorder(key,new Color(1,1,1,.75f),2*s);GUI.Label(key,"E",_key);GUI.Label(new Rect(x+68*s,y,w-80*s,h),p,_prompt);}

        private void DrawTacho(float s,DriveableCar car)
        {
            float size=276*s,x=Screen.width-26*s-size,y=Screen.height-22*s-size;Vector2 c=new(x+size*.5f,y+size*.53f);float outer=118*s;
            DrawCircleFilled(c,outer,new Color(.012f,.013f,.016f,.78f),64);DrawCircleRing(c,outer,new Color(.92f,.92f,.90f,.88f),2.4f*s,90);DrawCircleRing(c,outer-7*s,new Color(1,1,1,.13f),1*s,90);
            const float minA=-130,maxA=130,maxK=50;
            for(int i=0;i<=50;i++){float t=i/50f,a=Mathf.Lerp(minA,maxA,t);bool major=i%5==0;Color tc=t>.82f?new Color(.92f,.20f,.16f,1f):(major?Text:new Color(1,1,1,.50f));DrawRadialTick(c,outer-7*s,a,(major?17:8)*s,(major?2.2f:1.1f)*s,tc);if(major){Vector2 lp=Point(c,outer-37*s,a);GUI.Label(new Rect(lp.x-16*s,lp.y-8*s,32*s,16*s),Mathf.RoundToInt(maxK*t).ToString(),_dial);}}
            // redline arc
            for(int i=0;i<10;i++){float a=Mathf.Lerp(82f,130f,i/9f);DrawRadialTick(c,outer-4*s,a,13*s,4*s,new Color(.88f,.17f,.13f,.95f));}
            float kmh=Mathf.Clamp(car.SpeedKmh,0,maxK),needle=Mathf.Lerp(minA,maxA,kmh/maxK);DrawNeedle(c,needle,84*s,3.2f*s,new Color(.94f,.94f,.92f,1f));DrawCircleFilled(c,7*s,new Color(.94f,.94f,.92f,1f),24);
            GUI.Label(new Rect(c.x-70*s,c.y+11*s,140*s,54*s),Mathf.RoundToInt(car.SpeedKmh).ToString(),_speed);GUI.Label(new Rect(c.x-55*s,c.y+57*s,110*s,18*s),"KM/H",_unit);
            // fuel gauge from mockup
            Vector2 fc=new(c.x,c.y+87*s);GUI.Label(new Rect(fc.x-82*s,fc.y-7*s,18*s,16*s),"E",_unit);GUI.Label(new Rect(fc.x+64*s,fc.y-7*s,18*s,16*s),"F",_unit);GUI.Label(new Rect(fc.x-8*s,fc.y-7*s,16*s,16*s),"▣",_unit);DrawArc(fc,62*s,-105,-75,new Color(1f,.45f,.10f,1f),5*s,14);DrawArc(fc,62*s,-75,-20,new Color(.65f,.65f,.62f,.75f),5*s,18);
        }

        private static Vector2 Point(Vector2 c,float r,float deg){float a=(deg-90)*Mathf.Deg2Rad;return c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}
        private static void DrawRadialTick(Vector2 c,float r,float a,float len,float w,Color col){Vector2 p=Point(c,r-len*.5f,a);Matrix4x4 m=GUI.matrix;Color o=GUI.color;GUI.color=col;GUIUtility.RotateAroundPivot(a,p);GUI.DrawTexture(new Rect(p.x-w*.5f,p.y-len*.5f,w,len),Texture2D.whiteTexture);GUI.matrix=m;GUI.color=o;}
        private static void DrawNeedle(Vector2 c,float a,float len,float w,Color col){Matrix4x4 m=GUI.matrix;Color o=GUI.color;GUI.color=col;GUIUtility.RotateAroundPivot(a,c);GUI.DrawTexture(new Rect(c.x-w*.5f,c.y-len+8,w,len),Texture2D.whiteTexture);GUI.matrix=m;GUI.color=o;}
        private static void DrawCircleFilled(Vector2 c,float r,Color col,int seg){for(int i=0;i<seg;i++){float a0=i*360f/seg,a1=(i+1)*360f/seg;Vector2 p0=Point(c,r,a0),p1=Point(c,r,a1);DrawLineSegment(c,p0,r,col);DrawLineSegment(c,p1,r,col);}}
        private static void DrawCircleRing(Vector2 c,float r,Color col,float width,int seg){Vector2 prev=Point(c,r,0);for(int i=1;i<=seg;i++){Vector2 next=Point(c,r,i*360f/seg);DrawLineSegment(prev,next,width,col);prev=next;}}
        private static void DrawArc(Vector2 c,float r,float start,float end,Color col,float width,int seg){Vector2 prev=Point(c,r,start);for(int i=1;i<=seg;i++){Vector2 next=Point(c,r,Mathf.Lerp(start,end,i/(float)seg));DrawLineSegment(prev,next,width,col);prev=next;}}
        private static void DrawLineSegment(Vector2 a,Vector2 b,float width,Color col){Vector2 d=b-a;float len=d.magnitude;if(len<.1f)return;float ang=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;Matrix4x4 m=GUI.matrix;Color o=GUI.color;GUI.color=col;GUIUtility.RotateAroundPivot(ang,a);GUI.DrawTexture(new Rect(a.x,a.y-width*.5f,len,width),Texture2D.whiteTexture);GUI.matrix=m;GUI.color=o;}

        private static DriveableCar FindOccupiedCar(){foreach(var c in UnityEngine.Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))if(c!=null&&c.IsOccupied)return c;return null;}
        private static string CleanPrompt(string p){p=p.Trim().Replace("[ E ]","").Replace("[E]","").Replace("(E)","").Replace("E -","").Trim();return string.IsNullOrWhiteSpace(p)?"Interagieren":p;}
        private void DrawCard(Rect r,Color c,bool border=false){DrawRounded(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.12f));DrawRounded(r,c);if(border)DrawBorder(r,new Color(1,1,1,.16f),1*safescale());}
        private float safescale()=>Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.78f,1.22f);
        private void DrawBorder(Rect r,Color c,float width){DrawRounded(new Rect(r.x-width,r.y-width,r.width+2*width,r.height+2*width),c);DrawRounded(r,new Color(.025f,.026f,.03f,.70f));}
        private void DrawRounded(Rect r,Color c){Color o=GUI.color;GUI.color=c;GUI.DrawTexture(r,_round!=null?_round:Texture2D.whiteTexture,ScaleMode.StretchToFill,true);GUI.color=o;}
        private static void DrawLine(Rect r,Color c){Color o=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=o;}
        private static Texture2D CreateRoundedMask(int size,float radius){Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] px=new Color32[size*size];float l=radius,r=size-1-radius,b=radius,top=size-1-radius;for(int y=0;y<size;y++)for(int x=0;x<size;x++){float cx=Mathf.Clamp(x,l,r),cy=Mathf.Clamp(y,b,top),d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy)),a=1-Mathf.Clamp01(d-radius+1);px[y*size+x]=new Color(1,1,1,a);}t.SetPixels32(px);t.Apply(false,true);return t;}
    }
}
