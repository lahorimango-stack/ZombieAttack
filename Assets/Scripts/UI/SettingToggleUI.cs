using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sound / Music / Haptic on-off button (icon swap)
/// </summary>
public class SettingToggleUI : MonoBehaviour
{
    public enum SettingType { Sound, Music, Haptic }

    [SerializeField] private SettingType setting;
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite onIcon;
    [SerializeField] private Sprite offIcon;
    [SerializeField] private Sprite onBackground;
    [SerializeField] private Sprite offBackground;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(Toggle);
    }

    void OnEnable()
    {
        Refresh();
    }

    private bool GetState()
    {
        switch (setting)
        {
            case SettingType.Sound: return SoundManager.Instance == null || !SoundManager.Instance.isSfxMuted;
            case SettingType.Music: return SoundManager.Instance == null || !SoundManager.Instance.isMusicMuted;
            default: return GameManager.Instance == null || GameManager.Instance.isHapticEnabled;
        }
    }

    private void Toggle()
    {
        bool newState = !GetState();
        switch (setting)
        {
            case SettingType.Sound: SoundManager.Instance?.ToggleSFX(newState); break;
            case SettingType.Music: SoundManager.Instance?.ToggleMusic(newState); break;
            default:
                GameManager.Instance?.ToggleHaptics(newState);
                if (newState) GameManager.Instance?.TriggerMediumHaptic();
                break;
        }
        SoundManager.Instance?.PlayButtonClick();
        Refresh();
    }

    public void Refresh()
    {
        bool on = GetState();
        if (iconImage != null && onIcon != null && offIcon != null) iconImage.sprite = on ? onIcon : offIcon;
        if (iconImage != null && (onIcon == null || offIcon == null)) iconImage.color = on ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        if (backgroundImage != null && onBackground != null && offBackground != null) backgroundImage.sprite = on ? onBackground : offBackground;
    }
}
