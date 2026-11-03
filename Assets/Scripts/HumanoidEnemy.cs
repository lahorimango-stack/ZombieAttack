using System;
using UnityEngine;

[RequireComponent(typeof(RagdollController), typeof(AudioSource))]
public abstract class HumanoidEnemy : MonoBehaviour, IDamageable
{
    [Header("--- Base Health ---")]
    [SerializeField] protected int maxHealth = 100;
    public float Health { get; protected set; }
    public bool IsDead => Health <= 0;
    public int MaxHealth => maxHealth;
    public float HealthPercent => maxHealth > 0 ? Mathf.Clamp01(Health / maxHealth) : 0f;

    [Header("--- Reward ---")]
    [Tooltip("Is zombie ko marne par kitne coins milenge")]
    [SerializeField] protected int coinReward = 1;
    public int CoinReward => coinReward;

    /// <summary> Fallback damage jab weapon ne damage na bheja ho </summary>
    protected abstract int DamagePerHit { get; }
    protected RagdollController ragdollController;
    protected Collider mainRootCollider;
    protected AudioSource audioSource;

    public event Action<HumanoidEnemy> OnDamaged;
    /// <summary> Maut par (bomber explosion waghaira ke liye) </summary>
    public event Action<HumanoidEnemy> OnDied;

    protected void RaiseDied() => OnDied?.Invoke(this);

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

    /// <summary> Level difficulty: health ko multiply karo (spawn par) </summary>
    public void ScaleHealth(float multiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        Health = maxHealth;
    }

    public virtual void OnSpawn()
    {
        Health = maxHealth;
        if (ragdollController != null)
            ragdollController.DeactivateRagdoll();

        if (mainRootCollider != null)
            mainRootCollider.enabled = true;
    }

    /// <param name="damage">Is hit ka damage</param>
    /// <param name="hitPoint">World hit point</param>
    /// <param name="impulse">Direction * force (death ragdoll ke liye)</param>
    protected virtual void OnGetHit(float damage, Vector3 hitPoint, Vector3 impulse)
    {
        // Handled in child
    }

    /// <summary>
    /// Universal Damage Hook: hitDirection ki length = impact force
    /// </summary>
    public virtual void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsDead) return;
        OnGetHit(damageAmount > 0f ? damageAmount : DamagePerHit, hitPoint, hitDirection);
        OnDamaged?.Invoke(this);
    }
}
