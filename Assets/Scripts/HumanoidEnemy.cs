using UnityEngine;

[RequireComponent(typeof(RagdollController), typeof(AudioSource))]
public abstract class HumanoidEnemy : MonoBehaviour, IDamageable
{
    [Header("--- Base Health ---")]
    [SerializeField] protected int maxHealth = 100;
    public int Health { get; protected set; }
    public bool IsDead => Health <= 0;

    protected abstract int DamagePerHit { get; }
    protected RagdollController ragdollController;
    protected Collider mainRootCollider;
    protected AudioSource audioSource;

    [HideInInspector] public float minX = -3.5f;
    [HideInInspector] public float maxX = 3.5f;

    protected virtual void Awake()
    {
        Health = maxHealth;
        ragdollController = GetComponent<RagdollController>();
        mainRootCollider = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
    }

    protected virtual void Update()
    {
        // Handled in child
    }

    public virtual void OnSpawn()
    {
        Health = maxHealth;
        if (ragdollController != null)
            ragdollController.DeactivateRagdoll();

        if (mainRootCollider != null)
            mainRootCollider.enabled = true;
    }

    protected virtual void OnGetHit()
    {
        // Handled in child
    }

    /// <summary>
    /// Universal Damage Hook for Knife collisions
    /// </summary>
    public virtual void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsDead) return;
        OnGetHit();
    }
}