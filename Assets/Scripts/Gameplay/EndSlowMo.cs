using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;

/// <summary>
/// Level ke aakhir (jeet / haar) ka cinematic slow-mo - film jaisa, DOTween se smooth:
///  - Waqt ease ke saath dheema (jhatke se nahi), phir result panel se pehle ease se wapas
///  - Camera aahista andar push (FOV), letterbox pattiyan, HUD gayab
///  - Rang: jeet = garam sunehra, haar = be-rang laal (apna Volume)
///  - Music ki pitch neeche (sab dheema mehsoos ho) + time-warp awaaz
///  - Beech mein bara title ("LEVEL CLEARED" / "OVERRUN") - haroof phail kar aate hain
/// </summary>
public class EndSlowMo : MonoBehaviour
{
    public static EndSlowMo Instance { get; private set; }

    [Header("--- References (scene mein) ---")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Volume winVolume;
    [SerializeField] private Volume failVolume;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private RectTransform letterboxTop;
    [SerializeField] private RectTransform letterboxBottom;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private AudioSource musicSource;

    [Header("--- Feel ---")]
    [SerializeField] private float slowScale = 0.18f;
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private float letterboxHeight = 150f;
    [SerializeField] private float fovPush = 9f;
    [SerializeField] private AudioClip slowSound;
    [SerializeField] private AudioClip resumeSound;
    [SerializeField] private Color winColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color failColor = new Color(0.9f, 0.12f, 0.1f);

    private float baseFov;
    private float fixedDt;
    private float musicPitch = 1f;
    private Sequence seq;

    void Awake()
    {
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
        baseFov = mainCamera != null ? mainCamera.fieldOfView : 60f;
        fixedDt = Time.fixedDeltaTime;
        ResetAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public IEnumerator PlayWin(string subtitle) => Play(true, "LEVEL CLEARED", subtitle);
    public IEnumerator PlayFail(string subtitle) => Play(false, "OVERRUN", subtitle);

    private IEnumerator Play(bool win, string title, string subtitle)
    {
        seq?.Kill();
        Volume vol = win ? winVolume : failVolume;
        if (musicSource != null) musicPitch = musicSource.pitch;
        if (slowSound != null) SoundManager.Instance?.PlaySFX(slowSound, win ? 1f : 0.85f, win ? 1f : 0.85f, 0.85f);

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = win ? winColor : failColor;
            titleText.characterSpacing = 60f;
            titleText.alpha = 0f;
            titleText.gameObject.SetActive(true);
        }
        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
            subtitleText.alpha = 0f;
            subtitleText.gameObject.SetActive(true);
        }

        // 1. Andar: waqt dheema, camera push, rang, letterbox, title
        Vector2 titlePos = titleText != null ? titleText.rectTransform.anchoredPosition : Vector2.zero;
        seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(DOTween.To(() => Time.timeScale, SetTimeScale, slowScale, 0.45f).SetEase(Ease.OutCubic));
        seq.Join(DOTween.To(GetLetterbox, SetLetterbox, 1f, 0.4f).SetEase(Ease.OutCubic));
        if (vol != null) seq.Join(DOTween.To(() => vol.weight, w => vol.weight = w, 1f, 0.5f));
        if (mainCamera != null) seq.Join(mainCamera.DOFieldOfView(baseFov - fovPush, holdTime + 0.5f).SetEase(Ease.OutSine));
        if (musicSource != null) seq.Join(musicSource.DOPitch(win ? 0.75f : 0.55f, 0.6f));
        if (titleText != null)
        {
            seq.Insert(0.2f, DOTween.To(() => titleText.characterSpacing, sp => titleText.characterSpacing = sp, 6f, 0.9f).SetEase(Ease.OutCubic));
            seq.Insert(0.2f, titleText.DOFade(1f, 0.35f));
        }
        if (subtitleText != null) seq.Insert(0.55f, subtitleText.DOFade(0.85f, 0.4f));
        yield return seq.WaitForCompletion();

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdTime - 0.5f));

        // 2. Bahar: waqt ease se normal, title upar ud kar gayab, letterbox hata (panel aane wala hai)
        if (resumeSound != null) SoundManager.Instance?.PlaySFX(resumeSound, 1f, 1f, 0.6f);
        seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(DOTween.To(() => Time.timeScale, SetTimeScale, 1f, 0.35f).SetEase(Ease.InCubic));
        seq.Join(DOTween.To(GetLetterbox, SetLetterbox, 0f, 0.35f));
        if (titleText != null)
        {
            seq.Join(titleText.DOFade(0f, 0.3f));
            seq.Join(titleText.rectTransform.DOAnchorPosY(titlePos.y + 60f, 0.35f));
        }
        if (subtitleText != null) seq.Join(subtitleText.DOFade(0f, 0.25f));
        if (musicSource != null) seq.Join(musicSource.DOPitch(musicPitch, 0.5f));
        if (vol != null) seq.Join(DOTween.To(() => vol.weight, w => vol.weight = w, 0.55f, 0.4f)); // panel ke peeche halka rang rahe
        yield return seq.WaitForCompletion();
        if (titleText != null) titleText.rectTransform.anchoredPosition = titlePos;
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (subtitleText != null) subtitleText.gameObject.SetActive(false);
        SetTimeScale(1f);
    }

    /// <summary> Naya level / menu: sab saaf </summary>
    public void ResetAll()
    {
        seq?.Kill();
        if (Time.timeScale > 0f) SetTimeScale(1f);
        if (mainCamera != null) { mainCamera.DOKill(); mainCamera.fieldOfView = baseFov; }
        if (winVolume != null) winVolume.weight = 0f;
        if (failVolume != null) failVolume.weight = 0f;
        if (musicSource != null) { musicSource.DOKill(); musicSource.pitch = 1f; }
        SetLetterbox(0f);
        if (titleText != null) { titleText.gameObject.SetActive(false); }
        if (subtitleText != null) subtitleText.gameObject.SetActive(false);
    }

    private void SetTimeScale(float s)
    {
        Time.timeScale = s;
        Time.fixedDeltaTime = fixedDt * Mathf.Max(0.05f, s);
    }

    private float letterbox;
    private float GetLetterbox() => letterbox;

    private void SetLetterbox(float k)
    {
        letterbox = k;
        if (hudGroup != null) hudGroup.alpha = 1f - k;
        float h = letterboxHeight * k;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, h);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, h);
    }
}
