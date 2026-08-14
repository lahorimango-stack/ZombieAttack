using UnityEngine;

public class WeaponProjectile : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    [Tooltip("Finger UP hone par knife kitni tez aage shoot hogi")]
    [SerializeField] private float launchSpeed = 40f;

    [Tooltip("Shoot hone ke kitne seconds baad destroy hoga")]
    [SerializeField] private float lifeTime = 2.5f;

    [Header("--- Visual & Trail (Optional) ---")]
    [Tooltip("Knife ke peeche lagne wala Trail VFX jo shoot hone par on hoga")]
    [SerializeField] private GameObject trailVFX;

    private bool isFlying = false;

    void Awake()
    {
        isFlying = false;

        // Spawn ke waqt Trail band rahega
        if (trailVFX != null)
            trailVFX.SetActive(false);
    }

    void Update()
    {
        if (isFlying)
        {
            // Seedha forward direction mein shoot hoga
            transform.position += transform.forward * launchSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Finger Release par call hota hai: Knife aage shoot hogi
    /// </summary>
    public void Launch()
    {
        if (isFlying) return;
        isFlying = true;

        GetComponent<ProjectileMover>().isShoot = true;

        // Shoot hote hi cool trail VFX active ho jayega
        if (trailVFX != null)
            trailVFX.SetActive(true);

        Destroy(gameObject, lifeTime);
    }

    public void ResetState()
    {
        isFlying = false;
        if (trailVFX != null)
            trailVFX.SetActive(false);
    }
}