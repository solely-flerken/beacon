using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct EnemyStatUpdateSystem : ISystem
    {
        private ComponentLookup<EnemyTypeBlobRef> _blobModLookup;
        private BufferLookup<StatModifier> _typeModLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blobModLookup = state.GetComponentLookup<EnemyTypeBlobRef>(isReadOnly: true);
            _typeModLookup = state.GetBufferLookup<StatModifier>(isReadOnly: true);
            
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _blobModLookup.Update(ref state);
            _typeModLookup.Update(ref state);

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new SpawnInitJob
            {
                BlobLookup = _blobModLookup,
                TypeModLookup = _typeModLookup,
                Ecb = ecb
            }.ScheduleParallel();

            new UpdateStatsJob
            {
                BlobLookup = _blobModLookup,
                TypeModLookup = _typeModLookup,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Uninitialized))]
        public partial struct SpawnInitJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyTypeBlobRef> BlobLookup;
            [ReadOnly] public BufferLookup<StatModifier> TypeModLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute(
                Entity entity,
                [ChunkIndexInQuery] int chunkIndex,
                ref EnemySpeed speed,
                ref EnemyHealth health,
                ref ContactDamage contactDamage,
                in EnemyTierComponent tier,
                in EnemyTypeRef typeRef,
                in DynamicBuffer<StatModifier> entityMods)
            {
                ref BlobArray<TieredStatModifier> blob = ref BlobLookup[typeRef.Value].Value.Value.ConfigModifiers;
                DynamicBuffer<StatModifier> typeMods = TypeModLookup[typeRef.Value];

                speed.Value = StatResolver.ResolveEnemy(speed.BaseValue, ref blob, typeMods, entityMods, StatType.EnemySpeed, tier.Value);
                health.Max = StatResolver.ResolveEnemy(health.BaseMax, ref blob, typeMods, entityMods, StatType.EnemyMaxHealth, tier.Value);
                contactDamage.Value = StatResolver.ResolveEnemy(contactDamage.BaseValue, ref blob, typeMods, entityMods, StatType.EnemyContactDamage, tier.Value);

                health.Current = health.Max;

                Ecb.RemoveComponent<Uninitialized>(chunkIndex, entity);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Uninitialized))]
        public partial struct UpdateStatsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyTypeBlobRef> BlobLookup;
            [ReadOnly] public BufferLookup<StatModifier> TypeModLookup;

            private void Execute(
                ref EnemySpeed speed,
                ref EnemyHealth health,
                ref ContactDamage contactDamage,
                EnabledRefRW<EnemyStatUpdateRequired> updateRequired,
                in EnemyTierComponent tier,
                in EnemyTypeRef typeRef,
                in DynamicBuffer<StatModifier> entityMods)
            {
                ref BlobArray<TieredStatModifier> blob = ref BlobLookup[typeRef.Value].Value.Value.ConfigModifiers;
                DynamicBuffer<StatModifier> typeMods = TypeModLookup[typeRef.Value];

                speed.Value = StatResolver.ResolveEnemy(speed.BaseValue, ref blob, typeMods, entityMods, StatType.EnemySpeed, tier.Value);
                health.Max = StatResolver.ResolveEnemy(health.BaseMax, ref blob, typeMods, entityMods, StatType.EnemyMaxHealth, tier.Value);
                contactDamage.Value = StatResolver.ResolveEnemy(contactDamage.BaseValue, ref blob, typeMods, entityMods, StatType.EnemyContactDamage, tier.Value);

                updateRequired.ValueRW = false;
            }
        }
    }
}