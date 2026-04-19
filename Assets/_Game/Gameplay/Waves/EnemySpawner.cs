using _Game.Components;
using _Game.Config;
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
        public static void SpawnWave(EntityManager em, SpawnGroup group)
        {
            using EntityQuery catalogQuery = em.CreateEntityQuery(typeof(EnemyTypeCatalogEntry));
            if (catalogQuery.IsEmpty)
            {
                Debug.LogError("[EnemySpawner] EnemyTypeCatalogEntry not found. Add EnemyTypeCatalogAuthoring to your SubScene.");
                return;
            }

            DynamicBuffer<EnemyTypeCatalogEntry> catalog = em.GetBuffer<EnemyTypeCatalogEntry>(catalogQuery.GetSingletonEntity());

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
                SpawnRadius = ArenaConfig.Width * 0.5f - 1f,
                Seed = seed,
            }.Schedule(count, 64).Complete();

            for (int i = 0; i < count; i++)
            {
                LocalTransform transform = em.GetComponentData<LocalTransform>(entities[i]);
                transform.Position = new float3(positions[i].x, positions[i].y, 0f);
                em.SetComponentData(entities[i], transform);
                em.SetComponentData(entities[i], new EnemyTypeRef { Value = typeEntity });
                em.SetComponentData(entities[i], new EnemyTierComponent { Value = group.Tier });
            }

            entities.Dispose();
            positions.Dispose();

            Debug.Log($"[EnemySpawner] Spawned {count} {group.Tier} {group.Type}.");
        }
    }

    [BurstCompile]
    internal struct RandomSpawnPositionsJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float2> Positions;
        [ReadOnly] public float SpawnRadius;
        [ReadOnly] public uint Seed;

        public void Execute(int i)
        {
            uint hashedSeed = Seed * 2654435761u ^ (uint)(i + 1) * 2246822519u;
            if (hashedSeed == 0) hashedSeed = 1;
            Random rng = new(hashedSeed);
            float angle = rng.NextFloat(0f, math.PI * 2f);
            Positions[i] = new float2(math.cos(angle), math.sin(angle)) * SpawnRadius;
        }
    }
}
