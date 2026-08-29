using System.Collections.Generic;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [Header("--- Bone References ---")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody mainHipsRigidbody;

    private List<Rigidbody> ragdollBones = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();
    private bool isRagdollActive = false;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        SetupRagdoll();
    }

    public void SetupRagdoll()
    {
        ragdollBones.Clear();
        ragdollColliders.Clear();

        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        Collider[] cols = GetComponentsInChildren<Collider>();

        foreach (var rb in rbs)
        {
            if (rb.gameObject != this.gameObject)
            {
                ragdollBones.Add(rb);

                if (mainHipsRigidbody == null && (rb.name.ToLower().Contains("hip") || rb.name.ToLower().Contains("pelvis") || rb.name.ToLower().Contains("spine")))
                {
                    mainHipsRigidbody = rb;
                }
            }
        }

        foreach (var c in cols)
        {
            if (c.gameObject != this.gameObject)
            {
                ragdollColliders.Add(c);
            }
        }

        DeactivateRagdoll();
    }

    public void ActivateRagdoll()
    {
        isRagdollActive = true;

        if (animator != null)
            animator.enabled = false;

        foreach (var rb in ragdollBones)
        {
            if (rb != null)
                rb.isKinematic = false;
        }

        foreach (var col in ragdollColliders)
        {
            if (col != null)
                col.enabled = true;
        }
    }

    public void DeactivateRagdoll()
    {
        isRagdollActive = false;

        foreach (var rb in ragdollBones)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        if (animator != null)
            animator.enabled = true;
    }

    /// <summary>
    /// Z-Axis Launch Velocity across bones
    /// </summary>
    public void ApplyRagdollForce(Vector3 launchVelocity)
    {
        foreach (var rb in ragdollBones)
        {
            if (rb != null)
            {
                rb.linearVelocity = launchVelocity;
                rb.angularVelocity = Random.insideUnitSphere * 5f;
            }
        }
    }
}