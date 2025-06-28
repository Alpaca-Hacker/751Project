namespace SoftBody.Scripts.Models
{
    public struct VolumeConstraint
    {
        public int P1, P2, P3, P4;
        public float RestVolume;
        public float Compliance;
        public float Lambda;
        public float PressureMultiplier;
    }
}