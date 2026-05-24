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

    public struct RunResult
    {
        public int WavesReached;
        public int EnemiesKilled;
        public int ShardsEarned;
        public bool IsNewRecord;
    }
}