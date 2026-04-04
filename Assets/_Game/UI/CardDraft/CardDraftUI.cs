using System;
using _Game.Cards;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Game.UI.CardDraft
{
    public class CardDraftUI : MonoBehaviour
    {
        private const int CardCount = 3;

        [SerializeField] private UIDocument uiDocument;

        /// <summary>
        /// Raised when the player taps a card. Argument is the offer index (0–2).
        /// </summary>
        public event Action<int> OnCardSelected;

        private VisualElement _overlay;
        private VisualElement[] _cardPanels;
        private Label[] _cardTitles;
        private Label[] _cardDescriptions;

        private void Awake()
        {
            VisualElement root = uiDocument.rootVisualElement;

            _overlay = root.Q<VisualElement>("draft-overlay");
            _cardPanels = new VisualElement[CardCount];
            _cardTitles = new Label[CardCount];
            _cardDescriptions = new Label[CardCount];

            for (int i = 0; i < CardCount; i++)
            {
                int captured = i;
                _cardPanels[i] = root.Q<VisualElement>($"card-{i}");
                _cardTitles[i] = root.Q<Label>($"card-title-{i}");
                _cardDescriptions[i] = root.Q<Label>($"card-description-{i}");
                _cardPanels[i].RegisterCallback<ClickEvent>(_ => OnCardSelected?.Invoke(captured));
            }

            Hide();
        }

        public void ShowCards(CardData[] cards)
        {
            for (int i = 0; i < CardCount; i++)
            {
                if (i < cards.Length)
                {
                    _cardTitles[i].text = cards[i].Title;
                    _cardDescriptions[i].text = cards[i].Description;
                    _cardPanels[i].style.display = DisplayStyle.Flex;
                }
                else
                {
                    _cardPanels[i].style.display = DisplayStyle.None;
                }
            }

            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide() => _overlay.style.display = DisplayStyle.None;
    }
}