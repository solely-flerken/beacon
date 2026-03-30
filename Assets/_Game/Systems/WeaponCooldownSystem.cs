using _Game.Components;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<WeaponState> weaponState, RefRO<WeaponStats> stats, Entity entity) in SystemAPI
                         .Query<RefRW<WeaponState>, RefRO<WeaponStats>>().WithDisabled<ReadyToFire>().WithEntityAccess())
            {
                weaponState.ValueRW.Timer += deltaTime;

                float cooldown = 1f / stats.ValueRO.FireRate;

                if (!(weaponState.ValueRO.Timer >= cooldown)) continue;

                weaponState.ValueRW.Timer = 0f;
                SystemAPI.SetComponentEnabled<ReadyToFire>(entity, true);
            }
        }
    }
}