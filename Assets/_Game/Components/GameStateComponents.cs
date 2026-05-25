using Unity.Entities;

namespace _Game.Components
{
    public struct WaveEndTag : IComponentData { }

    public struct WaveStateSingleton : IComponentData
    {
        public bool IsActive;
        public float WaveElapsed;
    }
}