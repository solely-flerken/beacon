using System;
using _Game.Components;
using _Game.Gameplay.Waves;
using Unity.Entities;
using UnityEngine;

namespace _Game.Cards
{
    /// <summary>
    /// One-shot utility that applies a card's effects.
    /// Every effect becomes one filtered modifier in the global or wave buffer.
    /// Changing either buffer marks all stat owners dirty, and entities spawned later pick the modifier up on their first stat update.
    /// </summary>
    public static class CardModifierApplier
    {
        public static void ApplyCard(CardData card, EntityManager em)
        {
            using EntityQuery globalQuery = em.CreateEntityQuery(typeof(GlobalModSingleton));
            if (globalQuery.IsEmpty)
            {
                Debug.LogWarning($"[CardModifierApplier] No GlobalModSingleton found, card '{card.Title}' not applied.");
                return;
            }

            Entity singleton = globalQuery.GetSingletonEntity();

            foreach (CardEffect effect in card.Effects)
            {
                switch (effect.Lifetime)
                {
                    case CardEffectLifetime.Run:
                        em.GetBuffer<GlobalStatModifier>(singleton).Add(new GlobalStatModifier
                        {
                            Stat = effect.Stat,
                            Filter = effect.Filter,
                            Type = effect.ModifierType,
                            Value = effect.Value,
                            RunScoped = true
                        });
                        break;

                    case CardEffectLifetime.Wave:
                        float duration = effect.Duration <= 0f ? float.MaxValue : effect.Duration;
                        em.GetBuffer<WaveStatModifier>(singleton).Add(new WaveStatModifier
                        {
                            Stat = effect.Stat,
                            Filter = effect.Filter,
                            Type = effect.ModifierType,
                            Value = effect.Value,
                            ExpiryTime = WaveManager.Instance.WaveElapsed + duration,
                        });
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(effect.Lifetime), effect.Lifetime, null);
                }
            }
        }
    }
}
