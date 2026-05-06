using _Game.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Game.Cards
{
    /// <summary>
    /// One-shot utility that applies a card's effects to the appropriate ECS entities.
    /// Called on the main thread while simulation is paused (CardDraft state).
    /// </summary>
    public static class CardModifierApplier
    {
        public static void ApplyCard(CardData card, EntityManager em)
        {
            foreach (CardEffect effect in card.Effects)
            {
                switch (effect.TargetType)
                {
                    case CardTargetType.Beacon:
                        ApplyBeaconModifier(effect, em);
                        break;
                    case CardTargetType.BeaconWeapons:
                        ApplyBeaconWeaponModifier(effect, em);
                        break;
                    case CardTargetType.WeaponsOfType:
                        ApplyWeaponTypeModifier(effect, em);
                        break;
                    case CardTargetType.EnemiesOfType:
                        ApplyEnemyTypeModifier(effect, em);
                        break;
                }
            }
        }

        private static void ApplyBeaconModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery beaconQuery = em.CreateEntityQuery(typeof(BeaconStats), typeof(StatModifier));
            if (beaconQuery.IsEmpty)
            {
                Debug.LogWarning("[CardModifierApplier] No beacon entity found to apply modifiers to.");
                return;
            }

            Entity beacon = beaconQuery.GetSingletonEntity();
            em.GetBuffer<StatModifier>(beacon).Add(new StatModifier
            {
                Stat = effect.Stat,
                Type = effect.ModifierType,
                Value = effect.Value
            });
            em.SetComponentEnabled<BeaconStatUpdateRequired>(beacon, true);
        }

        // TODO: Refactor - right now setting all weapons as dirty
        private static void ApplyBeaconWeaponModifier(CardEffect effect, EntityManager em)
        {
            ApplyBeaconModifier(effect, em);

            EntityQueryDesc weaponDesc = new()
            {
                All = new[] { ComponentType.ReadOnly<WeaponStatUpdateRequired>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };
            using EntityQuery weaponQuery = em.CreateEntityQuery(weaponDesc);
            em.SetComponentEnabled<WeaponStatUpdateRequired>(weaponQuery, true);
        }

        private static void ApplyWeaponTypeModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery query = em.CreateEntityQuery(typeof(WeaponTag), typeof(WeaponTypeRef), typeof(StatModifier));
            using NativeArray<Entity> weapons = query.ToEntityArray(Allocator.Temp);

            bool found = false;
            foreach (Entity weapon in weapons)
            {
                if (em.GetComponentData<WeaponTypeRef>(weapon).WeaponType != effect.WeaponType) continue;

                em.GetBuffer<StatModifier>(weapon).Add(new StatModifier
                {
                    Stat = effect.Stat,
                    Type = effect.ModifierType,
                    Value = effect.Value
                });
                em.SetComponentEnabled<WeaponStatUpdateRequired>(weapon, true);
                found = true;
            }

            if (!found)
            {
                Debug.LogWarning($"[CardModifierApplier] No weapons of type {effect.WeaponType} found.");
            }
        }
        
        private static void ApplyEnemyTypeModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery globalQuery = em.CreateEntityQuery(typeof(GlobalModSingleton));
            if (globalQuery.IsEmpty)
            {
                Debug.LogWarning("[CardModifierApplier] No GlobalModSingleton found.");
                return;
            }

            DynamicBuffer<GlobalStatModifier> globalBuffer = em.GetBuffer<GlobalStatModifier>(globalQuery.GetSingletonEntity());

            globalBuffer.Add(new GlobalStatModifier
            {
                Stat = effect.Stat,
                Mask = new Classification { Faction = FactionMask.Enemy, Role = effect.EnemyType },
                Type = effect.ModifierType,
                Value = effect.Value
            });

            EntityQueryDesc enemyDesc = new()
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EnemyTag>(),
                    ComponentType.ReadOnly<EnemyStatUpdateRequired>(),
                    EnemyTypeTagMap.Get(effect.EnemyType)
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };
            using EntityQuery enemyQ = em.CreateEntityQuery(enemyDesc);
            em.SetComponentEnabled<EnemyStatUpdateRequired>(enemyQ, true);
        }
    }
}
