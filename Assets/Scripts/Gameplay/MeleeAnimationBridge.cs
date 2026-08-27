using System.Collections;
using System.Linq;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEngine;

public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private float minimumAttackGap = 0.18f;
    [SerializeField] private float crossFade = 0.025f;
    [SerializeField, Range(0.75f, 1f)] private float finishNormalizedTime = 0.97f;
    [SerializeField] private float maxAttackAnimationSeconds = 3f;

    [Header("360 degree hit detection")]
    [SerializeField] private float hitRadius = 2.15f;
    [SerializeField, Range(0.05f, 0.9f)] private float hitMomentNormalized = 0.34f;

    private static readonly int Punch1Hash = Animator.StringToHash("Base Layer.Punch1");
    private static readonly int Punch2Hash = Animator.StringToHash("Base Layer.Punch2");

    private CharacterAnimationDriver locomotionDriver;
    private NetworkPlayerController movementController;
    private float nextAttackTime;
    private int attackIndex;
    private bool attackRunning;

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
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !attackRunning)
        {
            nextAttackTime = Time.time + minimumAttackGap;
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        attackRunning = true;
        RefreshReferences();
        attackIndex++;
        int state = attackIndex % 2 == 1 ? Punch1Hash : Punch2Hash;
        string stateName = attackIndex % 2 == 1 ? "Punch1" : "Punch2";

        // GTA-style commitment: once the punch starts, WASD/sprint/jump are locked
        // until the actual animation has finished.
        if (movementController != null) movementController.SetCombatMovementLocked(true);
        if (locomotionDriver != null) locomotionDriver.enabled = false;

        bool stateExists = playerAnimator != null && playerAnimator.HasState(0, state);
        if (stateExists)
        {
            playerAnimator.enabled = true;
            playerAnimator.applyRootMotion = false;
            playerAnimator.speed = 1f;
            playerAnimator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
            Debug.Log($"[CYDOY MELEE] Playing complete {stateName}", playerAnimator);
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
                    TryHitNearestNpc360();
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

        if (!hitApplied) TryHitNearestNpc360();

        if (locomotionDriver != null) locomotionDriver.enabled = true;
        if (movementController != null) movementController.SetCombatMovementLocked(false);
        attackRunning = false;
    }

    private void OnDisable()
    {
        // Safety: never leave the player permanently frozen if this component gets disabled.
        if (movementController != null) movementController.SetCombatMovementLocked(false);
        if (locomotionDriver != null) locomotionDriver.enabled = true;
        attackRunning = false;
    }

    private void TryHitNearestNpc360()
    {
        Vector3 center = transform.position + Vector3.up * 0.9f;
        Collider[] hits = Physics.OverlapSphere(center, hitRadius, ~0, QueryTriggerInteraction.Collide);

        NPCWanderer npc = hits
            .Where(c => c != null && !c.transform.IsChildOf(transform))
            .Select(c => c.GetComponentInParent<NPCWanderer>() ?? c.GetComponentInChildren<NPCWanderer>(true))
            .Where(n => n != null && !n.IsDown)
            .Distinct()
            .OrderBy(n => HorizontalDistanceSquared(transform.position, n.transform.position))
            .FirstOrDefault();

        if (npc == null) return;

        Vector3 direction = npc.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;

        npc.HitByPlayerPunch(direction.normalized, attackIndex % 2 == 1 ? 1 : 2, transform);
        Debug.Log($"[CYDOY MELEE] Hit {npc.name} with {(attackIndex % 2 == 1 ? "Punch1" : "Punch2")}", npc);
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }
}
