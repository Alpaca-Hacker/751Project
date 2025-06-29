namespace SoftBody.Scripts.Models
{
    public struct PerformanceMetrics
    {
        public float totalFrameTime;
        public float integrationTime;
        public float constraintSolvingTime;
        public float volumeConstraintTime;
        public float collisionTime;
        public float meshUpdateTime;
        public float lambdaDecayTime;      
        public float velocityUpdateTime;   
    
        public int activeParticles;
        public int activeConstraints;
        public int solverIterations;
        public float memoryUsageMB;
    
        // Calculated properties for analysis
        public float PhysicsTimePercentage => (constraintSolvingTime + volumeConstraintTime + collisionTime) / totalFrameTime * 100f;
        public float ConstraintTimePerIteration => constraintSolvingTime / solverIterations;
        public float CollisionTimePerIteration => collisionTime / solverIterations;
    }
}