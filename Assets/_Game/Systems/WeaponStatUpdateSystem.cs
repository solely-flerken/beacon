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
            if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<GlobalStatModifier> globalBuffer, isReadOnly: true))
                return;

            if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<WaveStatModifier> waveBuffer, isReadOnly: true))
                return;

            foreach ((RefRW<WeaponStats> stats, RefRW<AttackStats> attack, RefRW<Classification> mask, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<WeaponStats>, RefRW<AttackStats>, RefRW<Classification>, DynamicBuffer<StatModifier>>().WithAll<WeaponStatUpdateRequired>().WithEntityAccess())
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

                stats.ValueRW.Damage = StatResolver.ResolveWeapon(StatType.Damage, stats.ValueRO.BaseDamage, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.FireRate = StatResolver.ResolveWeapon(StatType.FireRate, stats.ValueRO.BaseFireRate, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.TargetRange = StatResolver.ResolveWeapon(StatType.TargetRange, stats.ValueRO.BaseTargetRange, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                attack.ValueRW.ArmorPenetration = (int)StatResolver.ResolveWeapon(StatType.ArmorPenetration, attack.ValueRO.BaseArmorPenetration, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);

                if (SystemAPI.HasComponent<ProjectileStats>(entity))
                {
                    RefRW<ProjectileStats> proj = SystemAPI.GetComponentRW<ProjectileStats>(entity);

                    proj.ValueRW.Speed = StatResolver.ResolveWeapon(StatType.ProjectileSpeed, proj.ValueRO.BaseSpeed, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.Pierce = (int)StatResolver.ResolveWeapon(StatType.Pierce, proj.ValueRO.BasePierce, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceCount = (int)StatResolver.ResolveWeapon(StatType.BounceCount, proj.ValueRO.BaseBounceCount, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceRange = StatResolver.ResolveWeapon(StatType.BounceRange, proj.ValueRO.BaseBounceRange, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceFalloff = StatResolver.ResolveWeapon(StatType.BounceFalloff, proj.ValueRO.BaseBounceFalloff, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceDamageFloor = StatResolver.ResolveWeapon(StatType.BounceDamageFloor, proj.ValueRO.BaseBounceDamageFloor, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                if (SystemAPI.HasComponent<BeamStats>(entity))
                {
                    RefRW<BeamStats> beam = SystemAPI.GetComponentRW<BeamStats>(entity);
                    
                    beam.ValueRW.Width = StatResolver.ResolveWeapon(StatType.BeamWidth, beam.ValueRO.BaseWidth, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    beam.ValueRW.RotationSpeed = StatResolver.ResolveWeapon(StatType.BeamRotationSpeed, beam.ValueRO.BaseRotationSpeed, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                if (SystemAPI.HasComponent<ChainStats>(entity))
                {
                    RefRW<ChainStats> chain = SystemAPI.GetComponentRW<ChainStats>(entity);

                    chain.ValueRW.Count = (int)StatResolver.ResolveWeapon(StatType.ChainCount, chain.ValueRO.BaseCount, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.Range = StatResolver.ResolveWeapon(StatType.ChainRange, chain.ValueRO.BaseRange, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.Falloff = StatResolver.ResolveWeapon(StatType.ChainFalloff, chain.ValueRO.BaseFalloff, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.DamageFloor = StatResolver.ResolveWeapon(StatType.ChainDamageFloor, chain.ValueRO.BaseDamageFloor, mask.ValueRO, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                SystemAPI.SetComponentEnabled<WeaponStatUpdateRequired>(entity, false);
            }
        }
    }
}