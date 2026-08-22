using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMover : MonoBehaviour, IHitter
{
    [Header("--- 1. Movement & Damage ---")]
    public float speed = 35f;
    public int damageValue = 35;
    public float impactForce = 15f;
    public float lifeTimeAfterLaunch = 3f;
    public bool isShoot = false;

    public int Damage => damageValue;
    public Vector3 Direction => transform.forward;
    public float Force => impactForce;

    [Header("--- 2. VFX Settings ---")]
    public GameObject flash;
    public GameObject hit;
    public float hitOffset = 0f;

    private Rigidbody rb;
    private Collider[] allColliders;
    private ParticleSystem[] allChildParticles;
    private bool hasCollided = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        allColliders = GetComponentsInChildren<Collider>(true);
        allChildParticles = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void OnSpawned()
    {
        isShoot = false;
        hasCollided = false;
        SetAllCollidersState(false);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        foreach (var ps in allChildParticles)
        {
            if (ps != null) { ps.Clear(true); ps.Play(true); }
        }

        if (flash != null)
        {
            var flashInstance = Instantiate(flash, transform.position, Quaternion.identity);
            flashInstance.transform.forward = transform.forward;
            var flashPs = flashInstance.GetComponent<ParticleSystem>();
            Destroy(flashInstance, flashPs != null ? flashPs.main.duration : 1f);
        }
    }

    void FixedUpdate()
    {
        if (speed != 0 && isShoot && rb != null && !hasCollided)
        {
            rb.linearVelocity = transform.forward * speed;
        }
    }

    public void Launch()
    {
        if (isShoot) return;
        isShoot = true;
        SetAllCollidersState(true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }

        Destroy(gameObject, lifeTimeAfterLaunch);
    }

    private void SetAllCollidersState(bool state)
    {
        if (allColliders == null) return;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null) allColliders[i].enabled = state;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isShoot || hasCollided) return;
        if (collision.gameObject.GetComponent<ProjectileMover>() != null) return;

        // 1. IHittable Check (Bone ya Root dono ko detect karega)
        IHittable hittable = collision.gameObject.GetComponent<IHittable>();
        if (hittable == null)
            hittable = collision.gameObject.GetComponentInParent<IHittable>();

        if (hittable != null)
        {
            hittable.OnHit(this, collision.contacts[0].point);
            Debug.Log($"<color=yellow>[PROJECTILE HIT SUCCESS]</color> Hit: {collision.gameObject.name}");
        }

        HandleHit(collision.contacts[0].point, collision.contacts[0].normal);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isShoot || hasCollided) return;
        if (other.gameObject.GetComponent<ProjectileMover>() != null) return;

        IHittable hittable = other.GetComponent<IHittable>();
        if (hittable == null)
            hittable = other.GetComponentInParent<IHittable>();

        if (hittable != null)
        {
            hittable.OnHit(this, transform.position);
            Debug.Log($"<color=yellow>[PROJECTILE TRIGGER SUCCESS]</color> Hit: {other.gameObject.name}");
        }

        HandleHit(transform.position, -transform.forward);
    }

    private void HandleHit(Vector3 point, Vector3 normal)
    {
        hasCollided = true;
        if (rb != null) { rb.constraints = RigidbodyConstraints.FreezeAll; rb.linearVelocity = Vector3.zero; }
        speed = 0;

        if (hit != null)
        {
            var hitInstance = Instantiate(hit, point + normal * hitOffset, Quaternion.LookRotation(normal));
            var hitPs = hitInstance.GetComponent<ParticleSystem>();
            Destroy(hitInstance, hitPs != null ? hitPs.main.duration : 1.5f);
        }

        Destroy(gameObject);
    }
}