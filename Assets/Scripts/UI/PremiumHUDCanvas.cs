using System;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PremiumHUDCanvas : MonoBehaviour
    {
        private static readonly Color White = new(.98f,.98f,.96f,1f);
        private static readonly Color Muted = new(.69f,.70f,.69f,1f);
        private static readonly Color Panel = new(.025f,.028f,.034f,.34f);
        private static readonly Color PanelSoft = new(.025f,.028f,.034f,.24f);
        private static readonly Color Green = new(.38f,.95f,.50f,1f);
        private static readonly Color Red = new(.95f,.22f,.30f,1f);
        private static readonly Color Orange = new(1f,.63f,.16f,1f);
        private static readonly Color Blue = new(.16f,.65f,.96f,1f);
        private static readonly Color StaminaColor = new(.34f,.88f,.42f,1f);
        private static readonly Color Purple = new(.66f,.39f,.95f,1f);

        private Font _font;
        private Sprite _roundSprite,_circleSprite,_ringSprite;
        private PlayerAgent _player;
        private PlayerInteractor _interactor;
        private VehicleInteractor _vehicleInteractor;
        private NetworkPlayerController _movement;
        private CorpseCarryController _corpseCarry;
        private global::MeleeAnimationBridge _melee;
        private Text _cash,_bank,_aura,_clock,_day,_interactionKeyText,_interactionText,_healthValue,_hungerValue,_energyValue,_staminaValue,_speedText;
        private Image _healthFill,_hungerFill,_energyFill,_staminaFill,_needle,_reticleRing,_reticleDot;
        private GameObject _interactionRoot,_tachoRoot,_reticleRoot;
        private DriveableCar _occupiedCar;
        private float _nextHudRefresh;

        private void Awake(){_font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");_roundSprite=MakeRoundedSprite(96,22);_circleSprite=MakeCircleSprite(128);_ringSprite=MakeRingSprite(128,11);BuildCanvas();}

        private void Update()
        {
            TryBind(); if(_player==null)return;
            if(Time.unscaledTime>=_nextHudRefresh)
            {
                _nextHudRefresh=Time.unscaledTime+.1f;
                _occupiedCar=FindOccupiedCar();
                _cash.text="$ "+_player.Wallet.Cash.Value.ToString("N0");_bank.text="$ "+_player.Wallet.Bank.Value.ToString("N0");_aura.text=_player.Aura.Aura.Value.ToString("+0;-0;0");
                UpdateNeed(_healthValue,_healthFill,_player.Needs.Health.Value);UpdateNeed(_hungerValue,_hungerFill,_player.Needs.Hunger.Value);UpdateNeed(_energyValue,_energyFill,_player.Needs.Energy.Value);
                if(_movement!=null){_staminaValue.text=Mathf.RoundToInt(_movement.Stamina)+"%";_staminaFill.fillAmount=_movement.Stamina01;}
                DateTime now=DateTime.Now;_clock.text="☀  "+now.ToString("HH:mm");_day.text=GermanDay(now.DayOfWeek);
                bool onFoot=_occupiedCar==null;string prompt=null;string interactionKey="E";
                if(onFoot&&_corpseCarry!=null&&(_corpseCarry.HasCarriedBody||_corpseCarry.CanPickupBody)){prompt=_corpseCarry.HasCarriedBody?"Körper loslassen":"Körper ziehen";interactionKey="G";}else if(onFoot&&_vehicleInteractor!=null&&_vehicleInteractor.enabled&&_vehicleInteractor.CanEnterVehicle)prompt="Fahren";else if(onFoot&&_interactor!=null&&_interactor.enabled&&!string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))prompt=CleanPrompt(_interactor.CurrentPrompt);
                _interactionRoot.SetActive(onFoot&&!string.IsNullOrWhiteSpace(prompt));if(_interactionRoot.activeSelf){_interactionKeyText.text=interactionKey;_interactionText.text=prompt;}
                _tachoRoot.SetActive(_occupiedCar!=null);
                bool showReticle=onFoot&&_movement!=null;_reticleRoot.SetActive(showReticle);
                if(showReticle){bool locked=_melee!=null&&_melee.HasStrikeTarget;Color c=locked?Green:new Color(White.r,White.g,White.b,.82f);_reticleRing.color=c;_reticleDot.color=locked?Green:White;}
            }

            if(_occupiedCar!=null){float kmh=_occupiedCar.SpeedKmh;string speed=Mathf.RoundToInt(kmh).ToString();if(_speedText.text!=speed)_speedText.text=speed;float angle=Mathf.Lerp(-130f,130f,Mathf.Clamp01(kmh/50f));_needle.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);}
            if(_reticleRoot.activeSelf){float pulse=_melee!=null&&_melee.IsAttacking?1.08f+.05f*Mathf.Sin(Time.unscaledTime*22f):1f;_reticleRoot.transform.localScale=Vector3.one*pulse;}
        }

        private static void UpdateNeed(Text value,Image fill,float raw){float v=Mathf.Clamp(raw,0,100);value.text=Mathf.RoundToInt(v)+"%";fill.fillAmount=v/100f;}
        private void TryBind(){if(_player!=null){if(_corpseCarry==null)_corpseCarry=_player.GetComponent<CorpseCarryController>();if(_melee==null)_melee=_player.GetComponent<global::MeleeAnimationBridge>();return;}if(NetworkManager.Singleton==null||!NetworkManager.Singleton.IsListening)return;var local=NetworkManager.Singleton.LocalClient?.PlayerObject;if(local==null)return;_player=local.GetComponent<PlayerAgent>();_interactor=local.GetComponent<PlayerInteractor>();_vehicleInteractor=local.GetComponent<VehicleInteractor>();_movement=local.GetComponent<NetworkPlayerController>();_corpseCarry=local.GetComponent<CorpseCarryController>();_melee=local.GetComponent<global::MeleeAnimationBridge>();}

        private void BuildCanvas()
        {
            GameObject canvasGo=new("PremiumHUD_Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            BuildWallet(canvasGo.transform);BuildMission(canvasGo.transform);BuildClock(canvasGo.transform);BuildNeeds(canvasGo.transform);BuildLocation(canvasGo.transform);BuildInteraction(canvasGo.transform);BuildTacho(canvasGo.transform);BuildMeleeReticle(canvasGo.transform);
        }

        private void BuildMeleeReticle(Transform root)
        {
            _reticleRoot=new GameObject("MeleeReticle",typeof(RectTransform));_reticleRoot.transform.SetParent(root,false);RectTransform rt=_reticleRoot.GetComponent<RectTransform>();SetRect(rt,Vector2.zero,new Vector2(54,54),new Vector2(.5f,.5f));
            Image shadow=ImageEl(rt,"RingShadow",new Color(0,0,0,.58f),_ringSprite);SetRect(shadow.rectTransform,Vector2.zero,new Vector2(34,34),new Vector2(.5f,.5f));
            _reticleRing=ImageEl(rt,"AimRing",new Color(White.r,White.g,White.b,.82f),_ringSprite);SetRect(_reticleRing.rectTransform,Vector2.zero,new Vector2(30,30),new Vector2(.5f,.5f));
            BuildReticleTick(rt,"Left",new Vector2(-22,0),new Vector2(8,2));BuildReticleTick(rt,"Right",new Vector2(22,0),new Vector2(8,2));BuildReticleTick(rt,"Top",new Vector2(0,22),new Vector2(2,8));BuildReticleTick(rt,"Bottom",new Vector2(0,-22),new Vector2(2,8));
            _reticleDot=ImageEl(rt,"CenterDot",White,_circleSprite);SetRect(_reticleDot.rectTransform,Vector2.zero,new Vector2(4,4),new Vector2(.5f,.5f));
            _reticleRoot.SetActive(false);
        }

        private void BuildReticleTick(Transform root,string name,Vector2 pos,Vector2 size){Image tick=ImageEl(root,name,new Color(White.r,White.g,White.b,.72f),_roundSprite);tick.type=Image.Type.Sliced;SetRect(tick.rectTransform,pos,size,new Vector2(.5f,.5f));}

        private void BuildWallet(Transform root)
        {
            RectTransform card=PanelBox(root,"Wallet",new Vector2(24,-24),new Vector2(360,106),new Vector2(0,1),Panel);RectTransform badge=PanelBox(card,"Badge",new Vector2(16,-16),new Vector2(56,56),new Vector2(0,1),new Color(.05f,.12f,.07f,.55f));TextEl(badge,"$",26,Green,FontStyle.Bold,TextAnchor.MiddleCenter);
            TextEl(card,"CASH",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(88,-14),new Vector2(100,18),new Vector2(0,1));_cash=TextEl(card,"$ 1,500",35,Green,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(88,-33),new Vector2(240,40),new Vector2(0,1));Bar(card,new Vector2(88,-82),new Vector2(244,7),Green,.52f,out _);
            RectTransform sub=PanelBox(root,"WalletSub",new Vector2(24,-137),new Vector2(360,60),new Vector2(0,1),PanelSoft);TextEl(sub,"BANK",10,new Color(.62f,.82f,.66f,1),FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(16,-7),new Vector2(100,18),new Vector2(0,1));_bank=TextEl(sub,"$ 0",18,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(16,-27),new Vector2(140,25),new Vector2(0,1));TextEl(sub,"AURA",10,new Color(.77f,.57f,.98f,1),FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(200,-7),new Vector2(100,18),new Vector2(0,1));_aura=TextEl(sub,"0",18,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(200,-27),new Vector2(140,25),new Vector2(0,1));
        }

        private void BuildMission(Transform root)
        {
            TextEl(root,"AKTIVE MISSIONEN",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(24,-220),new Vector2(260,18),new Vector2(0,1));RectTransform card=PanelBox(root,"Mission",new Vector2(24,-246),new Vector2(360,82),new Vector2(0,1),PanelSoft);Image dot=ImageEl(card,"Dot",Purple,_circleSprite);SetRect(dot.rectTransform,new Vector2(16,-18),new Vector2(9,9),new Vector2(0,1));TextEl(card,"Keine aktive Mission",14,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(34,-10),new Vector2(290,22),new Vector2(0,1));TextEl(card,"Missionen erscheinen hier automatisch",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(34,-37),new Vector2(300,20),new Vector2(0,1));
        }

        private void BuildClock(Transform root){_clock=TextEl(root,"☀  23:50",31,White,FontStyle.Bold,TextAnchor.MiddleRight,new Vector2(-24,-22),new Vector2(190,38),new Vector2(1,1));_day=TextEl(root,"MITTWOCH",11,Muted,FontStyle.Normal,TextAnchor.MiddleRight,new Vector2(-24,-61),new Vector2(190,18),new Vector2(1,1));}

        private void BuildNeeds(Transform root)
        {
            RectTransform group=Empty(root,"Needs",new Vector2(24,28),new Vector2(378,244),new Vector2(0,0));
            BuildNeed(group,0,"♥","HEALTH",Red,out _healthFill,out _healthValue);BuildNeed(group,62,"●","HUNGER",Orange,out _hungerFill,out _hungerValue);BuildNeed(group,124,"▰","ENERGY",Blue,out _energyFill,out _energyValue);BuildNeed(group,186,"ϟ","STAMINA",StaminaColor,out _staminaFill,out _staminaValue);
        }

        private void BuildNeed(Transform parent,float y,string icon,string label,Color color,out Image fill,out Text value)
        {
            RectTransform card=PanelBox(parent,label,new Vector2(0,-y),new Vector2(378,58),new Vector2(0,1),Panel);TextEl(card,icon,25,color,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(12,-9),new Vector2(38,38),new Vector2(0,1));TextEl(card,label,14,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(58,-9),new Vector2(120,20),new Vector2(0,1));value=TextEl(card,"100%",12,White,FontStyle.Bold,TextAnchor.MiddleRight,new Vector2(-16,-9),new Vector2(55,20),new Vector2(1,1));fill=Bar(card,new Vector2(58,-35),new Vector2(298,8),color,1f,out _);
        }

        private void BuildLocation(Transform root){TextEl(root,"◆  Eastwood",12,White,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(24,8),new Vector2(190,22),new Vector2(0,0));}

        private void BuildInteraction(Transform root)
        {
            _interactionRoot=new GameObject("InteractionRoot",typeof(RectTransform));_interactionRoot.transform.SetParent(root,false);RectTransform rt=_interactionRoot.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);rt.pivot=new Vector2(.5f,0);rt.anchoredPosition=new Vector2(0,32);rt.sizeDelta=new Vector2(290,60);RectTransform card=PanelBox(rt,"Card",Vector2.zero,new Vector2(290,60),Vector2.zero,new Color(.025f,.028f,.034f,.40f));RectTransform key=PanelBox(card,"Key",new Vector2(12,-11),new Vector2(38,38),new Vector2(0,1),new Color(.12f,.13f,.15f,.58f));_interactionKeyText=TextEl(key,"E",15,White,FontStyle.Bold,TextAnchor.MiddleCenter);_interactionText=TextEl(card,"Fahren",15,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(68,-12),new Vector2(190,36),new Vector2(0,1));_interactionRoot.SetActive(false);
        }

        private void BuildTacho(Transform root)
        {
            _tachoRoot=new GameObject("Tacho",typeof(RectTransform));_tachoRoot.transform.SetParent(root,false);RectTransform tr=_tachoRoot.GetComponent<RectTransform>();tr.anchorMin=tr.anchorMax=new Vector2(1,0);tr.pivot=new Vector2(1,0);tr.anchoredPosition=new Vector2(-36,30);tr.sizeDelta=new Vector2(250,250);

            // Everything inside the gauge shares ONE exact center now.
            Image face=ImageEl(tr,"Face",new Color(.012f,.014f,.018f,.48f),_circleSprite);SetRect(face.rectTransform,Vector2.zero,new Vector2(238,238),new Vector2(.5f,.5f));
            RectTransform dial=Empty(tr,"Dial",Vector2.zero,Vector2.zero,new Vector2(.5f,.5f));

            float radius=103f;
            for(int i=0;i<=50;i++){float t=i/50f,a=Mathf.Lerp(-130f,130f,t);bool major=i%5==0;Color col=t>.82f?new Color(.96f,.22f,.16f,1f):(major?White:new Color(1,1,1,.34f));float len=major?15f:7f;RectTransform tick=ImageEl(dial,"Tick",col,null).rectTransform;tick.sizeDelta=new Vector2(major?2.2f:1f,len);tick.pivot=new Vector2(.5f,.5f);float rad=a*Mathf.Deg2Rad;tick.anchoredPosition=new Vector2(Mathf.Sin(rad)*(radius-len*.5f),Mathf.Cos(rad)*(radius-len*.5f));tick.localRotation=Quaternion.Euler(0,0,-a);if(major){Text label=TextEl(dial,Mathf.RoundToInt(50*t).ToString(),9,White,FontStyle.Bold,TextAnchor.MiddleCenter);label.rectTransform.sizeDelta=new Vector2(28,18);label.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(rad)*72,Mathf.Cos(rad)*72);}}
            _needle=ImageEl(dial,"Needle",White,null);_needle.rectTransform.sizeDelta=new Vector2(3,76);_needle.rectTransform.pivot=new Vector2(.5f,0);_needle.rectTransform.anchoredPosition=Vector2.zero;Image hub=ImageEl(dial,"Hub",White,_circleSprite);hub.rectTransform.sizeDelta=new Vector2(12,12);hub.rectTransform.anchoredPosition=Vector2.zero;_speedText=TextEl(dial,"0",40,White,FontStyle.Bold,TextAnchor.MiddleCenter);_speedText.rectTransform.sizeDelta=new Vector2(120,50);_speedText.rectTransform.anchoredPosition=new Vector2(0,-42);Text unit=TextEl(dial,"KM/H",10,Muted,FontStyle.Bold,TextAnchor.MiddleCenter);unit.rectTransform.sizeDelta=new Vector2(90,18);unit.rectTransform.anchoredPosition=new Vector2(0,-72);_tachoRoot.SetActive(false);
        }

        private RectTransform PanelBox(Transform parent,string name,Vector2 pos,Vector2 size,Vector2 anchor,Color color){Image img=ImageEl(parent,name,color,_roundSprite);img.type=Image.Type.Sliced;SetRect(img.rectTransform,pos,size,anchor);Shadow shadow=img.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.10f);shadow.effectDistance=new Vector2(0,-2);return img.rectTransform;}
        private Image Bar(Transform parent,Vector2 pos,Vector2 size,Color color,float amount,out Image track){track=ImageEl(parent,"Track",new Color(0,0,0,.64f),_roundSprite);track.type=Image.Type.Sliced;SetRect(track.rectTransform,pos,size,new Vector2(0,1));Image fill=ImageEl(track.transform,"Fill",color,_roundSprite);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillAmount=amount;Stretch(fill.rectTransform);return fill;}
        private Image ImageEl(Transform parent,string name,Color color,Sprite sprite){GameObject go=new(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);Image img=go.GetComponent<Image>();img.color=color;img.sprite=sprite;img.raycastTarget=false;return img;}
        private Text TextEl(Transform parent,string text,int size,Color color,FontStyle style,TextAnchor align,Vector2? pos=null,Vector2? dims=null,Vector2? anchor=null){GameObject go=new("Text_"+text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Text t=go.GetComponent<Text>();t.font=_font;t.text=text;t.fontSize=size;t.fontStyle=style;t.color=color;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;if(pos.HasValue)SetRect(t.rectTransform,pos.Value,dims??new Vector2(100,20),anchor??new Vector2(0,1));else Stretch(t.rectTransform);return t;}
        private RectTransform Empty(Transform parent,string name,Vector2 pos,Vector2 size,Vector2 anchor){GameObject go=new(name,typeof(RectTransform));go.transform.SetParent(parent,false);RectTransform rt=go.GetComponent<RectTransform>();SetRect(rt,pos,size,anchor);return rt;}
        private static void SetRect(RectTransform rt,Vector2 pos,Vector2 size,Vector2 anchor){rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=pos;rt.sizeDelta=size;}
        private static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;}
        private static DriveableCar FindOccupiedCar(){foreach(DriveableCar car in DriveableCar.ActiveCars)if(car!=null&&car.IsOccupied)return car;return null;}
        private static string CleanPrompt(string p){p=p.Trim().Replace("[ E ]","").Replace("[E]","").Replace("(E)","").Replace("E -","").Trim();return string.IsNullOrWhiteSpace(p)?"Interagieren":p;}
        private static string GermanDay(DayOfWeek d)=>d switch{DayOfWeek.Monday=>"MONTAG",DayOfWeek.Tuesday=>"DIENSTAG",DayOfWeek.Wednesday=>"MITTWOCH",DayOfWeek.Thursday=>"DONNERSTAG",DayOfWeek.Friday=>"FREITAG",DayOfWeek.Saturday=>"SAMSTAG",_=>"SONNTAG"};

        private static Sprite MakeRoundedSprite(int size,int radius){Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] p=new Color32[size*size];float l=radius,r=size-1-radius,b=radius,top=size-1-radius;for(int y=0;y<size;y++)for(int x=0;x<size;x++){float cx=Mathf.Clamp(x,l,r),cy=Mathf.Clamp(y,b,top);float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));byte alpha=(byte)(255*Mathf.Clamp01(radius+1-d));p[y*size+x]=new Color32(255,255,255,alpha);}t.SetPixels32(p);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));}
        private static Sprite MakeCircleSprite(int size){Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] p=new Color32[size*size];Vector2 c=new((size-1)*.5f,(size-1)*.5f);float r=(size-2)*.5f;for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),c);byte alpha=(byte)(255*Mathf.Clamp01(r+1-d));p[y*size+x]=new Color32(255,255,255,alpha);}t.SetPixels32(p);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);}
        private static Sprite MakeRingSprite(int size,float thickness){Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] p=new Color32[size*size];Vector2 c=new((size-1)*.5f,(size-1)*.5f);float outer=(size-3)*.5f,inner=outer-thickness;for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),c);float a=Mathf.Min(Mathf.Clamp01(outer+1-d),Mathf.Clamp01(d-inner+1));p[y*size+x]=new Color32(255,255,255,(byte)(255*a));}t.SetPixels32(p);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);}
    }
}
