using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    // Compatibility bootstrap: existing scenes already reference PrototypeHUD.
    // The visual HUD itself now lives in PremiumHUDCanvas and uses Unity Canvas/uGUI,
    // not immediate-mode OnGUI drawing.
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<PremiumHUDCanvas>() == null)
                gameObject.AddComponent<PremiumHUDCanvas>();
        }
    }
}
