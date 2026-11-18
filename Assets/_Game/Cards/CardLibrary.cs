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
        #region Filter presets

        private static TagFilter BeaconFilter => new() { required = Tags.Unit | Tags.Beacon };
        private static TagFilter PlayerWeapons => new() { required = Tags.Weapon | Tags.Player };
        private static TagFilter PlayerWeaponsOfType(params WeaponType[] types) => new() { required = Tags.Weapon | Tags.Player | types.ToTags() };
        private static TagFilter Enemies(params EnemyType[] types) => new() { required = Tags.Unit | Tags.Enemy, any = types.ToTags() };

        #endregion

        #region Effect factories

        private static CardEffect RunFx(TagFilter filter, StatType stat, ModifierType mod, float value) => new()
        {
            Filter = filter,
            Lifetime = CardEffectLifetime.Run,
            Stat = stat,
            ModifierType = mod,
            Value = value
        };

        private static CardEffect WaveFx(TagFilter filter, StatType stat, ModifierType mod, float value, float duration = 0f) => new()
        {
            Filter = filter,
            Lifetime = CardEffectLifetime.Wave,
            Stat = stat,
            ModifierType = mod,
            Value = value,
            Duration = duration,
        };

        private static CardEffect BeaconFx(StatType stat, ModifierType mod, float value) => RunFx(BeaconFilter, stat, mod, value);
        private static CardEffect WeaponFx(StatType stat, ModifierType mod, float value) => RunFx(PlayerWeapons, stat, mod, value);
        private static CardEffect WeaponTypeFx(WeaponType type, StatType stat, ModifierType mod, float value) => RunFx(PlayerWeaponsOfType(type), stat, mod, value);
        private static CardEffect EnemyFx(StatType stat, ModifierType mod, float value) => RunFx(Enemies(), stat, mod, value);
        private static CardEffect EnemyFx(EnemyType type, StatType stat, ModifierType mod, float value) => RunFx(Enemies(type), stat, mod, value);
        private static CardEffect WaveEnemyFx(StatType stat, ModifierType mod, float value, float duration = 0f) => WaveFx(Enemies(), stat, mod, value, duration);
        private static CardEffect WaveEnemyFx(EnemyType type, StatType stat, ModifierType mod, float value, float duration = 0f) => WaveFx(Enemies(type), stat, mod, value, duration);
        private static CardEffect WaveWeaponFx(StatType stat, ModifierType mod, float value, float duration = 0f) => WaveFx(PlayerWeapons, stat, mod, value, duration);

        #endregion

        #region Constraint factories

        private static ICardConstraint HasCard(int cardId) => new HasDraftedCardConstraint { RequiredCardId = cardId };
        private static ICardConstraint MinCards(int count) => new MinDraftedCardsConstraint { MinCount = count };
        private static ICardConstraint MinHealth(float percent) => new BeaconHealthPercentConstraint { MinHealthPercent = percent };
        private static ICardConstraint HasWeaponOfType(WeaponType type) => new HasWeaponTypeConstraint { WeaponType = type };
        private static ICardConstraint HasAnyWeaponOfType(params WeaponType[] types) => new HasAnyWeaponTypeConstraint { WeaponTypes = types };

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
            constraints: new[] { HasCard(RapidFire.CardId) }
        );

        public static readonly CardData DeepMagazine = new(
            cardId: 203,
            title: "Deep Magazine",
            description: "Unlock after drafting 3 cards. All weapons gain +50% range.",
            effects: new[] { WeaponFx(StatType.TargetRange, ModifierType.Multiplicative, 50f) },
            constraints: new[] { MinCards(3) }
        );
        
        public static readonly CardData OverloadLens = new(
            cardId: 204,
            title: "Overload Lens",
            description: "Beam width increases by 400%.",
            effects: new[] { WeaponFx(StatType.BeamWidth, ModifierType.Multiplicative, 400) },
            constraints: new[] { HasWeaponOfType(WeaponType.Beam) }
        );

        public static readonly CardData LaserShow = new(
            cardId: 205,
            title: "Laser Show",
            description: "Beam rotation speed doubles.",
            effects: new[] { WeaponFx(StatType.BeamRotationSpeed, ModifierType.Multiplicative, 100) },
            constraints: new[] { HasWeaponOfType(WeaponType.Beam) }
        );

        public static readonly CardData EliteHunter = new(
            cardId: 206,
            title: "Elite Hunter",
            description: "All weapons deal 50% more damage to elite enemies.",
            effects: new[] { WeaponFx(StatType.BonusDamageElite, ModifierType.Additive, 50f) }
        );

        public static readonly CardData Bossbreaker = new(
            cardId: 207,
            title: "Bossbreaker",
            description: "All weapons deal 100% more damage to bosses.",
            effects: new[] { WeaponFx(StatType.BonusDamageBoss, ModifierType.Additive, 100f) }
        );

        // Per-type weapon cards
        public static readonly CardData ArmorPiercing = new(
            cardId: 251,
            title: "Armor Piercing",
            description: "Basic weapons pierce one additional target.",
            effects: new[] { WeaponTypeFx(WeaponType.Basic, StatType.Pierce, ModifierType.Additive, 1f) }
        );

        public static readonly CardData Ricochet = new(
            cardId: 252,
            title: "Ricochet",
            description: "Basic and Scatter shots bounce to 2 more enemies, dealing 15% less damage per bounce (down to 50%).",
            effects: new[]
            {
                WeaponTypeFx(WeaponType.Basic, StatType.BounceCount, ModifierType.Additive, 2f),
                WeaponTypeFx(WeaponType.Basic, StatType.BounceFalloff, ModifierType.Overwrite, 0.85f),
                WeaponTypeFx(WeaponType.Basic, StatType.BounceDamageFloor, ModifierType.Overwrite, 0.5f),
                WeaponTypeFx(WeaponType.Scatter, StatType.BounceCount, ModifierType.Additive, 2f),
                WeaponTypeFx(WeaponType.Scatter, StatType.BounceFalloff, ModifierType.Overwrite, 0.85f),
                WeaponTypeFx(WeaponType.Scatter, StatType.BounceDamageFloor, ModifierType.Overwrite, 0.5f),
            },
            constraints: new[] { HasAnyWeaponOfType(WeaponType.Basic, WeaponType.Scatter) }
        );

        // Enemy-debuff cards
        public static readonly CardData SlowBasicEnemies = new(
            cardId: 301,
            title: "Tar Traps",
            description: "Basic enemies move 30% slower.",
            effects: new[] { EnemyFx(EnemyType.Basic, StatType.Speed, ModifierType.Multiplicative, -30f) }
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
                WaveEnemyFx(StatType.Speed, ModifierType.Multiplicative, -40f),
                WaveEnemyFx(StatType.ContactDamage, ModifierType.Multiplicative, 50f),
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
            Ricochet,
            SlowBasicEnemies,
            Overclocked,
            DangerousGame,
            OverloadLens,
            LaserShow,
            EliteHunter,
            Bossbreaker
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
