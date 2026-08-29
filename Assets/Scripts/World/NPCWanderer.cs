using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using System.Collections.Generic;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour, IPlayerStrikeTarget
    {
        private static readonly HashSet<NPCWanderer> ActiveNpcSet = new();
        public static IEnumerable<NPCWanderer> ActiveNpcs => ActiveNpcSet;

        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 1.35f, wanderRadius = 10f, turnSpeed = 5f, minPause = 1.25f, maxPause = 4f, gravity = -20f;
        [SerializeField, Min(.1f)] private float movementAcceleration = 5.5f;
        [SerializeField, Min(.1f)] private float movementDeceleration = 8f;

        [Header("Grounding and avoidance")]
        [SerializeField, Min(.1f)] private float groundStickSpeed = 3.5f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 32f;
        [SerializeField] private float toeToSoleDistance = .025f;
        [SerializeField] private float footToSoleDistance = .085f;
        [SerializeField] private float surfaceSink = .012f;
        [SerializeField, Min(.1f)] private float maxGroundPoseCorrection = .75f;
        [SerializeField, Min(.1f)] private float downPoseGroundingSpeed = 1.15f;
        [SerializeField, Min(.05f)] private float awarenessInterval = .24f;
        [SerializeField, Min(.05f)] private float obstacleScanInterval = .16f;
        [SerializeField, Min(.2f)] private float obstacleLookAhead = .85f;
        [SerializeField, Min(.1f)] private float personalSpace = .75f;
        [SerializeField, Min(.1f)] private float playerPersonalSpace = .35f;
        [SerializeField, Min(.5f)] private float playerAvoidanceRange = 1.55f;
        [SerializeField, Min(.5f)] private float playerSeparationSpeed = 3.5f;

        [Header("Vehicle reaction")]
        [SerializeField] private float carAwarenessRadius = 7f, fleeSpeed = 2.25f, fleeOnlyAboveKmh = 30f, impactSpeedThreshold = .20f;
        [SerializeField] private float minimumLieSeconds = 1.25f, impactCarryDistance = .20f;
        [SerializeField] private float groundRayHeight = 6f, groundRayDistance = 20f;
        [SerializeField] private float carClearanceBeforeGetUp = 3.2f, extraWaitAfterCarClears = .65f, maxAnimationFallTravel = 4f;
        [SerializeField, Range(-1f, 1f)] private float rearHitDotThreshold = -.25f;

        [Header("Player punch escalation")]
        [SerializeField] private float fleeAfterPunchSeconds = 5f;
        [SerializeField] private float punchFleeSpeed = 2.35f;
        [SerializeField, Range(.75f, 1f)] private float hitFinishNormalizedTime = .97f;
        [SerializeField] private float maxHitAnimationSeconds = 3f;
        [SerializeField] private float secondHitExtraStunSeconds = .9f;
        [SerializeField] private int knockdownOnHit = 4;
        [SerializeField] private float knockdownLieSeconds = 2.6f;
        [SerializeField] private float maxKnockdownAnimationSeconds = 4f;
        [SerializeField] private float maxGetUpAnimationSeconds = 4f;
        [SerializeField, Min(.1f)] private float fleeAccelerationSeconds = .55f;
        [SerializeField, Range(0f, 1f)] private float fleeRunAlignment = .72f;
        [SerializeField, Min(0f)] private float hitRecoilSpeed = 1.35f;

        [Header("Combat blending")]
        [SerializeField, Min(0f)] private float hitBlend = .13f;
        [SerializeField, Min(0f)] private float heavyHitBlend = .16f;
        [SerializeField, Min(0f)] private float knockdownBlend = .17f;
        [SerializeField, Min(0f)] private float getUpBlend = .20f;
        [SerializeField, Min(0f)] private float returnToIdleBlend = .18f;
        [SerializeField, Min(.1f)] private float meleeTurnSharpness = 10f;

        [Header("Witness reaction")]
        [SerializeField, Min(1f)] private float witnessEscalationSeconds = 10f;
        [SerializeField, Min(5f)] private float witnessEscapeDistance = 18f;
        [SerializeField, Min(.1f)] private float witnessRedIndicatorSeconds = 3.5f;
        [SerializeField, Min(1f)] private float witnessFleeSeconds = 7f;
        [SerializeField, Min(0f)] private float policeCallBlend = .20f;

        [Header("NPC counter combat")]
        [SerializeField, Range(0f, 1f)] private float counterAttackChance = .50f;
        [SerializeField, Min(1f)] private float counterCombatSeconds = 9f;
        [SerializeField, Min(1f)] private float counterMaximumChaseDistance = 8f;
        [SerializeField, Min(.5f)] private float counterAttackRange = 1.60f;
        [SerializeField, Min(.1f)] private float counterChaseSpeed = 2.05f;
        [SerializeField, Min(0f)] private float counterAttackDamage = 12f;
        [SerializeField, Range(.05f, .9f)] private float counterHitNormalizedTime = .34f;
        [SerializeField, Min(0f)] private float counterAttackBlend = .11f;

        [Header("Ground finisher and carrying")]
        [SerializeField, Min(0f)] private float dyingBlend = .16f;
        [SerializeField, Min(1f)] private float maximumDyingAnimationSeconds = 6f;
        [SerializeField] private Vector3 carriedLocalPosition = new(0f, .32f, .92f);
        [SerializeField] private Vector3 carriedLocalEuler = new(0f, 180f, 0f);
        [SerializeField, Min(.5f)] private float bodyDropForwardDistance = 1.35f;

        private const float ClipFinishedNormalizedTime = .985f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int FallHash = Animator.StringToHash("Base Layer.Fall");
        private static readonly int GettingUpHash = Animator.StringToHash("Base Layer.GettingUp");
        private static readonly int FallRearHash = Animator.StringToHash("Base Layer.FallRear");
        private static readonly int GettingUpRearHash = Animator.StringToHash("Base Layer.GettingUpRear");
        private static readonly int Hit1Hash = Animator.StringToHash("Base Layer.Hit1");
        private static readonly int Hit2Hash = Animator.StringToHash("Base Layer.Hit2");
        private static readonly int HeavyHitHash = Animator.StringToHash("Base Layer.HeavyHit");
        private static readonly int KnockdownHash = Animator.StringToHash("Base Layer.Knockdown");
        private static readonly int GetUpHash = Animator.StringToHash("Base Layer.GetUp");
        private static readonly int PoliceCallHash = Animator.StringToHash("Base Layer.PoliceCall");
        private static readonly int Punch1Hash = Animator.StringToHash("Base Layer.Punch1");
        private static readonly int Punch2Hash = Animator.StringToHash("Base Layer.Punch2");
        private static readonly int DyingHash = Animator.StringToHash("Base Layer.Dying");

        private enum MeleePhase { None, Reaction, HeavyReaction, HeavyStun, Knockdown, Lying, GetUp, CounterAttack, Finisher, Dead }
        private enum WitnessPhase { None, Calling, Escalated }

        private CharacterController _controller;
        private readonly RaycastHit[] _groundHits = new RaycastHit[12];
        private readonly RaycastHit[] _obstacleHits = new RaycastHit[8];
        private readonly List<GroundContactBone> _groundContactBones = new(24);
        private Vector3 _home, _target, _impactAnchor, _fallOriginAnchor, _fallBodyStartCenter;
        private Vector3 _meleeFallOriginAnchor, _meleeFallBodyStartCenter;
        private Vector3 _currentPlanarVelocity;
        private float _pause, _verticalVelocity, _fallEarliestGetUp, _safeGetUpAfter;
        private float _nextAwarenessCheck, _nextObstacleScan;
        private float _nextPlayerLookup;
        private Vector3 _avoidanceDirection;
        private Vector3 _groundNormal = Vector3.up;
        private bool _walking, _running, _gettingUp, _rearImpact, _fallMotionTracking;
        private DriveableCar _dangerCar;
        private SkinnedMeshRenderer _mainSkinnedMesh;
        private Collider[] _allColliders;
        private bool[] _colliderStates;

        private Transform _meleeAttacker, _visualRoot, _meleeMotionReference;
        private Transform _leftFoot, _rightFoot, _leftToe, _rightToe;
        private float _groundContactRadiusScale = 1f;
        private MeleePhase _meleePhase;
        private float _meleePhaseStarted;
        private float _meleePhaseUntil;
        private float _forcedFleeUntil;
        private float _fleeAccelerationStarted;
        private int _expectedMeleeState;
        private bool _enteredExpectedMeleeState;
        private int _playerHitCount;
        private int _normalHitToggle;
        private Quaternion _visualBaseRotation;
        private Vector3 _visualBaseLocalPosition;
        private Vector3 _visualRestoreStartLocalPosition;
        private Quaternion _visualRestoreStartLocalRotation;
        private Vector3 _meleeFacingDirection;
        private Vector3 _meleeLandingPoseAnchor;
        private bool _meleeFallTracking;
        private bool _visualRestoreActive;
        private NetworkPlayerController _playerToAvoid;
        private CharacterController _playerControllerToAvoid;
        private bool _animationImportanceInitialized;
        private bool _highFidelityAnimation;
        private WitnessPhase _witnessPhase;
        private Transform _witnessSuspect;
        private float _witnessEscalatesAt;
        private float _witnessRedUntil;
        private float _nextWitnessIncidentAllowed;
        private Transform _witnessMarker;
        private TextMesh _witnessMarkerText;
        private Vector3 _witnessMarkerBaseLocalPosition;
        private bool _counterCombatActive;
        private bool _willFightBack;
        private bool _counterHitApplied;
        private int _counterPunchToggle;
        private float _counterCombatUntil;
        private float _nextCounterAttack;
        private float _lastPlayerHitTime = -999f;
        private Transform _carrier;
        private Transform _parentBeforeCarry;
        private Transform _downHitboxRoot;
        private BoxCollider _downHitbox;
        private NPCCorpseRagdoll _corpseRagdoll;

        private bool VehicleDown => _fallEarliestGetUp > 0f || _gettingUp;
        private bool MeleeDown => _meleePhase == MeleePhase.Knockdown || _meleePhase == MeleePhase.Lying ||
                                  _meleePhase == MeleePhase.GetUp || _meleePhase == MeleePhase.Finisher ||
                                  _meleePhase == MeleePhase.Dead;
        public bool IsDown => VehicleDown || MeleeDown;
        public bool IsDead => _meleePhase == MeleePhase.Finisher || _meleePhase == MeleePhase.Dead;
        public bool IsCarried => _carrier != null;
        public bool IsFinishable => _meleePhase == MeleePhase.Lying || IsKnockdownReadyForFinisher();
        public bool CanReceivePlayerStrike => !IsDead && (!IsDown || IsFinishable);
        public Vector3 DownPosition => _corpseRagdoll != null && _corpseRagdoll.IsActive
            ? _corpseRagdoll.BodyCenter
            : _mainSkinnedMesh != null && IsDown ? _mainSkinnedMesh.bounds.center : _impactAnchor;
        public Vector3 StrikeTargetPosition => IsDown ? DownPosition : transform.position;
        public bool IsCallingPolice => _witnessPhase != WitnessPhase.None;
        internal bool CanReactAsWitness => isActiveAndEnabled && !IsDown && _meleePhase == MeleePhase.None &&
                                            _witnessPhase == WitnessPhase.None && _dangerCar == null;

        private readonly struct GroundContactBone
        {
            public readonly Transform transform;
            public readonly float radius;
            public readonly bool scaleRadius;

            public GroundContactBone(Transform transform, float radius, bool scaleRadius)
            {
                this.transform = transform;
                this.radius = radius;
                this.scaleRadius = scaleRadius;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                _visualRoot = animator.transform;
                _visualBaseRotation = _visualRoot.localRotation;
                _visualBaseLocalPosition = _visualRoot.localPosition;
                _meleeMotionReference = FindMeleeMotionReference();
            }
            CacheBody();
            CreateDownBodyHitbox();
            PrepareGroundingBones();
            CacheColliders();
            SetAnimationImportance(false);
        }

        private void OnEnable()
        {
            ActiveNpcSet.Add(this);
            _nextAwarenessCheck = Time.time + Random.Range(0f, awarenessInterval);
            _nextObstacleScan = Time.time + Random.Range(0f, obstacleScanInterval);
        }

        private void OnDisable()
        {
            ActiveNpcSet.Remove(this);
            ClearWitnessMarker();
            _witnessPhase = WitnessPhase.None;
            _witnessSuspect = null;
        }

        private void CacheBody()
        {
            float best = -1f;
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin == null) continue;
                Vector3 s = skin.bounds.size;
                float v = Mathf.Abs(s.x * s.y * s.z);
                if (v > best) { best = v; _mainSkinnedMesh = skin; }
            }
            if (_mainSkinnedMesh != null) _mainSkinnedMesh.updateWhenOffscreen = true;
        }

        private void CacheColliders()
        {
            _allColliders = GetComponentsInChildren<Collider>(true);
            _colliderStates = new bool[_allColliders.Length];
        }

        private void Start()
        {
            _home = transform.position;
            Pause(Random.Range(.4f, 2.5f));
        }

        private void Update()
        {
            bool important = VehicleDown || (_meleePhase != MeleePhase.None && _meleePhase != MeleePhase.Dead) ||
                             _witnessPhase != WitnessPhase.None;
            SetAnimationImportance(important);

            if (IsCarried) return;

            if (VehicleDown)
            {
                UpdateVehicleDown();
                return;
            }

            if (UpdateMeleePhase()) return;
            if (UpdateWitnessCall()) return;

            if (Time.time >= _nextAwarenessCheck)
            {
                _nextAwarenessCheck = Time.time + awarenessInterval + Random.Range(0f, .05f);
                FindDangerousCar();
            }
            Vector3 move = Vector3.zero;

            if (_dangerCar != null)
            {
                Vector3 away = transform.position - _dangerCar.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < .01f) away = transform.right;
                away = GetSteeredDirection(away.normalized);
                Face(away, turnSpeed * 1.45f);
                move = away * fleeSpeed;
                SetRunning(true);
            }
            else if (TryUpdateCounterCombat(out Vector3 counterMove))
            {
                move = counterMove;
            }
            else if (_meleeAttacker != null && Time.time < _forcedFleeUntil)
            {
                Vector3 away = transform.position - _meleeAttacker.position;
                away.y = 0f;
                if (away.sqrMagnitude < .01f) away = -transform.forward;
                away = GetSteeredDirection(away.normalized);
                Face(away, turnSpeed * 1.45f);

                float elapsed = Time.time - _fleeAccelerationStarted;
                float ramp = Mathf.Clamp01(elapsed / Mathf.Max(.1f, fleeAccelerationSeconds));
                ramp = ramp * ramp * (3f - 2f * ramp);
                float alignment = Mathf.Clamp01((Vector3.Dot(transform.forward, away) + 1f) * .5f);
                float speedFactor = ramp * Mathf.Lerp(.18f, 1f, alignment);

                // Movement follows the body while it turns. This prevents the NPC from sliding or
                // sprinting backwards before its GetUp-facing has rotated into the flee direction.
                move = transform.forward * (punchFleeSpeed * speedFactor);
                if (ramp >= .55f && alignment >= fleeRunAlignment)
                {
                    SetRunning(true);
                }
                else
                {
                    if (_running) SetRunning(false);
                    SetWalking(true);
                }
            }
            else if (_pause > 0f)
            {
                _meleeAttacker = null;
                SetRunning(false);
                _pause -= Time.deltaTime;
                if (_pause <= 0f) PickTarget();
            }
            else
            {
                _meleeAttacker = null;
                SetRunning(false);
                Vector3 to = _target - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < .16f) Pause(Random.Range(minPause, maxPause));
                else
                {
                    Vector3 d = GetSteeredDirection(to.normalized);
                    Face(d, turnSpeed);
                    move = d * walkSpeed;
                    SetWalking(true);
                }
            }

            MoveWithGravity(move);
        }

        private void UpdateVehicleDown()
        {
            if (!_gettingUp)
            {
                bool fallFinished = IsActiveClipFinished(_rearImpact ? FallRearHash : FallHash, FallHash);
                if (Time.time >= _fallEarliestGetUp && fallFinished)
                {
                    if (IsCarTooClose()) _safeGetUpAfter = Time.time + extraWaitAfterCarClears;
                    else if (Time.time >= _safeGetUpAfter) StartGettingUp();
                }
            }
            else
            {
                bool getUpFinished = IsActiveClipFinished(_rearImpact ? GettingUpRearHash : GettingUpHash, GettingUpHash);
                if (getUpFinished) FinishGettingUp();
            }
        }

        private bool UpdateMeleePhase()
        {
            if (_meleePhase == MeleePhase.Reaction ||
                _meleePhase == MeleePhase.HeavyReaction ||
                _meleePhase == MeleePhase.HeavyStun ||
                _meleePhase == MeleePhase.CounterAttack)
                UpdateMeleeFacing();

            switch (_meleePhase)
            {
                case MeleePhase.None:
                    return false;

                case MeleePhase.Reaction:
                    if (MeleeAnimationFinished(maxHitAnimationSeconds))
                    {
                        _meleePhase = MeleePhase.None;
                        BeginPostHitBehaviour();
                        return false;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.HeavyReaction:
                    if (MeleeAnimationFinished(maxHitAnimationSeconds))
                    {
                        _meleePhase = MeleePhase.HeavyStun;
                        _meleePhaseUntil = Time.time + secondHitExtraStunSeconds;
                        PlayState(IdleHash, returnToIdleBlend);
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.HeavyStun:
                    if (Time.time >= _meleePhaseUntil)
                    {
                        _meleePhase = MeleePhase.None;
                        BeginPostHitBehaviour();
                        return false;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.Knockdown:
                    if (MeleeAnimationFinished(maxKnockdownAnimationSeconds))
                    {
                        CaptureMeleeLandingPose();
                        _meleePhase = MeleePhase.Lying;
                        _meleePhaseUntil = Time.time + knockdownLieSeconds;
                        if (animator != null) animator.speed = 0f;
                        SetDownBodyHitbox(true);
                        if (_controller != null) _controller.enabled = false;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.Lying:
                    if (Time.time >= _meleePhaseUntil)
                    {
                        StartMeleeGetUp();
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.GetUp:
                    if (MeleeAnimationFinished(maxGetUpAnimationSeconds))
                    {
                        FinalizeMeleeGetUpPosition();
                        _meleePhase = MeleePhase.None;
                        _playerHitCount = 0;
                        BeginPostHitBehaviour();
                        return false;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.CounterAttack:
                    TryApplyCounterHit();
                    if (MeleeAnimationFinished(maxHitAnimationSeconds))
                    {
                        _meleePhase = MeleePhase.None;
                        _nextCounterAttack = Time.time + Random.Range(.22f, .46f);
                        PlayState(IdleHash, .10f);
                        return false;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.Finisher:
                    if (MeleeAnimationFinished(maximumDyingAnimationSeconds))
                    {
                        FinishDeathPose();
                        _meleePhase = MeleePhase.Dead;
                    }
                    ApplyGravityOnly();
                    return true;

                case MeleePhase.Dead:
                    _currentPlanarVelocity = Vector3.zero;
                    ApplyGravityOnly();
                    return true;
            }
            return false;
        }

        private bool MeleeAnimationFinished(float timeout)
        {
            if (animator == null) return true;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

            if (info.fullPathHash == _expectedMeleeState)
            {
                _enteredExpectedMeleeState = true;
                if (info.normalizedTime >= hitFinishNormalizedTime && !animator.IsInTransition(0)) return true;
            }
            else if (_enteredExpectedMeleeState)
            {
                return true;
            }

            return Time.time - _meleePhaseStarted >= timeout;
        }

        private bool IsKnockdownReadyForFinisher()
        {
            if (_meleePhase != MeleePhase.Knockdown) return false;
            if (animator == null) return true;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == KnockdownHash && state.normalizedTime >= .72f;
        }

        private void BeginFlee()
        {
            _counterCombatActive = false;
            _forcedFleeUntil = Time.time + fleeAfterPunchSeconds + Random.Range(0f, .8f);
            _fleeAccelerationStarted = Time.time;
            _running = false;
            _walking = false;
            SetWalking(true);
        }

        private void BeginPostHitBehaviour()
        {
            if (_willFightBack && _meleeAttacker != null)
                BeginCounterCombat();
            else
                BeginFlee();
        }

        private void BeginCounterCombat()
        {
            if (_meleeAttacker == null)
            {
                BeginFlee();
                return;
            }

            _counterCombatActive = true;
            _counterCombatUntil = Time.time + counterCombatSeconds;
            _nextCounterAttack = Time.time + Random.Range(.18f, .38f);
            _forcedFleeUntil = 0f;
            _currentPlanarVelocity = Vector3.zero;
            _walking = false;
            _running = false;
        }

        private bool TryUpdateCounterCombat(out Vector3 move)
        {
            move = Vector3.zero;
            if (!_counterCombatActive) return false;

            if (_meleeAttacker == null || Time.time >= _counterCombatUntil)
            {
                BeginFlee();
                return false;
            }

            Vector3 toPlayer = _meleeAttacker.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > counterMaximumChaseDistance)
            {
                BeginFlee();
                return false;
            }

            Vector3 direction = distance > .001f ? toPlayer / distance : transform.forward;
            _meleeFacingDirection = direction;
            if (distance > counterAttackRange)
            {
                direction = GetSteeredDirection(direction);
                Face(direction, turnSpeed * 1.25f);
                move = direction * counterChaseSpeed;
                if (distance > counterAttackRange + 1.25f) SetRunning(true);
                else
                {
                    SetRunning(false);
                    SetWalking(true);
                }
            }
            else
            {
                Face(direction, turnSpeed * 1.55f);
                SetRunning(false);
                SetWalking(false);
                if (Time.time >= _nextCounterAttack) StartCounterAttack();
            }
            return true;
        }

        private void StartCounterAttack()
        {
            if (animator == null) return;
            _counterPunchToggle++;
            int state = _counterPunchToggle % 2 == 0 ? Punch2Hash : Punch1Hash;
            if (!animator.HasState(0, state))
            {
                _counterCombatActive = false;
                BeginFlee();
                return;
            }

            _counterHitApplied = false;
            _meleePhase = MeleePhase.CounterAttack;
            StartMeleeState(state);
        }

        private void TryApplyCounterHit()
        {
            if (_counterHitApplied || animator == null || _meleeAttacker == null) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != _expectedMeleeState || state.normalizedTime < counterHitNormalizedTime) return;
            _counterHitApplied = true;

            Vector3 distance = _meleeAttacker.position - transform.position;
            distance.y = 0f;
            if (distance.sqrMagnitude > (counterAttackRange + .30f) * (counterAttackRange + .30f)) return;

            global::MeleeAnimationBridge playerMelee = _meleeAttacker.GetComponent<global::MeleeAnimationBridge>();
            if (playerMelee != null && playerMelee.IsRolling) return;

            PlayerAgent player = _meleeAttacker.GetComponent<PlayerAgent>();
            if (player != null && player.Needs != null) player.Needs.RequestDamage(counterAttackDamage);
        }

        internal bool TryBeginPoliceCall(Transform suspect)
        {
            if (!CanReactAsWitness || suspect == null || animator == null ||
                !animator.HasState(0, PoliceCallHash)) return false;

            _witnessSuspect = suspect;
            _witnessPhase = WitnessPhase.Calling;
            _witnessEscalatesAt = Time.time + witnessEscalationSeconds;
            _witnessRedUntil = 0f;
            _meleeAttacker = null;
            _forcedFleeUntil = 0f;
            _dangerCar = null;
            _walking = false;
            _running = false;
            _pause = 0f;
            _currentPlanarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            SetAnimationImportance(true);
            CreateWitnessMarker();
            PlayState(PoliceCallHash, policeCallBlend);
            return true;
        }

        internal void FleeFromWitnessIncident(Transform suspect)
        {
            if (suspect == null || IsDown || _meleePhase != MeleePhase.None) return;
            CancelWitnessCall(false);
            _witnessSuspect = null;
            _meleeAttacker = suspect;
            _dangerCar = null;
            _forcedFleeUntil = Time.time + Mathf.Max(fleeAfterPunchSeconds, witnessFleeSeconds) + Random.Range(0f, .8f);
            _fleeAccelerationStarted = Time.time;
            _walking = false;
            _running = false;
            _pause = 0f;
            _currentPlanarVelocity = Vector3.zero;
            SetWalking(true);
        }

        private bool UpdateWitnessCall()
        {
            if (_witnessPhase == WitnessPhase.None) return false;

            if (_witnessSuspect == null || !_witnessSuspect.gameObject.activeInHierarchy)
            {
                CancelWitnessCall(true);
                ApplyGravityOnly();
                return true;
            }

            Vector3 towardSuspect = _witnessSuspect.position - transform.position;
            towardSuspect.y = 0f;
            if (towardSuspect.sqrMagnitude > .001f)
                Face(towardSuspect.normalized, turnSpeed * .55f);

            if (_witnessPhase == WitnessPhase.Calling && Time.time >= _witnessEscalatesAt)
            {
                if (towardSuspect.sqrMagnitude <= witnessEscapeDistance * witnessEscapeDistance)
                {
                    _witnessPhase = WitnessPhase.Escalated;
                    _witnessRedUntil = Time.time + witnessRedIndicatorSeconds;
                    SetWitnessMarkerColor(new Color(1f, .12f, .08f, 1f));
                    NPCWitnessCoordinator.CompletePoliceReport(transform.position, _witnessSuspect);
                }
                else
                {
                    CancelWitnessCall(true);
                    ApplyGravityOnly();
                    return true;
                }
            }
            else if (_witnessPhase == WitnessPhase.Escalated && Time.time >= _witnessRedUntil)
            {
                Transform suspect = _witnessSuspect;
                CancelWitnessCall(false);
                FleeFromWitnessIncident(suspect);
                ApplyGravityOnly();
                return true;
            }

            ApplyGravityOnly();
            return true;
        }

        private void CancelWitnessCall(bool resumeIdle)
        {
            if (_witnessPhase == WitnessPhase.None) return;
            _witnessPhase = WitnessPhase.None;
            _witnessSuspect = null;
            _witnessEscalatesAt = 0f;
            _witnessRedUntil = 0f;
            ClearWitnessMarker();
            if (resumeIdle)
            {
                PlayState(IdleHash, returnToIdleBlend);
                Pause(Random.Range(.25f, .65f));
            }
        }

        private void CreateWitnessMarker()
        {
            ClearWitnessMarker();
            GameObject marker = new("PoliceCallAlert");
            marker.transform.SetParent(transform, false);
            _witnessMarker = marker.transform;

            float height = 2.12f;
            if (_mainSkinnedMesh != null)
                height = transform.InverseTransformPoint(_mainSkinnedMesh.bounds.max).y + .24f;
            _witnessMarkerBaseLocalPosition = new Vector3(0f, height, 0f);
            _witnessMarker.localPosition = _witnessMarkerBaseLocalPosition;

            _witnessMarkerText = marker.AddComponent<TextMesh>();
            _witnessMarkerText.text = "!";
            _witnessMarkerText.fontSize = 96;
            _witnessMarkerText.characterSize = .045f;
            _witnessMarkerText.fontStyle = FontStyle.Bold;
            _witnessMarkerText.anchor = TextAnchor.MiddleCenter;
            _witnessMarkerText.alignment = TextAlignment.Center;
            SetWitnessMarkerColor(new Color(1f, .78f, .08f, 1f));
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 500;
        }

        private void SetWitnessMarkerColor(Color color)
        {
            if (_witnessMarkerText != null) _witnessMarkerText.color = color;
        }

        private void UpdateWitnessMarker()
        {
            if (_witnessMarker == null) return;
            _witnessMarker.localPosition = _witnessMarkerBaseLocalPosition +
                                           Vector3.up * (.045f * Mathf.Sin(Time.time * 4.5f));
            Camera camera = Camera.main;
            if (camera != null)
                _witnessMarker.rotation = Quaternion.LookRotation(_witnessMarker.position - camera.transform.position, camera.transform.up);
        }

        private void ClearWitnessMarker()
        {
            if (_witnessMarker != null) Destroy(_witnessMarker.gameObject);
            _witnessMarker = null;
            _witnessMarkerText = null;
        }

        private void StartMeleeGetUp()
        {
            // Cross-fade the paired poses while LateUpdate transfers only their large positional
            // mismatch into the real CharacterController. The visible hips stay on their exact
            // final Knockdown point for every transition frame.
            _currentPlanarVelocity = Vector3.zero;
            SetDownBodyHitbox(false);
            if (_controller != null) _controller.enabled = true;
            _meleePhase = MeleePhase.GetUp;
            StartMeleeState(GetUpHash);
        }

        private void StartMeleeState(int state)
        {
            _expectedMeleeState = state;
            _enteredExpectedMeleeState = false;
            _meleePhaseStarted = Time.time;
            float blend = state == KnockdownHash
                ? knockdownBlend
                : state == GetUpHash
                    ? getUpBlend
                    : state == HeavyHitHash
                        ? heavyHitBlend
                        : state == Punch1Hash || state == Punch2Hash
                            ? counterAttackBlend
                            : state == DyingHash
                                ? dyingBlend
                            : hitBlend;
            PlayState(state, blend);
        }

        private Vector3 GetSteeredDirection(Vector3 desired)
        {
            desired.y = 0f;
            if (desired.sqrMagnitude < .001f) return transform.forward;
            desired.Normalize();

            if (Time.time < _nextObstacleScan && _avoidanceDirection.sqrMagnitude > .001f)
                return _avoidanceDirection;

            _nextObstacleScan = Time.time + obstacleScanInterval + Random.Range(0f, .04f);

            Vector3 separation = Vector3.zero;
            float personalSpaceSqr = personalSpace * personalSpace;
            foreach (NPCWanderer other in ActiveNpcSet)
            {
                if (other == null || other == this || other.IsDown) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float sqr = away.sqrMagnitude;
                if (sqr < .001f || sqr >= personalSpaceSqr) continue;
                separation += away.normalized * (1f - Mathf.Sqrt(sqr) / personalSpace);
            }

            RefreshPlayerAvoidanceTarget();
            if (_playerToAvoid != null && _playerToAvoid.transform.parent == null)
            {
                Vector3 awayFromPlayer = transform.position - _playerToAvoid.transform.position;
                awayFromPlayer.y = 0f;
                float distance = awayFromPlayer.magnitude;
                if (distance < playerAvoidanceRange)
                {
                    if (distance < .001f) awayFromPlayer = -_playerToAvoid.transform.forward;
                    else awayFromPlayer /= distance;
                    float strength = 1f - Mathf.Clamp01(distance / playerAvoidanceRange);
                    separation += awayFromPlayer * (1.4f + strength * 2.2f);
                }
            }

            Vector3 steered = (desired + separation * .85f).normalized;
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(.45f, _controller.height * .45f);
            float radius = Mathf.Max(.12f, _controller.radius * .82f);
            int hitCount = Physics.SphereCastNonAlloc(origin, radius, steered, _obstacleHits, obstacleLookAhead, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 obstacleNormal = Vector3.zero;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _obstacleHits[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (hit.collider.GetComponentInParent<DriveableCar>() != null) continue;
                if (hit.normal.y > .72f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                obstacleNormal = hit.normal;
            }

            if (obstacleNormal.sqrMagnitude > .001f)
            {
                obstacleNormal.y = 0f;
                obstacleNormal.Normalize();
                Vector3 tangent = Vector3.Cross(Vector3.up, obstacleNormal).normalized;
                if (Vector3.Dot(tangent, desired) < Vector3.Dot(-tangent, desired)) tangent = -tangent;
                float urgency = 1f - Mathf.Clamp01(nearest / obstacleLookAhead);
                steered = Vector3.Slerp(steered, tangent, Mathf.Lerp(.55f, .9f, urgency)).normalized;
            }

            _avoidanceDirection = steered;
            return steered;
        }

        private void MoveWithGravity(Vector3 planarMove)
        {
            if (_controller == null || !_controller.enabled)
            {
                _currentPlanarVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                return;
            }
            planarMove = ApplyPlayerSeparation(planarMove);
            float moveRate = planarMove.sqrMagnitude > .001f ? movementAcceleration : movementDeceleration;
            _currentPlanarVelocity = Vector3.MoveTowards(_currentPlanarVelocity, planarMove, moveRate * Time.deltaTime);

            bool grounded = ProbeGround(out _groundNormal);
            if (grounded && _currentPlanarVelocity.sqrMagnitude > .001f)
                _currentPlanarVelocity = Vector3.ProjectOnPlane(_currentPlanarVelocity, _groundNormal).normalized * _currentPlanarVelocity.magnitude;

            if (grounded && _verticalVelocity <= 0f)
                _verticalVelocity = -groundStickSpeed;
            else
                _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * Time.deltaTime, -maxFallSpeed);

            CollisionFlags flags = _controller.Move((_currentPlanarVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && _verticalVelocity < -groundStickSpeed)
                _verticalVelocity = -groundStickSpeed;
        }

        private Vector3 ApplyPlayerSeparation(Vector3 planarMove)
        {
            if (IsDown) return planarMove;
            RefreshPlayerAvoidanceTarget();
            if (_playerToAvoid == null || _playerToAvoid.transform.parent != null) return planarMove;

            Vector3 toPlayer = _playerToAvoid.transform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance >= playerAvoidanceRange) return planarMove;

            Vector3 towardPlayer;
            Vector3 awayFromPlayer;
            if (distance < .001f)
            {
                awayFromPlayer = -_playerToAvoid.transform.forward;
                awayFromPlayer.y = 0f;
                if (awayFromPlayer.sqrMagnitude < .001f) awayFromPlayer = transform.right;
                awayFromPlayer.Normalize();
                towardPlayer = -awayFromPlayer;
            }
            else
            {
                towardPlayer = toPlayer / distance;
                awayFromPlayer = -towardPlayer;
            }

            float playerRadius = _playerControllerToAvoid != null ? _playerControllerToAvoid.radius : .34f;
            float minimumDistance = _controller.radius + playerRadius + playerPersonalSpace;

            // Never keep velocity that would reduce the protected player distance.
            float approachingSpeed = Vector3.Dot(planarMove, towardPlayer);
            if (approachingSpeed > 0f)
                planarMove -= towardPlayer * approachingSpeed;

            if (distance < minimumDistance)
            {
                float penetration = minimumDistance - distance;
                float separateSpeed = Mathf.Min(playerSeparationSpeed, .45f + penetration * 8f);
                planarMove += awayFromPlayer * separateSpeed;
            }

            float allowedSpeed = Mathf.Max(
                Mathf.Max(fleeSpeed, punchFleeSpeed),
                Mathf.Max(walkSpeed, playerSeparationSpeed));
            return Vector3.ClampMagnitude(planarMove, allowedSpeed);
        }

        private void RefreshPlayerAvoidanceTarget()
        {
            if (_playerToAvoid != null || Time.time < _nextPlayerLookup) return;
            _nextPlayerLookup = Time.time + 1f;
            _playerToAvoid = Object.FindAnyObjectByType<NetworkPlayerController>();
            _playerControllerToAvoid = _playerToAvoid != null
                ? _playerToAvoid.GetComponent<CharacterController>()
                : null;
        }

        private bool ProbeGround(out Vector3 normal)
        {
            normal = Vector3.up;
            if (_controller == null || !_controller.enabled) return false;

            float radius = Mathf.Max(.06f, _controller.radius * .9f);
            float halfHeight = Mathf.Max(_controller.height * .5f, radius);
            Vector3 center = transform.TransformPoint(_controller.center);
            Vector3 origin = center - Vector3.up * (halfHeight - radius) + Vector3.up * .05f;
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHits, .28f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            float minimumNormalY = Mathf.Cos((_controller.slopeLimit + 1f) * Mathf.Deg2Rad);
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (hit.normal.y < minimumNormalY || hit.distance >= nearest) continue;
                nearest = hit.distance;
                normal = hit.normal;
                found = true;
            }

            return found;
        }

        private void ApplyGravityOnly() => MoveWithGravity(Vector3.zero);

        private void Face(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < .001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private void LateUpdate()
        {
            UpdateWitnessMarker();
            UpdateDownBodyHitbox();
            if (IsCarried)
            {
                if (_corpseRagdoll != null && _corpseRagdoll.IsActive) return;
                Vector3 carriedSample = _mainSkinnedMesh != null ? _mainSkinnedMesh.bounds.center : transform.position;
                ResolveBodyGroundContact(carriedSample);
                return;
            }
            if (_corpseRagdoll != null && _corpseRagdoll.IsActive) return;
            if (_mainSkinnedMesh == null) return;

            if (VehicleDown && !_gettingUp)
            {
                Bounds body = _mainSkinnedMesh.bounds;
                if (!_fallMotionTracking)
                {
                    _fallBodyStartCenter = body.center;
                    _fallMotionTracking = true;
                }
                else
                {
                    Vector3 travel = body.center - _fallBodyStartCenter;
                    travel.y = 0f;
                    if (travel.magnitude > maxAnimationFallTravel) travel = travel.normalized * maxAnimationFallTravel;
                    _impactAnchor.x = _fallOriginAnchor.x + travel.x;
                    _impactAnchor.z = _fallOriginAnchor.z + travel.z;
                }
            }

            if (_meleePhase == MeleePhase.Knockdown && _meleeFallTracking)
                TrackMeleeLandingAnchor();

            // Dying starts from the exact frozen Knockdown pose. The clip may animate its own
            // root, but it is not allowed to turn that authored motion into another world-space
            // teleport away from the place where the NPC was lying.
            if (_meleePhase == MeleePhase.Finisher)
                LockMeleeGetUpToLandingPose();

            if (_gettingUp)
                RestoreVisualBaseTransform(false);
            else if (_meleePhase == MeleePhase.GetUp)
                LockMeleeGetUpToLandingPose();

            if (_visualRoot != null && _visualRoot.localRotation != _visualBaseRotation && !VehicleDown && _meleePhase == MeleePhase.None)
                _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _visualBaseRotation, 1f - Mathf.Exp(-16f * Time.deltaTime));

            // Resolve the evaluated pose every rendered frame: body bones support Fall/GetUp,
            // then toe/foot bones take over for locomotion without a mesh-bounds height jump.
            ResolveBodyGroundContact(VehicleDown ? _impactAnchor : transform.position);
        }

        private void TrackMeleeLandingAnchor()
        {
            Vector3 travel = GetMeleeMotionReferencePosition() - _meleeFallBodyStartCenter;
            travel.y = 0f;
            if (travel.magnitude > maxAnimationFallTravel)
                travel = travel.normalized * maxAnimationFallTravel;

            _impactAnchor = _meleeFallOriginAnchor + travel;
            Vector3 ground = FindGroundPoint(_impactAnchor);
            _impactAnchor.y = ground.y;
        }

        private void CaptureMeleeLandingPose()
        {
            if (!_meleeFallTracking) return;
            TrackMeleeLandingAnchor();
            _meleeLandingPoseAnchor = GetMeleeMotionReferencePosition();
            _meleeFallTracking = false;
        }

        private void LockMeleeGetUpToLandingPose()
        {
            if (_meleeMotionReference == null) return;

            Vector3 correction = _meleeLandingPoseAnchor - _meleeMotionReference.position;
            correction.y = 0f;
            if (correction.sqrMagnitude > maxAnimationFallTravel * maxAnimationFallTravel)
                correction = correction.normalized * maxAnimationFallTravel;
            if (correction.sqrMagnitude < .0000001f) return;

            transform.position += correction;
            _home += correction;
            _target += correction;
            Vector3 ground = FindGroundPoint(transform.position);
            _impactAnchor = new Vector3(transform.position.x, ground.y, transform.position.z);
        }

        private void FinalizeMeleeGetUpPosition()
        {
            RestoreVisualBaseTransform(true);
            _impactAnchor = FindGroundPoint(transform.position);
        }

        private void MoveGameplayRootToLanding(Vector3 landing)
        {
            Vector3 delta = new(landing.x - transform.position.x, 0f, landing.z - transform.position.z);
            if (delta.sqrMagnitude < .000001f) return;

            transform.position += delta;
            _home += delta;
            _target += delta;
            if (_visualRoot != null && _visualRoot != transform)
                _visualRoot.position -= delta;
        }

        private void RestoreVisualBaseTransform(bool immediate)
        {
            if (_visualRoot == null) return;
            if (immediate)
            {
                _visualRoot.localPosition = _visualBaseLocalPosition;
                _visualRoot.localRotation = _visualBaseRotation;
                _visualRestoreActive = false;
                return;
            }

            if (!_visualRestoreActive) BeginVisualRootRestore();
            float progress = GetActiveGetUpProgress();
            float blend = progress * progress * (3f - 2f * progress);
            _visualRoot.localPosition = Vector3.LerpUnclamped(_visualRestoreStartLocalPosition, _visualBaseLocalPosition, blend);
            _visualRoot.localRotation = Quaternion.SlerpUnclamped(_visualRestoreStartLocalRotation, _visualBaseRotation, blend);
        }

        private void BeginVisualRootRestore()
        {
            if (_visualRoot == null) return;
            _visualRestoreStartLocalPosition = _visualRoot.localPosition;
            _visualRestoreStartLocalRotation = _visualRoot.localRotation;
            _visualRestoreActive = true;
        }

        private float GetActiveGetUpProgress()
        {
            if (animator == null) return 1f;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool inGetUp = state.fullPathHash == GetUpHash ||
                           state.fullPathHash == GettingUpHash ||
                           state.fullPathHash == GettingUpRearHash;
            return inGetUp ? Mathf.Clamp01(state.normalizedTime / ClipFinishedNormalizedTime) : 0f;
        }

        public void HitByPlayerPunch(Vector3 hitDirection, int punchVariant, Transform attacker)
        {
            if (IsFinishable)
            {
                BeginGroundFinisher(hitDirection, attacker);
                return;
            }
            if (VehicleDown || MeleeDown) return;

            CancelWitnessCall(false);
            if (attacker != null && Time.time >= _nextWitnessIncidentAllowed)
            {
                _nextWitnessIncidentAllowed = Time.time + 12f;
                NPCWitnessCoordinator.ReportAssault(this, attacker);
            }

            if (Time.time - _lastPlayerHitTime > 10f)
            {
                _playerHitCount = 0;
                _willFightBack = Random.value < counterAttackChance;
            }
            _lastPlayerHitTime = Time.time;
            _playerHitCount++;
            _meleeAttacker = attacker;
            _forcedFleeUntil = 0f;
            _dangerCar = null;
            _walking = false;
            _running = false;
            _currentPlanarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _pause = 0f;
            SetAnimationImportance(true);

            if (hitDirection.sqrMagnitude > .001f)
            {
                hitDirection.y = 0f;
                hitDirection.Normalize();
                Vector3 face = -hitDirection.normalized;
                if (face.sqrMagnitude > .001f) _meleeFacingDirection = face;
                _currentPlanarVelocity = hitDirection * hitRecoilSpeed;
            }

            if (_playerHitCount >= knockdownOnHit)
            {
                _meleeFallOriginAnchor = FindGroundPoint(transform.position);
                _meleeFallBodyStartCenter = GetMeleeMotionReferencePosition();
                _meleeFallTracking = true;
                _meleePhase = MeleePhase.Knockdown;
                SetDownBodyHitbox(true);
                if (_controller != null) _controller.enabled = false;
                StartMeleeState(KnockdownHash);
                return;
            }

            if (_playerHitCount == 2)
            {
                _meleePhase = MeleePhase.HeavyReaction;
                StartMeleeState(HeavyHitHash);
                return;
            }

            _normalHitToggle++;
            int normalHit = (_normalHitToggle % 2 == 0) ? Hit2Hash : Hit1Hash;
            _meleePhase = MeleePhase.Reaction;
            StartMeleeState(normalHit);
        }

        private void BeginGroundFinisher(Vector3 hitDirection, Transform attacker)
        {
            if (!IsFinishable) return;

            if (_meleePhase == MeleePhase.Knockdown)
                CaptureMeleeLandingPose();

            CancelWitnessCall(false);
            _counterCombatActive = false;
            _meleeAttacker = attacker;
            _forcedFleeUntil = 0f;
            _currentPlanarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _meleePhase = MeleePhase.Finisher;
            _meleeLandingPoseAnchor = GetMeleeMotionReferencePosition();
            _meleeFallTracking = false;
            StartMeleeState(DyingHash);
            PlayerPoliceStatus.RecordHomicide(attacker, this);

            if (attacker != null && Time.time >= _nextWitnessIncidentAllowed)
            {
                _nextWitnessIncidentAllowed = Time.time + 12f;
                NPCWitnessCoordinator.ReportAssault(this, attacker);
            }
        }

        private void FinishDeathPose()
        {
            _meleeFallTracking = false;
            _counterCombatActive = false;
            _witnessPhase = WitnessPhase.None;
            ClearWitnessMarker();
            _walking = false;
            _running = false;
            _playerHitCount = 0;
            SetDownBodyHitbox(false);
            if (_controller != null) _controller.enabled = false;
            if (animator != null) animator.speed = 0f;
            if (_corpseRagdoll == null) _corpseRagdoll = gameObject.AddComponent<NPCCorpseRagdoll>();
            _corpseRagdoll.Activate(animator, _mainSkinnedMesh);
            SetAnimationImportance(false);
        }

        public bool BeginCarry(Transform carrier)
        {
            if (!IsDead || IsCarried || carrier == null) return false;
            if (_meleePhase == MeleePhase.Finisher)
            {
                FinishDeathPose();
                _meleePhase = MeleePhase.Dead;
            }
            if (_corpseRagdoll != null && _corpseRagdoll.IsActive)
            {
                if (!_corpseRagdoll.BeginDrag(carrier)) return false;
                _carrier = carrier;
                return true;
            }
            _carrier = carrier;
            _parentBeforeCarry = transform.parent;
            if (_controller != null) _controller.enabled = false;
            transform.SetParent(carrier, true);
            transform.rotation = carrier.rotation * Quaternion.Euler(carriedLocalEuler);
            Vector3 desiredBodyCenter = carrier.TransformPoint(carriedLocalPosition);
            Vector3 actualBodyCenter = _mainSkinnedMesh != null ? _mainSkinnedMesh.bounds.center : transform.position;
            transform.position += desiredBodyCenter - actualBodyCenter;
            return true;
        }

        public void DropCarriedBody()
        {
            if (!IsCarried) return;
            if (_corpseRagdoll != null && _corpseRagdoll.IsActive)
            {
                _corpseRagdoll.EndDrag();
                _carrier = null;
                _parentBeforeCarry = null;
                return;
            }
            Transform carrier = _carrier;
            Transform originalParent = _parentBeforeCarry;
            _carrier = null;
            _parentBeforeCarry = null;

            Vector3 desired = carrier.position + carrier.forward * bodyDropForwardDistance + carrier.right * .45f;
            transform.SetParent(originalParent, true);
            desired = FindGroundPoint(desired);
            transform.position = desired;
            transform.rotation = Quaternion.Euler(0f, carrier.eulerAngles.y + 90f, 0f);
            _home = desired;
            _target = desired;
            _impactAnchor = desired;
            if (_controller != null) _controller.enabled = !IsDead;
            Physics.SyncTransforms();
        }

        private void CreateDownBodyHitbox()
        {
            GameObject hitbox = new("DownBodyHitbox");
            hitbox.transform.SetParent(transform, false);
            _downHitboxRoot = hitbox.transform;
            _downHitbox = hitbox.AddComponent<BoxCollider>();
            _downHitbox.isTrigger = true;
            _downHitbox.enabled = false;
        }

        private void SetDownBodyHitbox(bool enabled)
        {
            if (_downHitbox != null) _downHitbox.enabled = enabled;
        }

        private void UpdateDownBodyHitbox()
        {
            if (_downHitbox == null || !_downHitbox.enabled || _mainSkinnedMesh == null) return;
            Bounds body = _mainSkinnedMesh.bounds;
            _downHitboxRoot.position = body.center;
            _downHitboxRoot.rotation = Quaternion.identity;
            Vector3 scale = _downHitboxRoot.lossyScale;
            _downHitbox.size = new Vector3(
                body.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                body.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                body.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z))) + new Vector3(.08f, .08f, .08f);
            _downHitbox.center = Vector3.zero;
        }

        private void ResolveBodyGroundContact(Vector3 samplePosition)
        {
            Vector3 ground = FindGroundPoint(samplePosition);
            bool useFullBodyContact = VehicleDown || MeleeDown;
            bool foundContact = useFullBodyContact
                ? TryGetFullBodyContactY(out float contactY)
                : TryGetStandingSoleY(out contactY);

            if (!foundContact && _mainSkinnedMesh != null)
            {
                contactY = _mainSkinnedMesh.bounds.min.y;
                foundContact = true;
            }
            if (!foundContact) return;

            float correction = (ground.y - surfaceSink) - contactY;
            correction = Mathf.Clamp(correction, -maxGroundPoseCorrection, maxGroundPoseCorrection);
            bool smoothDownPose = !_gettingUp && (_meleePhase == MeleePhase.Knockdown ||
                                                   _meleePhase == MeleePhase.Lying ||
                                                   _meleePhase == MeleePhase.Finisher);
            if (smoothDownPose)
            {
                float maximumStep = downPoseGroundingSpeed * Time.deltaTime;
                correction = Mathf.Clamp(correction, -maximumStep, maximumStep);
            }
            Vector3 p = transform.position;
            p.y += correction;
            if (_gettingUp) { p.x = _impactAnchor.x; p.z = _impactAnchor.z; }
            transform.position = p;
            if (VehicleDown) _impactAnchor.y = ground.y;
        }

        private void PrepareGroundingBones()
        {
            _groundContactBones.Clear();
            _leftFoot = null;
            _rightFoot = null;
            _leftToe = null;
            _rightToe = null;
            if (animator == null) return;

            float highest = float.NegativeInfinity;
            float lowest = float.PositiveInfinity;
            foreach (Transform candidate in animator.GetComponentsInChildren<Transform>(true))
            {
                string name = NormalizeBoneName(candidate.name);
                bool left = name.Contains("left") || name.EndsWith("lfoot") || name.EndsWith("ltoe");
                bool right = name.Contains("right") || name.EndsWith("rfoot") || name.EndsWith("rtoe");

                if (EndsWithAny(name, "lefttoebase", "righttoebase", "lefttoe", "righttoe", "ltoe", "rtoe"))
                {
                    if (left) _leftToe = candidate;
                    if (right) _rightToe = candidate;
                    AddGroundContact(candidate, toeToSoleDistance, false, ref highest, ref lowest);
                }
                else if (EndsWithAny(name, "leftfoot", "rightfoot", "lfoot", "rfoot", "footl", "footr"))
                {
                    if (left || name.EndsWith("footl")) _leftFoot = candidate;
                    if (right || name.EndsWith("footr")) _rightFoot = candidate;
                    AddGroundContact(candidate, footToSoleDistance, false, ref highest, ref lowest);
                }
                else
                {
                    float radius = GetBodyContactRadius(name);
                    if (radius > 0f) AddGroundContact(candidate, radius, true, ref highest, ref lowest);
                }
            }

            if (_groundContactBones.Count > 0 && highest > lowest)
                _groundContactRadiusScale = Mathf.Clamp((highest - lowest) / 1.55f, .65f, 1.6f);
        }

        private void AddGroundContact(Transform bone, float radius, bool scaleRadius, ref float highest, ref float lowest)
        {
            _groundContactBones.Add(new GroundContactBone(bone, radius, scaleRadius));
            highest = Mathf.Max(highest, bone.position.y);
            lowest = Mathf.Min(lowest, bone.position.y);
        }

        private static float GetBodyContactRadius(string name)
        {
            if (EndsWithAny(name, "head")) return .13f;
            if (EndsWithAny(name, "hips", "spine", "spine1", "spine2", "chest", "upperchest")) return .14f;
            if (EndsWithAny(name, "neck")) return .07f;
            if (EndsWithAny(name, "leftshoulder", "rightshoulder", "lshoulder", "rshoulder")) return .09f;
            if (EndsWithAny(name, "leftarm", "rightarm", "larm", "rarm")) return .075f;
            if (EndsWithAny(name, "leftforearm", "rightforearm", "lforearm", "rforearm")) return .06f;
            if (EndsWithAny(name, "lefthand", "righthand", "lhand", "rhand")) return .05f;
            if (EndsWithAny(name, "leftupleg", "rightupleg", "lupleg", "rupleg", "leftthigh", "rightthigh")) return .10f;
            if (EndsWithAny(name, "leftleg", "rightleg", "lleg", "rleg", "leftcalf", "rightcalf")) return .07f;
            return 0f;
        }

        private bool TryGetFullBodyContactY(out float contactY)
        {
            contactY = float.PositiveInfinity;
            bool found = false;
            foreach (GroundContactBone contact in _groundContactBones)
            {
                if (contact.transform == null) continue;
                float radius = contact.scaleRadius ? contact.radius * _groundContactRadiusScale : contact.radius;
                contactY = Mathf.Min(contactY, contact.transform.position.y - radius);
                found = true;
            }
            return found;
        }

        private bool TryGetStandingSoleY(out float soleY)
        {
            soleY = float.PositiveInfinity;
            bool found = false;
            AddLowestGroundPoint(_leftToe, toeToSoleDistance, ref soleY, ref found);
            AddLowestGroundPoint(_rightToe, toeToSoleDistance, ref soleY, ref found);
            if (!found)
            {
                AddLowestGroundPoint(_leftFoot, footToSoleDistance, ref soleY, ref found);
                AddLowestGroundPoint(_rightFoot, footToSoleDistance, ref soleY, ref found);
            }
            return found;
        }

        private static void AddLowestGroundPoint(Transform bone, float soleDistance, ref float soleY, ref bool found)
        {
            if (bone == null) return;
            soleY = Mathf.Min(soleY, bone.position.y - soleDistance);
            found = true;
        }

        private static string NormalizeBoneName(string value)
        {
            return value.Replace(":", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        private static bool EndsWithAny(string value, params string[] suffixes)
        {
            foreach (string suffix in suffixes)
                if (value.EndsWith(suffix)) return true;
            return false;
        }

        private bool IsActiveClipFinished(int preferredHash, int fallbackHash)
        {
            if (animator == null) return true;
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
            bool isExpected = s.fullPathHash == preferredHash || s.fullPathHash == fallbackHash;
            if (!isExpected) return false;
            return s.normalizedTime >= ClipFinishedNormalizedTime;
        }

        private bool IsCarTooClose()
        {
            foreach (DriveableCar car in DriveableCar.ActiveCars)
            {
                if (car == null) continue;
                Vector3 d = car.transform.position - _impactAnchor;
                d.y = 0f;
                if (d.sqrMagnitude <= carClearanceBeforeGetUp * carClearanceBeforeGetUp) return true;
            }
            return false;
        }

        private void DisablePhysicalCollision()
        {
            if (_allColliders == null) CacheColliders();
            for (int i = 0; i < _allColliders.Length; i++)
            {
                Collider c = _allColliders[i];
                if (c == null) continue;
                _colliderStates[i] = c.enabled;
                c.enabled = false;
            }
            if (_controller != null) _controller.enabled = false;
        }

        private void RestorePhysicalCollision()
        {
            if (_allColliders != null)
                for (int i = 0; i < _allColliders.Length; i++)
                    if (_allColliders[i] != null) _allColliders[i].enabled = _colliderStates[i];
            if (_controller != null) _controller.enabled = true;
        }

        private Vector3 FindGroundPoint(Vector3 desired)
        {
            Vector3 origin = new Vector3(desired.x, desired.y + groundRayHeight, desired.z);
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, groundRayDistance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float y = float.NegativeInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform) || hit.normal.y < .55f) continue;
                if (hit.collider.GetComponentInParent<DriveableCar>() != null || hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (!found || hit.point.y > y) { y = hit.point.y; found = true; }
            }
            if (found) desired.y = y;
            return desired;
        }

        private void StartGettingUp()
        {
            MoveGameplayRootToLanding(_impactAnchor);
            _gettingUp = true;
            _fallMotionTracking = false;
            BeginVisualRootRestore();
            int state = _rearImpact ? GettingUpRearHash : GettingUpHash;
            bool ok = PlayState(state, getUpBlend);
            if (!ok && _rearImpact) PlayState(GettingUpHash, getUpBlend);
        }

        private void FinishGettingUp()
        {
            ResolveBodyGroundContact(_impactAnchor);
            _gettingUp = false;
            _fallEarliestGetUp = 0f;
            _fallMotionTracking = false;
            RestoreVisualBaseTransform(true);
            RestorePhysicalCollision();
            PlayState(IdleHash, returnToIdleBlend);
            Pause(Random.Range(.15f, .45f));
        }

        private bool PlayState(int hash, float fade)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return false;
            animator.applyRootMotion = false;
            animator.speed = 1f;
            if (!animator.HasState(0, hash)) return false;
            animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
            return true;
        }

        private void UpdateMeleeFacing()
        {
            if (_meleeFacingDirection.sqrMagnitude < .001f) return;
            Quaternion target = Quaternion.LookRotation(_meleeFacingDirection);
            float blend = 1f - Mathf.Exp(-meleeTurnSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, blend);
        }

        private Transform FindMeleeMotionReference()
        {
            if (animator == null) return null;
            if (animator.isHuman)
            {
                try
                {
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips != null) return hips;
                }
                catch { }
            }

            foreach (Transform candidate in animator.GetComponentsInChildren<Transform>(true))
            {
                string name = candidate.name.Replace(":", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();
                if (name == "hips" || name.EndsWith("hips")) return candidate;
            }
            return null;
        }

        private Vector3 GetMeleeMotionReferencePosition()
        {
            if (_meleeMotionReference != null) return _meleeMotionReference.position;
            if (_mainSkinnedMesh != null) return _mainSkinnedMesh.bounds.center;
            return transform.position;
        }

        private void FindDangerousCar()
        {
            _dangerCar = null;
            float best = carAwarenessRadius;
            foreach (DriveableCar car in DriveableCar.ActiveCars)
            {
                if (car == null || !car.IsOccupied || !car.IsThreateningPoint(transform.position, fleeOnlyAboveKmh)) continue;
                float d = Vector3.Distance(transform.position, car.transform.position);
                if (d < best) { best = d; _dangerCar = car; }
            }
        }

        public bool HitByVehicle(Vector3 carVelocity, Vector3 carPosition)
        {
            float speed = carVelocity.magnitude;
            if (speed < impactSpeedThreshold || VehicleDown) return false;

            CancelWitnessCall(false);

            _meleeAttacker = null;
            _meleePhase = MeleePhase.None;
            _forcedFleeUntil = 0f;

            Vector3 toCar = carPosition - transform.position;
            toCar.y = 0f;
            if (toCar.sqrMagnitude > .001f) toCar.Normalize();
            _rearImpact = Vector3.Dot(transform.forward, toCar) < rearHitDotThreshold;

            Vector3 carry = carVelocity;
            carry.y = 0f;
            if (carry.sqrMagnitude > .001f) carry = carry.normalized * impactCarryDistance;

            Vector3 hitPoint = transform.position + carry;
            _impactAnchor = FindGroundPoint(hitPoint);
            _impactAnchor.x = hitPoint.x;
            _impactAnchor.z = hitPoint.z;
            _fallOriginAnchor = _impactAnchor;
            _fallMotionTracking = false;
            _fallEarliestGetUp = Time.time + minimumLieSeconds;
            _safeGetUpAfter = _fallEarliestGetUp;
            _gettingUp = false;
            _dangerCar = null;
            _walking = false;
            _running = false;
            _currentPlanarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            SetAnimationImportance(true);

            DisablePhysicalCollision();
            int state = _rearImpact ? FallRearHash : FallHash;
            bool ok = PlayState(state, 0f);
            if (!ok && _rearImpact) PlayState(FallHash, 0f);
            return true;
        }

        private void PickTarget()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 c = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = _home + new Vector3(c.x, 0f, c.y);
                candidate.y = transform.position.y;
                Vector3 ground = FindGroundPoint(candidate);
                if (Mathf.Abs(ground.y - transform.position.y) > 1.8f) continue;
                _target = ground;
                SetWalking(true);
                return;
            }

            Pause(Random.Range(.5f, 1.5f));
        }

        private void Pause(float duration)
        {
            _pause = duration;
            SetRunning(false);
            SetWalking(false);
        }

        private void SetRunning(bool running)
        {
            if (_running == running) return;
            _running = running;
            if (running)
            {
                _walking = false;
                PlayState(RunHash, .14f);
            }
            else if (_dangerCar == null && _walking)
            {
                PlayState(WalkHash, .16f);
            }
        }

        private void SetWalking(bool walking)
        {
            if (_running) return;
            if (_walking == walking) return;
            _walking = walking;
            PlayState(walking ? WalkHash : IdleHash, .16f);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
            SetAnimationImportance(false);
        }

        private void SetAnimationImportance(bool important)
        {
            if (_animationImportanceInitialized && _highFidelityAnimation == important) return;
            _animationImportanceInitialized = true;
            _highFidelityAnimation = important;

            if (animator != null)
                animator.cullingMode = important
                    ? AnimatorCullingMode.AlwaysAnimate
                    : AnimatorCullingMode.CullUpdateTransforms;
            if (_mainSkinnedMesh != null)
                _mainSkinnedMesh.updateWhenOffscreen = important;
        }
    }
}
