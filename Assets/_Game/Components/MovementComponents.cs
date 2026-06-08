using Unity.Entities;

namespace _Game.Components
{
    /// <summary>
    /// Movement speed in world units per second.
    /// </summary>
    public struct Speed : IComponentData
    {
        public float BaseValue;
        public float Value;
    }
}