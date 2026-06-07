using _Game.Authoring;
using _Game.Components;
using _Game.Util;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Gameplay.Abilities
{
    public class FlareAbility : Ability
    {
        private float BaseEffectRadius => 10f;
        private float BaseDuration => 5f;

        public float EffectiveEffectRadius => ManagedStatResolver.Resolve(AbilityStatType.EffectRadius, BaseEffectRadius, Modifiers);
        public float EffectiveDuration => ManagedStatResolver.Resolve(AbilityStatType.Duration, BaseDuration, Modifiers);

        public FlareAbility() : base(baseCooldown: 15f, baseMaxCharges: 3) { }

        protected override void OnActivate(AbilityContext ctx)
        {
            using NativeArray<LocalTransform> beaconTransforms = ctx.BeaconQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            float3 beaconPos = beaconTransforms.Length > 0 ? beaconTransforms[0].Position : float3.zero;
            float2 randomOffset = (float2)UnityEngine.Random.insideUnitCircle * EffectiveEffectRadius;
            float3 position = new(beaconPos.xy + randomOffset, 0f);

            PrefabRefs prefabs = ctx.PrefabQuery.GetSingleton<PrefabRefs>();
            Entity flare = ctx.EntityManager.Instantiate(prefabs.Flare);

            ctx.EntityManager.SetComponentData(flare, LocalTransform.FromPosition(position));
            ctx.EntityManager.SetComponentData(flare, new Lifetime { Remaining = EffectiveDuration });
        }
    }
}