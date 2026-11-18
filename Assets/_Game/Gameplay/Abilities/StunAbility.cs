using _Game.Components;
using _Game.Gameplay.Waves;
using _Game.Util;
using Unity.Entities;

namespace _Game.Gameplay.Abilities
{
    public class StunAbility : Ability
    {
        private float BaseDuration => 3f;

        public float EffectiveDuration => ManagedStatResolver.Resolve(AbilityStatType.Duration, BaseDuration, Modifiers);

        public StunAbility() : base(baseCooldown: 20f, baseMaxCharges: 1) { }

        protected override void OnActivate(AbilityContext ctx)
        {
            using EntityQuery modQuery = ctx.EntityManager.CreateEntityQuery(typeof(GlobalModSingleton), typeof(WaveStatModifier));
            if (modQuery.IsEmpty) return;

            DynamicBuffer<WaveStatModifier> waveMods = ctx.EntityManager.GetBuffer<WaveStatModifier>(modQuery.GetSingletonEntity());
            waveMods.Add(new WaveStatModifier
            {
                Stat = StatType.Speed,
                Filter = new TagFilter { required = Tags.Unit | Tags.Enemy },
                Type = ModifierType.Overwrite,
                Value = 0f,
                ExpiryTime = WaveManager.Instance.WaveElapsed + EffectiveDuration,
            });
        }
    }
}
