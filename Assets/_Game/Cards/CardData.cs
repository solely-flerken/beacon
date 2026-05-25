using System;
using _Game.Components;

namespace _Game.Cards
{
    public enum CardTargetType : byte
    {
        Beacon,
        BeaconWeapons,
        WeaponsOfType,
        EnemiesOfType,
        WaveEnemies,
        WaveWeapons,
    }

    public struct CardEffect
    {
        public CardTargetType TargetType;
        public RoleMask EnemyType; // relevant for EnemiesOfType and WaveEnemies
        public WeaponType WeaponType; // relevant for WeaponsOfType
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