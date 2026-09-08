using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using UnityEngine;

namespace CheatOnYourDayOnes.Casino
{
    public enum CasinoGame { Roulette, Slots, Blackjack, Poker }
    public sealed class CasinoStation : MonoBehaviour,IInteractable
    {
        public CasinoSession session;
        public CasinoGame game;
        public Transform view;
        public Transform wheel;
        public Transform ball;
        public TextMesh[] displays;
        public string GetInteractionText(PlayerAgent player)=>game+" spielen";
        public bool CanInteract(PlayerAgent player)=>session!=null&&player!=null&&player.IsOwner&&!session.IsOpen;
        public void InteractServer(PlayerAgent player){if(CanInteract(player))session.Open(this,player);}
    }
}
