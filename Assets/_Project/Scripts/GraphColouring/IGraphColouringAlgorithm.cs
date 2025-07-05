using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public interface IGraphColouringAlgorithm
    {
        void ApplyColouring(List<Constraint> constraints, int particleCount);
    }
}