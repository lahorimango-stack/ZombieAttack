using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using DG.Tweening;

/// <summary>
/// Zombie hamla kare to screen par saaf "mujhe chot lagi" ehsaas (DOTween):
///  - Khoon wale laal kinare (blood edge overlay) zor se chamakte hain - jitna bara hamla utna gehra
///  - Post FX jhatka: laal vignette + chromatic aberration + rang udna
///  - Camera ka chhota jhatka (punch rotation)
///  - Health kam (40% se neeche): kinare dhadakte hain (heartbeat) + dhadkan ki awaaz - khatra mehsoos ho
/// Barricade ki health ghatne se khud chalta hai (koi aur code call nahi karta).
/// </summary>
public class DamageScreenFX : MonoBehaviour
{
    public static DamageScreenFX Instance { get; private set; }

    [Header("--- References (scene mein) ---")]
    [Tooltip("Poori screen ka khooni kinara (beech khaali)")]
    [SerializeField] private Image bloodEdge;
    [Tooltip("Laal vignette + chromatic aberration wala Volume (weight 0..1)")]
    [SerializeField] private Volume damageVolume;
    [SerializeField] private Camera mainCamera;

    [Header("--- Hit Flash ---")]
    [SerializeField] private float minFlash = 0.45f;
    [SerializeField] private float maxFlash = 0.95f;
    [SerializeField] private float fadeTime = 0.6f;
    [SerializeField] private float cameraPunch = 1.6f;
    [Tooltip("Bohat zombies ek saath maarein to har hit par poora flash nahi (aankhon ko takleef)")]
    [SerializeField] private float minGap = 0.12f;

    [Header("--- Low Health ---")]
    [Range(0f, 1f)][SerializeField] private float lowHealthThreshold = 0.4f;
    [SerializeField] private AudioClip heartbeatSound;

    private float lastPercent = 1f;
    private float lowBase;          // kam health par hamesha rehne wala kinara
    private float nextFlashTime;
    private Tween flashTween, volumeTween;
    private Sequence heartbeat;
    private bool subscribed;

    void Awake()
    {
        Instance = this;
        SetEdge(0f);
        if (damageVolume != null) damageVolume.weight = 0f;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void OnEnable() => TrySubscribe();
    void Start() => TrySubscribe();

    void OnDisable()
    {
        if (subscribed && Barricade.Instance != null) Barricade.Instance.OnHealthChanged -= OnHealthChanged;
        subscribed = false;
        StopHeartbeat();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void TrySubscribe()
    {
        if (subscribed || Barricade.Instance == null) return;
        Barricade.Instance.OnHealthChanged += OnHealthChanged;
        subscribed = true;
        lastPercent = Barricade.Instance.HealthPercent;
    }

    private void OnHealthChanged(float percent)
    {
        float drop = lastPercent - percent;
        lastPercent = percent;

        // Naya level / health wapis: sab saaf
        if (drop < 0f || percent >= 0.999f)
        {
            lowBase = 0f;
            StopHeartbeat();
            flashTween?.Kill();
            SetEdge(0f);
            if (damageVolume != null) { volumeTween?.Kill(); damageVolume.weight = 0f; }
            return;
        }

        UpdateLowHealth(percent);
        if (drop > 0f) Hit(drop, percent);
    }

    /// <summary> Hamla laga: flash + post fx + camera jhatka </summary>
    private void Hit(float drop, float percent)
    {
        if (Time.unscaledTime < nextFlashTime) return;
        nextFlashTime = Time.unscaledTime + minGap;

        // Jitna bara hamla aur jitni kam health, utna gehra laal
        float strength = Mathf.Clamp01(drop * 12f + (1f - percent) * 0.4f);
        float peak = Mathf.Lerp(minFlash, maxFlash, strength);

        if (bloodEdge != null)
        {
            flashTween?.Kill();
            SetEdge(peak);
            bloodEdge.rectTransform.localScale = Vector3.one * 1.06f;
            flashTween = DOTween.Sequence()
                .Append(DOTween.To(GetEdge, SetEdge, lowBase, fadeTime).SetEase(Ease.InQuad))
                .Join(bloodEdge.rectTransform.DOScale(1f, 0.25f).SetEase(Ease.OutQuad))
                .SetUpdate(true);
        }

        if (damageVolume != null)
        {
            volumeTween?.Kill();
            damageVolume.weight = Mathf.Lerp(0.6f, 1f, strength);
            volumeTween = DOTween.To(() => damageVolume.weight, w => damageVolume.weight = w, 0f, fadeTime * 0.8f)
                                 .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        if (mainCamera != null)
        {
            Transform ct = mainCamera.transform;
            ct.DOComplete();
            ct.DOPunchRotation(new Vector3(-cameraPunch, Random.Range(-1f, 1f) * cameraPunch, Random.Range(-1f, 1f) * cameraPunch * 0.8f), 0.28f, 10, 0.6f)
              .SetUpdate(true);
        }
    }

    private void UpdateLowHealth(float percent)
    {
        if (percent > lowHealthThreshold || percent <= 0f)
        {
            lowBase = 0f;
            StopHeartbeat();
            return;
        }
        float k = 1f - percent / lowHealthThreshold; // 0 -> 1 jitni kam health
        lowBase = Mathf.Lerp(0.15f, 0.35f, k);
        float beat = Mathf.Lerp(1.05f, 0.62f, k);  // kam health = tez dhadkan
        if (heartbeat != null && heartbeat.IsActive() && Mathf.Abs(heartbeat.Duration() - beat) < 0.05f) return;

        StopHeartbeat();
        // "Lub-dub": do jhatke, phir aaraam
        heartbeat = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
            .AppendCallback(PlayBeat)
            .Append(DOTween.To(GetEdge, SetEdgeBeat, lowBase + 0.2f, beat * 0.12f))
            .Append(DOTween.To(GetEdge, SetEdgeBeat, lowBase, beat * 0.13f))
            .Append(DOTween.To(GetEdge, SetEdgeBeat, lowBase + 0.12f, beat * 0.1f))
            .Append(DOTween.To(GetEdge, SetEdgeBeat, lowBase, beat * 0.2f))
            .AppendInterval(beat * 0.45f);
    }

    private void PlayBeat()
    {
        if (heartbeatSound != null) SoundManager.Instance?.PlaySFX(heartbeatSound, 0.55f, 0.6f, 0.7f);
    }

    private void StopHeartbeat()
    {
        heartbeat?.Kill();
        heartbeat = null;
    }

    private float GetEdge() => bloodEdge != null ? bloodEdge.color.a : 0f;

    // Heartbeat hit flash ke upar na chadhe (flash chal raha ho to dhadkan skip)
    private void SetEdgeBeat(float a)
    {
        if (flashTween != null && flashTween.IsActive() && flashTween.IsPlaying()) return;
        SetEdge(a);
    }

    private void SetEdge(float a)
    {
        if (bloodEdge == null) return;
        bloodEdge.enabled = a > 0.003f;
        Color c = bloodEdge.color; c.a = a; bloodEdge.color = c;
    }
}
