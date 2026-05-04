using Unity.Entities;

namespace _Game.Components
{
    public struct PlayerTag : IComponentData { }
    
    public struct BeaconStats : IComponentData
    {
        // Base Values (Authored)
        public float BaseArrivalRadius;

        // Result Values (Calculated)
        public float ArrivalRadius;
    }

    /// <summary>
    /// Dirty flag. Enable to force BeaconStatUpdateSystem to recalculate.
    /// </summary>
    public struct BeaconStatUpdateRequired : IComponentData, IEnableableComponent { }
}