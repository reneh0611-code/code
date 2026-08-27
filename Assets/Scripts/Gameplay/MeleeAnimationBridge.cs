using System.Collections;
using System.Linq;
using UnityEngine;

public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string punch1State = "Punch1";
    [SerializeField] private string punch2State = "Punch2";
    [SerializeField] private float attackCooldown = 0.42f;
    [SerializeField] private float crossFade = 0.035f;

    [Header("360 degree hit detection")]
    [SerializeField] private float hitRadius = 2.15f;
    [SerializeField, Range(0f, 1f)] private float hitMoment = 0.34f;

    private float nextAttackTime;
    private int attackIndex;

    private void Awake() => RefreshReferences();
    private void OnEnable() => RefreshReferences();

    private void RefreshReferences()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>(true);
    }

    private void Update()
    {
        // Attack is independent of movement: walking/running/standing all allow a punch.
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        if (!playerAnimator) RefreshReferences();
        attackIndex++;
        string state = attackIndex % 2 == 1 ? punch1State : punch2State;

        if (playerAnimator)
        {
            playerAnimator.applyRootMotion = false;
            int hash = Animator.StringToHash(state);
            if (playerAnimator.HasState(0, hash))
                playerAnimator.CrossFadeInFixedTime(hash, crossFade, 0, 0f);
            else
                Debug.LogWarning($"[Melee] Missing Animator state '{state}'. The click is detected, but the combat states still need to exist in the active player Animator Controller.", playerAnimator);
        }

        yield return new WaitForSeconds(Mathf.Max(0.02f, attackCooldown * hitMoment));
        TryHitNearestNpc360();
    }

    private void TryHitNearestNpc360()
    {
        // Deliberately 360 degrees: if the player is beside or behind an NPC it can still be hit.
        // We choose the closest NPC in physical reach rather than requiring it to be in front of the camera.
        Vector3 center = transform.position + Vector3.up * 0.9f;
        Collider[] hits = Physics.OverlapSphere(center, hitRadius, ~0, QueryTriggerInteraction.Ignore);

        Animator npc = hits
            .Where(c => c && !c.transform.IsChildOf(transform))
            .Select(c => c.GetComponentInChildren<Animator>(true) ?? c.GetComponentInParent<Animator>())
            .Where(a => a && a != playerAnimator && IsNpc(a.transform))
            .OrderBy(a => HorizontalDistanceSquared(transform.position, a.transform.position))
            .FirstOrDefault();

        if (npc) PlayNpcHit(npc, transform.position);
    }

    private static bool IsNpc(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.GetComponent<CheatOnYourDayOnes.World.NPCWanderer>() != null) return true;
            if (current.name.StartsWith("AmbientNPC_")) return true;
            if (current.name == "Generated_NPCs") return true;
            current = current.parent;
        }
        return false;
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return (a - b).sqrMagnitude;
    }

    public static void PlayNpcHit(Animator npcAnimator, Vector3 attackerPosition)
    {
        if (!npcAnimator) return;
        NpcMeleeReaction reaction = npcAnimator.GetComponent<NpcMeleeReaction>();
        if (!reaction) reaction = npcAnimator.gameObject.AddComponent<NpcMeleeReaction>();
        reaction.React(attackerPosition);
    }
}

public class NpcMeleeReaction : MonoBehaviour
{
    [SerializeField] private string hit1State = "Hit1";
    [SerializeField] private string hit2State = "Hit2";
    [SerializeField] private float reactionLock = 0.55f;
    [SerializeField] private float fleeSpeed = 4.2f;
    [SerializeField] private float fleeSeconds = 2.8f;

    private Animator animator;
    private int hitIndex;
    private Coroutine routine;

    private void Awake() => animator = GetComponent<Animator>();

    public void React(Vector3 attackerPosition)
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReactRoutine(attackerPosition));
    }

    private IEnumerator ReactRoutine(Vector3 attackerPosition)
    {
        hitIndex++;
        string state = hitIndex % 2 == 1 ? hit1State : hit2State;
        animator.applyRootMotion = false;
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, 0.035f, 0, 0f);

        yield return new WaitForSeconds(reactionLock);

        float until = Time.time + fleeSeconds;
        while (Time.time < until)
        {
            Vector3 away = transform.position - attackerPosition;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f)
            {
                away.Normalize();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * 9f);
                CharacterController cc = GetComponentInParent<CharacterController>();
                if (cc != null && cc.enabled) cc.Move(away * fleeSpeed * Time.deltaTime);
                else transform.position += away * fleeSpeed * Time.deltaTime;
            }
            yield return null;
        }
        routine = null;
    }
}
