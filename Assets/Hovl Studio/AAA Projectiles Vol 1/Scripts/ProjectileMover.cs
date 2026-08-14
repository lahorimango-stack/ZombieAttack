using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    [Tooltip("Shoot hone ke baad aage bhaagne ki speed")]
    public float speed = 35f;

    [Tooltip("Shoot hone ke kitne seconds baad despawn ho kar pool mein wapis jaye")]
    public float lifeTimeAfterLaunch = 3f;
    public bool isShoot = false;

    [Header("--- VFX Settings ---")]
    [Tooltip("Spawn hote hi jo Flash VFX chalega")]
    public GameObject flash;

    [Tooltip("Takrane par jo Hit VFX spawn hoga")]
    public GameObject hit;
    public float hitOffset = 0f;
    public bool UseFirePointRotation;
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Takrane par jo particles/trails alag (detach) hone chahiye")]
    public GameObject[] Detached;

    [HideInInspector] public int weaponIndex = 0;

    private Rigidbody rb;
    private Collider col;
    private bool hasCollided = false;
    private Coroutine autoDespawnRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    /// <summary>
    /// Pool se nikal kar spawn hote hi call hota hai
    /// </summary>
    public void OnSpawned()
    {
        isShoot = false;
        hasCollided = false;

        // 1. CRITICAL FIX: Drag ke waqt collider band rakhein taake aapas me takra kar destroy na hon
        if (col != null)
        {
            col.enabled = false;
        }

        // 2. Physics freeze
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
        }

        // 3. Flash VFX on Spawn
        if (flash != null)
        {
            var flashInstance = Instantiate(flash, transform.position, Quaternion.identity);
            flashInstance.transform.forward = transform.forward;
            var flashPs = flashInstance.GetComponent<ParticleSystem>();
            if (flashPs != null)
            {
                Destroy(flashInstance, flashPs.main.duration);
            }
            else
            {
                Destroy(flashInstance, 1.5f);
            }
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

        // 1. Shoot hote hi Collider aur Physics enable karein
        if (col != null)
        {
            col.enabled = true;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }

        // 2. Auto-Despawn timer shuru
        if (autoDespawnRoutine != null) StopCoroutine(autoDespawnRoutine);
        autoDespawnRoutine = StartCoroutine(AutoDespawnTimer(lifeTimeAfterLaunch));
    }

    private IEnumerator AutoDespawnTimer(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    // 3. CRITICAL FIX: Safe Collision Handling
    void OnCollisionEnter(Collision collision)
    {
        // Agar shoot nahi hui ya pehle se takra chuki hai to ignore karein
        if (!isShoot || hasCollided) return;

        // Agar doosri knife se takraye to ignore karein (sirf enemy/wall par hit ho)
        if (collision.gameObject.GetComponent<ProjectileMover>() != null) return;

        HandleHit(collision.contacts[0].point, collision.contacts[0].normal);
    }

    void OnTriggerEnter(Collider other)
    {
        // Agar Trigger Collider use ho raha ho
        if (!isShoot || hasCollided) return;
        if (other.gameObject.GetComponent<ProjectileMover>() != null) return;

        HandleHit(transform.position, -transform.forward);
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
            if (hitPs != null)
            {
                Destroy(hitInstance, hitPs.main.duration);
            }
            else
            {
                Destroy(hitInstance, 1.5f);
            }
        }

        foreach (var detachedPrefab in Detached)
        {
            if (detachedPrefab != null)
            {
                detachedPrefab.transform.parent = null;
            }
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
    }
}