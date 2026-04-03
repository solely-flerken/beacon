using Unity.Entities;

namespace _Game.Components
{
    public struct WeaponTag : IComponentData { }
    
    /// <summary>
    /// Enabled by the Cooldown system when the weapon is ready to fire.
    /// </summary>
    public struct ReadyToFire : IComponentData, IEnableableComponent { }
    
    public struct WeaponState : IComponentData
    {
        public float Timer;
    }
    
    public struct WeaponStats : IComponentData
    {
        // Base Values (Authored)
        public float BaseDamage;
        public float BaseFireRate;
        public float BaseTargetRange;
        
        // Result Values (Calculated)
        public float Damage;
        public float FireRate;
        public float TargetRange;
    }

    public struct ProjectileStats : IComponentData
    {
        // Base Values (Authored)
        public float BaseSpeed;
        public int BasePierce;
        
        // Result Values (Calculated)
        public float Speed;
        public int Pierce;
    }
    
    public struct ShootProjectile : IComponentData
    {
        public Entity ProjectilePrefab;
    }
    
    /// <summary>
    /// Dirty flag. Enable this to force WeaponStatUpdateSystem to recalculate.
    /// </summary>
    public struct WeaponStatUpdateRequired : IComponentData, IEnableableComponent { }
}