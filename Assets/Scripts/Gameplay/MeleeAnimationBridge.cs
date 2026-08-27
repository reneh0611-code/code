using System.Collections;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEngine;

public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private float minimumAttackGap = 0.08f;
    [SerializeField] private float crossFade = 0.06f;
    [SerializeField] private float returnToLocomotionBlend = 0.12f;
    [SerializeField, Range(0.75f, 1f)] private float finishNormalizedTime = 0.97f;
    [SerializeField, Range(0.35f, 0.9f)] private float comboWindowNormalized = 0.58f;
    [SerializeField] private float maxAttackAnimationSeconds = 3f;

    [Header("Very close melee range")]
    [SerializeField] private float hitRadius = 1.50f;
    [SerializeField, Range(0.05f, 0.9f)] private float hitMomentNormalized = 0.34f;

    private static readonly int[] PunchHashes =
    {
        Animator.StringToHash("Base Layer.Punch1"),
        Animator.StringToHash("Base Layer.Punch2"),
        Animator.StringToHash("Base Layer.Punch3"),
        Animator.StringToHash("Base Layer.Punch4"),
        Animator.StringToHash("Base Layer.Punch5")
    };

    private CharacterAnimationDriver locomotionDriver;
    private NetworkPlayerController movementController;
    private float nextAttackTime;
    private int lastPunchIndex = -1;
    private int currentPunchIndex;
    private bool attackRunning;
    private bool comboWindowOpen;
    private bool queuedPunch;
    private readonly Collider[] hitBuffer = new Collider[24];

    private void Awake() => RefreshReferences();
    private void OnEnable() => RefreshReferences();

    private void RefreshReferences()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>(true);
        if (!locomotionDriver) locomotionDriver = GetComponent<CharacterAnimationDriver>();
        if (!movementController) movementController = GetComponent<NetworkPlayerController>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0) || Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + minimumAttackGap;

        if (!attackRunning)
        {
            StartCoroutine(AttackChain());
            return;
        }

        if (comboWindowOpen)
            queuedPunch = true;
    }

    private int PickPunchIndex()
    {
        if (PunchHashes.Length <= 1) return 0;
        int index;
        do index = Random.Range(0, PunchHashes.Length);
        while (index == lastPunchIndex);
        lastPunchIndex = index;
        return index;
    }

    private IEnumerator AttackChain()
    {
        attackRunning = true;
        queuedPunch = false;
        RefreshReferences();

        if (movementController != null) movementController.SetCombatMovementLocked(true);
        if (locomotionDriver != null) locomotionDriver.enabled = false;

        bool continueCombo;
        do
        {
            continueCombo = false;
            comboWindowOpen = false;
            queuedPunch = false;

            currentPunchIndex = PickPunchIndex();
            int state = PunchHashes[currentPunchIndex];
            string stateName = $"Punch{currentPunchIndex + 1}";

            bool stateExists = playerAnimator != null && playerAnimator.HasState(0, state);
            if (stateExists)
            {
                playerAnimator.enabled = true;
                playerAnimator.applyRootMotion = false;
                playerAnimator.speed = 1f;
                playerAnimator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
            }
            else
            {
                Debug.LogError($"[CYDOY MELEE] Missing {stateName} state in {playerAnimator?.runtimeAnimatorController?.name}.", playerAnimator);
            }

            bool hitApplied = false;
            float started = Time.time;
            bool enteredState = false;

            while (stateExists && Time.time - started < maxAttackAnimationSeconds)
            {
                AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);

                if (info.fullPathHash == state)
                {
                    enteredState = true;

                    if (!hitApplied && info.normalizedTime >= hitMomentNormalized)
                    {
                        hitApplied = true;
                        TryHitNearestNpc();
                    }

                    if (info.normalizedTime >= comboWindowNormalized)
                        comboWindowOpen = true;

                    if (queuedPunch && comboWindowOpen)
                    {
                        continueCombo = true;
                        break;
                    }

                    if (info.normalizedTime >= finishNormalizedTime && !playerAnimator.IsInTransition(0))
                        break;
                }
                else if (enteredState)
                {
                    break;
                }

                yield return null;
            }

            if (!hitApplied) TryHitNearestNpc();

            if (queuedPunch && comboWindowOpen)
                continueCombo = true;

        } while (continueCombo);

        comboWindowOpen = false;
        queuedPunch = false;

        if (movementController != null) movementController.SetCombatMovementLocked(false);
        if (locomotionDriver != null)
        {
            locomotionDriver.enabled = true;
            locomotionDriver.ResumeFromCombat(returnToLocomotionBlend);
        }
        attackRunning = false;
    }

    private void OnDisable()
    {
        comboWindowOpen = false;
        queuedPunch = false;
        if (movementController != null) movementController.SetCombatMovementLocked(false);
        if (locomotionDriver != null)
        {
            locomotionDriver.enabled = true;
            locomotionDriver.ResumeFromCombat(returnToLocomotionBlend);
        }
        attackRunning = false;
    }

    private void TryHitNearestNpc()
    {
        Vector3 center = transform.position + Vector3.up * 0.9f;
        int count = Physics.OverlapSphereNonAlloc(center, hitRadius, hitBuffer, ~0, QueryTriggerInteraction.Collide);
        NPCWanderer best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            NPCWanderer npc = hit.GetComponentInParent<NPCWanderer>();
            if (npc == null || npc.IsDown) continue;

            float distance = HorizontalDistanceSquared(transform.position, npc.transform.position);
            if (distance > hitRadius * hitRadius || distance >= bestDistance) continue;
            bestDistance = distance;
            best = npc;
        }

        if (best == null) return;
        Vector3 hitDirection = best.transform.position - transform.position;
        hitDirection.y = 0f;
        best.HitByPlayerPunch(hitDirection.normalized, currentPunchIndex + 1, transform);
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }
}
