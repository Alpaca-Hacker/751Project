namespace SoftBody.Scripts.Models
{
    public struct PerformanceMetrics
    {
        public float TotalFrameTime;
        public float IntegrationTime;
        public float ConstraintSolvingTime;
        public float VolumeConstraintTime;
        public float CollisionTime;
        public float MeshUpdateTime;
        public float LambdaDecayTime;      
        public float VelocityUpdateTime;   
    
        public int ActiveParticles;
        public int ActiveConstraints;
        public int SolverIterations;
        public float MemoryUsageMb;
    
        // Calculated properties for analysis
        public float PhysicsTimePercentage => (ConstraintSolvingTime + VolumeConstraintTime + CollisionTime) / TotalFrameTime * 100f;
        public float ConstraintTimePerIteration => ConstraintSolvingTime / SolverIterations;
        public float CollisionTimePerIteration => CollisionTime / SolverIterations;
    }
}