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
                    case CardTargetType.GlobalWeapons:
                        ApplyGlobalWeaponModifier(effect, em);
                        break;
                    case CardTargetType.WeaponsOfType:
                        ApplyWeaponTypeModifier(effect, em);
                        break;
                    case CardTargetType.EnemiesOfType:
                        ApplyEnemyModifier(effect, em);
                        break;
                }
            }
        }

        private static void ApplyBeaconModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery query = em.CreateEntityQuery(typeof(BeaconStats), typeof(StatModifier));
            if (query.IsEmpty)
            {
                Debug.LogWarning("[CardModifierApplier] No beacon entity found for Beacon modifier.");
                return;
            }

            Entity beacon = query.GetSingletonEntity();
            em.GetBuffer<StatModifier>(beacon).Add(new StatModifier
            {
                Stat = effect.Stat,
                Type = effect.ModifierType,
                Value = effect.Value
            });
            em.SetComponentEnabled<BeaconStatUpdateRequired>(beacon, true);
        }

        private static void ApplyGlobalWeaponModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery beaconQuery = em.CreateEntityQuery(typeof(BeaconStats), typeof(GlobalStatModifier));
            if (beaconQuery.IsEmpty)
            {
                Debug.LogWarning("[CardModifierApplier] No beacon entity found for GlobalWeapons modifier.");
                return;
            }

            Entity beacon = beaconQuery.GetSingletonEntity();
            em.GetBuffer<GlobalStatModifier>(beacon).Add(new GlobalStatModifier
            {
                Stat = effect.Stat,
                Type = effect.ModifierType,
                Value = effect.Value
            });

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
        
        private static void ApplyEnemyModifier(CardEffect effect, EntityManager em)
        {
            using EntityQuery catalogQuery = em.CreateEntityQuery(typeof(EnemyTypeCatalogEntry));
            if (catalogQuery.IsEmpty)
            {
                Debug.LogWarning("[CardModifierApplier] No EnemyTypeCatalog found for EnemiesOfType modifier.");
                return;
            }

            DynamicBuffer<EnemyTypeCatalogEntry> catalog = em.GetBuffer<EnemyTypeCatalogEntry>(catalogQuery.GetSingletonEntity());

            Entity typeEntity = Entity.Null;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].TypeId != effect.EnemyType) continue;
                typeEntity = catalog[i].TypeEntity;
                break;
            }

            if (typeEntity == Entity.Null)
            {
                Debug.LogWarning($"[CardModifierApplier] No catalog entry for EnemyType.{effect.EnemyType}.");
                return;
            }

            em.GetBuffer<StatModifier>(typeEntity).Add(new StatModifier
            {
                Stat = effect.Stat,
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
