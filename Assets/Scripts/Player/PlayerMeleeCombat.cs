using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    public sealed class PlayerMeleeCombat : NetworkBehaviour
    {
        [Header("Punch")]
        [SerializeField] private float range = 1.65f;
        [SerializeField] private float radius = 0.62f;
        [SerializeField] private float cooldown = 0.48f;
        [SerializeField] private float hitMoment = 0.15f;
        [SerializeField] private LayerMask hitMask = ~0;

        private Animator _animator;
        private Transform _visual;
        private Transform _chest;
        private Transform _upperArmR;
        private Transform _forearmR;
        private Transform _upperArmL;
        private Transform _forearmL;
        private float _nextPunch;
        private float _punchStarted = -10f;
        private bool _punchVariant;
        private bool _hitApplied;
        private Quaternion _chestBase, _upperRBase, _foreRBase, _upperLBase, _foreLBase;

        private static readonly int Punch1Hash = Animator.StringToHash("Base Layer.Punch1");
        private static readonly int Punch2Hash = Animator.StringToHash("Base Layer.Punch2");

        private void Awake()
        {
            _visual = transform.Find("CharacterVisual") ?? transform;
            _animator = _visual.GetComponentInChildren<Animator>(true);
            CacheBones();
        }

        private void Update()
        {
            if (NetworkObject != null && !IsOwner) return;

            if (Input.GetMouseButtonDown(1) && Time.time >= _nextPunch)
                StartPunch();

            if (_punchStarted > 0f && !_hitApplied && Time.time - _punchStarted >= hitMoment)
            {
                _hitApplied = true;
                ApplyHit();
            }
        }

        private void LateUpdate()
        {
            if (_punchStarted <= 0f || Time.time - _punchStarted > cooldown)
                return;

            int state = _punchVariant ? Punch2Hash : Punch1Hash;
            if (_animator != null && _animator.runtimeAnimatorController != null && _animator.HasState(0, state))
                return;

            ApplyProceduralPunch();
        }

        private void StartPunch()
        {
            _punchVariant = !_punchVariant;
            _nextPunch = Time.time + cooldown;
            _punchStarted = Time.time;
            _hitApplied = false;

            int state = _punchVariant ? Punch2Hash : Punch1Hash;
            if (_animator != null && _animator.runtimeAnimatorController != null && _animator.HasState(0, state))
                _animator.CrossFadeInFixedTime(state, 0.045f, 0, 0f);
        }

        private void ApplyHit()
        {
            Vector3 center = transform.position + Vector3.up * 1.05f + transform.forward * range;
            Collider[] hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Collide);
            NPCWanderer best = null;
            float bestScore = float.MaxValue;

            foreach (Collider c in hits)
            {
                if (c == null || c.transform.IsChildOf(transform)) continue;
                NPCWanderer npc = c.GetComponentInParent<NPCWanderer>();
                if (npc == null || !npc.CanReceivePlayerStrike) continue;

                Vector3 to = npc.StrikeTargetPosition - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > range + radius) continue;
                float facing = Vector3.Dot(transform.forward, to.normalized);
                if (facing < 0.18f) continue;
                float score = dist - facing * 0.6f;
                if (score < bestScore) { bestScore = score; best = npc; }
            }

            if (best != null)
            {
                Vector3 direction = best.StrikeTargetPosition - transform.position;
                direction.y = 0f;
                best.HitByPlayerPunch(direction.normalized, _punchVariant ? 2 : 1, transform);
            }
        }

        private void CacheBones()
        {
            if (_animator == null) return;
            _chest = SafeBone(HumanBodyBones.Chest) ?? SafeBone(HumanBodyBones.Spine);
            _upperArmR = SafeBone(HumanBodyBones.RightUpperArm);
            _forearmR = SafeBone(HumanBodyBones.RightLowerArm);
            _upperArmL = SafeBone(HumanBodyBones.LeftUpperArm);
            _forearmL = SafeBone(HumanBodyBones.LeftLowerArm);

            if (_chest != null) _chestBase = _chest.localRotation;
            if (_upperArmR != null) _upperRBase = _upperArmR.localRotation;
            if (_forearmR != null) _foreRBase = _forearmR.localRotation;
            if (_upperArmL != null) _upperLBase = _upperArmL.localRotation;
            if (_forearmL != null) _foreLBase = _forearmL.localRotation;
        }

        private Transform SafeBone(HumanBodyBones bone)
        {
            try { return _animator != null && _animator.isHuman ? _animator.GetBoneTransform(bone) : null; }
            catch { return null; }
        }

        private void ApplyProceduralPunch()
        {
            float t = Mathf.Clamp01((Time.time - _punchStarted) / cooldown);
            float attack = t < 0.42f ? Mathf.SmoothStep(0f, 1f, t / 0.42f) : Mathf.SmoothStep(1f, 0f, (t - 0.42f) / 0.58f);
            float twist = Mathf.Sin(attack * Mathf.PI * 0.5f);

            if (_punchVariant)
            {
                if (_chest != null) _chest.localRotation = _chestBase * Quaternion.Euler(0f, 28f * twist, -5f * twist);
                if (_upperArmR != null) _upperArmR.localRotation = _upperRBase * Quaternion.Euler(-18f * attack, 12f * attack, -72f * attack);
                if (_forearmR != null) _forearmR.localRotation = _foreRBase * Quaternion.Euler(0f, 0f, -64f * attack);
            }
            else
            {
                if (_chest != null) _chest.localRotation = _chestBase * Quaternion.Euler(0f, -24f * twist, 5f * twist);
                if (_upperArmL != null) _upperArmL.localRotation = _upperLBase * Quaternion.Euler(-12f * attack, -10f * attack, 68f * attack);
                if (_forearmL != null) _forearmL.localRotation = _foreLBase * Quaternion.Euler(0f, 0f, 58f * attack);
            }
        }
    }
}
