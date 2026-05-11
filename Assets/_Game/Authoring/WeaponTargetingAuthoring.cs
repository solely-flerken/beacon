using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    [RequireComponent(typeof(WeaponBaseAuthoring))]
    public class WeaponTargetingAuthoring : MonoBehaviour
    {
        public TargetStrategy strategy = TargetStrategy.Nearest;
        
        [Header("Sticky Targeting")]
        public bool stickyLock = false;
        public float swapCooldown = 0.35f;

        private class Baker : Baker<WeaponTargetingAuthoring>
        {
            public override void Bake(WeaponTargetingAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new WeaponTargeting
                {
                    Strategy = authoring.strategy,
                    CurrentTarget = Entity.Null,
                    HasTarget = false,
                    StickyLock = authoring.stickyLock,
                    SwapCooldown = authoring.swapCooldown,
                    SwapCooldownRemaining = authoring.swapCooldown,
                });
            }
        }
    }
}