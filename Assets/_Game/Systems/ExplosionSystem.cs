using _Game.Components;
using _Game.Config;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);

            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach ((RefRW<LocalTransform> transform, RefRW<ExplosionState> explosionState, RefRW<URPMaterialPropertyBaseColor> color, Entity entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<ExplosionState>, RefRW<URPMaterialPropertyBaseColor>>().WithAll<ExplosionTag>().WithEntityAccess())
            {
                explosionState.ValueRW.Elapsed += deltaTime;
                float t = explosionState.ValueRO.Duration > 0f ? explosionState.ValueRO.Elapsed / explosionState.ValueRO.Duration : 1f;

                float fade = math.max(0f, 1f - t);
                transform.ValueRW.Scale = explosionState.ValueRO.StartScale * fade;
                color.ValueRW.Value.w = fade;

                if (explosionState.ValueRO.Elapsed >= explosionState.ValueRO.Duration)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}