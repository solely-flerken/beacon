using _Game.Cards;
using _Game.Core;
using _Game.Components;
using _Game.UI.CardDraft;
using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Owns the card draft flow. Drives CardDraftUI directly.
    /// </summary>
    public class CardDraftManager : MonoBehaviour
    {
        public static CardDraftManager Instance { get; private set; }

        [SerializeField] private CardDraftUI draftUI;

        private readonly DraftHistory _history = new();
        private CardData[] _currentOffer;
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
        }

        private void OnDisable()
        {
            EventBus.OnLevelUp -= OnLevelUp;
            draftUI.OnCardSelected -= HandleCardSelected;
        } 

        private void OnLevelUp(int _)
        {
            if (!GameStateMachine.Instance.TransitionTo(GameState.CardDraft)) return;
            StartDraft(3);
        }

        private void StartDraft(int numCards)
        {
            CardDraftContext ctx = BuildContext();
            _currentOffer = CardLibrary.SelectCards(ctx, numCards);

            draftUI.ShowCards(_currentOffer);
        }

        private void HandleCardSelected(int offerIndex)
        {
            if (_currentOffer == null || offerIndex < 0 || offerIndex >= _currentOffer.Length) return;

            CardData chosen = _currentOffer[offerIndex];
            _currentOffer = null; // prevent double-selection

            CardModifierApplier.ApplyCard(chosen, _em);
            _history.Add(chosen.CardId);

            EventBus.FireCardDrafted(chosen.CardId);

            draftUI.Hide();
            GameStateMachine.Instance.TransitionToPrevious();
        }

        public void ResetHistory() => _history.Clear();

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
