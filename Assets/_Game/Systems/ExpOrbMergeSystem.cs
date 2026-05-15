using _Game.Components;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExpOrbGridSystem))]
    public partial struct ExpOrbMergeSystem : ISystem
    {
        private static readonly int2[] NeighborOffsets =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1, 0), new(0, 0), new(1, 0),
            new(-1, 1), new(0, 1), new(1, 1),
        };
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbTag>();
            state.RequireForUpdate<ExpOrbConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!ExpOrbGridShared.IsValid) return;

            ExpOrbConfig config = SystemAPI.GetSingleton<ExpOrbConfig>();
            EntityManager entityManager = state.EntityManager;

            int count = ExpOrbGridShared.Count;
            NativeArray<Entity> entities = ExpOrbGridShared.Entities;
            NativeArray<float2> positions = ExpOrbGridShared.Positions;
            NativeArray<ExpOrbData> dataArray = ExpOrbGridShared.OrbData;
            NativeParallelMultiHashMap<int2, int> cellMap = ExpOrbGridShared.CellMap;
            
            float cellSize = config.MergeRadius;
            float mergeRadiusSq = config.MergeRadius * config.MergeRadius;

            // Pair idle orbs bidirectionally using the spatial grid - O(n·k)
            EntityCommandBuffer pairingEcb = new(state.WorldUpdateAllocator);
            NativeHashSet<Entity> paired = new(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
            {
                Entity entityI = entities[i];
                
                if (paired.Contains(entityI)) continue;
                if (dataArray[i].State != ExpOrbState.Idle) continue;

                float2 posI = positions[i];
                int2 cellI = new((int)math.floor(posI.x / cellSize), (int)math.floor(posI.y / cellSize));

                int nearestOrbIndex = -1;
                float shortestDistSq = mergeRadiusSq;

                foreach (int2 offset in NeighborOffsets)
                {
                    int2 neighbor = cellI + offset;
                    if (!cellMap.TryGetFirstValue(neighbor, out int j, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                    do
                    {
                        if (j == i || paired.Contains(entities[j])) continue;

                        float distSq = math.distancesq(posI, positions[j]);
                        if (distSq >= shortestDistSq) continue;

                        shortestDistSq = distSq;
                        nearestOrbIndex = j;
                    }
                    while (cellMap.TryGetNextValue(out j, ref it));
                }

                if (nearestOrbIndex == -1) continue;

                paired.Add(entityI);
                paired.Add(entities[nearestOrbIndex]);

                ExpOrbData updatedI = dataArray[i];
                updatedI.State = ExpOrbState.Merging;
                updatedI.MergeTarget = entities[nearestOrbIndex];
                pairingEcb.SetComponent(entityI, updatedI);

                ExpOrbData updatedJ = dataArray[nearestOrbIndex];
                updatedJ.State = ExpOrbState.Merging;
                updatedJ.MergeTarget = entityI;
                pairingEcb.SetComponent(entities[nearestOrbIndex], updatedJ);
            }

            paired.Dispose();
            pairingEcb.Playback(entityManager);

            // Build target lookup for move job from shared data (all non-saturated orbs)
            NativeHashMap<Entity, float2> targetPositions = new(count, Allocator.TempJob);
            for (int i = 0; i < count; i++)
            {
                targetPositions.TryAdd(entities[i], positions[i]);
            }
            
            new MoveOrbsJob
            {
                TargetPositions = targetPositions,
                MergeSpeed = config.MergeSpeed,
                MergeRadius = config.MergeRadius,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
            
            // Sync required: main thread reads components in the next step
            state.Dependency.Complete();
            targetPositions.Dispose();
            
            // Merge exp orbs
            EntityCommandBuffer mergeEcb = new(state.WorldUpdateAllocator);

            foreach ((RefRW<ExpOrbData> dataRW, RefRW<LocalTransform> transform, Entity entity) in SystemAPI.Query<RefRW<ExpOrbData>, RefRW<LocalTransform>>().WithAll<ExpOrbTag>().WithNone<SaturatedTag>().WithEntityAccess())
            {
                if (dataRW.ValueRO.State != ExpOrbState.Merging) continue;

                Entity target = dataRW.ValueRO.MergeTarget;

                if (!entityManager.Exists(target) || !entityManager.HasComponent<ExpOrbData>(target))
                {
                    dataRW.ValueRW.State = ExpOrbState.Idle;
                    dataRW.ValueRW.MergeTarget = Entity.Null;
                    continue;
                }

                ExpOrbData targetData = entityManager.GetComponentData<ExpOrbData>(target);
                if (targetData.State == ExpOrbState.Saturated)
                {
                    dataRW.ValueRW.State = ExpOrbState.Idle;
                    dataRW.ValueRW.MergeTarget = Entity.Null;
                    continue;
                }

                float2 targetPos = entityManager.GetComponentData<LocalTransform>(target).Position.xy;
                float2 pos = transform.ValueRO.Position.xy;

                if (math.distancesq(pos, targetPos) >= 0.05f * 0.05f) continue;

                // Arrived - Merge exp orbs, reset merge target and set new data
                float newValue = targetData.Value + dataRW.ValueRO.Value;
                float newScale = ValueToScale(newValue, config);
                bool saturated = newValue >= targetData.MaxValue;

                if (saturated)
                {
                    mergeEcb.AddComponent<SaturatedTag>(target);
                }
                
                targetData.Value = newValue;
                targetData.VisualScale = newScale;
                targetData.State = saturated ? ExpOrbState.Saturated : ExpOrbState.Idle;
                targetData.MergeTarget = Entity.Null;
                entityManager.SetComponentData(target, targetData);

                LocalTransform targetTransform = entityManager.GetComponentData<LocalTransform>(target);
                targetTransform.Scale = newScale;
                entityManager.SetComponentData(target, targetTransform);

                mergeEcb.DestroyEntity(entity);
            }

            mergeEcb.Playback(entityManager);
        }

        private static float ValueToScale(float value, in ExpOrbConfig config)
        {
            float t = math.saturate(value / config.MaxOrbValue);
            return math.lerp(config.BaseSize, config.MaxSize, t);
        }
    }

    [BurstCompile]
    internal partial struct MoveOrbsJob : IJobEntity
    {
        [ReadOnly] public NativeHashMap<Entity, float2> TargetPositions;
        [ReadOnly] public float MergeSpeed;
        [ReadOnly] public float MergeRadius;
        [ReadOnly] public float DeltaTime;

        private void Execute(ref ExpOrbData data, ref LocalTransform transform)
        {
            if (data.State != ExpOrbState.Merging) return;
            if (!TargetPositions.TryGetValue(data.MergeTarget, out float2 targetPos)) return;

            float2 pos = transform.Position.xy;
            float2 dir = targetPos - pos;
            float dist = math.length(dir);

            if (dist < 0.05f) return; // arrival handled on main thread

            float t = 1f - math.saturate(dist / MergeRadius);
            float speed = math.lerp(MergeSpeed, MergeSpeed * 2.5f, t);
            transform.Position += new float3(math.normalize(dir) * speed * DeltaTime, 0f);
        }
    }
}