using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class ExplosionAuthoring : MonoBehaviour
    {
        public float duration = 0.4f;

        private class Baker : Baker<ExplosionAuthoring>
        {
            public override void Bake(ExplosionAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<ExplosionTag>(entity);

                AddComponent(entity, new ExplosionState
                {
                    Duration = authoring.duration,
                    StartScale = 1f,
                    Elapsed = 0f,
                });
            }
        }
    }
}