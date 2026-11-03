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

        [Header("Chain Lightning")]
        public int chainCount = 0;
        public float chainRange = 6f;
        public float chainFalloff = 0.8f;
        public float chainDamageFloor = 0.5f;

        private class Baker : Baker<WeaponBeamAuthoring>
        {
            public override void Bake(WeaponBeamAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<BeamTag>(entity);

                AddComponent(entity, new ChainStats
                {
                    BaseCount = authoring.chainCount,
                    BaseRange = authoring.chainRange,
                    BaseFalloff = authoring.chainFalloff,
                    BaseDamageFloor = authoring.chainDamageFloor,
                    Count = authoring.chainCount,
                    Range = authoring.chainRange,
                    Falloff = authoring.chainFalloff,
                    DamageFloor = authoring.chainDamageFloor,
                });

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
