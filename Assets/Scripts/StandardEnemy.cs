using System.Collections;
using UnityEngine;

public class StandardEnemy : HumanoidEnemy
{
    [Header("--- Enemy Movement Settings ---")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected int _damagePerHit = 35;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float hitMoveSpeed = 4f;

    [Header("--- Death Force Settings ---")]
    [SerializeField] protected float deathBackwardForce = 20f;
    [SerializeField] protected float deathUpwardLift = 6f;

    protected override int DamagePerHit => _damagePerHit;

    protected bool damageEffectPlaying = false;
    protected Coroutine damageEffect;
    protected float speedMultiplier = 1f;

    protected override void Awake()
    {
        base.Awake();
        if (animator == null) animator = GetComponent<Animator>();
    }

    protected override void Update()
    {
        float dt = timeScalable != null ? timeScalable.DeltaTime : Time.deltaTime;

        if (!IsDead && !damageEffectPlaying)
        {
            transform.position += transform.forward * moveSpeed * speedMultiplier * dt;
        }
        else if (!IsDead && damageEffectPlaying)
        {
            transform.position -= transform.forward * hitMoveSpeed * dt;
        }

        base.Update();
    }

    protected override void OnGetHit()
    {
        if (IsDead) return;

        Health -= DamagePerHit;

        Debug.Log($"<color=green>[ENEMY HIT]</color> {gameObject.name} took {DamagePerHit} damage! Remaining Health: <b>{Health}</b>");

        GameManager.Instance?.TriggerLightHaptic();
        SoundManager.Instance?.PlayHitSound();

        // 1. Death Condition
        if (IsDead)
        {
            damageEffectPlaying = false;
            if (damageEffect != null) StopCoroutine(damageEffect);

            Debug.Log($"<color=red>[ENEMY DIED]</color> {gameObject.name} Ragdoll Activated!");

            LevelManager.Instance?.OnEnemyKilled(gameObject);

            if (ragdollController != null)
            {
                ragdollController.ActivateRagdoll();
                Vector3 launchImpulse = new Vector3(0f, deathUpwardLift, deathBackwardForce);
                ragdollController.ApplyRagdollForce(launchImpulse);
            }

            StartCoroutine(DisableObjectAfterDelay(4f));
        }
        // 2. Alive Hit Reaction
        else
        {
            if (damageEffect != null) StopCoroutine(damageEffect);
            damageEffect = StartCoroutine(DamageEffect());
        }
    }

    private IEnumerator DisableObjectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    protected IEnumerator DamageEffect()
    {
        damageEffectPlaying = true;

        if (animator != null && animator.enabled)
        {
            animator.SetTrigger("Hit");
            yield return new WaitForAnimatorState(animator, "Hit");
        }
        else
        {
            yield return new WaitForSeconds(0.15f);
        }

        damageEffectPlaying = false;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        speedMultiplier = 1f;
        damageEffectPlaying = false;
    }
}