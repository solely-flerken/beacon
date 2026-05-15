using _Game.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// Written by ExpOrbGridSystem once per frame. Read-only for all other systems.
    /// Do NOT write to this from outside ExpOrbGridSystem.
    /// Do NOT store references to the arrays - read them fresh each frame.
    /// </summary>
    public static class ExpOrbGridShared
    {
        /// <summary>
        /// World positions of all non-saturated orbs, indexed 0...Count-1.
        /// </summary>
        public static NativeArray<float2> Positions;

        /// <summary>
        /// ECS entities matching Positions by index.
        /// </summary>
        public static NativeArray<Entity> Entities;

        /// <summary>
        /// ExpOrbData matching Positions by index.
        /// </summary>
        public static NativeArray<ExpOrbData> OrbData;

        /// <summary>
        /// Spatial cell map rebuilt every frame. Only indexes Idle orbs.
        /// Key: cell coordinate. Value: index into Positions/Entities/OrbData.
        /// Cell size is read from ExpOrbConfig.MergeRadius, meaning that a higher MergeRadius leads to worse performance.
        /// </summary>
        public static NativeParallelMultiHashMap<int2, int> CellMap;

        /// <summary>
        /// Number of non-saturated orbs this frame.
        /// </summary>
        public static int Count;

        public static bool IsValid => CellMap.IsCreated && Count > 0;
    }
}
