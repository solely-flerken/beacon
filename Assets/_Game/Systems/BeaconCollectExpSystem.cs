using _Game.Components;
using _Game.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WaveEndCollectSystem))]
    public partial struct BeaconCollectExpSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SaturatedTag>();
            state.RequireForUpdate<ExpOrbConfig>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ExpOrbConfig cfg = SystemAPI.GetSingleton<ExpOrbConfig>();
            float2 beaconPos = GetBeaconPosition(ref state);

            EntityCommandBuffer.ParallelWriter ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            bool hasExpQueue = DotsBridge.ExpCollectedQueue.IsCreated;
            NativeQueue<float>.ParallelWriter expQueueWriter = default;
            if (hasExpQueue) expQueueWriter = DotsBridge.ExpCollectedQueue.AsParallelWriter();

            state.Dependency = new MoveOrbsToBeaconJob
            {
                BeaconPos = beaconPos,
                ArrivalRadius = cfg.BeaconArrivalRadius,
                CollectBaseSpeed = cfg.CollectBaseSpeed,
                CollectMaxSpeed = cfg.CollectMaxSpeed,
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = ecb,
                HasExpQueue = hasExpQueue,
                ExpCollectedQueue = expQueueWriter,
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        private float2 GetBeaconPosition(ref SystemState state)
        {
            foreach (RefRO<LocalTransform> transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<BeaconStats>())
            {
                return transform.ValueRO.Position.xy;
            }

            return float2.zero;
        }
    }

    [BurstCompile]
    [WithAll(typeof(ExpOrbTag), typeof(SaturatedTag))]
    internal partial struct MoveOrbsToBeaconJob : IJobEntity
    {
        public float2 BeaconPos;
        public float ArrivalRadius;
        public float CollectBaseSpeed;
        public float CollectMaxSpeed;
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        public bool HasExpQueue;
        [WriteOnly] public NativeQueue<float>.ParallelWriter ExpCollectedQueue;

        private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref LocalTransform transform, in ExpOrbData data)
        {
            float2 pos = transform.Position.xy;
            float2 dir = BeaconPos - pos;
            float dist = math.length(dir);

            if (dist < ArrivalRadius)
            {
                if (HasExpQueue)
                {
                    ExpCollectedQueue.Enqueue(data.Value);
                }
                
                ECB.DestroyEntity(sortKey, entity);
                return;
            }

            float t = 1f - math.saturate(dist / 20f);
            float speed = math.lerp(CollectBaseSpeed, CollectMaxSpeed, t);
            transform.Position += new float3(math.normalize(dir) * speed * DeltaTime, 0f);
        }
    }
}
