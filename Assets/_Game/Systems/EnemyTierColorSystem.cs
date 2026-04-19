using _Game.Components;
using UnityEngine;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EnemyTierColorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach ((RefRO<EnemyTierComponent> tier, Entity entity) in SystemAPI.Query<RefRO<EnemyTierComponent>>().WithAll<Uninitialized>().WithEntityAccess())
            {
                if (tier.ValueRO.Value == EnemyTier.Normal) continue;

                SpriteRenderer sr = EntityManager.GetComponentObject<SpriteRenderer>(entity);
                sr.color = tier.ValueRO.Value == EnemyTier.Boss
                    ? new Color(0f, 0f, 0f) // Boss
                    : new Color(0.6f, 0.6f, 0.6f); // Elite
            }
        }
    }
}
