using Unity.Profiling;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts
{

    public class SoftBodyProfiler : MonoBehaviour
    {
        [Header("Profiling Settings")] public bool enableDetailedProfiling = true;
        public bool logPerformanceWarnings = true;
        public float warningThresholdMs = 5f;

        // Unity Profiler Markers
        private static readonly ProfilerMarker SIntegrationMarker = new("SoftBody.Integration");
        private static readonly ProfilerMarker SConstraintSolvingMarker = new("SoftBody.ConstraintSolving");
        private static readonly ProfilerMarker SVolumeConstraintsMarker = new("SoftBody.VolumeConstraints");
        private static readonly ProfilerMarker SCollisionMarker = new("SoftBody.Collision");
        private static readonly ProfilerMarker SMeshUpdateMarker = new("SoftBody.MeshUpdate");
        private static readonly ProfilerMarker SBufferOperationsMarker = new("SoftBody.BufferOps");

        // GPU Timing
        private Dictionary<string, float> _gpuTimings = new();
        private Dictionary<string, int> _frameCounters = new();

        // Performance metrics

        private PerformanceMetrics _currentMetrics;
        private readonly Queue<PerformanceMetrics> _metricsHistory = new(60); // Store 60 frames

        public static void BeginSample(string name)
        {
            switch (name)
            {
                case "Integration":
                    SIntegrationMarker.Begin();
                    break;
                case "ConstraintSolving":
                    SConstraintSolvingMarker.Begin();
                    break;
                case "VolumeConstraints":
                    SVolumeConstraintsMarker.Begin();
                    break;
                case "Collision":
                    SCollisionMarker.Begin();
                    break;
                case "MeshUpdate":
                    SMeshUpdateMarker.Begin();
                    break;
                case "BufferOps":
                    SBufferOperationsMarker.Begin();
                    break;
            }
        }

        public static void EndSample(string name)
        {
            switch (name)
            {
                case "Integration":
                    SIntegrationMarker.End();
                    break;
                case "ConstraintSolving":
                    SConstraintSolvingMarker.End();
                    break;
                case "VolumeConstraints":
                    SVolumeConstraintsMarker.End();
                    break;
                case "Collision":
                    SCollisionMarker.End();
                    break;
                case "MeshUpdate":
                    SMeshUpdateMarker.End();
                    break;
                case "BufferOps":
                    SBufferOperationsMarker.End();
                    break;
            }
        }

        public void RecordMetrics(PerformanceMetrics metrics)
        {
            _currentMetrics = metrics;
            _metricsHistory.Enqueue(metrics);

            if (_metricsHistory.Count > 60)
                _metricsHistory.Dequeue();

            // Check for performance warnings
            if (logPerformanceWarnings && metrics.TotalFrameTime > warningThresholdMs)
            {
                LogPerformanceWarning(metrics);
            }
        }

        private void LogPerformanceWarning(PerformanceMetrics metrics)
        {
            Debug.LogWarning($"SoftBody Performance Warning: Frame time {metrics.TotalFrameTime:F2}ms " +
                             $"(Particles: {metrics.ActiveParticles}, Constraints: {metrics.ActiveConstraints})");
        }

        public PerformanceMetrics GetAverageMetrics()
        {
            if (_metricsHistory.Count == 0) 
                return new PerformanceMetrics();
    
            var sum = new PerformanceMetrics();
            var count = _metricsHistory.Count;
    
            foreach (var metrics in _metricsHistory)
            {
                sum.TotalFrameTime += metrics.TotalFrameTime;
                sum.IntegrationTime += metrics.IntegrationTime;
                sum.ConstraintSolvingTime += metrics.ConstraintSolvingTime;
                sum.VolumeConstraintTime += metrics.VolumeConstraintTime;
                sum.CollisionTime += metrics.CollisionTime;
                sum.MeshUpdateTime += metrics.MeshUpdateTime;
                sum.LambdaDecayTime += metrics.LambdaDecayTime;
                sum.VelocityUpdateTime += metrics.VelocityUpdateTime;
            }
    
            // Use the most recent values for non-time metrics
            var recent = _metricsHistory.Last();
    
            return new PerformanceMetrics
            {
                TotalFrameTime = sum.TotalFrameTime / count,
                IntegrationTime = sum.IntegrationTime / count,
                ConstraintSolvingTime = sum.ConstraintSolvingTime / count,
                VolumeConstraintTime = sum.VolumeConstraintTime / count,
                CollisionTime = sum.CollisionTime / count,
                MeshUpdateTime = sum.MeshUpdateTime / count,
                LambdaDecayTime = sum.LambdaDecayTime / count,
                VelocityUpdateTime = sum.VelocityUpdateTime / count,
                ActiveParticles = recent.ActiveParticles,
                ActiveConstraints = recent.ActiveConstraints,
                SolverIterations = recent.SolverIterations,
                MemoryUsageMb = recent.MemoryUsageMb
            };
        }
    }
}