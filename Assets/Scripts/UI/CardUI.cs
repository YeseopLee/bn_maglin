using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Maglin.Cards;
using System.Collections;

namespace Maglin.UI
{
    /// <summary>
    /// 개별 카드 UI - 카드 정보 표시 및 드래그 기능 제공
    /// </summary>
    public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                          IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Image cardImage;
        [SerializeField] private Image elementIcon;
        [SerializeField] private Text cardNameText;
        [SerializeField] private Text damageText;
        [SerializeField] private Text manaCostText;
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private GameObject selectionEffect;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("드래그 설정")]
        [SerializeField] private float dragScale = 1.2f;
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;

        [Header("카드 상태 색상")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.cyan;
        [SerializeField] private Color disabledColor = Color.gray;
        [SerializeField] private Color insufficientManaColor = Color.red;

        // 카드 데이터
        private Card associatedCard;
        private bool isSelected;
        private bool isDragging;
        private bool isInteractable = true;
        private bool hasInsufficientMana;

        // 드래그 관련
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Canvas dragCanvas;

        // 참조
        private HandCardUI parentHand;
        private CardSlotUI currentSlot;

        // 이벤트
        public System.Action<CardUI> OnCardClicked;
        public System.Action<CardUI> OnCardSelected;
        public System.Action<CardUI> OnCardDeselected;
        public System.Action<CardUI> OnCardHoverEnter;
        public System.Action<CardUI> OnCardHoverExit;

        #region Unity Events

        private void Awake()
        {
            originalScale = transform.localScale;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 카드 초기화
        /// </summary>
        public void Initialize(Card card, HandCardUI hand)
        {
            associatedCard = card;
            parentHand = hand;

            UpdateCardDisplay();
            UpdateInteractability();
        }

        /// <summary>
        /// 카드 표시 업데이트
        /// </summary>
        public void UpdateCardDisplay()
        {
            if (associatedCard == null) return;

            var cardData = associatedCard.CardData;

            // 카드 이미지
            if (cardImage != null && cardData.Image != null)
                cardImage.sprite = cardData.Image;

            // 속성 아이콘 (ElementType에 따른 처리 필요)
            if (elementIcon != null)
            {
                // ElementType에 따라 적절한 아이콘 스프라이트 설정
                // 추후 ElementType별 아이콘 매핑 시스템 구현 필요
            }

            // 카드 이름
            if (cardNameText != null)
                cardNameText.text = cardData.CardName;

            // 데미지 (효과값 반영)
            if (damageText != null)
            {
                int currentDamage = associatedCard.CurrentDamage;
                damageText.text = currentDamage.ToString();
            }

            // 마나 비용 (효과값 반영)
            if (manaCostText != null)
            {
                int currentCost = associatedCard.CurrentManaCost;
                manaCostText.text = currentCost.ToString();
            }

            UpdateCardColors();
        }

        /// <summary>
        /// 카드 선택 상태 설정
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (isSelected == selected) return;

            isSelected = selected;

            if (selectionEffect != null)
                selectionEffect.SetActive(selected);

            UpdateCardColors();

            if (selected)
                OnCardSelected?.Invoke(this);
            else
                OnCardDeselected?.Invoke(this);
        }

        /// <summary>
        /// 카드 상호작용 가능 여부 설정
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            UpdateInteractability();
        }

        /// <summary>
        /// 마나 부족 상태 설정
        /// </summary>
        public void SetInsufficientMana(bool insufficient)
        {
            hasInsufficientMana = insufficient;
            UpdateCardColors();
        }

        /// <summary>
        /// 카드가 슬롯에 배치되었을 때 호출
        /// </summary>
        public void OnCardPlacedInSlot(CardSlotUI slot)
        {
            currentSlot = slot;
            SetSelected(false);

            // 손패에서 제거 표시
            if (parentHand != null)
                parentHand.OnCardPlacedInSlot(this);
        }

        /// <summary>
        /// 카드가 슬롯에서 제거되었을 때 호출
        /// </summary>
        public void OnCardRemovedFromSlot()
        {
            currentSlot = null;

            // 손패로 복귀 표시
            if (parentHand != null)
                parentHand.OnCardReturnedFromSlot(this);
        }

        /// <summary>
        /// 카드 애니메이션 (스케일)
        /// </summary>
        public void AnimateScale(Vector3 targetScale, float duration = -1f)
        {
            if (duration < 0) duration = animationDuration;

            StopAllCoroutines();
            StartCoroutine(ScaleAnimation(targetScale, duration));
        }

        #endregion

        #region Properties

        public Card AssociatedCard => associatedCard;
        public bool IsSelected => isSelected;
        public bool IsDragging => isDragging;
        public bool IsInteractable => isInteractable;
        public CardSlotUI CurrentSlot => currentSlot;

        #endregion

        #region Private Methods

        /// <summary>
        /// 상호작용 가능 여부 업데이트
        /// </summary>
        private void UpdateInteractability()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = isInteractable;
                canvasGroup.blocksRaycasts = isInteractable;
            }

            UpdateCardColors();
        }

        /// <summary>
        /// 카드 색상 업데이트
        /// </summary>
        private void UpdateCardColors()
        {
            Color targetColor = normalColor;

            if (!isInteractable)
                targetColor = disabledColor;
            else if (hasInsufficientMana)
                targetColor = insufficientManaColor;
            else if (isSelected)
                targetColor = selectedColor;

            if (cardImage != null)
                cardImage.color = targetColor;
        }

        /// <summary>
        /// 스케일 애니메이션 코루틴
        /// </summary>
        private IEnumerator ScaleAnimation(Vector3 targetScale, float duration)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 부드러운 곡선 적용
                t = Mathf.SmoothStep(0f, 1f, t);

                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
        }

        #endregion

        #region Event Handlers

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable || isDragging) return;

            OnCardClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable || isDragging) return;

            if (highlightEffect != null)
                highlightEffect.SetActive(true);

            AnimateScale(originalScale * hoverScale);
            OnCardHoverEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;

            if (highlightEffect != null)
                highlightEffect.SetActive(false);

            AnimateScale(originalScale);
            OnCardHoverExit?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isInteractable) return;

            isDragging = true;

            // 드래그 준비
            originalPosition = transform.position;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            // 최상위 캔버스로 이동 (다른 UI 위에 표시되도록)
            dragCanvas = FindDragCanvas();
            if (dragCanvas != null)
                transform.SetParent(dragCanvas.transform, true);

            // 드래그 시각 효과
            AnimateScale(originalScale * dragScale);
            if (canvasGroup != null)
                canvasGroup.alpha = 0.8f;

            if (highlightEffect != null)
                highlightEffect.SetActive(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;

            // 슬롯에 드롭되지 않았다면 원래 위치로 복귀
            if (currentSlot == null)
            {
                transform.SetParent(originalParent, true);
                transform.SetSiblingIndex(originalSiblingIndex);
                transform.position = originalPosition;
            }

            // 시각 효과 복원
            AnimateScale(originalScale);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// 드래그용 캔버스 찾기
        /// </summary>
        private Canvas FindDragCanvas()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            Canvas topCanvas = null;
            int highestSortingOrder = int.MinValue;

            foreach (Canvas canvas in canvases)
            {
                if (canvas.sortingOrder > highestSortingOrder)
                {
                    highestSortingOrder = canvas.sortingOrder;
                    topCanvas = canvas;
                }
            }

            return topCanvas;
        }

        #endregion
    }
}