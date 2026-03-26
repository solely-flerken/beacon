using Unity.Entities;

namespace _Game.Components
{
    public struct BeaconDeathTag: IComponentData{}
    
    public struct BeaconStats : IComponentData
    {
        public float CurrentHp;
        public float MaxHp;
        public float ArrivalRadius;
        public bool IsAlive;
    }

    public struct BeaconWeapon : IComponentData
    {
        public float FireRate;
        public float TargetRange;
        public float Timer;
    }
}