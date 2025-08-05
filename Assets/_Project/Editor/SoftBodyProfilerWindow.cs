#if UNITY_EDITOR

using SoftBody.Scripts;
using SoftBody.Scripts.Models;
using UnityEngine;
using UnityEditor;

namespace _Project.Editor
{

    public class SoftBodyProfilerWindow : EditorWindow
    {
        private SoftBodyProfiler _targetProfiler;
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private double _lastRefreshTime;

        [MenuItem("Tools/Soft Body Profiler")]
        public static void ShowWindow()
        {
            GetWindow<SoftBodyProfilerWindow>("Soft Body Profiler");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_autoRefresh && EditorApplication.timeSinceStartup - _lastRefreshTime > 0.1f)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.LabelField("Soft Body Performance Profiler", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Controls
            GUILayout.BeginHorizontal();
            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
            if (GUILayout.Button("Manual Refresh"))
            {
                FindTargetProfiler();
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Check if Unity is playing
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Profiler data is only available during Play mode. Press Play to see performance metrics.",
                    MessageType.Info);
                return;
            }

            // Find profiler if needed
            if (_targetProfiler == null)
            {
                FindTargetProfiler();
            }

            if (_targetProfiler == null)
            {
                EditorGUILayout.HelpBox(
                    "No SoftBodyProfiler found in scene. Make sure you have a SoftBodyPhysics component with profiling enabled.",
                    MessageType.Warning);
                return;
            }

            // Check if profiler is active
            if (!_targetProfiler.enabled || !_targetProfiler.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox("SoftBodyProfiler is disabled or inactive.", MessageType.Warning);
                return;
            }

            // Display metrics with scroll view
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DisplayMetrics();
            EditorGUILayout.EndScrollView();
        }

        private void FindTargetProfiler()
        {
            if (!Application.isPlaying)
            {
                _targetProfiler = null;
                return;
            }

            _targetProfiler = FindFirstObjectByType<SoftBodyProfiler>();

            if (!_targetProfiler)
            {
                var softBodyPhysics = FindFirstObjectByType<SoftBodyPhysics>();
                if (softBodyPhysics)
                {
                    _targetProfiler = softBodyPhysics.GetComponent<SoftBodyProfiler>();
                }
            }
        }

        private void DisplayMetrics()
        {
            if (_targetProfiler == null || !Application.isPlaying)
            {
                EditorGUILayout.HelpBox("No profiling data available.", MessageType.Info);
                return;
            }

            var avgMetrics = _targetProfiler.GetAverageMetrics();

            // Performance overview
            EditorGUILayout.LabelField("Performance Metrics (60-frame average)", EditorStyles.boldLabel);

            // Frame time breakdown box
            GUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Frame Time Breakdown:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Total Frame Time: {avgMetrics.TotalFrameTime:F2} ms");

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                $"Integration: {avgMetrics.IntegrationTime:F2} ms ({GetPercentage(avgMetrics.IntegrationTime, avgMetrics.TotalFrameTime):F1}%)");
            EditorGUILayout.LabelField(
                $"Constraint Solving: {avgMetrics.ConstraintSolvingTime:F2} ms ({GetPercentage(avgMetrics.ConstraintSolvingTime, avgMetrics.TotalFrameTime):F1}%)");
            EditorGUILayout.LabelField(
                $"Volume Constraints: {avgMetrics.VolumeConstraintTime:F2} ms ({GetPercentage(avgMetrics.VolumeConstraintTime, avgMetrics.TotalFrameTime):F1}%)");
            EditorGUILayout.LabelField(
                $"Collision: {avgMetrics.CollisionTime:F2} ms ({GetPercentage(avgMetrics.CollisionTime, avgMetrics.TotalFrameTime):F1}%)");
            EditorGUILayout.LabelField(
                $"Mesh Update: {avgMetrics.MeshUpdateTime:F2} ms ({GetPercentage(avgMetrics.MeshUpdateTime, avgMetrics.TotalFrameTime):F1}%)");
            EditorGUI.indentLevel--;
            GUILayout.EndVertical();

            EditorGUILayout.Space();

            // System information box
            GUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("System Information:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Active Particles: {avgMetrics.ActiveParticles:N0}");
            EditorGUILayout.LabelField($"Active Constraints: {avgMetrics.ActiveConstraints:N0}");
            EditorGUILayout.LabelField($"Solver Iterations: {avgMetrics.SolverIterations}");
            EditorGUILayout.LabelField($"Memory Usage: {avgMetrics.MemoryUsageMb:F1} MB");

            if (avgMetrics.SolverIterations > 0)
            {
                EditorGUILayout.LabelField(
                    $"Constraint Time/Iteration: {avgMetrics.ConstraintSolvingTime / avgMetrics.SolverIterations:F2} ms");
                EditorGUILayout.LabelField(
                    $"Collision Time/Iteration: {avgMetrics.CollisionTime / avgMetrics.SolverIterations:F2} ms");
            }

            GUILayout.EndVertical();

            EditorGUILayout.Space();

            // Performance recommendations
            ShowPerformanceRecommendations(avgMetrics);
        }

        private static float GetPercentage(float part, float total)
        {
            return total > 0 ? part / total * 100f : 0f;
        }

        private static void ShowPerformanceRecommendations(PerformanceMetrics metrics)
        {
            EditorGUILayout.LabelField("Optimization Recommendations", EditorStyles.boldLabel);

            if (metrics.TotalFrameTime > 16.67f)
            {
                EditorGUILayout.HelpBox(
                    $"Frame time is {metrics.TotalFrameTime:F1}ms (target: <16.7ms for 60 FPS). Consider optimizations below.",
                    MessageType.Warning);
            }

            if (metrics.ConstraintSolvingTime > metrics.TotalFrameTime * 0.6f)
            {
                EditorGUILayout.HelpBox(
                    "Constraint solving is the main bottleneck. Try:\n• Graph coloring optimization\n• Reduce solver iterations\n• Implement constraint LOD",
                    MessageType.Info);
            }

            if (metrics.MeshUpdateTime > 2f)
            {
                EditorGUILayout.HelpBox(
                    "Mesh updates are expensive. Try:\n• Reduce mesh update frequency\n• Use async GPU readback\n• Implement mesh LOD",
                    MessageType.Info);
            }

            if (metrics.CollisionTime > metrics.TotalFrameTime * 0.3f)
            {
                EditorGUILayout.HelpBox(
                    "Collision solving is expensive. Try:\n• Reduce collision iterations\n• Optimize collider count\n• Use spatial partitioning",
                    MessageType.Info);
            }

            if (metrics.ActiveParticles > 1000)
            {
                EditorGUILayout.HelpBox(
                    $"High particle count ({metrics.ActiveParticles:N0}). Consider:\n• Distance-based LOD\n• Particle culling\n• Sleep system",
                    MessageType.Info);
            }

            if (metrics.TotalFrameTime <= 16.67f && metrics.ConstraintSolvingTime <= metrics.TotalFrameTime * 0.6f &&
                metrics.MeshUpdateTime <= 2f && metrics.CollisionTime <= metrics.TotalFrameTime * 0.3f &&
                metrics.ActiveParticles <= 1000)
            {
                EditorGUILayout.HelpBox("Performance looks good! Frame time is within acceptable limits.",
                    MessageType.Info);
            }
        }
    }
}

#endif