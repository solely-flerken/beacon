using _Game.Components;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [BurstCompile]
    public partial struct LifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new LifetimeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = ecb,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct LifetimeJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref Lifetime lifetime)
        {
            lifetime.Remaining -= DeltaTime;

            if (lifetime.Remaining > 0) return;

            ECB.DestroyEntity(sortKey, entity);
        }
    }
}
