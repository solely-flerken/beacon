using Unity.Entities;

namespace _Game.Components
{
    public struct PlayerTag : IComponentData { }
    
    public struct BeaconStats : IComponentData
    {
        // Base Values (Authored)
        public float BaseArrivalRadius;
        public float BaseExpGain;

        // Result Values (Calculated)
        public float ArrivalRadius;

        /// <summary>
        /// Multiplier on collected exp.
        /// </summary>
        public float ExpGain;
    }

    /// <summary>
    /// Dirty flag. Enable to force BeaconStatUpdateSystem to recalculate.
    /// </summary>
    public struct BeaconStatUpdateRequired : IComponentData, IEnableableComponent { }
}
