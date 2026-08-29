using System.Collections;
using CheatOnYourDayOnes.World;
using CheatOnYourDayOnes.CameraSystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Player
{
    public sealed class CorpseCarryController : MonoBehaviour
    {
        [SerializeField, Min(.5f)] private float pickupDistance = 2.25f;
        [SerializeField, Range(-1f, 1f)] private float pickupFacingThreshold = -.15f;
        [SerializeField, Range(.1f, .9f)] private float attachAtNormalizedTime = .58f;
        [SerializeField, Range(.1f, .9f)] private float releaseAtNormalizedTime = .48f;
        [SerializeField, Range(.2f, 1f)] private float pullingSpeedMultiplier = .52f;
        [SerializeField, Min(0f)] private float animationBlend = .14f;
        [SerializeField, Min(1f)] private float maximumTransitionSeconds = 6f;

        private static readonly int PullStartHash = Animator.StringToHash("Base Layer.PullStart");
        private static readonly int PullHash = Animator.StringToHash("Base Layer.Pull");
        private static readonly int PullStopHash = Animator.StringToHash("Base Layer.PullStop");

        private NetworkObject _networkObject;
        private NetworkPlayerController _movement;
        private CharacterAnimationDriver _animationDriver;
        private Animator _animator;
        private ThirdPersonCamera _camera;
        private NPCWanderer _carriedBody;
        private Coroutine _transitionRoutine;
        private bool _busy;

        public bool HasCarriedBody => _carriedBody != null;
        public bool BlocksCombat => _busy || HasCarriedBody;
        public bool CanPickupBody => !_busy && FindNearestBody() != null;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            RefreshReferences();
        }

        private void RefreshReferences()
        {
            if (_movement == null) _movement = GetComponent<NetworkPlayerController>();
            if (_animationDriver == null) _animationDriver = GetComponent<CharacterAnimationDriver>();
            if (_camera == null) _camera = Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
            Transform visual = transform.Find("CharacterVisual");
            Animator current = visual != null ? visual.GetComponentInChildren<Animator>(true) : GetComponentInChildren<Animator>(true);
            if (current != null) _animator = current;
        }

        private void Update()
        {
            if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner) return;

            if (_carriedBody != null && transform.parent != null)
            {
                ForceRelease();
                return;
            }

            if (_busy || Keyboard.current == null || !Keyboard.current.gKey.wasPressedThisFrame) return;
            if (_carriedBody != null) _transitionRoutine = StartCoroutine(PullStopSequence());
            else
            {
                NPCWanderer body = FindNearestBody();
                if (body != null) _transitionRoutine = StartCoroutine(PullStartSequence(body));
            }
        }

        private IEnumerator PullStartSequence(NPCWanderer body)
        {
            _busy = true;
            RefreshReferences();
            PreparePullAnimation();

            bool attached = false;
            bool stateExists = PlayState(PullStartHash);
            float started = Time.time;
            bool entered = false;
            while (stateExists && Time.time - started < maximumTransitionSeconds)
            {
                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == PullStartHash)
                {
                    entered = true;
                    if (!attached && state.normalizedTime >= attachAtNormalizedTime)
                    {
                        attached = body != null && body.BeginCarry(transform);
                        if (attached) _carriedBody = body;
                    }
                    if (state.normalizedTime >= .985f && !_animator.IsInTransition(0)) break;
                }
                else if (entered) break;
                yield return null;
            }

            if (!attached && body != null)
            {
                attached = body.BeginCarry(transform);
                if (attached) _carriedBody = body;
            }

            if (attached)
            {
                PlayState(PullHash);
                if (_movement != null)
                {
                    _movement.SetCarryMovement(true, pullingSpeedMultiplier * .8f);
                    _movement.SetCombatMovementLocked(false);
                }
            }
            else
            {
                RestoreLocomotion();
            }

            _busy = false;
            _transitionRoutine = null;
        }

        private IEnumerator PullStopSequence()
        {
            _busy = true;
            RefreshReferences();
            if (_movement != null)
            {
                _movement.SetCarryMovement(false);
                _movement.SetCombatMovementLocked(true);
            }

            bool released = false;
            bool stateExists = PlayState(PullStopHash);
            float started = Time.time;
            bool entered = false;
            while (stateExists && Time.time - started < maximumTransitionSeconds)
            {
                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == PullStopHash)
                {
                    entered = true;
                    if (!released && state.normalizedTime >= releaseAtNormalizedTime)
                    {
                        ReleaseBody();
                        released = true;
                    }
                    if (state.normalizedTime >= .985f && !_animator.IsInTransition(0)) break;
                }
                else if (entered) break;
                yield return null;
            }

            if (!released) ReleaseBody();
            RestoreLocomotion();
            _busy = false;
            _transitionRoutine = null;
        }

        private void PreparePullAnimation()
        {
            if (_movement != null)
            {
                _movement.SetCarryMovement(false);
                _movement.SetCombatMovementLocked(true);
            }
            if (_animationDriver != null) _animationDriver.enabled = false;
            if (_camera != null) _camera.EnterPullingMode(transform);
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.applyRootMotion = false;
                _animator.speed = 1f;
            }
        }

        private bool PlayState(int stateHash)
        {
            if (_animator == null || !_animator.HasState(0, stateHash)) return false;
            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(stateHash, animationBlend, 0, 0f);
            return true;
        }

        private void RestoreLocomotion()
        {
            if (_movement != null)
            {
                _movement.SetCarryMovement(false);
                _movement.SetCombatMovementLocked(false);
            }
            if (_animator != null) _animator.speed = 1f;
            if (_camera != null) _camera.ExitPullingMode(transform);
            if (_animationDriver != null)
            {
                _animationDriver.enabled = true;
                _animationDriver.ResumeFromCombat(.16f);
            }
        }

        private void ReleaseBody()
        {
            if (_carriedBody != null) _carriedBody.DropCarriedBody();
            _carriedBody = null;
        }

        private void ForceRelease()
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
            ReleaseBody();
            _busy = false;
            RestoreLocomotion();
        }

        private NPCWanderer FindNearestBody()
        {
            if (_carriedBody != null || transform.parent != null) return null;
            float bestSqr = pickupDistance * pickupDistance;
            NPCWanderer best = null;
            foreach (NPCWanderer npc in NPCWanderer.ActiveNpcs)
            {
                if (npc == null || !npc.IsDead || npc.IsCarried) continue;
                Vector3 toBody = npc.DownPosition - transform.position;
                toBody.y = 0f;
                float sqr = toBody.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                float distance = Mathf.Sqrt(Mathf.Max(.0001f, sqr));
                if (Vector3.Dot(transform.forward, toBody / distance) < pickupFacingThreshold) continue;
                bestSqr = sqr;
                best = npc;
            }
            return best;
        }

        private void OnDisable()
        {
            if (_carriedBody != null || _busy) ForceRelease();
        }
    }
}
