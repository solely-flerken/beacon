using System;
using _Game.Components;

namespace _Game.Cards
{
    public enum CardTargetType : byte
    {
        Beacon,
        GlobalWeapons,
        EnemiesOfType,
    }

    public struct CardEffect
    {
        public CardTargetType TargetType;
        public EnemyType EnemyType; // only relevant when TargetType == EnemiesOfType
        public StatType Stat;
        public ModifierType ModifierType;
        public float Value;
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