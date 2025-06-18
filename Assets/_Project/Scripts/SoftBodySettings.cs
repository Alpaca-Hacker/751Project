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
     
        public float mass = 1f;
        public float damping = 0.01f;
        public float gravity = 9.81f;
        public float lambdaDecay = 0.99f;
        public int solverIterations = 4;
        
        [Header("Constraint Compliance")]

        public float structuralCompliance = 0.0001f; 
        public float shearCompliance = 0.001f;       
        public float bendCompliance = 0.01f; 
        public float volumeCompliance= 0.0001f;
        
        [Header("Collision")]
        public Transform floorTransform;
        public float collisionCompliance = 0.0f;
    
        [Header("Interaction")]
        public bool enableCollision = true;
        public LayerMask collisionLayers = -1;
        
        [Header("Debug Options")]
        public bool useCPUFallback = false;
        public bool debugMode;
        public GraphColouringMethod GraphColouringMethod = GraphColouringMethod.None;
        

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