using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using System.Collections.Generic;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour
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

        [Header("Combat blending")]
        [SerializeField, Min(0f)] private float hitBlend = .13f;
        [SerializeField, Min(0f)] private float heavyHitBlend = .16f;
        [SerializeField, Min(0f)] private float knockdownBlend = .17f;
        [SerializeField, Min(0f)] private float getUpBlend = .20f;
        [SerializeField, Min(0f)] private float returnToIdleBlend = .18f;
        [SerializeField, Min(.1f)] private float meleeTurnSharpness = 10f;

        private const float ForcedBodyGroundClearance = -.100f;
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

        private enum MeleePhase { None, Reaction, HeavyReaction, HeavyStun, Knockdown, Lying, GetUp }

        private CharacterController _controller;
        private readonly RaycastHit[] _groundHits = new RaycastHit[12];
        private readonly RaycastHit[] _obstacleHits = new RaycastHit[8];
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
        private MeleePhase _meleePhase;
        private float _meleePhaseStarted;
        private float _meleePhaseUntil;
        private float _forcedFleeUntil;
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

        private bool VehicleDown => _fallEarliestGetUp > 0f || _gettingUp;
        private bool MeleeDown => _meleePhase == MeleePhase.Knockdown || _meleePhase == MeleePhase.Lying || _meleePhase == MeleePhase.GetUp;
        public bool IsDown => VehicleDown || MeleeDown;
        public Vector3 DownPosition => _impactAnchor;

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
            CacheColliders();
            SetAnimationImportance(false);
        }

        private void OnEnable()
        {
            ActiveNpcSet.Add(this);
            _nextAwarenessCheck = Time.time + Random.Range(0f, awarenessInterval);
            _nextObstacleScan = Time.time + Random.Range(0f, obstacleScanInterval);
        }

        private void OnDisable() => ActiveNpcSet.Remove(this);

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
            SetAnimationImportance(VehicleDown || _meleePhase != MeleePhase.None);

            if (VehicleDown)
            {
                UpdateVehicleDown();
                return;
            }

            if (UpdateMeleePhase()) return;

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
            else if (_meleeAttacker != null && Time.time < _forcedFleeUntil)
            {
                Vector3 away = transform.position - _meleeAttacker.position;
                away.y = 0f;
                if (away.sqrMagnitude < .01f) away = -transform.forward;
                away = GetSteeredDirection(away.normalized);
                Face(away, turnSpeed * 1.45f);
                move = away * punchFleeSpeed;
                SetRunning(true);
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
                _meleePhase == MeleePhase.HeavyStun)
                UpdateMeleeFacing();

            switch (_meleePhase)
            {
                case MeleePhase.None:
                    return false;

                case MeleePhase.Reaction:
                    if (MeleeAnimationFinished(maxHitAnimationSeconds))
                    {
                        _meleePhase = MeleePhase.None;
                        BeginFlee();
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
                        BeginFlee();
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
                        BeginFlee();
                        return false;
                    }
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

        private void BeginFlee()
        {
            _forcedFleeUntil = Time.time + fleeAfterPunchSeconds + Random.Range(0f, .8f);
            _running = false;
            SetRunning(true);
        }

        private void StartMeleeGetUp()
        {
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

            if (_gettingUp)
                RestoreVisualBaseTransform(false);
            else if (_meleePhase == MeleePhase.GetUp)
                PinMeleeGetUpToLanding();

            if (_visualRoot != null && _visualRoot.localRotation != _visualBaseRotation && !VehicleDown && _meleePhase == MeleePhase.None)
                _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _visualBaseRotation, 1f - Mathf.Exp(-16f * Time.deltaTime));

            // Bounds and the animated pose change every rendered frame during Fall/GetUp. Updating
            // the contact only every 60 ms made the body visibly jump between corrections and also
            // left standing NPCs at the CharacterController clearance height.
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

        private void PinMeleeGetUpToLanding()
        {
            if (_visualRoot == null || _meleeMotionReference == null) return;
            Vector3 correction = _meleeLandingPoseAnchor - _meleeMotionReference.position;
            correction.y = 0f;
            if (correction.sqrMagnitude > maxAnimationFallTravel * maxAnimationFallTravel)
                correction = correction.normalized * maxAnimationFallTravel;
            _visualRoot.position += correction;
        }

        private void FinalizeMeleeGetUpPosition()
        {
            PinMeleeGetUpToLanding();
            BakeVisualOffsetIntoGameplayRoot();
            RestoreVisualBaseTransform(true);
            _impactAnchor = FindGroundPoint(transform.position);
        }

        private void BakeVisualOffsetIntoGameplayRoot()
        {
            if (_visualRoot == null || _visualRoot.parent == null) return;

            Vector3 baseWorldPosition = _visualRoot.parent.TransformPoint(_visualBaseLocalPosition);
            Vector3 delta = _visualRoot.position - baseWorldPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < .000001f) return;

            transform.position += delta;
            _home += delta;
            _target += delta;
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
            if (VehicleDown || MeleeDown) return;

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
                Vector3 face = -hitDirection.normalized;
                if (face.sqrMagnitude > .001f) _meleeFacingDirection = face;
            }

            if (_playerHitCount >= knockdownOnHit)
            {
                _meleeFallOriginAnchor = FindGroundPoint(transform.position);
                _meleeFallBodyStartCenter = GetMeleeMotionReferencePosition();
                _meleeFallTracking = true;
                _meleePhase = MeleePhase.Knockdown;
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

        private void ResolveBodyGroundContact(Vector3 samplePosition)
        {
            Vector3 ground = FindGroundPoint(samplePosition);
            Bounds body = _mainSkinnedMesh.bounds;
            float bottomOffset = body.min.y - transform.position.y;
            Vector3 p = transform.position;
            p.y = (ground.y + ForcedBodyGroundClearance) - bottomOffset;
            if (_gettingUp) { p.x = _impactAnchor.x; p.z = _impactAnchor.z; }
            transform.position = p;
            if (VehicleDown) _impactAnchor.y = ground.y;
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
