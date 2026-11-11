using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Player ke samne wala barricade. Zombies isay maarte hain; HP 0 = Level Fail.
/// </summary>
public class Barricade : MonoBehaviour
{
    public static Barricade Instance { get; private set; }

    [Header("--- 1. Health ---")]
    [SerializeField] private float maxHealth = 100f;

    [Header("--- 2. Zombie Stop Line ---")]
    [Tooltip("Zombies is Z line par ruk kar hamla karenge (barricade ka saamne wala hissa)")]
    [SerializeField] private float frontZOffset = 0.6f;

    [Header("--- 3. Visual Feedback ---")]
    [Tooltip("Hit par ye hilega")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Health kam hone par ye pieces ek ek kar ke gir jayenge (pehla piece 75% par)")]
    [SerializeField] private GameObject[] breakablePieces;
    [SerializeField] private GameObject hitVfx;
    [SerializeField] private GameObject breakVfx;
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip breakSound;
    [Tooltip("Do hit sounds ke darmiyan minimum gap (spam se bachne ke liye)")]
    [SerializeField] private float hitSoundCooldown = 0.12f;
    [Tooltip("Zombie ke hamle par camera kitna hile")]
    [SerializeField] private float hitCameraShake = 0.05f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float HealthPercent => maxHealth > 0 ? Mathf.Clamp01(Health / maxHealth) : 0f;
    public float FrontZ => transform.position.z + frontZOffset;
    public bool IsBroken => Health <= 0f;

    public event Action<float> OnHealthChanged; // percent

    private float nextHitSoundTime;
    private float nextHitVfxTime;
    private Vector3 visualStartPos;
    private Tween shakeTween;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (visualRoot == null) visualRoot = transform;
        visualStartPos = visualRoot.localPosition;
        Health = maxHealth;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetHealth(float newMaxHealth)
    {
        if (newMaxHealth > 0f) maxHealth = newMaxHealth;
        Health = maxHealth;

        shakeTween?.Kill();
        visualRoot.localPosition = visualStartPos;

        if (breakablePieces != null)
        {
            foreach (var piece in breakablePieces)
                if (piece != null) piece.SetActive(true);
        }

        OnHealthChanged?.Invoke(HealthPercent);
    }

    public void TakeDamage(float amount, Vector3 fromPosition)
    {
        if (IsBroken || LevelManager.Instance == null || LevelManager.Instance.CurrentState != LevelManager.GameState.Playing) return;

        Health = Mathf.Max(0f, Health - amount);
        OnHealthChanged?.Invoke(HealthPercent);

        // Shake
        shakeTween?.Kill();
        visualRoot.localPosition = visualStartPos;
        shakeTween = visualRoot.DOShakePosition(0.18f, new Vector3(0.06f, 0.02f, 0.06f), 18, 90f, false, true);

        // Bohat zombies ek saath maarein to har hit par naya VFX nahi (qareeb aane par lag ki ek wajah)
        if (hitVfx != null && Time.time >= nextHitVfxTime)
        {
            nextHitVfxTime = Time.time + 0.25f;
            Vector3 p = new Vector3(fromPosition.x, transform.position.y + 0.8f, FrontZ);
            var fx = Instantiate(hitVfx, p, Quaternion.LookRotation(Vector3.forward));
            Destroy(fx, 1.5f);
        }

        if (Time.time >= nextHitSoundTime && hitSounds != null && hitSounds.Length > 0)
        {
            nextHitSoundTime = Time.time + hitSoundCooldown;
            SoundManager.Instance?.PlayRandomSFX(hitSounds, 0.85f, 1.1f, 0.7f);
        }

        GameManager.Instance?.TriggerMediumHaptic();
        GameManager.Instance?.WeaponController?.Shake(hitCameraShake, 0.25f);
        UIManager.Instance?.FlashDamage();

        UpdateBreakablePieces();

        if (IsBroken)
        {
            if (breakVfx != null)
            {
                var fx = Instantiate(breakVfx, transform.position + Vector3.up, Quaternion.identity);
                Destroy(fx, 3f);
            }
            if (breakSound != null) SoundManager.Instance?.PlaySFX(breakSound);
            GameManager.Instance?.WeaponController?.Shake(0.25f, 0.4f);
            LevelManager.Instance?.OnLevelFailed();
        }
    }

    private void UpdateBreakablePieces()
    {
        if (breakablePieces == null || breakablePieces.Length == 0) return;

        // e.g. 3 pieces: 75%, 50%, 25% par girenge
        int n = breakablePieces.Length;
        for (int i = 0; i < n; i++)
        {
            float threshold = 1f - (i + 1f) / (n + 1f);
            GameObject piece = breakablePieces[i];
            if (piece != null && piece.activeSelf && HealthPercent <= threshold)
            {
                piece.transform.DOKill();
                piece.SetActive(false);
                if (breakVfx != null)
                {
                    var fx = Instantiate(breakVfx, piece.transform.position, Quaternion.identity);
                    Destroy(fx, 3f);
                }
            }
        }
    }
}
