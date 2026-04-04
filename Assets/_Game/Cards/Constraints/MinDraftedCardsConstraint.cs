namespace _Game.Cards.Constraints
{
    /// <summary>
    /// Satisfied when the player has drafted at least N cards total in this run.
    /// </summary>
    public class MinDraftedCardsConstraint : ICardConstraint
    {
        public int MinCount = 1;

        public bool IsMet(CardDraftContext ctx) => ctx.DraftedCardIds.Count >= MinCount;

        public string GetLabel() => $"Requires {MinCount}+ cards already drafted";
    }
}
