using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    [UpdateBefore(typeof(EnemyDeathSystem))]
    public partial struct ExpOrbSpawnSystem : ISystem
    {
        private NativeQueue<DeathEvent> _expQueue;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbConfig>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _expQueue = new NativeQueue<DeathEvent>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new ExpOrbEvents { Queue = _expQueue });
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            if (_expQueue.IsCreated) _expQueue.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<ExpOrbEvents>();
            state.EntityManager.CompleteDependencyBeforeRW<ExpOrbEvents>();

            if (_expQueue.Count == 0) return;
            if (!SystemAPI.TryGetSingleton(out PrefabRefs prefabs)) return;

            ExpOrbConfig config = SystemAPI.GetSingleton<ExpOrbConfig>();

            // Snapshot all live orbs that are still collecting exp.
            NativeList<Entity> existingEntities = new(config.MaxBuckets, Allocator.Temp);
            NativeList<float2> existingPositions = new(config.MaxBuckets, Allocator.Temp);
            NativeList<ExpOrbData> existingData = new(config.MaxBuckets, Allocator.Temp);
            NativeList<bool> existingModified = new(config.MaxBuckets, Allocator.Temp);

            foreach ((RefRO<ExpOrbData> data, Entity entity) in SystemAPI.Query<RefRO<ExpOrbData>>().WithAll<ExpOrbTag>().WithNone<SaturatedTag>().WithEntityAccess())
            {
                existingEntities.Add(entity);
                existingPositions.Add(data.ValueRO.Position);
                existingData.Add(data.ValueRO);
                existingModified.Add(false);
            }

            // Orbs queued for spawning this frame. Tracked separately so that multiple deaths
            // at the same location in one frame collapse into a single orb rather than many.
            NativeList<float2> spawnPositions = new(config.MaxBuckets, Allocator.Temp);
            NativeList<ExpOrbData> spawnData = new(config.MaxBuckets, Allocator.Temp);

            float mergeRadiusSq = config.MergeRadius * config.MergeRadius;

            while (_expQueue.TryDequeue(out DeathEvent death))
            {
                float2 deathPos = death.Position.xy;

                // Find the nearest orb across both live entities and this frame's pending spawns.
                int bestExistingIdx = -1;
                int bestSpawnIdx = -1;
                float bestDistSq = float.MaxValue;

                for (int i = 0; i < existingEntities.Length; i++)
                {
                    float distanceSq = math.distancesq(deathPos, existingPositions[i]);
                    if (distanceSq > bestDistSq) continue;
                    
                    bestDistSq = distanceSq;
                    bestExistingIdx = i;
                    bestSpawnIdx = -1;
                }

                for (int i = 0; i < spawnPositions.Length; i++)
                {
                    float distanceSq = math.distancesq(deathPos, spawnPositions[i]);
                    if (distanceSq > bestDistSq) continue;
                    
                    bestDistSq = distanceSq;
                    bestSpawnIdx = i;
                    bestExistingIdx = -1;
                }

                bool withinRadius = (bestExistingIdx >= 0 || bestSpawnIdx >= 0) && bestDistSq <= mergeRadiusSq;
                bool canSpawn = existingEntities.Length + spawnPositions.Length < config.MaxBuckets;

                if (!withinRadius && canSpawn)
                {
                    // No nearby orb and a slot is free — spawn a new orb at this death location.
                    Vector2 rnd = Random.insideUnitCircle * 0.3f;
                    float2 spawnPos = deathPos + new float2(rnd.x, rnd.y);

                    spawnPositions.Add(spawnPos);
                    spawnData.Add(new ExpOrbData
                    {
                        Value = math.max(death.Exp, 0f),
                        MaxValue = config.MaxOrbValue,
                        Position = spawnPos,
                    });
                }
                else if (bestExistingIdx >= 0)
                {
                    // Nearby orb exists (or bucket is full) — pile exp into the nearest live orb.
                    Accumulate(ref existingData.ElementAt(bestExistingIdx), death.Exp);
                    existingModified[bestExistingIdx] = true;
                }
                else if (bestSpawnIdx >= 0)
                {
                    // Nearest orb is a pending spawn this frame — accumulate before it's created.
                    Accumulate(ref spawnData.ElementAt(bestSpawnIdx), death.Exp);
                }
            }

            // Using EndSim ECB to defer structural changes to the end of each frame.
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Push value/scale/saturation updates for orbs that received exp this frame.
            for (int i = 0; i < existingEntities.Length; i++)
            {
                if (!existingModified[i]) continue;

                ExpOrbData data = existingData[i];
                ecb.SetComponent(existingEntities[i], data);
                ecb.SetComponent(existingEntities[i], new LocalTransform
                {
                    Position = new float3(existingPositions[i], 0f),
                    Rotation = quaternion.identity,
                    Scale = ValueToScale(data.Value, config),
                });

                if (data.Value >= data.MaxValue)
                {
                    ecb.AddComponent<SaturatedTag>(existingEntities[i]);
                }
            }

            // Instantiate new orb entities with their final accumulated values.
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                ExpOrbData data = spawnData[i];
                Entity orb = ecb.Instantiate(prefabs.ExpOrb);
                LocalTransform spawnTransform = LocalTransform.FromPositionRotationScale(new float3(spawnPositions[i], 0f), quaternion.identity, ValueToScale(data.Value, config));

                ecb.SetComponent(orb, spawnTransform);
                ecb.SetComponent(orb, new LocalToWorld { Value = spawnTransform.ToMatrix() });
                ecb.SetComponent(orb, data);

                if (data.Value >= data.MaxValue)
                {
                    ecb.AddComponent<SaturatedTag>(orb);
                }
            }

            existingEntities.Dispose();
            existingPositions.Dispose();
            existingData.Dispose();
            existingModified.Dispose();
            spawnPositions.Dispose();
            spawnData.Dispose();
        }

        private static void Accumulate(ref ExpOrbData data, float exp)
        {
            data.Value += exp;
        }

        private static float ValueToScale(float value, in ExpOrbConfig config)
        {
            float t = math.saturate(value / config.MaxOrbValue);
            return math.lerp(config.BaseSize, config.MaxSize, t);
        }
    }
}
