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
                    BaseMaxHp = config.maxHp,
                    BaseArrivalRadius = config.arrivalRadius,
                    MaxHp = config.maxHp,
                    ArrivalRadius = config.arrivalRadius,
                    
                    CurrentHp = config.maxHp,
                    IsAlive = true
                });

                AddComponent(entity, new NavigationAttractor { Range = 9999f });

                AddBuffer<StatModifier>(entity);
                AddBuffer<GlobalStatModifier>(entity);
                
                AddComponent<BeaconStatUpdateRequired>(entity);
            }
        }
    }
}