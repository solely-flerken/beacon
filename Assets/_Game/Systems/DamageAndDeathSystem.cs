using _Game.Components;
using _Game.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    [UpdateAfter(typeof(BeaconArrivalSystem))]
    public partial struct DamageAndDeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            BeginSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new ProcessDamageAndDeathJob
            {
                ECB = ecb,
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        [BurstCompile]
        [WithChangeFilter(typeof(DamageBufferElement))]
        internal partial struct ProcessDamageAndDeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute(
                Entity entity,
                [EntityIndexInQuery] int sortKey,
                ref Health health,
                ref DynamicBuffer<DamageBufferElement> damageBuffer)
            {
                if (damageBuffer.IsEmpty) return;

                foreach (DamageBufferElement damage in damageBuffer)
                    health.Current -= damage.Value;

                damageBuffer.Clear();

                if (health.Current > 0f) return;

                health.IsAlive = false;
                ECB.SetComponentEnabled<IsDead>(sortKey, entity, true);
            }
        }
    }
}
