using CheatOnYourDayOnes.Player;

namespace CheatOnYourDayOnes.Interaction
{
    public interface IInteractable
    {
        string GetInteractionText(PlayerAgent player);
        bool CanInteract(PlayerAgent player);
        void InteractServer(PlayerAgent player);
    }
}
