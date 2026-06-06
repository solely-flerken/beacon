using System.Collections.Generic;
using _Game.Util;

namespace _Game.Gameplay.Abilities
{
    public abstract class Ability
    {
        protected float BaseCooldown { get; }
        protected int BaseMaxCharges { get; }

        public float EffectiveCooldown => ManagedStatResolver.Resolve(AbilityStatType.Cooldown, BaseCooldown, Modifiers);
        public int EffectiveMaxCharges => (int)ManagedStatResolver.Resolve(AbilityStatType.MaxCharges, BaseMaxCharges, Modifiers);

        public float RechargeTimer { get; private set; }
        public int Charges { get; private set; }

        protected List<Modifier<AbilityStatType>> Modifiers { get; } = new();

        protected Ability(float baseCooldown, int baseMaxCharges)
        {
            BaseCooldown = baseCooldown;
            BaseMaxCharges = baseMaxCharges;
        }

        public void Tick(float dt)
        {
            if (Charges >= EffectiveMaxCharges) return;

            RechargeTimer -= dt;
            if (RechargeTimer > 0f) return;

            Charges++;
            RechargeTimer = EffectiveCooldown;
        }

        public bool TryUse(AbilityContext ctx)
        {
            if (!TryConsume()) return false;
            OnActivate(ctx);
            return true;
        }

        public void AddModifier(Modifier<AbilityStatType> mod)
        {
            Modifiers.Add(mod);
        }

        public void Reset()
        {
            Charges = EffectiveMaxCharges;
            RechargeTimer = 0f;
        }

        private bool TryConsume()
        {
            if (Charges <= 0) return false;

            Charges--;
            if (RechargeTimer <= 0f)
            {
                RechargeTimer = EffectiveCooldown;
            }

            return true;
        }

        protected abstract void OnActivate(AbilityContext ctx);
    }
}