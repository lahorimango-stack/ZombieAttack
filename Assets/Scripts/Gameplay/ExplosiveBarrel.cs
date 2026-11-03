using UnityEngine;
using DG.Tweening;

/// <summary>
/// Street mein rakha laal barrel: weapon lage to phat jata hai aur aas paas ke zombies uda deta hai.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExplosiveBarrel : MonoBehaviour, IDamageable
{
    [SerializeField] private float radius = 3.6f;
    [SerializeField] private float damage = 300f;
    [SerializeField] private float force = 38f;
    [SerializeField] private LayerMask damageMask = 1 << 7;
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private AudioClip[] explosionSounds;
    [Tooltip("Barrel ke upar chamakti warning light (optional)")]
    [SerializeField] private Light warningLight;
    [Tooltip("Doosre explosion se lagne par phatne se pehle chhota sa delay (chain reaction ka maza)")]
    [SerializeField] private float chainDelay = 0.15f;

    private bool exploded;

    void Start()
    {
        if (warningLight != null)
            warningLight.DOIntensity(warningLight.intensity * 0.3f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (exploded) return;
        exploded = true;
        transform.DOKill();
        if (warningLight != null) warningLight.DOKill();

        // Weapon se lage to foran, doosre explosion se lage to thoda ruk kar (chain reaction ka maza)
        if (Explosions.IsDetonating) Invoke(nameof(Explode), chainDelay); else Explode();
    }

    private void Explode()
    {
        Vector3 p = transform.position + Vector3.up * 0.5f;
        GetComponent<Collider>().enabled = false;
        gameObject.SetActive(false);
        Explosions.Detonate(p, radius, damage, force, damageMask, explosionVfx, explosionSounds, 0.35f);
    }

    /// <summary> Level restart par barrel wapis </summary>
    public void ResetBarrel()
    {
        CancelInvoke();
        exploded = false;
        gameObject.SetActive(true);
        GetComponent<Collider>().enabled = true;
    }
}
