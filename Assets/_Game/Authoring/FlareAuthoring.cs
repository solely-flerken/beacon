using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class FlareAuthoring : MonoBehaviour
    {
        public float range = 12f;
        public float duration = 5f;

        private class Baker : Baker<FlareAuthoring>
        {
            public override void Bake(FlareAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new FlareTag());
                
                AddComponent(entity, new NavigationAttractor { Range = authoring.range });
                
                AddComponent(entity, new Lifetime { Remaining = authoring.duration });
            }
        }
    }
}