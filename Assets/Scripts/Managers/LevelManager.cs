using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public enum GameState { Playing, Won, Failed }
    public GameState CurrentState { get; private set; } = GameState.Playing;

    [Header("--- 1. Resources Loading Settings ---")]
    [SerializeField] private string resourcesLevelsFolder = "Levels";
    [SerializeField] private string levelFilePrefix = "Level_";
    [SerializeField] private int totalConfiguredLevels = 5;

    [Header("--- 2. Dev / Testing Check ---")]
    [SerializeField] private bool useDevLevel = false;
    [SerializeField] private int devLevelNumber = 1;

    [Header("--- 3. Spawner Center Point ---")]
    [SerializeField] private Transform spawnCenterPoint;

    [Header("--- 4. LAG-FREE SPAWNING SETTINGS ---")]
    [Tooltip("Lag khatam karne ke liye har zombie ke darmiyan micro-delay (0.03s - 0.05s). Is se 60 FPS smooth rahega")]
    [SerializeField] private float enemySpawnStaggerTime = 0.04f;

    // Progression Tracking
    private int currentDisplayLevelNumber = 1;
    private int actualResourceLevelNumber = 1;
    private LevelDataSO currentActiveLevelData;

    // Tracking
    private int totalEnemiesInLevel = 0;
    private int totalAliveEnemiesCount = 0;
    private int currentWaveTotalEnemies = 0;
    private int currentWaveKilledEnemies = 0;

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
        StartCurrentLevel();
    }

    private void LoadProgressionData()
    {
        currentDisplayLevelNumber = PlayerPrefs.GetInt("PlayerDisplayLevel", 1);
        int savedActualLevel = PlayerPrefs.GetInt("PlayerActualLevel", 1);

        if (useDevLevel)
        {
            actualResourceLevelNumber = Mathf.Clamp(devLevelNumber, 1, totalConfiguredLevels);
            currentDisplayLevelNumber = devLevelNumber;
        }
        else
        {
            if (savedActualLevel > totalConfiguredLevels)
                actualResourceLevelNumber = Random.Range(1, totalConfiguredLevels + 1);
            else
                actualResourceLevelNumber = savedActualLevel;
        }
    }

    public void StartCurrentLevel()
    {
        CurrentState = GameState.Playing;
        ClearRemainingEnemies();

        string resourcePath = $"{resourcesLevelsFolder}/{levelFilePrefix}{actualResourceLevelNumber}";
        currentActiveLevelData = Resources.Load<LevelDataSO>(resourcePath);

        if (currentActiveLevelData == null)
        {
            Debug.LogError($"[LevelManager] '{resourcePath}' nahi mila! Assets/Resources/{resourcesLevelsFolder}/ check karein.");
            return;
        }

        UIManager.Instance?.UpdateLevelText(currentDisplayLevelNumber);
        UIManager.Instance?.HideAllPanels();
        SetWeaponControllerState(true);

        // Count total level enemies
        totalEnemiesInLevel = 0;
        foreach (var wave in currentActiveLevelData.waves)
        {
            foreach (var pack in wave.enemyPacks)
            {
                totalEnemiesInLevel += pack.count;
            }
        }
        totalAliveEnemiesCount = totalEnemiesInLevel;

        if (waveSpawnerRoutine != null) StopCoroutine(waveSpawnerRoutine);
        waveSpawnerRoutine = StartCoroutine(SpawnWavesRoutine());

        Debug.Log($"<color=green>[LEVEL LOADED]</color> {resourcePath} | Total Enemies: {totalEnemiesInLevel}");
    }

    private IEnumerator SpawnWavesRoutine()
    {
        for (int w = 0; w < currentActiveLevelData.waves.Count; w++)
        {
            if (CurrentState != GameState.Playing) yield break;

            var wave = currentActiveLevelData.waves[w];

            if (wave.delayBeforeWave > 0)
                yield return new WaitForSeconds(wave.delayBeforeWave);

            currentWaveTotalEnemies = 0;
            foreach (var pack in wave.enemyPacks)
                currentWaveTotalEnemies += pack.count;

            currentWaveKilledEnemies = 0;

            // LAG-FREE: Micro-Staggered Pack Spawning
            yield return StartCoroutine(SpawnWavePacksLagFree(wave));

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
    private IEnumerator SpawnWavePacksLagFree(LevelDataSO.WaveData wave)
    {
        Vector3 baseSpawn = spawnCenterPoint != null ? spawnCenterPoint.position : new Vector3(0, 0, 25f);
        float minX = currentActiveLevelData.minMoveX;
        float maxX = currentActiveLevelData.maxMoveX;

        foreach (var pack in wave.enemyPacks)
        {
            if (pack.enemyPrefab == null) continue;

            for (int i = 0; i < pack.count; i++)
            {
                if (CurrentState != GameState.Playing) yield break;

                float randX = Random.Range(minX, maxX);
                float randZ = Random.Range(0f, pack.clusterSpread);
                Vector3 spawnPos = baseSpawn + new Vector3(randX, 0f, randZ);

                GameObject enemyObj = Instantiate(pack.enemyPrefab, spawnPos, Quaternion.Euler(0, 180f, 0));

                HumanoidEnemy enemyScript = enemyObj.GetComponent<HumanoidEnemy>();
                if (enemyScript != null)
                {
                    enemyScript.minX = minX;
                    enemyScript.maxX = maxX;
                }

                activeSpawnedEnemies.Add(enemyObj);

                // MICRO-STAGGER: Har zombie ke baad 0.04s delay (CPU Spike 0 ho jata hai)
                if (enemySpawnStaggerTime > 0)
                    yield return new WaitForSeconds(enemySpawnStaggerTime);
                else
                    yield return null; // Wait 1 frame
            }
        }
    }

    public void OnEnemyKilled(GameObject enemyObj)
    {
        if (CurrentState != GameState.Playing) return;

        currentWaveKilledEnemies++;
        totalAliveEnemiesCount--;

        if (totalAliveEnemiesCount <= 0)
        {
            OnLevelCompleted();
        }
    }

    public void OnLevelCompleted()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Won;

        SetWeaponControllerState(false);
        SoundManager.Instance?.PlayLevelWin();
        GameManager.Instance?.TriggerHeavyHaptic();

        if (!useDevLevel)
        {
            currentDisplayLevelNumber++;
            PlayerPrefs.SetInt("PlayerDisplayLevel", currentDisplayLevelNumber);

            int nextActual = PlayerPrefs.GetInt("PlayerActualLevel", 1) + 1;
            PlayerPrefs.SetInt("PlayerActualLevel", nextActual);
        }

        UIManager.Instance?.ShowWinScreen();
    }

    public void OnLevelFailed()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Failed;

        if (waveSpawnerRoutine != null) StopCoroutine(waveSpawnerRoutine);

        SetWeaponControllerState(false);
        SoundManager.Instance?.PlayLevelFail();
        GameManager.Instance?.TriggerHeavyHaptic();

        UIManager.Instance?.ShowFailScreen();
    }

    public void LoadNextLevel()
    {
        Resources.UnloadUnusedAssets();
        LoadProgressionData();
        StartCurrentLevel();
    }

    public void RestartCurrentLevel()
    {
        StartCurrentLevel();
    }

    private void SetWeaponControllerState(bool state)
    {
        if (GameManager.Instance != null && GameManager.Instance.WeaponController != null)
        {
            GameManager.Instance.WeaponController.enabled = state;
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