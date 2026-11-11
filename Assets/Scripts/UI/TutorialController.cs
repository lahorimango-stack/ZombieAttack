using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pehli dafa khelne wale ke liye "haath se sikhao" tutorial (Level 1 se pehle):
///  1. Haath screen par lakeer kheenchta hai - jahan se guzarta hai wahan asli jaise (dummy) weapons bante hain
///  2. Haath uthta hai -> "RELEASE TO THROW!" -> weapons ud jaate hain
///  3. Ye demo tab tak chalta hai jab tak player khud ungli na rakhe
///  4. Player khud draw kar ke phenke -> "NICE!" -> zombies aana shuru
/// Zombies tutorial khatam hone tak nahi aate (LevelManager intezar karta hai).
/// </summary>
public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }

    private const string TutorialKey = "TutorialDone";

    [Header("--- References (scene mein) ---")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform hand;
    [SerializeField] private Image handImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [Tooltip("Raaste ke dots (guide line) - haath ke aage aage roshan hote hain")]
    [SerializeField] private Image[] pathDots;

    [Header("--- Path (screen 0..1) ---")]
    [SerializeField] private Vector2 pathStart = new Vector2(0.2f, 0.3f);
    [SerializeField] private Vector2 pathMid = new Vector2(0.5f, 0.22f);
    [SerializeField] private Vector2 pathEnd = new Vector2(0.8f, 0.33f);
    [SerializeField] private float drawDuration = 1.5f;
    [SerializeField] private int demoWeapons = 8;

    /// <summary> Tutorial chal raha hai: zombies abhi nahi aayenge </summary>
    public bool Blocking { get; private set; }

    private RectTransform root;
    private readonly List<ThrownWeapon> dummies = new List<ThrownWeapon>();
    private Coroutine demo;
    private bool playerTookOver;
    private WeaponFormationController wc;

    void Awake()
    {
        Instance = this;
        root = transform as RectTransform;
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (wc != null) wc.OnWeaponsLaunched -= OnPlayerThrew;
    }

    public static bool IsDone => PlayerPrefs.GetInt(TutorialKey, 0) == 1;

    /// <summary> Level shuru: pehli dafa ho to tutorial </summary>
    public void Begin()
    {
        Stop();
        if (IsDone) return;
        wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc == null) return;

        Blocking = true;
        playerTookOver = false;
        wc.OnWeaponsLaunched -= OnPlayerThrew;
        wc.OnWeaponsLaunched += OnPlayerThrew;
        gameObject.SetActive(true);
        if (group != null) group.alpha = 1f;
        SetTitle("DRAW A LINE", "Drag your finger to create weapons");
        demo = StartCoroutine(DemoLoop());
    }

    /// <summary> Level khatam / restart </summary>
    public void Stop()
    {
        if (demo != null) StopCoroutine(demo);
        demo = null;
        ClearDummies();
        if (wc != null) wc.OnWeaponsLaunched -= OnPlayerThrew;
        Blocking = false;
        Hide();
    }

    void Update()
    {
        if (!Blocking || playerTookOver || wc == null) return;

        // Player ne khud ungli rakhi: demo band, ab woh khud seekhe
        if (wc.IsDrawing)
        {
            playerTookOver = true;
            if (demo != null) StopCoroutine(demo);
            demo = null;
            ClearDummies();
            SetHand(false, 0f);
            SetDots(-1);
            SetTitle("KEEP DRAWING...", "Then lift your finger to throw!");
        }
    }

    private IEnumerator DemoLoop()
    {
        while (true)
        {
            SetTitle("DRAW A LINE", "Drag your finger to create weapons");
            ClearDummies();

            // 1. Haath shuru par aata hai aur "dabata" hai
            Vector2 p0 = ScreenAt(0f);
            PlaceHand(p0);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = t / 0.35f;
                SetHand(true, k);
                hand.localScale = Vector3.one * Mathf.Lerp(1.25f, 0.9f, k); // ungli screen par dabi
                SetDots(0);
                yield return null;
            }

            // 2. Lakeer kheencho - raaste par weapons bante jayen
            int spawned = 0;
            int count = Mathf.Max(3, demoWeapons);
            t = 0f;
            while (t < drawDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / drawDuration);
                float e = k * k * (3f - 2f * k); // smooth
                Vector2 sp = ScreenAt(e);
                PlaceHand(sp);
                hand.localScale = Vector3.one * 0.9f;
                SetDots(Mathf.RoundToInt(e * (pathDots != null ? pathDots.Length : 0)));
                int want = Mathf.FloorToInt(e * (count - 1)) + 1;
                while (spawned < want)
                {
                    var w = wc.SpawnDummyAt(ScreenAt((float)spawned / (count - 1)), spawned);
                    if (w != null) dummies.Add(w);
                    spawned++;
                }
                yield return null;
            }

            // 3. Ungli uthao -> throw
            SetTitle("RELEASE TO THROW!", "Lift your finger and they fly at the zombies");
            yield return Wait(0.35f);
            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                hand.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.2f, t / 0.2f);
                yield return null;
            }
            wc.LaunchDummies(dummies);
            dummies.Clear();
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                SetHand(true, 1f - t / 0.35f);
                yield return null;
            }
            SetHand(false, 0f);
            SetDots(-1);
            SetTitle("YOUR TURN!", "Draw a line and release");
            yield return Wait(1.3f);
        }
    }

    private void OnPlayerThrew()
    {
        if (!Blocking) return;
        PlayerPrefs.SetInt(TutorialKey, 1);
        PlayerPrefs.Save();
        if (demo != null) StopCoroutine(demo);
        demo = null;
        ClearDummies();
        SetHand(false, 0f);
        SetDots(-1);
        StartCoroutine(Finish());
    }

    private IEnumerator Finish()
    {
        SetTitle("NICE!", "Here they come...");
        if (titleText != null) titleText.rectTransform.localScale = Vector3.one * 1.3f;
        float t = 0f;
        while (t < 1.1f)
        {
            t += Time.deltaTime;
            if (titleText != null) titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(t / 0.2f));
            if (group != null) group.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.3f;
            yield return null;
        }
        Blocking = false; // ab zombies aayenge
        if (wc != null) wc.OnWeaponsLaunched -= OnPlayerThrew;
        Hide();
    }

    // ---------------- helpers ----------------

    private IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.deltaTime; yield return null; }
    }

    /// <summary> Raaste par point (0..1) - screen pixels mein (quadratic curve) </summary>
    private Vector2 ScreenAt(float k)
    {
        Vector2 a = Vector2.Lerp(pathStart, pathMid, k);
        Vector2 b = Vector2.Lerp(pathMid, pathEnd, k);
        Vector2 n = Vector2.Lerp(a, b, k);
        return new Vector2(n.x * Screen.width, n.y * Screen.height);
    }

    private void PlaceHand(Vector2 screen)
    {
        if (hand == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        hand.anchoredPosition = local; // pivot ungli ki nok par hai
    }

    private void SetHand(bool on, float alpha)
    {
        if (hand == null) return;
        hand.gameObject.SetActive(on);
        if (handImage != null) { Color c = handImage.color; c.a = alpha; handImage.color = c; }
    }

    /// <summary> Guide dots: 'upto' tak roshan (haath guzar chuka), aage wale halke </summary>
    private void SetDots(int upto)
    {
        if (pathDots == null) return;
        for (int i = 0; i < pathDots.Length; i++)
        {
            if (pathDots[i] == null) continue;
            bool show = upto >= 0;
            pathDots[i].gameObject.SetActive(show);
            if (!show) continue;
            float k = pathDots.Length > 1 ? (float)i / (pathDots.Length - 1) : 0f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, ScreenAt(k), null, out Vector2 local);
            pathDots[i].rectTransform.anchoredPosition = local;
            Color c = pathDots[i].color;
            c.a = i < upto ? 0.15f : 0.75f; // guzre hue halke (wahan weapon ban gaya), aage wale saaf
            pathDots[i].color = c;
        }
    }

    private void SetTitle(string title, string sub)
    {
        if (titleText != null) titleText.text = title;
        if (subText != null) subText.text = sub;
    }

    private void ClearDummies()
    {
        foreach (var w in dummies)
            if (w != null && !w.IsLaunched) Destroy(w.gameObject);
        dummies.Clear();
    }

    private void Hide()
    {
        SetHand(false, 0f);
        SetDots(-1);
        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }
}
