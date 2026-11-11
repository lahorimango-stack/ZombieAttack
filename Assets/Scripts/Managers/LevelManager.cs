using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public enum GameState { Menu, Playing, Won, Failed }
    public GameState CurrentState { get; private set; } = GameState.Menu;

    [Header("--- 1. Levels (Scene mein assign karein) ---")]
    [Tooltip("Level 1 se Level N tak order mein. Aakhri level ke baad random repeat hoga.")]
    [SerializeField] private List<LevelDataSO> levels = new List<LevelDataSO>();

    [Header("--- 2. Dev / Testing Check ---")]
    [SerializeField] private bool useDevLevel = false;
    [SerializeField] private int devLevelNumber = 1;
    [Tooltip("True = Home screen skip kar ke seedha level start (testing)")]
    [SerializeField] private bool autoStartLevel = false;

    [Header("--- 3. Spawner Points ---")]
    [SerializeField] private Transform spawnCenterPoint;
    [Tooltip("Saamne wale zombies blockade ke gap ke left/right containers ke PEECHE paida hote hain (x = ±min..max) - nazar nahi aate, phir side se gap mein nikalte hain")]
    [SerializeField] private float coverSpawnMinX = 2.7f;
    [SerializeField] private float coverSpawnMaxX = 3.8f;
    [Tooltip("Left gali (alley) jahan se zombies side se nikalte hain")]
    [SerializeField] private Transform leftAlleySpawn;
    [SerializeField] private Transform rightAlleySpawn;

    [Tooltip("Safety: is Z se qareeb (player ke paas ya peeche) kabhi koi zombie paida nahi hoga")]
    [SerializeField] private float minSpawnZ = 12f;

    [Tooltip("Blockade (containers ki deewar) ki Z line aur gap ki aadhi chaurai")]
    [SerializeField] private float blockadeZ = 19.5f;
    [SerializeField] private float blockadeGapHalfWidth = 1.3f;

    [Header("--- 3a. Director AI ---")]
    [Tooltip("Screen par kam se kam itne zombies hon - warna off-screen wale daud kar aate hain")]
    [SerializeField] private int minVisibleEnemies = 2;
    [SerializeField] private float directorInterval = 0.5f;

    [Header("--- 3a2. Mobile Performance ---")]
    [Tooltip("Ek waqt mein zyada se zyada itne zinda zombies. Zyada hon to naye zombies thoda ruk kar aate hain (FPS bacha rahe)")]
    [SerializeField] private int maxAliveEnemies = 26;
    [Tooltip("Wave ke sab zombies aa chuke hon aur field par itne ya kam bachein to rengne wale khade ho kar daudte hain")]
    [SerializeField] private int crawlerStandUpWhenLeft = 3;

    [Header("--- 3b. Per-Level Environment ---")]
    [Tooltip("Level 1..N ke props groups (order mein). Sirf current level wala on hoga")]
    [SerializeField] private List<LevelEnvironment> levelEnvironments = new List<LevelEnvironment>();

    [Header("--- 4. LAG-FREE SPAWNING SETTINGS ---")]
    [Tooltip("Lag khatam karne ke liye har zombie ke darmiyan micro-delay (0.03s - 0.05s). Is se 60 FPS smooth rahega")]
    [SerializeField] private float enemySpawnStaggerTime = 0.04f;

    [Header("--- 5. Game Feel ---")]
    [Tooltip("Aakhri zombie marne par slow motion")]
    [SerializeField] private float finalKillSlowMoScale = 0.25f;
    [SerializeField] private float finalKillSlowMoDuration = 0.9f;
    [SerializeField] private float resultPanelDelay = 1.2f;

    public int CurrentDisplayLevel => currentDisplayLevelNumber;
    public LevelDataSO CurrentLevelData => currentActiveLevelData;
    public int TotalEnemiesInLevel => totalEnemiesInLevel;
    public int KilledEnemies => totalEnemiesInLevel - totalAliveEnemiesCount;
    public int CoinsEarnedThisLevel { get; private set; }
    public int LevelsCount => levels.Count;

    // Progression Tracking
    private int currentDisplayLevelNumber = 1;
    private int actualResourceLevelNumber = 1;
    private LevelDataSO currentActiveLevelData;

    // Tracking
    private int totalEnemiesInLevel = 0;
    private int totalAliveEnemiesCount = 0;
    private int currentWaveTotalEnemies = 0;
    private int currentWaveKilledEnemies = 0;
    private int runningPackSpawners = 0;

    private List<GameObject> activeSpawnedEnemies = new List<GameObject>();
    private Coroutine waveSpawnerRoutine;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        LoadProgressionData();
        SetWeaponControllerState(false);

        if (autoStartLevel)
            StartCurrentLevel();
        else
            EnterMenu();
    }

    private void LoadProgressionData()
    {
        currentDisplayLevelNumber = PlayerPrefs.GetInt("PlayerDisplayLevel", 1);
        int savedActualLevel = PlayerPrefs.GetInt("PlayerActualLevel", 1);
        int totalConfiguredLevels = Mathf.Max(1, levels.Count);

        if (useDevLevel)
        {
            actualResourceLevelNumber = Mathf.Clamp(devLevelNumber, 1, totalConfiguredLevels);
            currentDisplayLevelNumber = devLevelNumber;
        }
        else
        {
            if (savedActualLevel > totalConfiguredLevels)
                actualResourceLevelNumber = Random.Range(2, totalConfiguredLevels + 1); // Level 1 tutorial repeat na ho
            else
                actualResourceLevelNumber = savedActualLevel;
        }

        actualResourceLevelNumber = Mathf.Clamp(actualResourceLevelNumber, 1, totalConfiguredLevels);
    }

    public void EnterMenu()
    {
        CurrentState = GameState.Menu;
        LoadProgressionData();
        StopSpawner();
        ClearRemainingEnemies();
        KillCam.Instance?.Cancel();
        EndSlowMo.Instance?.ResetAll();
        TutorialController.Instance?.Stop();
        SetWeaponControllerState(false);
        Time.timeScale = 1f;

        Barricade.Instance?.ResetHealth(levels.Count > 0 && levels[actualResourceLevelNumber - 1] != null ? levels[actualResourceLevelNumber - 1].barricadeHealth : 100f);
        ApplyEnvironment(actualResourceLevelNumber);
        UIManager.Instance?.ShowHome(currentDisplayLevelNumber);
        SoundManager.Instance?.PlayMenuMusic();
    }

    /// <summary> Home screen ka Play button </summary>
    public void StartGame()
    {
        // Win ke baad Home -> Play: naya (agla) level load ho, purana nahi
        LoadProgressionData();
        StartCurrentLevel();
    }

    public void StartCurrentLevel()
    {
        StopSpawner();
        ClearRemainingEnemies();
        KillCam.Instance?.Cancel();
        EndSlowMo.Instance?.ResetAll();
        KillCam.FinalKillArmed = KillCam.QuietMomentArmed = false;
        Time.timeScale = 1f;

        if (levels.Count == 0 || levels[actualResourceLevelNumber - 1] == null)
        {
            Debug.LogError("[LevelManager] Levels list khali hai! LevelManager ke Inspector mein LevelDataSO assign karein.");
            return;
        }

        currentActiveLevelData = levels[actualResourceLevelNumber - 1];
        CurrentState = GameState.Playing;
        waveSpawnDone = false;
        CoinsEarnedThisLevel = 0;

        // Count total level enemies
        totalEnemiesInLevel = 0;
        foreach (var wave in currentActiveLevelData.waves)
        {
            foreach (var pack in wave.enemyPacks)
            {
                if (pack.enemyPrefab != null) totalEnemiesInLevel += pack.count;
            }
        }
        totalAliveEnemiesCount = totalEnemiesInLevel;

        Barricade.Instance?.ResetHealth(currentActiveLevelData.barricadeHealth);
        ApplyEnvironment(actualResourceLevelNumber);
        GameManager.Instance?.WeaponController?.ClearPendingWeapons();

        UIManager.Instance?.ShowGameplayHUD(currentDisplayLevelNumber, currentActiveLevelData);
        UIManager.Instance?.UpdateProgress(0, totalEnemiesInLevel);
        SetWeaponControllerState(true);

        if (currentActiveLevelData.isBossLevel)
            SoundManager.Instance?.PlayBossMusic();
        else
            SoundManager.Instance?.PlayGameplayMusic();

        // Pehli dafa: haath wala tutorial (zombies uske baad)
        TutorialController.Instance?.Begin();

        waveSpawnerRoutine = StartCoroutine(SpawnWavesRoutine());

        Debug.Log($"<color=green>[LEVEL LOADED]</color> {currentActiveLevelData.name} | Total Enemies: {totalEnemiesInLevel}");
    }

    private IEnumerator SpawnWavesRoutine()
    {
        int waveCount = currentActiveLevelData.waves.Count;

        // Tutorial chal raha ho to zombies ruk kar aayen (player pehle draw/throw seekh le)
        while (TutorialController.Instance != null && TutorialController.Instance.Blocking && CurrentState == GameState.Playing)
            yield return null;

        for (int w = 0; w < waveCount; w++)
        {
            if (CurrentState != GameState.Playing) yield break;

            var wave = currentActiveLevelData.waves[w];

            if (wave.delayBeforeWave > 0)
                yield return WaitEnemyTime(wave.delayBeforeWave);

            if (CurrentState != GameState.Playing) yield break;

            currentWaveTotalEnemies = 0;
            foreach (var pack in wave.enemyPacks)
                if (pack.enemyPrefab != null) currentWaveTotalEnemies += pack.count;

            currentWaveKilledEnemies = 0;
            UIManager.Instance?.SetWave(w + 1, waveCount);

            if (wave.showBanner)
            {
                bool isFinal = w == waveCount - 1 && waveCount > 1;
                if (w == 0)
                    UIManager.Instance?.ShowLevelIntro(currentActiveLevelData.levelName, currentActiveLevelData.levelSubtitle, false);
                else
                    UIManager.Instance?.ShowWaveBanner(w + 1, waveCount, isFinal, currentActiveLevelData.isBossLevel && isFinal);
                SoundManager.Instance?.PlayWaveStart(isFinal);
            }

            // LAG-FREE: Har pack apne coroutine mein (startDelay + spawnInterval)
            waveSpawnDone = false;
            runningPackSpawners = 0;
            foreach (var pack in wave.enemyPacks)
            {
                if (pack.enemyPrefab == null) continue;
                runningPackSpawners++;
                StartCoroutine(SpawnPackRoutine(pack));
            }

            yield return new WaitUntil(() => runningPackSpawners <= 0 || CurrentState != GameState.Playing);
            waveSpawnDone = true;

            // Wave Trigger Percentage Check (e.g. 75% enemies dead)
            int requiredKillsToTriggerNextWave = Mathf.CeilToInt(currentWaveTotalEnemies * wave.waveClearPercentage);

            yield return new WaitUntil(() =>
                currentWaveKilledEnemies >= requiredKillsToTriggerNextWave ||
                CurrentState != GameState.Playing
            );

            Debug.Log($"<color=cyan>[WAVE THRESHOLD REACHED]</color> Wave {w + 1} Cleared {wave.waveClearPercentage * 100}%!");
        }
    }

    /// <summary>
    /// LAG-FREE PACK SPAWNER: Workload ko frames par distribute karta hai
    /// </summary>
    private IEnumerator SpawnPackRoutine(LevelDataSO.EnemyPackConfig pack)
    {
        if (pack.startDelay > 0f)
            yield return WaitEnemyTime(pack.startDelay);

        Vector3 baseSpawn = spawnCenterPoint != null ? spawnCenterPoint.position : new Vector3(0, 0, 25f);
        float minX = currentActiveLevelData.minMoveX;
        float maxX = currentActiveLevelData.maxMoveX;

        for (int i = 0; i < pack.count; i++)
        {
            // Organized flow + mobile: field par level ki had se zyada zombies nahi - naya tab aaye jab koi mare
            while (CurrentState == GameState.Playing && AliveOnField() >= AliveCap)
                yield return null;
            if (CurrentState != GameState.Playing) break;

            // Kahan se aaye: saamne ya left/right gali
            int side = PickSide(pack.spawnFrom, i); // 0 = front, -1 = left, 1 = right
            Transform alley = side < 0 ? leftAlleySpawn : (side > 0 ? rightAlleySpawn : null);

            Vector3 spawnPos;
            Quaternion spawnRot;
            if (alley != null)
            {
                spawnPos = alley.position + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-1.2f, 1.2f));
                spawnRot = Quaternion.LookRotation(new Vector3(-side, 0f, -0.6f).normalized);
            }
            else
            {
                // Container ke peeche (camera se chhupa), gap ki taraf munh - khali zameen par achanak paida nahi hota
                int coverSide = Random.value < 0.5f ? -1 : 1;
                spawnPos = new Vector3(coverSide * Random.Range(coverSpawnMinX, coverSpawnMaxX), 0f, baseSpawn.z + Random.Range(-0.25f, 0.25f));
                spawnRot = Quaternion.LookRotation(new Vector3(-coverSide, 0f, -0.15f).normalized);
            }
            spawnPos.z = Mathf.Max(spawnPos.z, minSpawnZ);

            GameObject enemyObj = Instantiate(PickVariant(pack), spawnPos, spawnRot);

            HumanoidEnemy enemyScript = enemyObj.GetComponent<HumanoidEnemy>();
            if (enemyScript != null)
            {
                enemyScript.minX = minX;
                enemyScript.maxX = maxX;
                enemyScript.ScaleHealth(currentActiveLevelData.enemyHealthMultiplier);

                if (enemyScript is StandardEnemy se)
                {
                    if (se.IsBoss) UIManager.Instance?.ShowBossBar(se, se.BossTitle);
                    se.ApplySpeedMultiplier(currentActiveLevelData.enemySpeedMultiplier);
                    se.SetGap(blockadeZ, blockadeGapHalfWidth);
                    if (alley != null)
                    {
                        // Gali se nikalne wale daud kar lane mein aate hain (game slow na ho)
                        se.BeginSideEntry(Random.Range(minX * 0.6f, maxX * 0.6f));
                        se.Hurry();
                    }
                    else
                    {
                        // Container ke peeche se side mein chal kar gap mein nikle, phir player ki taraf
                        float gapEntry = Mathf.Max(0.3f, blockadeGapHalfWidth - 0.25f);
                        se.BeginSideEntry(Random.Range(-gapEntry * 0.8f, gapEntry * 0.8f), gapEntry);
                    }
                }
            }

            activeSpawnedEnemies.Add(enemyObj);

            float wait = pack.spawnInterval > 0f ? pack.spawnInterval : enemySpawnStaggerTime;
            if (wait > 0)
                yield return WaitEnemyTime(wait);
            else
                yield return null; // Wait 1 frame
        }

        runningPackSpawners--;
    }

    /// <summary> Zombie-time mein intezar: weapon draw (slow-mo) ke dauran naye zombies bhi dheere aayen </summary>
    private IEnumerator WaitEnemyTime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += EnemyTime.DeltaTime;
            yield return null;
        }
    }

    /// <summary> Ek hi type ki alag alag shakal (horde ek jaisa na lage) </summary>
    private static GameObject PickVariant(LevelDataSO.EnemyPackConfig pack)
    {
        int count = pack.variantPrefabs != null ? pack.variantPrefabs.Count : 0;
        int pick = Random.Range(0, count + 1);
        if (pick == 0) return pack.enemyPrefab;
        GameObject v = pack.variantPrefabs[pick - 1];
        return v != null ? v : pack.enemyPrefab;
    }

    private float directorTimer;
    private bool waveSpawnDone;
    private readonly List<StandardEnemy> hiddenEnemies = new List<StandardEnemy>();

    void Update()
    {
        if (CurrentState != GameState.Playing) return;
        directorTimer -= Time.deltaTime;
        if (directorTimer > 0f) return;
        directorTimer = directorInterval;
        RunDirector();
        StandUpLastCrawlers();
        UpdateKillCamArming();
    }

    /// <summary> Is level mein ek waqt mein zyada se zyada zinda zombies (level ki apni had, mobile had se zyada nahi) </summary>
    private int AliveCap
    {
        get
        {
            int lvl = currentActiveLevelData != null ? currentActiveLevelData.maxAliveOnField : 0;
            return lvl > 0 ? Mathf.Min(lvl, maxAliveEnemies) : maxAliveEnemies;
        }
    }

    /// <summary>
    /// Kill cam kab: level ka aakhri zombie, ya beech mein sirf ek door wala zombie (danger kam)
    /// </summary>
    private void UpdateKillCamArming()
    {
        KillCam.FinalKillArmed = CurrentState == GameState.Playing && totalAliveEnemiesCount == 1;

        bool quiet = false;
        if (CurrentState == GameState.Playing && runningPackSpawners <= 0 && !KillCam.FinalKillArmed)
        {
            StandardEnemy only = null;
            int alive = 0;
            var list = StandardEnemy.Active;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) { alive++; only = list[i]; }
            quiet = alive == 1 && !only.IsThreat;
        }
        KillCam.QuietMomentArmed = quiet;
    }

    private static int AliveOnField()
    {
        int alive = 0;
        var list = StandardEnemy.Active;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && !list[i].IsDead) alive++;
        return alive;
    }

    /// <summary>
    /// Wave ke aakhir mein rengne wale bach jayen to khade ho kar daudte hain (game latakti nahi)
    /// </summary>
    private void StandUpLastCrawlers()
    {
        if (!waveSpawnDone) return;
        var list = StandardEnemy.Active;
        int alive = 0, crawlers = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || list[i].IsDead) continue;
            alive++;
            if (list[i].CanStandUp) crawlers++;
        }
        if (crawlers == 0 || (alive > crawlerStandUpWhenLeft && crawlers < alive)) return;

        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].CanStandUp) list[i].StandUpAndRun();
    }

    /// <summary>
    /// Director AI: screen par zombies kam hon to sab se qareeb off-screen zombies ko daudao.
    /// Is se screen kabhi lambi der khaali nahi rehti.
    /// </summary>
    private void RunDirector()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        int visible = 0;
        hiddenEnemies.Clear();
        var list = StandardEnemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var se = list[i];
            if (se == null || se.IsDead) continue;

            Vector3 p = se.transform.position;
            Vector3 vp = cam.WorldToViewportPoint(p + Vector3.up);
            bool onScreen = vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0f && vp.y < 1f && p.z < blockadeZ - 0.5f;
            if (onScreen) visible++;
            else if (!se.IsHurried) hiddenEnemies.Add(se);
        }

        int need = minVisibleEnemies - visible;
        if (need <= 0 || hiddenEnemies.Count == 0) return;

        hiddenEnemies.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        for (int i = 0; i < Mathf.Min(need + 1, hiddenEnemies.Count); i++)
            hiddenEnemies[i].Hurry();
    }

    private int PickSide(LevelDataSO.SpawnSide from, int index)
    {
        switch (from)
        {
            case LevelDataSO.SpawnSide.Left: return -1;
            case LevelDataSO.SpawnSide.Right: return 1;
            case LevelDataSO.SpawnSide.Sides: return index % 2 == 0 ? -1 : 1;
            case LevelDataSO.SpawnSide.Mixed:
                float r = Random.value;
                return r < 0.6f ? 0 : (r < 0.8f ? -1 : 1);
            default: return 0;
        }
    }

    private void ApplyEnvironment(int levelNumber)
    {
        if (levelEnvironments == null || levelEnvironments.Count == 0) return;
        int idx = Mathf.Clamp(levelNumber - 1, 0, levelEnvironments.Count - 1);
        for (int i = 0; i < levelEnvironments.Count; i++)
        {
            if (levelEnvironments[i] == null) continue;
            if (i == idx) levelEnvironments[i].Apply();
            else levelEnvironments[i].gameObject.SetActive(false);
        }
    }

    public void OnEnemyKilled(GameObject enemyObj)
    {
        if (CurrentState != GameState.Playing) return;

        currentWaveKilledEnemies++;
        totalAliveEnemiesCount--;

        HumanoidEnemy enemy = enemyObj != null ? enemyObj.GetComponent<HumanoidEnemy>() : null;
        int coins = enemy != null ? enemy.CoinReward : 1;
        CoinsEarnedThisLevel += coins;
        GameManager.Instance?.AddCoins(coins);

        UIManager.Instance?.UpdateProgress(KilledEnemies, totalEnemiesInLevel);
        UIManager.Instance?.OnEnemyKilled(enemyObj != null ? enemyObj.transform.position : Vector3.zero, coins);

        if (totalAliveEnemiesCount <= 0)
        {
            OnLevelCompleted();
        }
        UpdateKillCamArming();
    }

    public void OnLevelCompleted()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Won;

        StopSpawner();
        SetWeaponControllerState(false);
        GameManager.Instance?.TriggerHeavyHaptic();

        int bonus = currentActiveLevelData != null ? currentActiveLevelData.completionCoins : 0;
        CoinsEarnedThisLevel += bonus;
        GameManager.Instance?.AddCoins(bonus);

        int completedLevel = currentDisplayLevelNumber;

        if (!useDevLevel)
        {
            currentDisplayLevelNumber++;
            PlayerPrefs.SetInt("PlayerDisplayLevel", currentDisplayLevelNumber);

            int nextActual = PlayerPrefs.GetInt("PlayerActualLevel", 1) + 1;
            PlayerPrefs.SetInt("PlayerActualLevel", nextActual);
            PlayerPrefs.Save();
        }

        StartCoroutine(WinSequence(completedLevel));
    }

    private IEnumerator WinSequence(int completedLevel)
    {
        // Final kill: kill cam chal raha ho to woh khatam hone do, warna seedha slow-mo
        if (KillCam.Instance != null && KillCam.Instance.Busy)
        {
            while (KillCam.Instance != null && KillCam.Instance.Busy) yield return null;
        }
        // Cinematic jeet: dheema waqt, camera push, sunehra rang, 'LEVEL CLEARED' title
        if (EndSlowMo.Instance != null)
        {
            SoundManager.Instance?.PlayLevelWin();
            yield return EndSlowMo.Instance.PlayWin($"{totalEnemiesInLevel} ZOMBIES DOWN");
        }
        else
        {
            Time.timeScale = finalKillSlowMoScale;
            yield return new WaitForSecondsRealtime(finalKillSlowMoDuration);
            Time.timeScale = 1f;
            SoundManager.Instance?.PlayLevelWin();
            yield return new WaitForSecondsRealtime(resultPanelDelay * 0.5f);
        }

        WeaponDataSO unlocked = GameManager.Instance != null && GameManager.Instance.WeaponController != null
            ? GameManager.Instance.WeaponController.GetWeaponUnlockedAfterLevel(completedLevel)
            : null;

        UIManager.Instance?.ShowWinScreen(completedLevel, CoinsEarnedThisLevel, unlocked);
    }

    public void OnLevelFailed()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Failed;

        StopSpawner();
        SetWeaponControllerState(false);
        GameManager.Instance?.TriggerHeavyHaptic();

        StartCoroutine(FailSequence());
    }

    private IEnumerator FailSequence()
    {
        // Cinematic haar: dheema waqt, be-rang laal duniya, 'OVERRUN' title
        if (EndSlowMo.Instance != null)
        {
            SoundManager.Instance?.PlayLevelFail();
            yield return EndSlowMo.Instance.PlayFail("THE HORDE BROKE THROUGH");
        }
        else
        {
            yield return new WaitForSecondsRealtime(resultPanelDelay);
            SoundManager.Instance?.PlayLevelFail();
        }
        UIManager.Instance?.ShowFailScreen(currentDisplayLevelNumber, KilledEnemies, totalEnemiesInLevel);
    }

    public void LoadNextLevel()
    {
        LoadProgressionData();
        StartCurrentLevel();
    }

    public void RestartCurrentLevel()
    {
        StartCurrentLevel();
    }

    private void StopSpawner()
    {
        StopAllCoroutines();
        waveSpawnerRoutine = null;
        runningPackSpawners = 0;
    }

    private void SetWeaponControllerState(bool state)
    {
        if (GameManager.Instance != null && GameManager.Instance.WeaponController != null)
        {
            GameManager.Instance.WeaponController.SetInputEnabled(state);
        }
    }

    private void ClearRemainingEnemies()
    {
        for (int i = 0; i < activeSpawnedEnemies.Count; i++)
        {
            if (activeSpawnedEnemies[i] != null)
            {
                Destroy(activeSpawnedEnemies[i]);
            }
        }
        activeSpawnedEnemies.Clear();
    }
}
