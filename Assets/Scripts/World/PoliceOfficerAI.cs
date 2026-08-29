using CheatOnYourDayOnes.Player;
using System.Collections.Generic;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PoliceOfficerAI : MonoBehaviour, IPlayerStrikeTarget
    {
        private static readonly HashSet<PoliceOfficerAI> ActiveOfficerSet = new();
        public static IEnumerable<PoliceOfficerAI> ActiveOfficers => ActiveOfficerSet;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int Hit1Hash = Animator.StringToHash("Base Layer.Hit1");
        private static readonly int Hit2Hash = Animator.StringToHash("Base Layer.Hit2");
        private static readonly int TaserHash = Animator.StringToHash("Base Layer.Taser");

        [SerializeField] private float patrolSpeed = 1.45f;
        [SerializeField] private float responseSpeed = 4.15f;
        [SerializeField] private float turnSharpness = 9f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float taserRange = 10f;
        [SerializeField] private float taserCooldown = 4.5f;
        [SerializeField] private float taserChargeDuration = 1.2f;
        [SerializeField] private float pursuitEscapeDistance = 30f;
        [SerializeField] private float controlStandDistance = 1.65f;
        [SerializeField] private float patrolRadius = 18f;
        [SerializeField] private float visibleSoleOffset = -.15f;

        private CharacterController _controller;
        private Animator _animator;
        private Transform _visualGroundAnchor;
        private SkinnedMeshRenderer _mainSkin;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftToe;
        private Transform _rightToe;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private float _leftFootToSole;
        private float _rightFootToSole;
        private float _leftToeToSole;
        private float _rightToeToSole;
        private readonly RaycastHit[] _groundHits = new RaycastHit[12];
        private PoliceOfficerAI _partner;
        private Transform _suspect;
        private Vector3 _home;
        private Vector3 _patrolTarget;
        private Vector3 _incidentPosition;
        private float _verticalVelocity;
        private float _pauseUntil;
        private float _responseUntil;
        private float _nextAttack;
        private float _attackStarted;
        private float _stunnedUntil;
        private int _groupIndex;
        private int _activeState;
        private bool _leader;
        private bool _responding;
        private bool _attacking;
        private bool _attackApplied;
        private bool _hasApproachedSuspect;
        private int _hitToggle;
        private bool _usingTaserClip;

        public bool IsResponding => _responding;
        public int GroupIndex => _groupIndex;
        public bool CanReceivePlayerStrike => Time.time >= _stunnedUntil - .35f;
        public Vector3 StrikeTargetPosition => _mainSkin != null ? _mainSkin.bounds.center : transform.position + Vector3.up * .9f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>(true);
            _visualGroundAnchor = transform.Find("PoliceGroundAnchor");
            float largestSkin = -1f;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                float volume = skin.bounds.size.x * skin.bounds.size.y * skin.bounds.size.z;
                if (volume <= largestSkin) continue;
                largestSkin = volume;
                _mainSkin = skin;
            }
            FindGroundingBones();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        private void OnEnable() => ActiveOfficerSet.Add(this);
        private void OnDisable()
        {
            ActiveOfficerSet.Remove(this);
            CancelTaserWarning();
        }

        public void Configure(int groupIndex, bool leader, Vector3 home)
        {
            _groupIndex = groupIndex;
            _leader = leader;
            _home = home;
            PickPatrolTarget();
            Play(IdleHash, 0f);
        }

        public void SetPartner(PoliceOfficerAI partner) => _partner = partner;

        public void RespondToPoliceCall(Vector3 incidentPosition, Transform suspect)
        {
            if (_responding && _suspect == suspect)
            {
                _incidentPosition = incidentPosition;
                _responseUntil = Mathf.Max(_responseUntil, Time.time + 20f);
                return;
            }
            _incidentPosition = incidentPosition;
            _suspect = suspect;
            _responding = true;
            _attacking = false;
            _hasApproachedSuspect = Vector3.SqrMagnitude(transform.position - incidentPosition) <= 20f * 20f;
            _responseUntil = Time.time + 45f;
            _nextAttack = Time.time + Random.Range(.25f, .65f);
        }

        public void HoldPoliceControl(Transform suspect)
        {
            if (suspect == null) return;
            _suspect = suspect;
            _incidentPosition = suspect.position;
            _responding = true;
            _attacking = false;
            _hasApproachedSuspect = true;
            _responseUntil = Time.time + 120f;
            CancelTaserWarning();
        }

        private void Update()
        {
            if (Time.time < _stunnedUntil)
            {
                ApplyMovement(Vector3.zero, 0f);
                return;
            }

            if (_attacking)
            {
                UpdateAttack();
                ApplyMovement(Vector3.zero, 0f);
                return;
            }

            if (_responding)
            {
                UpdateResponse();
                return;
            }

            UpdatePatrol();
        }

        private void LateUpdate()
        {
            if (_attacking && !_usingTaserClip) ApplyProceduralTaserPose();
            if (_visualGroundAnchor == null || !TryGetSoleGroundCorrection(out float correction)) return;
            if (Mathf.Abs(correction) < .00005f) return;
            Vector3 position = _visualGroundAnchor.position;
            position.y += correction;
            _visualGroundAnchor.position = position;
        }

        private void FindGroundingBones()
        {
            if (_animator == null || _mainSkin == null) return;
            foreach (Transform candidate in _animator.GetComponentsInChildren<Transform>(true))
            {
                string normalized = NormalizeBoneName(candidate.name);
                if (normalized.EndsWith("lefttoebase") || normalized.EndsWith("lefttoe")) _leftToe = candidate;
                else if (normalized.EndsWith("righttoebase") || normalized.EndsWith("righttoe")) _rightToe = candidate;
                else if (normalized.EndsWith("leftfoot")) _leftFoot = candidate;
                else if (normalized.EndsWith("rightfoot")) _rightFoot = candidate;
                else if (normalized.EndsWith("leftarm")) _leftUpperArm = candidate;
                else if (normalized.EndsWith("leftforearm")) _leftLowerArm = candidate;
                else if (normalized.EndsWith("rightarm")) _rightUpperArm = candidate;
                else if (normalized.EndsWith("rightforearm")) _rightLowerArm = candidate;
            }

            float soleY = _mainSkin.bounds.min.y;
            if (_leftFoot != null) _leftFootToSole = Mathf.Max(0f, _leftFoot.position.y - soleY);
            if (_rightFoot != null) _rightFootToSole = Mathf.Max(0f, _rightFoot.position.y - soleY);
            if (_leftToe != null) _leftToeToSole = Mathf.Max(0f, _leftToe.position.y - soleY);
            if (_rightToe != null) _rightToeToSole = Mathf.Max(0f, _rightToe.position.y - soleY);
        }

        private bool TryGetSoleGroundCorrection(out float correction)
        {
            correction = float.NegativeInfinity;
            bool found = false;
            AddSoleCorrection(_leftFoot, _leftFootToSole, ref correction, ref found);
            AddSoleCorrection(_rightFoot, _rightFootToSole, ref correction, ref found);
            AddSoleCorrection(_leftToe, _leftToeToSole, ref correction, ref found);
            AddSoleCorrection(_rightToe, _rightToeToSole, ref correction, ref found);

            if (!found && _mainSkin != null && TryFindGroundY(transform.position, out float groundY))
            {
                correction = groundY + visibleSoleOffset - _mainSkin.bounds.min.y;
                found = true;
            }
            return found;
        }

        private void AddSoleCorrection(Transform bone, float boneToSole, ref float correction, ref bool found)
        {
            if (bone == null || !TryFindGroundY(bone.position, out float groundY)) return;
            float soleY = bone.position.y - boneToSole;
            correction = Mathf.Max(correction, groundY + visibleSoleOffset - soleY);
            found = true;
        }

        private bool TryFindGroundY(Vector3 sample, out float groundY)
        {
            Vector3 origin = sample + Vector3.up * .75f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 3f, ~0, QueryTriggerInteraction.Ignore);
            groundY = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < .55f) continue;
                if (hit.collider.GetComponentInParent<PoliceOfficerAI>() != null) continue;
                if (hit.collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (hit.collider.GetComponentInParent<PlayerAgent>() != null) continue;
                if (!found || hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    found = true;
                }
            }
            return found;
        }

        private static string NormalizeBoneName(string value) => value.Replace(":", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        private void UpdateResponse()
        {
            PlayerPoliceStatus status = _suspect != null ? _suspect.GetComponent<PlayerPoliceStatus>() : null;
            if (status != null && status.IsInPoliceControl)
            {
                Vector3 towardPlayer = _suspect.position - transform.position;
                towardPlayer.y = 0f;
                if (status.IsControlOfficer(this))
                {
                    Vector3 standPosition = _suspect.position + _suspect.forward * controlStandDistance;
                    Vector3 toStandPosition = standPosition - transform.position;
                    toStandPosition.y = 0f;
                    if (toStandPosition.sqrMagnitude > .12f)
                    {
                        Vector3 controlDirection = SteerAroundObstacles(toStandPosition.normalized);
                        Face(towardPlayer);
                        ApplyMovement(controlDirection, patrolSpeed);
                        Play(WalkHash, .12f);
                    }
                    else
                    {
                        Face(towardPlayer);
                        ApplyMovement(Vector3.zero, 0f);
                        Play(IdleHash, .12f);
                    }
                }
                else
                {
                    Face(towardPlayer);
                    ApplyMovement(Vector3.zero, 0f);
                    Play(IdleHash, .12f);
                }
                return;
            }
            if (status != null && status.WantedStars <= 0 && !status.IsInPoliceControl)
            {
                ReturnToPatrol();
                return;
            }

            if (Time.time >= _responseUntil)
            {
                ReturnToPatrol();
                return;
            }

            Vector3 destination = _suspect != null && _suspect.gameObject.activeInHierarchy
                ? _suspect.position
                : _incidentPosition;
            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;
            float distance = toDestination.magnitude;
            if (distance <= 20f) _hasApproachedSuspect = true;
            if (_suspect != null && _hasApproachedSuspect && distance > pursuitEscapeDistance)
            {
                ReturnToPatrol();
                return;
            }

            if (_suspect != null && distance <= taserRange)
            {
                Face(toDestination);
                ApplyMovement(Vector3.zero, 0f);
                if (Time.time >= _nextAttack) BeginAttack();
                return;
            }

            if (_suspect == null && distance < 1.2f)
            {
                ReturnToPatrol();
                return;
            }

            Vector3 direction = distance > .01f ? SteerAroundObstacles(toDestination / distance) : transform.forward;
            Face(direction);
            ApplyMovement(direction, responseSpeed);
            Play(RunHash, .12f);
        }

        private void UpdatePatrol()
        {
            Vector3 destination;
            if (!_leader && _partner != null)
            {
                float side = (_groupIndex & 1) == 0 ? 1f : -1f;
                destination = _partner.transform.position - _partner.transform.forward * .85f + _partner.transform.right * (side * .72f);
            }
            else
            {
                destination = _patrolTarget;
                if (Time.time < _pauseUntil)
                {
                    ApplyMovement(Vector3.zero, 0f);
                    Play(IdleHash, .18f);
                    return;
                }
            }

            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;
            if (toDestination.sqrMagnitude < (_leader ? .65f : .36f))
            {
                ApplyMovement(Vector3.zero, 0f);
                Play(IdleHash, .18f);
                if (_leader)
                {
                    _pauseUntil = Time.time + Random.Range(1.2f, 3.2f);
                    PickPatrolTarget();
                }
                return;
            }

            Vector3 direction = SteerAroundObstacles(toDestination.normalized);
            Face(direction);
            ApplyMovement(direction, patrolSpeed);
            Play(WalkHash, .16f);
        }

        private void BeginAttack()
        {
            _attacking = true;
            _attackApplied = false;
            _attackStarted = Time.time;
            _usingTaserClip = _animator != null && _animator.HasState(0, TaserHash);
            Play(_usingTaserClip ? TaserHash : IdleHash, .12f, true);
            PlayerPoliceStatus status = _suspect != null ? _suspect.GetComponent<PlayerPoliceStatus>() : null;
            if (status != null) status.BeginTaserWarning(this, taserChargeDuration);
        }

        private void UpdateAttack()
        {
            if (_suspect != null)
            {
                Vector3 toward = _suspect.position - transform.position;
                toward.y = 0f;
                Face(toward);

                global::MeleeAnimationBridge melee = _suspect.GetComponent<global::MeleeAnimationBridge>();
                bool rolling = melee != null && melee.IsRolling;
                bool inRange = toward.sqrMagnitude <= taserRange * taserRange;
                if (!inRange || rolling || !HasClearTaserLine(_suspect))
                {
                    CancelAttack(true);
                    return;
                }

                if (!_attackApplied && Time.time - _attackStarted >= taserChargeDuration)
                {
                    _attackApplied = true;
                    PlayerPoliceStatus status = _suspect.GetComponent<PlayerPoliceStatus>();
                    if (status != null) status.ApplyTaser(this);
                }
            }

            if (Time.time - _attackStarted < taserChargeDuration + .32f) return;
            _attacking = false;
            CancelTaserWarning();
            _nextAttack = Time.time + taserCooldown + Random.Range(-.45f, .65f);
            Play(IdleHash, .10f);
        }

        private void CancelAttack(bool escaped)
        {
            _attacking = false;
            _attackApplied = false;
            CancelTaserWarning();
            _nextAttack = Time.time + (escaped ? 1.15f : taserCooldown);
            Play(RunHash, .10f);
        }

        private void CancelTaserWarning()
        {
            PlayerPoliceStatus status = _suspect != null ? _suspect.GetComponent<PlayerPoliceStatus>() : null;
            if (status != null) status.CancelTaserWarning(this);
        }

        private bool HasClearTaserLine(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up * 1.35f;
            Vector3 destination = target.position + Vector3.up * 1.05f;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance < .01f) return true;
            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                return true;
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        private void ApplyProceduralTaserPose()
        {
            float elapsed = Time.time - _attackStarted;
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .28f));
            float lower = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .72f) / .30f));
            float weight = raise * lower;
            if (_rightUpperArm != null) _rightUpperArm.localRotation *= Quaternion.Euler(-28f * weight, 8f * weight, -62f * weight);
            if (_rightLowerArm != null) _rightLowerArm.localRotation *= Quaternion.Euler(0f, 0f, -54f * weight);
            if (_leftUpperArm != null) _leftUpperArm.localRotation *= Quaternion.Euler(-18f * weight, -5f * weight, 42f * weight);
            if (_leftLowerArm != null) _leftLowerArm.localRotation *= Quaternion.Euler(0f, 0f, 48f * weight);
        }

        public void HitByPlayerPunch(Vector3 hitDirection, int punchVariant, Transform attacker)
        {
            if (!CanReceivePlayerStrike) return;
            PlayerPoliceStatus.RecordAssault(attacker, this);
            CancelTaserWarning();
            _suspect = attacker;
            _incidentPosition = transform.position;
            _responding = true;
            _attacking = false;
            _responseUntil = Time.time + 60f;
            _stunnedUntil = Time.time + .62f;
            _hitToggle++;
            Play((_hitToggle & 1) == 0 ? Hit2Hash : Hit1Hash, .10f, true);
            NPCWitnessCoordinator.CompletePoliceReport(transform.position, attacker);
        }

        private void ReturnToPatrol()
        {
            _responding = false;
            CancelTaserWarning();
            _suspect = null;
            _hasApproachedSuspect = false;
            _home = transform.position;
            _pauseUntil = Time.time + Random.Range(.4f, 1.1f);
            PickPatrolTarget();
        }

        private void PickPatrolTarget()
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _home + new Vector3(offset.x, 0f, offset.y);
        }

        private Vector3 SteerAroundObstacles(Vector3 desired)
        {
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(.55f, _controller.height * .45f);
            if (!Physics.SphereCast(origin, _controller.radius * .72f, desired, out RaycastHit hit, .85f, ~0, QueryTriggerInteraction.Ignore))
                return desired;
            if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PoliceOfficerAI>() != null)
                return desired;
            Vector3 tangent = Vector3.Cross(Vector3.up, hit.normal).normalized;
            if (Vector3.Dot(tangent, desired) < 0f) tangent = -tangent;
            return Vector3.Slerp(desired, tangent, .72f).normalized;
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;
            Quaternion wanted = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
        }

        private void ApplyMovement(Vector3 planarDirection, float speed)
        {
            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity <= 0f) _verticalVelocity = -3.5f;
            else _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * Time.deltaTime, -32f);
            Vector3 velocity = planarDirection * speed + Vector3.up * _verticalVelocity;
            CollisionFlags flags = _controller.Move(velocity * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0) _verticalVelocity = -3.5f;
        }

        private void Play(int stateHash, float blend, bool restart = false)
        {
            if (_animator == null || !_animator.HasState(0, stateHash)) return;
            if (!restart && _activeState == stateHash) return;
            _activeState = stateHash;
            _animator.CrossFadeInFixedTime(stateHash, blend, 0, 0f);
        }
    }
}
