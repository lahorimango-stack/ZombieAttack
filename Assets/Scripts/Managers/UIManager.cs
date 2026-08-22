using UnityEngine;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("--- Top HUD ---")]
    [Tooltip("Screen ke top par Level text (e.g. 'Level 1')")]
    [SerializeField] private TMP_Text levelText;

    [Header("--- Win & Fail Panels ---")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject failPanel;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        HideAllPanels();
    }

    public void UpdateLevelText(int levelNumber)
    {
        if (levelText != null)
        {
            levelText.text = $"Level {levelNumber}";
        }
    }

    public void ShowWinScreen()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }
    }

    public void ShowFailScreen()
    {
        if (failPanel != null)
        {
            failPanel.SetActive(true);
            failPanel.transform.localScale = Vector3.zero;
            failPanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }
    }

    public void HideAllPanels()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
    }

    // UI BUTTON METHODS
    public void OnNextButtonClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        GameManager.Instance?.TriggerLightHaptic();
        LevelManager.Instance?.LoadNextLevel();
    }

    public void OnRestartButtonClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        GameManager.Instance?.TriggerLightHaptic();
        LevelManager.Instance?.RestartCurrentLevel();
    }
}