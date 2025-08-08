using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodyDebugger : MonoBehaviour
    {
        [Header("Debug Visualization")] public bool showParticles;
        public bool showConstraints;
        public bool showForces;
        public float particleSize = 0.1f;
        public Color particleColor = Color.red;
        public Color constraintColor = Color.green;
        public Color forceColor = Color.blue;

        [Header("Performance Monitoring")] public bool showPerformanceStats = true;

        private SoftBodyPhysics _softBody;
        private float _frameTime;
        private int _frameCount;
        private Vector3[] _currentParticlePositions;

        private void Start()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
        }

        private void Update()
        {
            if (showPerformanceStats)
            {
                _frameTime += Time.deltaTime;
                _frameCount++;
            }

            // Update particle positions for visualization
            if (_softBody && (showParticles || showConstraints))
            {
                UpdateParticlePositions();
            }
        }

        private void UpdateParticlePositions()
        {
            // Get current mesh vertices (which represent particle positions)
            var mesh = _softBody.GetComponent<MeshFilter>().mesh;
            if (mesh != null)
            {
                var vertices = mesh.vertices;
                _currentParticlePositions = new Vector3[vertices.Length];

                // Convert from local to world space
                for (var i = 0; i < vertices.Length; i++)
                {
                    _currentParticlePositions[i] = transform.TransformPoint(vertices[i]);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_softBody == null || _currentParticlePositions == null) return;

            if (showParticles)
            {
                Gizmos.color = particleColor;
                foreach (var pos in _currentParticlePositions)
                {
                    Gizmos.DrawWireSphere(pos, particleSize);
                }
            }

            if (showConstraints && _currentParticlePositions.Length > 0)
            {
                Gizmos.color = constraintColor;
                var res = Mathf.RoundToInt(Mathf.Pow(_currentParticlePositions.Length, 1f / 3f));

                // Draw constraint lines (simplified grid connections)
                for (var x = 0; x < res; x++)
                {
                    for (var y = 0; y < res; y++)
                    {
                        for (var z = 0; z < res; z++)
                        {
                            var index = x * res * res + y * res + z;
                            if (index >= _currentParticlePositions.Length) continue;

                            var pos = _currentParticlePositions[index];

                            // Draw connections to adjacent particles
                            if (x < res - 1)
                            {
                                var nextIndex = (x + 1) * res * res + y * res + z;
                                if (nextIndex < _currentParticlePositions.Length)
                                    Gizmos.DrawLine(pos, _currentParticlePositions[nextIndex]);
                            }

                            if (y < res - 1)
                            {
                                var nextIndex = x * res * res + (y + 1) * res + z;
                                if (nextIndex < _currentParticlePositions.Length)
                                    Gizmos.DrawLine(pos, _currentParticlePositions[nextIndex]);
                            }

                            if (z < res - 1)
                            {
                                var nextIndex = x * res * res + y * res + (z + 1);
                                if (nextIndex < _currentParticlePositions.Length)
                                    Gizmos.DrawLine(pos, _currentParticlePositions[nextIndex]);
                            }
                        }
                    }
                }
            }
        }

        // private void OnGUI()
        // {
        //     if (!showPerformanceStats) return;
        //
        //     GUILayout.BeginArea(new Rect(10, 10, 200, 150));
        //     GUILayout.Label("Soft Body Debug Info", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        //
        //     if (_frameCount > 0)
        //     {
        //         var avgFrameTime = _frameTime / _frameCount;
        //         var fps = 1f / avgFrameTime;
        //         GUILayout.Label($"FPS: {fps:F1}");
        //         GUILayout.Label($"Frame Time: {avgFrameTime * 1000:F2}ms");
        //     }
        //
        //     if (_softBody != null && _currentParticlePositions != null)
        //     {
        //         GUILayout.Label($"Particles: {_currentParticlePositions.Length}");
        //         GUILayout.Label("Simulation Active");
        //     }
        //
        //     GUILayout.EndArea();
        //
        //     // Reset counters periodically
        //     if (_frameCount > 60)
        //     {
        //         _frameTime = 0f;
        //         _frameCount = 0;
        //     }
        // }
    }
}
