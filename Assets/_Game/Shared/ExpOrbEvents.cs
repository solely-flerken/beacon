using _Game.Core;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Shared
{
    /// <summary>
    /// Enemy deaths that drop exp, written by EnemyDeathSystem and drained by ExpOrbSpawnSystem, which creates and disposes the queue.
    /// </summary>
    public struct ExpOrbEvents : IComponentData
    {
        public NativeQueue<DeathEvent> Queue;
    }
}
