using UnityEngine;
using DG.Tweening;

/// <summary>
/// Street mein khari gaadi: weapons maaro to pehle chingariyan, phir dhuaan, phir zor ka dhamaka -
/// gap se nikalte zombies ka poora group uda deti hai. Phat kar jali hui gaadi ban jati hai (aag ke saath).
/// Sab references scene/prefab mein assign hote hain.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExplosiveVehicle : MonoBehaviour, IDamageable
{
    [Header("--- 1. Health ---")]
    [SerializeField] private float maxHealth = 140f;

    [Header("--- 2. Explosion ---")]
    [Tooltip("Dhamaka is point par (gaadi ka lane wala kinara) - zombie lane ko cover kare")]
    [SerializeField] private Transform blastPoint;
    [SerializeField] private float radius = 4f;
    [SerializeField] private float damage = 450f;
    [SerializeField] private float force = 44f;
    [SerializeField] private LayerMask damageMask = 1 << 7;
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private AudioClip[] explosionSounds;

    [Header("--- 3. Damage Look ---")]
    [Tooltip("Gaadi ka model (uchhalta hai, rang badalta hai)")]
    [SerializeField] private Transform body;
    [Tooltip("Saalim gaadi ka model")]
    [SerializeField] private GameObject intactModel;
    [Tooltip("Jali hui gaadi ka model (shuru mein band) - phatne par saalim ki jagah")]
    [SerializeField] private GameObject burntModel;
    [Tooltip("Aadhi health par chalne wala dhuaan (child, shuru mein band)")]
    [SerializeField] private GameObject smokeFx;
    [Tooltip("Phatne ke baad aag (child, shuru mein band)")]
    [SerializeField] private GameObject fireFx;
    [SerializeField] private GameObject hitSparkVfx;
    [SerializeField] private AudioClip[] hitSounds;
    [Tooltip("Hit par chamakne wali laal/narangi alarm light (optional)")]
    [SerializeField] private Light alarmLight;

    private float health;
    private bool exploded;
    private Vector3 bodyStartPos;
    private Quaternion bodyStartRot;
    private float nextSparkTime;

    void Awake()
    {
        health = maxHealth;
        if (body == null) body = transform;
        bodyStartPos = body.localPosition;
        bodyStartRot = body.localRotation;
        if (alarmLight != null) alarmLight.enabled = false;
    }

    public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (exploded) return;

        // Doosre dhamake se lage to seedha phat jao (chain reaction)
        health -= Explosions.IsDetonating ? maxHealth : Mathf.Max(damageAmount, 20f);

        if (Time.time >= nextSparkTime)
        {
            nextSparkTime = Time.time + 0.1f;
            if (hitSparkVfx != null) Destroy(Instantiate(hitSparkVfx, hitPoint, Quaternion.LookRotation(-hitDirection.normalized + Vector3.up * 0.01f)), 1.5f);
            SoundManager.Instance?.PlayRandomSFX(hitSounds, 0.9f, 1.1f, 0.7f);
            body.DOKill(true);
            body.DOPunchPosition(hitDirection.normalized * 0.06f, 0.2f, 12, 0.5f);
        }

        if (smokeFx != null && !smokeFx.activeSelf && health <= maxHealth * 0.55f) smokeFx.SetActive(true);
        if (alarmLight != null && !alarmLight.enabled)
        {
            alarmLight.enabled = true;
            alarmLight.DOIntensity(0f, 0.25f).From(alarmLight.intensity).SetLoops(-1, LoopType.Yoyo);
        }

        if (health <= 0f)
        {
            if (Explosions.IsDetonating) Invoke(nameof(Explode), 0.2f); else Explode();
            exploded = true;
        }
    }

    private void Explode()
    {
        exploded = true;
        Vector3 p = blastPoint != null ? blastPoint.position : transform.position + Vector3.up * 0.6f;
        Explosions.Detonate(p, radius, damage, force, damageMask, explosionVfx, explosionSounds, 0.45f);

        // Jali hui gaadi: rang kaala, aag, ek jhatke se uchhal kar wapas
        if (burntModel != null) { burntModel.SetActive(true); if (intactModel != null) intactModel.SetActive(false); }
        if (fireFx != null) fireFx.SetActive(true);
        if (alarmLight != null) { alarmLight.DOKill(); alarmLight.enabled = false; }

        body.DOKill(true);
        body.DOLocalJump(bodyStartPos, 0.7f, 1, 0.6f);
        body.DOLocalRotate(bodyStartRot.eulerAngles + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-8f, 8f)), 0.6f);
        GetComponent<Collider>().enabled = false;
    }

    /// <summary> Level restart: nayi gaadi wapas </summary>
    public void ResetVehicle()
    {
        CancelInvoke();
        exploded = false;
        health = maxHealth;
        body.DOKill();
        body.localPosition = bodyStartPos;
        body.localRotation = bodyStartRot;
        if (intactModel != null) intactModel.SetActive(true);
        if (burntModel != null) burntModel.SetActive(false);
        if (smokeFx != null) smokeFx.SetActive(false);
        if (fireFx != null) fireFx.SetActive(false);
        if (alarmLight != null) { alarmLight.DOKill(); alarmLight.enabled = false; }
        GetComponent<Collider>().enabled = true;
    }
}
