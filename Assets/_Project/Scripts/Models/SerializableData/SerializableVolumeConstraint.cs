using System;

namespace SoftBody.Scripts.Models
{
    [Serializable]
    public struct SerializableVolumeConstraint
    {
        public int p1, p2, p3, p4;
        public float restVolume;
        public float compliance;
        public float pressureMultiplier;
        
        public VolumeConstraint ToVolumeConstraint()
        {
            return new VolumeConstraint
            {
                P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                RestVolume = restVolume,
                Compliance = compliance,
                Lambda = 0f,
                PressureMultiplier = pressureMultiplier
            };
        }
        
        public static SerializableVolumeConstraint FromVolumeConstraint(VolumeConstraint vc)
        {
            return new SerializableVolumeConstraint
            {
                p1 = vc.P1, p2 = vc.P2, p3 = vc.P3, p4 = vc.P4,
                restVolume = vc.RestVolume,
                compliance = vc.Compliance,
                pressureMultiplier = vc.PressureMultiplier
            };
        }
    }
}