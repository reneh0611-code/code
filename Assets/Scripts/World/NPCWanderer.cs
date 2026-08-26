using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 1.35f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float minPause = 1.25f;
        [SerializeField] private float maxPause = 4f;
        [SerializeField] private float groundSearchUp = 1.25f;
        [SerializeField] private float groundSearchDown = 3f;
        [SerializeField] private float maxGroundCorrection = 0.5f;

        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");

        private CharacterController _controller;
        private SkinnedMeshRenderer[] _skinnedRenderers;
        private Mesh[] _bakedMeshes;
        private Vector3 _home;
        private Vector3 _target;
        private float _pause;
        private bool _walking;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r != null)
                .ToArray();

            _bakedMeshes = new Mesh[_skinnedRenderers.Length];
            for (int i = 0; i < _bakedMeshes.Length; i++)
            {
                _bakedMeshes[i] = new Mesh
                {
                    name = $"{name}_GroundBake_{i}"
                };
                _bakedMeshes[i].MarkDynamic();
            }
        }

        private void OnDestroy()
        {
            if (_bakedMeshes == null) return;
            foreach (Mesh mesh in _bakedMeshes)
            {
                if (mesh != null)
                    Destroy(mesh);
            }
        }

        private void Start()
        {
            _home = transform.position;
            Pause(Random.Range(0.4f, 2.5f));
        }

        private void Update()
        {
            if (_pause > 0f)
            {
                _pause -= Time.deltaTime;
                if (_pause <= 0f)
                    PickTarget();
                return;
            }

            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.4f * 0.4f)
            {
                Pause(Random.Range(minPause, maxPause));
                return;
            }

            Vector3 direction = toTarget.normalized;
            Quaternion wantedRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

            // Horizontal locomotion only. Exact vertical placement is solved from the actual skinned mesh in LateUpdate.
            _controller.Move(direction * walkSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            LockActualMeshToGround();
        }

        private void PickTarget()
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            _target = _home + new Vector3(circle.x, 0f, circle.y);
            SetWalking(true);
        }

        private void Pause(float duration)
        {
            _pause = duration;
            SetWalking(false);
        }

        private void SetWalking(bool walking)
        {
            if (_walking == walking)
                return;

            _walking = walking;
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
                return;

            int state = walking ? WalkHash : IdleHash;
            if (animator.HasState(0, state))
                animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
        }

        private void LockActualMeshToGround()
        {
            if (_controller == null || _skinnedRenderers == null || _skinnedRenderers.Length == 0)
                return;

            bool foundVertex = false;
            float lowestWorldY = float.PositiveInfinity;
            Vector3 sampleCenter = transform.position;
            int centerSamples = 0;

            for (int i = 0; i < _skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer smr = _skinnedRenderers[i];
                Mesh baked = _bakedMeshes[i];
                if (smr == null || baked == null || !smr.enabled || !smr.gameObject.activeInHierarchy)
                    continue;

                smr.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                Matrix4x4 localToWorld = smr.transform.localToWorldMatrix;

                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 world = localToWorld.MultiplyPoint3x4(vertices[v]);
                    if (world.y < lowestWorldY)
                        lowestWorldY = world.y;
                }

                sampleCenter += smr.bounds.center;
                centerSamples++;
                foundVertex = true;
            }

            if (!foundVertex || float.IsInfinity(lowestWorldY))
                return;

            if (centerSamples > 0)
                sampleCenter /= centerSamples + 1f;

            Vector3 origin = new(sampleCenter.x, lowestWorldY + groundSearchUp, sampleCenter.z);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                groundSearchUp + groundSearchDown,
                ~0,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            float groundY = 0f;
            bool foundGround = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    foundGround = true;
                }
            }

            if (!foundGround)
                return;

            float delta = Mathf.Clamp(groundY - lowestWorldY, -maxGroundCorrection, maxGroundCorrection);
            if (Mathf.Abs(delta) > 0.0005f)
                _controller.Move(Vector3.up * delta);
        }

        public void Configure(float speed, float radius)
        {
            walkSpeed = speed;
            wanderRadius = radius;
        }
    }
}
