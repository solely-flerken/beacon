using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class ArenaBoundsAuthoring : MonoBehaviour
    {
        [SerializeField] private int width = 60;
        [SerializeField] private int height = 60;
        
        [SerializeField] private float flowFieldCellSize = 1f;
        [SerializeField] private float spatialGridCellSize = 1f;

        private class Baker : Baker<ArenaBoundsAuthoring>
        {
            public override void Bake(ArenaBoundsAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new ArenaBounds
                {
                    Width = authoring.width,
                    Height = authoring.height,
                    FlowFieldCellSize = authoring.flowFieldCellSize,
                    SpatialGridCellSize = authoring.spatialGridCellSize,
                });
            }
        }
    }
}