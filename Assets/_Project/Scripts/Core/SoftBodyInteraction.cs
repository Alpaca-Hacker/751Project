using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class SoftBodyInteraction
    {
        private readonly SoftBodySimulation _simulation;
        private readonly SleepSystem _sleepSystem;
        private readonly SoftBodySettings _settings;

        public SoftBodyInteraction(SoftBodySimulation simulation, SleepSystem sleepSystem, SoftBodySettings settings)
        {
            _simulation = simulation;
            _sleepSystem = sleepSystem;
            _settings = settings;
        }

        public void PokeAtPosition(Vector3 worldPosition, Vector3 impulse, float radius = 1f)
        {
            if (_simulation.ParticleCount == 0)
            {
                Debug.LogWarning("Cannot poke, physics system not initialized.");
                return;
            }

            _sleepSystem?.WakeUp();
            _simulation.ApplyImpulse(worldPosition, impulse, radius);

            if (_settings.debugMessages)
            {
                Debug.Log($"Applied poke at {worldPosition} with impulse {impulse} and radius {radius}");
            }
        }

        public void ApplyContinuousForce(Vector3 worldPosition, Vector3 force, float radius = 1f)
        {
            if (_simulation.ParticleCount == 0) return;

            _sleepSystem?.WakeUp();
            _simulation.ApplyContinuousForce(worldPosition, force, radius);
        }

        public void SetWorldPosition(Vector3 newPosition)
        {
            if (_simulation.ParticleCount == 0)
            {
                Debug.LogWarning("Cannot set world position - physics system not initialized.");
                return;
            }

            _simulation.SetWorldPosition(newPosition);

            if (_settings.debugMessages)
            {
                Debug.Log($"Set world position to {newPosition}");
            }
        }

        public void ResetToInitialState(List<Particle> initialParticles)
        {
            if (initialParticles == null || initialParticles.Count == 0)
            {
                Debug.LogWarning("Cannot reset - no initial state available");
                return;
            }

            var resetParticles = initialParticles.Select(p =>
            {
                var particle = p;
                particle.Velocity = Vector4.zero;
                particle.Force = Vector4.zero;
                return particle;
            }).ToList();

            _simulation.SetParticleData(resetParticles);
            _sleepSystem?.WakeUp();

            if (_settings.debugMessages)
            {
                Debug.Log("Reset soft body to initial state");
            }
        }

        public void ResetVelocities()
        {
            _simulation?.ResetVelocities();
            
            if (_settings.debugMessages)
            {
                Debug.Log("Reset all velocities to zero");
            }
        }

        public void GetParticleData(Particle[] outputArray)
        {
            _simulation?.GetParticleData(outputArray);
        }

        public void SetParticleData(Particle[] inputArray)
        {
            _simulation?.SetParticleData(new List<Particle>(inputArray));
        }
    }
}