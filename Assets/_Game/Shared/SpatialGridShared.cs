using Unity.Collections;
using Unity.Mathematics;

namespace _Game.Shared
{
    public static class SpatialGridShared
    {
        // Set once in SpatialGridSystem.OnCreate
        public static float CellSize;
        public static int GridWidth;
        public static int GridHeight;

        // Rebuilt each frame by SpatialGridSystem
        public static NativeParallelMultiHashMap<int2, int> CellMap;

        public static bool IsValid => CellMap.IsCreated;
    }
}