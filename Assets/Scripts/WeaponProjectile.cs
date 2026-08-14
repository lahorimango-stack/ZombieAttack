using UnityEngine;

public class WeaponProjectile : MonoBehaviour
{
    [Header("--- Projectile Movement Settings ---")]
    [Tooltip("Finger release (Up) karne par knife kitni tez raftaar (speed) se seedha aage move karegi.")]
    [SerializeField] private float shootSpeed = 35f;

    [Tooltip("Shoot hone ke kitne seconds baad knife automatically scene se destroy/gayab ho jayegi.")]
    [SerializeField] private float autoDestroyTime = 2f;

    private bool isFlying = false;

    void Update()
    {
        if (isFlying)
        {
            // Seedha forward direction me fly karega
            transform.position += transform.forward * shootSpeed * Time.deltaTime;
        }
    }

    public void Launch()
    {
        isFlying = true;
        Destroy(gameObject, autoDestroyTime);
    }

    public void ResetState()
    {
        isFlying = false;
    }
}