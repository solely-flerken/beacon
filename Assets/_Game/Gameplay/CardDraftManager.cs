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
        private EntityQuery _saturatedOrbQuery;

        // Post-wave aggregation: level-ups during post-wave orb collection are queued and
        // drained one-by-one after all orbs have been absorbed.
        private bool _postWaveQueuing;
        private int _pendingLevelUps;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _saturatedOrbQuery = _em.CreateEntityQuery(typeof(SaturatedTag));
        }
        
        private void OnEnable()
        {
            EventBus.OnLevelUp += OnLevelUp;
            EventBus.OnWaveCleared += OnWaveCleared;
            draftUI.OnCardSelected += HandleCardSelected;
        }

        private void OnDisable()
        {
            EventBus.OnLevelUp -= OnLevelUp;
            EventBus.OnWaveCleared -= OnWaveCleared;
            draftUI.OnCardSelected -= HandleCardSelected;
        } 

        // TODO: Refactor - can we remove the update loop entirely?
        //       Could be event-driven but would require additional ECS coupling
        private void Update()
        {
            if (!_postWaveQueuing) return;
            if (!_saturatedOrbQuery.IsEmpty) return;

            _postWaveQueuing = false;
            DrainNextPending();
        }

        private void OnWaveCleared(int _)
        {
            _postWaveQueuing = true;
            _pendingLevelUps = 0;
        }

        private void OnLevelUp(int _)
        {
            if (_postWaveQueuing)
            {
                _pendingLevelUps++;
                return;
            }

            TryStartDraft();
        }

        private void DrainNextPending()
        {
            if (_pendingLevelUps <= 0) return;
            
            _pendingLevelUps--;
            TryStartDraft();
        }

        private void TryStartDraft()
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

            if (GameStateMachine.Instance.CurrentState == GameState.WaveEnd && _pendingLevelUps > 0)
            {
                DrainNextPending();
            }
        }

        public void ResetHistory()
        {
            _history.Clear();
            
            _pendingLevelUps = 0;
            _postWaveQueuing = false;
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
