using Unity.Entities;

namespace _Game.Components
{
    public struct BeaconDeathTag: IComponentData{}
    
    public struct BeaconStats : IComponentData
    {
        // Base Values (Authored)
        public float BaseMaxHp;
        public float BaseArrivalRadius;

        // Result Values (Calculated)
        public float MaxHp;
        public float ArrivalRadius;

        // Runtime
        public float CurrentHp;
        public bool IsAlive;
    }

    /// <summary>
    /// Dirty flag. Enable to force BeaconStatUpdateSystem to recalculate.
    /// </summary>
    public struct BeaconStatUpdateRequired : IComponentData, IEnableableComponent { }
}