using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class MortarShellAuthoring : MonoBehaviour
    {
        private class Baker : Baker<MortarShellAuthoring>
        {
            public override void Bake(MortarShellAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent<MortarShellTag>(entity);
                AddComponent<MortarTrajectory>(entity);
                
                AddComponent(entity, new ProjectileSource { Weapon = Entity.Null, Owner = Entity.Null });
            }
        }
    }
}
