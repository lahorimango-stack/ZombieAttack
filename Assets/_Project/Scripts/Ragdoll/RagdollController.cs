using System;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RagdollController[] ragdollToIgnore = new RagdollController[0];

    private RagdollBodyPart[] bodyParts;
    public Action<RagdollBodyPart> OnGetHit;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        // AUTO-ATTACH: Saare child bones jinpe Rigidbody hai unpe RagdollBodyPart khud lagayein
        Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in childRbs)
        {
            if (rb.gameObject != this.gameObject)
            {
                if (rb.GetComponent<RagdollBodyPart>() == null)
                {
                    rb.gameObject.AddComponent<RagdollBodyPart>();
                }
            }
        }

        bodyParts = GetComponentsInChildren<RagdollBodyPart>();

        foreach (var part in bodyParts)
        {
            part.OnGetHit += OnBodyPartGetHit;
            part.Init(this);
            part.CacheInitialLocalPose();
        }
    }

    private void Start()
    {
        // NULL CHECK FIX: Agar empty ho to crash na ho
        if (ragdollToIgnore != null)
        {
            foreach (var ragdoll in ragdollToIgnore)
            {
                if (ragdoll != null)
                    IgnoreCollision(ragdoll);
            }
        }

        ResetForSpawn();
    }

    public void ResetForSpawn()
    {
        if (bodyParts == null) return;

        foreach (var part in bodyParts)
        {
            part.DeactivateRagdoll();
            part.RestoreInitialLocalPose();
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void OnBodyPartGetHit(RagdollBodyPart part) => OnGetHit?.Invoke(part);

    public void ActivateRagdoll()
    {
        if (animator != null) animator.enabled = false;
        if (bodyParts == null) return;

        foreach (var part in bodyParts)
        {
            part.ActivateRagdoll();
        }
    }

    public void ApplyRagdollForce(Vector3 force)
    {
        if (bodyParts == null) return;
        foreach (var part in bodyParts)
        {
            part.ApplyForce(force);
        }
    }

    private void IgnoreCollision(RagdollController ragdoll)
    {
        Collider[] other = ragdoll.GetAllColliders();
        Collider[] my = GetAllColliders();
        for (int i = 0; i < other.Length; i++)
            for (int j = 0; j < my.Length; j++)
                Physics.IgnoreCollision(other[i], my[j]);
    }

    public Collider[] GetAllColliders()
    {
        if (bodyParts == null) return new Collider[0];
        var colliders = new Collider[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
            colliders[i] = bodyParts[i].Collider;
        return colliders;
    }
}