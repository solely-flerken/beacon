using Unity.Entities;
using Unity.Rendering;

namespace _Game.Components
{
    /// <summary>
    /// World time of the entity's last hit.
    /// </summary>
    [MaterialProperty("_HitTime")]
    public struct HitFlashTime : IComponentData
    {
        /// <summary>
        /// Freshly spawned entities shouldn't flash.
        /// </summary>
        public const float Never = -1000f;

        public float Value;
    }
}
