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
    /// One hit that bursts into sparks, uploaded to the GPU as is (32 bytes).
    /// This struct layout must match the spark_hit struct in HitSpark.shader.
    /// </summary>
    public struct HitSparkData
    {
        /// <summary>
        /// Center of the hit target.
        /// </summary>
        public float2 Center;

        /// <summary>
        /// Unit direction from the hit's source into the target.
        /// </summary>
        public float2 Direction;

        /// <summary>
        /// Radius of the target; the sparks start on its edge.
        /// </summary>
        public float Radius;

        public float StartTime;

        public uint Seed;

        public uint Style;
    }

    /// <summary>
    /// Data for one enemy death that bursts into fragments.
    /// This struct layout must match the burst_data struct in DeathBurst.shader.
    /// </summary>
    public struct DeathBurstData
    {
        public float2 Center;

        /// <summary>
        /// Radius of the enemy. The fragments start on its outline and scale with it.
        /// </summary>
        public float Radius;

        public float StartTime;

        /// <summary>
        /// The enemy's tint.
        /// </summary>
        public float3 Color;

        public uint Seed;
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
