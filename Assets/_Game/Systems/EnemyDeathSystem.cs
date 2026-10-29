using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace _Game.Systems
{
    /// <summary>
    /// Sends death events for enemies killed this frame. DespawnSystem removes them.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageAndDeathSystem))]
    public partial struct EnemyDeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbEvents>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasDeathQueue = DotsBridge.DeathQueue.IsCreated;
            NativeQueue<DeathEvent>.ParallelWriter deathQueueWriter = default;
            if (hasDeathQueue) deathQueueWriter = DotsBridge.DeathQueue.AsParallelWriter();

            NativeQueue<DeathEvent> expOrbQueue = SystemAPI.GetSingletonRW<ExpOrbEvents>().ValueRO.Queue;

            state.Dependency = new EnemyDeathJob
            {
                HasDeathQueue = hasDeathQueue,
                DeathQueue = deathQueueWriter,
                ExpOrbQueue = expOrbQueue.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        internal partial struct EnemyDeathJob : IJobEntity
        {
            public bool HasDeathQueue;

            [WriteOnly] public NativeQueue<DeathEvent>.ParallelWriter DeathQueue;
            [WriteOnly] public NativeQueue<DeathEvent>.ParallelWriter ExpOrbQueue;

            private void Execute(in Despawn despawn, in EnemyReward reward, in LocalTransform transform)
            {
                if (!despawn.Killed) return;

                DeathEvent deathEvent = new()
                {
                    Position = transform.Position,
                    Exp = reward.ExpValue
                };

                if (HasDeathQueue) DeathQueue.Enqueue(deathEvent);
                ExpOrbQueue.Enqueue(deathEvent);
            }
        }
    }
}
