using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponStatUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<GlobalStatModifier> globalBuffer))
                return;

            foreach ((RefRW<WeaponStats> stats, RefRW<Classification> mask, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<WeaponStats>, RefRW<Classification>, DynamicBuffer<StatModifier>>().WithAll<WeaponStatUpdateRequired>().WithEntityAccess())
            {
                DynamicBuffer<StatModifier> ownerMods = default;
                BlobAssetReference<EnemyTypeBlob> ownerBlobRef = default;

                // IDENTITY SYNC: If the weapon has an owner, inherit their Faction/Role/Tier
                if (SystemAPI.HasComponent<WeaponOwner>(entity))
                {
                    Entity ownerEntity = SystemAPI.GetComponent<WeaponOwner>(entity).Value;
                
                    if (SystemAPI.HasComponent<Classification>(ownerEntity))
                    {
                        Classification ownerMask = SystemAPI.GetComponent<Classification>(ownerEntity);
                    
                        // Update the weapon's mask to match the owner
                        mask.ValueRW.Faction = ownerMask.Faction;
                        mask.ValueRW.Role = ownerMask.Role;
                        mask.ValueRW.Tier = ownerMask.Tier;
                    }

                    if (SystemAPI.HasBuffer<StatModifier>(ownerEntity))
                    {
                        ownerMods = SystemAPI.GetBuffer<StatModifier>(ownerEntity);
                    }

                    if (SystemAPI.HasComponent<EnemyTypeRef>(ownerEntity))
                    {
                        Entity typeEntity = SystemAPI.GetComponent<EnemyTypeRef>(ownerEntity).Value;
                        if (SystemAPI.HasComponent<EnemyTypeBlobRef>(typeEntity))
                        {
                            ownerBlobRef = SystemAPI.GetComponent<EnemyTypeBlobRef>(typeEntity).Value;
                        }
                    }
                }

                stats.ValueRW.Damage = StatResolver.ResolveWeapon(StatType.Damage, stats.ValueRO.BaseDamage, mask.ValueRO, globalBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.FireRate = StatResolver.ResolveWeapon(StatType.FireRate, stats.ValueRO.BaseFireRate, mask.ValueRO, globalBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.TargetRange = StatResolver.ResolveWeapon(StatType.TargetRange, stats.ValueRO.BaseTargetRange, mask.ValueRO, globalBuffer, localMods, ownerMods, ownerBlobRef);

                if (SystemAPI.HasComponent<ProjectileStats>(entity))
                {
                    RefRW<ProjectileStats> proj = SystemAPI.GetComponentRW<ProjectileStats>(entity);

                    proj.ValueRW.Speed = StatResolver.ResolveWeapon(StatType.ProjectileSpeed, proj.ValueRO.BaseSpeed, mask.ValueRO, globalBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.Pierce = (int)StatResolver.ResolveWeapon(StatType.Pierce, proj.ValueRO.BasePierce, mask.ValueRO, globalBuffer, localMods, ownerMods, ownerBlobRef);
                }

                if (SystemAPI.HasComponent<BeamStats>(entity))
                {
                    RefRW<BeamStats> beam = SystemAPI.GetComponentRW<BeamStats>(entity);
                    
                    // TODO: Create StatTypes and resolve stats to enable Beam modifications
                    beam.ValueRW.Width = beam.ValueRO.BaseWidth;
                    beam.ValueRW.RotationSpeed = beam.ValueRO.BaseRotationSpeed;
                }

                SystemAPI.SetComponentEnabled<WeaponStatUpdateRequired>(entity, false);
            }
        }
    }
}