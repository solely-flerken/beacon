using Unity.Entities;

namespace _Game.Components
{
    public struct WeaponTag : IComponentData { }
    
    public struct WeaponOwner : IComponentData
    {
        public Entity Value;
    }
    
    /// <summary>
    /// Enabled by the Cooldown system when the weapon is ready to fire.
    /// </summary>
    public struct ReadyToFire : IComponentData, IEnableableComponent { }
    
    public enum WeaponType : byte
    {
        Basic,
        Beam,
        Scatter,
        Mortar,
    }

    public struct WeaponTypeRef : IComponentData
    {
        public WeaponType WeaponType;
    }
    
    public struct WeaponState : IComponentData
    {
        public float Timer;

        // False until WeaponCooldownSystem gives the weapon a random cooldown phase, so weapons spawned together don't fire together.
        public bool Seeded;
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
        public int BaseBounceCount;
        public float BaseBounceRange;
        public float BaseBounceFalloff;
        public float BaseBounceDamageFloor;

        // Result Values (Calculated)
        public float Speed;
        public int Pierce;
        public int BounceCount;
        public float BounceRange;
        public float BounceFalloff;
        public float BounceDamageFloor;
    }
    
    public struct ShootProjectile : IComponentData
    {
        public Entity ProjectilePrefab;
    }
    
    /// <summary>
    /// Dirty flag. Enable this to force WeaponStatUpdateSystem to recalculate.
    /// </summary>
    public struct WeaponStatUpdateRequired : IComponentData, IEnableableComponent { }

    public struct WeaponCatalogEntry : IBufferElementData
    {
        public WeaponType Type;
        public Entity Prefab;
    }

    #region Scatter

    public struct ShootScatter : IComponentData
    {
        public Entity ProjectilePrefab;
    }
    
    public struct ScatterStats : IComponentData
    {
        public int ProjectileCount;
        public float SpreadAngle;
        public float SpeedVariation;
    }

    #endregion
}