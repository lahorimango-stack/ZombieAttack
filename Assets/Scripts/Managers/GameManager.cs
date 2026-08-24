using UnityEngine;
using CandyCoded.HapticFeedback;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- Core References ---")]
    [Tooltip("Scene ka WeaponFormationController yahan direct Drag & Drop karein")]
    public WeaponFormationController weaponController;
    public WeaponFormationController WeaponController => weaponController;

    [Header("--- Haptic Feedback Settings ---")]
    public bool isHapticEnabled = true;

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
    }

    #region Haptic Trigger Methods

    public void TriggerLightHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.LightFeedback();
    }

    public void TriggerMediumHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.MediumFeedback();
    }

    public void TriggerHeavyHaptic()
    {
        if (!isHapticEnabled) return;
        HapticFeedback.HeavyFeedback();
    }

    public void ToggleHaptics(bool state)
    {
        isHapticEnabled = state;
    }

    #endregion
}