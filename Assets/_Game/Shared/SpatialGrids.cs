using _Game.Components;
using _Game.Util;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    public struct SpatialGrids : IComponentData
    {
        /// <summary>
        /// Number of factions, each with its own grid. Keep in sync with <see cref="Faction"/>.
        /// </summary>
        public const int FactionCount = 2;

        [ReadOnly] public SpatialGrid Allies;
        [ReadOnly] public SpatialGrid Enemies;

        public readonly bool EnemiesValid => Enemies.IsCreated && Enemies.Count > 0;
        public readonly bool AlliesValid => Allies.IsCreated && Allies.Count > 0;

        /// <summary>
        /// The grid of a faction, by its <see cref="Faction"/> value.
        /// </summary>
        public readonly SpatialGrid this[int faction]
        {
            get
            {
                return (Faction)faction switch
                {
                    Faction.Player => Allies,
                    Faction.Enemy => Enemies,
                    _ => default
                };
            }
        }

        /// <summary>
        /// True if <paramref name="faction"/> is part of <paramref name="factions"/> and has units.
        /// </summary>
        public readonly bool Has(FactionMask factions, int faction)
        {
            return ((int)factions & (1 << faction)) != 0 && this[faction].Count > 0;
        }

        /// <summary>
        /// True if any faction of <paramref name="factions"/> has units.
        /// </summary>
        public readonly bool AnyUnits(FactionMask factions)
        {
            for (int faction = 0; faction < FactionCount; faction++)
            {
                if (Has(factions, faction)) return true;
            }

            return false;
        }

        public readonly Entity EntityOf(GridUnit unit) => this[unit.Faction].Entities[unit.Index];
        public readonly float2 PositionOf(GridUnit unit) => this[unit.Faction].Positions[unit.Index];
    }

    public readonly struct GridUnit
    {
        /// <summary>
        /// Index of the unit's faction. (Position of the bit in <see cref="FactionMask"/>)
        /// </summary>
        public readonly int Faction;
        
        /// <summary>
        /// Index of the unit in that faction's grid.
        /// </summary>
        public readonly int Index; 

        public GridUnit(int faction, int index)
        {
            Faction = faction;
            Index = index;
        }

        /// <summary>
        /// Faction and index packed into one int.
        /// </summary>
        public int Key => (Index << 3) | Faction;
    }
}
