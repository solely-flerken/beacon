using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// Static shared data written by EnemyDataSystem once per frame.
    ///
    /// Do NOT write to this from outside EnemyDataSystem.
    /// Do NOT store references to the arrays - read them fresh each frame.
    /// </summary>
    public static class EnemyDataShared
    {
        /// <summary>
        /// World positions of all active enemies, indexed 0...Count-1.
        /// </summary>
        public static NativeArray<float2> Positions;

        /// <summary>
        /// ECS entities matching Positions by index.
        /// </summary>
        public static NativeArray<Entity> Entities;

        /// <summary>
        /// Number of active enemies this frame.
        /// </summary>
        public static int Count;

        public static bool IsValid => Positions.IsCreated && Count > 0;
    }
}