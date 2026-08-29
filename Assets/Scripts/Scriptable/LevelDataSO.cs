using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_01", menuName = "Game/Level Data")]
public class LevelDataSO : ScriptableObject
{
    [Serializable]
    public class EnemyPackConfig
    {
        [Tooltip("Enemy Type ka Prefab (e.g. Normal, Fast, ya Tank Zombie)")]
        public GameObject enemyPrefab;

        [Tooltip("Is type ke kitne zombies is pack mein honge")]
        public int count = 3;

        [Tooltip("Is group ke zombies kitne radius ke andar bikhray hon")]
        public float clusterSpread = 1.5f;
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

        [Header("--- Enemy Packs in this Wave ---")]
        [Tooltip("Ek hi wave mein alag alag kism ke zombies ka combination")]
        public List<EnemyPackConfig> enemyPacks = new List<EnemyPackConfig>();
    }

    [Header("--- 1. Level Configuration ---")]
    public string levelName = "Level 1";

    [Header("--- 2. Level Movement Bounds ---")]
    [Tooltip("Is level mein zombies X-axis par kitna Left ja sakte hain")]
    public float minMoveX = -3.5f;

    [Tooltip("Is level mein zombies X-axis par kitna Right ja sakte hain")]
    public float maxMoveX = 3.5f;

    [Header("--- 3. Waves Sequence ---")]
    public List<WaveData> waves = new List<WaveData>();
}