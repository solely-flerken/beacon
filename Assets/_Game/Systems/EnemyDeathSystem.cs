using _Game.Components;
using _Game.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageAndDeathSystem))]
    public partial struct EnemyDeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            BeginSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            bool hasDeathQueue = DotsBridge.DeathQueue.IsCreated;
            bool hasExpQueue = DotsBridge.ExpOrbQueue.IsCreated;

            NativeQueue<DeathEvent>.ParallelWriter deathQueueWriter = default;
            NativeQueue<DeathEvent>.ParallelWriter expQueueWriter = default;

            if (hasDeathQueue) deathQueueWriter = DotsBridge.DeathQueue.AsParallelWriter();
            if (hasExpQueue) expQueueWriter = DotsBridge.ExpOrbQueue.AsParallelWriter();

            state.Dependency = new EnemyDeathJob
            {
                ECB = ecb,
                HasDeathQueue = hasDeathQueue,
                HasExpQueue = hasExpQueue,
                DeathQueue = deathQueueWriter,
                ExpOrbQueue = expQueueWriter,
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag), typeof(IsDead))]
        internal partial struct EnemyDeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            public bool HasDeathQueue;
            public bool HasExpQueue;

            [WriteOnly] public NativeQueue<DeathEvent>.ParallelWriter DeathQueue;
            [WriteOnly] public NativeQueue<DeathEvent>.ParallelWriter ExpOrbQueue;

            private void Execute(
                Entity entity,
                [EntityIndexInQuery] int sortKey,
                in EnemyReward reward,
                in LocalTransform transform)
            {
                DeathEvent deathEvent = new()
                {
                    Position = transform.Position,
                    Exp = reward.ExpValue
                };

                if (HasDeathQueue) DeathQueue.Enqueue(deathEvent);
                if (HasExpQueue) ExpOrbQueue.Enqueue(deathEvent);

                ECB.DestroyEntity(sortKey, entity);
            }
        }
    }
}
