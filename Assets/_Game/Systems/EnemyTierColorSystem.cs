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
            foreach ((RefRO<Classification> classification, Entity entity) in SystemAPI.Query<RefRO<Classification>>().WithAll<EnemyTag, Uninitialized>().WithEntityAccess())
            {
                TierMask tier = classification.ValueRO.Tier;
                if (tier == TierMask.Normal) continue;

                SpriteRenderer sr = EntityManager.GetComponentObject<SpriteRenderer>(entity);
                sr.color = (tier & TierMask.Boss) != 0
                    ? new Color(0f, 0f, 0f)
                    : new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }
}
