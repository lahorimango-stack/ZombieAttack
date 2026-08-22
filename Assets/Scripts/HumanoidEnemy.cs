using System;
using UnityEngine;

public abstract class HumanoidEnemy : MonoBehaviour, IHittable
{
    [Header("--- Health & Defense ---")]
    [SerializeField] protected int maxHealth = 100;
    public int Health { get; protected set; }

    [Header("--- References ---")]
    [SerializeField] protected RagdollController ragdollController;
    [SerializeField] protected TimeScalable timeScalable;

    public bool IsDead => Health <= 0;
    protected abstract int DamagePerHit { get; }

    public event Action<IHittable> OnDespawn;

    protected virtual void Awake()
    {
        Health = maxHealth;

        if (ragdollController == null)
            ragdollController = GetComponent<RagdollController>();

        if (timeScalable == null)
            timeScalable = GetComponent<TimeScalable>();

        if (ragdollController != null)
        {
            ragdollController.OnGetHit += (part) => OnGetHit();
        }
    }

    // FIX: virtual Update method taake StandardEnemy isay override kar sake
    protected virtual void Update()
    {
    }

    public virtual void OnHit(IHitter hitter, Vector3 position)
    {
        if (IsDead) return;
        OnGetHit();
    }

    protected virtual void OnGetHit()
    {
    }

    public virtual void OnSpawn()
    {
        Health = maxHealth;
        if (ragdollController != null)
        {
            ragdollController.ResetForSpawn();
        }
    }
}