using _Game.Components;
using _Game.Config;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class BeaconAuthoring : MonoBehaviour
    {
        public BeaconConfig config;

        private class Baker : Baker<BeaconAuthoring>
        {
            public override void Bake(BeaconAuthoring authoring)
            {
                BeaconConfig config = authoring.config;
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<PlayerTag>(entity);
                
                // Classification
                AddComponent(entity, new Classification
                {
                    Target = TargetMask.Character,
                    Faction = FactionMask.Player,
                    Role = RoleMask.None,
                    Tier = TierMask.None
                });
                
                // Basic beacon stats
                AddComponent(entity, new BeaconStats
                {
                    BaseArrivalRadius = config.arrivalRadius,
                    ArrivalRadius = config.arrivalRadius,
                });

                AddComponent(entity, new Health
                {
                    BaseMax = config.maxHp,
                    Max = config.maxHp,
                    Current = config.maxHp,
                    IsAlive = true
                });

                AddComponent(entity, new Speed
                {
                    BaseValue = config.moveSpeed,
                    Value = config.moveSpeed,
                });
                AddComponent<PlayerMoveInput>(entity);
                
                // Attractor
                AddComponent(entity, new NavigationAttractor { Range = 9999f });
                
                // Damage
                AddBuffer<HitCooldown>(entity);
                AddBuffer<DamageBufferElement>(entity);
                AddComponent<IsDead>(entity);
                SetComponentEnabled<IsDead>(entity, false);
                
                // Modifier
                AddBuffer<StatModifier>(entity);
                AddComponent<BeaconStatUpdateRequired>(entity);
            }
        }
    }
}