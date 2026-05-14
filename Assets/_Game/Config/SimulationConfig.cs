namespace _Game.Config
{
    public static class SimulationConfig
    {
        /// <summary>
        /// Maximum delta time used for gameplay simulation (seconds).
        /// Prevents large lag spikes from causing oversized position jumps.
        /// </summary>
        public const float MaxDeltaTime = 1f / 20f;
    }
}
