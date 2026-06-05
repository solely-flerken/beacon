using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    namespace _Game.Authoring
    {
        public class ExpOrbConfigAuthoring : MonoBehaviour
        {
            [Header("Orb Values")] 
            public float maxOrbValue = 5f;

            [Header("Visual")] 
            public float baseSize = 0.15f;
            public float maxSize = 0.4f;

            [Header("Accumulation")]
            [Tooltip("Radius within which a death accumulates into an existing orb instead of spawning a new one.")]
            public float mergeRadius = 2f;
            [Tooltip("Maximum simultaneous exp orbs in the world.")]
            public int maxBuckets = 32;

            [Header("Movement")] 
            public float collectBaseSpeed = 5f;
            public float collectMaxSpeed = 20f;

            public float beaconArrivalRadius = 0.5f;

            private class Baker : Baker<ExpOrbConfigAuthoring>
            {
                public override void Bake(ExpOrbConfigAuthoring a)
                {
                    Entity entity = GetEntity(TransformUsageFlags.None);

                    AddComponent(entity, new ExpOrbConfig
                    {
                        MaxOrbValue = a.maxOrbValue,
                        BaseSize = a.baseSize,
                        MaxSize = a.maxSize,
                        MergeRadius = a.mergeRadius,
                        MaxBuckets = a.maxBuckets,
                        CollectBaseSpeed = a.collectBaseSpeed,
                        CollectMaxSpeed = a.collectMaxSpeed,
                        BeaconArrivalRadius = a.beaconArrivalRadius,
                    });
                }
            }
        }
    }
}