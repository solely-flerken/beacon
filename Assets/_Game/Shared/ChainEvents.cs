using _Game.Components;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Shared
{
    /// <summary>
    /// A hit that starts a chain lightning.
    /// </summary>
    public struct ChainRequest
    {
        /// <summary>
        /// The unit the chain starts from.
        /// </summary>
        public GridUnit InitialUnit;

        /// <summary>
        /// Factions the chain can jump to, copied from the weapon.
        /// </summary>
        public FactionMask TargetFactions;

        public bool ReportHits;

        public float Damage;
        
        public int Count;
        public float Range;
        public float MinDamage;
        public float Falloff;

        /// <summary>
        /// Weapon that started the chain: the source of its hits.
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
