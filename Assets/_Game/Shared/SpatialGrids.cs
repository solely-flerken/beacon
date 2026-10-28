using _Game.Util;
using Unity.Entities;

namespace _Game.Shared
{
    public struct SpatialGrids : IComponentData
    {
        public SpatialGrid Enemies;
        public SpatialGrid Allies;

        public bool EnemiesValid => Enemies.IsCreated && Enemies.Count > 0;
        public bool AlliesValid => Allies.IsCreated && Allies.Count > 0;
    }
}