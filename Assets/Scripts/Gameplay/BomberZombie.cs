using UnityEngine;

/// <summary>
/// Hara chamakta "Bomber" zombie: marne par phat jata hai (aas paas ke zombies bhi marte hain).
/// Agar player tak pahunch jaye to wahin phat kar bohat nuqsan karta hai.
/// </summary>
[RequireComponent(typeof(HumanoidEnemy))]
public class BomberZombie : MonoBehaviour
{
    [SerializeField] private float radius = 3.2f;
    [SerializeField] private float damage = 220f;
    [SerializeField] private float force = 34f;
    [SerializeField] private float playerDamage = 35f;
    [SerializeField] private LayerMask damageMask = 1 << 7;
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private AudioClip[] explosionSounds;

    private HumanoidEnemy enemy;
    private bool exploded;

    void Awake()
    {
        enemy = GetComponent<HumanoidEnemy>();
        enemy.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (enemy != null) enemy.OnDied -= HandleDied;
    }

    private void HandleDied(HumanoidEnemy e)
    {
        if (exploded) return;
        exploded = true;
        Explosions.Detonate(e.transform.position + Vector3.up, radius, damage, force, damageMask,
                            explosionVfx, explosionSounds, 0.3f, 2.5f, playerDamage);
    }
}
