using System;
using UnityEngine;

public class RagdollBodyPart : MonoBehaviour, IHittable
{
    public Action<RagdollBodyPart> OnGetHit;
    public Collider Collider => _collider;
    public RagdollController RagdollController => _controller;

    private Rigidbody rb;
    private Collider _collider;
    private RagdollController _controller;
    private Action<IHittable> onDespawn;

    // NEW: cached bind pose, relative to the parent bone
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

    public event Action<IHittable> OnDespawn { add => onDespawn += value; remove => onDespawn -= value; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    public void Init(RagdollController controller) => _controller = controller;

    public void CacheInitialLocalPose()
    {
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
    }

    public void RestoreInitialLocalPose()
    {
        transform.localPosition = initialLocalPos;
        transform.localRotation = initialLocalRot;
    }

    public void ActivateRagdoll() => rb.isKinematic = false;

    public void DeactivateRagdoll()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void OnHit(IHitter hitter, Vector3 position) => OnGetHit?.Invoke(this);

    public void ApplyForce(Vector3 force) => rb.AddForce(force, ForceMode.Impulse);

    public void Despawn() => onDespawn?.Invoke(this);

    public void IgnoreCollision(Collider collider) => Physics.IgnoreCollision(_collider, collider);
}