using Unity.Collections;
using Unity.Mathematics;

namespace _Game.Shared
{
    public static class FlowFieldShared
    {
        // Written once by FlowFieldSystem.OnCreate
        public static float CellSize;
        public static int GridWidth;
        public static int GridHeight;

        // Written each BFS recompute
        public static NativeArray<float2> Directions;
        public static NativeArray<bool> Blocked;

        public static bool IsValid => Directions.IsCreated && Directions.Length > 0;
    }
}