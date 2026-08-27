using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class NPCCombatReaction : MonoBehaviour
    {
        [Header("Reaction timing")]
        [SerializeField] private float normalStateFinish = .99f;
        [SerializeField] private float maxStateSeconds = 4f;
        [SerializeField] private float secondHitExtraStun = .9f;
        [SerializeField] private float knockdownLieSeconds = 2.6f;

        [Header("Smooth transitions")]
        [SerializeField] private float hitCrossFade = .055f;
        [SerializeField] private float heavyHitCrossFade = .07f;
        [SerializeField] private float knockdownCrossFade = .055f;
        [SerializeField] private float getUpCrossFade = .08f;
        [SerializeField] private float runCrossFade = .10f;

        [Header("Flee")]
        [SerializeField] private float fleeSeconds = 4.5f;
        [SerializeField] private float fleeSpeed = 2.25f;

        [Header("Knockdown pose tracking")]
        [SerializeField] private float maxKnockdownTravel = 3.5f;
        [SerializeField] private float maxLandingYawCorrection = 115f;

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
        private Quaternion _visualBaseLocalRotation;
        private Coroutine _routine;
        private int _hitCount;
        private int _normalHitToggle;
        private bool _hardLocked;
        private bool _down;
        private bool _runAlreadyPrepared;
        private Transform _attacker;

        private Transform _hips, _head, _leftFoot, _rightFoot, _leftHand, _rightHand;

        public bool CanReceiveHit => !_hardLocked && !_down;
        public bool IsDown => _down;

        private struct BodyPose
        {
            public bool valid;
            public Vector3 center;
            public Vector3 lateral;
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _wanderer = GetComponent<NPCWanderer>();
            _controller = GetComponent<CharacterController>();

            if (_animator != null)
            {
                _visualRoot = _animator.transform;
                _visualBaseLocalPosition = _visualRoot.localPosition;
                _visualBaseLocalRotation = _visualRoot.localRotation;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CacheBodyReferences();
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

        private void CacheBodyReferences()
        {
            if (_animator == null) return;

            if (_animator.isHuman)
            {
                _hips = SafeHumanBone(HumanBodyBones.Hips);
                _head = SafeHumanBone(HumanBodyBones.Head);
                _leftFoot = SafeHumanBone(HumanBodyBones.LeftFoot);
                _rightFoot = SafeHumanBone(HumanBodyBones.RightFoot);
                _leftHand = SafeHumanBone(HumanBodyBones.LeftHand);
                _rightHand = SafeHumanBone(HumanBodyBones.RightHand);
            }

            Transform[] all = _animator.GetComponentsInChildren<Transform>(true);
            _hips ??= FindBone(all, "hips", "pelvis", "hip");
            _head ??= FindBone(all, "head");
            _leftFoot ??= FindSideBone(all, true, "foot", "ankle");
            _rightFoot ??= FindSideBone(all, false, "foot", "ankle");
            _leftHand ??= FindSideBone(all, true, "hand", "wrist");
            _rightHand ??= FindSideBone(all, false, "hand", "wrist");
        }

        private Transform SafeHumanBone(HumanBodyBones bone)
        {
            try { return _animator.GetBoneTransform(bone); }
            catch { return null; }
        }

        private static Transform FindBone(IEnumerable<Transform> all, params string[] tokens)
        {
            return all.FirstOrDefault(t =>
            {
                string n = Normalize(t.name);
                return tokens.Any(token => n.Contains(Normalize(token)));
            });
        }

        private static Transform FindSideBone(IEnumerable<Transform> all, bool left, params string[] bodyTokens)
        {
            string[] sideTokens = left ? new[] { "left", "l" } : new[] { "right", "r" };
            return all.FirstOrDefault(t =>
            {
                string n = Normalize(t.name);
                bool body = bodyTokens.Any(token => n.Contains(Normalize(token)));
                if (!body) return false;
                return sideTokens.Any(side => n.StartsWith(side) || n.EndsWith(side) || n.Contains("_" + side) || n.Contains(side + "_"));
            });
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        public void TakePunch(Transform attacker)
        {
            if (!CanReceiveHit || _animator == null) return;

            _hitCount++;
            _attacker = attacker;
            _runAlreadyPrepared = false;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            SetWandererEnabled(false);
            FaceAttacker();

            if (_hitCount >= 4)
                _routine = StartCoroutine(FourthHitKnockdownSequence());
            else
                _routine = StartCoroutine(ReactionSequence(_hitCount));
        }

        private IEnumerator ReactionSequence(int hitNumber)
        {
            _hardLocked = false;
            _normalHitToggle++;
            int baseHit = (_normalHitToggle % 2 == 0) ? Hit2Hash : Hit1Hash;
            PlayState(baseHit, hitCrossFade);
            yield return WaitForStateComplete(baseHit, maxStateSeconds);

            if (hitNumber == 2)
            {
                PlayState(HeavyHitHash, heavyHitCrossFade);
                yield return WaitForStateComplete(HeavyHitHash, maxStateSeconds);
                PlayState(IdleHash, .08f);
                yield return InterruptibleWait(secondHitExtraStun);
            }

            if (_routine == null) yield break;
            yield return FleeSequence();
            _routine = null;
        }

        private IEnumerator FourthHitKnockdownSequence()
        {
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
            _runAlreadyPrepared = false;

            BodyPose startPose = CaptureBodyPose();
            Vector3 fallbackStart = _body != null ? _body.bounds.center : transform.position;

            PlayState(KnockdownHash, knockdownCrossFade);

            float started = Time.time;
            bool entered = false;
            BodyPose finalPose = startPose;
            Vector3 fallbackEnd = fallbackStart;

            while (Time.time - started < maxStateSeconds)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (info.fullPathHash == KnockdownHash)
                {
                    entered = true;
                    BodyPose current = CaptureBodyPose();
                    if (current.valid) finalPose = current;
                    if (_body != null) fallbackEnd = _body.bounds.center;

                    if (info.normalizedTime >= normalStateFinish && !_animator.IsInTransition(0))
                        break;
                }
                else if (entered)
                {
                    break;
                }
                yield return null;
            }

            BodyPose lyingWorldPose = finalPose.valid ? finalPose : CaptureBodyPose();
            BakeLandingIntoRoot(startPose, finalPose, fallbackEnd - fallbackStart);

            yield return new WaitForSeconds(knockdownLieSeconds);

            if (_animator.HasState(0, GetUpHash))
            {
                _animator.Play(GetUpHash, 0, 0f);
                _animator.Update(0f);
                BodyPose getUpStartPose = CaptureBodyPose();
                AlignCurrentPoseToTarget(getUpStartPose, lyingWorldPose);

                _animator.CrossFadeInFixedTime(GetUpHash, getUpCrossFade, 0, 0f);
                yield return WaitForStateComplete(GetUpHash, maxStateSeconds, .997f);

                // IMPORTANT: capture where the complete body ACTUALLY finished standing up.
                BodyPose getUpEndPose = CaptureBodyPose();

                // Sample the first Run frame immediately, restore the visual wrapper, then move/rotate
                // the gameplay root so Run starts at the exact final GetUp pose. This removes the
                // brief standstill and the 1-2m position jump after getting up.
                if (_animator.HasState(0, RunHash))
                {
                    _animator.Play(RunHash, 0, 0f);
                    _animator.Update(0f);

                    if (_visualRoot != null)
                    {
                        _visualRoot.localPosition = _visualBaseLocalPosition;
                        _visualRoot.localRotation = _visualBaseLocalRotation;
                    }

                    BodyPose runStartPose = CaptureBodyPose();
                    AlignCurrentPoseToTarget(runStartPose, getUpEndPose);
                    GroundRootAtCurrentXZ();

                    // Start moving visually right away instead of showing one Idle frame.
                    _animator.CrossFadeInFixedTime(RunHash, runCrossFade, 0, 0f);
                    _runAlreadyPrepared = true;
                }
            }

            if (_visualRoot != null && !_runAlreadyPrepared)
            {
                _visualRoot.localPosition = _visualBaseLocalPosition;
                _visualRoot.localRotation = _visualBaseLocalRotation;
            }

            _down = false;
        }

        private BodyPose CaptureBodyPose()
        {
            var points = new List<Vector3>(6);
            AddPoint(points, _hips);
            AddPoint(points, _head);
            AddPoint(points, _leftFoot);
            AddPoint(points, _rightFoot);
            AddPoint(points, _leftHand);
            AddPoint(points, _rightHand);

            BodyPose pose = new BodyPose();
            if (points.Count >= 2)
            {
                Vector3 center = Vector3.zero;
                foreach (Vector3 p in points) center += p;
                center /= points.Count;
                pose.center = center;
                pose.valid = true;
            }
            else if (_body != null)
            {
                pose.center = _body.bounds.center;
                pose.valid = true;
            }

            Vector3 lateral = Vector3.zero;
            if (_leftFoot != null && _rightFoot != null)
                lateral = _rightFoot.position - _leftFoot.position;
            else if (_leftHand != null && _rightHand != null)
                lateral = _rightHand.position - _leftHand.position;

            lateral.y = 0f;
            if (lateral.sqrMagnitude > .0004f)
                pose.lateral = lateral.normalized;
            else
                pose.lateral = transform.right;

            return pose;
        }

        private static void AddPoint(List<Vector3> points, Transform t)
        {
            if (t != null) points.Add(t.position);
        }

        private void BakeLandingIntoRoot(BodyPose startPose, BodyPose finalPose, Vector3 fallbackTravel)
        {
            Vector3 travel = fallbackTravel;
            if (startPose.valid && finalPose.valid)
                travel = finalPose.center - startPose.center;

            travel.y = 0f;
            if (travel.magnitude > maxKnockdownTravel)
                travel = travel.normalized * maxKnockdownTravel;

            float yaw = 0f;
            if (startPose.valid && finalPose.valid && startPose.lateral.sqrMagnitude > .001f && finalPose.lateral.sqrMagnitude > .001f)
            {
                yaw = Vector3.SignedAngle(startPose.lateral, finalPose.lateral, Vector3.up);
                yaw = Mathf.Clamp(yaw, -maxLandingYawCorrection, maxLandingYawCorrection);
            }

            transform.position += travel;
            transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;

            if (_visualRoot != null)
            {
                _visualRoot.localPosition -= transform.InverseTransformVector(travel);
                _visualRoot.localRotation = Quaternion.AngleAxis(-yaw, Vector3.up) * _visualRoot.localRotation;
            }

            GroundRootAtCurrentXZ();
        }

        private void AlignCurrentPoseToTarget(BodyPose current, BodyPose target)
        {
            if (!current.valid || !target.valid) return;

            if (current.lateral.sqrMagnitude > .001f && target.lateral.sqrMagnitude > .001f)
            {
                float yaw = Vector3.SignedAngle(current.lateral, target.lateral, Vector3.up);
                yaw = Mathf.Clamp(yaw, -maxLandingYawCorrection, maxLandingYawCorrection);
                transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;
            }

            BodyPose rotated = CaptureBodyPose();
            if (rotated.valid)
            {
                Vector3 delta = target.center - rotated.center;
                delta.y = 0f;
                if (delta.magnitude > maxKnockdownTravel)
                    delta = delta.normalized * maxKnockdownTravel;
                transform.position += delta;
            }

            GroundRootAtCurrentXZ();
        }

        private void GroundRootAtCurrentXZ()
        {
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

        private IEnumerator InterruptibleWait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        private IEnumerator FleeSequence()
        {
            if (_attacker == null)
            {
                _runAlreadyPrepared = false;
                SetWandererEnabled(true);
                yield break;
            }

            if (!_runAlreadyPrepared)
                PlayState(RunHash, runCrossFade);
            _runAlreadyPrepared = false;

            float until = Time.time + fleeSeconds;
            while (Time.time < until)
            {
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
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toward.normalized), .85f);
        }

        private void SetWandererEnabled(bool value)
        {
            if (_wanderer != null) _wanderer.enabled = value;
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _runAlreadyPrepared = false;
            if (_visualRoot != null)
            {
                _visualRoot.localPosition = _visualBaseLocalPosition;
                _visualRoot.localRotation = _visualBaseLocalRotation;
            }
            if (_wanderer != null) _wanderer.enabled = true;
        }
    }
}
