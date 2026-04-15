using System.Collections.Generic;
using _Game.Components;

namespace _Game.Gameplay.Waves
{
    public static class WaveGenerator
    {
        public static WaveDefinition Generate(int waveNumber)
        {
            int basicCount = 20 + waveNumber * 3;

            return new WaveDefinition
            {
                WaveNumber = waveNumber,
                SpawnGroups = new List<SpawnGroup>
                {
                    new() { Type = EnemyType.Basic, Count = basicCount },
                }
            };
        }
    }
}
