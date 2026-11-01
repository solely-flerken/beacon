using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Authoring
{
    public class ProjectileAuthoring : MonoBehaviour
    {
        public float lifetime = 4f;

        private class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<ProjectileTag>(entity);

                // Containers (Values will be set by the Weapon when spawned)
                AddComponent(entity, new ProjectileVelocity { Value = float2.zero });
                AddComponent(entity, new ProjectileDamage { Value = 0f });
                AddComponent(entity, new ProjectilePierce { Remaining = 0 });
                AddComponent(entity, new ProjectileBounce { Remaining = 0 });
                AddComponent(entity, new ProjectileTarget { Value = Entity.Null });

                AddComponent(entity, new Lifetime { Remaining = authoring.lifetime });
            }
        }
    }
}
