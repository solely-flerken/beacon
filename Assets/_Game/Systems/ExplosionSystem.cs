using _Game.Components;
using _Game.Config;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MortarSystem))]
    public partial struct ExplosionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExplosionTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);

            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach ((RefRW<LocalTransform> transform, RefRW<ExplosionState> explosionState, Entity entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<ExplosionState>>().WithAll<ExplosionTag>().WithEntityAccess())
            {
                explosionState.ValueRW.Elapsed += deltaTime;
                float t = explosionState.ValueRO.Duration > 0f ? explosionState.ValueRO.Elapsed / explosionState.ValueRO.Duration : 1f;

                float scale = explosionState.ValueRO.StartScale * math.max(0f, 1f - t);
                transform.ValueRW.Scale = scale;

                SpriteRenderer spriteRenderer = state.EntityManager.GetComponentObject<SpriteRenderer>(entity);
                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = math.max(0f, 1f - t);
                    spriteRenderer.color = color;
                }

                if (explosionState.ValueRO.Elapsed >= explosionState.ValueRO.Duration)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}