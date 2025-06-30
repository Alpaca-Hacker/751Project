using UnityEngine;

namespace SoftBody.Scripts.Spawning
{
    [System.Serializable]
    public class SpawnSettings
    {
        [Header("Spawn Timing")]
        public bool autoSpawn = false;
        public float spawnInterval = 1f;
        public float spawnDelay = 0f;
    
        [Header("Spawn Limits")]
        public int maxActiveObjects = 50;
        public int totalSpawnLimit = -1; // -1 = unlimited
    
        [Header("Spawn Location")]
        public Transform[] spawnPoints;
        public bool randomizeSpawnPoint = true;
        public Vector3 spawnAreaSize = Vector3.one;
        public bool useSpawnArea = false;
    
        [Header("Spawn Variation")]
        public bool randomizeRotation = true;
        public Vector3 initialVelocityMin = Vector3.zero;
        public Vector3 initialVelocityMax = Vector3.zero;
        public bool addRandomTorque = false;
        public float maxTorque = 10f;
    }
}