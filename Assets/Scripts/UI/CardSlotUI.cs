using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Maglin.Cards;

namespace Maglin.UI
{
    /// <summary>
    /// 카드 슬롯 UI - 드래그 앤 드롭으로 카드를 배치할 수 있는 슬롯
    /// </summary>
    public class CardSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Image slotImage;
        [SerializeField] private Image cardImage;
        [SerializeField] private Text cardNameText;
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private GameObject emptySlotIndicator;

        [Header("슬롯 설정")]
        [SerializeField] private int slotIndex;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;
        [SerializeField] private Color invalidColor = Color.red;

        // 현재 배치된 카드
        private Card placedCard;
        private bool isHighlighted;
        private bool canAcceptCard = true;

        // 이벤트
        public System.Action<CardSlotUI, Card> OnCardPlaced;
        public System.Action<CardSlotUI> OnCardRemoved;
        public System.Action<CardSlotUI> OnSlotHighlighted;

        #region Unity Events

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        public void Initialize()
        {
            UpdateSlotDisplay();
            SetHighlight(false);
        }

        /// <summary>
        /// 카드 배치
        /// </summary>
        public bool PlaceCard(Card card)
        {
            if (!CanPlaceCard(card))
                return false;

            // 기존 카드가 있다면 제거
            if (placedCard != null)
                RemoveCard();

            placedCard = card;
            UpdateSlotDisplay();

            OnCardPlaced?.Invoke(this, card);
            return true;
        }

        /// <summary>
        /// 카드 제거
        /// </summary>
        public Card RemoveCard()
        {
            Card removedCard = placedCard;
            placedCard = null;
            UpdateSlotDisplay();

            OnCardRemoved?.Invoke(this);
            return removedCard;
        }

        /// <summary>
        /// 카드 배치 가능 여부 확인
        /// </summary>
        public bool CanPlaceCard(Card card)
        {
            if (!canAcceptCard || card == null)
                return false;

            // 추가 조건들 (예: 카드 타입 제한 등)
            return true;
        }

        /// <summary>
        /// 슬롯 활성화/비활성화
        /// </summary>
        public void SetAcceptCard(bool accept)
        {
            canAcceptCard = accept;
            UpdateSlotDisplay();
        }

        /// <summary>
        /// 하이라이트 설정
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            if (highlightEffect != null)
                highlightEffect.SetActive(highlight);

            if (highlight)
                OnSlotHighlighted?.Invoke(this);
        }

        #endregion

        #region Properties

        public Card PlacedCard => placedCard;
        public int SlotIndex => slotIndex;
        public bool HasCard => placedCard != null;
        public bool CanAcceptCard => canAcceptCard;

        #endregion

        #region Private Methods

        /// <summary>
        /// 슬롯 표시 업데이트
        /// </summary>
        private void UpdateSlotDisplay()
        {
            if (placedCard != null)
            {
                // 카드가 배치된 상태
                if (cardImage != null)
                {
                    cardImage.gameObject.SetActive(true);
                    cardImage.sprite = placedCard.CardData.Image;
                }

                if (cardNameText != null)
                {
                    cardNameText.gameObject.SetActive(true);
                    cardNameText.text = placedCard.CardData.CardName;
                }

                if (emptySlotIndicator != null)
                    emptySlotIndicator.SetActive(false);
            }
            else
            {
                // 빈 슬롯 상태
                if (cardImage != null)
                    cardImage.gameObject.SetActive(false);

                if (cardNameText != null)
                    cardNameText.gameObject.SetActive(false);

                if (emptySlotIndicator != null)
                    emptySlotIndicator.SetActive(true);
            }

            // 슬롯 색상 업데이트
            Color targetColor = normalColor;
            if (!canAcceptCard)
                targetColor = invalidColor;
            else if (isHighlighted)
                targetColor = highlightColor;

            if (slotImage != null)
                slotImage.color = targetColor;
        }

        #endregion

        #region Drag & Drop Handlers

        public void OnDrop(PointerEventData eventData)
        {
            // 드래그된 카드 UI 가져오기
            CardUI draggedCard = eventData.pointerDrag?.GetComponent<CardUI>();
            if (draggedCard != null && draggedCard.AssociatedCard != null)
            {
                if (PlaceCard(draggedCard.AssociatedCard))
                {
                    // 성공적으로 배치됨
                    draggedCard.OnCardPlacedInSlot(this);
                }
            }

            SetHighlight(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 드래그 중인 카드가 있다면 하이라이트
            if (eventData.pointerDrag != null)
            {
                CardUI draggedCard = eventData.pointerDrag.GetComponent<CardUI>();
                if (draggedCard != null && CanPlaceCard(draggedCard.AssociatedCard))
                {
                    SetHighlight(true);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
        }

        #endregion
    }
}