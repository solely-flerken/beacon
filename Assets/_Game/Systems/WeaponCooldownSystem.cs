using _Game.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WeaponTargetingSystem))]
    public partial struct WeaponCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<WeaponState> weaponState, RefRO<WeaponStats> stats, Entity entity) in SystemAPI
                         .Query<RefRW<WeaponState>, RefRO<WeaponStats>>().WithDisabled<ReadyToFire>().WithEntityAccess())
            {
                float cooldown = 1f / stats.ValueRO.FireRate;

                if (!weaponState.ValueRO.Seeded)
                {
                    Random rng = new(math.hash(new uint2((uint)entity.Index, (uint)entity.Version)) | 1u);
                    weaponState.ValueRW.Timer = math.isfinite(cooldown) ? rng.NextFloat() * cooldown : 0f;
                    weaponState.ValueRW.Seeded = true;
                }

                weaponState.ValueRW.Timer += deltaTime;

                if (weaponState.ValueRO.Timer < cooldown) continue;

                // Keep the overshoot so the fire rate doesn't depend on frame rate.
                weaponState.ValueRW.Timer = math.min(weaponState.ValueRO.Timer - cooldown, cooldown);
                SystemAPI.SetComponentEnabled<ReadyToFire>(entity, true);
            }
        }
    }
}
