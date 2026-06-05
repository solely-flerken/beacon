using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class ExpOrbAuthoring : MonoBehaviour
    {
        private class Baker : Baker<ExpOrbAuthoring>
        {
            public override void Bake(ExpOrbAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<ExpOrbTag>(entity);

                AddComponent(entity, new ExpOrbData());
            }
        }
    }
}