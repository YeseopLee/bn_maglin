using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Maglin.Cards;
using System.Collections;

namespace Maglin.UI
{
    /// <summary>
    /// 개별 카드 UI - 카드 정보 표시 및 hover 기능 제공 (드래그는 CardDraggable에서 처리)
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

        [Header("애니메이션 설정")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float animationDuration = 0.05f;

        [Header("카드 상태 색상")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.cyan;
        [SerializeField] private Color disabledColor = Color.gray;
        [SerializeField] private Color insufficientManaColor = Color.red;

        // 카드 데이터
        private Card associatedCard;
        private bool isSelected;

        private bool isInteractable = true;
        private bool hasInsufficientMana;

        // 스케일 관련
        private Vector3 originalScale;

        // hover z-order 관리
        private int originalSortOrder;
        private bool isHovering;

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

            // 원본 정렬 순서 저장
            originalSortOrder = transform.GetSiblingIndex();
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

            // 카드 일러스트 이미지 (Transform.Find를 사용하여 직접 찾기)
            var cardIllustration = transform.Find("CardIllustration")?.GetComponent<Image>();
            if (cardIllustration != null && cardData.Image != null)
            {
                cardIllustration.sprite = cardData.Image;
            }
            // 폴백: 기존 cardImage 필드 사용
            else if (cardImage != null && cardData.Image != null)
            {
                cardImage.sprite = cardData.Image;
            }

            // 속성 아이콘 (ElementType에 따른 처리 필요)
            if (elementIcon != null)
            {
                // ElementType에 따라 적절한 아이콘 스프라이트 설정
                // 추후 ElementType별 아이콘 매핑 시스템 구현 필요
            }

            // 카드 이름 (Transform.Find를 사용하여 직접 찾기)
            var cardNameTextComponent = transform.Find("CardName")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (cardNameTextComponent != null)
            {
                cardNameTextComponent.text = cardData.CardName;
            }
            // 폴백: 기존 cardNameText 필드 사용
            else if (cardNameText != null)
            {
                cardNameText.text = cardData.CardName;
            }

            // 데미지 (Transform.Find를 사용하여 직접 찾기)
            var damageTextComponent = transform.Find("CardDamage")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (damageTextComponent != null)
            {
                int currentDamage = associatedCard.CurrentDamage;
                damageTextComponent.text = currentDamage.ToString();
            }
            // 폴백: 기존 damageText 필드 사용
            else if (damageText != null)
            {
                int currentDamage = associatedCard.CurrentDamage;
                damageText.text = currentDamage.ToString();
            }

            // 마나 비용 (Transform.Find를 사용하여 직접 찾기)
            var manaCostTextComponent = transform.Find("CardCost")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (manaCostTextComponent != null)
            {
                int currentCost = associatedCard.CurrentManaCost;
                manaCostTextComponent.text = currentCost.ToString();
            }
            // 폴백: 기존 manaCostText 필드 사용
            else if (manaCostText != null)
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
            if (!isInteractable) return;

            OnCardClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) return;

            isHovering = true;

            if (highlightEffect != null)
                highlightEffect.SetActive(true);

            // 카드를 맨 앞으로 가져오기 (겹침 방지)
            BringToFront();

            AnimateScale(originalScale * hoverScale);
            OnCardHoverEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {

            isHovering = false;

            if (highlightEffect != null)
                highlightEffect.SetActive(false);

            // 원래 위치로 되돌리기
            RestoreOriginalOrder();

            AnimateScale(originalScale);
            OnCardHoverExit?.Invoke(this);
        }

        // 드래그 기능은 CardDraggable 컴포넌트에서 처리

        /// <summary>
        /// 카드를 맨 앞으로 가져오기 (hover 시 겹침 방지)
        /// </summary>
        private void BringToFront()
        {
            // Canvas 컴포넌트 추가하여 sorting order로 앞으로 가져오기
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
            }

            // GraphicRaycaster도 함께 추가 (UI 이벤트를 위해)
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            // 높은 sorting order로 설정하여 앞에 표시
            canvas.sortingOrder = 1000;
        }

        /// <summary>
        /// 원래 정렬 순서로 되돌리기
        /// </summary>
        private void RestoreOriginalOrder()
        {
            // Canvas와 GraphicRaycaster 제거하여 원래 상태로 복구
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Destroy(raycaster);
            }

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                Destroy(canvas);
            }
        }

        #endregion
    }
}