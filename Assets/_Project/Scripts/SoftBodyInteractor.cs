using System.Collections.Generic;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodyInteractor : MonoBehaviour
    {
        [Header("Target")] [SerializeField] private SoftBodyPhysics targetSoftBody;

        [Header("Interaction Settings")] [SerializeField]
        private float pokeStrength = 10f;

        [SerializeField] private float pokeRadius = 0.5f;
        [SerializeField] private float continuousForceStrength = 5f;

        [Header("Interaction Modes")] [SerializeField]
        private bool enablePoke = true;

        [SerializeField] private bool enableContinuousForce = true;
        [SerializeField] private bool enableGrab = true;

        [Header("Visual Feedback")] [SerializeField]
        private GameObject interactionIndicatorPrefab;

        [SerializeField] private bool showVisualFeedback = true;

        private Camera _mainCamera;
        private bool _isGrabbing = false;
        private Vector3 _lastGrabPosition;
        private GameObject _currentIndicator;
        private LineRenderer _pokeRay;
        private Vector3 _grabWorldPosition;
        private List<int> _grabbedParticleIndices = new();
        private Vector3 _grabOffset;
        private float _grabDistance;
        private Vector3 _lastMousePosition;

        private void Start()
        {
            _mainCamera = Camera.main;
            if (targetSoftBody == null)
            {
                targetSoftBody = FindFirstObjectByType<SoftBodyPhysics>();
            }

            // Ensure the soft body has a mesh collider for raycasting
            EnsureMeshCollider();
            CreateVisualFeedback();
        }

        private void EnsureMeshCollider()
        {
            if (targetSoftBody == null) return;

            var meshCollider = targetSoftBody.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = targetSoftBody.gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = true; // Required for raycasting with deforming meshes
                Debug.Log("Added MeshCollider for interaction");
            }
        }

        private void Update()
        {
            if (!targetSoftBody) return;

            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

            // Show interaction preview
            ShowInteractionPreview(ray);

            // Handle different interaction modes
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseDown(ray);
            }
            else if (Input.GetMouseButton(0))
            {
                HandleMouseHeld(ray);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                HandleMouseUp();
            }

            // Additional controls
            HandleKeyboardInput(ray);
        }

        private void HandleMouseDown(Ray ray)
        {
            if (enablePoke)
            {
                PerformPoke(ray);
            }

            if (enableGrab)
            {
                StartGrab(ray);
            }
        }

        private void HandleMouseHeld(Ray ray)
        {
            if (_isGrabbing && enableGrab)
            {
                ContinueGrab(ray);
            }
            else if (enableContinuousForce)
            {
                ApplyContinuousForce(ray);
            }
        }

        private void HandleMouseUp()
        {
            if (_isGrabbing)
            {
                EndGrab();
            }
        }

        private void PerformPoke(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.gameObject == targetSoftBody.gameObject)
                {
                    var impulse = ray.direction * pokeStrength;
                    targetSoftBody.PokeAtPosition(hit.point, impulse, pokeRadius);

                    // Visual feedback
                    CreatePokeEffect(hit.point);
                }
            }
        }

        private void ApplyContinuousForce(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.gameObject == targetSoftBody.gameObject)
                {
                    var force = ray.direction * continuousForceStrength;
                    targetSoftBody.ApplyContinuousForce(hit.point, force, pokeRadius);
                }
            }
        }

        private void HandleKeyboardInput(Ray ray)
        {
            // R key to reset
            if (Input.GetKeyDown(KeyCode.R))
            {
                // You can add a reset method to SoftBodyPhysics if needed
                Debug.Log("Reset requested");
            }

            // Space to apply upward force
            if (Input.GetKey(KeyCode.Space))
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    if (hit.collider.gameObject == targetSoftBody.gameObject)
                    {
                        targetSoftBody.ApplyContinuousForce(hit.point, Vector3.up * 20f, pokeRadius);
                    }
                }
            }
        }

        private void CreateVisualFeedback()
        {
            if (!showVisualFeedback) return;

            // Create interaction indicator sphere
            if (interactionIndicatorPrefab == null)
            {
                // Create a simple sphere if no prefab is assigned
                _currentIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _currentIndicator.name = "Interaction Indicator";

                // Make it wireframe-like
                var renderer = _currentIndicator.GetComponent<Renderer>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.color = new Color(1, 1, 0, 0.5f); // Yellow, semi-transparent
                renderer.material = material;

                // Remove collider
                Destroy(_currentIndicator.GetComponent<Collider>());
            }
            else
            {
                _currentIndicator = Instantiate(interactionIndicatorPrefab);
            }

            // Create line renderer for ray visualization
            var rayObject = new GameObject("Poke Ray");
            _pokeRay = rayObject.AddComponent<LineRenderer>();
            _pokeRay.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _pokeRay.startColor = Color.red;
            _pokeRay.endColor = Color.red;
            _pokeRay.startWidth = 0.02f;
            _pokeRay.endWidth = 0.02f;
            _pokeRay.positionCount = 2;

            // Initially hide both
            _currentIndicator.SetActive(false);
            _pokeRay.enabled = false;
        }

        private void ShowInteractionPreview(Ray ray)
        {
            if (!showVisualFeedback) return;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.gameObject == targetSoftBody.gameObject)
                {
                    // Show and position indicator
                    _currentIndicator.SetActive(true);
                    _currentIndicator.transform.position = hit.point;
                    _currentIndicator.transform.localScale = Vector3.one * (pokeRadius * 2f);

                    // Change color based on interaction state
                    var renderer = _currentIndicator.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = _isGrabbing
                            ? new Color(0, 1, 0, 0.5f)
                            : // Green when grabbing
                            new Color(1, 1, 0, 0.5f); // Yellow normally
                    }

                    // Show ray
                    _pokeRay.enabled = true;
                    _pokeRay.SetPosition(0, ray.origin);
                    _pokeRay.SetPosition(1, hit.point);
                }
                else
                {
                    HideVisualFeedback();
                }
            }
            else
            {
                HideVisualFeedback();
            }
        }

        private void HideVisualFeedback()
        {
            if (_currentIndicator != null)
                _currentIndicator.SetActive(false);

            if (_pokeRay != null)
                _pokeRay.enabled = false;
        }

        private void CreatePokeEffect(Vector3 position)
        {
            Debug.Log($"Poke effect at {position}");

            if (!showVisualFeedback) return;

            // Create a temporary effect
            StartCoroutine(ShowTemporaryEffect(position));
        }

        private System.Collections.IEnumerator ShowTemporaryEffect(Vector3 position)
        {
            // Create temporary effect sphere
            var effectSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effectSphere.name = "Poke Effect";
            effectSphere.transform.position = position;
            effectSphere.transform.localScale = Vector3.one * 0.1f;

            // Remove collider
            Destroy(effectSphere.GetComponent<Collider>());

            // Set red material
            var renderer = effectSphere.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = Color.red;
            renderer.material = material;

            // Animate scale up then destroy
            float timer = 0f;
            float duration = 0.5f;
            Vector3 startScale = Vector3.one * 0.1f;
            Vector3 endScale = Vector3.one * (pokeRadius * 2f);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // Scale up and fade out
                effectSphere.transform.localScale = Vector3.Lerp(startScale, endScale, t);

                var color = material.color;
                color.a = 1f - t;
                material.color = color;

                yield return null;
            }

            Destroy(effectSphere);
        }

        private void OnDestroy()
        {
            // Clean up visual objects
            if (_currentIndicator != null)
                Destroy(_currentIndicator);

            if (_pokeRay != null)
                Destroy(_pokeRay.gameObject);
        }

        private void StartGrab(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.gameObject == targetSoftBody.gameObject)
                {
                    _isGrabbing = true;
                    _grabWorldPosition = hit.point;
                    _grabDistance = Vector3.Distance(_mainCamera.transform.position, hit.point);

                    // Find and store indices of particles we're grabbing
                    FindGrabbedParticles(hit.point);

                    Debug.Log($"Grabbed {_grabbedParticleIndices.Count} particles at {hit.point}");
                }
            }
        }

        private void FindGrabbedParticles(Vector3 grabPoint)
        {
            _grabbedParticleIndices.Clear();

            var currentParticles = new Particle[targetSoftBody.ParticleCount];
            targetSoftBody.GetParticleData(currentParticles);

            var grabRadiusSq = (pokeRadius * 1.5f) * (pokeRadius * 1.5f);

            for (int i = 0; i < currentParticles.Length; i++)
            {
                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - grabPoint);
                if (distSq <= grabRadiusSq && currentParticles[i].InvMass > 0)
                {
                    _grabbedParticleIndices.Add(i);
                }
            }
        }

        private void ContinueGrab(Ray ray)
        {
            if (!_isGrabbing || _grabbedParticleIndices.Count == 0) return;

            // Calculate where the grab point should be now
            var targetGrabPosition = ray.GetPoint(_grabDistance);
            var grabMovement = targetGrabPosition - _grabWorldPosition;

            // Apply strong forces to move grabbed particles
            var currentParticles = new Particle[targetSoftBody.ParticleCount];
            targetSoftBody.GetParticleData(currentParticles);

            var springStrength = 200f; // Very strong spring force
            var damping = 0.9f;

            foreach (var particleIndex in _grabbedParticleIndices)
            {
                var p = currentParticles[particleIndex];

                // Calculate target position for this particle
                var targetPosition = p.Position + grabMovement;
                var displacement = targetPosition - p.Position;

                // Apply spring force
                var springForce = displacement * springStrength;

                // Apply damping to prevent oscillation
                p.Velocity.x *= damping;
                p.Velocity.y *= damping;
                p.Velocity.z *= damping;

                // Add spring force as velocity change
                var deltaV = springForce * p.InvMass * Time.deltaTime;
                p.Velocity.x += deltaV.x;
                p.Velocity.y += deltaV.y;
                p.Velocity.z += deltaV.z;

                currentParticles[particleIndex] = p;
            }

            // Update the buffer
            targetSoftBody.SetParticleData(currentParticles);

            // Update grab world position for next frame
            _grabWorldPosition = targetGrabPosition;
        }

        private void EndGrab()
        {
            _isGrabbing = false;
            _grabbedParticleIndices.Clear();
            Debug.Log("Stopped grabbing");
        }


        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 150, 300, 140));
            GUILayout.Label("Soft Body Interaction Controls:",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label("Left Click: Poke");
            GUILayout.Label("Hold Left Click: Continuous force");
            GUILayout.Label("Drag: Grab and pull");
            GUILayout.Label("Space + Mouse: Apply upward force");
            GUILayout.Label("R: Reset (if implemented)");
            GUILayout.EndArea();
        }
    }
}