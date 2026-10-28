using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    public struct DamageEvent
    {
        /// <summary>
        /// Target entity getting damaged.
        /// </summary>
        public Entity Target;
        
        /// <summary>
        /// Damage value.
        /// </summary>
        public float Value;

        /// <summary>
        /// Weapon credited with the hit in run stats. Entity.Null for damage that isn't counted.
        /// </summary>
        public Entity Weapon;

        /// <summary>
        /// Where the hit is reported.
        /// </summary>
        public float3 Position;
    }

    /// <summary>
    /// Damage dealt this frame, applied in one pass by DamageAndDeathSystem, which creates and disposes the queue.
    /// </summary>
    public struct DamageEvents : IComponentData
    {
        public NativeQueue<DamageEvent> Queue;
    }
}
