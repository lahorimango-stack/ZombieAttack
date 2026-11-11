using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Level ke aakhir ka cinematic - camera ki asal harkat se kahani (bara title nahi):
///  JEET: waqt pal bhar dheema, phir camera aahista upar uth kar peeche hat-ta hai aur laashon se bhari
///        sadak dikhata hai (crane shot), garam roshni. Side se patli "AREA CLEARED" patti (mission complete).
///  HAAR: player gir jata hai - camera jhuk kar zameen par, zombies ke qadam qareeb aate hain,
///        dhadkan, screen laal phir andheri. Phir result panel.
/// DOTween se, unscaled time (pause/slow-mo se azad).
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
    [Tooltip("Side se aane wali patli patti (mission complete)")]
    [SerializeField] private RectTransform band;
    [SerializeField] private Image bandAccent;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [Tooltip("Haar par andhera (kaala full screen)")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private AudioSource musicSource;

    [Header("--- Feel ---")]
    [SerializeField] private float letterboxHeight = 130f;
    [SerializeField] private AudioClip slowSound;
    [SerializeField] private AudioClip resumeSound;
    [SerializeField] private AudioClip fallSound;
    [SerializeField] private Color winColor = new Color(1f, 0.78f, 0.25f);
    [SerializeField] private Color failColor = new Color(0.9f, 0.14f, 0.1f);

    private Vector3 camPos;
    private Quaternion camRot;
    private bool camSaved;
    private float fixedDt;
    private float musicPitch = 1f;
    private Sequence seq;
    private float bandX;

    void Awake()
    {
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
        fixedDt = Time.fixedDeltaTime;
        if (band != null) bandX = band.anchoredPosition.x;
        ResetAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public IEnumerator PlayWin(string subtitle)
    {
        Begin(winVolume);
        var cam = mainCamera.transform;
        Vector3 start = cam.position;
        // Crane: upar aur peeche, neeche sadak ki taraf dekhta hua
        Vector3 up = start + Vector3.up * 2.1f - cam.forward * 1.4f;
        Quaternion look = Quaternion.Euler(24f, cam.eulerAngles.y, 0f);

        if (slowSound != null) SoundManager.Instance?.PlaySFX(slowSound, 1f, 1f, 0.7f);
        seq = DOTween.Sequence().SetUpdate(true);
        // Aakhri laash ko girta dekho (waqt dheema), phir aahista normal
        seq.Append(DOTween.To(() => Time.timeScale, SetTimeScale, 0.3f, 0.25f).SetEase(Ease.OutQuad));
        seq.Join(DOTween.To(GetLetterbox, SetLetterbox, 1f, 0.5f).SetEase(Ease.OutCubic));
        if (winVolume != null) seq.Join(DOTween.To(() => winVolume.weight, w => winVolume.weight = w, 0.85f, 1.2f));
        if (musicSource != null) seq.Join(musicSource.DOPitch(0.85f, 0.8f));
        seq.Insert(0.6f, DOTween.To(() => Time.timeScale, SetTimeScale, 1f, 0.9f).SetEase(Ease.InOutSine));
        seq.Insert(0.35f, cam.DOMove(up, 2.4f).SetEase(Ease.InOutSine));
        seq.Insert(0.35f, cam.DORotateQuaternion(look, 2.4f).SetEase(Ease.InOutSine));
        ShowBand(seq, 0.9f, "AREA CLEARED", subtitle, winColor);
        yield return seq.WaitForCompletion();
        yield return new WaitForSecondsRealtime(0.35f);
        if (resumeSound != null) SoundManager.Instance?.PlaySFX(resumeSound, 1f, 1f, 0.5f);
        HideBand();
        if (musicSource != null) musicSource.DOPitch(musicPitch, 0.5f).SetUpdate(true);
    }

    public IEnumerator PlayFail(string subtitle)
    {
        Begin(failVolume);
        var cam = mainCamera.transform;
        Vector3 start = cam.position;
        // Gir gaya: ek taraf lurak kar zameen par (0.35m), zombies ki taraf upar dekhta hua
        Vector3 ground = new Vector3(start.x + 0.35f, 0.38f, start.z + 0.4f);
        Quaternion fallen = Quaternion.Euler(-12f, cam.eulerAngles.y + 8f, 68f);

        if (fallSound != null) SoundManager.Instance?.PlaySFX(fallSound, 0.8f, 0.8f, 1f);
        seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(cam.DOMove(start + Vector3.down * 0.25f, 0.18f).SetEase(Ease.OutQuad));           // jhatka
        seq.Append(cam.DOMove(ground, 0.55f).SetEase(Ease.InQuad));                                   // girna
        seq.Join(cam.DORotateQuaternion(fallen, 0.6f).SetEase(Ease.InQuad));
        seq.Append(cam.DOShakePosition(0.25f, 0.08f, 18, 90f, false, true));                         // zameen se takrana
        seq.Join(DOTween.To(GetLetterbox, SetLetterbox, 1f, 0.4f));
        if (failVolume != null) seq.Join(DOTween.To(() => failVolume.weight, w => failVolume.weight = w, 1f, 0.8f));
        if (musicSource != null) seq.Join(musicSource.DOPitch(0.5f, 1.2f));
        ShowBand(seq, 0.95f, "OVERRUN", subtitle, failColor);
        // Zombies qareeb aate rahein, nazar dhundli ho kar andheri
        if (fadeImage != null)
        {
            fadeImage.enabled = true;
            seq.Insert(1.3f, fadeImage.DOFade(0.72f, 1.2f).SetEase(Ease.InQuad));
        }
        yield return seq.WaitForCompletion();
        HideBand();
    }

    private void Begin(Volume vol)
    {
        seq?.Kill();
        if (mainCamera == null) return;
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc != null) wc.CinematicLock = true;
        if (!camSaved) { camPos = mainCamera.transform.position; camRot = mainCamera.transform.rotation; camSaved = true; }
        if (musicSource != null) musicPitch = 1f;
    }

    /// <summary> Side se patti: accent pehle, phir text </summary>
    private void ShowBand(Sequence s, float at, string title, string sub, Color accent)
    {
        if (band == null) return;
        band.gameObject.SetActive(true);
        band.anchoredPosition = new Vector2(bandX - 1400f, band.anchoredPosition.y);
        if (bandAccent != null) bandAccent.color = accent;
        if (titleText != null) { titleText.text = title; titleText.alpha = 0f; }
        if (subtitleText != null) { subtitleText.text = sub; subtitleText.alpha = 0f; }
        s.Insert(at, band.DOAnchorPosX(bandX, 0.45f).SetEase(Ease.OutCubic));
        if (titleText != null) s.Insert(at + 0.25f, titleText.DOFade(1f, 0.3f));
        if (subtitleText != null) s.Insert(at + 0.4f, subtitleText.DOFade(0.85f, 0.3f));
    }

    private void HideBand()
    {
        if (band == null) return;
        band.DOAnchorPosX(bandX + 1400f, 0.3f).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => band.gameObject.SetActive(false));
    }

    /// <summary> Naya level / menu: sab saaf, camera wapis </summary>
    public void ResetAll()
    {
        seq?.Kill();
        if (Time.timeScale > 0f) SetTimeScale(1f);
        if (mainCamera != null)
        {
            mainCamera.transform.DOKill();
            if (camSaved) mainCamera.transform.SetPositionAndRotation(camPos, camRot);
        }
        camSaved = false;
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc != null) wc.CinematicLock = false;
        if (winVolume != null) winVolume.weight = 0f;
        if (failVolume != null) failVolume.weight = 0f;
        if (musicSource != null) { musicSource.DOKill(); musicSource.pitch = 1f; }
        if (fadeImage != null) { fadeImage.DOKill(); Color c = fadeImage.color; c.a = 0f; fadeImage.color = c; fadeImage.enabled = false; }
        if (band != null) { band.DOKill(); band.gameObject.SetActive(false); }
        SetLetterbox(0f);
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
