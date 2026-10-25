using System.Collections.Generic;

namespace _Game.Gameplay.Waves
{
    public class WaveDefinition
    {
        public int WaveNumber;
        public float SpawnDuration;
        public int TotalEnemies;

        /// <summary>
        /// Sorted by Time ascending.
        /// </summary>
        public List<SpawnPulse> Pulses;
    }

    /// <summary>
    /// A pack of enemies that spawns together.
    /// </summary>
    public class SpawnPulse
    {
        public float Time;
        public float Angle; // Radians, center of the spawn arc
        public List<SpawnGroup> Groups;
    }
}