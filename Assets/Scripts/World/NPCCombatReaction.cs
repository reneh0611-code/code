using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    /// <summary>
    /// Owns NPC melee reactions independently from wandering so combat animations cannot be
    /// overwritten by locomotion. Keeps the NPC root at the visual knockdown landing position.
    /// </summary>
    public sealed class NPCCombatReaction : MonoBehaviour
    {
        [SerializeField] private float normalStateFinish = .985f;
        [SerializeField] private float maxStateSeconds = 4f;
        [SerializeField] private float secondHitExtraStun = .9f;
        [SerializeField] private float knockdownLieSeconds = 2.6f;
        [SerializeField] private float fleeSeconds = 4.5f;
        [SerializeField] private float fleeSpeed = 2.25f;
        [SerializeField] private float maxKnockdownTravel = 3.5f;

        private static readonly int Hit1Hash = Animator.StringToHash("Base Layer.Hit1");
        private static readonly int Hit2Hash = Animator.StringToHash("Base Layer.Hit2");
        private static readonly int HeavyHitHash = Animator.StringToHash("Base Layer.HeavyHit");
        private static readonly int KnockdownHash = Animator.StringToHash("Base Layer.Knockdown");
        private static readonly int GetUpHash = Animator.StringToHash("Base Layer.GetUp");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");

        private Animator _animator;
        private NPCWanderer _wanderer;
        private CharacterController _controller;
        private SkinnedMeshRenderer _body;
        private Transform _visualRoot;
        private Vector3 _visualBaseLocalPosition;
        private Coroutine _routine;
        private int _hitCount;
        private int _normalHitToggle;
        private bool _reactionLocked;
        private bool _down;
        private Transform _attacker;

        public bool CanReceiveHit => !_reactionLocked && !_down;
        public bool IsDown => _down;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _wanderer = GetComponent<NPCWanderer>();
            _controller = GetComponent<CharacterController>();

            if (_animator != null)
            {
                _visualRoot = _animator.transform;
                _visualBaseLocalPosition = _visualRoot.localPosition;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            float best = -1f;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin == null) continue;
                Vector3 s = skin.bounds.size;
                float volume = Mathf.Abs(s.x * s.y * s.z);
                if (volume > best) { best = volume; _body = skin; }
            }
            if (_body != null) _body.updateWhenOffscreen = true;
        }

        public void TakePunch(Transform attacker)
        {
            if (!CanReceiveHit || _animator == null) return;

            _hitCount++;
            _attacker = attacker;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ReactionSequence(_hitCount));
        }

        private IEnumerator ReactionSequence(int hitNumber)
        {
            _reactionLocked = true;
            SetWandererEnabled(false);

            FaceAttacker();

            // EVERY successful hit always gets a visible base Hit reaction first.
            _normalHitToggle++;
            int baseHit = (_normalHitToggle % 2 == 0) ? Hit2Hash : Hit1Hash;
            PlayState(baseHit, .02f);
            yield return WaitForStateComplete(baseHit, maxStateSeconds);

            if (hitNumber == 2)
            {
                // Second hit: normal hit PLUS the heavier reaction and a short stun.
                PlayState(HeavyHitHash, .025f);
                yield return WaitForStateComplete(HeavyHitHash, maxStateSeconds);
                PlayState(IdleHash, .04f);
                yield return new WaitForSeconds(secondHitExtraStun);
            }
            else if (hitNumber >= 4)
            {
                // Fourth hit: normal hit PLUS knockdown. The animation's horizontal travel is baked
                // into the NPC root so GetUp begins where the body actually landed.
                yield return KnockdownSequence();
                _hitCount = 0;
            }

            _reactionLocked = false;
            if (!_down)
                yield return FleeSequence();

            _routine = null;
        }

        private IEnumerator KnockdownSequence()
        {
            _down = true;

            Vector3 bodyStart = _body != null ? _body.bounds.center : transform.position;
            Vector3 lastTravel = Vector3.zero;

            PlayState(KnockdownHash, .025f);

            float started = Time.time;
            bool entered = false;
            while (Time.time - started < maxStateSeconds)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (info.fullPathHash == KnockdownHash)
                {
                    entered = true;
                    if (_body != null)
                    {
                        lastTravel = _body.bounds.center - bodyStart;
                        lastTravel.y = 0f;
                        if (lastTravel.magnitude > maxKnockdownTravel)
                            lastTravel = lastTravel.normalized * maxKnockdownTravel;
                    }

                    if (info.normalizedTime >= normalStateFinish && !_animator.IsInTransition(0))
                        break;
                }
                else if (entered)
                {
                    break;
                }
                yield return null;
            }

            BakeLandingIntoRoot(lastTravel);

            // Keep the final knockdown pose exactly where it landed.
            yield return new WaitForSeconds(knockdownLieSeconds);

            // Force the first frame of GetUp at the NEW root position, then remove the temporary
            // visual counter-offset. This prevents the body teleporting back to its old position.
            if (_animator.HasState(0, GetUpHash))
            {
                _animator.Play(GetUpHash, 0, 0f);
                _animator.Update(0f);
                if (_visualRoot != null) _visualRoot.localPosition = _visualBaseLocalPosition;
                yield return WaitForStateComplete(GetUpHash, maxStateSeconds, .995f);
            }

            if (_visualRoot != null) _visualRoot.localPosition = _visualBaseLocalPosition;
            _down = false;
        }

        private void BakeLandingIntoRoot(Vector3 worldTravel)
        {
            if (worldTravel.sqrMagnitude < .0001f) return;

            // Move the actual NPC/gameplay root to the visual body's landing point.
            transform.position += worldTravel;

            // Counter-shift the visual while holding the knockdown end pose so there is no pop now.
            if (_visualRoot != null)
                _visualRoot.localPosition -= transform.InverseTransformVector(worldTravel);

            // Keep the root grounded at its new X/Z location.
            Vector3 origin = transform.position + Vector3.up * 5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.transform.IsChildOf(transform))
                {
                    Vector3 p = transform.position;
                    p.y = hit.point.y + .05f;
                    transform.position = p;
                }
            }
        }

        private IEnumerator FleeSequence()
        {
            if (_attacker == null)
            {
                SetWandererEnabled(true);
                yield break;
            }

            PlayState(RunHash, .055f);
            float until = Time.time + fleeSeconds;

            while (Time.time < until)
            {
                // A new valid punch can interrupt fleeing, but not an active Hit/Knockdown/GetUp.
                _reactionLocked = false;

                Vector3 away = transform.position - _attacker.position;
                away.y = 0f;
                if (away.sqrMagnitude < .001f) away = -transform.forward;
                away.Normalize();

                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), 1f - Mathf.Exp(-8f * Time.deltaTime));

                if (_controller != null && _controller.enabled)
                    _controller.Move(away * fleeSpeed * Time.deltaTime);
                else
                    transform.position += away * fleeSpeed * Time.deltaTime;

                yield return null;
            }

            _reactionLocked = false;
            SetWandererEnabled(true);
        }

        private IEnumerator WaitForStateComplete(int stateHash, float timeout, float finish = -1f)
        {
            float target = finish > 0f ? finish : normalStateFinish;
            float started = Time.time;
            bool entered = false;

            while (Time.time - started < timeout)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (info.fullPathHash == stateHash)
                {
                    entered = true;
                    if (info.normalizedTime >= target && !_animator.IsInTransition(0)) yield break;
                }
                else if (entered)
                {
                    yield break;
                }
                yield return null;
            }
        }

        private void PlayState(int stateHash, float fade)
        {
            if (_animator == null || !_animator.HasState(0, stateHash)) return;
            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(stateHash, fade, 0, 0f);
        }

        private void FaceAttacker()
        {
            if (_attacker == null) return;
            Vector3 toward = _attacker.position - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(toward.normalized);
        }

        private void SetWandererEnabled(bool value)
        {
            if (_wanderer != null) _wanderer.enabled = value;
        }

        private void OnDisable()
        {
            if (_visualRoot != null) _visualRoot.localPosition = _visualBaseLocalPosition;
            if (_wanderer != null) _wanderer.enabled = true;
        }
    }
}
