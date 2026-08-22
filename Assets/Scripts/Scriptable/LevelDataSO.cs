using System;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnPattern
{
    RandomSpread,   // Automatic random scatter across width
    LineRow,        // Single horizontal line (Evenly spaced)
    GridBlock,      // Marching army square/grid (Columns & Rows)
    VShape,         // V formation (Arrowhead)
    SingleStream,   // Single file line (One behind another)
    CustomOffsets   // Agar kabhi manual position deni ho
}

[CreateAssetMenu(fileName = "Level_01", menuName = "Game/Level Data")]
public class LevelDataSO : ScriptableObject
{
    [Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";

        [Tooltip("Wave start hone se pehle ka delay (seconds)")]
        public float delayBeforeWave = 1.5f;

        [Header("--- Enemy Setup ---")]
        [Tooltip("Zombie Prefab")]
        public GameObject enemyPrefab;

        [Tooltip("Is wave mein kitne zombies spawn karne hain")]
        public int enemyCount = 5;

        [Tooltip("Do zombies ke spawn hone ka time interval")]
        public float spawnInterval = 0.3f;

        [Header("--- Auto Pattern / Formation ---")]
        [Tooltip("Kis pattern mein zombies banna chahiye")]
        public SpawnPattern pattern = SpawnPattern.RandomSpread;

        [Tooltip("Road/Track ki total width (Left to Right, e.g. 6 = -3 to +3)")]
        public float laneWidth = 6f;

        [Tooltip("Grid ya V-Shape mein do zombies ke darmiyan faasla")]
        public float spacing = 1.2f;

        [Tooltip("GridBlock ke liye kitne Columns hon (e.g. 3 ya 4)")]
        public int gridColumns = 3;

        [Header("--- Custom Positions (Only if Pattern = CustomOffsets) ---")]
        public List<Vector3> customOffsets = new List<Vector3>();
    }

    [Header("--- Level Configuration ---")]
    public string levelName = "Level 1";
    public List<WaveData> waves = new List<WaveData>();
}