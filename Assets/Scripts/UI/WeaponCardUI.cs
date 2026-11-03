using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Weapon Select panel ka ek card. Scene mein bana hota hai, weapon data Inspector se assign.
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

    [Header("--- Frame Sprites ---")]
    [SerializeField] private Sprite normalFrame;
    [SerializeField] private Sprite selectedFrame;
    [SerializeField] private Sprite lockedFrame;

    public WeaponDataSO Weapon => weapon;
    public event Action<WeaponCardUI> OnClicked;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Refresh(bool unlocked, bool selected)
    {
        if (weapon == null) { gameObject.SetActive(false); return; }

        if (iconImage != null)
        {
            iconImage.sprite = weapon.icon;
            iconImage.enabled = weapon.icon != null;
            iconImage.color = unlocked ? Color.white : new Color(0.15f, 0.15f, 0.2f, 1f);
        }
        if (nameText != null) nameText.text = unlocked ? weapon.displayName.ToUpper() : "???";

        if (statsRoot != null) statsRoot.SetActive(unlocked);
        if (lockedRoot != null) lockedRoot.SetActive(!unlocked);
        if (lockedText != null) lockedText.text = $"LEVEL {weapon.unlockLevel}";
        if (equippedBadge != null) equippedBadge.SetActive(unlocked && selected);

        if (damageBar != null) damageBar.fillAmount = weapon.DamageRating;
        if (speedBar != null) speedBar.fillAmount = weapon.SpeedRating;
        if (areaBar != null) areaBar.fillAmount = Mathf.Max(0.08f, weapon.AreaRating);
        if (damageValue != null) damageValue.text = DamageText(weapon);
        if (speedValue != null) speedValue.text = Mathf.RoundToInt(weapon.launchSpeed).ToString();
        if (areaValue != null) areaValue.text = AreaText(weapon);

        if (frameImage != null)
        {
            Sprite s = !unlocked ? lockedFrame : (selected ? selectedFrame : normalFrame);
            if (s != null) frameImage.sprite = s;
        }

        if (button != null) button.interactable = unlocked;

        transform.DOKill();
        transform.localScale = selected ? Vector3.one * 1.04f : Vector3.one;
    }

    public static string DamageText(WeaponDataSO w)
    {
        float dmg = w.damage + w.explosionDamage + w.fireZoneDps * w.fireZoneDuration;
        return Mathf.RoundToInt(dmg).ToString();
    }

    /// <summary> "3m" blast, "x3" aar paar, "-" single </summary>
    public static string AreaText(WeaponDataSO w)
    {
        if (w.explosionRadius > 0f) return $"{w.explosionRadius:0.#}m";
        if (w.pierceCount > 0) return $"x{w.pierceCount + 1}";
        return "1";
    }

    public void PlaySelectPunch()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 8, 0.8f).SetUpdate(true)
                 .OnComplete(() => transform.localScale = Vector3.one * 1.04f);
    }
}
