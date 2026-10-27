using _Game.Components;
using _Game.Shared;
using _Game.Util;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    public partial struct BeaconArrivalSystem : ISystem // TODO: Extend this to not only be used for the beacon but also for other destroyable entities.
    {
        private ComponentLookup<ContactDamage> _contactDamageLookup;
        private ComponentLookup<Health> _healthLookup;
        private BufferLookup<DamageBufferElement> _damageLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeaconStats>();

            _contactDamageLookup = state.GetComponentLookup<ContactDamage>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _damageLookup = state.GetBufferLookup<DamageBufferElement>(isReadOnly: false);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.EnemyGridValid || !EnemyDataShared.IsValid) return;

            _contactDamageLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _damageLookup.Update(ref state);

            SpatialGrid grid = SpatialGridShared.EnemyGrid;

            foreach ((RefRO<BeaconStats> stats, RefRO<Health> health, RefRO<LocalTransform> transform, Entity beaconEntity) in
                SystemAPI.Query<RefRO<BeaconStats>, RefRO<Health>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!health.ValueRO.IsAlive) continue;

                float2 beaconPos = transform.ValueRO.Position.xy;
                float radiusSq = stats.ValueRO.ArrivalRadius * stats.ValueRO.ArrivalRadius;
                float totalDamage = 0f;

                foreach (int i in grid.QueryNeighbors(beaconPos, 1))
                {
                    if (math.distancesq(grid.Positions[i], beaconPos) > radiusSq) continue;

                    Entity enemy = grid.Entities[i];
                    if (!_contactDamageLookup.TryGetComponent(enemy, out ContactDamage contactDamage)) continue;
                    if (!_healthLookup.TryGetComponent(enemy, out Health enemyHealth) || !enemyHealth.IsAlive) continue;

                    totalDamage += contactDamage.Value;

                    // TODO: Destroy enemy on contact?
                    _damageLookup[enemy].Add(new DamageBufferElement { Value = float.MaxValue });
                }

                if (totalDamage <= 0f) continue;
                _damageLookup[beaconEntity].Add(new DamageBufferElement { Value = totalDamage });
            }
        }
    }
}