using _Game.Authoring;
using _Game.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Gameplay
{
    public static class EnemySpawner
    {
        public static void SpawnWave(EntityManager em, int count)
        {
            using EntityQuery prefabQuery = em.CreateEntityQuery(typeof(PrefabRefs));
            if (prefabQuery.IsEmpty)
            {
                UnityEngine.Debug.LogError("[EnemySpawner] PrefabRefs not found. Add SpawnerAuthoring to your SubScene.");
                return;
            }

            PrefabRefs prefabs = prefabQuery.GetSingleton<PrefabRefs>();
            NativeArray<Entity> entities = new(count, Allocator.TempJob);
            em.Instantiate(prefabs.Enemy, entities);

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
            }

            entities.Dispose();
            positions.Dispose();

            UnityEngine.Debug.Log($"[EnemySpawner] Spawned {count} enemies.");
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