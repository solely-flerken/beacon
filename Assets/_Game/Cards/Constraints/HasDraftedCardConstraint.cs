using System.Linq;

namespace _Game.Cards.Constraints
{
    /// <summary>
    /// Satisfied when a specific card has already been drafted in this run.
    /// </summary>
    public class HasDraftedCardConstraint : ICardConstraint
    {
        public int RequiredCardId;

        public bool IsMet(CardDraftContext ctx) => ctx.DraftedCardIds.Contains(RequiredCardId);

        public string GetLabel() => $"Requires card #{RequiredCardId} already drafted";
    }
}