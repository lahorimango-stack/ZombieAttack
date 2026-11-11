using System;
using UnityEngine;

/// <summary>
/// GAME ECONOMY - ek hi currency (coins).
///
/// KAMANA: har zombie (1-6, boss 30/60), level jeet bonus x stars (1x / 1.25x / 1.5x), roz ka FREE crate (streak ke saath).
/// KHARCH:
///   1. Weapon khareedna - level pahunchne par "available", phir coins se khareedo (knife muft)
///   2. Weapon upgrade (5 level) - har level +12% damage, har 2 level par ek draw mein +1 weapon
///   3. Armor (10 level) - player ki health +8% har level
/// IAP: coin packs + ek dafa Starter Pack (IAPManager)
///
/// Save: PlayerPrefs (project ka mojooda save tareeqa).
/// </summary>
public static class Economy
{
    public const int MaxWeaponLevel = 5;
    public const int MaxArmorLevel = 10;

    public static event Action OnChanged;

    private static void Changed() { PlayerPrefs.Save(); OnChanged?.Invoke(); }

    // ---------------- Coins ----------------
    public static int Coins => GameManager.Instance != null ? GameManager.Instance.Coins : PlayerPrefs.GetInt("PlayerCoins", 0);

    public static bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendCoins(amount)) return false;
        Changed();
        return true;
    }

    public static void Grant(int amount)
    {
        if (amount <= 0) return;
        GameManager.Instance?.AddCoins(amount);
        Changed();
    }

    // ---------------- Weapons ----------------
    public static bool IsOwned(WeaponDataSO w) => w != null && (w.coinPrice <= 0 || PlayerPrefs.GetInt("own_" + w.weaponId, 0) == 1);

    public static void SetOwned(WeaponDataSO w)
    {
        if (w == null) return;
        PlayerPrefs.SetInt("own_" + w.weaponId, 1);
        Changed();
    }

    public static bool TryBuyWeapon(WeaponDataSO w)
    {
        if (w == null || IsOwned(w)) return false;
        if (!TrySpend(w.coinPrice)) return false;
        SetOwned(w);
        return true;
    }

    public static int WeaponLevel(WeaponDataSO w) => w == null ? 0 : PlayerPrefs.GetInt("lvl_" + w.weaponId, 0);

    /// <summary> Upgrade ki qeemat: weapon jitna mehnga, upgrade utna mehnga, har level par barhti </summary>
    public static int UpgradeCost(WeaponDataSO w)
    {
        int lvl = WeaponLevel(w);
        float baseCost = w.coinPrice * 0.35f + 90f;
        return Mathf.RoundToInt(baseCost * Mathf.Pow(lvl + 1, 1.4f) / 10f) * 10;
    }

    public static bool TryUpgradeWeapon(WeaponDataSO w)
    {
        if (w == null || !IsOwned(w) || WeaponLevel(w) >= MaxWeaponLevel) return false;
        if (!TrySpend(UpgradeCost(w))) return false;
        PlayerPrefs.SetInt("lvl_" + w.weaponId, WeaponLevel(w) + 1);
        Changed();
        return true;
    }

    public static float DamageMultiplier(WeaponDataSO w) => 1f + 0.12f * WeaponLevel(w);
    public static int ExtraCount(WeaponDataSO w) => WeaponLevel(w) / 2;
    public static int EffectiveMaxCount(WeaponDataSO w) => w == null ? 0 : w.maxCount + ExtraCount(w);

    // ---------------- Armor ----------------
    public static int ArmorLevel => PlayerPrefs.GetInt("armor_lvl", 0);
    public static float ArmorMultiplier => 1f + 0.08f * ArmorLevel;
    public static int ArmorCost => Mathf.RoundToInt(120f * Mathf.Pow(1.42f, ArmorLevel) / 10f) * 10;

    public static bool TryUpgradeArmor()
    {
        if (ArmorLevel >= MaxArmorLevel) return false;
        if (!TrySpend(ArmorCost)) return false;
        PlayerPrefs.SetInt("armor_lvl", ArmorLevel + 1);
        Changed();
        return true;
    }

    public static void AddArmorLevels(int n)
    {
        PlayerPrefs.SetInt("armor_lvl", Mathf.Min(MaxArmorLevel, ArmorLevel + n));
        Changed();
    }

    // ---------------- Level reward ----------------
    /// <summary> 3 star = 1.5x, 2 star = 1.25x, 1 star = 1x </summary>
    public static int Stars(float healthPercent) => healthPercent > 0.7f ? 3 : (healthPercent > 0.35f ? 2 : 1);
    public static float StarMultiplier(int stars) => stars >= 3 ? 1.5f : (stars == 2 ? 1.25f : 1f);

    // ---------------- Daily free crate ----------------
    private static string Today => DateTime.UtcNow.ToString("yyyyMMdd");

    public static bool CanClaimDaily => PlayerPrefs.GetString("daily_last", "") != Today;

    /// <summary> Lagataar din = zyada (100 se 250 tak) </summary>
    public static int DailyAmount
    {
        get
        {
            int streak = PlayerPrefs.GetInt("daily_streak", 0);
            string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
            if (PlayerPrefs.GetString("daily_last", "") != yesterday) streak = 0;
            return 100 + Mathf.Min(streak, 6) * 25;
        }
    }

    public static int ClaimDaily()
    {
        if (!CanClaimDaily) return 0;
        string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
        int amount = DailyAmount;
        int streak = PlayerPrefs.GetString("daily_last", "") == yesterday ? PlayerPrefs.GetInt("daily_streak", 0) + 1 : 1;
        PlayerPrefs.SetInt("daily_streak", streak);
        PlayerPrefs.SetString("daily_last", Today);
        Grant(amount);
        return amount;
    }

    /// <summary> Agle crate tak waqt (UI timer) </summary>
    public static TimeSpan TimeToNextDaily => DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;

    // ---------------- IAP ledger (ek order ka inaam do dafa nahi) ----------------
    public static bool IsOrderProcessed(string orderId) => !string.IsNullOrEmpty(orderId) && PlayerPrefs.GetInt("iap_" + orderId, 0) == 1;
    public static void MarkOrderProcessed(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        PlayerPrefs.SetInt("iap_" + orderId, 1);
        PlayerPrefs.Save();
    }

    public static bool StarterPackOwned
    {
        get => PlayerPrefs.GetInt("starter_pack", 0) == 1;
        set { PlayerPrefs.SetInt("starter_pack", value ? 1 : 0); Changed(); }
    }
}
