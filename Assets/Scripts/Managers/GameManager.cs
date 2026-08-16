using UnityEngine;
using CandyCoded.HapticFeedback;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- References ---")]
    [Tooltip("Scene ka WeaponFormationController assign karein")]
    public WeaponFormationController weaponController;

    [Header("--- Haptic Feedback Settings ---")]
    [Tooltip("Haptics on ya off karne ka master switch (Settings UI ke liye)")]
    public bool isHapticEnabled = true;

    void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Agar multi-scene hai to enable karein
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (weaponController == null)
        {
            weaponController = FindFirstObjectByType<WeaponFormationController>();
        }
    }

    #region Haptic Trigger Methods

    /// <summary>
    /// Light click vibration (e.g. Knife spawn hone par, UI button click par)
    /// </summary>
    public void TriggerLightHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.LightFeedback();
    }

    /// <summary>
    /// Medium vibration (e.g. Finger UP shoot hone par, Target hit hone par)
    /// </summary>
    public void TriggerMediumHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.MediumFeedback();
    }

    /// <summary>
    /// Heavy vibration (e.g. Explosion, Level Win, Boss Death par)
    /// </summary>
    public void TriggerHeavyHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.HeavyFeedback();
    }

    /// <summary>
    /// Settings menu se haptics toggle karne ke liye
    /// </summary>
    public void ToggleHaptics(bool state)
    {
        isHapticEnabled = state;
    }

    #endregion
}