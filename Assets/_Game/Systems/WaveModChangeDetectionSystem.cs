using System;
using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Systems
{
    /// <summary>
    /// Marks stat owners dirty when the global or wave modifier buffer changes, but only the ones a changed modifier can affect.
    /// Entries added or removed in the buffers contribute their filter, and an entity is marked if its tags match any of those filters.
    /// Entities spawned later resolve their stats on their own.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TimedModifierCleanupSystem))]
    [UpdateBefore(typeof(BeaconStatUpdateSystem))]
    [UpdateBefore(typeof(EnemyStatUpdateSystem))]
    [UpdateBefore(typeof(WeaponStatUpdateSystem))]
    [BurstCompile]
    public partial struct WaveModChangeDetectionSystem : ISystem
    {
        private EntityQuery _waveModQuery;
        private EntityQuery _globalModQuery;

        private NativeList<WaveStatModifier> _lastWaveMods;
        private NativeList<GlobalStatModifier> _lastGlobalMods;

        public void OnCreate(ref SystemState state)
        {
            // Wave modifiers
            _waveModQuery = SystemAPI.QueryBuilder().WithAll<WaveStatModifier>().Build();
            _waveModQuery.SetChangedVersionFilter(ComponentType.ReadOnly<WaveStatModifier>());

            // Global modifiers
            _globalModQuery = SystemAPI.QueryBuilder().WithAll<GlobalStatModifier>().Build();
            _globalModQuery.SetChangedVersionFilter(ComponentType.ReadOnly<GlobalStatModifier>());

            _lastWaveMods = new NativeList<WaveStatModifier>(16, Allocator.Persistent);
            _lastGlobalMods = new NativeList<GlobalStatModifier>(16, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            _lastWaveMods.Dispose();
            _lastGlobalMods.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool waveChanged = !_waveModQuery.IsEmpty;
            bool globalChanged = !_globalModQuery.IsEmpty;
            if (!waveChanged && !globalChanged) return;

            NativeList<TagFilter> changedFilters = new(8, state.WorldUpdateAllocator);

            if (waveChanged)
            {
                CollectChangedFilters(SystemAPI.GetSingletonBuffer<WaveStatModifier>(isReadOnly: true), _lastWaveMods, changedFilters);
            }

            if (globalChanged)
            {
                CollectChangedFilters(SystemAPI.GetSingletonBuffer<GlobalStatModifier>(isReadOnly: true), _lastGlobalMods, changedFilters);
            }

            if (changedFilters.IsEmpty) return;

            NativeArray<TagFilter> filters = changedFilters.AsArray();

            state.Dependency = new MarkBeaconDirtyJob { ChangedFilters = filters }.ScheduleParallel(state.Dependency);
            state.Dependency = new MarkEnemiesDirtyJob { ChangedFilters = filters }.ScheduleParallel(state.Dependency);
            state.Dependency = new MarkWeaponsDirtyJob { ChangedFilters = filters }.ScheduleParallel(state.Dependency);
        }

        /// <summary>
        /// Adds the filters of all entries that differ between <paramref name="current"/> and <paramref name="last"/>
        /// </summary>
        private static void CollectChangedFilters<T>(DynamicBuffer<T> current, NativeList<T> last, NativeList<TagFilter> changed) where T : unmanaged, IBufferElementData, IFilteredModifier, IEquatable<T>
        {
            NativeArray<bool> matched = new(last.Length, Allocator.Temp);

            foreach (T modifier in current)
            {
                bool found = false;
                for (int j = 0; j < last.Length; j++)
                {
                    if (matched[j] || !last[j].Equals(modifier)) continue;

                    matched[j] = true;
                    found = true;
                    break;
                }

                if (found) continue;
                
                changed.Add(modifier.TagFilter);
            }

            // Removed modifiers, also need to trigger stat updates.
            for (int j = 0; j < last.Length; j++)
            {
                if (matched[j]) continue;
                
                changed.Add(last[j].TagFilter);
            }

            last.CopyFrom(current.AsNativeArray());
        }

        private static bool AnyMatches(NativeArray<TagFilter> filters, Tags tags)
        {
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].Matches(tags)) return true;
            }

            return false;
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct MarkBeaconDirtyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<TagFilter> ChangedFilters;

            private void Execute(EnabledRefRW<BeaconStatUpdateRequired> updateRequired, in Classification classification, in UnitFaction faction)
            {
                if (AnyMatches(ChangedFilters, classification.ForUnit(faction.Value)))
                {
                    updateRequired.ValueRW = true;
                }
            }
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct MarkEnemiesDirtyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<TagFilter> ChangedFilters;

            private void Execute(EnabledRefRW<EnemyStatUpdateRequired> updateRequired, in Classification classification, in UnitFaction faction)
            {
                if (AnyMatches(ChangedFilters, classification.ForUnit(faction.Value)))
                {
                    updateRequired.ValueRW = true;
                }
            }
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct MarkWeaponsDirtyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<TagFilter> ChangedFilters;

            private void Execute(EnabledRefRW<WeaponStatUpdateRequired> updateRequired, in Classification classification)
            {
                if (AnyMatches(ChangedFilters, classification.Tags))
                {
                    updateRequired.ValueRW = true;
                }
            }
        }
    }
}
