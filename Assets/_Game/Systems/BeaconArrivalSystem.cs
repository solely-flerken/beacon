using _Game.Components;
using _Game.Core;
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

            foreach ((RefRW<BeaconStats> stats, RefRO<LocalTransform> transform) in SystemAPI.Query<RefRW<BeaconStats>, RefRO<LocalTransform>>())
            {
                if (!stats.ValueRO.IsAlive) continue;

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
                        totalDamage += state.EntityManager.GetComponentData<ContactDamage>(enemy).Value;

                        // TODO: Refactor - can possibly code enemies as projectiles themselves and make beacon able to be damaged by them
                        float3 pos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                        EnemyReward reward = state.EntityManager.GetComponentData<EnemyReward>(enemy);
                        DeathEvent deathEvent = new() { Position = pos, Exp = reward.ExpValue };

                        if (DotsBridge.DeathQueue.IsCreated) DotsBridge.DeathQueue.Enqueue(deathEvent);
                        if (DotsBridge.ExpOrbQueue.IsCreated) DotsBridge.ExpOrbQueue.Enqueue(deathEvent);
                        
                        ecb.DestroyEntity(enemy);
                    } while (grid.TryGetNextValue(out idx, ref it));
                }

                if (totalDamage <= 0f) continue;

                stats.ValueRW.CurrentHp = math.max(0f, stats.ValueRO.CurrentHp - totalDamage);
                EventBus.FireBeaconHit(stats.ValueRO.CurrentHp, stats.ValueRO.MaxHp);

                if (stats.ValueRO.CurrentHp <= 0f)
                {
                    stats.ValueRW.IsAlive = false;
                    ecb.AddComponent<BeaconDeathTag>(ecb.CreateEntity());
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}