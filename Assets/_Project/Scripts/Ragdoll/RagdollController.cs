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

        // Humanoid ho to asal Hips bone (naam "Hip_L" waghaira se dhoka na ho)
        if (mainHipsRigidbody == null && animator != null && animator.isHuman)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) mainHipsRigidbody = hips.GetComponent<Rigidbody>();
        }

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

        // Mobile par tez jhatke se bones joint se alag ho kar mesh lamba khinch jata tha (sar "danda" ban jata tha)
        foreach (var rb in ragdollBones)
        {
            rb.solverIterations = 16;
            rb.solverVelocityIterations = 8;
            rb.maxDepenetrationVelocity = 4f;
            rb.maxAngularVelocity = 12f;
        }
        foreach (var joint in GetComponentsInChildren<CharacterJoint>(true))
        {
            joint.enableProjection = true;
            joint.projectionDistance = 0.02f;
            joint.projectionAngle = 5f;
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
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate; // girti laash smooth dikhe
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
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                // Zinda zombie: animation bones chalati hai. Interpolation on ho to physics purani position
                // likh deta tha aur sar/gardan peeche reh kar lamba khinch jata tha
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        if (animator != null)
            animator.enabled = true;
    }

    /// <summary>
    /// Corpse ko zameen mein dhansane se pehle physics band karna
    /// </summary>
    public void FreezeForSink()
    {
        foreach (var rb in ragdollBones)
        {
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        foreach (var col in ragdollColliders)
        {
            if (col != null)
                col.enabled = false;
        }
    }

    /// <summary>
    /// Realistic hit: jis bone ko laga usay zor ka jhatka, baqi body halki si saath jaye
    /// </summary>
    public void ApplyImpactAtPoint(Vector3 impulse, Vector3 point, float bodyShare = 0.3f)
    {
        Rigidbody nearest = null;
        float best = float.MaxValue;
        foreach (var rb in ragdollBones)
        {
            if (rb == null) continue;
            float d = (rb.worldCenterOfMass - point).sqrMagnitude;
            if (d < best) { best = d; nearest = rb; }
        }

        // Had: poori body max ~9 m/s, lagne wali bone ko sirf thoda extra (zyada se joint toot kar stretch hota hai)
        Vector3 body = Vector3.ClampMagnitude(impulse * bodyShare, 9f);
        foreach (var rb in ragdollBones)
        {
            if (rb == null) continue;
            rb.linearVelocity = body;
            rb.angularVelocity = Random.insideUnitSphere * 2f;
        }

        if (nearest != null)
            nearest.AddForce(Vector3.ClampMagnitude(impulse * 0.35f, 6f), ForceMode.VelocityChange);
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