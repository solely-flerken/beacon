using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Util
{
    /// <summary>
    /// Decides which units a hop (ricochet, chain) may not jump to, e.g. ones it already hit.
    /// </summary>
    public interface IHopFilter
    {
        bool IsExcluded(int index, Entity entity);
    }

    /// <summary>
    /// Target search for effects that jump from one unit to the next.
    /// </summary>
    public static class HopTargeting
    {
        /// <summary>
        /// Finds the nearest unit within <paramref name="range"/> of <paramref name="origin"/> that <paramref name="filter"/> doesn't exclude.
        /// </summary>
        public static bool FindNearest<TFilter>(float2 origin, SpatialGrid grid, float range, ref TFilter filter, out int resultIndex) where TFilter : struct, IHopFilter
        {
            resultIndex = -1;
            float bestSq = range * range;

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
                    if (distSq >= bestSq || filter.IsExcluded(i, grid.Entities[i])) continue;

                    bestSq = distSq;
                    resultIndex = i;
                }
            }

            return resultIndex != -1;
        }
    }
}
