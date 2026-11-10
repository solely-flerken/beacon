using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace _Game.Systems
{
    /// <summary>
    /// Sends death events for enemies killed this frame. DespawnSystem removes them.
    /// Also records a death burst per killed enemy (capped per frame) for DeathBurstRenderer.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageAndDeathSystem))]
    public partial struct EnemyDeathSystem : ISystem
    {
        private uint _frame;

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

            JobHandle deathJob = new EnemyDeathJob
            {
                HasDeathQueue = hasDeathQueue,
                DeathQueue = deathQueueWriter,
                ExpOrbQueue = expOrbQueue.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            JobHandle burstJob = default;
            if (DotsBridge.DeathBurstList.IsCreated)
            {
                _frame++;

                burstJob = new DeathBurstJob
                {
                    Bursts = DotsBridge.DeathBurstList,
                    Now = (float)SystemAPI.Time.ElapsedTime,
                    SeedBase = _frame * 2654435761u
                }.Schedule(JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies));
            }

            state.Dependency = JobHandle.CombineDependencies(deathJob, burstJob);
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

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        internal partial struct DeathBurstJob : IJobEntity
        {
            public NativeList<DeathBurstData> Bursts;
            public float Now;
            public uint SeedBase;

            private void Execute(Entity entity, in Despawn despawn, in LocalTransform transform, in URPMaterialPropertyBaseColor color)
            {
                if (!despawn.Killed) return;

                // Over the cap: the rest of this frame's deaths just vanish
                if (Bursts.Length >= Bursts.Capacity) return;

                Bursts.AddNoResize(new DeathBurstData
                {
                    Center = transform.Position.xy,
                    Radius = transform.Scale * 0.5f,
                    StartTime = Now,
                    Color = color.Value.xyz,
                    Seed = math.hash(new uint2((uint)entity.Index, SeedBase)),
                });
            }
        }
    }
}
