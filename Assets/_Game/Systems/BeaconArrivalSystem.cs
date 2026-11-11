using _Game.Components;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
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
        private ComponentLookup<Despawn> _despawnLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeaconStats>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();

            _contactDamageLookup = state.GetComponentLookup<ContactDamage>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _despawnLookup = state.GetComponentLookup<Despawn>(isReadOnly: false);
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;
            if (!grids.EnemiesValid) return;

            _contactDamageLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _despawnLookup.Update(ref state);

            state.Dependency = new BeaconArrivalJob
            {
                Grid = grids.Enemies,
                ContactDamageLookup = _contactDamageLookup,
                HealthLookup = _healthLookup,
                DespawnLookup = _despawnLookup,
                DamageQueue = damageQueue,
            }.Schedule(state.Dependency);
        }
    }

    /// <summary>
    /// Despawns enemies that reach a beacon and queues their contact damage against it.
    /// </summary>
    [BurstCompile]
    internal partial struct BeaconArrivalJob : IJobEntity
    {
        [ReadOnly] public SpatialGrid Grid;
        [ReadOnly] public ComponentLookup<ContactDamage> ContactDamageLookup;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        public ComponentLookup<Despawn> DespawnLookup;
        public NativeQueue<DamageEvent> DamageQueue;

        private void Execute(Entity beaconEntity, in BeaconStats stats, in LocalTransform transform)
        {
            if (!HealthLookup.TryGetComponent(beaconEntity, out Health health) || health.Current <= 0f) return;

            float2 beaconPos = transform.Position.xy;
            float radiusSq = stats.ArrivalRadius * stats.ArrivalRadius;

            foreach (int i in Grid.QueryNeighbors(beaconPos, 1))
            {
                if (math.distancesq(Grid.Positions[i], beaconPos) > radiusSq) continue;

                Entity enemy = Grid.Entities[i];
                if (!ContactDamageLookup.TryGetComponent(enemy, out ContactDamage contactDamage)) continue;
                // Only deal arrival damage if the entity can be removed, and isn't being removed already
                if (!DespawnLookup.HasComponent(enemy) || DespawnLookup.IsComponentEnabled(enemy)) continue;

                DespawnLookup.SetComponentEnabled(enemy, true);

                if (contactDamage.Value <= 0f) continue;

                DamageQueue.Enqueue(new DamageEvent { Target = beaconEntity, Value = contactDamage.Value, Source = enemy });
            }
        }
    }
}
