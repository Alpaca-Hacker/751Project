using UnityEngine;

namespace SoftBody.Scripts.Pooling
{
    [System.Serializable]
    public class PoolSettings
    {
        [Header("Pool Configuration")]
        public GameObject prefab;
        public int initialSize = 10;
        public int maxSize = 100;
        public bool allowGrowth = true;
        public bool autoReturn = true;
        public float autoReturnTime = 30f;
    
        [Header("Spawn Settings")]
        public bool resetTransformOnGet = true;
        public bool resetPhysicsOnGet = true;
        public bool randomizeRotationOnGet = false;
    }
}