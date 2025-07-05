using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public class NoColouringAlgorithm : IGraphColouringAlgorithm
    {
        public void ApplyColouring(List<Constraint> constraints, int particleCount)
        {
            // Assign each constraint its own unique colour group
            for (var i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                c.ColourGroup = i;
                constraints[i] = c;
            }
        }
    }
}