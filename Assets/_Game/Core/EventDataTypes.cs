using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Core
{
    public struct DeathEvent
    {
        public float3 Position;
        public float Exp;
    }

    public struct HitEvent
    {
        public float3 Position;
        public float Damage;
        public Entity WeaponEntity;
    }

    /// <summary>
    /// Data for one jump of a chain lightning, for the visuals.
    /// This struct layout must must match the arc_data struct in ChainLightning.shader.
    /// </summary>
    public struct ChainArcVisualData
    {
        public float2 From;
        public float2 To;
        
        public float2 FromVelocity;
        public float2 ToVelocity;
        
        public float StartTime;
        
        public float Intensity;
        
        public uint Seed;
    }

    public struct RunResult
    {
        public int WavesReached;
        public int EnemiesKilled;
        public int ShardsEarned;
        public bool IsNewRecord;
    }
}
