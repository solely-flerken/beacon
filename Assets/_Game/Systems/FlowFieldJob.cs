using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [BurstCompile]
    public struct FlowFieldJob : IJob
    {
        // --- Input Data ---
        public NativeQueue<int2> Queue;
        public NativeArray<float> Distances;
        public NativeArray<float2> Directions;
        public NativeArray<float2> AttractorOwner;
        
        [ReadOnly] public NativeArray<bool> Blocked;
        [ReadOnly] public NativeArray<NavigationAttractor> Attractors;
        [ReadOnly] public NativeArray<LocalTransform> AttractorTransforms;

        // --- Config ---
        [ReadOnly] public int GridWidth;
        [ReadOnly] public int GridHeight;
        [ReadOnly] public float CellSize;

        // Pre-defined offsets for neighbors
        private static readonly int2[] NeighborOffsets =
        {
            new(0, 1), new(0, -1), new(1, 0), new(-1, 0), // Cardinal
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) // Diagonal
        };

        private const float DiagonalCost = 1.4142135f;
        private const float CardinalCost = 1.0f;

        [BurstCompile]
        public void Execute()
        {
            bool hasGlobalAttractor = false;
            for (int i = 0; i < Attractors.Length; i++)
            {
                if (!(Attractors[i].Range > 1000f)) continue;

                hasGlobalAttractor = true;
                break;
            }

            while (Queue.TryDequeue(out int2 current))
            {
                int currentIdx = FlowFieldUtil.GetIndex(current, GridWidth);
                float currentDist = Distances[currentIdx];
                float2 ownerPos = AttractorOwner[currentIdx];

                if (!hasGlobalAttractor && !IsWithinAttractionRange(current, currentDist)) continue;

                foreach (int2 offset in NeighborOffsets)
                {
                    int2 neighbor = current + offset;

                    if (!FlowFieldUtil.IsInsideGrid(neighbor, GridWidth, GridHeight)) continue;

                    int neighborIndex = FlowFieldUtil.GetIndex(neighbor, GridWidth);
                    if (Blocked[neighborIndex]) continue;

                    // Calculate move cost (Diagonal vs Cardinal)
                    float moveCost = offset.x != 0 && offset.y != 0 ? DiagonalCost : CardinalCost;
                    float newDist = currentDist + moveCost;

                    if (!(Distances[neighborIndex] > newDist)) continue;

                    Distances[neighborIndex] = newDist;
                    Directions[neighborIndex] = math.normalize(new float2(current.x - neighbor.x, current.y - neighbor.y));
                    AttractorOwner[neighborIndex] = ownerPos;
                    
                    Queue.Enqueue(neighbor);
                }
            }
        }

        private bool IsWithinAttractionRange(int2 cell, float pathDist)
        {
            if (pathDist == 0) return true;

            float2 worldPos = FlowFieldUtil.CellToWorld(cell, CellSize, GridWidth, GridHeight);

            for (int i = 0; i < Attractors.Length; i++)
            {
                // If we find even one attractor that covers this cell, return true
                float distanceSq = math.distancesq(worldPos, AttractorTransforms[i].Position.xy);
                if (distanceSq <= Attractors[i].Range * Attractors[i].Range) return true;
            }

            return false;
        }
    }
}