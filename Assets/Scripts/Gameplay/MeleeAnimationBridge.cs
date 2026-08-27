using System.Collections;
using System.Linq;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEngine;

public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private float attackCooldown = 0.48f;
    [SerializeField] private float crossFade = 0.035f;
    [SerializeField] private float locomotionUnlockDelay = 0.44f;

    [Header("360 degree hit detection")]
    [SerializeField] private float hitRadius = 2.15f;
    [SerializeField, Range(0f, 1f)] private float hitMoment = 0.34f;

    private static readonly int Punch1Hash = Animator.StringToHash("Base Layer.Punch1");
    private static readonly int Punch2Hash = Animator.StringToHash("Base Layer.Punch2");

    private CharacterAnimationDriver locomotionDriver;
    private float nextAttackTime;
    private int attackIndex;

    private void Awake() => RefreshReferences();
    private void OnEnable() => RefreshReferences();

    private void RefreshReferences()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>(true);
        if (!locomotionDriver) locomotionDriver = GetComponent<CharacterAnimationDriver>();
    }

    private void Update()
    {
        // LEFT CLICK. Always allowed whether standing, walking or running.
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        RefreshReferences();
        attackIndex++;
        int state = attackIndex % 2 == 1 ? Punch1Hash : Punch2Hash;

        // Prevent the locomotion script from replacing Punch with Idle/Walk/Run on the next frame.
        if (locomotionDriver != null) locomotionDriver.enabled = false;

        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;
            playerAnimator.applyRootMotion = false;
            playerAnimator.speed = 1f;

            if (playerAnimator.HasState(0, state))
            {
                playerAnimator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
                Debug.Log($"[CYDOY MELEE] Playing {(attackIndex % 2 == 1 ? "Punch1" : "Punch2")}", playerAnimator);
            }
            else
            {
                Debug.LogError($"[CYDOY MELEE] Missing {(attackIndex % 2 == 1 ? "Punch1" : "Punch2")} state in {playerAnimator.runtimeAnimatorController?.name}.", playerAnimator);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.02f, attackCooldown * hitMoment));
        TryHitNearestNpc360();

        yield return new WaitForSeconds(Mathf.Max(0.02f, locomotionUnlockDelay - attackCooldown * hitMoment));
        if (locomotionDriver != null) locomotionDriver.enabled = true;
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
