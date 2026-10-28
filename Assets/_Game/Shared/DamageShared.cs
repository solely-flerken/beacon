using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace _Game.Shared
{
    public struct DamageEvent
    {
        public Entity Target;
        public float Value;
    }

    /// <summary>
    /// Damage dealt this frame, applied in one pass by DamageAndDeathSystem.
    /// Owned by DamageAndDeathSystem, which creates and disposes the queue.
    /// </summary>
    public static class DamageShared
    {
        public static NativeQueue<DamageEvent> Queue;
        public static JobHandle Writers;
    }
}
