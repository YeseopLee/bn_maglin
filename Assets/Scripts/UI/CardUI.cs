using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Maglin.Cards;
using System.Collections;
using System.Reflection;
using TMPro;

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
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private TextMeshProUGUI manaCostText;
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

        // hover UI 관련
        private GameObject hoverUIInstance;

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

        private void OnDestroy()
        {
            // hover UI가 남아있다면 정리
            HideHoverUI();
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

            // 스케일업 대신 hover UI 표시
            ShowHoverUI();
            OnCardHoverEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;

            if (highlightEffect != null)
                highlightEffect.SetActive(false);

            // hover UI 숨기기
            HideHoverUI();
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

        /// <summary>
        /// hover UI 표시
        /// </summary>
        private void ShowHoverUI()
        {
            // 이미 hover UI가 있으면 제거
            if (hoverUIInstance != null)
            {
                HideHoverUI();
            }

            // 카드 데이터 먼저 확인
            Card cardData = GetCardData();
            if (cardData == null) return;

            // BattleUIManager에서 CardHoverUIPrefab 가져오기
            var battleUIManager = Maglin.Battle.BattleUIManager.Instance;
            if (battleUIManager == null) return;

            var hoverPrefab = GetCardHoverPrefab(battleUIManager);
            if (hoverPrefab == null) return;

            // 부모 Canvas 찾기 (보통 현재 카드의 최상위 Canvas)
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            // hover UI 생성
            hoverUIInstance = Instantiate(hoverPrefab, parentCanvas.transform);

            // 현재 카드와 같은 위치에 배치 (바로 그 자리에서 크게 표시)
            var rectTransform = hoverUIInstance.GetComponent<RectTransform>();
            var currentRectTransform = GetComponent<RectTransform>();

            if (rectTransform != null && currentRectTransform != null)
            {
                // UI 좌표계에서 현재 카드의 위치를 그대로 사용
                rectTransform.position = currentRectTransform.position;
                rectTransform.localScale = Vector3.one;

                // 높은 sorting order로 설정하여 맨 앞에 표시
                var canvas = hoverUIInstance.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = hoverUIInstance.AddComponent<Canvas>();
                    canvas.overrideSorting = true;
                }
                canvas.sortingOrder = 2000; // 매우 높은 값으로 설정

                // GraphicRaycaster는 추가하지 않음 (마우스 이벤트가 원본 카드로 전달되어야 함)
            }

            // hover UI에 카드 정보 설정
            UpdateHoverUIInfo(hoverUIInstance, cardData);
        }

        /// <summary>
        /// hover UI 숨기기
        /// </summary>
        private void HideHoverUI()
        {
            if (hoverUIInstance != null)
            {
                Destroy(hoverUIInstance);
                hoverUIInstance = null;
            }
        }

        /// <summary>
        /// BattleUIManager 또는 ShopUIManager에서 CardHoverUIPrefab 가져오기 (리플렉션 사용)
        /// </summary>
        private GameObject GetCardHoverPrefab(Maglin.Battle.BattleUIManager battleUIManager)
        {
            // 먼저 BattleUIManager에서 시도
            var battleField = typeof(Maglin.Battle.BattleUIManager).GetField("cardHoverUIPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var hoverPrefab = battleField?.GetValue(battleUIManager) as GameObject;
            if (hoverPrefab != null)
            {
                return hoverPrefab;
            }

            // BattleUIManager에서 찾지 못했다면 ShopUIManager에서 시도
            var shopUIManager = Maglin.Shop.ShopUIManager.Instance;
            if (shopUIManager != null)
            {
                var shopField = typeof(Maglin.Shop.ShopUIManager).GetField("cardHoverUIPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                hoverPrefab = shopField?.GetValue(shopUIManager) as GameObject;
                if (hoverPrefab != null)
                {
                    return hoverPrefab;
                }
            }

            return null;
        }

        /// <summary>
        /// 카드 데이터 가져오기 (associatedCard 또는 CardUIData에서)
        /// </summary>
        private Card GetCardData()
        {
            // 먼저 associatedCard 확인 (CardUI.Initialize에서 설정된 경우)
            if (associatedCard != null)
            {
                return associatedCard;
            }

            // CardUIData 컴포넌트에서 확인 (BattleUIManager에서 생성된 경우)
            var cardUIData = GetComponent<Maglin.Battle.CardUIData>();
            if (cardUIData != null && cardUIData.CardInstance != null)
            {
                return cardUIData.CardInstance;
            }

            return null;
        }

        /// <summary>
        /// hover UI에 카드 정보 업데이트
        /// </summary>
        private void UpdateHoverUIInfo(GameObject hoverUI, Card card)
        {
            if (hoverUI == null || card == null) return;

            var cardData = card.CardData;

            // CardHoverUIPrefab은 CardUIPrefab과 구조가 동일하므로 같은 방식으로 업데이트

            // 카드 일러스트 이미지
            var cardIllustration = hoverUI.transform.Find("CardIllustration")?.GetComponent<Image>();
            if (cardIllustration != null && cardData.Image != null)
            {
                cardIllustration.sprite = cardData.Image;
            }

            // 카드 이름
            var cardNameTextComponent = hoverUI.transform.Find("CardName")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (cardNameTextComponent != null)
            {
                cardNameTextComponent.text = cardData.CardName;
            }

            // 데미지
            var damageTextComponent = hoverUI.transform.Find("CardDamage")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (damageTextComponent != null)
            {
                int currentDamage = card.CurrentDamage;
                damageTextComponent.text = currentDamage.ToString();
            }

            // 마나 비용 (BattleUIManager의 형식과 맞춤)
            var manaCostTextComponent = hoverUI.transform.Find("CardCost")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (manaCostTextComponent != null)
            {
                int currentCost = card.CurrentManaCost;
                manaCostTextComponent.text = currentCost.ToString(); // "비용: " 제거하고 숫자만
            }

            // 카드 설명 (CardDescription이 있다면)
            var cardDescComponent = hoverUI.transform.Find("CardDescription")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (cardDescComponent != null)
            {
                cardDescComponent.text = card.Description;
            }

            // 카드 정보 (ElementType과 CardType)
            var cardInfoComponent = hoverUI.transform.Find("CardInfo")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (cardInfoComponent != null)
            {
                cardInfoComponent.text = $"{card.Element} | {card.Type}";
            }

            // 디버그 로그
            Debug.Log($"[CardUI] Hover UI 정보 업데이트: {card.CardName} (데미지: {card.CurrentDamage}, 비용: {card.CurrentManaCost})");
        }

        #endregion
    }
}