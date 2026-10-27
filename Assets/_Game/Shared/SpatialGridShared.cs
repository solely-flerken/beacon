using _Game.Util;

namespace _Game.Shared
{
    public static class SpatialGridShared
    {
        // Rebuilt each frame by SpatialGridSystem
        public static SpatialGrid EnemyGrid;
        public static SpatialGrid AllyGrid;

        public static bool EnemyGridValid => EnemyGrid.IsCreated && EnemyDataShared.IsValid;
        public static bool AllyGridValid => AllyGrid.IsCreated && AllyDataShared.IsValid;
    }
}
