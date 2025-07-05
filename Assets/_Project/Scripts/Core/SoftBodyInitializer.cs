using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Generation;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class SoftBodyInitializer
    {
        private readonly SoftBodySettings _settings;
        private readonly Transform _transform;

        public SoftBodyInitializer(SoftBodySettings settings, Transform transform)
        {
            _settings = settings;
            _transform = transform;
        }

        public InitializationResult Initialize(ComputeShader computeShader)
        {
            var result = new InitializationResult();

            try
            {
                // Validate compute shader
                if (!ValidateComputeShader(computeShader))
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid compute shader configuration";
                    return result;
                }

                // Handle random mesh selection
                HandleRandomMeshSelection();

                // Generate soft body data
                result.SoftBodyData = GenerateSoftBodyData();
                if (!result.SoftBodyData.IsValid)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to generate valid soft body data";
                    return result;
                }

                // Apply graph colouring
                ApplyGraphColouring(result.SoftBodyData.Constraints);

                // Store initial state
                result.InitialParticles = new List<Particle>(result.SoftBodyData.Particles);

                // Initialize core systems
                InitializeSystems(computeShader, result);

                result.Success = true;

                if (_settings.debugMessages)
                {
                    Debug.Log($"Soft body initialization complete: {result.SoftBodyData.Particles.Count} particles, " +
                             $"{result.SoftBodyData.Constraints.Count} constraints");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Soft body initialization failed: {e.Message}\n{e.StackTrace}");
                result.Success = false;
                result.ErrorMessage = e.Message;
            }

            return result;
        }

        private bool ValidateComputeShader(ComputeShader computeShader)
        {
            if (computeShader == null)
            {
                Debug.LogError("Compute Shader not assigned! Please assign the SoftBodyCompute compute shader.");
                return false;
            }

            // Test that required kernels exist
            var requiredKernels = new[]
            {
                "IntegrateAndStorePositions",
                "SolveConstraints", 
                "UpdateMesh",
                "DecayLambdas"
            };

            foreach (var kernelName in requiredKernels)
            {
                if (computeShader.FindKernel(kernelName) == -1)
                {
                    Debug.LogError($"Required compute shader kernel '{kernelName}' not found!");
                    return false;
                }
            }

            return true;
        }

        private void HandleRandomMeshSelection()
        {
            if (_settings.useRandomMesh && _settings.randomMeshes.Length > 0)
            {
                var randomIndex = Random.Range(0, _settings.randomMeshes.Length);
                var selectedMesh = _settings.randomMeshes[randomIndex];

                if (selectedMesh != null)
                {
                    _settings.inputMesh = selectedMesh;
                    _settings.useProceduralCube = false;

                    if (_settings.debugMessages)
                    {
                        Debug.Log($"Selected random mesh: {selectedMesh.name}");
                    }
                }
            }
        }

        private SoftBodyData GenerateSoftBodyData()
        {
            // Use the main generator which handles stuffing, connectivity, validation, etc.
            List<Particle> particles;
            List<Constraint> constraints;
            List<VolumeConstraint> volumeConstraints;
            List<int> indices;
            Vector2[] weldedUVs;
    
            SoftBodyGenerator.GenerateSoftBody(_settings, _transform, 
                out particles, out constraints, out volumeConstraints, out indices, out weldedUVs);

            return new SoftBodyData
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = indices,
                UVs = weldedUVs
            };
        }

        private void ApplyGraphColouring(List<Constraint> constraints)
        {
            // Initialize all constraints with colour group 0 as fallback
            for (var i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                c.ColourGroup = 0;
                constraints[i] = c;
            }

            try
            {
                var algorithm = GraphColouringFactory.Create(_settings.graphColouringMethod);
                algorithm.ApplyColouring(constraints, constraints.Count > 0 ? GetMaxParticleIndex(constraints) + 1 : 0);

                if (_settings.debugMessages)
                {
                    var maxColour = constraints.Count > 0 ? constraints.Max(c => c.ColourGroup) : 0;
                    Debug.Log($"Applied {_settings.graphColouringMethod} graph colouring: {maxColour + 1} colour groups");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Graph colouring failed: {e.Message}");
            }
        }

        private int GetMaxParticleIndex(List<Constraint> constraints)
        {
            var maxIndex = 0;
            foreach (var constraint in constraints)
            {
                maxIndex = Mathf.Max(maxIndex, constraint.ParticleA, constraint.ParticleB);
            }
            return maxIndex;
        }

        private void InitializeSystems(ComputeShader computeShader, InitializationResult result)
        {
            // Initialize core systems
            result.BufferManager = new BufferManager();
            result.ComputeManager = new ComputeShaderManager(computeShader);
            result.ComputeManager.SetBufferManager(result.BufferManager);

            result.Simulation = new SoftBodySimulation(_settings, result.ComputeManager, result.BufferManager);
            result.Simulation.Initialize(result.SoftBodyData, _transform.position);

            result.Renderer = new SoftBodyRenderer(_transform, _settings);
            result.CollisionSystem = new CollisionSystem(_settings, _transform, result.ComputeManager, result.BufferManager);
            result.SleepSystem = new SleepSystem(_settings, _transform);
        }
    }

    public class InitializationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        
        public SoftBodyData SoftBodyData { get; set; }
        public List<Particle> InitialParticles { get; set; }
        
        // Systems
        public SoftBodySimulation Simulation { get; set; }
        public SoftBodyRenderer Renderer { get; set; }
        public BufferManager BufferManager { get; set; }
        public ComputeShaderManager ComputeManager { get; set; }
        public CollisionSystem CollisionSystem { get; set; }
        public SleepSystem SleepSystem { get; set; }
    }
}