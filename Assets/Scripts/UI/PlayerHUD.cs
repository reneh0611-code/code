using CheatOnYourDayOnes.Economy;
using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PlayerHUD : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private TMP_Text bankText;
        [SerializeField] private TMP_Text auraText;
        [SerializeField] private TMP_Text interactionText;

        [Header("Bars")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider hungerSlider;
        [SerializeField] private Slider energySlider;

        private PlayerAgent _player;
        private PlayerInteractor _interactor;

        private void Update()
        {
            if (_player == null)
                TryBindLocalPlayer();

            if (_player == null)
                return;

            Refresh();

            if (interactionText != null && _interactor != null)
                interactionText.text = _interactor.CurrentPrompt;
        }

        private void TryBindLocalPlayer()
        {
            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening ||
                NetworkManager.Singleton.LocalClient == null ||
                NetworkManager.Singleton.LocalClient.PlayerObject == null)
                return;

            _player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerAgent>();
            _interactor = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInteractor>();
        }

        private void Refresh()
        {
            if (cashText != null)
                cashText.text = $"Cash  ${_player.Wallet.Cash.Value:N0}";

            if (bankText != null)
                bankText.text = $"Bank  ${_player.Wallet.Bank.Value:N0}";

            if (auraText != null)
                auraText.text = $"Aura  {_player.Aura.Aura.Value:+0;-0;0}";

            if (healthSlider != null)
                healthSlider.value = _player.Needs.Health.Value / 100f;

            if (hungerSlider != null)
                hungerSlider.value = _player.Needs.Hunger.Value / 100f;

            if (energySlider != null)
                energySlider.value = _player.Needs.Energy.Value / 100f;
        }
    }
}
