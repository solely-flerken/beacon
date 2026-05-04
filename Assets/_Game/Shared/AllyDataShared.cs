using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// Mirrors <see cref="EnemyDataShared"/> just for allies.
    /// </summary>
    public static class AllyDataShared
    {
        public static NativeArray<float2> Positions;
        public static NativeArray<Entity> Entities;
        
        public static int Count;
        public static bool IsValid => Positions.IsCreated && Count > 0;
    }
}