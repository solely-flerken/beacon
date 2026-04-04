using System.Collections.Generic;

namespace _Game.Cards
{
    /// <summary>
    /// Tracks which card IDs have been drafted in the current run.
    /// Lives on CardDraftManager. Reset at run start.
    /// </summary>
    public class DraftHistory
    {
        private readonly List<int> _cardIds = new();

        public IReadOnlyList<int> CardIds => _cardIds;

        public void Add(int cardId) => _cardIds.Add(cardId);
        
        public void Clear() => _cardIds.Clear();
    }
}
