using _Game.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponTargetingSystem))]
    public partial struct MortarLaunchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            state.Dependency = new MortarLaunchJob
            {
                ECB = ecb,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(ReadyToFire))]
    internal partial struct MortarLaunchJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        private void Execute(
            Entity entity,
            [EntityIndexInQuery] int sortKey,
            in MortarLaunchData mortarData,
            in MortarStats mortarStats,
            in ProjectileStats projStats,
            in WeaponTargeting targeting,
            in LocalToWorld transform,
            in Classification classification,
            in WeaponOwner owner)
        {
            if (!targeting.HasTarget) return;

            float2 weaponPos = transform.Position.xy;
            float2 targetPos = targeting.TargetPosition;
            float2 delta = targetPos - weaponPos;

            const float minDistSq = 1.7f * 1.7f;
            if (math.lengthsq(delta) < minDistSq) return;

            float dist = math.length(delta);
            float duration = dist / projStats.Speed;
            float3 spawnPos = new(weaponPos, 0f);

            LocalTransform shellTransform = LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, MortarSystem.ShellGroundScale);
            LocalTransform shadowTransform = LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, MortarSystem.ShadowGroundScale);

            Entity shell = ECB.Instantiate(sortKey, mortarData.ShellPrefab);
            ECB.SetComponent(sortKey, shell, shellTransform);
            ECB.SetComponent(sortKey, shell, new LocalToWorld { Value = shellTransform.ToMatrix() });
            ECB.SetComponent(sortKey, shell, new ProjectileSource { Weapon = entity, Owner = owner.Value });
            ECB.SetComponent(sortKey, shell, new MortarTrajectory
            {
                StartPos = weaponPos,
                TargetPos = targetPos,
                Duration = duration,
                Elapsed = 0f,
                ArcHeight = dist * mortarData.ArcHeightPerUnit,
                ExplosionPrefab = mortarData.ExplosionPrefab,
            });
            
            Entity shadow = ECB.Instantiate(sortKey, mortarData.ShadowPrefab);
            ECB.SetComponent(sortKey, shadow, shadowTransform);
            ECB.SetComponent(sortKey, shadow, new LocalToWorld { Value = shadowTransform.ToMatrix() });
            ECB.AddComponent(sortKey, shadow, new MortarShadow { Shell = shell });
            
            bool isPlayer = (classification.Faction & FactionMask.Player) != 0;
            if (isPlayer)
            {
                ECB.AddComponent<PlayerProjectileTag>(sortKey, shell);
            }
            else
            {
                ECB.AddComponent<EnemyProjectileTag>(sortKey, shell);
            }

            ECB.SetComponentEnabled<ReadyToFire>(sortKey, entity, false);
        }
    }
}
