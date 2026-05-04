using System.Collections.Generic;
using _Game.Components;
using Unity.Entities;

namespace _Game.Cards
{
    /// <summary>
    /// Snapshot of game state passed to constraints during card draft evaluation.
    /// Populated once per draft on the main thread (simulation is paused).
    /// </summary>
    public class CardDraftContext
    {
        public IReadOnlyList<int> DraftedCardIds;
        public BeaconStats BeaconStats;
        public Health BeaconHealth;
        public EntityManager EntityManager;
    }

    /// <summary>
    /// Pluggable precondition that gates whether a card can be offered during a draft.
    /// </summary>
    public interface ICardConstraint
    {
        bool IsMet(CardDraftContext context);

        /// <summary>
        /// Human-readable description.
        /// </summary>
        string GetLabel();
    }
}
