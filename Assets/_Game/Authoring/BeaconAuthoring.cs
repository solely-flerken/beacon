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
                    CurrentHp = config.maxHp,
                    MaxHp = config.maxHp,
                    ArrivalRadius = config.arrivalRadius,
                    IsAlive = true
                });

                AddComponent(entity, new NavigationAttractor { Range = 9999f });
            }
        }
    }
}