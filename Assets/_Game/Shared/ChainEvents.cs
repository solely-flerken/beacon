using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// A hit that starts a chain lightning.
    /// </summary>
    public struct ChainRequest
    {
        /// <summary>
        /// Index of <see cref="InitialTarget"/> in this frame's enemy spatial grid.
        /// </summary>
        public int InitialIndex;
        public Entity InitialTarget;
        public float2 Position;

        public float Damage;
        
        public int Count;
        public float Range;
        public float MinDamage;
        public float Falloff;

        /// <summary>
        /// Weapon that started the chain: the source of its hits, and credited with them in run stats.
        /// </summary>
        public Entity Weapon;

        /// <summary>
        /// Owner of <see cref="Weapon"/>. The chain never jumps to it.
        /// </summary>
        public Entity Owner;
    }

    /// <summary>
    /// Chain lightnings started this frame, resolved by ChainLightningSystem, which creates and disposes the queue.
    /// </summary>
    public struct ChainRequests : IComponentData
    {
        public NativeQueue<ChainRequest> Queue;
    }
}
