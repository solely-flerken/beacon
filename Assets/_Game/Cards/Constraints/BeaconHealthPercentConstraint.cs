namespace _Game.Cards.Constraints
{
    /// <summary>
    /// Satisfied when beacon's current HP is at or above a percentage threshold.
    /// </summary>
    public class BeaconHealthPercentConstraint : ICardConstraint
    {
        public float MinHealthPercent = 0.5f;

        public bool IsMet(CardDraftContext ctx)
        {
            if (ctx.BeaconStats.MaxHp <= 0f) return false;
            return ctx.BeaconStats.CurrentHp / ctx.BeaconStats.MaxHp >= MinHealthPercent;
        }

        public string GetLabel() => $"Beacon health >= {MinHealthPercent:P0}";
    }
}