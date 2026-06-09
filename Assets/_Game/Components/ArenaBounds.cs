using Unity.Entities;

namespace _Game.Components
{
    public struct ArenaBounds : IComponentData
    {
        public int Width;
        public int Height;
        public float FlowFieldCellSize;
        public float SpatialGridCellSize;
    }
}