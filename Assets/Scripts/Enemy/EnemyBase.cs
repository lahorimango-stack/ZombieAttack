using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("--- 1. Dev / Debug Testing ---")]
    [Tooltip("DEV CHECK: Ise tick karne par Zombie start hote hi permanent Ragdoll ban jayega")]
    [SerializeField] protected bool debugAlwaysRagdoll = false;


    [Header("--- 2. Base Stats ---")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 3.5f;
    protected float currentHealth;


    [Header("--- 3. Z-Axis Hit Knockback ---")]
    [Tooltip("Hit lagne par zombie kitne seconds tak rukaa rahega")]
    [SerializeField] protected float hitMovementFreezeDuration = 0.5f;

    [Tooltip("Knife lagne par zombie Z-Axis par kitne units peeche dhakka khayega")]
    [SerializeField] protected float hitKnockbackDistance = 0.4f;

    [Tooltip("Peeche push hone ka time (seconds)")]
    [SerializeField] protected float knockbackDuration = 0.12f;


    [Header("--- 4. Z-Axis Death Ragdoll Impulse ---")]
    [Tooltip("Marte waqt Z-Axis par kitni backward force lagay (Seedha peeche urrne ke liye)")]
    [SerializeField] protected float deathKickForce = 20f;

    [Tooltip("Hawa mein kitna uncha toss hoga (Upward Arc)")]
    [SerializeField] protected float deathUpwardLift = 7f;

    [Tooltip("Hawa mein urrte waqt realistic tumble")]
    [SerializeField] protected float deathTorque = 5f;


    [Header("--- 5. Visual & Mesh Flash ---")]
    [SerializeField] protected Color damageFlashColor = Color.white;
    [SerializeField] protected float flashDuration = 0.08f;
    [SerializeField] protected string hitTriggerName = "Hit";


    // References
    public Animator animator;
    protected Collider mainCollider;
    protected Rigidbody rootRigidbody;
    protected Rigidbody[] ragdollRigidbodies;
    protected Collider[] ragdollColliders;

    private Renderer[] allBodyRenderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    protected bool isDead = false;

    private float resumeMovementTime = 0f;
    private Vector3 initialScale;
    private float groundFixedY;
    private Quaternion defaultRotation;
    private Tween knockbackTween;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        mainCollider = GetComponent<Collider>();
        rootRigidbody = GetComponent<Rigidbody>();

        initialScale = transform.localScale;
        groundFixedY = transform.position.y;
        defaultRotation = transform.rotation;

        if (rootRigidbody != null)
        {
            rootRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        CacheAllMeshRenderers();

        if (debugAlwaysRagdoll)
            SetRagdollState(true);
        else
            SetRagdollState(false);
    }

    private void CacheAllMeshRenderers()
    {
        allBodyRenderers = GetComponentsInChildren<Renderer>(true);
        originalColors.Clear();

        foreach (var rend in allBodyRenderers)
        {
            if (rend != null)
            {
                Material[] mats = rend.materials;
                Color[] colors = new Color[mats.Length];

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i].HasProperty("_Color"))
                        colors[i] = mats[i].color;
                    else if (mats[i].HasProperty("_BaseColor"))
                        colors[i] = mats[i].GetColor("_BaseColor");
                }
                originalColors[rend] = colors;
            }
        }
    }

    protected virtual void Update()
    {
        if (isDead || debugAlwaysRagdoll) return;

        if (Time.time >= resumeMovementTime)
        {
            MoveTowardsTarget();
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, 10f * Time.deltaTime);
    }

    protected virtual void MoveTowardsTarget()
    {
        Vector3 moveDir = transform.forward;
        moveDir.y = 0f;

        transform.position += moveDir.normalized * moveSpeed * Time.deltaTime;

        Vector3 lockedPos = transform.position;
        lockedPos.y = groundFixedY;
        transform.position = lockedPos;
    }

    public virtual void SetRagdollState(bool isRagdollActive)
    {
        if (animator != null)
            animator.enabled = !isRagdollActive;

        if (mainCollider != null)
            mainCollider.enabled = !isRagdollActive;

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = !isRagdollActive;
            if (isRagdollActive)
                rootRigidbody.constraints = RigidbodyConstraints.None;
        }

        if (ragdollRigidbodies != null)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                if (rb.gameObject != this.gameObject)
                {
                    rb.isKinematic = !isRagdollActive;
                }
            }
        }

        if (ragdollColliders != null)
        {
            foreach (var col in ragdollColliders)
            {
                if (col != mainCollider)
                {
                    col.enabled = isRagdollActive;
                }
            }
        }
    }

    public virtual void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damageAmount;

        resumeMovementTime = Time.time + hitMovementFreezeDuration;
        StartCoroutine(FlashDamageMeshRoutine());

        transform.DOKill(false);
        transform.localScale = initialScale;
        transform.DOPunchScale(initialScale * 0.08f, 0.12f, 1, 0.5f)
                 .OnComplete(() => transform.localScale = initialScale);

        // REQUIREMENT: Strict Z-Axis Knockback
        ApplyHitKnockback(hitDirection);

        GameManager.Instance?.TriggerLightHaptic();
        SoundManager.Instance?.PlayHitSound();

        if (currentHealth > 0)
        {
            OnHitReaction(hitPoint, hitDirection);
        }
        else
        {
            Die(hitPoint, hitDirection);
        }
    }

    /// <summary>
    /// REQUIREMENT: Force hamesha seedha Z-Axis par lagegi (No sideways drift)
    /// </summary>
    private void ApplyHitKnockback(Vector3 hitDirection)
    {
        // Z-Direction check (Hamesha seedha Z+ ya Z- par push hoga)
        float zSign = hitDirection.z >= 0 ? 1f : -1f;
        Vector3 pushDir = new Vector3(0f, 0f, zSign);

        Vector3 targetPushPos = transform.position + (pushDir * hitKnockbackDistance);
        targetPushPos.y = groundFixedY; // Ground protection

        knockbackTween?.Kill();
        knockbackTween = transform.DOMove(targetPushPos, knockbackDuration).SetEase(Ease.OutQuad);
    }

    protected virtual void OnHitReaction(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (animator != null && !string.IsNullOrEmpty(hitTriggerName))
        {
            animator.SetTrigger(hitTriggerName);
        }
    }

    /// <summary>
    /// REQUIREMENT: Death Force Strictly Z-Axis Backward + Y-Axis Lift
    /// </summary>
    protected virtual void Die(Vector3 hitPoint, Vector3 hitDirection)
    {
        isDead = true;

        knockbackTween?.Kill();
        transform.DOKill();
        transform.localScale = initialScale;

        GameManager.Instance?.TriggerHeavyHaptic();
        SetRagdollState(true);

        // Z-Axis strict flight calculation (X = 0 taake left/right na jaye)
        float zSign = hitDirection.z >= 0 ? 1f : -1f;
        Vector3 zLaunchVelocity = new Vector3(0f, deathUpwardLift, zSign * deathKickForce);

        // Saare ragdoll bones par Z-Axis velocity apply karein
        if (ragdollRigidbodies != null)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                if (rb != null && rb.gameObject != this.gameObject)
                {
                    rb.linearVelocity = zLaunchVelocity; // Seedha Z par launch
                    rb.angularVelocity = Random.insideUnitSphere * deathTorque;
                }
            }
        }

        Destroy(gameObject, 5f);
    }

    private IEnumerator FlashDamageMeshRoutine()
    {
        foreach (var rend in allBodyRenderers)
        {
            if (rend != null)
            {
                foreach (var mat in rend.materials)
                {
                    if (mat.HasProperty("_Color"))
                        mat.color = damageFlashColor;
                    else if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", damageFlashColor);
                }
            }
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var rend in allBodyRenderers)
        {
            if (rend != null && originalColors.ContainsKey(rend))
            {
                Color[] colors = originalColors[rend];
                Material[] mats = rend.materials;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (i < colors.Length)
                    {
                        if (mats[i].HasProperty("_Color"))
                            mats[i].color = colors[i];
                        else if (mats[i].HasProperty("_BaseColor"))
                            mats[i].SetColor("_BaseColor", colors[i]);
                    }
                }
            }
        }
    }

    protected virtual void OnValidate()
    {
        if (Application.isPlaying && ragdollRigidbodies != null)
        {
            SetRagdollState(debugAlwaysRagdoll);
        }
    }
}