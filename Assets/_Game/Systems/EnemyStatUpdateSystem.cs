using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct EnemyStatUpdateSystem : ISystem
    {
        private ComponentLookup<EnemyTypeBlobRef> _blobLookup;
        private uint _spawnSeed;

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

            _spawnSeed++;

            new SpawnInitJob
            {
                GlobalMods = globalMods,
                WaveMods = waveMods,
                BlobLookup = _blobLookup,
                Ecb = ecb,
                Seed = _spawnSeed,
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
            public uint Seed;

            private void Execute(
                Entity entity,
                [ChunkIndexInQuery] int chunkIndex,
                ref Speed speed,
                ref Health health,
                ref ContactDamage contactDamage,
                ref EnemyReward reward,
                in EnemySpeedVariation speedVariation,
                in Classification mask,
                in EnemyTypeRef typeRef,
                in DynamicBuffer<StatModifier> entityMods)
            {
                ref BlobArray<TieredStatModifier> blob = ref BlobLookup[typeRef.Value].Value.Value.ConfigModifiers;
                EnemyTier tierEnum = TierMaskMap.Get(mask.Tier);

                // Roll the per-enemy speed once and bake it into BaseValue, so modifiers include speed variations
                Random rng = Random.CreateFromIndex((uint)entity.Index ^ (Seed * 0x9E3779B9u));
                float variation = math.saturate(speedVariation.Value);
                speed.BaseValue *= 1f + rng.NextFloat(-variation, variation);

                speed.Value = StatResolver.ResolveEnemy(tierEnum, StatType.Speed, speed.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);
                health.Max = StatResolver.ResolveEnemy(tierEnum, StatType.MaxHp, health.BaseMax, mask, GlobalMods, WaveMods, entityMods, ref blob);
                contactDamage.Value = StatResolver.ResolveEnemy(tierEnum, StatType.ContactDamage, contactDamage.BaseValue, mask, GlobalMods, WaveMods, entityMods, ref blob);
                reward.ExpValue = StatResolver.ResolveEnemy(tierEnum, StatType.Exp, reward.BaseExpValue, mask, GlobalMods, WaveMods, entityMods, ref blob);

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
                ref Speed speed,
                ref Health health,
                ref ContactDamage contactDamage,
                ref EnemyReward reward,
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
                reward.ExpValue = StatResolver.ResolveEnemy(tierEnum, StatType.Exp, reward.BaseExpValue, mask, GlobalMods, WaveMods, entityMods, ref blob);

                updateRequired.ValueRW = false;
            }
        }
    }
}
