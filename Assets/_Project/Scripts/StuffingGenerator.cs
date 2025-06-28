using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    public static class StuffingGenerator
    {
        public static void CreateStuffedBodyStructure(List<Particle> particles,
            List<Constraint> constraints, List<VolumeConstraint> volumeConstraints,
            SoftBodySettings settings, Transform transform)
        {
            var initialConstraintCount = constraints.Count;
            var maxAdditionalConstraints = settings.maxAdditionalConstraints;

            Debug.Log($"Starting stuffed body creation. Initial constraints: {initialConstraintCount}");
            Debug.Log($"Constraint budget: {maxAdditionalConstraints} additional constraints");

            var surfaceParticles = new List<int>();
            var interiorParticles = new List<int>();

            ClassifyParticles(particles, surfaceParticles, interiorParticles);

            if (interiorParticles.Count == 0)
            {
                GenerateStuffingParticles(particles, interiorParticles, settings, transform);
            }

            var constraintsBefore = constraints.Count;
            CreateStuffingNetwork(interiorParticles, particles, constraints, settings);
            var stuffingConstraints = constraints.Count - constraintsBefore;

            constraintsBefore = constraints.Count;
            CreateSkinConstraints(surfaceParticles, interiorParticles, particles, constraints, settings);
            var skinConstraints = constraints.Count - constraintsBefore;

            var volumeBefore = volumeConstraints.Count;
            CreatePressureChambers(particles, volumeConstraints, surfaceParticles, interiorParticles, settings);
            var volumeConstraintsAdded = volumeConstraints.Count - volumeBefore;

            var totalAdded = constraints.Count - initialConstraintCount;

            Debug.Log($"Stuffed body creation complete:");
            Debug.Log($"  - Stuffing constraints: {stuffingConstraints}");
            Debug.Log($"  - Skin constraints: {skinConstraints}");
            Debug.Log($"  - Volume constraints: {volumeConstraintsAdded}");
            Debug.Log($"  - Total added: {totalAdded} (budget: {maxAdditionalConstraints})");
            Debug.Log($"  - Final total: {constraints.Count} constraints");

            if (totalAdded > maxAdditionalConstraints)
            {
                Debug.LogWarning($"Exceeded constraint budget! Consider reducing stuffing density.");
            }
        }

        private static void ClassifyParticles(List<Particle> particles, List<int> surfaceParticles, List<int> interiorParticles)
        {
            // Calculate bounding box
            var bounds = CalculateParticleBounds(particles);
            var threshold = Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z) * 0.15f; // 15% inset

            for (var i = 0; i < particles.Count; i++)
            {
                var pos = particles[i].Position;
                var distanceFromSurface = CalculateDistanceFromSurface(pos, bounds);

                if (distanceFromSurface < threshold)
                {
                    surfaceParticles.Add(i);
                }
                else
                {
                    interiorParticles.Add(i);
                }
            }
        }

        private static void CreateStuffingNetwork(List<int> interiorParticles, 
            List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
        {
            if (interiorParticles.Count == 0)
            {
                return;
            }
    
            var maxDistance = CalculateAverageEdgeLength(particles) * 2.0f;
            var maxConnectionsPerParticle = 6;
            var totalConstraintsAdded = 0;
            var maxTotalStuffingConstraints = settings.maxStuffingConstraints;
    
            foreach (var i in interiorParticles)
            {
                if (totalConstraintsAdded >= maxTotalStuffingConstraints)
                {
                    break;
                }
        
                var connections = 0;
                var nearbyParticles = FindNearestParticles(i, interiorParticles, particles, maxConnectionsPerParticle * 2);
        
                foreach (var j in nearbyParticles)
                {
                    if (i >= j || connections >= maxConnectionsPerParticle)
                    {
                        break;
                    }
                    if (totalConstraintsAdded >= maxTotalStuffingConstraints)
                    {
                        break;
                    }
            
                    var distance = Vector3.Distance(particles[i].Position, particles[j].Position);
                    if (distance < maxDistance)
                    {
                        SoftBodyGenerator.AddConstraint(particles, constraints, i, j, settings.structuralCompliance * 5f);
                        connections++;
                        totalConstraintsAdded++;
                    }
                }
            }
    
            Debug.Log($"Created {totalConstraintsAdded} stuffing network constraints (performance limited)");
        }

        private static List<int> FindNearestParticles(int particleIdx, List<int> candidateParticles, 
            List<Particle> particles, int maxCount)
        {
            var pos = particles[particleIdx].Position;
    
            return candidateParticles
                .Where(idx => idx != particleIdx)
                .OrderBy(idx => Vector3.SqrMagnitude(particles[idx].Position - pos))
                .Take(maxCount)
                .ToList();
        }

        private static void CreateSkinConstraints(List<int> surfaceParticles, List<int> interiorParticles,
            List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
        {
            if (interiorParticles.Count == 0) return;
    
            var skinCompliance = settings.structuralCompliance * 3f;
            var maxSkinConstraints = settings.maxSkinConstraints;
            var skinConstraintsAdded = 0;
    
            // Only connect every Nth surface particle to reduce connections
            var surfaceStep = Mathf.Max(1, surfaceParticles.Count / 30); 
    
            for (var i = 0; i < surfaceParticles.Count; i += surfaceStep)
            {
                if (skinConstraintsAdded >= maxSkinConstraints) break;
        
                var surfaceIdx = surfaceParticles[i];
                var maxConnections = 2; 
                var connections = 0;
                
                var nearestInterior = FindNearestParticles(surfaceIdx, interiorParticles, particles, 5);
        
                foreach (var interiorIdx in nearestInterior)
                {
                    if (connections >= maxConnections || skinConstraintsAdded >= maxSkinConstraints)
                    {
                        break;
                    }
            
                    var distance = Vector3.Distance(particles[surfaceIdx].Position, particles[interiorIdx].Position);
                    var connectionThreshold = CalculateAverageEdgeLength(particles) * 1.5f;
            
                    if (distance < connectionThreshold)
                    {
                        SoftBodyGenerator.AddConstraint(particles, constraints, surfaceIdx, interiorIdx, skinCompliance);
                        connections++;
                        skinConstraintsAdded++;
                    }
                }
            }
    
            Debug.Log($"Created {skinConstraintsAdded} skin constraints (performance limited)");
        }

        private static void GenerateStuffingParticles(List<Particle> particles,
            List<int> interiorParticles, SoftBodySettings settings, Transform transform)
        {
            var bounds = CalculateParticleBounds(particles);
            var maxStuffingParticles = Mathf.Min(settings.maxStuffingParticles, particles.Count / 4);
            var stuffingCount = Mathf.RoundToInt(maxStuffingParticles * settings.stuffingDensity);
            stuffingCount = Mathf.Clamp(stuffingCount, 5, maxStuffingParticles);
    
            Debug.Log($"Generating {stuffingCount} stuffing particles (capped for performance)");

            for (var i = 0; i < stuffingCount; i++)
            {
                // Generate random interior point
                var localPos = new Vector3(
                    Random.Range(bounds.min.x * 0.7f, bounds.max.x * 0.7f),
                    Random.Range(bounds.min.y * 0.7f, bounds.max.y * 0.7f),
                    Random.Range(bounds.min.z * 0.7f, bounds.max.z * 0.7f)
                );

                var stuffingParticle = new Particle
                {
                    Position = transform.TransformPoint(localPos),
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / (settings.mass * 0.5f) // Lighter stuffing particles
                };

                particles.Add(stuffingParticle);
                interiorParticles.Add(particles.Count - 1);
            }

            Debug.Log($"Generated {stuffingCount} stuffing particles");
        }

        private static void AddPressureConstraint(List<Particle> particles,
            List<VolumeConstraint> volumeConstraints, int p1, int p2, int p3, int p4,
            float compliance, float pressureMultiplier)
        {
            var pos1 = particles[p1].Position;
            var pos2 = particles[p2].Position;
            var pos3 = particles[p3].Position;
            var pos4 = particles[p4].Position;

            var restVolume = Vector3.Dot(pos1 - pos4, Vector3.Cross(pos2 - pos4, pos3 - pos4)) / 6.0f;

            if (Mathf.Abs(restVolume) > 0.001f)
            {
                volumeConstraints.Add(new VolumeConstraint
                {
                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                    RestVolume = Mathf.Abs(restVolume),
                    Compliance = compliance,
                    Lambda = 0,
                    PressureMultiplier = pressureMultiplier
                });
            }
        }

        private static Bounds CalculateParticleBounds(List<Particle> particles)
        {
            if (particles.Count == 0) return new Bounds();

            var min = particles[0].Position;
            var max = particles[0].Position;

            foreach (var particle in particles)
            {
                min = Vector3.Min(min, particle.Position);
                max = Vector3.Max(max, particle.Position);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static float CalculateDistanceFromSurface(Vector3 position, Bounds bounds)
        {
            // Calculate minimum distance to any face of the bounding box
            var center = bounds.center;
            var extents = bounds.extents;

            var distances = new float[]
            {
                extents.x - Mathf.Abs(position.x - center.x), // Distance to left/right faces
                extents.y - Mathf.Abs(position.y - center.y), // Distance to top/bottom faces
                extents.z - Mathf.Abs(position.z - center.z) // Distance to front/back faces
            };

            return Mathf.Min(distances);
        }

        private static void CreatePressureChambers(List<Particle> particles, 
            List<VolumeConstraint> volumeConstraints, List<int> surfaceParticles, 
            List<int> interiorParticles, SoftBodySettings settings)
        {
            if (interiorParticles.Count == 0) return;
    
            var pressureCompliance = settings.volumeCompliance * 0.1f;
            var maxVolumeConstraints = settings.maxVolumeConstraints;
            var volumeConstraintsAdded = 0;
            
            var interiorStep = Mathf.Max(1, interiorParticles.Count / 10); 
    
            for (var i = 0; i < interiorParticles.Count; i += interiorStep)
            {
                if (volumeConstraintsAdded >= maxVolumeConstraints) break;
        
                var interiorIdx = interiorParticles[i];
                var nearestSurface = FindNearestSurfaceParticles(interiorIdx, surfaceParticles, particles, 3);
        
                if (nearestSurface.Count >= 3)
                {
                    AddPressureConstraint(particles, volumeConstraints, 
                        interiorIdx, nearestSurface[0], nearestSurface[1], nearestSurface[2],
                        pressureCompliance, settings.pressureResistance);
                    volumeConstraintsAdded++;
                }
            }
    
            Debug.Log($"Created {volumeConstraintsAdded} pressure chamber constraints (performance limited)");
        }

        private static List<int> FindNearestSurfaceParticles(int interiorIdx,
            List<int> surfaceParticles, List<Particle> particles, int count)
        {
            var interiorPos = particles[interiorIdx].Position;

            return surfaceParticles
                .OrderBy(idx => Vector3.Distance(particles[idx].Position, interiorPos))
                .Take(count)
                .ToList();
        }
        
        private static float CalculateAverageEdgeLength(List<Particle> particles)
        {
            if (particles.Count < 2) return 1f;

            var totalDistance = 0f;
            var sampleCount = Mathf.Min(100, particles.Count * particles.Count / 4);

            for (var i = 0; i < sampleCount; i++)
            {
                var a = Random.Range(0, particles.Count);
                var b = Random.Range(0, particles.Count);
                if (a != b)
                {
                    totalDistance += Vector3.Distance(particles[a].Position, particles[b].Position);
                }
            }

            return totalDistance / sampleCount;
        }
    }
}