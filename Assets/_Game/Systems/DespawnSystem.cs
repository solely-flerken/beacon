using _Game.Components;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    /// <summary>
    /// Everything with Despawn enabled is removed at the end of the frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct DespawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new DespawnJob
            {
                ECB = ecb.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Despawn))]
        internal partial struct DespawnJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute(Entity entity, [EntityIndexInQuery] int sortKey)
            {
                ECB.DestroyEntity(sortKey, entity);
            }
        }
    }
}
