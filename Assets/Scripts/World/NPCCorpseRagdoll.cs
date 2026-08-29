using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class NPCCorpseRagdoll : MonoBehaviour
    {
        private readonly List<Rigidbody> _bodies = new();
        private readonly List<Collider> _colliders = new();
        private readonly List<Collider> _ignoredCarrierColliders = new();

        private Animator _animator;
        private SkinnedMeshRenderer _skin;
        private Rigidbody _chestBody;
        private Rigidbody _leftShoulderBody;
        private Rigidbody _rightShoulderBody;
        private Transform _carrier;
        private Vector3 _chestGrabStartPosition;
        private Vector3 _leftGrabStartPosition;
        private Vector3 _rightGrabStartPosition;
        private Quaternion _chestGrabStartRotation;
        private Quaternion _leftGrabStartRotation;
        private Quaternion _rightGrabStartRotation;
        private Quaternion _chestGrabRotationOffset;
        private Quaternion _leftGrabRotationOffset;
        private Quaternion _rightGrabRotationOffset;
        private float _shoulderHalfSpan;
        private float _grabStartedAt;
        private bool _active;

        public bool IsActive => _active;
        public bool IsDragged => _carrier != null;
        public Vector3 BodyCenter => _skin != null ? _skin.bounds.center : transform.position;

        public bool Activate(Animator animator, SkinnedMeshRenderer skin)
        {
            if (_active) return true;
            if (animator == null) return false;

            _animator = animator;
            _skin = skin;
            Transform[] all = animator.GetComponentsInChildren<Transform>(true);
            Transform hips = FindBone(all, null, "hips", "pelvis");
            Transform chest = FindBone(all, null, "spine2", "chest", "spine1", "spine");
            Transform head = FindBone(all, null, "head");
            Transform leftUpperArm = FindBone(all, "left", "upperarm", "arm");
            Transform leftLowerArm = FindBone(all, "left", "forearm", "lowerarm");
            Transform leftHand = FindBone(all, "left", "hand", "wrist");
            Transform rightUpperArm = FindBone(all, "right", "upperarm", "arm");
            Transform rightLowerArm = FindBone(all, "right", "forearm", "lowerarm");
            Transform rightHand = FindBone(all, "right", "hand", "wrist");
            Transform leftUpperLeg = FindBone(all, "left", "upleg", "thigh", "upperleg");
            Transform leftLowerLeg = FindBone(all, "left", "leg", "calf", "lowerleg");
            Transform leftFoot = FindBone(all, "left", "foot", "ankle");
            Transform rightUpperLeg = FindBone(all, "right", "upleg", "thigh", "upperleg");
            Transform rightLowerLeg = FindBone(all, "right", "leg", "calf", "lowerleg");
            Transform rightFoot = FindBone(all, "right", "foot", "ankle");

            if (hips == null || chest == null || head == null) return false;

            Rigidbody hipsBody = AddBody(hips, 5.5f);
            AddBoxCollider(hips, new Vector3(.30f, .20f, .24f));
            Rigidbody chestBody = AddBody(chest, 4.5f);
            AddBoxCollider(chest, new Vector3(.34f, .25f, .20f));
            Connect(chestBody, hipsBody, 25f, 35f);
            Rigidbody headBody = AddBody(head, 1.6f);
            AddSphereCollider(head, .13f);
            Connect(headBody, chestBody, 22f, 32f);

            Rigidbody leftUpperBody = BuildLimb(leftUpperArm, leftLowerArm, chestBody, .075f, 1.6f);
            Rigidbody leftLowerBody = BuildLimb(leftLowerArm, leftHand, BodyOf(leftUpperArm), .060f, 1.2f);
            BuildEnd(leftHand, leftLowerBody, .055f, .65f);
            Rigidbody rightUpperBody = BuildLimb(rightUpperArm, rightLowerArm, chestBody, .075f, 1.6f);
            Rigidbody rightLowerBody = BuildLimb(rightLowerArm, rightHand, BodyOf(rightUpperArm), .060f, 1.2f);
            BuildEnd(rightHand, rightLowerBody, .055f, .65f);
            BuildLimb(leftUpperLeg, leftLowerLeg, hipsBody, .095f, 2.6f);
            Rigidbody leftLowerLegBody = BuildLimb(leftLowerLeg, leftFoot, BodyOf(leftUpperLeg), .075f, 2.0f);
            BuildEnd(leftFoot, leftLowerLegBody, .07f, .8f);
            BuildLimb(rightUpperLeg, rightLowerLeg, hipsBody, .095f, 2.6f);
            Rigidbody rightLowerLegBody = BuildLimb(rightLowerLeg, rightFoot, BodyOf(rightUpperLeg), .075f, 2.0f);
            BuildEnd(rightFoot, rightLowerLegBody, .07f, .8f);

            if (_bodies.Count < 8) return false;
            for (int i = 0; i < _colliders.Count; i++)
                for (int j = i + 1; j < _colliders.Count; j++)
                    Physics.IgnoreCollision(_colliders[i], _colliders[j], true);

            _chestBody = chestBody;
            _leftShoulderBody = leftUpperBody;
            _rightShoulderBody = rightUpperBody;
            if (_chestBody == null || _leftShoulderBody == null || _rightShoulderBody == null) return false;

            // Keep the exact last evaluated Dying pose until the player actually grabs the body.
            // Waking the ragdoll here would let overlapping ground colliders move the corpse away
            // from the visible death point before the pull interaction even starts.
            animator.enabled = false;
            foreach (Rigidbody body in _bodies)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            _active = true;
            return true;
        }

        public bool BeginDrag(Transform carrier)
        {
            if (!_active || _chestBody == null || _leftShoulderBody == null ||
                _rightShoulderBody == null || carrier == null || IsDragged)
                return false;

            _carrier = carrier;
            _chestGrabStartPosition = _chestBody.position;
            _leftGrabStartPosition = _leftShoulderBody.position;
            _rightGrabStartPosition = _rightShoulderBody.position;
            _chestGrabStartRotation = _chestBody.rotation;
            _leftGrabStartRotation = _leftShoulderBody.rotation;
            _rightGrabStartRotation = _rightShoulderBody.rotation;
            _chestGrabRotationOffset = Quaternion.Inverse(carrier.rotation) * _chestBody.rotation;
            _leftGrabRotationOffset = Quaternion.Inverse(carrier.rotation) * _leftShoulderBody.rotation;
            _rightGrabRotationOffset = Quaternion.Inverse(carrier.rotation) * _rightShoulderBody.rotation;
            _shoulderHalfSpan = Mathf.Clamp(
                Vector3.Distance(_leftShoulderBody.position, _rightShoulderBody.position) * .5f,
                .18f,
                .32f);
            _grabStartedAt = Time.time;

            foreach (Rigidbody body in _bodies)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                bool upperBodyAnchor = body == _chestBody || body == _leftShoulderBody || body == _rightShoulderBody;
                body.isKinematic = upperBodyAnchor;
                body.collisionDetectionMode = upperBodyAnchor
                    ? CollisionDetectionMode.ContinuousSpeculative
                    : CollisionDetectionMode.Discrete;
                if (!upperBodyAnchor) body.WakeUp();
            }

            _ignoredCarrierColliders.Clear();
            foreach (Collider carrierCollider in carrier.GetComponentsInChildren<Collider>(true))
            {
                if (carrierCollider == null) continue;
                _ignoredCarrierColliders.Add(carrierCollider);
                foreach (Collider bodyCollider in _colliders)
                    if (bodyCollider != null) Physics.IgnoreCollision(bodyCollider, carrierCollider, true);
            }
            return true;
        }

        public void EndDrag()
        {
            if (!IsDragged) return;
            foreach (Collider carrierCollider in _ignoredCarrierColliders)
                foreach (Collider bodyCollider in _colliders)
                    if (carrierCollider != null && bodyCollider != null)
                        Physics.IgnoreCollision(bodyCollider, carrierCollider, false);
            _ignoredCarrierColliders.Clear();
            foreach (Rigidbody body in _bodies)
            {
                if (body == null) continue;
                body.isKinematic = false;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            _carrier = null;
        }

        private void FixedUpdate()
        {
            if (!IsDragged || _chestBody == null || _leftShoulderBody == null || _rightShoulderBody == null) return;

            Vector3 leftTarget = _carrier.TransformPoint(new Vector3(-_shoulderHalfSpan, .71f, .50f));
            Vector3 rightTarget = _carrier.TransformPoint(new Vector3(_shoulderHalfSpan, .71f, .50f));
            Vector3 chestTarget = _carrier.TransformPoint(new Vector3(0f, .57f, .52f));
            Quaternion chestTargetRotation = _carrier.rotation * _chestGrabRotationOffset;
            Quaternion leftTargetRotation = _carrier.rotation * _leftGrabRotationOffset;
            Quaternion rightTargetRotation = _carrier.rotation * _rightGrabRotationOffset;
            float t = Mathf.Clamp01((Time.time - _grabStartedAt) / .58f);
            t = t * t * (3f - 2f * t);

            _chestBody.MovePosition(Vector3.Lerp(_chestGrabStartPosition, chestTarget, t));
            _leftShoulderBody.MovePosition(Vector3.Lerp(_leftGrabStartPosition, leftTarget, t));
            _rightShoulderBody.MovePosition(Vector3.Lerp(_rightGrabStartPosition, rightTarget, t));
            _chestBody.MoveRotation(Quaternion.Slerp(_chestGrabStartRotation, chestTargetRotation, t));
            _leftShoulderBody.MoveRotation(Quaternion.Slerp(_leftGrabStartRotation, leftTargetRotation, t));
            _rightShoulderBody.MoveRotation(Quaternion.Slerp(_rightGrabStartRotation, rightTargetRotation, t));

            foreach (Rigidbody body in _bodies)
            {
                if (body == null || body.isKinematic) continue;
                Vector3 velocity = body.linearVelocity;
                Vector3 angularVelocity = body.angularVelocity;
                if (!IsFinite(velocity)) velocity = Vector3.zero;
                if (!IsFinite(angularVelocity)) angularVelocity = Vector3.zero;
                body.linearVelocity = Vector3.ClampMagnitude(velocity, 4.5f);
                body.angularVelocity = Vector3.ClampMagnitude(angularVelocity, 8f);
            }
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private Rigidbody BuildLimb(Transform bone, Transform next, Rigidbody parent, float radius, float mass)
        {
            if (bone == null || next == null || parent == null) return null;
            Rigidbody body = AddBody(bone, mass);
            AddCapsuleCollider(bone, next, radius);
            Connect(body, parent, 35f, 48f);
            return body;
        }

        private Rigidbody BuildEnd(Transform bone, Rigidbody parent, float radius, float mass)
        {
            if (bone == null || parent == null) return null;
            Rigidbody body = AddBody(bone, mass);
            AddSphereCollider(bone, radius);
            Connect(body, parent, 40f, 52f);
            return body;
        }

        private Rigidbody AddBody(Transform bone, float mass)
        {
            Rigidbody body = bone.GetComponent<Rigidbody>();
            if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            body.isKinematic = true;
            body.linearDamping = .16f;
            body.angularDamping = 1.8f;
            body.maxAngularVelocity = 12f;
            body.maxDepenetrationVelocity = 1.5f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _bodies.Add(body);
            return body;
        }

        private Rigidbody BodyOf(Transform bone) => bone != null ? bone.GetComponent<Rigidbody>() : null;

        private void AddCapsuleCollider(Transform bone, Transform next, float radius)
        {
            Vector3 local = bone.InverseTransformPoint(next.position);
            float length = local.magnitude;
            if (length < .02f) return;
            GameObject shape = new("RagdollCapsule");
            shape.transform.SetParent(bone, false);
            shape.transform.localPosition = local * .5f;
            shape.transform.localRotation = Quaternion.FromToRotation(Vector3.up, local.normalized);
            CapsuleCollider collider = shape.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, length);
            _colliders.Add(collider);
        }

        private void AddSphereCollider(Transform bone, float radius)
        {
            SphereCollider collider = bone.gameObject.AddComponent<SphereCollider>();
            collider.radius = radius;
            _colliders.Add(collider);
        }

        private void AddBoxCollider(Transform bone, Vector3 size)
        {
            BoxCollider collider = bone.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            _colliders.Add(collider);
        }

        private static void Connect(Rigidbody body, Rigidbody parent, float swing, float twist)
        {
            if (body == null || parent == null) return;
            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = false;
            joint.enablePreprocessing = false;
            SoftJointLimit low = joint.lowTwistLimit; low.limit = -twist; joint.lowTwistLimit = low;
            SoftJointLimit high = joint.highTwistLimit; high.limit = twist; joint.highTwistLimit = high;
            SoftJointLimit one = joint.swing1Limit; one.limit = swing; joint.swing1Limit = one;
            SoftJointLimit two = joint.swing2Limit; two.limit = swing; joint.swing2Limit = two;
        }

        private static Transform FindBone(IEnumerable<Transform> bones, string side, params string[] priorities)
        {
            Transform[] candidates = bones.Where(bone => string.IsNullOrEmpty(side) || Normalize(bone.name).Contains(side)).ToArray();
            foreach (string token in priorities)
            {
                string normalized = Normalize(token);
                Transform exact = candidates
                    .Where(bone => Normalize(bone.name).EndsWith(normalized, StringComparison.Ordinal))
                    .OrderBy(bone => Normalize(bone.name).Length)
                    .FirstOrDefault();
                if (exact != null) return exact;
            }
            return null;
        }

        private static string Normalize(string value) => string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private void OnDisable()
        {
            if (IsDragged) EndDrag();
        }
    }
}
