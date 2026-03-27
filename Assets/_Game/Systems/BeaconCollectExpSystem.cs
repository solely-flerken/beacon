using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExpOrbMergeSystem))]
    public partial struct BeaconCollectExpSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SaturatedTag>();
            state.RequireForUpdate<ExpOrbConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ExpOrbConfig cfg = SystemAPI.GetSingleton<ExpOrbConfig>();
            float2 beaconPos = GetBeaconPosition(ref state);
            float delta = SystemAPI.Time.DeltaTime;

            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);
            
            foreach ((RefRW<ExpOrbData> dataRW, RefRW<LocalTransform> transform, Entity entity) in SystemAPI.Query<RefRW<ExpOrbData>, RefRW<LocalTransform>>().WithAll<ExpOrbTag, SaturatedTag>().WithEntityAccess())
            {
                float2 pos = transform.ValueRO.Position.xy;
                float2 dir = beaconPos - pos;
                float dist = math.length(dir);

                if (dist < cfg.BeaconArrivalRadius)
                {
                    // TODO: Feed dataRW.ValueRO.Value into the Exp/leveling system here.
                    Debug.Log($"[BeaconCollectExpSystem] Granted {dataRW.ValueRO.Value} exp.");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                float t = 1f - math.saturate(dist / 20f);
                float speed = math.lerp(cfg.CollectBaseSpeed, cfg.CollectMaxSpeed, t);
                transform.ValueRW.Position += new float3(math.normalize(dir) * speed * delta, 0f);
            }

            ecb.Playback(state.EntityManager);
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
}