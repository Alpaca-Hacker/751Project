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
        private static readonly ProfilerMarker s_IntegrationMarker = new("SoftBody.Integration");
        private static readonly ProfilerMarker s_ConstraintSolvingMarker = new("SoftBody.ConstraintSolving");
        private static readonly ProfilerMarker s_VolumeConstraintsMarker = new("SoftBody.VolumeConstraints");
        private static readonly ProfilerMarker s_CollisionMarker = new("SoftBody.Collision");
        private static readonly ProfilerMarker s_MeshUpdateMarker = new("SoftBody.MeshUpdate");
        private static readonly ProfilerMarker s_BufferOperationsMarker = new("SoftBody.BufferOps");

        // GPU Timing
        private Dictionary<string, float> _gpuTimings = new();
        private Dictionary<string, int> _frameCounters = new();

        // Performance metrics

        private PerformanceMetrics _currentMetrics;
        private Queue<PerformanceMetrics> _metricsHistory = new(60); // Store 60 frames

        public static void BeginSample(string name)
        {
            switch (name)
            {
                case "Integration":
                    s_IntegrationMarker.Begin();
                    break;
                case "ConstraintSolving":
                    s_ConstraintSolvingMarker.Begin();
                    break;
                case "VolumeConstraints":
                    s_VolumeConstraintsMarker.Begin();
                    break;
                case "Collision":
                    s_CollisionMarker.Begin();
                    break;
                case "MeshUpdate":
                    s_MeshUpdateMarker.Begin();
                    break;
                case "BufferOps":
                    s_BufferOperationsMarker.Begin();
                    break;
            }
        }

        public static void EndSample(string name)
        {
            switch (name)
            {
                case "Integration":
                    s_IntegrationMarker.End();
                    break;
                case "ConstraintSolving":
                    s_ConstraintSolvingMarker.End();
                    break;
                case "VolumeConstraints":
                    s_VolumeConstraintsMarker.End();
                    break;
                case "Collision":
                    s_CollisionMarker.End();
                    break;
                case "MeshUpdate":
                    s_MeshUpdateMarker.End();
                    break;
                case "BufferOps":
                    s_BufferOperationsMarker.End();
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
            if (logPerformanceWarnings && metrics.totalFrameTime > warningThresholdMs)
            {
                LogPerformanceWarning(metrics);
            }
        }

        private void LogPerformanceWarning(PerformanceMetrics metrics)
        {
            Debug.LogWarning($"SoftBody Performance Warning: Frame time {metrics.totalFrameTime:F2}ms " +
                             $"(Particles: {metrics.activeParticles}, Constraints: {metrics.activeConstraints})");
        }

        public PerformanceMetrics GetAverageMetrics()
        {
            if (_metricsHistory.Count == 0) 
                return new PerformanceMetrics();
    
            var sum = new PerformanceMetrics();
            var count = _metricsHistory.Count;
    
            foreach (var metrics in _metricsHistory)
            {
                sum.totalFrameTime += metrics.totalFrameTime;
                sum.integrationTime += metrics.integrationTime;
                sum.constraintSolvingTime += metrics.constraintSolvingTime;
                sum.volumeConstraintTime += metrics.volumeConstraintTime;
                sum.collisionTime += metrics.collisionTime;
                sum.meshUpdateTime += metrics.meshUpdateTime;
                sum.lambdaDecayTime += metrics.lambdaDecayTime;
                sum.velocityUpdateTime += metrics.velocityUpdateTime;
            }
    
            // Use the most recent values for non-time metrics
            var recent = _metricsHistory.Last();
    
            return new PerformanceMetrics
            {
                totalFrameTime = sum.totalFrameTime / count,
                integrationTime = sum.integrationTime / count,
                constraintSolvingTime = sum.constraintSolvingTime / count,
                volumeConstraintTime = sum.volumeConstraintTime / count,
                collisionTime = sum.collisionTime / count,
                meshUpdateTime = sum.meshUpdateTime / count,
                lambdaDecayTime = sum.lambdaDecayTime / count,
                velocityUpdateTime = sum.velocityUpdateTime / count,
                activeParticles = recent.activeParticles,
                activeConstraints = recent.activeConstraints,
                solverIterations = recent.solverIterations,
                memoryUsageMB = recent.memoryUsageMB
            };
        }
    }
}