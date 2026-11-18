using System;
using Unity.Entities;

namespace _Game.Components
{
    /// <summary>
    /// What an entity is, as a set of flags. An entity carries only the tags that apply to it.
    /// Modifiers pick their entities with a <see cref="TagFilter"/>.
    /// </summary>
    [Flags]
    public enum Tags : uint
    {
        None = 0,

        // Kind
        Unit = 1 << 0,
        Weapon = 1 << 1,

        // Side. Not stored on the entity, rather added from UnitFaction when the tags are resolved.
        Player = 1 << 2,
        Enemy = 1 << 3,

        // Unit type
        Beacon = 1 << 4,
        EnemyBasic = 1 << 5,
        EnemyFast = 1 << 6,
        EnemyTank = 1 << 7,
        EnemyRange = 1 << 8,

        // Tier
        Normal = 1 << 12,
        Elite = 1 << 13,
        Boss = 1 << 14,

        // Weapon type
        WeaponBasic = 1 << 16,
        Beam = 1 << 17,
        Scatter = 1 << 18,
        Mortar = 1 << 19,
    }
    
    public static class TagGroups
    {
        public const Tags Kind = Tags.Unit | Tags.Weapon;
        public const Tags Side = Tags.Player | Tags.Enemy;
        public const Tags UnitType = Tags.Beacon | Tags.EnemyBasic | Tags.EnemyFast | Tags.EnemyTank | Tags.EnemyRange;
        public const Tags Tier = Tags.Normal | Tags.Elite | Tags.Boss;
        public const Tags WeaponType = Tags.WeaponBasic | Tags.Beam | Tags.Scatter | Tags.Mortar;

        /// <summary>
        /// Tags a weapon takes over from its owner.
        /// </summary>
        public const Tags FromWeaponOwner = Side | UnitType | Tier;
    }
    
    public struct Classification : IComponentData
    {
        public Tags Tags;

        /// <summary>
        /// Full tags of a unit: its own plus its side.
        /// </summary>
        public readonly Tags ForUnit(Faction faction) => Tags | faction.ToTag();

        /// <summary>
        /// Full tags of a weapon: its own plus side, unit type and tier of its owner.
        /// </summary>
        public readonly Tags ForWeapon(Tags ownerTags) => Tags | (ownerTags & TagGroups.FromWeaponOwner);
    }

    /// <summary>
    /// Picks entities by their tags. An empty filter matches everything.
    /// </summary>
    [Serializable]
    public struct TagFilter
    {
        /// <summary>
        /// The entity has all of these.
        /// </summary>
        public Tags required;

        /// <summary>
        /// The entity has at least one of these. Ignored if None.
        /// </summary>
        public Tags any;

        /// <summary>
        /// The entity has none of these.
        /// </summary>
        public Tags excluded;

        public readonly bool Matches(Tags tags)
        {
            return (tags & required) == required
                   && (any == Tags.None || (tags & any) != 0)
                   && (tags & excluded) == 0;
        }
    }

    public static class TagExtensions
    {
        public static Tags ToTag(this Faction faction) => faction switch
        {
            Faction.Player => Tags.Player,
            Faction.Enemy => Tags.Enemy,
            _ => throw new ArgumentOutOfRangeException(nameof(faction), faction, null)
        };

        public static Tags ToTag(this EnemyTier tier) => tier switch
        {
            EnemyTier.Normal => Tags.Normal,
            EnemyTier.Elite => Tags.Elite,
            EnemyTier.Boss => Tags.Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
        };

        public static Tags ToTag(this WeaponType type) => type switch
        {
            WeaponType.Basic => Tags.WeaponBasic,
            WeaponType.Beam => Tags.Beam,
            WeaponType.Scatter => Tags.Scatter,
            WeaponType.Mortar => Tags.Mortar,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        public static Tags ToTag(this EnemyType type) => type switch
        {
            EnemyType.Basic => Tags.EnemyBasic,
            EnemyType.Fast => Tags.EnemyFast,
            EnemyType.Tank => Tags.EnemyTank,
            EnemyType.Range => Tags.EnemyRange,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        public static Tags ToTags(this Faction[] values) => Combine(values, v => v.ToTag());
        public static Tags ToTags(this EnemyTier[] values) => Combine(values, v => v.ToTag());
        public static Tags ToTags(this WeaponType[] values) => Combine(values, v => v.ToTag());
        public static Tags ToTags(this EnemyType[] values) => Combine(values, v => v.ToTag());

        /// <summary>
        /// ORs the tags of all <paramref name="values"/>.
        /// </summary>
        private static Tags Combine<T>(T[] values, Func<T, Tags> toTag) where T : struct, Enum
        {
            Tags tags = Tags.None;
            foreach (T value in values)
            {
                tags |= toTag(value);
            }
            
            return tags;
        }
        
        /// <summary>
        /// Extracts the tier in <paramref name="tags"/> and maps it to an EnemyTier. Normal if they hold no tier tag.
        /// </summary>
        public static EnemyTier Tier(this Tags tags)
        {
            if ((tags & Tags.Boss) != 0) return EnemyTier.Boss;
            if ((tags & Tags.Elite) != 0) return EnemyTier.Elite;
            return EnemyTier.Normal;
        }

        /// <summary>
        /// <paramref name="tags"/> with its tier replaced by <paramref name="tier"/>.
        /// </summary>
        public static Tags WithTier(this Tags tags, EnemyTier tier)
        {
            return (tags & ~TagGroups.Tier) | tier.ToTag();
        }
    }
}
