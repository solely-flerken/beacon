using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// Static shared data written by UnitDataSystem once per frame.
    ///
    /// Do NOT write to this from outside UnitDataSystem.
    /// Do NOT store references to the arrays - read them fresh each frame.
    /// Do NOT index the arrays with your own query's EntityIndexInQuery,
    /// since the iteration order isn't guaranteed to match UnitDataSystem's.
    /// </summary>
    public static class EnemyDataShared
    {
        /// <summary>
        /// World positions of all active enemies, indexed 0...Count-1.
        /// </summary>
        public static NativeArray<float2> Positions;

        /// <summary>
        /// ECS entities matching Positions by index - Positions[i] and Entities[i] are the same unit.
        /// </summary>
        public static NativeArray<Entity> Entities;

        /// <summary>
        /// Number of active enemies this frame.
        /// </summary>
        public static int Count;

        public static bool IsValid => Positions.IsCreated && Count > 0;
    }
}