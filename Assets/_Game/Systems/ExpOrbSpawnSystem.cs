using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct ExpOrbSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DotsBridge.ExpOrbQueue.IsCreated || DotsBridge.ExpOrbQueue.Count == 0) return;

            if (!SystemAPI.TryGetSingleton(out PrefabRefs prefabs)) return;

            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);

            ExpOrbConfig config = SystemAPI.GetSingleton<ExpOrbConfig>();
            LocalTransform prefabTransform = SystemAPI.GetComponent<LocalTransform>(prefabs.ExpOrb);

            while (DotsBridge.ExpOrbQueue.TryDequeue(out DeathEvent death))
            {
                float2 offset = UnityEngine.Random.insideUnitCircle * 0.3f;
                float3 pos = new(death.Position.x + offset.x, death.Position.y + offset.y, 0f);

                Entity orb = ecb.Instantiate(prefabs.ExpOrb);

                float scale = ValueToScale(math.max(death.Exp, 0f), config);

                LocalTransform transform = prefabTransform;
                transform.Position = pos;
                transform.Scale = scale;
                ecb.SetComponent(orb, transform);

                bool saturated = death.Exp >= config.MaxOrbValue;

                ecb.SetComponent(orb, new ExpOrbData
                {
                    Value = death.Exp,
                    MaxValue = config.MaxOrbValue,
                    VisualScale = scale,
                    State = saturated ? ExpOrbState.Saturated : ExpOrbState.Idle,
                    MergeTarget = Entity.Null,
                });
            }

            ecb.Playback(state.EntityManager);
        }

        private static float ValueToScale(float value, in ExpOrbConfig config)
        {
            float ratio = value / config.MaxOrbValue;
            return math.lerp(config.BaseSize, config.MaxSize, ratio);
        }
    }
}