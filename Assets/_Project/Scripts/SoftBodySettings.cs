using System;
using UnityEngine;

namespace SoftBody.Scripts
{
    [System.Serializable]
    public class SoftBodySettings
    {
        [Header("Mesh Input")]
        public Mesh inputMesh;
        public bool useProceduralCube = true; 
        [Header("Random Mesh Selection")]
        public bool useRandomMesh = false;
        [Tooltip("Array of meshes to randomly choose from")]
        public Mesh[] randomMeshes = Array.Empty<Mesh>();
        [Tooltip("Choose a new random mesh each time the object is activated")]
        public bool changeOnActivation = true;
        [Header("High Poly Tetrahedralization")]
        public bool useTetrahedralizationForHighPoly = true;
        public int maxSurfaceVerticesBeforeTetra = 500;
        public float interiorPointDensity = 0.3f; 
        [Header("Cube Settings")]
        public Vector3 size = Vector3.one;
        [Range(2, 20)]
        public int resolution = 4;
        
        [Header("Mesh Processing")]
        public bool preserveUVs = true;
        public bool preserveNormals = false;
        [Range(0.5f, 2f)]
        public float constraintDensityMultiplier = 1f;
        [Header("Mesh Connectivity")]
        public bool autoFixConnectivity = true;
        public ConnectivityMethod connectivityMethod = ConnectivityMethod.BridgeConstraints;
    
        [Header("Physics")]
     
        public float mass = 1f;
        public float damping = 0.01f;
        public float gravity = 9.81f;
        public float lambdaDecay = 0.99f;
        public int solverIterations = 4;
        
        [Header("Stuffing Behavior")]
        public bool enableStuffingMode = true;
        public float stuffingDensity = 0.5f;           // How densely packed the interior is
        public float pressureResistance = 2.0f;        // How much it resists compression
        public float skinFlexibility = 3.0f;           // How flexible the "skin" is
        public float stuffingStiffness = 0.1f;         // How stiff the internal stuffing is
        public int maxStuffingParticles = 30;
        public int maxStuffingConstraints = 200;
        public int maxSkinConstraints = 150;
        public int maxVolumeConstraints = 50;
        public int maxAdditionalConstraints = 500;
        public bool enablePerformanceLimits = true;
        
        [Header("Constraint Compliance")]

        public float structuralCompliance = 0.0001f; 
        public float shearCompliance = 0.001f;       
        public float bendCompliance = 0.01f; 
        public float volumeCompliance= 0.0001f;
        
        [Header("Constraint Optimization")]
        public bool enableConstraintFiltering = true;
        public int maxConstraintsPerParticle = 10; 
        public float maxConstraintLength = 0.12f;    
    
        [Header("Sleep & Movement System")]
        public bool enableSleepSystem = true;
        public float sleepVelocityThreshold = 0.005f;
        public float sleepTimeThreshold = 1f;
        public float wakeDistanceThreshold = 0.1f;
        public bool showSleepState = false;
        
        [Header("Proximity Wake System")]
        public bool enableProximityWake = true;
        public float proximityWakeRadius = 3f;
        public int proximityCheckInterval = 60;

        [Header("Movement Dampening")]
        public bool enableMovementDampening = true;
        public float stillnessThreshold = 0.01f;     // When to start dampening
        public float dampeningStrength = 0.95f;      // How aggressive (0.9 = 10% reduction per frame)
        public float minMovementSpeed = 0.001f;      // Stop dampening below this speed
        
        [Header("Collision")]
        public Transform floorTransform;
    
        [Header("Interaction")]
        public bool enableCollision = true;
        public LayerMask collisionLayers = -1;
        
        [Header("Soft Body Interactions")]
        public bool enableSoftBodyCollisions = true;
        [Tooltip("How much soft bodies push each other (0 = no interaction, 1 = full physics)")]
        public float interactionStrength = 0.8f;
        [Tooltip("Maximum distance to detect other soft bodies")]
        public float maxInteractionDistance = 5f;
        
        [Header("Debug Options")]
        public bool SkipUpdate = false;
        public bool debugMode;
        public GraphColouringMethod graphColouringMethod = GraphColouringMethod.None;
        public bool debugMessages = false;


        public void LogSettings()
        {
            if (!debugMessages) return;
            Debug.Log($"SoftBody Settings: Size={size}, " +
                      $"Resolution={resolution}, " +
                      $"Mass={mass}, " +
                      $"Damping={damping}, " +
                      $"Gravity={gravity}, " +
                      $"SolverIterations={solverIterations}, " +
                      $"EnableCollision={enableCollision}," +
                      $"StructuralCompliance={structuralCompliance}," +
                      $"ShearCompliance={shearCompliance}," +
                      $"BendCompliance={bendCompliance}, " +
                      $"VolumeCompliance={volumeCompliance}, " +
                      $"StuffingDensity={stuffingDensity}, " +
                      $"PressureResistance={pressureResistance}, " +
                      $"SkinFlexibility={skinFlexibility}, " +
                      $"StuffingStiffness={stuffingStiffness}, " +
                      $"MaxStuffingParticles={maxStuffingParticles}, " +
                      $"MaxStuffingConstraints={maxStuffingConstraints}, " +
                      $"MaxSkinConstraints={maxSkinConstraints}, " +
                      $"MaxVolumeConstraints={maxVolumeConstraints}, " +
                      $"MaxAdditionalConstraints={maxAdditionalConstraints}, " +
                      $"EnablePerformanceLimits={enablePerformanceLimits}, " +
                      $"GraphColouringMethod={graphColouringMethod}");
        }
        
        public enum ConnectivityMethod
        {
            BridgeConstraints,    // Minimal, targeted fixes
            ProximityBased,       // Robust but more constraints  
            Hybrid               // Try bridge first, then proximity
        }
    }
}