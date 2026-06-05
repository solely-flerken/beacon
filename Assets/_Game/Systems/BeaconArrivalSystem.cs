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

        private ComponentLookup<ContactDamage> _contactDamageLookup;
        private ComponentLookup<Health> _healthLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeaconStats>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _contactDamageLookup = state.GetComponentLookup<ContactDamage>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.EnemyGridValid || !EnemyDataShared.IsValid) return;

            _contactDamageLookup.Update(ref state);
            _healthLookup.Update(ref state);

            NativeArray<float2> positions = EnemyDataShared.Positions;
            NativeArray<Entity> entities = EnemyDataShared.Entities;
            NativeParallelMultiHashMap<int2, int> grid = SpatialGridShared.EnemyCellMap;
            float cellSize = SpatialGridShared.CellSize;

            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach ((RefRO<BeaconStats> stats, RefRO<Health> health, RefRO<LocalTransform> transform, Entity beaconEntity) in
                SystemAPI.Query<RefRO<BeaconStats>, RefRO<Health>, RefRO<LocalTransform>>().WithEntityAccess())
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
                        if (!_contactDamageLookup.TryGetComponent(enemy, out ContactDamage contactDamage)) continue;
                        if (!_healthLookup.TryGetComponent(enemy, out Health enemyHealth) || !enemyHealth.IsAlive) continue;

                        totalDamage += contactDamage.Value;

                        // TODO: Destroy enemy on contact?
                        ecb.AppendToBuffer(enemy, new DamageBufferElement { Value = float.MaxValue });
                    } while (grid.TryGetNextValue(out idx, ref it));
                }

                if (totalDamage <= 0f) continue;
                ecb.AppendToBuffer(beaconEntity, new DamageBufferElement { Value = totalDamage });
            }
        }
    }
}