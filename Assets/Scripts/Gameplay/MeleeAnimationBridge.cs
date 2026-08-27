using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime melee animation bridge. It intentionally does not modify the map/terrain.
/// Add this to the player (or let your bootstrap add it) and assign the player Animator.
/// Left mouse alternates Punch1/Punch2. NPC hit reactions can be triggered with PlayNpcHit.
/// </summary>
public class MeleeAnimationBridge : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string punch1State = "Punch1";
    [SerializeField] private string punch2State = "Punch2";
    [SerializeField] private float attackCooldown = 0.48f;
    [SerializeField] private float crossFade = 0.06f;

    [Header("Hit detection")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float attackRange = 2.1f;
    [SerializeField] private float attackRadius = 0.8f;
    [SerializeField, Range(0f, 1f)] private float hitMoment = 0.38f;

    private bool attacking;
    private int attackIndex;

    private void Awake()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>(true);
        if (!attackOrigin) attackOrigin = transform;
    }

    private void Update()
    {
        // 0 = LEFT mouse button.
        if (Input.GetMouseButtonDown(0) && !attacking)
            StartCoroutine(Attack());
    }

    private IEnumerator Attack()
    {
        attacking = true;
        attackIndex++;
        string state = attackIndex % 2 == 1 ? punch1State : punch2State;

        if (playerAnimator)
        {
            playerAnimator.applyRootMotion = false;
            int hash = Animator.StringToHash(state);
            if (playerAnimator.HasState(0, hash))
                playerAnimator.CrossFadeInFixedTime(hash, crossFade, 0, 0f);
            else
                Debug.LogWarning($"[Melee] Player Animator has no state '{state}'. Run the melee animator installer once after pulling.", playerAnimator);
        }

        yield return new WaitForSeconds(Mathf.Max(0.03f, attackCooldown * hitMoment));
        TryHitNpc();
        yield return new WaitForSeconds(Mathf.Max(0.03f, attackCooldown * (1f - hitMoment)));
        attacking = false;
    }

    private void TryHitNpc()
    {
        Vector3 origin = attackOrigin.position + Vector3.up * 1.05f;
        Vector3 center = origin + transform.forward * attackRange * 0.55f;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, ~0, QueryTriggerInteraction.Ignore);

        Animator npc = hits
            .Where(c => c && !c.transform.IsChildOf(transform))
            .Select(c => c.GetComponentInParent<Animator>())
            .FirstOrDefault(a => a && a != playerAnimator);

        if (npc) PlayNpcHit(npc, transform.position);
    }

    public static void PlayNpcHit(Animator npcAnimator, Vector3 attackerPosition)
    {
        if (!npcAnimator) return;

        var reaction = npcAnimator.GetComponent<NpcMeleeReaction>();
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
        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(hash, 0.04f, 0, 0f);

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
                transform.position += away * fleeSpeed * Time.deltaTime;
            }
            yield return null;
        }

        routine = null;
    }
}
