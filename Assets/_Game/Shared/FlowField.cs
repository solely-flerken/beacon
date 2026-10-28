using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    public struct FlowField : IComponentData
    {
        // Written once by FlowFieldSystem on initialization
        public float CellSize;
        public int GridWidth;
        public int GridHeight;

        // Written each BFS recompute
        public NativeArray<float2> Directions;
        public NativeArray<bool> Blocked;

        /// <summary>
        /// Per-cell nearest attractor world position.
        /// When Directions[i] is zero, move directly toward NearestAttractorPos[i].
        /// </summary>
        public NativeArray<float2> NearestAttractorPos;

        public bool IsValid => Directions.IsCreated && Directions.Length > 0;
    }
}