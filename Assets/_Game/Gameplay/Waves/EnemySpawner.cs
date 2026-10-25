using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace _Game.Gameplay.Waves
{
    public static class EnemySpawner
    {
        // Ring radius around the player at which enemies appear.
        private const float SpawnRingRadius = 20f;

        /// <summary>
        /// Spawns a group on an arc of the spawn ring around center.
        /// </summary>
        public static void Spawn(EntityManager em, SpawnGroup group, Entity catalogEntity, float2 center, float2 halfExtents, float arcCenter, float arcWidthRad)
        {
            DynamicBuffer<EnemyTypeCatalogEntry> catalog = em.GetBuffer<EnemyTypeCatalogEntry>(catalogEntity);

            Entity prefab = Entity.Null;
            Entity typeEntity = Entity.Null;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].TypeId != group.Type) continue;
                prefab = catalog[i].Prefab;
                typeEntity = catalog[i].TypeEntity;
                break;
            }

            if (prefab == Entity.Null)
            {
                Debug.LogError($"[EnemySpawner] No catalog entry found for EnemyType.{group.Type}.");
                return;
            }

            int count = group.Count;
            NativeArray<Entity> entities = new(count, Allocator.TempJob);
            em.Instantiate(prefab, entities);

            NativeArray<float2> positions = new(count, Allocator.TempJob);
            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

            new RandomSpawnPositionsJob
            {
                Positions = positions,
                Center = center,
                SpawnRadius = SpawnRingRadius,
                ArcCenter = arcCenter,
                ArcWidth = arcWidthRad,
                HalfExtents = halfExtents,
                Seed = seed,
            }.Schedule(count, 64).Complete();

            TierMask tierMask = TierMaskMap.Get(group.Tier);
            for (int i = 0; i < count; i++)
            {
                em.SetComponentData(entities[i], new EnemyTypeRef { Value = typeEntity });
                
                LocalTransform transform = em.GetComponentData<LocalTransform>(entities[i]);
                transform.Position = new float3(positions[i].x, positions[i].y, 0f);
                em.SetComponentData(entities[i], transform);
                
                Classification classification = em.GetComponentData<Classification>(entities[i]);
                classification.Tier = tierMask;
                em.SetComponentData(entities[i], classification);
            }

            entities.Dispose();
            positions.Dispose();
        }
    }

    [BurstCompile]
    internal struct RandomSpawnPositionsJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float2> Positions;
        [ReadOnly] public float2 Center;
        [ReadOnly] public float SpawnRadius;
        [ReadOnly] public float ArcCenter;
        [ReadOnly] public float ArcWidth;
        [ReadOnly] public float2 HalfExtents;
        [ReadOnly] public uint Seed;

        public void Execute(int i)
        {
            uint hashedSeed = Seed * 2654435761u ^ (uint)(i + 1) * 2246822519u;
            if (hashedSeed == 0) hashedSeed = 1;
            Random rng = new(hashedSeed);
            float angle = ArcCenter + rng.NextFloat(-0.5f, 0.5f) * ArcWidth;
            float2 pos = Center + new float2(math.cos(angle), math.sin(angle)) * SpawnRadius;
            Positions[i] = math.clamp(pos, -HalfExtents, HalfExtents);
        }
    }
}
