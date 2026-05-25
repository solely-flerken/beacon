using System.Collections.Generic;
using _Game.Cards.Constraints;
using _Game.Components;
using UnityEngine;

namespace _Game.Cards
{
    /// <summary>
    /// Central registry of all cards in the game.
    /// </summary>
    public static class CardLibrary
    {
        #region Effect factories

        private static CardEffect BeaconFx(StatType stat, ModifierType mod, float value) => new()
        {
            TargetType = CardTargetType.Beacon,
            Stat = stat,
            ModifierType = mod,
            Value = value
        };

        private static CardEffect WeaponFx(StatType stat, ModifierType mod, float value) => new()
        {
            TargetType = CardTargetType.BeaconWeapons,
            Stat = stat,
            ModifierType = mod,
            Value = value
        };

        private static CardEffect WeaponTypeFx(WeaponType type, StatType stat, ModifierType mod, float value) => new()
        {
            TargetType = CardTargetType.WeaponsOfType,
            WeaponType = type,
            Stat = stat,
            ModifierType = mod,
            Value = value
        };

        private static CardEffect EnemyFx(RoleMask type, StatType stat, ModifierType mod, float value) => new()
        {
            TargetType = CardTargetType.EnemiesOfType,
            EnemyType = type,
            Stat = stat,
            ModifierType = mod,
            Value = value
        };

        private static CardEffect WaveEnemyFx(RoleMask role, StatType stat, ModifierType mod, float value, float duration = 0f) => new()
        {
            TargetType = CardTargetType.WaveEnemies,
            EnemyType = role,
            Stat = stat,
            ModifierType = mod,
            Value = value,
            Duration = duration,
        };

        private static CardEffect WaveWeaponFx(StatType stat, ModifierType mod, float value, float duration = 0f) => new()
        {
            TargetType = CardTargetType.WaveWeapons,
            Stat = stat,
            ModifierType = mod,
            Value = value,
            Duration = duration,
        };

        #endregion

        #region Constraint factories

        private static ICardConstraint HasCard(int cardId) => new HasDraftedCardConstraint { RequiredCardId = cardId };
        private static ICardConstraint MinCards(int count) => new MinDraftedCardsConstraint { MinCount = count };
        private static ICardConstraint MinHealth(float percent) => new BeaconHealthPercentConstraint { MinHealthPercent = percent };

        #endregion

        #region Card definitions

        // Beacon cards
        public static readonly CardData ReinforcedArmour = new(
            cardId: 101,
            title: "Reinforced Armor",
            description: "Beacon gains +50% max HP.",
            effects: new[] { BeaconFx(StatType.MaxHp, ModifierType.Multiplicative, 50f) }
        );

        // Weapon cards
        public static readonly CardData RapidFire = new(
            cardId: 201,
            title: "Rapid Fire",
            description: "All weapons fire twice as fast.",
            effects: new[] { WeaponFx(StatType.FireRate, ModifierType.Multiplicative, 100f) }
        );

        public static readonly CardData OverchargedBarrels = new(
            cardId: 202,
            title: "Overcharged Barrels",
            description: "All weapons deal 50% more damage. Requires Rapid Fire.",
            effects: new[] { WeaponFx(StatType.Damage, ModifierType.Multiplicative, 50f) },
            constraints: new[] { HasCard(101) }
        );

        public static readonly CardData DeepMagazine = new(
            cardId: 203,
            title: "Deep Magazine",
            description: "Unlock after drafting 3 cards. All weapons gain +50% range.",
            effects: new[] { WeaponFx(StatType.TargetRange, ModifierType.Multiplicative, 50f) },
            constraints: new[] { MinCards(3) }
        );

        // Per-type weapon cards
        public static readonly CardData ArmorPiercing = new(
            cardId: 251,
            title: "Armor Piercing",
            description: "Basic weapons pierce one additional target.",
            effects: new[] { WeaponTypeFx(WeaponType.Basic, StatType.Pierce, ModifierType.Additive, 1f) }
        );

        // Enemy-debuff cards
        public static readonly CardData SlowBasicEnemies = new(
            cardId: 301,
            title: "Tar Traps",
            description: "Basic enemies move 30% slower.",
            effects: new[] { EnemyFx(RoleMask.Basic, StatType.Speed, ModifierType.Multiplicative, -30f) }
        );

        // Wave-scoped cards
        public static readonly CardData Overclocked = new(
            cardId: 401,
            title: "Overclocked",
            description: "All weapons deal 100% more damage for 5 seconds.",
            effects: new[] { WaveWeaponFx(StatType.Damage, ModifierType.Multiplicative, 100f, duration: 5f) }
        );

        public static readonly CardData DangerousGame = new(
            cardId: 402,
            title: "Dangerous Game",
            description: "All enemies move 40% slower for this wave, but deal 50% more contact damage.",
            effects: new[]
            {
                WaveEnemyFx(RoleMask.None, StatType.Speed, ModifierType.Multiplicative, -40f),
                WaveEnemyFx(RoleMask.None, StatType.ContactDamage, ModifierType.Multiplicative, 50f),
            }
        );

        #endregion

        private static readonly CardData[] All =
        {
            ReinforcedArmour,
            RapidFire,
            OverchargedBarrels,
            DeepMagazine,
            ArmorPiercing,
            SlowBasicEnemies,
            Overclocked,
            DangerousGame,
        };

        #region Utility functions

        public static CardData[] SelectCards(CardDraftContext ctx, int count)
        {
            List<CardData> eligible = new();
            foreach (CardData card in All)
            {
                if (AreConstraintsMet(ctx, card))
                {
                    eligible.Add(card);
                }
            }

            Shuffle(eligible);

            int take = Mathf.Min(count, eligible.Count);
            CardData[] result = new CardData[take];
            for (int i = 0; i < take; i++)
            {
                result[i] = eligible[i];
            }
            
            return result;
        }

        private static bool AreConstraintsMet(CardDraftContext ctx, CardData card)
        {
            foreach (ICardConstraint constraint in card.Constraints)
            {
                if (!constraint.IsMet(ctx)) return false;
            }
            
            return true;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}