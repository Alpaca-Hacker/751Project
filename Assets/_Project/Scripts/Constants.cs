using UnityEngine;

namespace SoftBody.Scripts
{
    public static class Constants
    {
        public static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        public static readonly int Gravity = Shader.PropertyToID("gravity");
        public static readonly int Damping = Shader.PropertyToID("damping");
        public static readonly int WorldPosition = Shader.PropertyToID("worldPosition");
        public static readonly int ParticleCount = Shader.PropertyToID("particleCount");
        public static readonly int ConstraintCount = Shader.PropertyToID("constraintCount");
        public static readonly int LambdaDecay = Shader.PropertyToID("lambdaDecay");
        public static readonly int Particles = Shader.PropertyToID("particles");
        public static readonly int Constraints = Shader.PropertyToID("constraints");
        public static readonly int Vertices = Shader.PropertyToID("vertices");
        public static readonly int DebugBuffer = Shader.PropertyToID("debugBuffer");
        public static readonly int CurrentColourGroup = Shader.PropertyToID("currentColourGroup");
        public static readonly int VolumeConstraintCount = Shader.PropertyToID("volumeConstraintCount");
        public static readonly int VolumeConstraints = Shader.PropertyToID("volumeConstraints");
        public static readonly int CollisionCompliance = Shader.PropertyToID("collisionCompliance");
        public static readonly int PreviousPositions = Shader.PropertyToID("previousPositions");
        public static readonly int Colliders = Shader.PropertyToID("colliders");
        public static readonly int ColliderCount = Shader.PropertyToID("colliderCount");
        public static readonly int CollisionCorrections = Shader.PropertyToID("collisionCorrections");
    }
}