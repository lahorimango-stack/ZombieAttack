using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public enum GameState { Playing, Won, Failed }
    public GameState CurrentState { get; private set; } = GameState.Playing;

    [Header("--- 1. Resources Loading Settings ---")]
    [Tooltip("Assets/Resources folder ke andar ka subfolder jahan levels pare hain")]
    [SerializeField] private string resourcesLevelsFolder = "Levels";

    [Tooltip("Level file ka prefix name (e.g. 'Level_1' ke liye 'Level_')")]
    [SerializeField] private string levelFilePrefix = "Level_";

    [Tooltip("Resources folder mein total kitne levels banaye hain (e.g. 5 ya 10)")]
    [SerializeField] private int totalConfiguredLevels = 5;


    [Header("--- 2. Dev / Testing Check ---")]
    [Tooltip("DEV CHECK: Ise tick karke apni marzi ka specific level test karein")]
    [SerializeField] private bool useDevLevel = false;
    [SerializeField] private int devLevelNumber = 1;


    [Header("--- 3. Spawner Center Point ---")]
    [Tooltip("Zombies kis center point ke aage spawn honge (Scene me EnemySpawnPoint assign karein)")]
    [SerializeField] private Transform spawnCenterPoint;


    // Progression Tracking
    private int currentDisplayLevelNumber = 1;
    private int actualResourceLevelNumber = 1;
    private LevelDataSO currentActiveLevelData;

    // Wave Tracking (Current Wave vs Total Level)
    private int totalEnemiesInLevel = 0;
    private int totalAliveEnemiesCount = 0;
    private int currentWaveAliveCount = 0; // REQUIREMENT: Current wave ke zinda zombies
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
            {
                actualResourceLevelNumber = Random.Range(1, totalConfiguredLevels + 1);
            }
            else
            {
                actualResourceLevelNumber = savedActualLevel;
            }
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
            Debug.LogError($"[LevelManager] '{resourcePath}' nahi mila! Resources/{resourcesLevelsFolder}/ check karein.");
            return;
        }

        UIManager.Instance?.UpdateLevelText(currentDisplayLevelNumber);
        UIManager.Instance?.HideAllPanels();

        SetWeaponControllerState(true);

        // Count total enemies in level
        totalEnemiesInLevel = 0;
        foreach (var wave in currentActiveLevelData.waves)
        {
            totalEnemiesInLevel += wave.pattern == SpawnPattern.CustomOffsets
                ? wave.customOffsets.Count
                : wave.enemyCount;
        }
        totalAliveEnemiesCount = totalEnemiesInLevel;
        currentWaveAliveCount = 0;

        // Waves Spawner Start
        if (waveSpawnerRoutine != null) StopCoroutine(waveSpawnerRoutine);
        waveSpawnerRoutine = StartCoroutine(SpawnWavesRoutine());

        Debug.Log($"<color=green>[LEVEL STARTED]</color> {resourcePath} Loaded! Total Waves: {currentActiveLevelData.waves.Count}");
    }

    /// <summary>
    /// REQUIREMENT: Wave Clear hone ke baad hi agli wave aayegi
    /// </summary>
    private IEnumerator SpawnWavesRoutine()
    {
        for (int w = 0; w < currentActiveLevelData.waves.Count; w++)
        {
            if (CurrentState != GameState.Playing) yield break;

            var wave = currentActiveLevelData.waves[w];

            // Wave shuru hone se pehle ka rest delay (e.g. 1.5 seconds)
            if (wave.delayBeforeWave > 0)
            {
                yield return new WaitForSeconds(wave.delayBeforeWave);
            }

            // Positions calculate karein
            List<Vector3> spawnPositions = GeneratePatternPositions(wave);
            currentWaveAliveCount = spawnPositions.Count; // Is wave ke total zombies

            Debug.Log($"<color=yellow>[WAVE STARTED]</color> Wave {w + 1} ({wave.waveName}) Spawned with {currentWaveAliveCount} enemies!");

            // Wave ke zombies spawn karein
            for (int i = 0; i < spawnPositions.Count; i++)
            {
                if (CurrentState != GameState.Playing) yield break;

                SpawnSingleEnemy(wave.enemyPrefab, spawnPositions[i]);

                if (wave.spawnInterval > 0)
                    yield return new WaitForSeconds(wave.spawnInterval);
            }

            // CRITICAL FIX: JAB TAK IS WAVE KE SAARE ZOMBIES NAHI MAR JATE, WAIT KAREIN!
            yield return new WaitUntil(() => currentWaveAliveCount <= 0 || CurrentState != GameState.Playing);

            Debug.Log($"<color=cyan>[WAVE CLEARED]</color> Wave {w + 1} Cleared!");
        }
    }

    private List<Vector3> GeneratePatternPositions(LevelDataSO.WaveData wave)
    {
        List<Vector3> positions = new List<Vector3>();
        int count = wave.enemyCount;
        float halfWidth = wave.laneWidth * 0.5f;

        switch (wave.pattern)
        {
            case SpawnPattern.RandomSpread:
                for (int i = 0; i < count; i++)
                {
                    float randX = Random.Range(-halfWidth, halfWidth);
                    float randZ = Random.Range(0f, count * 0.4f);
                    positions.Add(new Vector3(randX, 0f, randZ));
                }
                break;

            case SpawnPattern.LineRow:
                float step = count > 1 ? wave.laneWidth / (count - 1) : 0;
                for (int i = 0; i < count; i++)
                {
                    float x = -halfWidth + (i * step);
                    positions.Add(new Vector3(x, 0f, 0f));
                }
                break;

            case SpawnPattern.GridBlock:
                int cols = Mathf.Max(1, wave.gridColumns);
                for (int i = 0; i < count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;

                    float startX = -((cols - 1) * wave.spacing * 0.5f);
                    float x = startX + (col * wave.spacing);
                    float z = row * wave.spacing;

                    positions.Add(new Vector3(x, 0f, z));
                }
                break;

            case SpawnPattern.VShape:
                for (int i = 0; i < count; i++)
                {
                    int side = (i % 2 == 0) ? 1 : -1;
                    int rank = (i + 1) / 2;

                    float x = side * rank * wave.spacing;
                    float z = rank * wave.spacing;

                    positions.Add(new Vector3(x, 0f, z));
                }
                break;

            case SpawnPattern.SingleStream:
                for (int i = 0; i < count; i++)
                {
                    positions.Add(new Vector3(0f, 0f, i * wave.spacing));
                }
                break;

            case SpawnPattern.CustomOffsets:
                positions.AddRange(wave.customOffsets);
                break;
        }

        return positions;
    }

    private void SpawnSingleEnemy(GameObject prefab, Vector3 offset)
    {
        if (prefab == null) return;

        Vector3 spawnBase = spawnCenterPoint != null ? spawnCenterPoint.position : new Vector3(0, 0, 25f);
        Vector3 finalPos = spawnBase + offset;

        GameObject enemyObj = Instantiate(prefab, finalPos, Quaternion.Euler(0, 180f, 0));
        activeSpawnedEnemies.Add(enemyObj);
    }

    /// <summary>
    /// Zombie marne par dono counters kam honge
    /// </summary>
    public void OnEnemyKilled(GameObject enemyObj)
    {
        if (CurrentState != GameState.Playing) return;

        currentWaveAliveCount--;
        totalAliveEnemiesCount--;

        // Saari waves ke saare dushman mar gaye -> Win
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