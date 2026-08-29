using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardEnemy : HumanoidEnemy
{
    [System.Serializable]
    public class HitReactionData
    {
        [Tooltip("Animator mein state ka exact naam (e.g. 'Hit 0', 'Hit 1', 'Hit 2')")]
        public string stateName = "Hit 0";
        [Tooltip("Is hit animation par kitna peeche dhakka khayega")]
        public float knockbackDistance = 0.45f;
        [Tooltip("Peeche push hone ka duration")]
        public float knockbackDuration = 0.25f;
        [Tooltip("Hit khatam hone ke baad rest delay")]
        public float recoveryDelay = 0.2f;
    }

    [Header("--- Enemy Movement Settings ---")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected int _damagePerHit = 35;
    [SerializeField] protected float baseMoveSpeed = 3.5f;

    [Tooltip("Animator se exact Walk state names (e.g. 'Walk1', 'Walk2', 'Walk3')")]
    [SerializeField] protected string[] moveAnimations = { "Walk1", "Walk2", "Walk3", "Walk4", "Walk5" };

    [Header("--- Multiple Hit Reactions ---")]
    [SerializeField] protected List<HitReactionData> hitReactions = new List<HitReactionData>();

    [Header("--- Dev Testing ---")]
    [SerializeField] protected bool useDevHitReaction = false;
    [SerializeField] protected int devHitIndex = 0;

    [Header("--- Anti-Merge & Avoidance ---")]
    [SerializeField] protected float antiMergeRadius = 0.5f;
    [SerializeField] protected LayerMask enemyLayer;

    [Header("--- 3D Audio ---")]
    [SerializeField] protected AudioClip[] hitSounds;
    [SerializeField] protected AudioClip[] groanSounds;
    [SerializeField] protected float minGroanInterval = 3f;
    [SerializeField] protected float maxGroanInterval = 7f;

    [Header("--- Death Force Settings ---")]
    [SerializeField] protected float deathBackwardForce = 22f;
    [SerializeField] protected float deathUpwardLift = 7f;

    protected override int DamagePerHit => _damagePerHit;

    protected bool damageEffectPlaying = false;
    protected bool isKnockedBack = false;
    protected float knockbackTimer = 0f;
    protected float currentKnockbackDistance = 0.45f;
    protected float currentKnockbackDuration = 0.25f;
    protected Coroutine damageEffect;
    protected float actualMoveSpeed = 3.5f;
    protected string currentActiveWalkState = "Walk1";
    private float nextGroanTime = 0f;

    protected override void Awake()
    {
        base.Awake();
        if (animator == null) animator = GetComponent<Animator>();

        SetupMovementAndAnimationSpeed();
        nextGroanTime = Time.time + Random.Range(minGroanInterval, maxGroanInterval);
    }

    private void SetupMovementAndAnimationSpeed()
    {
        if (animator != null)
        {
            if (moveAnimations.Length > 0)
            {
                currentActiveWalkState = moveAnimations[Random.Range(0, moveAnimations.Length)];
                animator.Play(currentActiveWalkState, 0, Random.Range(0f, 1f)); // Random start point
            }

            float animSpeedMultiplier = Random.Range(0.9f, 1.15f);
            animator.speed = animSpeedMultiplier;
            actualMoveSpeed = baseMoveSpeed * animSpeedMultiplier;
        }
        else
        {
            actualMoveSpeed = baseMoveSpeed;
        }
    }

    protected override void Update()
    {
        float dt = Time.deltaTime;

        // 1. Knockback Movement (Anti-merge back check)
        if (isKnockedBack && !IsDead)
        {
            knockbackTimer -= dt;
            if (knockbackTimer > 0)
            {
                if (!IsOtherEnemyBlocking(Vector3.forward * 0.3f))
                {
                    float knockbackProgress = 1f - (knockbackTimer / currentKnockbackDuration);
                    float easeOut = 1f - (knockbackProgress * knockbackProgress);
                    transform.position += Vector3.forward * (currentKnockbackDistance / currentKnockbackDuration) * easeOut * dt;
                }
            }
            else
            {
                isKnockedBack = false;
            }
        }
        // 2. Walk Forward (Anti-merge front check)
        else if (!IsDead && !damageEffectPlaying)
        {
            if (!IsOtherEnemyBlocking(-Vector3.forward * 0.4f))
            {
                transform.position += transform.forward * actualMoveSpeed * dt;
            }
        }

        // 3. Level Bounds Lock
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;

        HandleGroanSounds();

        base.Update();
    }

    private bool IsOtherEnemyBlocking(Vector3 checkOffset)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + checkOffset, antiMergeRadius, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.gameObject != this.gameObject && !hit.transform.IsChildOf(this.transform))
                return true;
        }
        return false;
    }

    private void HandleGroanSounds()
    {
        if (groanSounds.Length == 0 || audioSource == null) return;

        if (Time.time >= nextGroanTime)
        {
            Play3DSound(groanSounds[Random.Range(0, groanSounds.Length)]);
            nextGroanTime = Time.time + Random.Range(minGroanInterval, maxGroanInterval);
        }
    }

    private void Play3DSound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        float masterVolume = SoundManager.Instance != null ? SoundManager.Instance.sfxVolume : 1f;
        bool isMuted = SoundManager.Instance != null && SoundManager.Instance.isSfxMuted;

        if (!isMuted)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip, masterVolume);
        }
    }

    protected override void OnGetHit()
    {
        if (IsDead) return;

        Health -= DamagePerHit;

        if (hitSounds.Length > 0)
            Play3DSound(hitSounds[Random.Range(0, hitSounds.Length)]);
        else
            SoundManager.Instance?.PlayHitSound();

        GameManager.Instance?.TriggerLightHaptic();

        // 1. Death Condition
        if (IsDead)
        {
            damageEffectPlaying = false;
            isKnockedBack = false;
            if (damageEffect != null) StopCoroutine(damageEffect);

            LevelManager.Instance?.OnEnemyKilled(gameObject);
            GameManager.Instance?.TriggerHeavyHaptic();

            if (ragdollController != null)
            {
                ragdollController.ActivateRagdoll();
                Vector3 launchImpulse = new Vector3(0f, deathUpwardLift, deathBackwardForce);
                ragdollController.ApplyRagdollForce(launchImpulse);
            }

            StartCoroutine(DisableObjectAfterDelay(4f));
        }
        // 2. Hit Reaction
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

    /// <summary>
    /// REQUIREMENT FIX: Direct State CrossFade + Auto Return to Walk
    /// </summary>
    protected IEnumerator DamageEffect()
    {
        damageEffectPlaying = true;
        isKnockedBack = true;

        HitReactionData hitData = null;
        if (hitReactions.Count > 0)
        {
            int index = useDevHitReaction ? Mathf.Clamp(devHitIndex, 0, hitReactions.Count - 1) : Random.Range(0, hitReactions.Count);
            hitData = hitReactions[index];
        }

        string hitState = hitData != null ? hitData.stateName : "Hit 0";
        currentKnockbackDistance = hitData != null ? hitData.knockbackDistance : 0.45f;
        currentKnockbackDuration = hitData != null ? hitData.knockbackDuration : 0.25f;
        float recoveryDelay = hitData != null ? hitData.recoveryDelay : 0.2f;

        knockbackTimer = currentKnockbackDuration;

        // DIRECT PLAY: Arrow/Transition ke baghair direct Hit Animation chalayega
        if (animator != null && animator.enabled && !string.IsNullOrEmpty(hitState))
        {
            animator.CrossFade(hitState, 0.05f, 0, 0f);
        }

        // Wait for knockback and reaction time
        yield return new WaitForSeconds(currentKnockbackDuration + recoveryDelay);

        // AUTO RETURN: Hit khatam hone par wapis Walk animation par smooth shift hoga
        if (!IsDead && animator != null && animator.enabled)
        {
            animator.CrossFade(currentActiveWalkState, 0.15f);
        }

        damageEffectPlaying = false;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        damageEffectPlaying = false;
        isKnockedBack = false;
        knockbackTimer = 0f;
        SetupMovementAndAnimationSpeed();
    }
}