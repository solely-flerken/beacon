using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace _Game.Systems
{
    /// <summary>
    /// Everything with Despawn enabled is removed at the end of the frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct DespawnSystem : ISystem
    {
        private EntityQuery _despawnQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _despawnQuery = SystemAPI.QueryBuilder().WithAll<Despawn>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            NativeList<Entity> entities = _despawnQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out JobHandle gatherHandle);

            state.Dependency = new DespawnJob
            {
                Entities = entities,
                ECB = ecb,
            }.Schedule(gatherHandle);
        }

        [BurstCompile]
        private struct DespawnJob : IJob
        {
            [ReadOnly] public NativeList<Entity> Entities;
            public EntityCommandBuffer ECB;

            public void Execute()
            {
                if (Entities.Length == 0) return;

                ECB.DestroyEntity(Entities.AsArray());
            }
        }
    }
}
