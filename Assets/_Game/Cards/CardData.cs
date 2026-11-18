using System;
using _Game.Components;

namespace _Game.Cards
{
    /// <summary>
    /// How long a card effect lasts.
    /// </summary>
    public enum CardEffectLifetime : byte
    {
        /// <summary>
        /// Until the run ends (global buffer, run-scoped).
        /// </summary>
        Run,

        /// <summary>
        /// Until the wave ends, or for <see cref="CardEffect.Duration"/> seconds (wave buffer).
        /// </summary>
        Wave,
    }

    public struct CardEffect
    {
        public TagFilter Filter;
        public CardEffectLifetime Lifetime;
        public StatType Stat;
        public ModifierType ModifierType;
        public float Value;
        public float Duration; // seconds; 0 = rest of wave
    }

    public class CardData
    {
        public int CardId { get; }
        public string Title { get; }
        public string Description { get; }
        public CardEffect[] Effects { get; }
        public ICardConstraint[] Constraints { get; }

        public CardData(int cardId, string title, string description, CardEffect[] effects, ICardConstraint[] constraints = null)
        {
            CardId = cardId;
            Title = title;
            Description = description;
            Effects = effects;
            Constraints = constraints ?? Array.Empty<ICardConstraint>();
        }
    }
}
