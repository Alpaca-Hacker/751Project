using UnityEngine;
using System.Diagnostics;

namespace SoftBody.Scripts.Performance
{
    public static class SpawnProfiler
    {
        public static void ProfileSpawn(System.Action spawnAction, string context = "Spawn")
        {
            var stopwatch = Stopwatch.StartNew();
            spawnAction();
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 5) // Log if > 5ms
            {
                UnityEngine.Debug.Log($"{context} took {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}