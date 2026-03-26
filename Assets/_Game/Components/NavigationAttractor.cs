using Unity.Entities;

namespace _Game.Components
{
    public struct NavigationAttractor : IComponentData
    {
        public float Range; // Use a very high number (e.g. 9999) for the Beacon
    }
}