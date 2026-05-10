using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct HitCooldownCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CleanupHitCooldownsJob
            {
                CurrentTime = (float)SystemAPI.Time.ElapsedTime,
                EntityLookup = SystemAPI.GetEntityStorageInfoLookup()
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct CleanupHitCooldownsJob : IJobEntity
    {
        [ReadOnly] public float CurrentTime;
        [ReadOnly] public EntityStorageInfoLookup EntityLookup;

        private void Execute(ref DynamicBuffer<HitCooldown> cooldowns)
        {
            for (int i = cooldowns.Length - 1; i >= 0; i--)
            {
                HitCooldown cooldown = cooldowns[i];
                if (cooldown.Expiry <= CurrentTime || !EntityLookup.Exists(cooldown.Source))
                {
                    cooldowns.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
