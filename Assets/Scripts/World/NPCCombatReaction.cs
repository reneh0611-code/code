using System.Collections;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
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
        private bool _hardLocked;
        private bool _down;
        private Transform _attacker;

        // Normal Hit/HeavyHit/Stun/Flee can be interrupted by the player's next combo punch.
        // Knockdown, lying and GetUp are intentionally protected.
        public bool CanReceiveHit => !_hardLocked && !_down;
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

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            SetWandererEnabled(false);
            FaceAttacker();

            if (_hitCount >= 4)
            {
                // Fourth punch is special: if the NPC is still inside Hit3's reaction,
                // that reaction is cancelled immediately and Knockdown starts NOW.
                _routine = StartCoroutine(FourthHitKnockdownSequence());
            }
            else
            {
                _routine = StartCoroutine(ReactionSequence(_hitCount));
            }
        }

        private IEnumerator ReactionSequence(int hitNumber)
        {
            _hardLocked = false;

            _normalHitToggle++;
            int baseHit = (_normalHitToggle % 2 == 0) ? Hit2Hash : Hit1Hash;
            PlayState(baseHit, .02f);
            yield return WaitForStateComplete(baseHit, maxStateSeconds);

            if (hitNumber == 2)
            {
                // This can also be interrupted by a third combo hit.
                PlayState(HeavyHitHash, .025f);
                yield return WaitForStateComplete(HeavyHitHash, maxStateSeconds);
                PlayState(IdleHash, .04f);
                yield return InterruptibleWait(secondHitExtraStun);
            }

            if (_routine == null) yield break;
            yield return FleeSequence();
            _routine = null;
        }

        private IEnumerator FourthHitKnockdownSequence()
        {
            // No extra Hit animation here: the player is striking INTO the currently playing
            // third-hit reaction, so the fourth impact itself immediately launches Knockdown.
            _hardLocked = true;
            yield return KnockdownSequence();
            _hitCount = 0;
            _hardLocked = false;

            if (!_down)
                yield return FleeSequence();

            _routine = null;
        }

        private IEnumerator KnockdownSequence()
        {
            _down = true;
            Vector3 bodyStart = _body != null ? _body.bounds.center : transform.position;
            Vector3 lastTravel = Vector3.zero;

            PlayState(KnockdownHash, .02f);

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
            yield return new WaitForSeconds(knockdownLieSeconds);

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

        private IEnumerator InterruptibleWait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        private void BakeLandingIntoRoot(Vector3 worldTravel)
        {
            if (worldTravel.sqrMagnitude < .0001f) return;

            transform.position += worldTravel;

            if (_visualRoot != null)
                _visualRoot.localPosition -= transform.InverseTransformVector(worldTravel);

            Vector3 origin = transform.position + Vector3.up * 5f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < .55f) continue;
                if (!found || hit.point.y > bestY) { bestY = hit.point.y; found = true; }
            }

            if (found)
            {
                Vector3 p = transform.position;
                p.y = bestY + .05f;
                transform.position = p;
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
                // Vulnerable while fleeing too; a new punch simply cancels this coroutine.
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
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_visualRoot != null) _visualRoot.localPosition = _visualBaseLocalPosition;
            if (_wanderer != null) _wanderer.enabled = true;
        }
    }
}
