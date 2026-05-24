using System.Collections.Generic;
using _Game.Cards;

namespace _Game.Gameplay
{
    public class PendingCardQueue
    {
        private readonly Queue<CardData[]> _queue = new();

        public int Count => _queue.Count;
        public bool HasPending => _queue.Count > 0;

        public void Enqueue(CardData[] cardSet) => _queue.Enqueue(cardSet);

        public CardData[] Peek() => _queue.Count > 0 ? _queue.Peek() : null;

        public CardData[] Dequeue() => _queue.Dequeue();

        public void Clear() => _queue.Clear();
    }
}