using Unity.Entities;

namespace _Game.Components
{
    /// <summary>
    /// The side a unit is on.
    /// </summary>
    public enum Faction : byte
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>
    /// The side a unit is on, which decides the grid it sits in. Only units have it.
    /// </summary>
    public struct UnitFaction : ISharedComponentData
    {
        public Faction Value;
    }

    /// <summary>
    /// Factions a unit attacks. Its weapons take their <see cref="AttackRules.TargetFactions"/> from it.
    /// </summary>
    public struct Hostility : IComponentData
    {
        public FactionMask Targets;
    }

    public static class FactionExtensions
    {
        public static FactionMask ToMask(this Faction faction) => (FactionMask)(1 << (int)faction);
    }
}
