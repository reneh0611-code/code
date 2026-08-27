using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public static class PremiumHUDBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePremiumHudExists()
        {
            PremiumHUDCanvas existing = Object.FindFirstObjectByType<PremiumHUDCanvas>();
            if (existing != null) return;

            GameObject hudRoot = new("PremiumHUD_Runtime");
            hudRoot.AddComponent<PremiumHUDCanvas>();
            Object.DontDestroyOnLoad(hudRoot);
        }
    }
}
