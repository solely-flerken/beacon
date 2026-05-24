using _Game.Cards;
using _Game.Components;
using _Game.Core;
using _Game.UI.CardDraft;
using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Owns the non-blocking card draft flow.
    /// Level-ups enqueue card offers into a pending queue.
    /// The player opens the drawer at will to pick; the game never pauses.
    /// </summary>
    public class CardDraftManager : MonoBehaviour
    {
        public static CardDraftManager Instance { get; private set; }

        [SerializeField] private CardDraftUI draftUI;

        private readonly DraftHistory _history = new();
        private readonly PendingCardQueue _queue = new();

        private EntityManager _em;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void OnEnable()
        {
            EventBus.OnLevelUp += OnLevelUp;
            draftUI.OnCardSelected += HandleCardSelected;
            draftUI.OnToggleRequested += ToggleDrawer;
        }

        private void OnDisable()
        {
            EventBus.OnLevelUp -= OnLevelUp;
            draftUI.OnCardSelected -= HandleCardSelected;
            draftUI.OnToggleRequested -= ToggleDrawer;
        }

        private void OnLevelUp(int _)
        {
            TryEnqueueCards();
        }

        private void TryEnqueueCards()
        {
            CardDraftContext ctx = BuildContext();
            CardData[] cards = CardLibrary.SelectCards(ctx, 3);
            _queue.Enqueue(cards);
            NotifyPendingChanged();
        }

        /// <summary>
        /// Called by input to open or close the card drawer.
        /// </summary>
        public void ToggleDrawer()
        {
            if (draftUI.IsVisible)
            {
                draftUI.Hide();
                return;
            }

            if (!_queue.HasPending) return;

            draftUI.ShowCards(_queue.Peek());
        }

        private void HandleCardSelected(int offerIndex)
        {
            CardData[] offer = _queue.Peek();
            if (offer == null || offerIndex < 0 || offerIndex >= offer.Length) return;

            CardData chosen = offer[offerIndex];

            CardModifierApplier.ApplyCard(chosen, _em);
            EventBus.FireCardDrafted(chosen.CardId);

            _history.Add(chosen.CardId);
            _queue.Dequeue();
            NotifyPendingChanged();

            if (_queue.HasPending)
            {
                draftUI.ShowCards(_queue.Peek());
            }
            else
            {
                draftUI.Hide();
            }
        }

        public void ResetHistory()
        {
            _history.Clear();
            _queue.Clear();
            NotifyPendingChanged();
        }

        private void NotifyPendingChanged()
        {
            draftUI.UpdateBadge(_queue.Count);
        } 

        private CardDraftContext BuildContext()
        {
            BeaconStats beaconStats = default;
            Health beaconHealth = default;
            
            using EntityQuery beaconQ = _em.CreateEntityQuery(typeof(BeaconStats), typeof(Health));
            if (!beaconQ.IsEmpty)
            {
                beaconStats = beaconQ.GetSingleton<BeaconStats>();
                beaconHealth = beaconQ.GetSingleton<Health>();
            }

            return new CardDraftContext
            {
                BeaconStats = beaconStats,
                BeaconHealth = beaconHealth,
                DraftedCardIds = _history.CardIds,
                EntityManager = _em
            };
        }
    }
}