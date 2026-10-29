using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    public struct MortarShellTag : IComponentData { }

    public struct MortarTrajectory : IComponentData
    {
        public float2 StartPos;
        public float2 TargetPos;
        public float Duration;
        public float Elapsed;
        public float ArcHeight;
        public Entity ExplosionPrefab;
    }

    public struct MortarShadow : IComponentData
    {
        public Entity Shell;
    }

    /// <summary>
    /// Snapshot of weapon stats at fire time, stored on the shell.
    /// </summary>
    public struct MortarStats : IComponentData
    {
        public float ExplosionRadius;
    }

    public struct MortarLaunchData : IComponentData
    {
        public Entity ShellPrefab;
        public Entity ExplosionPrefab;
        public Entity ShadowPrefab;
        public float ArcHeight;
    }
}
