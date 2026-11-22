using System;
using UnityEngine;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- Core References ---")]
    [Tooltip("Scene ka WeaponFormationController yahan direct Drag & Drop karein")]
    public WeaponFormationController weaponController;
    public WeaponFormationController WeaponController => weaponController;

    [Header("--- Haptic Feedback Settings ---")]
    public bool isHapticEnabled = true;

    public int Coins { get; private set; }
    public event Action<int> OnCoinsChanged;

    private const string CoinsKey = "PlayerCoins";
    private const string HapticKey = "Haptic_Enabled";

    void Awake()
    {
        // Singleton Pattern (No FindObject)
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        Application.targetFrameRate = 60;

        // DOTween: pehle se jagah (capacity) - gameplay ke beech list barhne ki GC atak nahi.
        // Safe mode: destroy hue object ki tween crash nahi karti. Sirf errors log (mobile par log bhi mehnga)
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly).SetCapacity(400, 60);
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Coins = PlayerPrefs.GetInt(CoinsKey, 0);
        isHapticEnabled = PlayerPrefs.GetInt(HapticKey, 1) == 1;
    }

    #region Coins

    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        Coins = Mathf.Max(0, Coins + amount);
        PlayerPrefs.SetInt(CoinsKey, Coins);
        OnCoinsChanged?.Invoke(Coins);
    }

    public bool TrySpendCoins(int amount)
    {
        if (Coins < amount) return false;
        AddCoins(-amount);
        return true;
    }

    #endregion

    #region Haptic Trigger Methods

    // GameHaptics: phone ki motor ke hisaab se chhoti/halki vibration + waqfa (low-end par light band)
    public void TriggerLightHaptic()
    {
        if (isHapticEnabled) GameHaptics.Play(GameHaptics.Strength.Light);
    }

    public void TriggerMediumHaptic()
    {
        if (isHapticEnabled) GameHaptics.Play(GameHaptics.Strength.Medium);
    }

    public void TriggerHeavyHaptic()
    {
        if (isHapticEnabled) GameHaptics.Play(GameHaptics.Strength.Heavy);
    }

    public void ToggleHaptics(bool state)
    {
        isHapticEnabled = state;
        PlayerPrefs.SetInt(HapticKey, state ? 1 : 0);
    }

    #endregion
}
