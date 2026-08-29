using System;
using CheatOnYourDayOnes.Core;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NeedsSystem : NetworkBehaviour
    {
        [Header("Drain per real minute")]
        [SerializeField, Min(0f)] private float hungerDrainPerMinute = 3f;
        [SerializeField, Min(0f)] private float energyDrainPerMinute = 2f;
        [SerializeField, Min(0f)] private float starvationDamagePerMinute = 10f;

        [Header("Server tick")]
        [SerializeField, Min(0.1f)] private float tickInterval = 1f;

        public NetworkVariable<float> Health = new(
            GameConstants.MaxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> Hunger = new(
            GameConstants.MaxHunger,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> Energy = new(
            GameConstants.MaxEnergy,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public event Action NeedsChanged;

        private float _tickTimer;

        public bool IsLowHunger => Hunger.Value <= 20f;
        public bool IsLowEnergy => Energy.Value <= 20f;

        private void Update()
        {
            if (!IsServer || !IsSpawned)
                return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
                return;

            float elapsed = _tickTimer;
            _tickTimer = 0f;

            Hunger.Value = Mathf.Clamp(Hunger.Value - hungerDrainPerMinute * elapsed / 60f, 0f, GameConstants.MaxHunger);
            Energy.Value = Mathf.Clamp(Energy.Value - energyDrainPerMinute * elapsed / 60f, 0f, GameConstants.MaxEnergy);

            if (Hunger.Value <= 0.01f)
            {
                Health.Value = Mathf.Clamp(
                    Health.Value - starvationDamagePerMinute * elapsed / 60f,
                    0f,
                    GameConstants.MaxHealth);
            }
        }

        public override void OnNetworkSpawn()
        {
            Health.OnValueChanged += OnAnyNeedChanged;
            Hunger.OnValueChanged += OnAnyNeedChanged;
            Energy.OnValueChanged += OnAnyNeedChanged;
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= OnAnyNeedChanged;
            Hunger.OnValueChanged -= OnAnyNeedChanged;
            Energy.OnValueChanged -= OnAnyNeedChanged;
        }

        public void ConsumeServer(float foodValue, float energyValue)
        {
            if (!IsServer)
                return;

            Hunger.Value = Mathf.Clamp(Hunger.Value + Mathf.Max(0f, foodValue), 0f, GameConstants.MaxHunger);
            Energy.Value = Mathf.Clamp(Energy.Value + energyValue, 0f, GameConstants.MaxEnergy);
        }

        public void DamageServer(float amount)
        {
            if (!IsServer || amount <= 0f)
                return;

            Health.Value = Mathf.Clamp(Health.Value - amount, 0f, GameConstants.MaxHealth);
        }

        public void RequestDamage(float amount)
        {
            amount = Mathf.Clamp(amount, 0f, GameConstants.MaxHealth);
            if (amount <= 0f || !IsSpawned) return;
            if (IsServer) DamageServer(amount);
            else RequestDamageRpc(amount);
        }

        [Rpc(SendTo.Server)]
        private void RequestDamageRpc(float amount) => DamageServer(Mathf.Clamp(amount, 0f, GameConstants.MaxHealth));

        private void OnAnyNeedChanged(float previous, float current) => NeedsChanged?.Invoke();
    }
}
