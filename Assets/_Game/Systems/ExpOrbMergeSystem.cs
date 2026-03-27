using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExpOrbSpawnSystem))]
    public partial struct ExpOrbMergeSystem : ISystem
    {
        private EntityQuery _expOrbQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbTag>();
            state.RequireForUpdate<ExpOrbConfig>();

            _expOrbQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<ExpOrbTag>(),
                ComponentType.ReadWrite<ExpOrbData>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.Exclude<SaturatedTag>());
        }

        public void OnUpdate(ref SystemState state)
        {
            ExpOrbConfig config = SystemAPI.GetSingleton<ExpOrbConfig>();
            EntityManager entityManager = state.EntityManager;

            using NativeArray<Entity> entities = _expOrbQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<LocalTransform> transforms = _expOrbQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<ExpOrbData> dataArray = _expOrbQuery.ToComponentDataArray<ExpOrbData>(Allocator.Temp);

            float mergeRadiusSq = config.MergeRadius * config.MergeRadius;

            // Pair idle orbs bidirectionally
            EntityCommandBuffer pairingEcb = new(state.WorldUpdateAllocator);
            NativeHashSet<Entity> paired = new(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (paired.Contains(entities[i])) continue;

                ExpOrbData expOrbData = dataArray[i];
                if (expOrbData.State != ExpOrbState.Idle) continue;

                float2 posI = transforms[i].Position.xy;
                
                int nearestOrbIndex = -1;
                float shortestDistSq = mergeRadiusSq;

                for (int j = 0; j < entities.Length; j++)
                {
                    if (i == j) continue;
                    if (paired.Contains(entities[j])) continue;
                    if (dataArray[j].State == ExpOrbState.Saturated) continue;

                    float distSq = math.distancesq(posI, transforms[j].Position.xy);
                    if (distSq >= shortestDistSq) continue;

                    shortestDistSq = distSq;
                    nearestOrbIndex = j;
                }

                if (nearestOrbIndex == -1) continue;

                paired.Add(entities[i]);
                paired.Add(entities[nearestOrbIndex]);

                ExpOrbData updatedI = expOrbData;
                updatedI.State = ExpOrbState.Merging;
                updatedI.MergeTarget = entities[nearestOrbIndex];
                pairingEcb.SetComponent(entities[i], updatedI);

                ExpOrbData updatedJ = dataArray[nearestOrbIndex];
                updatedJ.State = ExpOrbState.Merging;
                updatedJ.MergeTarget = entities[i];
                pairingEcb.SetComponent(entities[nearestOrbIndex], updatedJ);
            }

            paired.Dispose();
            pairingEcb.Playback(entityManager);

            // Move merging orbs
            NativeHashMap<Entity, float2> targetPositions = new(entities.Length, Allocator.TempJob);
            for (int i = 0; i < entities.Length; i++)
            {
                targetPositions.TryAdd(entities[i], transforms[i].Position.xy);
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