using System;
using Unity.Entities;

namespace _Game.Components
{
    [Flags]
    public enum FactionMask : byte
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1
    }

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
}
