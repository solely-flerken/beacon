using _Game.Components;
using _Game.Core;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    /// <summary>
    /// Resolves the chain lightnings started this frame.
    /// Each jumps to the nearest enemy the chain hasn't hit yet.
    /// Chains are independent, so they are resolved in parallel.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BeamSystem))]
    [UpdateAfter(typeof(EnemyMovementSystem))]
    [UpdateBefore(typeof(DamageAndDeathSystem))]
    public partial struct ChainLightningSystem : ISystem
    {
        /// <summary>
        /// Upper bound on jumps per chain, so the visited list (MaxJumps + 1 grid indices) fits in a FixedList512Bytes (127 ints).
        /// </summary>
        private const int MaxJumps = 126;

        /// <summary>
        /// Most jumps resolved per frame across all chains. Chains past the budget are shortened, then dropped.
        /// </summary>
        private const int MaxJumpsPerFrame = 4096;

        private NativeQueue<ChainRequest> _queue;
        private NativeList<ChainRequest> _requests;

        public void OnCreate(ref SystemState state)
        {
            _queue = new NativeQueue<ChainRequest>(Allocator.Persistent);
            _requests = new NativeList<ChainRequest>(64, Allocator.Persistent);
            
            state.EntityManager.CreateSingleton(new ChainRequests { Queue = _queue });

            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            if (_queue.IsCreated) _queue.Dispose();
            if (_requests.IsCreated) _requests.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<ChainRequests>();

            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            if (!grids.EnemiesValid)
            {
                state.Dependency = new ClearChainRequestsJob { Queue = _queue }.Schedule(state.Dependency);
                return;
            }

            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;
            bool hasArcQueue = DotsBridge.ArcQueue.IsCreated;

            state.Dependency = new GatherChainRequestsJob
            {
                Queue = _queue,
                Requests = _requests,
                Budget = MaxJumpsPerFrame,
            }.Schedule(state.Dependency);

            state.Dependency = new ChainLightningJob
            {
                Requests = _requests.AsDeferredJobArray(),
                SpatialGrid = grids.Enemies,
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                SteeringLookup = SystemAPI.GetComponentLookup<Steering>(true),
                DamageQueue = damageQueue.AsParallelWriter(),
                HasArcQueue = hasArcQueue,
                ArcQueue = hasArcQueue ? DotsBridge.ArcQueue.AsParallelWriter() : default,
                Now = (float)SystemAPI.Time.ElapsedTime,
            }.Schedule(_requests, 1, JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies));

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        /// <summary>
        /// Moves this frame's requests into a list the parallel job can index.
        /// </summary>
        [BurstCompile]
        private struct GatherChainRequestsJob : IJob
        {
            public NativeQueue<ChainRequest> Queue;
            public NativeList<ChainRequest> Requests;
            public int Budget;

            public void Execute()
            {
                Requests.Clear();

                int budget = Budget;
                while (Queue.TryDequeue(out ChainRequest request))
                {
                    int jumps = math.min(math.min(request.Count, MaxJumps), budget);
                    if (jumps <= 0) continue;

                    request.Count = jumps;
                    budget -= jumps;
                    Requests.Add(request);
                }
            }
        }

        [BurstCompile]
        private struct ChainLightningJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<ChainRequest> Requests;
            [ReadOnly] public SpatialGrid SpatialGrid;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<Steering> SteeringLookup;
            [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

            public bool HasArcQueue;
            [WriteOnly] public NativeQueue<ChainArcVisualData>.ParallelWriter ArcQueue;
            public float Now;

            public void Execute(int index)
            {
                ChainRequest request = Requests[index];

                ChainFilter filter = default;
                filter.Visited.Add(request.InitialIndex);

                float2 from = PositionOf(request.InitialTarget, request.Position);
                float2 fromVelocity = VelocityOf(request.InitialTarget);
                float damage = request.Damage;

                for (int jump = 0; jump < request.Count; jump++)
                {
                    if (!HopTargeting.FindNearest(from, SpatialGrid, request.Range, ref filter, out int next)) break;

                    Entity target = SpatialGrid.Entities[next];
                    float2 to = PositionOf(target, SpatialGrid.Positions[next]);
                    float2 toVelocity = VelocityOf(target);
                    filter.Visited.Add(next);

                    damage = math.min(damage * request.Falloff, request.MinDamage);

                    DamageQueue.Enqueue(new DamageEvent
                    {
                        Target = target,
                        Value = damage,
                        Weapon = request.Weapon,
                        Position = new float3(to, 0f),
                    });

                    if (HasArcQueue)
                    {
                        ArcQueue.Enqueue(new ChainArcVisualData
                        {
                            From = from,
                            To = to,
                            FromVelocity = fromVelocity,
                            ToVelocity = toVelocity,
                            StartTime = Now,
                            Intensity = request.Damage > 0f ? damage / request.Damage : 1f,
                            Seed = math.hash(new uint3((uint)index, (uint)jump, math.asuint(Now))),
                        });
                    }

                    from = to;
                    fromVelocity = toVelocity;
                }
            }

            private float2 PositionOf(Entity entity, float2 fallback)
            {
                return TransformLookup.TryGetComponent(entity, out LocalTransform transform)
                    ? transform.Position.xy
                    : fallback;
            }

            private float2 VelocityOf(Entity entity)
            {
                return SteeringLookup.TryGetComponent(entity, out Steering steering) ? steering.Velocity : float2.zero;
            }
        }

        [BurstCompile]
        private struct ClearChainRequestsJob : IJob
        {
            public NativeQueue<ChainRequest> Queue;

            public void Execute()
            {
                Queue.Clear();
            }
        }
    }

    /// <summary>
    /// Excludes enemies this chain has already hit.
    /// </summary>
    internal struct ChainFilter : IHopFilter
    {
        public FixedList512Bytes<int> Visited;

        public bool IsExcluded(int index, Entity entity)
        {
            for (int i = 0; i < Visited.Length; i++)
            {
                if (Visited[i] == index) return true;
            }

            return false;
        }
    }
}
