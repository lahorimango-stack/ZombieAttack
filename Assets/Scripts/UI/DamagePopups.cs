using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Hit par zombie ke upar uchhalta number (36, 72...), maut par sone ka "+2", aur
/// lagataar kills par beech mein "DOUBLE KILL / TRIPLE KILL / MASSACRE" combo.
/// Pool + apna sub-canvas: har frame position badalne se baaki HUD dobara nahi banta (mobile).
/// </summary>
public class DamagePopups : MonoBehaviour
{
    public static DamagePopups Instance { get; private set; }

    [Header("--- References (scene mein) ---")]
    [Tooltip("Number ka template (inactive). Pool isi ki copies banata hai")]
    [SerializeField] private TMP_Text template;
    [Tooltip("Combo text (DOUBLE KILL ...)")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private int poolSize = 18;
    [Tooltip("Hit par uchhalte damage numbers (36, 72...)")]
    [SerializeField] private bool showDamageNumbers = false;
    [Tooltip("Maut par sone ka +2")]
    [SerializeField] private bool showCoinPopups = false;

    [Header("--- Look ---")]
    [SerializeField] private Color normalColor = new Color(1f, 0.97f, 0.85f);
    [SerializeField] private Color bigHitColor = new Color(1f, 0.62f, 0.18f);
    [SerializeField] private Color coinColor = new Color(1f, 0.84f, 0.2f);
    [Tooltip("Is se zyada damage = bara narangi number")]
    [SerializeField] private float bigHitThreshold = 60f;
    [SerializeField] private float lifeTime = 0.75f;
    [SerializeField] private float risePixels = 120f;

    [Header("--- Kill Combo ---")]
    [Tooltip("Do kills ke darmiyan itne seconds se kam = combo")]
    [SerializeField] private float comboWindow = 0.9f;
    [SerializeField] private AudioClip comboSound;

    private class Popup
    {
        public TMP_Text text;
        public RectTransform rt;
        public Vector3 world;
        public float age, life, baseScale, drift, total;
        public int key;
        public bool active;
    }

    private readonly List<Popup> pool = new List<Popup>();
    private RectTransform canvasRect;
    private int next;
    private int comboCount;
    private float lastKillTime = -10f;
    private float comboShowUntil;
    private float comboAge;

    private static readonly string[] ComboNames = { "", "", "DOUBLE KILL!", "TRIPLE KILL!", "MULTI KILL!", "MASSACRE!" };

    void Awake()
    {
        Instance = this;
        canvasRect = transform as RectTransform;
        if (template != null)
        {
            template.gameObject.SetActive(false);
            for (int i = 0; i < poolSize; i++)
            {
                var t = Instantiate(template, template.transform.parent);
                t.gameObject.SetActive(false);
                pool.Add(new Popup { text = t, rt = t.rectTransform });
            }
        }
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary> Zombie ko laga hua damage </summary>
    public static void ShowDamage(Vector3 worldPos, float amount, bool killed, int key = 0)
    {
        if (Instance == null || amount <= 0f || !Instance.showDamageNumbers) return;

        // Ek hi zombie ko ek saath kai weapons lagen (formation) to alag alag numbers ka dher nahi - ek number jud kar bara hota hai
        if (key != 0)
        {
            for (int i = 0; i < Instance.pool.Count; i++)
            {
                Popup p = Instance.pool[i];
                if (!p.active || p.key != key || p.age > 0.35f) continue;
                p.total += amount;
                bool bigNow = p.total >= Instance.bigHitThreshold || killed;
                p.text.text = Mathf.RoundToInt(p.total).ToString();
                p.text.color = bigNow ? Instance.bigHitColor : Instance.normalColor;
                p.baseScale = bigNow ? 1.25f : 1f;
                p.age = 0f; // dobara "pop"
                return;
            }
        }

        bool big = amount >= Instance.bigHitThreshold || killed;
        Popup np = Instance.Spawn(worldPos, Mathf.RoundToInt(amount).ToString(), big ? Instance.bigHitColor : Instance.normalColor, big ? 1.25f : 1f);
        if (np != null) { np.key = key; np.total = amount; }
    }

    /// <summary> Maut par coins (+2) aur combo </summary>
    public static void ShowKill(Vector3 worldPos, int coins)
    {
        if (Instance == null) return;
        if (coins > 0 && Instance.showCoinPopups) Instance.Spawn(worldPos + Vector3.up * 0.35f, "+" + coins, Instance.coinColor, 1.1f, 1.15f);
        Instance.RegisterKill();
    }

    private Popup Spawn(Vector3 world, string value, Color color, float scale, float lifeMul = 1f)
    {
        if (pool.Count == 0) return null;
        Popup p = pool[next];
        next = (next + 1) % pool.Count;

        p.world = world;
        p.age = 0f;
        p.life = lifeTime * lifeMul;
        p.baseScale = scale;
        p.drift = Random.Range(-35f, 35f);
        p.active = true;
        p.text.text = value;
        p.text.color = color;
        p.key = 0;
        p.total = 0f;
        p.text.gameObject.SetActive(true);
        Place(p, 0f);
        return p;
    }

    private void RegisterKill()
    {
        float now = Time.unscaledTime;
        comboCount = now - lastKillTime <= comboWindow ? comboCount + 1 : 1;
        lastKillTime = now;
        if (comboCount < 2 || comboText == null) return;

        comboText.text = ComboNames[Mathf.Min(comboCount, ComboNames.Length - 1)] + (comboCount > 5 ? $"  x{comboCount}" : "");
        comboText.gameObject.SetActive(true);
        comboShowUntil = now + 1.1f;
        comboAge = 0f;
        if (comboSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(comboSound, 0.9f + comboCount * 0.05f, 0.95f + comboCount * 0.05f, 0.6f);
    }

    private Canvas ownCanvas;

    void LateUpdate()
    {
        // Kill cam (film) ke dauran numbers/combo nahi - saaf cinematic frame
        if (ownCanvas == null) ownCanvas = GetComponent<Canvas>();
        if (ownCanvas != null) ownCanvas.enabled = !(KillCam.Instance != null && KillCam.Instance.Busy);
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < pool.Count; i++)
        {
            Popup p = pool[i];
            if (!p.active) continue;
            p.age += dt;
            if (p.age >= p.life)
            {
                p.active = false;
                p.text.gameObject.SetActive(false);
                continue;
            }
            Place(p, p.age / p.life);
        }

        if (comboText != null && comboText.gameObject.activeSelf)
        {
            comboAge += dt;
            float left = comboShowUntil - Time.unscaledTime;
            if (left <= 0f) comboText.gameObject.SetActive(false);
            else
            {
                // Zor se aaye (1.6 -> 1), phir fade
                float pop = comboAge < 0.15f ? Mathf.Lerp(1.6f, 1f, comboAge / 0.15f) : 1f;
                comboText.rectTransform.localScale = Vector3.one * pop;
                Color c = comboText.color; c.a = Mathf.Clamp01(left / 0.3f); comboText.color = c;
            }
        }
    }

    private void Place(Popup p, float t)
    {
        Camera cam = KillCam.ViewCamera;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(p.world);
        if (sp.z <= 0f) { p.text.gameObject.SetActive(false); p.active = false; return; }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local);
        // Upar uchhal kar dheema (ease-out), halka side drift
        float ease = 1f - (1f - t) * (1f - t);
        local += new Vector2(p.drift * ease, risePixels * ease);
        p.rt.anchoredPosition = local;

        float pop = t < 0.12f ? Mathf.Lerp(1.7f, 1f, t / 0.12f) : 1f;
        p.rt.localScale = Vector3.one * (p.baseScale * pop);

        Color c = p.text.color;
        c.a = t < 0.6f ? 1f : Mathf.Clamp01((1f - t) / 0.4f);
        p.text.color = c;
    }
}
