using _Game.Components;
using _Game.Shared;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Util
{
    /// <summary>
    /// Decides which units a hop (ricochet, chain) may not jump to, e.g. ones it already hit.
    /// </summary>
    public interface IHopFilter
    {
        bool IsExcluded(GridUnit unit, Entity entity);
    }

    /// <summary>
    /// Target search for effects that jump from one unit to the next.
    /// </summary>
    public static class HopTargeting
    {
        /// <summary>
        /// Finds the nearest unit of <paramref name="factions"/> within <paramref name="range"/> of <paramref name="origin"/> that <paramref name="filter"/> doesn't exclude.
        /// </summary>
        public static bool FindNearest<TFilter>(float2 origin, in SpatialGrids grids, FactionMask factions, float range, ref TFilter filter, out GridUnit result) where TFilter : struct, IHopFilter
        {
            result = default;
            bool found = false;
            float bestSq = range * range;

            for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
            {
                if (!grids.Has(factions, faction)) continue;

                SpatialGrid grid = grids[faction];
                grid.GetCellBounds(origin - range, origin + range, out int2 minCell, out int2 maxCell);
                int2 center = grid.WorldToCell(origin);
                int maxRadius = math.cmax(math.max(center - minCell, maxCell - center));
                float edgeDist = grid.DistanceToCellEdge(origin);

                for (int radius = 0; radius <= maxRadius; radius++)
                {
                    float minDist = radius == 0 ? 0f : (radius - 1) * grid.CellSize + edgeDist;
                    if (minDist * minDist >= bestSq) break;

                    foreach (int i in grid.QueryRing(center, radius))
                    {
                        float distSq = math.distancesq(origin, grid.Positions[i]);
                        if (distSq >= bestSq) continue;

                        GridUnit unit = new(faction, i);
                        if (filter.IsExcluded(unit, grid.Entities[i])) continue;

                        bestSq = distSq;
                        result = unit;
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}
