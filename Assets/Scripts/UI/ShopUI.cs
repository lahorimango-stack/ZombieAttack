using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// SHOP panel: roz ka FREE crate, Starter Pack (ek dafa), 3 coin packs (IAP), Armor upgrade (coins).
/// Qeematein store se (localized). Sab references scene mein.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("--- Daily Crate ---")]
    [SerializeField] private Button dailyButton;
    [SerializeField] private TMP_Text dailyLabel;
    [SerializeField] private TMP_Text dailyAmount;
    [SerializeField] private Transform dailyIcon;

    [Header("--- Starter Pack ---")]
    [SerializeField] private GameObject starterRoot;
    [SerializeField] private Button starterButton;
    [SerializeField] private TMP_Text starterPrice;

    [Header("--- Coin Packs (IAPManager.Catalog order 0..2) ---")]
    [SerializeField] private Button[] packButtons;
    [SerializeField] private TMP_Text[] packPrices;
    [SerializeField] private TMP_Text[] packAmounts;

    [Header("--- Armor ---")]
    [SerializeField] private Button armorButton;
    [SerializeField] private TMP_Text armorLevelText;
    [SerializeField] private TMP_Text armorCost;

    [Header("--- Feedback ---")]
    [SerializeField] private TMP_Text messageText;

    private float nextTick;

    void Awake()
    {
        if (dailyButton != null) dailyButton.onClick.AddListener(ClaimDaily);
        if (starterButton != null) starterButton.onClick.AddListener(() => Buy(IAPManager.StarterPackId));
        if (packButtons != null)
            for (int i = 0; i < packButtons.Length; i++)
            {
                int idx = i;
                if (packButtons[i] != null) packButtons[i].onClick.AddListener(() => Buy(IAPManager.Catalog[idx].id));
            }
        if (armorButton != null) armorButton.onClick.AddListener(UpgradeArmor);
    }

    void OnEnable()
    {
        Economy.OnChanged += Refresh;
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnProductsReady += Refresh;
            IAPManager.Instance.OnPurchaseSucceeded += OnBought;
            IAPManager.Instance.OnPurchaseFailedMsg += ShowMessage;
        }
        if (messageText != null) messageText.text = "";
        Refresh();
    }

    void OnDisable()
    {
        Economy.OnChanged -= Refresh;
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnProductsReady -= Refresh;
            IAPManager.Instance.OnPurchaseSucceeded -= OnBought;
            IAPManager.Instance.OnPurchaseFailedMsg -= ShowMessage;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + 1f;
        RefreshDaily(); // timer har second
    }

    public void Refresh()
    {
        RefreshDaily();
        var iap = IAPManager.Instance;
        if (starterRoot != null) starterRoot.SetActive(!Economy.StarterPackOwned);
        if (starterPrice != null) starterPrice.text = iap != null ? iap.PriceOf(IAPManager.StarterPackId) : "...";
        if (starterButton != null) starterButton.interactable = iap != null && iap.CanBuy(IAPManager.StarterPackId);

        for (int i = 0; packButtons != null && i < packButtons.Length && i < 3; i++)
        {
            string id = IAPManager.Catalog[i].id;
            if (packPrices != null && i < packPrices.Length && packPrices[i] != null) packPrices[i].text = iap != null ? iap.PriceOf(id) : "...";
            if (packAmounts != null && i < packAmounts.Length && packAmounts[i] != null) packAmounts[i].text = IAPManager.Catalog[i].coins.ToString("N0");
            if (packButtons[i] != null) packButtons[i].interactable = iap != null && iap.CanBuy(id);
        }

        int lvl = Economy.ArmorLevel;
        if (armorLevelText != null) armorLevelText.text = lvl >= Economy.MaxArmorLevel ? $"ARMOR MAX  +{lvl * 8}% HP" : $"ARMOR LV {lvl}  +{lvl * 8}% HP";
        if (armorCost != null) armorCost.text = lvl >= Economy.MaxArmorLevel ? "MAX" : Economy.ArmorCost.ToString("N0");
        if (armorButton != null) armorButton.interactable = lvl < Economy.MaxArmorLevel && Economy.Coins >= Economy.ArmorCost;
    }

    private void RefreshDaily()
    {
        bool can = Economy.CanClaimDaily;
        if (dailyButton != null) dailyButton.interactable = can;
        if (dailyAmount != null) dailyAmount.text = "+" + Economy.DailyAmount;
        if (dailyLabel != null)
        {
            var t = Economy.TimeToNextDaily;
            dailyLabel.text = can ? "CLAIM" : $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
        if (dailyIcon != null && can && !DOTween.IsTweening(dailyIcon))
            dailyIcon.DOPunchRotation(new Vector3(0f, 0f, 8f), 0.6f, 6, 0.5f).SetUpdate(true).SetDelay(1f);
    }

    private void ClaimDaily()
    {
        int got = Economy.ClaimDaily();
        if (got <= 0) return;
        SoundManager.Instance?.PlayUnlock();
        GameManager.Instance?.TriggerMediumHaptic();
        if (dailyIcon != null) { dailyIcon.DOKill(true); dailyIcon.DOPunchScale(Vector3.one * 0.4f, 0.4f, 8, 0.6f).SetUpdate(true); }
        ShowMessage($"+{got} COINS!");
        Refresh();
    }

    private void Buy(string productId)
    {
        SoundManager.Instance?.PlayButtonClick();
        if (IAPManager.Instance == null) { ShowMessage("Store not available"); return; }
        ShowMessage("Connecting to store...");
        IAPManager.Instance.Buy(productId);
    }

    private void OnBought(string productId)
    {
        ShowMessage(productId == IAPManager.StarterPackId ? "STARTER PACK UNLOCKED!" : "COINS ADDED!");
        Refresh();
    }

    private void UpgradeArmor()
    {
        if (!Economy.TryUpgradeArmor()) return;
        SoundManager.Instance?.PlayUnlock();
        GameManager.Instance?.TriggerMediumHaptic();
        if (armorButton != null) { armorButton.transform.DOKill(true); armorButton.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 8, 0.6f).SetUpdate(true); }
        Refresh();
    }

    private void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        messageText.DOKill();
        messageText.alpha = 1f;
        messageText.DOFade(0f, 0.6f).SetDelay(2f).SetUpdate(true);
    }
}
