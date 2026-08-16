using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    [Header("--- 1. Movement & Damage ---")]
    [Tooltip("Shoot hone ke baad aage bhaagne ki speed")]
    public float speed = 35f;

    [Tooltip("Dushman ko kitna damage lagay ga")]
    public float damageAmount = 50f;

    [Tooltip("Shoot hone ke kitne seconds baad despawn ho kar pool mein wapis jaye")]
    public float lifeTimeAfterLaunch = 3f;
    public bool isShoot = false;


    [Header("--- 2. VFX Settings ---")]
    [Tooltip("Spawn hote hi jo Flash VFX chalega")]
    public GameObject flash;

    [Tooltip("Takrane par jo Hit VFX spawn hoga")]
    public GameObject hit;
    public float hitOffset = 0f;
    public bool UseFirePointRotation;
    public Vector3 rotationOffset = Vector3.zero;


    [HideInInspector] public int weaponIndex = 0;
    public Action<ProjectileMover> OnReturnToPoolCallback;

    private Rigidbody rb;
    private Collider[] allColliders; // Root + Child saare Sphere/Box colliders
    private ParticleSystem[] allChildParticles;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
    private bool hasCollided = false;
    private Coroutine autoDespawnRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // CRITICAL FIX 1: Saare child colliders (Sphere, Box, Capsule) dhoond lein
        allColliders = GetComponentsInChildren<Collider>(true);
        allChildParticles = GetComponentsInChildren<ParticleSystem>(true);
    }

    /// <summary>
    /// Pool se nikalte waqt call hota hai
    /// </summary>
    public void OnSpawned()
    {
        isShoot = false;
        hasCollided = false;

        // 1. Drag ke waqt SAARE Colliders OFF rahenge (No self-collision)
        SetAllCollidersState(false);

        // 2. Physics freeze
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
        }

        // 3. Particles reset
        foreach (var ps in allChildParticles)
        {
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        // 4. Spawn Flash VFX
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

    /// <summary>
    /// Finger UP hone par call hoga
    /// </summary>
    public void Launch()
    {
        if (isShoot) return;
        isShoot = true;

        // CRITICAL FIX 2: Shoot hote hi SAARE Sphere Colliders 100% ON ho jayenge
        SetAllCollidersState(true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }

        if (autoDespawnRoutine != null) StopCoroutine(autoDespawnRoutine);
        autoDespawnRoutine = StartCoroutine(AutoDespawnTimer(lifeTimeAfterLaunch));
    }

    private void SetAllCollidersState(bool state)
    {
        if (allColliders == null) return;

        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null)
            {
                allColliders[i].enabled = state;
            }
        }
    }

    private IEnumerator AutoDespawnTimer(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    // ========================================================
    // 1. PHYSICAL / SPHERE COLLIDER COLLISION (Solid Hits)
    // ========================================================
    void OnCollisionEnter(Collision collision)
    {
        if (!isShoot || hasCollided) return;
        if (collision.gameObject.GetComponent<ProjectileMover>() != null) return;

        IDamageable damageable = collision.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damageAmount, collision.contacts[0].point, transform.forward);
            GameObject targetObj = (damageable as MonoBehaviour)?.gameObject ?? collision.gameObject;
            Debug.Log($"<color=yellow>[SPHERE COLLIDER HIT]</color> Enemy: <b>{targetObj.name}</b> ko <b>{damageAmount} Damage</b> mila!");
        }

        HandleHit(collision.contacts[0].point, collision.contacts[0].normal);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isShoot || hasCollided) return;
        if (other.gameObject.GetComponent<ProjectileMover>() != null) return;

        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damageAmount, transform.position, transform.forward);
            GameObject targetObj = (damageable as MonoBehaviour)?.gameObject ?? other.gameObject;
            Debug.Log($"<color=yellow>[TRIGGER HIT]</color> Enemy: <b>{targetObj.name}</b> ko <b>{damageAmount} Damage</b> mila!");
        }

        HandleHit(transform.position, -transform.forward);
    }

    // ========================================================
    // 2. PARTICLE COLLISION (Particle Hits)
    // ========================================================
    void OnParticleCollision(GameObject other)
    {
        if (!isShoot || hasCollided) return;
        if (other.GetComponent<ProjectileMover>() != null) return;

        ParticleSystem ps = allChildParticles.Length > 0 ? allChildParticles[0] : null;
        int numEvents = ps != null ? ps.GetCollisionEvents(other, collisionEvents) : 0;

        Vector3 hitPoint = numEvents > 0 ? collisionEvents[0].intersection : transform.position;
        Vector3 hitNormal = numEvents > 0 ? collisionEvents[0].normal : -transform.forward;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damageAmount, hitPoint, transform.forward);
            GameObject targetObj = (damageable as MonoBehaviour)?.gameObject ?? other;
            Debug.Log($"<color=cyan>[PARTICLE HIT]</color> Enemy: <b>{targetObj.name}</b> ko <b>{damageAmount} Damage</b> mila!");
        }

        HandleHit(hitPoint, hitNormal);
    }

    private void HandleHit(Vector3 point, Vector3 normal)
    {
        hasCollided = true;

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.linearVelocity = Vector3.zero;
        }
        speed = 0;

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 pos = point + normal * hitOffset;

        if (hit != null)
        {
            var hitInstance = Instantiate(hit, pos, rot);
            if (UseFirePointRotation)
            {
                hitInstance.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
            }
            else if (rotationOffset != Vector3.zero)
            {
                hitInstance.transform.rotation = Quaternion.Euler(rotationOffset);
            }
            else
            {
                hitInstance.transform.LookAt(point + normal);
            }

            var hitPs = hitInstance.GetComponent<ParticleSystem>();
            Destroy(hitInstance, hitPs != null ? hitPs.main.duration : 1.5f);
        }

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (autoDespawnRoutine != null)
        {
            StopCoroutine(autoDespawnRoutine);
            autoDespawnRoutine = null;
        }

        gameObject.SetActive(false);
        OnReturnToPoolCallback?.Invoke(this);
    }
}