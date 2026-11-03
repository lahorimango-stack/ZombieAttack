using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_01", menuName = "Game/Level Data")]
public class LevelDataSO : ScriptableObject
{
    public enum SpawnSide { Front, Left, Right, Sides, Mixed }

    [Serializable]
    public class EnemyPackConfig
    {
        [Tooltip("Zombies kahan se aayenge: saamne se, ya left/right galiyon se")]
        public SpawnSide spawnFrom = SpawnSide.Front;

        [Tooltip("Enemy Type ka Prefab (e.g. Normal, Fast, ya Tank Zombie)")]
        public GameObject enemyPrefab;

        [Tooltip("Isi type ke doosri shakal wale prefabs (har zombie random mein se ek) - khali chhor sakte hain")]
        public List<GameObject> variantPrefabs = new List<GameObject>();

        [Tooltip("Is type ke kitne zombies is pack mein honge")]
        public int count = 3;

        [Tooltip("Is group ke zombies kitne radius ke andar bikhray hon")]
        public float clusterSpread = 1.5f;

        [Tooltip("Pack ke har zombie ke darmiyan delay (seconds). 0 = sab ek saath (stagger default)")]
        public float spawnInterval = 0f;

        [Tooltip("Ye pack wave shuru hone ke kitni der baad aaye (seconds)")]
        public float startDelay = 0f;
    }

    [Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";

        [Tooltip("Pichli wave clear hone ke baad kitna rest delay aayega (seconds)")]
        public float delayBeforeWave = 1.5f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Is wave ke kitne % zombies marne ke baad agli wave trigger ho jaye (e.g. 0.75 = 75%)")]
        public float waveClearPercentage = 0.8f;

        [Tooltip("Wave shuru hone par screen par banner dikhaye")]
        public bool showBanner = true;

        [Header("--- Enemy Packs in this Wave ---")]
        [Tooltip("Ek hi wave mein alag alag kism ke zombies ka combination")]
        public List<EnemyPackConfig> enemyPacks = new List<EnemyPackConfig>();
    }

    [Header("--- 1. Level Configuration ---")]
    public string levelName = "Level 1";
    [Tooltip("Level shuru hone par subtitle (e.g. 'Hold the line!')")]
    public string levelSubtitle = "";
    [Tooltip("Boss level par boss music/banner chalega")]
    public bool isBossLevel = false;

    [Header("--- 2. Level Movement Bounds ---")]
    [Tooltip("Is level mein zombies X-axis par kitna Left ja sakte hain")]
    public float minMoveX = -3.5f;

    [Tooltip("Is level mein zombies X-axis par kitna Right ja sakte hain")]
    public float maxMoveX = 3.5f;

    [Header("--- 2b. Difficulty (level ke saath barhe) ---")]
    [Tooltip("Zombies ki health ka multiplier (0.5 = aadhi health, shuru ke levels mein 1 hit)")]
    public float enemyHealthMultiplier = 1f;
    [Tooltip("Zombies ki raftar ka multiplier (animation speed - root motion ke saath)")]
    public float enemySpeedMultiplier = 1f;

    [Header("--- 3. Barricade & Rewards ---")]
    public float barricadeHealth = 100f;
    [Tooltip("Level jeetne par bonus coins")]
    public int completionCoins = 50;

    [Header("--- 4. Waves Sequence ---")]
    public List<WaveData> waves = new List<WaveData>();
}
