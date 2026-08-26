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
        private static readonly Color Muted = new(.67f,.69f,.70f,1f);
        private static readonly Color Panel = new(.025f,.028f,.034f,.76f);
        private static readonly Color PanelSoft = new(.025f,.028f,.034f,.56f);
        private static readonly Color Border = new(1f,1f,1f,.10f);
        private static readonly Color Green = new(.38f,.95f,.50f,1f);
        private static readonly Color Red = new(.95f,.22f,.30f,1f);
        private static readonly Color Orange = new(1f,.63f,.16f,1f);
        private static readonly Color Blue = new(.16f,.65f,.96f,1f);
        private static readonly Color StaminaColor = new(.34f,.88f,.42f,1f);
        private static readonly Color Purple = new(.66f,.39f,.95f,1f);
        private static readonly Color Gold = new(.98f,.79f,.39f,1f);

        private Font _font;
        private Sprite _roundSprite, _circleSprite;
        private PlayerAgent _player;
        private PlayerInteractor _interactor;
        private VehicleInteractor _vehicleInteractor;
        private NetworkPlayerController _movement;

        private Text _cash,_bank,_aura,_clock,_day,_location,_interactionText,_healthValue,_hungerValue,_energyValue,_staminaValue,_speedText;
        private Image _healthFill,_hungerFill,_energyFill,_staminaFill,_needle;
        private GameObject _interactionRoot,_staminaRoot,_tachoRoot;
        private float _staminaVisibleUntil;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _roundSprite = MakeRoundedSprite(96, 22);
            _circleSprite = MakeCircleSprite(128);
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (_roundSprite != null) Destroy(_roundSprite.texture);
            if (_circleSprite != null) Destroy(_circleSprite.texture);
        }

        private void Update()
        {
            TryBind();
            if (_player == null) return;

            DriveableCar car = FindOccupiedCar();
            _cash.text = "$ " + _player.Wallet.Cash.Value.ToString("N0");
            _bank.text = "$ " + _player.Wallet.Bank.Value.ToString("N0");
            _aura.text = _player.Aura.Aura.Value.ToString("+0;-0;0");
            _healthValue.text = Mathf.RoundToInt(_player.Needs.Health.Value) + "%";
            _hungerValue.text = Mathf.RoundToInt(_player.Needs.Hunger.Value) + "%";
            _energyValue.text = Mathf.RoundToInt(_player.Needs.Energy.Value) + "%";
            _healthFill.fillAmount = Mathf.Clamp01(_player.Needs.Health.Value / 100f);
            _hungerFill.fillAmount = Mathf.Clamp01(_player.Needs.Hunger.Value / 100f);
            _energyFill.fillAmount = Mathf.Clamp01(_player.Needs.Energy.Value / 100f);

            DateTime now = DateTime.Now;
            _clock.text = "☀  " + now.ToString("HH:mm");
            _day.text = GermanDay(now.DayOfWeek);

            bool onFoot = car == null;
            string prompt = null;
            if (onFoot && _vehicleInteractor != null && _vehicleInteractor.enabled && _vehicleInteractor.CanEnterVehicle) prompt = "Fahren";
            else if (onFoot && _interactor != null && _interactor.enabled && !string.IsNullOrWhiteSpace(_interactor.CurrentPrompt)) prompt = CleanPrompt(_interactor.CurrentPrompt);
            _interactionRoot.SetActive(onFoot && !string.IsNullOrWhiteSpace(prompt));
            if (_interactionRoot.activeSelf) _interactionText.text = prompt;

            if (_movement != null && onFoot)
            {
                if (_movement.IsSprinting || _movement.Stamina01 < .995f) _staminaVisibleUntil = Time.unscaledTime + 1.25f;
                bool show = Time.unscaledTime <= _staminaVisibleUntil;
                _staminaRoot.SetActive(show);
                if (show)
                {
                    _staminaFill.fillAmount = _movement.Stamina01;
                    _staminaValue.text = Mathf.RoundToInt(_movement.Stamina) + "%";
                }
            }
            else _staminaRoot.SetActive(false);

            _tachoRoot.SetActive(car != null);
            if (car != null)
            {
                float kmh = car.SpeedKmh;
                _speedText.text = Mathf.RoundToInt(kmh).ToString();
                float a = Mathf.Lerp(-130f,130f,Mathf.Clamp01(kmh/50f));
                _needle.rectTransform.localRotation = Quaternion.Euler(0,0,-a);
            }
        }

        private void TryBind()
        {
            if (_player != null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local == null) return;
            _player = local.GetComponent<PlayerAgent>();
            _interactor = local.GetComponent<PlayerInteractor>();
            _vehicleInteractor = local.GetComponent<VehicleInteractor>();
            _movement = local.GetComponent<NetworkPlayerController>();
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new("PremiumHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            BuildWallet(canvasGo.transform);
            BuildMission(canvasGo.transform);
            BuildClock(canvasGo.transform);
            BuildNeeds(canvasGo.transform);
            BuildLocation(canvasGo.transform);
            BuildInteraction(canvasGo.transform);
            BuildTacho(canvasGo.transform);
        }

        private void BuildWallet(Transform root)
        {
            RectTransform card = PanelBox(root,"Wallet",new Vector2(24,-24),new Vector2(360,106),new Vector2(0,1),Panel,22);
            RectTransform badge = PanelBox(card,"MoneyBadge",new Vector2(16,-16),new Vector2(56,56),new Vector2(0,1),new Color(.05f,.12f,.07f,.92f),18);
            AddOutline(badge.gameObject,new Color(.40f,1f,.52f,.42f),new Vector2(1,-1));
            Text badgeText = TextEl(badge,"$",26,Green,FontStyle.Bold,TextAnchor.MiddleCenter); Stretch(badgeText.rectTransform,Vector2.zero,Vector2.zero);
            TextEl(card,"CASH",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(88,-14),new Vector2(100,18),new Vector2(0,1));
            _cash = TextEl(card,"$ 1,500",35,Green,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(88,-33),new Vector2(240,40),new Vector2(0,1));
            Bar(card,new Vector2(88,-82),new Vector2(244,7),Green,.52f,out _);

            RectTransform sub = PanelBox(root,"WalletSub",new Vector2(24,-137),new Vector2(360,60),new Vector2(0,1),PanelSoft,20);
            Image divider = ImageEl(sub,"Divider",new Color(1,1,1,.12f),null); SetRect(divider.rectTransform,new Vector2(180,-10),new Vector2(1,40),new Vector2(0,1));
            TextEl(sub,"▣  BANK",10,new Color(.62f,.82f,.66f,1),FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(16,-7),new Vector2(100,18),new Vector2(0,1));
            _bank = TextEl(sub,"$ 0",18,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(16,-27),new Vector2(150,25),new Vector2(0,1));
            TextEl(sub,"♜  AURA",10,new Color(.77f,.57f,.98f,1),FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(198,-7),new Vector2(100,18),new Vector2(0,1));
            _aura = TextEl(sub,"0",18,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(198,-27),new Vector2(140,25),new Vector2(0,1));
        }

        private void BuildMission(Transform root)
        {
            TextEl(root,"AKTIVE MISSIONEN",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(24,-220),new Vector2(260,18),new Vector2(0,1));
            RectTransform card = PanelBox(root,"Mission",new Vector2(24,-246),new Vector2(360,82),new Vector2(0,1),PanelSoft,20);
            Image dot = ImageEl(card,"Dot",Purple,_circleSprite);SetRect(dot.rectTransform,new Vector2(16,-18),new Vector2(9,9),new Vector2(0,1));
            TextEl(card,"Keine aktive Mission",14,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(34,-10),new Vector2(290,22),new Vector2(0,1));
            TextEl(card,"Missionen erscheinen hier automatisch",11,Muted,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(34,-37),new Vector2(300,20),new Vector2(0,1));
        }

        private void BuildClock(Transform root)
        {
            _clock = TextEl(root,"☀  23:50",31,White,FontStyle.Bold,TextAnchor.MiddleRight,new Vector2(-24,-22),new Vector2(190,38),new Vector2(1,1));
            _day = TextEl(root,"MITTWOCH",11,Muted,FontStyle.Normal,TextAnchor.MiddleRight,new Vector2(-24,-61),new Vector2(190,18),new Vector2(1,1));
        }

        private void BuildNeeds(Transform root)
        {
            RectTransform group = Empty(root,"Needs",new Vector2(24,28),new Vector2(378,182),new Vector2(0,0));
            BuildNeed(group,0,"♥","HEALTH",Red,out _healthFill,out _healthValue);
            BuildNeed(group,62,"●","HUNGER",Orange,out _hungerFill,out _hungerValue);
            BuildNeed(group,124,"▰","ENERGY",Blue,out _energyFill,out _energyValue);

            _staminaRoot = new GameObject("StaminaRoot",typeof(RectTransform));
            _staminaRoot.transform.SetParent(root,false);
            RectTransform staminaRt = _staminaRoot.GetComponent<RectTransform>(); SetRect(staminaRt,new Vector2(24,28),new Vector2(378,58),new Vector2(0,0));
            RectTransform staminaCard = PanelBox(staminaRt,"StaminaCard",Vector2.zero,new Vector2(378,58),Vector2.zero,Panel,20);
            TextEl(staminaCard,"ϟ",27,StaminaColor,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(12,-9),new Vector2(38,38),new Vector2(0,1));
            TextEl(staminaCard,"STAMINA",14,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(58,-9),new Vector2(120,20),new Vector2(0,1));
            _staminaValue = TextEl(staminaCard,"100%",12,White,FontStyle.Bold,TextAnchor.MiddleRight,new Vector2(-16,-9),new Vector2(55,20),new Vector2(1,1));
            _staminaFill = Bar(staminaCard,new Vector2(58,-35),new Vector2(298,8),StaminaColor,1f,out _);
            _staminaRoot.SetActive(false);
        }

        private void BuildNeed(Transform parent,float y,string icon,string label,Color color,out Image fill,out Text value)
        {
            RectTransform card = PanelBox(parent,label,new Vector2(0,-y),new Vector2(378,58),new Vector2(0,1),Panel,20);
            TextEl(card,icon,25,color,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(12,-9),new Vector2(38,38),new Vector2(0,1));
            TextEl(card,label,14,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(58,-9),new Vector2(120,20),new Vector2(0,1));
            value = TextEl(card,"100%",12,White,FontStyle.Bold,TextAnchor.MiddleRight,new Vector2(-16,-9),new Vector2(55,20),new Vector2(1,1));
            fill = Bar(card,new Vector2(58,-35),new Vector2(298,8),color,1f,out _);
        }

        private void BuildLocation(Transform root)
        {
            _location = TextEl(root,"◆  Eastwood",12,White,FontStyle.Normal,TextAnchor.MiddleLeft,new Vector2(24,10),new Vector2(190,22),new Vector2(0,0));
        }

        private void BuildInteraction(Transform root)
        {
            _interactionRoot = new GameObject("InteractionRoot",typeof(RectTransform));
            _interactionRoot.transform.SetParent(root,false);
            RectTransform rt = _interactionRoot.GetComponent<RectTransform>();
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,0); rt.pivot=new Vector2(.5f,0); rt.anchoredPosition=new Vector2(0,32);rt.sizeDelta=new Vector2(290,60);
            RectTransform card=PanelBox(rt,"Card",Vector2.zero,new Vector2(290,60),Vector2.zero,new Color(.025f,.028f,.034f,.82f),20);
            RectTransform key=PanelBox(card,"Key",new Vector2(12,-11),new Vector2(38,38),new Vector2(0,1),new Color(.12f,.13f,.15f,.88f),12);AddOutline(key.gameObject,new Color(1,1,1,.34f),new Vector2(1,-1));TextEl(key,"E",15,White,FontStyle.Bold,TextAnchor.MiddleCenter);
            _interactionText=TextEl(card,"Fahren",15,White,FontStyle.Bold,TextAnchor.MiddleLeft,new Vector2(68,-12),new Vector2(190,36),new Vector2(0,1));
            _interactionRoot.SetActive(false);
        }

        private void BuildTacho(Transform root)
        {
            _tachoRoot = new GameObject("Tacho",typeof(RectTransform));_tachoRoot.transform.SetParent(root,false);
            RectTransform tr=_tachoRoot.GetComponent<RectTransform>();tr.anchorMin=tr.anchorMax=new Vector2(1,0);tr.pivot=new Vector2(1,0);tr.anchoredPosition=new Vector2(-30,28);tr.sizeDelta=new Vector2(300,300);
            Image face=ImageEl(tr,"Face",new Color(.012f,.014f,.018f,.88f),_circleSprite);SetRect(face.rectTransform,new Vector2(-150,150),new Vector2(270,270),new Vector2(1,0));
            AddOutline(face.gameObject,new Color(1,1,1,.56f),Vector2.zero);
            RectTransform dialRoot=Empty(tr,"Dial",new Vector2(-150,150),new Vector2(0,0),new Vector2(1,0));
            float radius=117f;
            for(int i=0;i<=50;i++)
            {
                float t=i/50f,a=Mathf.Lerp(-130,130,t);bool major=i%5==0;Color col=t>.82f?new Color(.96f,.22f,.16f,1f):(major?White:new Color(1,1,1,.35f));
                RectTransform tick=ImageEl(dialRoot,"Tick",col,null).rectTransform;float len=major?17:8;tick.sizeDelta=new Vector2(major?2.4f:1.1f,len);tick.pivot=new Vector2(.5f,.5f);float rad=a*Mathf.Deg2Rad;tick.anchoredPosition=new Vector2(Mathf.Sin(rad)*(radius-len*.5f),Mathf.Cos(rad)*(radius-len*.5f));tick.localRotation=Quaternion.Euler(0,0,-a);
                if(major){Text lab=TextEl(dialRoot,Mathf.RoundToInt(50*t).ToString(),10,White,FontStyle.Bold,TextAnchor.MiddleCenter);float lr=82;lab.rectTransform.sizeDelta=new Vector2(28,18);lab.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(rad)*lr,Mathf.Cos(rad)*lr);}
            }
            _needle=ImageEl(dialRoot,"Needle",White,null);_needle.rectTransform.sizeDelta=new Vector2(3.5f,88);_needle.rectTransform.pivot=new Vector2(.5f,0);_needle.rectTransform.anchoredPosition=Vector2.zero;
            Image hub=ImageEl(dialRoot,"Hub",White,_circleSprite);hub.rectTransform.sizeDelta=new Vector2(14,14);hub.rectTransform.anchoredPosition=Vector2.zero;
            _speedText=TextEl(dialRoot,"0",46,White,FontStyle.Bold,TextAnchor.MiddleCenter);_speedText.rectTransform.sizeDelta=new Vector2(130,56);_speedText.rectTransform.anchoredPosition=new Vector2(0,-48);
            Text unit=TextEl(dialRoot,"KM/H",11,Muted,FontStyle.Bold,TextAnchor.MiddleCenter);unit.rectTransform.sizeDelta=new Vector2(90,20);unit.rectTransform.anchoredPosition=new Vector2(0,-82);
            TextEl(dialRoot,"E",10,Muted,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(-74,-103),new Vector2(20,18),new Vector2(.5f,.5f));
            TextEl(dialRoot,"▣",11,Muted,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(0,-103),new Vector2(20,18),new Vector2(.5f,.5f));
            TextEl(dialRoot,"F",10,Muted,FontStyle.Bold,TextAnchor.MiddleCenter,new Vector2(74,-103),new Vector2(20,18),new Vector2(.5f,.5f));
            for(int i=0;i<16;i++){float a=Mathf.Lerp(-105,-20,i/15f),rad=a*Mathf.Deg2Rad;Image seg=ImageEl(dialRoot,"FuelSeg",i<5?new Color(1f,.43f,.10f,1f):new Color(.70f,.70f,.68f,.55f),null);seg.rectTransform.sizeDelta=new Vector2(5,10);seg.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(rad)*67,Mathf.Cos(rad)*67+9);seg.rectTransform.localRotation=Quaternion.Euler(0,0,-a);}
            _tachoRoot.SetActive(false);
        }

        private RectTransform PanelBox(Transform parent,string name,Vector2 pos,Vector2 size,Vector2 anchor,Color color,float radius)
        {
            Image img=ImageEl(parent,name,color,_roundSprite);img.type=Image.Type.Sliced;SetRect(img.rectTransform,pos,size,anchor);Shadow sh=img.gameObject.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,.22f);sh.effectDistance=new Vector2(0,-4);return img.rectTransform;
        }
        private void AddOutline(GameObject go,Color c,Vector2 distance){Outline o=go.AddComponent<Outline>();o.effectColor=c;o.effectDistance=distance==Vector2.zero?new Vector2(1,-1):distance;}
        private Image Bar(Transform parent,Vector2 pos,Vector2 size,Color color,float amount,out Image track)
        {
            track=ImageEl(parent,"Track",new Color(0,0,0,.74f),_roundSprite);track.type=Image.Type.Sliced;SetRect(track.rectTransform,pos,size,new Vector2(0,1));
            Image fill=ImageEl(track.transform,"Fill",color,_roundSprite);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=amount;Stretch(fill.rectTransform,Vector2.zero,Vector2.zero);return fill;
        }
        private Image ImageEl(Transform parent,string name,Color color,Sprite sprite){GameObject go=new(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);Image i=go.GetComponent<Image>();i.color=color;i.sprite=sprite;i.raycastTarget=false;return i;}
        private Text TextEl(Transform parent,string text,int size,Color color,FontStyle style,TextAnchor align,Vector2? pos=null,Vector2? dims=null,Vector2? anchor=null)
        {
            GameObject go=new("Text_"+text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Text t=go.GetComponent<Text>();t.font=_font;t.text=text;t.fontSize=size;t.fontStyle=style;t.color=color;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
            if(pos.HasValue)SetRect(t.rectTransform,pos.Value,dims??new Vector2(100,20),anchor??new Vector2(0,1));else Stretch(t.rectTransform,Vector2.zero,Vector2.zero);return t;
        }
        private RectTransform Empty(Transform parent,string name,Vector2 pos,Vector2 size,Vector2 anchor){GameObject go=new(name,typeof(RectTransform));go.transform.SetParent(parent,false);RectTransform rt=go.GetComponent<RectTransform>();SetRect(rt,pos,size,anchor);return rt;}
        private static void SetRect(RectTransform rt,Vector2 pos,Vector2 size,Vector2 anchor){rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(anchor.x,anchor.y);rt.anchoredPosition=pos;rt.sizeDelta=size;}
        private static void Stretch(RectTransform rt,Vector2 minOffset,Vector2 maxOffset){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=minOffset;rt.offsetMax=maxOffset;}
        private static DriveableCar FindOccupiedCar(){foreach(var c in UnityEngine.Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))if(c!=null&&c.IsOccupied)return c;return null;}
        private static string CleanPrompt(string p){p=p.Trim().Replace("[ E ]","").Replace("[E]","").Replace("(E)","").Replace("E -","").Trim();return string.IsNullOrWhiteSpace(p)?"Interagieren":p;}
        private static string GermanDay(DayOfWeek d)=>d switch{DayOfWeek.Monday=>"MONTAG",DayOfWeek.Tuesday=>"DIENSTAG",DayOfWeek.Wednesday=>"MITTWOCH",DayOfWeek.Thursday=>"DONNERSTAG",DayOfWeek.Friday=>"FREITAG",DayOfWeek.Saturday=>"SAMSTAG",_=>"SONNTAG"};

        private static Sprite MakeRoundedSprite(int size,int radius)
        {
            Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] p=new Color32[size*size];float left=radius,right=size-1-radius,bottom=radius,top=size-1-radius;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float cx=Mathf.Clamp(x,left,right),cy=Mathf.Clamp(y,bottom,top);float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));byte a=(byte)(255*Mathf.Clamp01(radius+1-d));p[y*size+x]=new Color32(255,255,255,a);}t.SetPixels32(p);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
        }
        private static Sprite MakeCircleSprite(int size)
        {
            Texture2D t=new(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Color32[] p=new Color32[size*size];Vector2 c=new((size-1)*.5f,(size-1)*.5f);float r=(size-2)*.5f;for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),c);byte a=(byte)(255*Mathf.Clamp01(r+1-d));p[y*size+x]=new Color32(255,255,255,a);}t.SetPixels32(p);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);}
    }
}
