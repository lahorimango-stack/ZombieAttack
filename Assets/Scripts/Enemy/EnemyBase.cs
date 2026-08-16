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


    [Header("--- 3. Hit Movement Freeze & Knockback ---")]
    [SerializeField] protected float hitMovementFreezeDuration = 0.5f;
    [SerializeField] protected float hitKnockbackDistance = 0.4f;
    [SerializeField] protected float knockbackDuration = 0.12f;


    [Header("--- 4. Death & Ragdoll Impulse (Hips & Spine) ---")]
    [Tooltip("Marte waqt Hips/Spine par kitni backward force lagay (Urrne ke liye)")]
    [SerializeField] protected float deathKickForce = 25f;

    [Tooltip("Hawa mein toss karne ke liye upward force")]
    [SerializeField] protected float deathUpwardLift = 6f;

    [Tooltip("Marte waqt body par realistic rotational spin")]
    [SerializeField] protected float deathTorque = 12f;

    [Tooltip("Specific Main Bone (Hips/Spine/Pelvis). Khali chhorne par script auto-detect kar legi")]
    [SerializeField] protected Rigidbody mainHipsRigidbody;


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

        // AUTO-DETECT HIPS / SPINE BONE
        FindMainHipsBone();

        CacheAllMeshRenderers();

        if (debugAlwaysRagdoll)
            SetRagdollState(true);
        else
            SetRagdollState(false);
    }

    private void FindMainHipsBone()
    {
        if (mainHipsRigidbody == null && ragdollRigidbodies != null)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                if (rb.gameObject == this.gameObject) continue;

                string boneName = rb.gameObject.name.ToLower();
                if (boneName.Contains("hip") || boneName.Contains("pelvis") || boneName.Contains("spine") || boneName.Contains("root"))
                {
                    mainHipsRigidbody = rb;
                    break;
                }
            }

            // Fallback to first child bone if name doesn't match
            if (mainHipsRigidbody == null && ragdollRigidbodies.Length > 1)
            {
                mainHipsRigidbody = ragdollRigidbodies[1];
            }
        }
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

    private void ApplyHitKnockback(Vector3 hitDirection)
    {
        Vector3 pushDir = hitDirection;
        pushDir.y = 0f;

        if (pushDir == Vector3.zero)
            pushDir = -transform.forward;

        Vector3 targetPushPos = transform.position + (pushDir.normalized * hitKnockbackDistance);
        targetPushPos.y = groundFixedY;

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
    /// REQUIREMENT FIX: Direct Hips/Spine par Heavy Impulse Force
    /// </summary>
    protected virtual void Die(Vector3 hitPoint, Vector3 hitDirection)
    {
        isDead = true;

        knockbackTween?.Kill();
        transform.DOKill();
        transform.localScale = initialScale;

        GameManager.Instance?.TriggerHeavyHaptic();
        SetRagdollState(true);

        // 1. Force Calculation: Direct Backward + High Upward Lift
        Vector3 finalImpulse = (hitDirection.normalized * deathKickForce) + (Vector3.up * deathUpwardLift);

        // 2. MAIN HIPS / SPINE FORCE (Toss the entire body)
        if (mainHipsRigidbody != null)
        {
            mainHipsRigidbody.linearVelocity = Vector3.zero;
            mainHipsRigidbody.AddForce(finalImpulse, ForceMode.Impulse);
            mainHipsRigidbody.AddTorque(Random.insideUnitSphere * deathTorque, ForceMode.Impulse);
        }

        // 3. Hit Point localized impact force
        Rigidbody closestBone = GetClosestBone(hitPoint);
        if (closestBone != null && closestBone != mainHipsRigidbody)
        {
            closestBone.AddForceAtPosition(finalImpulse * 0.5f, hitPoint, ForceMode.Impulse);
        }

        Destroy(gameObject, 5f);
    }

    private Rigidbody GetClosestBone(Vector3 point)
    {
        Rigidbody closest = null;
        float minDist = Mathf.Infinity;

        foreach (var rb in ragdollRigidbodies)
        {
            if (rb.gameObject == this.gameObject) continue;
            float dist = Vector3.Distance(rb.position, point);
            if (dist < minDist)
            {
                minDist = dist;
                closest = rb;
            }
        }
        return closest;
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