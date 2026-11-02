using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    public struct BeamTag : IComponentData { }

    /// <summary>
    /// Enabled when the beam has a valid target and is actively firing.
    /// Disabled by BeamSystem when no target is present.
    /// </summary>
    public struct BeamActive : IComponentData, IEnableableComponent { }

    public struct BeamStats : IComponentData
    {
        public float BaseWidth;
        public float Width;
        public float BaseRotationSpeed;
        public float RotationSpeed;
    }

    public struct BeamFacing : IComponentData
    {
        public float2 Direction;
        
        /// <summary>
        /// Direction when the window before the next pulse opened; the pulse damages the slice swept from here to <see cref="Direction"/>.
        /// Only valid while <see cref="WindowOpen"/>.
        /// </summary>
        public float2 WindowStartDirection;

        /// <summary>
        /// True while the beam's sweep counts toward the next pulse (the last MaxWindow seconds before it).
        /// </summary>
        public bool WindowOpen;
    }
}
