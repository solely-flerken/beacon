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
                
                AddComponent(entity, new NavigationAttractor { Range = 9999f });

                AddBuffer<StatModifier>(entity);
                AddBuffer<GlobalStatModifier>(entity);
                
                AddBuffer<DamageBufferElement>(entity);
                AddComponent<IsDead>(entity);
                SetComponentEnabled<IsDead>(entity, false);
                
                AddComponent<BeaconStatUpdateRequired>(entity);
            }
        }
    }
}