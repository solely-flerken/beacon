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

            foreach ((RefRW<WeaponStats> stats, RefRW<AttackStats> attack, RefRW<AttackRules> rules, RefRO<Classification> classification, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<WeaponStats>, RefRW<AttackStats>, RefRW<AttackRules>, RefRO<Classification>, DynamicBuffer<StatModifier>>().WithAll<WeaponStatUpdateRequired>().WithEntityAccess())
            {
                DynamicBuffer<StatModifier> ownerMods = default;
                BlobAssetReference<EnemyTypeBlob> ownerBlobRef = default;
                Tags ownerTags = Tags.None;
                FactionMask targetFactions = FactionMask.None;
                bool reportHits = false;

                if (SystemAPI.HasComponent<WeaponOwner>(entity))
                {
                    Entity ownerEntity = SystemAPI.GetComponent<WeaponOwner>(entity).Value;

                    // The weapon takes over its owner's tags and attacks the factions it is hostile to
                    if (SystemAPI.HasComponent<Hostility>(ownerEntity))
                    {
                        Faction ownerFaction = state.EntityManager.GetSharedComponent<UnitFaction>(ownerEntity).Value;
                        
                        ownerTags = SystemAPI.GetComponent<Classification>(ownerEntity).ForUnit(ownerFaction);
                        targetFactions = SystemAPI.GetComponent<Hostility>(ownerEntity).Targets;
                        reportHits = ownerFaction == Faction.Player; // TODO: Refactor?
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

                if (rules.ValueRO.TargetFactions != targetFactions)
                {
                    rules.ValueRW.TargetFactions = targetFactions;

                    // A locked target may be on a faction the weapon no longer targets
                    if (SystemAPI.HasComponent<WeaponTargeting>(entity))
                    {
                        RefRW<WeaponTargeting> targeting = SystemAPI.GetComponentRW<WeaponTargeting>(entity);
                        targeting.ValueRW.CurrentTarget = Entity.Null;
                        targeting.ValueRW.HasTarget = false;
                    }
                }

                rules.ValueRW.ReportHits = reportHits;

                Tags tags = classification.ValueRO.ForWeapon(ownerTags);

                stats.ValueRW.Damage = StatResolver.ResolveWeapon(StatType.Damage, stats.ValueRO.BaseDamage, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.FireRate = StatResolver.ResolveWeapon(StatType.FireRate, stats.ValueRO.BaseFireRate, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                stats.ValueRW.TargetRange = StatResolver.ResolveWeapon(StatType.TargetRange, stats.ValueRO.BaseTargetRange, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                attack.ValueRW.ArmorPenetration = (int)StatResolver.ResolveWeapon(StatType.ArmorPenetration, attack.ValueRO.BaseArmorPenetration, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                attack.ValueRW.CritChance = (int)StatResolver.ResolveWeapon(StatType.CritChance, attack.ValueRO.BaseCritChance, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                attack.ValueRW.CritDamage = (int)StatResolver.ResolveWeapon(StatType.CritDamage, attack.ValueRO.BaseCritDamage, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);

                if (SystemAPI.HasComponent<ProjectileStats>(entity))
                {
                    RefRW<ProjectileStats> proj = SystemAPI.GetComponentRW<ProjectileStats>(entity);

                    proj.ValueRW.Speed = StatResolver.ResolveWeapon(StatType.ProjectileSpeed, proj.ValueRO.BaseSpeed, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.Pierce = (int)StatResolver.ResolveWeapon(StatType.Pierce, proj.ValueRO.BasePierce, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceCount = (int)StatResolver.ResolveWeapon(StatType.BounceCount, proj.ValueRO.BaseBounceCount, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceRange = StatResolver.ResolveWeapon(StatType.BounceRange, proj.ValueRO.BaseBounceRange, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceFalloff = StatResolver.ResolveWeapon(StatType.BounceFalloff, proj.ValueRO.BaseBounceFalloff, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    proj.ValueRW.BounceDamageFloor = StatResolver.ResolveWeapon(StatType.BounceDamageFloor, proj.ValueRO.BaseBounceDamageFloor, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                if (SystemAPI.HasComponent<BeamStats>(entity))
                {
                    RefRW<BeamStats> beam = SystemAPI.GetComponentRW<BeamStats>(entity);
                    
                    beam.ValueRW.Width = StatResolver.ResolveWeapon(StatType.BeamWidth, beam.ValueRO.BaseWidth, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    beam.ValueRW.RotationSpeed = StatResolver.ResolveWeapon(StatType.BeamRotationSpeed, beam.ValueRO.BaseRotationSpeed, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                if (SystemAPI.HasComponent<ChainStats>(entity))
                {
                    RefRW<ChainStats> chain = SystemAPI.GetComponentRW<ChainStats>(entity);

                    chain.ValueRW.Count = (int)StatResolver.ResolveWeapon(StatType.ChainCount, chain.ValueRO.BaseCount, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.Range = StatResolver.ResolveWeapon(StatType.ChainRange, chain.ValueRO.BaseRange, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.Falloff = StatResolver.ResolveWeapon(StatType.ChainFalloff, chain.ValueRO.BaseFalloff, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                    chain.ValueRW.DamageFloor = StatResolver.ResolveWeapon(StatType.ChainDamageFloor, chain.ValueRO.BaseDamageFloor, tags, globalBuffer, waveBuffer, localMods, ownerMods, ownerBlobRef);
                }

                SystemAPI.SetComponentEnabled<WeaponStatUpdateRequired>(entity, false);
            }
        }
    }
}
