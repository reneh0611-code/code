using System.Collections;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(800)]
public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private float minimumAttackGap = 0.08f;
    [SerializeField] private float crossFade = 0.08f;
    [SerializeField] private float comboCrossFade = 0.12f;
    [SerializeField] private float returnToLocomotionBlend = 0.12f;
    [SerializeField, Range(0.75f, 1f)] private float finishNormalizedTime = 0.97f;
    [SerializeField, Range(0.35f, 0.9f)] private float comboWindowNormalized = 0.48f;
    [SerializeField] private float maxAttackAnimationSeconds = 3f;

    [Header("Player actions")]
    [SerializeField] private float actionCrossFade = .14f;
    [SerializeField, Range(.9f, 1f)] private float actionFinishNormalizedTime = .985f;
    [SerializeField] private float maxActionAnimationSeconds = 5f;
    [SerializeField] private float rollMovementSpeed = 3.0f;
    [SerializeField] private float runKickMovementSpeed = 2.8f;
    [SerializeField] private float rollAnimationSpeed = 1.25f;
    [SerializeField, Range(.2f, .8f)] private float rollMovementEndNormalized = .55f;
    [SerializeField, Range(.2f, .8f)] private float runKickMovementEndNormalized = .58f;
    [SerializeField] private float rollMaximumTravelSeconds = 1.15f;
    [SerializeField] private float runKickMaximumTravelSeconds = .9f;
    [SerializeField, Range(.05f, .9f)] private float runKickHitNormalized = .43f;

    [Header("Very close melee range")]
    [SerializeField] private float hitRadius = 1.50f;
    [SerializeField, Range(0.05f, 0.9f)] private float hitMomentNormalized = 0.34f;
    [SerializeField, Range(-.25f, .8f)] private float targetFacingThreshold = .05f;

    private static readonly int[] PunchHashes =
    {
        Animator.StringToHash("Base Layer.Punch1"),
        Animator.StringToHash("Base Layer.Punch2"),
        Animator.StringToHash("Base Layer.Punch3"),
        Animator.StringToHash("Base Layer.Punch4"),
        Animator.StringToHash("Base Layer.Punch5")
    };
    private static readonly int SitDownHash = Animator.StringToHash("Base Layer.SitDown");
    private static readonly int SittingIdleHash = Animator.StringToHash("Base Layer.SittingIdle");
    private static readonly int SitToStandHash = Animator.StringToHash("Base Layer.SitToStand");
    private static readonly int RollHash = Animator.StringToHash("Base Layer.Roll");
    private static readonly int RunKickHash = Animator.StringToHash("Base Layer.RunKick");

    private enum PlayerActionState { None, SittingDown, Sitting, StandingUp, Rolling }

    private CharacterAnimationDriver locomotionDriver;
    private NetworkPlayerController movementController;
    private CorpseCarryController carryController;
    private float nextAttackTime;
    private int currentPunchIndex;
    private bool attackRunning;
    private bool comboWindowOpen;
    private bool queuedPunch;
    private PlayerActionState actionState;
    private Coroutine actionRoutine;
    private bool runKickActive;
    private Transform actionVisualRoot;
    private Transform actionMotionReference;
    private Vector3 actionReferenceLocalPosition;
    private Vector2 actionVisualBaseLocalXZ;
    private bool stabilizeActionVisual;
    private bool recoverActionVisual;
    private float actionVisualRecoveryAfter;
    private NPCWanderer currentAttackTarget;
    private readonly Collider[] hitBuffer = new Collider[24];

    public bool IsRolling => actionState == PlayerActionState.Rolling;
    public bool IsRunKicking => runKickActive;
    public bool IsAttacking => attackRunning;
    public bool IsActionGroundingActive => stabilizeActionVisual;
    public bool HasStrikeTarget => actionState == PlayerActionState.None && FindBestHitTarget() != null;

    private void Awake() => RefreshReferences();
    private void OnEnable() => RefreshReferences();

    private void RefreshReferences()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>(true);
        if (!locomotionDriver) locomotionDriver = GetComponent<CharacterAnimationDriver>();
        if (!movementController) movementController = GetComponent<NetworkPlayerController>();
        if (!carryController) carryController = GetComponent<CorpseCarryController>();
    }

    private void Update()
    {
        if (carryController != null && carryController.BlocksCombat) return;
        if (HandleActionInput()) return;
        if (actionState != PlayerActionState.None) return;
        if (!Input.GetMouseButtonDown(0) || Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + minimumAttackGap;

        if (!attackRunning)
        {
            StartCoroutine(AttackChain());
            return;
        }

        // Remember the next click even when it arrives before the transition
        // window. This removes the dead pause between deliberate combo hits.
        queuedPunch = true;
    }

    private void LateUpdate()
    {
        if (!stabilizeActionVisual || actionVisualRoot == null || actionMotionReference == null) return;

        if (!recoverActionVisual || Time.time < actionVisualRecoveryAfter ||
            (playerAnimator != null && playerAnimator.IsInTransition(0)))
        {
            Vector3 desiredReference = transform.TransformPoint(actionReferenceLocalPosition);
            Vector3 correction = desiredReference - actionMotionReference.position;
            correction.y = 0f;
            actionVisualRoot.position += correction;
            return;
        }

        Vector3 local = actionVisualRoot.localPosition;
        Vector2 currentXZ = new(local.x, local.z);
        float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
        currentXZ = Vector2.Lerp(currentXZ, actionVisualBaseLocalXZ, blend);
        local.x = currentXZ.x;
        local.z = currentXZ.y;
        actionVisualRoot.localPosition = local;
        if ((currentXZ - actionVisualBaseLocalXZ).sqrMagnitude < .000001f)
        {
            local.x = actionVisualBaseLocalXZ.x;
            local.z = actionVisualBaseLocalXZ.y;
            actionVisualRoot.localPosition = local;
            stabilizeActionVisual = false;
            recoverActionVisual = false;
        }
    }

    private IEnumerator AttackChain()
    {
        attackRunning = true;
        queuedPunch = false;
        RefreshReferences();

        if (locomotionDriver != null) locomotionDriver.enabled = false;

        bool firstAttack = true;
        int comboStep = 0;
        bool startWithRunKick = movementController != null && movementController.IsSprinting &&
                                playerAnimator != null && playerAnimator.HasState(0, RunKickHash);

        bool continueCombo;
        do
        {
            continueCombo = false;
            comboWindowOpen = false;
            queuedPunch = false;

            bool runKick = firstAttack && startWithRunKick;
            runKickActive = runKick;
            // A fixed sequence lets every recovery pose flow into the intended
            // follow-up instead of jumping between random punch clips.
            currentPunchIndex = runKick ? PunchHashes.Length - 1 : comboStep % PunchHashes.Length;
            if (!runKick) comboStep++;
            int state = runKick ? RunKickHash : PunchHashes[currentPunchIndex];
            string stateName = runKick ? "RunKick" : $"Punch{currentPunchIndex + 1}";
            currentAttackTarget = FindBestHitTarget();
            ConfigureAttackMovement(runKick);
            if (!runKick && currentAttackTarget != null && movementController != null)
                movementController.FaceCombatTarget(currentAttackTarget.StrikeTargetPosition - transform.position);

            bool stateExists = playerAnimator != null && playerAnimator.HasState(0, state);
            if (stateExists)
            {
                if (runKick) BeginActionVisualStabilization();
                playerAnimator.enabled = true;
                playerAnimator.applyRootMotion = false;
                playerAnimator.speed = 1f;
                float attackBlend = runKick ? actionCrossFade : firstAttack ? crossFade : comboCrossFade;
                playerAnimator.CrossFadeInFixedTime(state, attackBlend, 0, 0f);
            }
            else
            {
                Debug.LogError($"[CYDOY MELEE] Missing {stateName} state in {playerAnimator?.runtimeAnimatorController?.name}.", playerAnimator);
            }

            bool hitApplied = false;
            float started = Time.time;
            bool enteredState = false;
            bool actionTravelStopped = !runKick;

            while (stateExists && Time.time - started < maxAttackAnimationSeconds)
            {
                AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);

                if (info.fullPathHash == state)
                {
                    enteredState = true;

                    if (runKick && !actionTravelStopped && info.normalizedTime >= runKickMovementEndNormalized)
                    {
                        StopActionTravel();
                        actionTravelStopped = true;
                    }

                    float hitAt = runKick ? runKickHitNormalized : hitMomentNormalized;
                    if (!hitApplied && info.normalizedTime >= hitAt)
                    {
                        hitApplied = true;
                        TryHitNearestNpc();
                    }

                    if (!runKick && info.normalizedTime >= comboWindowNormalized)
                        comboWindowOpen = true;

                    if (!runKick && queuedPunch && comboWindowOpen)
                    {
                        continueCombo = true;
                        break;
                    }

                    float finishAt = runKick ? actionFinishNormalizedTime : finishNormalizedTime;
                    if (info.normalizedTime >= finishAt && !playerAnimator.IsInTransition(0))
                        break;
                }
                else if (enteredState)
                {
                    break;
                }

                if (runKick && !actionTravelStopped && Time.time - started >= runKickMaximumTravelSeconds)
                {
                    StopActionTravel();
                    actionTravelStopped = true;
                }

                yield return null;
            }

            if (runKick && !actionTravelStopped) StopActionTravel();

            if (!hitApplied) TryHitNearestNpc();

            if (!runKick && queuedPunch && comboWindowOpen)
                continueCombo = true;

            firstAttack = false;

        } while (continueCombo);

        comboWindowOpen = false;
        queuedPunch = false;
        runKickActive = false;
        currentAttackTarget = null;
        if (playerAnimator != null) playerAnimator.speed = 1f;

        RestorePlayerControl();
        attackRunning = false;
    }

    private void ConfigureAttackMovement(bool runKick)
    {
        if (movementController == null) return;
        movementController.SetCombatMovementLocked(!runKick);
        movementController.SetActionMovement(runKick, runKickMovementSpeed);
    }

    private void OnDisable()
    {
        comboWindowOpen = false;
        queuedPunch = false;
        if (actionRoutine != null) StopCoroutine(actionRoutine);
        actionRoutine = null;
        actionState = PlayerActionState.None;
        runKickActive = false;
        RestorePlayerControl();
        ResetActionVisualImmediately();
        attackRunning = false;
    }

    private bool HandleActionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || transform.parent != null) return false;

        if (keyboard.cKey.wasPressedThisFrame)
        {
            if (actionState == PlayerActionState.Sitting)
            {
                actionRoutine = StartCoroutine(StandUpSequence());
                return true;
            }
            if (actionState == PlayerActionState.None && !attackRunning)
            {
                actionRoutine = StartCoroutine(SitDownSequence());
                return true;
            }
        }

        bool rollPressed = keyboard.leftAltKey.wasPressedThisFrame || keyboard.rightAltKey.wasPressedThisFrame;
        if (rollPressed && actionState == PlayerActionState.None && !attackRunning &&
            movementController != null && movementController.IsGrounded)
        {
            actionRoutine = StartCoroutine(RollSequence());
            return true;
        }
        return actionState != PlayerActionState.None;
    }

    private IEnumerator SitDownSequence()
    {
        RefreshReferences();
        if (!PreparePlayerAction(SitDownHash, PlayerActionState.SittingDown, false)) yield break;
        yield return WaitForStateComplete(SitDownHash);

        if (playerAnimator != null && playerAnimator.HasState(0, SittingIdleHash))
        {
            playerAnimator.CrossFadeInFixedTime(SittingIdleHash, actionCrossFade, 0, 0f);
            actionState = PlayerActionState.Sitting;
        }
        else
        {
            Debug.LogError("[CYDOY ACTION] Missing SittingIdle state.", playerAnimator);
            actionState = PlayerActionState.None;
            RestorePlayerControl();
        }
        actionRoutine = null;
    }

    private IEnumerator StandUpSequence()
    {
        if (!PreparePlayerAction(SitToStandHash, PlayerActionState.StandingUp, false)) yield break;
        yield return WaitForStateComplete(SitToStandHash);
        actionState = PlayerActionState.None;
        actionRoutine = null;
        RestorePlayerControl();
    }

    private IEnumerator RollSequence()
    {
        if (!PreparePlayerAction(RollHash, PlayerActionState.Rolling, true)) yield break;
        yield return WaitForStateComplete(RollHash, rollMovementEndNormalized, rollMaximumTravelSeconds);
        actionState = PlayerActionState.None;
        actionRoutine = null;
        RestorePlayerControl();
    }

    private bool PreparePlayerAction(int stateHash, PlayerActionState newState, bool movingAction)
    {
        RefreshReferences();
        if (playerAnimator == null || !playerAnimator.HasState(0, stateHash))
        {
            Debug.LogError($"[CYDOY ACTION] Missing animation state hash {stateHash}.", playerAnimator);
            actionState = PlayerActionState.None;
            return false;
        }

        actionState = newState;
        if (locomotionDriver != null) locomotionDriver.enabled = false;
        if (movementController != null)
        {
            movementController.SetCombatMovementLocked(!movingAction);
            movementController.SetActionMovement(movingAction, rollMovementSpeed);
        }
        playerAnimator.enabled = true;
        playerAnimator.applyRootMotion = false;
        playerAnimator.speed = stateHash == RollHash ? rollAnimationSpeed : 1f;
        if (stateHash == RollHash) BeginActionVisualStabilization();
        playerAnimator.CrossFadeInFixedTime(stateHash, actionCrossFade, 0, 0f);
        return true;
    }

    private IEnumerator WaitForStateComplete(int stateHash, float movementEndNormalized = -1f, float maximumTravelSeconds = 0f)
    {
        float started = Time.time;
        bool entered = false;
        bool actionTravelStopped = movementEndNormalized < 0f;
        while (playerAnimator != null && Time.time - started < maxActionAnimationSeconds)
        {
            AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.fullPathHash == stateHash)
            {
                entered = true;
                if (!actionTravelStopped && info.normalizedTime >= movementEndNormalized)
                {
                    StopActionTravel();
                    actionTravelStopped = true;
                }
                if (info.normalizedTime >= actionFinishNormalizedTime && !playerAnimator.IsInTransition(0))
                    yield break;
            }
            else if (entered)
            {
                yield break;
            }

            if (!actionTravelStopped && maximumTravelSeconds > 0f && Time.time - started >= maximumTravelSeconds)
            {
                StopActionTravel();
                actionTravelStopped = true;
            }
            yield return null;
        }
        if (!actionTravelStopped) StopActionTravel();
    }

    private void StopActionTravel()
    {
        if (movementController == null) return;
        movementController.SetActionMovement(false);
        movementController.SetCombatMovementLocked(true);
    }

    private void RestorePlayerControl()
    {
        if (movementController != null)
        {
            movementController.SetActionMovement(false);
            movementController.SetCombatMovementLocked(false);
        }
        if (locomotionDriver != null)
        {
            locomotionDriver.enabled = true;
            locomotionDriver.ResumeFromCombat(returnToLocomotionBlend);
        }
        if (stabilizeActionVisual)
        {
            recoverActionVisual = true;
            actionVisualRecoveryAfter = Time.time + Mathf.Max(.18f, returnToLocomotionBlend + .08f);
        }
    }

    private void BeginActionVisualStabilization()
    {
        if (stabilizeActionVisual) ResetActionVisualImmediately();
        actionVisualRoot = transform.Find("CharacterVisual");
        actionMotionReference = FindMotionReference();
        if (actionVisualRoot == null || actionMotionReference == null)
        {
            stabilizeActionVisual = false;
            return;
        }

        actionReferenceLocalPosition = transform.InverseTransformPoint(actionMotionReference.position);
        Vector3 baseLocal = actionVisualRoot.localPosition;
        actionVisualBaseLocalXZ = new Vector2(baseLocal.x, baseLocal.z);
        stabilizeActionVisual = true;
        recoverActionVisual = false;
    }

    private Transform FindMotionReference()
    {
        if (playerAnimator == null) return null;
        if (playerAnimator.isHuman)
        {
            try
            {
                Transform hips = playerAnimator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) return hips;
            }
            catch { }
        }

        foreach (Transform candidate in playerAnimator.GetComponentsInChildren<Transform>(true))
        {
            string normalized = candidate.name.Replace(":", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
            if (normalized == "hips" || normalized.EndsWith("hips")) return candidate;
        }
        return null;
    }

    private void ResetActionVisualImmediately()
    {
        if (actionVisualRoot != null)
        {
            Vector3 local = actionVisualRoot.localPosition;
            local.x = actionVisualBaseLocalXZ.x;
            local.z = actionVisualBaseLocalXZ.y;
            actionVisualRoot.localPosition = local;
        }
        stabilizeActionVisual = false;
        recoverActionVisual = false;
    }

    private void TryHitNearestNpc()
    {
        NPCWanderer best = IsTargetInHitRange(currentAttackTarget)
            ? currentAttackTarget
            : FindBestHitTarget();

        if (best == null) return;
        Vector3 hitDirection = best.StrikeTargetPosition - transform.position;
        hitDirection.y = 0f;
        best.HitByPlayerPunch(hitDirection.normalized, currentPunchIndex + 1, transform);
    }

    private NPCWanderer FindBestHitTarget()
    {
        Vector3 center = transform.position + Vector3.up * .9f;
        int count = Physics.OverlapSphereNonAlloc(center, hitRadius, hitBuffer, ~0, QueryTriggerInteraction.Collide);
        NPCWanderer best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            NPCWanderer npc = hit.GetComponentInParent<NPCWanderer>();
            if (npc == null || !npc.CanReceivePlayerStrike) continue;

            Vector3 toTarget = npc.StrikeTargetPosition - transform.position;
            toTarget.y = 0f;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance < .0001f || sqrDistance > hitRadius * hitRadius) continue;
            float distance = Mathf.Sqrt(sqrDistance);
            float facing = Vector3.Dot(transform.forward, toTarget / distance);
            if (facing < targetFacingThreshold) continue;

            // Prefer the NPC visually in front of the striking arm, not merely the closest
            // collider in the radial hit sphere. The movement controller then turns toward it
            // before the contact frame.
            float score = distance - facing * .55f;
            if (score >= bestScore) continue;
            bestScore = score;
            best = npc;
        }
        return best;
    }

    private bool IsTargetInHitRange(NPCWanderer target)
    {
        if (target == null || !target.CanReceivePlayerStrike) return false;
        Vector3 toTarget = target.StrikeTargetPosition - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude <= hitRadius * hitRadius;
    }

}
