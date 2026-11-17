using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace _Game.Systems
{
    /// <summary>
    /// Tints freshly spawned non-Normal enemies by tier.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EnemyStatUpdateSystem))]
    public partial struct EnemyTierColorSystem : ISystem
    {
        private float4 _eliteColor;
        private float4 _bossColor;

        public void OnCreate(ref SystemState state)
        {
            _eliteColor = ShaderColor.From(new Color(1f, 0.8f, 0.2f));
            _bossColor = ShaderColor.From(new Color(1f, 1f, 1f));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ApplyTierColorJob
            {
                EliteColor = _eliteColor,
                BossColor = _bossColor,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithAll(typeof(EnemyTag), typeof(Uninitialized))]
    internal partial struct ApplyTierColorJob : IJobEntity
    {
        public float4 EliteColor;
        public float4 BossColor;

        private void Execute(in Classification classification, ref URPMaterialPropertyBaseColor color)
        {
            EnemyTier tier = classification.Tags.Tier();
            if (tier == EnemyTier.Normal) return;

            color.Value = tier == EnemyTier.Boss
                ? BossColor
                : EliteColor;
        }
    }
}
