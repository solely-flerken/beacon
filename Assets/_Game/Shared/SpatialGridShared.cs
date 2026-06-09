using Unity.Collections;
using Unity.Mathematics;

namespace _Game.Shared
{
    public static class SpatialGridShared
    {
        // Set once on first update
        public static float CellSize;

        // Rebuilt each frame by SpatialGridSystem
        public static NativeParallelMultiHashMap<int2, int> EnemyCellMap;
        public static NativeParallelMultiHashMap<int2, int> AllyCellMap;
        
        public static bool EnemyGridValid => EnemyCellMap.IsCreated && EnemyDataShared.IsValid;
        public static bool AllyGridValid => AllyCellMap.IsCreated && AllyDataShared.IsValid;
    }
}