using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Authoring
{
    public class ProjectileAuthoring : MonoBehaviour
    {
        [Header("Stats")] 
        public float speed = 30f;
        
        public float damage = 10f;
        public int pierce = 0;

        public float lifetime = 4f;
        
        private class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<ProjectileTag>(entity);
                
                AddComponent(entity, new ProjectileVelocity { Value = float2.zero });
                AddComponent(entity, new ProjectileSpeed { Value = authoring.speed });
                AddComponent(entity, new ProjectileDamage { Value = authoring.damage });
                AddComponent(entity, new ProjectilePierce { Remaining = authoring.pierce });
                
                AddComponent(entity, new Lifetime { Remaining = authoring.lifetime });
            }
        }
    }
}