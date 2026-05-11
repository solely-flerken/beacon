using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Authoring
{
    [RequireComponent(typeof(WeaponBaseAuthoring))]
    [RequireComponent(typeof(WeaponTargetingAuthoring))]
    public class WeaponBeamAuthoring : MonoBehaviour
    {
        [Header("Beam Stats")]
        public float width = 0.1f;
        public float rotationSpeed = 3f;

        private class Baker : Baker<WeaponBeamAuthoring>
        {
            public override void Bake(WeaponBeamAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<BeamTag>(entity);
                
                AddComponent(entity, new BeamStats
                {
                    BaseWidth = authoring.width,
                    Width = authoring.width,
                    BaseRotationSpeed = authoring.rotationSpeed,
                    RotationSpeed = authoring.rotationSpeed,
                });
                
                AddComponent(entity, new BeamFacing { Direction = new float2(0f, 1f) });
                
                AddComponent<BeamActive>(entity);
                SetComponentEnabled<BeamActive>(entity, false);
            }
        }
    }
}