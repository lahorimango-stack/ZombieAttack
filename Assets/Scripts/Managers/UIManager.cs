using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("--- 1. Top Bar (hamesha visible) ---")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private RectTransform coinIcon;

    [Header("--- 2. Home Screen ---")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private TMP_Text homeLevelText;
    [SerializeField] private Image homeWeaponIcon;
    [SerializeField] private TMP_Text homeWeaponName;

    [Header("--- 3. Gameplay HUD ---")]
    [SerializeField] private GameObject hudPanel;
    [Tooltip("Screen ke top par Level text (e.g. 'Level 1')")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image barricadeFill;
    [SerializeField] private TMP_Text barricadeText;
    [SerializeField] private Gradient barricadeColor;
    [SerializeField] private Image hudWeaponIcon;
    [SerializeField] private TMP_Text hudWeaponName;
    [SerializeField] private TMP_Text hudWeaponPower;
    [SerializeField] private Image damageVignette;
    [SerializeField] private GameObject tutorialHint;

    [Header("--- 3b. Boss Bar (HUD ke upar) ---")]
    [SerializeField] private GameObject bossBarRoot;
    [SerializeField] private Image bossBarFill;
    [SerializeField] private TMP_Text bossBarName;

    [Header("--- 4. Wave Banner ---")]
    [SerializeField] private CanvasGroup waveBanner;
    [SerializeField] private TMP_Text waveBannerText;
    [SerializeField] private TMP_Text waveBannerSubText;
    [SerializeField] private Image waveBannerBackground;
    [SerializeField] private Sprite normalBannerSprite;
    [SerializeField] private Sprite dangerBannerSprite;

    [Header("--- 5. Weapon Select Panel ---")]
    [SerializeField] private GameObject weaponPanel;
    [SerializeField] private List<WeaponCardUI> weaponCards = new List<WeaponCardUI>();

    [Header("--- 6. Settings & Pause ---")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("--- 7. Win & Fail Panels ---")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TMP_Text winTitleText;
    [SerializeField] private TMP_Text winCoinsText;
    [SerializeField] private Image[] winStars;
    [SerializeField] private Sprite starOnSprite;
    [SerializeField] private Sprite starOffSprite;
    [SerializeField] private GameObject unlockRoot;
    [SerializeField] private Image unlockIcon;
    [SerializeField] private TMP_Text unlockNameText;

    [SerializeField] private GameObject failPanel;
    [SerializeField] private TMP_Text failInfoText;

    private const string TutorialKey = "TutorialDone";
    private bool pausedByPanel;
    private int hudLevelNumber = 1;
    private int hudWave, hudTotalWaves;
    private HumanoidEnemy trackedBoss;
    private Tween vignetteTween;
    private Tween coinTween;
    private int shownCoins;

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

        foreach (var card in weaponCards)
            if (card != null) card.OnClicked += OnWeaponCardClicked;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCoinsChanged += OnCoinsChanged;
            shownCoins = GameManager.Instance.Coins;
            if (coinText != null) coinText.text = shownCoins.ToString();

            if (GameManager.Instance.WeaponController != null)
            {
                GameManager.Instance.WeaponController.OnWeaponChanged += _ => RefreshWeaponIcons();
                GameManager.Instance.WeaponController.OnWeaponsLaunched += OnWeaponsLaunched;
            }
        }

        if (Barricade.Instance != null)
            Barricade.Instance.OnHealthChanged += UpdateBarricadeBar;

        if (damageVignette != null)
        {
            Color c = damageVignette.color; c.a = 0f; damageVignette.color = c;
        }

        RefreshWeaponIcons();
    }

    #region Screens

    public void ShowHome(int levelNumber)
    {
        HideAllPanels();
        if (hudPanel != null) hudPanel.SetActive(false);
        if (homeLevelText != null) homeLevelText.text = $"LEVEL {levelNumber}";
        RefreshWeaponIcons();
        PopIn(homePanel);
    }

    public void ShowGameplayHUD(int levelNumber, LevelDataSO data)
    {
        HideAllPanels();
        if (homePanel != null) homePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);
        hudWave = 0;
        hudTotalWaves = 0;
        UpdateLevelText(levelNumber);
        UpdateBarricadeBar(1f);
        HideBossBar();

        bool showTutorial = PlayerPrefs.GetInt(TutorialKey, 0) == 0;
        if (tutorialHint != null) tutorialHint.SetActive(showTutorial);

        RefreshWeaponIcons();
    }

    public void UpdateLevelText(int levelNumber)
    {
        hudLevelNumber = levelNumber;
        if (levelText != null)
        {
            levelText.text = hudTotalWaves > 0
                ? $"LEVEL {levelNumber}  <size=60%><color=#FFD34D>WAVE {hudWave}/{hudTotalWaves}</color></size>"
                : $"LEVEL {levelNumber}";
        }
    }

    /// <summary> HUD par mojooda wave (wave system saaf nazar aaye) </summary>
    public void SetWave(int wave, int totalWaves)
    {
        hudWave = wave;
        hudTotalWaves = totalWaves;
        UpdateLevelText(hudLevelNumber);
    }

    /// <summary> Boss aate hi HUD ke upar bara health bar (boss qareeb ho to bhi nazar aaye) </summary>
    public void ShowBossBar(HumanoidEnemy boss, string title)
    {
        if (bossBarRoot == null || boss == null) return;
        HideBossBar();
        trackedBoss = boss;
        boss.OnDamaged += OnBossDamaged;
        boss.OnDied += OnBossDied;
        if (bossBarName != null) bossBarName.text = title;
        if (bossBarFill != null) bossBarFill.fillAmount = boss.HealthPercent;
        bossBarRoot.SetActive(true);
        var rt = bossBarRoot.transform as RectTransform;
        rt.DOKill();
        rt.localScale = new Vector3(1.3f, 0f, 1f);
        rt.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    public void HideBossBar()
    {
        if (trackedBoss != null)
        {
            trackedBoss.OnDamaged -= OnBossDamaged;
            trackedBoss.OnDied -= OnBossDied;
            trackedBoss = null;
        }
        if (bossBarRoot != null) bossBarRoot.SetActive(false);
    }

    private void OnBossDamaged(HumanoidEnemy boss)
    {
        if (bossBarFill == null) return;
        bossBarFill.DOKill();
        bossBarFill.DOFillAmount(boss.HealthPercent, 0.2f);
        bossBarFill.transform.parent.DOKill(true);
        bossBarFill.transform.parent.DOShakePosition(0.15f, 6f, 20);
    }

    private void OnBossDied(HumanoidEnemy boss)
    {
        if (bossBarFill != null) bossBarFill.fillAmount = 0f;
        if (bossBarRoot != null)
            (bossBarRoot.transform as RectTransform).DOScaleY(0f, 0.35f).SetDelay(0.4f).SetEase(Ease.InBack)
                .OnComplete(HideBossBar);
    }

    public void UpdateProgress(int killed, int total)
    {
        float t = total > 0 ? (float)killed / total : 0f;
        if (progressFill != null)
        {
            progressFill.DOKill();
            progressFill.DOFillAmount(t, 0.25f).SetEase(Ease.OutQuad);
        }
        if (progressText != null) progressText.text = $"{killed}/{total}";
    }

    private void UpdateBarricadeBar(float percent)
    {
        if (barricadeFill != null)
        {
            barricadeFill.DOKill();
            barricadeFill.DOFillAmount(percent, 0.2f);
            if (barricadeColor != null) barricadeFill.color = barricadeColor.Evaluate(percent);
        }
        if (barricadeText != null) barricadeText.text = $"{Mathf.CeilToInt(percent * 100f)}%";
    }

    public void ShowWaveBanner(int wave, int totalWaves, bool isFinal, bool isBoss)
    {
        if (waveBanner == null) return;

        if (waveBannerText != null)
            waveBannerText.text = isBoss ? "BOSS INCOMING!" : (isFinal ? "FINAL WAVE!" : $"WAVE {wave}/{totalWaves}");
        if (waveBannerSubText != null)
            waveBannerSubText.text = isBoss ? "Bring everything you've got" : (isFinal ? "Hold the line!" : "Zombies are coming...");
        if (waveBannerBackground != null)
        {
            Sprite s = (isFinal || isBoss) ? dangerBannerSprite : normalBannerSprite;
            if (s != null) waveBannerBackground.sprite = s;
        }

        RectTransform rt = waveBanner.transform as RectTransform;
        waveBanner.gameObject.SetActive(true);
        waveBanner.DOKill();
        rt.DOKill();
        waveBanner.alpha = 0f;
        rt.localScale = Vector3.one * 1.6f;

        DOTween.Sequence().SetTarget(waveBanner)
            .Append(waveBanner.DOFade(1f, 0.2f))
            .Join(rt.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(1.4f)
            .Append(waveBanner.DOFade(0f, 0.3f))
            .OnComplete(() => waveBanner.gameObject.SetActive(false));
    }

    public void ShowWinScreen()
    {
        ShowWinScreen(LevelManager.Instance != null ? LevelManager.Instance.CurrentDisplayLevel - 1 : 1, 0, null);
    }

    public void ShowWinScreen(int completedLevel, int coinsEarned, WeaponDataSO unlockedWeapon)
    {
        if (hudPanel != null) hudPanel.SetActive(false);
        if (winTitleText != null) winTitleText.text = $"LEVEL {completedLevel}\nCLEARED!";
        if (winCoinsText != null)
        {
            winCoinsText.text = "+0";
            int shown = 0;
            DOTween.To(() => shown, x => { shown = x; winCoinsText.text = $"+{x}"; }, coinsEarned, 0.8f).SetDelay(0.4f).SetUpdate(true);
        }

        // Stars: barricade health ke hisaab se
        float hp = Barricade.Instance != null ? Barricade.Instance.HealthPercent : 1f;
        int stars = hp > 0.7f ? 3 : (hp > 0.35f ? 2 : 1);
        if (winStars != null)
        {
            for (int i = 0; i < winStars.Length; i++)
            {
                Image star = winStars[i];
                if (star == null) continue;
                bool on = i < stars;
                star.sprite = on ? starOnSprite : starOffSprite;
                star.transform.localScale = Vector3.zero;
                star.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetDelay(0.35f + i * 0.18f).SetUpdate(true);
            }
        }

        if (unlockRoot != null)
        {
            unlockRoot.SetActive(unlockedWeapon != null);
            if (unlockedWeapon != null)
            {
                if (unlockIcon != null) unlockIcon.sprite = unlockedWeapon.icon;
                if (unlockNameText != null) unlockNameText.text = $"NEW WEAPON: {unlockedWeapon.displayName.ToUpper()}";
                unlockRoot.transform.localScale = Vector3.zero;
                unlockRoot.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(1.1f).SetUpdate(true)
                    .OnStart(() => SoundManager.Instance?.PlayUnlock());
            }
        }

        PopIn(winPanel);
    }

    public void ShowFailScreen()
    {
        ShowFailScreen(LevelManager.Instance != null ? LevelManager.Instance.CurrentDisplayLevel : 1, 0, 0);
    }

    public void ShowFailScreen(int level, int kills, int total)
    {
        if (hudPanel != null) hudPanel.SetActive(false);
        if (failInfoText != null) failInfoText.text = $"Zombies killed: {kills}/{total}";
        PopIn(failPanel);
    }

    public void HideAllPanels()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
        if (weaponPanel != null) weaponPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (waveBanner != null) waveBanner.gameObject.SetActive(false);
        ResumeIfPausedByPanel();
    }

    private void PopIn(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
        SoundManager.Instance?.PlayPanelOpen();

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOKill();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.2f).SetUpdate(true);
        }

        // Panel ke andar ka "Window" child pop hoga (background dim static rahega)
        Transform window = panel.transform.Find("Window");
        Transform target = window != null ? window : panel.transform;
        target.DOKill();
        target.localScale = Vector3.one * 0.6f;
        target.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    #endregion

    #region Feedback

    public void FlashDamage()
    {
        if (damageVignette == null) return;
        vignetteTween?.Kill();
        Color c = damageVignette.color; c.a = 0.85f; damageVignette.color = c;
        vignetteTween = damageVignette.DOFade(0f, 0.7f).SetEase(Ease.InQuad);
    }

    public void OnEnemyKilled(Vector3 worldPos, int coins)
    {
        // Har kill par beep nahi - sirf coin counter animate hota hai
    }

    private void OnCoinsChanged(int coins)
    {
        if (coinText == null) return;
        coinTween?.Kill();
        coinTween = DOTween.To(() => shownCoins, x => { shownCoins = x; coinText.text = x.ToString(); }, coins, 0.3f).SetUpdate(true);

        if (coinIcon != null)
        {
            coinIcon.DOKill(true);
            coinIcon.DOPunchScale(Vector3.one * 0.25f, 0.2f, 6, 0.6f).SetUpdate(true);
        }
    }

    private void OnWeaponsLaunched()
    {
        if (tutorialHint != null && tutorialHint.activeSelf)
        {
            tutorialHint.SetActive(false);
            PlayerPrefs.SetInt(TutorialKey, 1);
        }
    }

    #endregion

    #region Weapon Select

    private void RefreshWeaponIcons()
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        WeaponDataSO current = wc != null ? wc.CurrentWeapon : null;

        if (current != null)
        {
            if (homeWeaponIcon != null) homeWeaponIcon.sprite = current.icon;
            if (hudWeaponIcon != null) hudWeaponIcon.sprite = current.icon;
            if (homeWeaponName != null) homeWeaponName.text = current.displayName.ToUpper();
            if (hudWeaponName != null) hudWeaponName.text = current.displayName.ToUpper();
            if (hudWeaponPower != null) hudWeaponPower.text = $"DMG {WeaponCardUI.DamageText(current)}  x{current.maxCount}";
        }

        foreach (var card in weaponCards)
        {
            if (card == null || card.Weapon == null) continue;
            bool unlocked = wc != null && wc.IsUnlocked(card.Weapon);
            card.Refresh(unlocked, card.Weapon == current);
        }
    }

    private void OnWeaponCardClicked(WeaponCardUI card)
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc == null || card.Weapon == null || !wc.IsUnlocked(card.Weapon)) return;

        wc.SelectWeapon(card.Weapon);
        RefreshWeaponIcons();
        card.PlaySelectPunch();
    }

    #endregion

    #region UI BUTTON METHODS

    private void ClickFeedback()
    {
        SoundManager.Instance?.PlayButtonClick();
        GameManager.Instance?.TriggerLightHaptic();
    }

    public void OnPlayButtonClicked()
    {
        ClickFeedback();
        LevelManager.Instance?.StartGame();
    }

    public void OnNextButtonClicked()
    {
        ClickFeedback();
        HideAllPanels();
        LevelManager.Instance?.LoadNextLevel();
    }

    public void OnRestartButtonClicked()
    {
        ClickFeedback();
        HideAllPanels();
        LevelManager.Instance?.RestartCurrentLevel();
    }

    public void OnHomeButtonClicked()
    {
        ClickFeedback();
        HideAllPanels();
        LevelManager.Instance?.EnterMenu();
    }

    public void OnWeaponButtonClicked()
    {
        ClickFeedback();
        RefreshWeaponIcons();
        PauseForPanel();
        PopIn(weaponPanel);
    }

    public void OnCloseWeaponPanel()
    {
        ClickFeedback();
        if (weaponPanel != null) weaponPanel.SetActive(false);
        ResumeIfPausedByPanel();
    }

    public void OnSettingsButtonClicked()
    {
        ClickFeedback();
        PauseForPanel();
        PopIn(settingsPanel);
    }

    public void OnCloseSettings()
    {
        ClickFeedback();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ResumeIfPausedByPanel();
    }

    public void OnPauseButtonClicked()
    {
        ClickFeedback();
        PauseForPanel();
        PopIn(pausePanel);
    }

    public void OnResumeButtonClicked()
    {
        ClickFeedback();
        if (pausePanel != null) pausePanel.SetActive(false);
        ResumeIfPausedByPanel();
    }

    // Mobile par app background mein jaye (home button / call) to game khud pause - wapas aa kar zombies aage na nikal jayein
    void OnApplicationPause(bool paused)
    {
        if (paused) AutoPause();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus && !Application.isEditor) AutoPause();
    }

    private void AutoPause()
    {
        if (pausedByPanel || pausePanel == null || pausePanel.activeSelf) return;
        if (LevelManager.Instance == null || LevelManager.Instance.CurrentState != LevelManager.GameState.Playing) return;
        PauseForPanel();
        pausePanel.SetActive(true);
    }

    /// <summary> Gameplay ke dauran panel khule to game ruk jaye </summary>
    private void PauseForPanel()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.CurrentState != LevelManager.GameState.Playing) return;
        pausedByPanel = true;
        Time.timeScale = 0f;
        GameManager.Instance?.WeaponController?.SetInputEnabled(false);
    }

    private void ResumeIfPausedByPanel()
    {
        if (!pausedByPanel) return;
        pausedByPanel = false;
        Time.timeScale = 1f;
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentState == LevelManager.GameState.Playing)
            GameManager.Instance?.WeaponController?.SetInputEnabled(true);
    }

    #endregion
}
