using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace _Game.Systems
{
    /// <summary>
    /// Drains the EnemySpawnRequest queue, capped per frame so large pulses spread over several frames.
    /// Runs first in the simulation group so new enemies are initialized and positioned before anything reads them.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct EnemySpawnSystem : ISystem
    {
        private const float SpawnRingRadius = 20f;
        private const int MaxSpawnsPerFrame = 1000;

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<EnemyTypeRef> _typeRefLookup;
        private ComponentLookup<Classification> _classificationLookup;
        private EntityQuery _beaconQuery;
        private Random _rng;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>();
            _typeRefLookup = state.GetComponentLookup<EnemyTypeRef>();
            _classificationLookup = state.GetComponentLookup<Classification>();
            _beaconQuery = SystemAPI.QueryBuilder().WithAll<PlayerTag, LocalTransform>().Build();
            _rng = new Random(0x6E624EB7u);

            state.EntityManager.CreateSingletonBuffer<EnemySpawnRequest>();

            state.RequireForUpdate<EnemyTypeCatalogEntry>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Persists across frames on the singleton entity; requests that don't fit this frame's budget wait here
            DynamicBuffer<EnemySpawnRequest> spawnQueue = SystemAPI.GetSingletonBuffer<EnemySpawnRequest>();
            if (spawnQueue.IsEmpty) return;

            NativeList<EnemySpawnRequest> batch = new(spawnQueue.Length, state.WorldUpdateAllocator);
            int budget = MaxSpawnsPerFrame;
            int consumed = 0;
            while (consumed < spawnQueue.Length && budget > 0)
            {
                EnemySpawnRequest request = spawnQueue[consumed];
                int take = math.min(request.Count, budget);
                budget -= take;

                if (take < request.Count)
                {
                    EnemySpawnRequest rest = request;
                    rest.Count -= take;
                    spawnQueue[consumed] = rest;
                    request.Count = take;
                }
                else
                {
                    consumed++;
                }

                if (take > 0)
                {
                    batch.Add(request);
                }
            }

            spawnQueue.RemoveRange(0, consumed);
            
            float2 center = float2.zero;
            if (!_beaconQuery.IsEmptyIgnoreFilter)
            {
                center = _beaconQuery.GetSingleton<LocalTransform>().Position.xy;
            }

            ArenaBounds bounds = SystemAPI.GetSingleton<ArenaBounds>();
            float2 halfExtents = new(bounds.Width * 0.5f - 1f, bounds.Height * 0.5f - 1f);

            DynamicBuffer<EnemyTypeCatalogEntry> catalog = SystemAPI.GetSingletonBuffer<EnemyTypeCatalogEntry>(isReadOnly: true);
            NativeArray<EnemyTypeCatalogEntry> requestCatalogEntries = CollectionHelper.CreateNativeArray<EnemyTypeCatalogEntry>(batch.Length, state.WorldUpdateAllocator);
            int total = 0;
            for (int b = 0; b < batch.Length; b++)
            {
                for (int i = 0; i < catalog.Length; i++)
                {
                    if (catalog[i].TypeId != batch[b].Type) continue;
                    requestCatalogEntries[b] = catalog[i];
                    break;
                }

                if (requestCatalogEntries[b].Prefab == Entity.Null)
                {
                    Debug.LogError($"[EnemySpawnSystem] No catalog entry found for enemy type {(int)batch[b].Type}.");
                    continue;
                }

                total += batch[b].Count;
            }

            NativeArray<Entity> spawnedEntities = CollectionHelper.CreateNativeArray<Entity>(total, state.WorldUpdateAllocator);
            int offset = 0;
            for (int b = 0; b < batch.Length; b++)
            {
                if (requestCatalogEntries[b].Prefab == Entity.Null) continue;
                state.EntityManager.Instantiate(requestCatalogEntries[b].Prefab, spawnedEntities.GetSubArray(offset, batch[b].Count));
                offset += batch[b].Count;
            }

            _transformLookup.Update(ref state);
            _typeRefLookup.Update(ref state);
            _classificationLookup.Update(ref state);

            offset = 0;
            for (int b = 0; b < batch.Length; b++)
            {
                if (requestCatalogEntries[b].Prefab == Entity.Null) continue;
                int count = batch[b].Count;

                state.Dependency = new InitSpawnedEnemiesJob
                {
                    Entities = spawnedEntities.GetSubArray(offset, count),
                    TransformLookup = _transformLookup,
                    TypeRefLookup = _typeRefLookup,
                    ClassificationLookup = _classificationLookup,
                    TypeEntity = requestCatalogEntries[b].TypeEntity,
                    Tier = batch[b].Tier,
                    Center = center,
                    ArcCenter = batch[b].ArcCenter,
                    ArcWidth = batch[b].ArcWidth,
                    HalfExtents = halfExtents,
                    Seed = _rng.NextUInt(1, uint.MaxValue),
                }.Schedule(count, 64, state.Dependency);

                offset += count;
            }
        }

        [BurstCompile]
        private struct InitSpawnedEnemiesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<EnemyTypeRef> TypeRefLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Classification> ClassificationLookup;
            [ReadOnly] public Entity TypeEntity;
            [ReadOnly] public EnemyTier Tier;
            [ReadOnly] public float2 Center;
            [ReadOnly] public float ArcCenter;
            [ReadOnly] public float ArcWidth;
            [ReadOnly] public float2 HalfExtents;
            [ReadOnly] public uint Seed;

            public void Execute(int i)
            {
                uint hashedSeed = Seed * 2654435761u ^ (uint)(i + 1) * 2246822519u;
                Random rng = new(hashedSeed);
                float angle = ArcCenter + rng.NextFloat(-0.5f, 0.5f) * ArcWidth;
                float2 pos = Center + new float2(math.cos(angle), math.sin(angle)) * SpawnRingRadius;
                pos = math.clamp(pos, -HalfExtents, HalfExtents);

                Entity entity = Entities[i];
                TypeRefLookup[entity] = new EnemyTypeRef { Value = TypeEntity };

                LocalTransform transform = TransformLookup[entity];
                transform.Position = new float3(pos.x, pos.y, 0f);
                TransformLookup[entity] = transform;

                Classification classification = ClassificationLookup[entity];
                classification.Tags = classification.Tags.WithTier(Tier);
                ClassificationLookup[entity] = classification;
            }
        }
    }
}