using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Armory ka ek card. Halatein:
///  LOCKED  - level abhi nahi pahuncha ("UNLOCKS AT LEVEL 9")
///  BUY     - level pahunch gaya, coins se khareedo
///  OWNED   - card dabao = equip, neeche UPGRADE (level 0-5, har level +12% damage, har 2 par +1 weapon)
/// Scene mein bana hota hai, weapon data Inspector se.
/// </summary>
public class WeaponCardUI : MonoBehaviour
{
    [Header("--- Data ---")]
    [SerializeField] private WeaponDataSO weapon;

    [Header("--- References ---")]
    [SerializeField] private Button button;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image damageBar;
    [SerializeField] private Image speedBar;
    [SerializeField] private Image areaBar;
    [SerializeField] private TMP_Text damageValue;
    [SerializeField] private TMP_Text speedValue;
    [SerializeField] private TMP_Text areaValue;
    [SerializeField] private GameObject statsRoot;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;
    [SerializeField] private GameObject equippedBadge;

    [Header("--- Economy ---")]
    [Tooltip("BUY / UPGRADE button")]
    [SerializeField] private Button actionButton;
    [SerializeField] private Image actionImage;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text actionCost;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Sprite buySprite;
    [SerializeField] private Sprite upgradeSprite;
    [SerializeField] private Sprite disabledSprite;

    [Header("--- Frame Sprites ---")]
    [SerializeField] private Sprite normalFrame;
    [SerializeField] private Sprite selectedFrame;
    [SerializeField] private Sprite lockedFrame;

    public WeaponDataSO Weapon => weapon;
    public event Action<WeaponCardUI> OnClicked;
    public event Action<WeaponCardUI> OnAction;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(() => OnClicked?.Invoke(this));
        if (actionButton != null) actionButton.onClick.AddListener(() => OnAction?.Invoke(this));
    }

    public void Refresh(bool available, bool owned, bool selected)
    {
        if (weapon == null) { gameObject.SetActive(false); return; }
        bool visible = available || owned;

        if (iconImage != null)
        {
            iconImage.sprite = weapon.icon;
            iconImage.enabled = weapon.icon != null;
            iconImage.color = owned ? Color.white : (available ? new Color(0.55f, 0.55f, 0.6f, 1f) : new Color(0.12f, 0.12f, 0.16f, 1f));
        }
        if (nameText != null) nameText.text = visible ? weapon.displayName.ToUpper() : "???";

        if (statsRoot != null) statsRoot.SetActive(visible);
        if (lockedRoot != null) lockedRoot.SetActive(!visible);
        if (lockedText != null) lockedText.text = $"LEVEL {weapon.unlockLevel}";
        if (equippedBadge != null) equippedBadge.SetActive(owned && selected);

        // Asal (upgrade ke saath) taqat
        float mult = Economy.DamageMultiplier(weapon);
        if (damageBar != null) damageBar.fillAmount = Mathf.Clamp01(weapon.DamageRating * mult);
        if (speedBar != null) speedBar.fillAmount = weapon.SpeedRating;
        if (areaBar != null) areaBar.fillAmount = Mathf.Max(0.08f, weapon.AreaRating);
        if (damageValue != null) damageValue.text = Mathf.RoundToInt(DamageNumber(weapon) * mult).ToString();
        if (speedValue != null) speedValue.text = Mathf.RoundToInt(weapon.launchSpeed).ToString();
        if (areaValue != null) areaValue.text = AreaText(weapon);

        int lvl = Economy.WeaponLevel(weapon);
        if (levelText != null)
        {
            levelText.gameObject.SetActive(owned);
            levelText.text = lvl >= Economy.MaxWeaponLevel ? "MAX" : $"LV {lvl + 1}";
        }

        // BUY / UPGRADE / MAX
        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(visible);
            int cost = 0;
            string label;
            if (!owned) { cost = weapon.coinPrice; label = "BUY"; }
            else if (lvl < Economy.MaxWeaponLevel) { cost = Economy.UpgradeCost(weapon); label = "UPGRADE"; }
            else label = "MAXED";
            bool can = (cost > 0 && Economy.Coins >= cost) || (!owned && cost == 0);
            if (owned && lvl >= Economy.MaxWeaponLevel) can = false;
            if (actionLabel != null) actionLabel.text = label;
            if (actionCost != null) { actionCost.gameObject.SetActive(cost > 0); actionCost.text = cost.ToString("N0"); actionCost.color = Economy.Coins >= cost ? new Color(1f, 0.86f, 0.3f) : new Color(1f, 0.45f, 0.4f); }
            if (actionImage != null)
            {
                Sprite s = !can ? disabledSprite : (owned ? upgradeSprite : buySprite);
                if (s != null) actionImage.sprite = s;
            }
            actionButton.interactable = can;
        }

        if (frameImage != null)
        {
            Sprite s = !owned ? lockedFrame : (selected ? selectedFrame : normalFrame);
            if (s != null) frameImage.sprite = s;
        }

        if (button != null) button.interactable = owned;

        transform.DOKill();
        transform.localScale = selected ? Vector3.one * 1.03f : Vector3.one;
    }

    private static float DamageNumber(WeaponDataSO w) => w.damage + w.explosionDamage + w.fireZoneDps * w.fireZoneDuration;

    public static string DamageText(WeaponDataSO w) => Mathf.RoundToInt(DamageNumber(w) * Economy.DamageMultiplier(w)).ToString();

    /// <summary> "3m" blast, "x3" aar paar, ricochet "x4", bijli "x5" </summary>
    public static string AreaText(WeaponDataSO w)
    {
        if (w.explosionRadius > 0f) return $"{w.explosionRadius:0.#}m";
        if (w.chainCount > 0) return $"x{w.chainCount + 1}";
        if (w.ricochetCount > 0) return $"x{w.ricochetCount + 1}";
        if (w.pierceCount > 0) return $"x{w.pierceCount + 1}";
        return "1";
    }

    public void PlaySelectPunch()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 8, 0.8f).SetUpdate(true)
                 .OnComplete(() => transform.localScale = Vector3.one * 1.03f);
    }

    /// <summary> Khareedne / upgrade par chamak </summary>
    public void PlayPurchasePunch()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 10, 0.7f).SetUpdate(true);
    }
}
