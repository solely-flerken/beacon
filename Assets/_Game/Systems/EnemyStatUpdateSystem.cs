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
        private ComponentLookup<EnemyTypeBlobRef> _blobLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blobLookup = state.GetComponentLookup<EnemyTypeBlobRef>(isReadOnly: true);

            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GlobalModSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _blobLookup.Update(ref state);

            DynamicBuffer<GlobalStatModifier> globalMods = SystemAPI.GetSingletonBuffer<GlobalStatModifier>(isReadOnly: true);
            DynamicBuffer<WaveStatModifier> waveMods = SystemAPI.GetSingletonBuffer<WaveStatModifier>(isReadOnly: true);

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new SpawnInitJob
            {
                GlobalMods = globalMods,
                WaveMods = waveMods,
                BlobLookup = _blobLookup,
                Ecb = ecb
            }.ScheduleParallel();

            new UpdateStatsJob
            {
                GlobalMods = globalMods,
                WaveMods = waveMods,
                BlobLookup = _blobLookup,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Uninitialized))]
        public partial struct SpawnInitJob : IJobEntity
        {
            [ReadOnly] public DynamicBuffer<GlobalStatModifier> GlobalMods;
            [ReadOnly] public DynamicBuffer<WaveStatModifier> WaveMods;
            [ReadOnly] public ComponentLookup<EnemyTypeBlobRef> BlobLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute(
                Entity entity,
                [ChunkIndexInQuery] int chunkIndex,
                ref EnemySpeed speed,
                ref Health health,
                ref ContactDamage contactDamage,
                in Classification mask,
                in EnemyTypeRef typeRef,
                in DynamicBuffer<StatModifier> entityMods)
            {
                ref BlobArray<TieredStatModifier> blob = ref BlobLookup[typeRef.Value].Value.Value.ConfigModifiers;
                EnemyTier tierEnum = TierMaskMap.Get(mask.Tier);

                speed.Value = StatResolver.ResolveEnemy(tierEnum, StatType.Speed, speed.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);
                health.Max = StatResolver.ResolveEnemy(tierEnum, StatType.MaxHp, health.BaseMax, mask, GlobalMods, WaveMods, entityMods, ref blob);
                contactDamage.Value = StatResolver.ResolveEnemy(tierEnum, StatType.ContactDamage, contactDamage.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);

                health.Current = health.Max;
                
                Ecb.RemoveComponent<Uninitialized>(chunkIndex, entity);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Uninitialized))]
        public partial struct UpdateStatsJob : IJobEntity
        {
            [ReadOnly] public DynamicBuffer<GlobalStatModifier> GlobalMods;
            [ReadOnly] public DynamicBuffer<WaveStatModifier> WaveMods;
            [ReadOnly] public ComponentLookup<EnemyTypeBlobRef> BlobLookup;

            private void Execute(
                ref EnemySpeed speed,
                ref Health health,
                ref ContactDamage contactDamage,
                EnabledRefRW<EnemyStatUpdateRequired> updateRequired,
                in Classification mask,
                in EnemyTypeRef typeRef,
                in DynamicBuffer<StatModifier> entityMods)
            {
                ref BlobArray<TieredStatModifier> blob = ref BlobLookup[typeRef.Value].Value.Value.ConfigModifiers;
                EnemyTier tierEnum = TierMaskMap.Get(mask.Tier);

                speed.Value = StatResolver.ResolveEnemy(tierEnum, StatType.Speed, speed.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);
                health.Max = StatResolver.ResolveEnemy(tierEnum, StatType.MaxHp, health.BaseMax, mask, GlobalMods, WaveMods, entityMods, ref blob);
                contactDamage.Value = StatResolver.ResolveEnemy(tierEnum, StatType.ContactDamage, contactDamage.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);

                updateRequired.ValueRW = false;
            }
        }
    }
}
