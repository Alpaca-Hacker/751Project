using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public class NaiveColouringAlgorithm : IGraphColouringAlgorithm
    {
        public void ApplyColouring(List<Constraint> constraints, int particleCount)
        {
            SoftBody.Scripts.GraphColouring.ApplyNaiveGraphColouring(constraints, false);
        }
    }
}