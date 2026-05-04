using _Game.Components;
using _Game.Shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BeaconArrivalSystem : ISystem // TODO: Extend this to not only be used for the beacon but also for other destroyable entities.
    {
        private static readonly int2[] NeighbourOffsets =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1,  0), new(0,  0), new(1,  0),
            new(-1,  1), new(0,  1), new(1,  1),
        };

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.IsValid || !EnemyDataShared.IsValid) return;

            NativeArray<float2> positions = EnemyDataShared.Positions;
            NativeArray<Entity> entities = EnemyDataShared.Entities;
            
            NativeParallelMultiHashMap<int2, int> grid = SpatialGridShared.CellMap;
            float cellSize = SpatialGridShared.CellSize;

            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);

            foreach ((RefRW<BeaconStats> stats, RefRW<Health> health, RefRO<LocalTransform> transform, Entity beaconEntity) in SystemAPI.Query<RefRW<BeaconStats>, RefRW<Health>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!health.ValueRO.IsAlive) continue;

                float2 beaconPos = transform.ValueRO.Position.xy;
                float radiusSq = stats.ValueRO.ArrivalRadius * stats.ValueRO.ArrivalRadius;
                int2 cell = new((int)math.floor(beaconPos.x / cellSize), (int)math.floor(beaconPos.y / cellSize));
                float totalDamage = 0f;

                foreach (int2 offset in NeighbourOffsets)
                {
                    if (!grid.TryGetFirstValue(cell + offset, out int idx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                    do
                    {
                        if (math.distancesq(positions[idx], beaconPos) > radiusSq) continue;

                        Entity enemy = entities[idx];
                        if (!state.EntityManager.HasComponent<ContactDamage>(enemy)) continue;
                        if (!state.EntityManager.GetComponentData<Health>(enemy).IsAlive) continue;
                        totalDamage += state.EntityManager.GetComponentData<ContactDamage>(enemy).Value;

                        // TODO: Destroy enemy on contact?
                        ecb.AppendToBuffer(enemy, new DamageBufferElement { Value = float.MaxValue });
                    } while (grid.TryGetNextValue(out idx, ref it));
                }

                if (totalDamage <= 0f) continue;
                ecb.AppendToBuffer(beaconEntity, new DamageBufferElement { Value = totalDamage });
            }

            ecb.Playback(state.EntityManager);
        }
    }
}