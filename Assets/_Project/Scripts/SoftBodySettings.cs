using UnityEngine;

namespace SoftBody.Scripts
{
    [System.Serializable]
    public class SoftBodySettings
    {
        [Header("Mesh Generation")]
        public Vector3 size = Vector3.one;
        [Range(2, 20)]
        public int resolution = 4;
    
        [Header("Physics")]
        [Range(0.1f, 10f)]
        public float mass = 1f;
        [Range(0f, 1f)]
        public float damping = 0.01f;
        [Range(0f, 20f)]
        public float gravity = 9.81f;
        [Range(0.85f, 0.99f)] 
        public float lambdaDecay = 0.99f;
        [Range(1, 10)]
        public int solverIterations = 4;
        
        [Header("Constraint Compliance")]
        [Range(0.0f, 0.01f)]
        public float structuralCompliance = 0.0001f; 
    
        [Range(0.0f, 0.1f)]
        public float shearCompliance = 0.001f;       
    
        [Range(0.0f, 0.5f)]
        public float bendCompliance = 0.01f; 
    
        [Header("Interaction")]
        public bool enableCollision = true;
        public LayerMask collisionLayers = -1;
        
        [Header("Debug Options")]
        public bool useCPUFallback = false;
        public bool debugMode;

        public void LogSettings()
        {
            Debug.Log($"SoftBody Settings: Size={size}, " +
                      $"Resolution={resolution}, " +
                      $"Mass={mass}, " +
                      $"Damping={damping}, " +
                      $"Gravity={gravity}, " +
                      $"SolverIterations={solverIterations}, " +
                      $"EnableCollision={enableCollision}," +
                      $"StructuralCompliance={structuralCompliance}," +
                      $"ShearCompliance={shearCompliance}," +
                      $"BendCompliance={bendCompliance}");
        }
    }
}