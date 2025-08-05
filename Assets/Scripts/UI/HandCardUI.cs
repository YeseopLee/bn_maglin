using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Maglin.Cards;
using Maglin.Core;
using Maglin.Player;
using System.Collections;

namespace Maglin.UI
{
    /// <summary>
    /// 손패 UI - 플레이어가 보유한 카드들을 표시하고 관리
    /// </summary>
    public class HandCardUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private ContentSizeFitter contentSizeFitter;

        [Header("카드 프리팹")]
        [SerializeField] private GameObject cardUIPrefab;

        [Header("레이아웃 설정")]
        [SerializeField] private int maxCardsPerRow = 5;
        [SerializeField] private float cardSpacing = 10f;
        [SerializeField] private Vector2 cardSize = new Vector2(100, 140);
        [SerializeField] private RectOffset containerPadding = new RectOffset(10, 10, 10, 10);

        [Header("애니메이션 설정")]
        [SerializeField] private float cardAnimationDuration = 0.3f;
        [SerializeField] private AnimationCurve cardAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 카드 관리
        private List<CardUI> cardUIList = new List<CardUI>();
        private List<Card> currentHand = new List<Card>();
        private List<CardUI> cardsInSlots = new List<CardUI>();

        // 선택 관리
        private CardUI selectedCard;
        private bool allowMultipleSelection = false;
        private List<CardUI> selectedCards = new List<CardUI>();

        // 참조
        private PlayerManager playerManager;
        private CardManager cardManager;

        // 이벤트
        public System.Action<Card> OnCardSelected;
        public System.Action<Card> OnCardDeselected;
        public System.Action<List<Card>> OnHandUpdated;
        public System.Action<CardUI> OnCardHovered;

        #region Unity Events

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeReferences();
            SetupLayout();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 손패 업데이트
        /// </summary>
        public void UpdateHand(List<Card> newHand)
        {
            currentHand = new List<Card>(newHand);
            RefreshCardDisplay();
            OnHandUpdated?.Invoke(currentHand);
        }

        /// <summary>
        /// 카드 추가
        /// </summary>
        public void AddCard(Card card)
        {
            if (card == null) return;

            currentHand.Add(card);
            CreateCardUI(card);
            UpdateLayout();
            OnHandUpdated?.Invoke(currentHand);
        }

        /// <summary>
        /// 카드 제거
        /// </summary>
        public void RemoveCard(Card card)
        {
            if (card == null) return;

            currentHand.Remove(card);
            RemoveCardUI(card);
            UpdateLayout();
            OnHandUpdated?.Invoke(currentHand);
        }

        /// <summary>
        /// 모든 카드 클리어
        /// </summary>
        public void ClearHand()
        {
            currentHand.Clear();
            ClearAllCardUI();
            OnHandUpdated?.Invoke(currentHand);
        }

        /// <summary>
        /// 카드 선택
        /// </summary>
        public void SelectCard(CardUI cardUI)
        {
            if (cardUI == null || !cardUIList.Contains(cardUI)) return;

            if (allowMultipleSelection)
            {
                if (selectedCards.Contains(cardUI))
                {
                    DeselectCard(cardUI);
                }
                else
                {
                    selectedCards.Add(cardUI);
                    cardUI.SetSelected(true);
                    OnCardSelected?.Invoke(cardUI.AssociatedCard);
                }
            }
            else
            {
                // 단일 선택 모드
                if (selectedCard != null)
                    DeselectCard(selectedCard);

                selectedCard = cardUI;
                cardUI.SetSelected(true);
                OnCardSelected?.Invoke(cardUI.AssociatedCard);
            }
        }

        /// <summary>
        /// 카드 선택 해제
        /// </summary>
        public void DeselectCard(CardUI cardUI)
        {
            if (cardUI == null) return;

            if (allowMultipleSelection)
            {
                selectedCards.Remove(cardUI);
            }
            else if (selectedCard == cardUI)
            {
                selectedCard = null;
            }

            cardUI.SetSelected(false);
            OnCardDeselected?.Invoke(cardUI.AssociatedCard);
        }

        /// <summary>
        /// 모든 카드 선택 해제
        /// </summary>
        public void DeselectAllCards()
        {
            if (allowMultipleSelection)
            {
                foreach (var card in selectedCards.ToList())
                {
                    DeselectCard(card);
                }
            }
            else if (selectedCard != null)
            {
                DeselectCard(selectedCard);
            }
        }

        /// <summary>
        /// 카드가 슬롯에 배치될 때 호출
        /// </summary>
        public void OnCardPlacedInSlot(CardUI cardUI)
        {
            if (!cardsInSlots.Contains(cardUI))
                cardsInSlots.Add(cardUI);

            // 손패에서 시각적으로 숨김
            cardUI.gameObject.SetActive(false);
            UpdateLayout();
        }

        /// <summary>
        /// 카드가 슬롯에서 돌아올 때 호출
        /// </summary>
        public void OnCardReturnedFromSlot(CardUI cardUI)
        {
            cardsInSlots.Remove(cardUI);

            // 손패에서 다시 표시
            cardUI.gameObject.SetActive(true);
            UpdateLayout();
        }

        /// <summary>
        /// 마나 상태 업데이트
        /// </summary>
        public void UpdateManaStatus()
        {
            if (playerManager == null) return;

            int currentMana = playerManager.CurrentMana;

            foreach (var cardUI in cardUIList)
            {
                if (cardUI.AssociatedCard != null)
                {
                    int cardCost = cardUI.AssociatedCard.CurrentManaCost;
                    cardUI.SetInsufficientMana(currentMana < cardCost);
                }
            }
        }

        /// <summary>
        /// 다중 선택 모드 설정
        /// </summary>
        public void SetMultipleSelectionMode(bool allow)
        {
            allowMultipleSelection = allow;
            if (!allow)
                DeselectAllCards();
        }

        #endregion

        #region Properties

        public List<Card> CurrentHand => new List<Card>(currentHand);
        public List<Card> SelectedCards
        {
            get
            {
                if (allowMultipleSelection)
                    return selectedCards.Select(ui => ui.AssociatedCard).ToList();
                else if (selectedCard != null)
                    return new List<Card> { selectedCard.AssociatedCard };
                else
                    return new List<Card>();
            }
        }
        public bool HasSelectedCards => SelectedCards.Count > 0;
        public int HandCount => currentHand.Count;

        #endregion

        #region Private Methods

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            if (cardContainer == null)
                cardContainer = transform;

            if (gridLayout == null)
                gridLayout = GetComponent<GridLayoutGroup>();

            if (scrollRect == null)
                scrollRect = GetComponentInParent<ScrollRect>();

            if (contentSizeFitter == null)
                contentSizeFitter = GetComponent<ContentSizeFitter>();
        }

        /// <summary>
        /// 참조 초기화
        /// </summary>
        private void InitializeReferences()
        {
            playerManager = PlayerManager.Instance;
            cardManager = CardManager.Instance;
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (playerManager != null)
            {
                PlayerManager.OnManaChanged += OnManaChanged;
            }

            if (cardManager != null)
            {
                CardManager.OnHandCardsChanged += UpdateHand;
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (playerManager != null)
            {
                PlayerManager.OnManaChanged -= OnManaChanged;
            }

            if (cardManager != null)
            {
                CardManager.OnHandCardsChanged -= UpdateHand;
            }
        }

        /// <summary>
        /// 레이아웃 설정
        /// </summary>
        private void SetupLayout()
        {
            if (gridLayout != null)
            {
                gridLayout.cellSize = cardSize;
                gridLayout.spacing = Vector2.one * cardSpacing;
                gridLayout.padding = containerPadding;
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = maxCardsPerRow;
            }

            if (contentSizeFitter != null)
            {
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        /// <summary>
        /// 카드 표시 새로고침
        /// </summary>
        private void RefreshCardDisplay()
        {
            ClearAllCardUI();

            foreach (var card in currentHand)
            {
                CreateCardUI(card);
            }

            UpdateLayout();
        }

        /// <summary>
        /// 카드 UI 생성
        /// </summary>
        private void CreateCardUI(Card card)
        {
            if (cardUIPrefab == null || card == null) return;

            GameObject cardObj = Instantiate(cardUIPrefab, cardContainer);
            CardUI cardUI = cardObj.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.Initialize(card, this);
                cardUI.OnCardClicked += OnCardUIClicked;
                cardUI.OnCardHoverEnter += OnCardUIHoverEnter;
                cardUI.OnCardHoverExit += OnCardUIHoverExit;

                cardUIList.Add(cardUI);
            }
        }

        /// <summary>
        /// 카드 UI 제거
        /// </summary>
        private void RemoveCardUI(Card card)
        {
            CardUI targetUI = cardUIList.FirstOrDefault(ui => ui.AssociatedCard == card);
            if (targetUI != null)
            {
                cardUIList.Remove(targetUI);
                selectedCards.Remove(targetUI);

                if (selectedCard == targetUI)
                    selectedCard = null;

                if (targetUI.gameObject != null)
                    Destroy(targetUI.gameObject);
            }
        }

        /// <summary>
        /// 모든 카드 UI 제거
        /// </summary>
        private void ClearAllCardUI()
        {
            foreach (var cardUI in cardUIList)
            {
                if (cardUI != null && cardUI.gameObject != null)
                    Destroy(cardUI.gameObject);
            }

            cardUIList.Clear();
            selectedCards.Clear();
            selectedCard = null;
            cardsInSlots.Clear();
        }

        /// <summary>
        /// 레이아웃 업데이트
        /// </summary>
        private void UpdateLayout()
        {
            if (gridLayout != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridLayout.GetComponent<RectTransform>());
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 카드 UI 클릭 이벤트
        /// </summary>
        private void OnCardUIClicked(CardUI cardUI)
        {
            SelectCard(cardUI);
        }

        /// <summary>
        /// 카드 UI 호버 시작 이벤트
        /// </summary>
        private void OnCardUIHoverEnter(CardUI cardUI)
        {
            OnCardHovered?.Invoke(cardUI);
        }

        /// <summary>
        /// 카드 UI 호버 종료 이벤트
        /// </summary>
        private void OnCardUIHoverExit(CardUI cardUI)
        {
            // 필요시 구현
        }

        /// <summary>
        /// 마나 변경 이벤트
        /// </summary>
        private void OnManaChanged(int currentMana, int maxMana)
        {
            UpdateManaStatus();
        }

        #endregion
    }
}