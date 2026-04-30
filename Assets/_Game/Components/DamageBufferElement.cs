using Unity.Entities;

namespace _Game.Components
{
    [InternalBufferCapacity(64)]
    public struct DamageBufferElement : IBufferElementData
    {
        public float Value;
    }
}