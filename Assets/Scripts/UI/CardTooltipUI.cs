using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Maglin.Cards;

namespace Maglin.UI
{
    /// <summary>
    /// 카드 툴팁 UI - 카드 호버 시 세부 정보 표시
    /// </summary>
    public class CardTooltipUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image elementIcon;
        [SerializeField] private Text cardNameText;
        [SerializeField] private Text cardTypeText;
        [SerializeField] private Text damageText;
        [SerializeField] private Text manaCostText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text priceText;

        [Header("조합 정보")]
        [SerializeField] private GameObject combinationPanel;
        [SerializeField] private Text combinationText;
        [SerializeField] private Transform combinationContainer;
        [SerializeField] private GameObject combinationItemPrefab;

        [Header("애니메이션 설정")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;
        [SerializeField] private Vector2 offset = new Vector2(10, 10);
        [SerializeField] private bool followMouse = true;

        [Header("위치 조정")]
        [SerializeField] private bool adjustPositionToFitScreen = true;
        [SerializeField] private Vector2 screenMargin = new Vector2(20, 20);

        // 상태 관리
        private Card currentCard;
        private bool isVisible = false;
        private Coroutine fadeCoroutine;
        private RectTransform canvasRect;
        private Camera uiCamera;

        // 싱글톤
        private static CardTooltipUI instance;
        public static CardTooltipUI Instance => instance;

        #region Unity Events

        private void Awake()
        {
            // 싱글톤 설정
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeComponents();
        }

        private void Start()
        {
            InitializeCanvas();
            Hide(true); // 즉시 숨김
        }

        private void Update()
        {
            if (isVisible && followMouse)
            {
                UpdatePosition(Input.mousePosition);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 카드 툴팁 표시
        /// </summary>
        public void ShowCard(Card card, Vector3 worldPosition = default)
        {
            if (card == null) return;

            currentCard = card;
            UpdateTooltipContent();

            Vector3 screenPosition = worldPosition == default ? Input.mousePosition : worldPosition;
            UpdatePosition(screenPosition);

            Show();
        }

        /// <summary>
        /// 카드 UI를 기반으로 툴팁 표시
        /// </summary>
        public void ShowCardUI(CardUI cardUI)
        {
            if (cardUI == null || cardUI.AssociatedCard == null) return;

            ShowCard(cardUI.AssociatedCard, cardUI.transform.position);
        }

        /// <summary>
        /// 툴팁 숨김
        /// </summary>
        public void Hide(bool immediate = false)
        {
            if (!isVisible && !immediate) return;

            isVisible = false;
            currentCard = null;

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (immediate)
            {
                canvasGroup.alpha = 0f;
                gameObject.SetActive(false);
            }
            else
            {
                fadeCoroutine = StartCoroutine(FadeOut());
            }
        }

        /// <summary>
        /// 위치 업데이트
        /// </summary>
        public void UpdatePosition(Vector3 screenPosition)
        {
            if (tooltipRect == null) return;

            Vector2 localPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, uiCamera, out localPosition);

            // 오프셋 적용
            localPosition += offset;

            // 화면 경계 조정
            if (adjustPositionToFitScreen)
            {
                localPosition = AdjustPositionToFitScreen(localPosition);
            }

            tooltipRect.anchoredPosition = localPosition;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (tooltipRect == null)
                tooltipRect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 캔버스 초기화
        /// </summary>
        private void InitializeCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
                uiCamera = canvas.worldCamera;
            }
        }

        /// <summary>
        /// 툴팁 표시
        /// </summary>
        private void Show()
        {
            if (isVisible) return;

            isVisible = true;
            gameObject.SetActive(true);

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 툴팁 내용 업데이트
        /// </summary>
        private void UpdateTooltipContent()
        {
            if (currentCard == null) return;

            var cardData = currentCard.CardData;

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

            // 카드 타입
            if (cardTypeText != null)
                cardTypeText.text = GetCardTypeString(cardData.Type);

            // 데미지 (현재 효과값 반영)
            if (damageText != null)
            {
                int currentDamage = currentCard.CurrentDamage;
                damageText.text = $"데미지: {currentDamage}";

                // 기본값과 다르면 색상 변경
                if (currentDamage != cardData.BaseDamage)
                {
                    damageText.color = currentDamage > cardData.BaseDamage ? Color.green : Color.red;
                }
                else
                {
                    damageText.color = Color.white;
                }
            }

            // 마나 비용 (현재 비용 반영)
            if (manaCostText != null)
            {
                int currentCost = currentCard.CurrentManaCost;
                manaCostText.text = $"마나: {currentCost}";

                // 기본값과 다르면 색상 변경
                if (currentCost != cardData.ManaCost)
                {
                    manaCostText.color = currentCost < cardData.ManaCost ? Color.green : Color.red;
                }
                else
                {
                    manaCostText.color = Color.white;
                }
            }

            // 설명
            if (descriptionText != null)
                descriptionText.text = cardData.Description;

            // 가격
            if (priceText != null)
            {
                int currentPrice = currentCard.CurrentPrice;
                priceText.text = $"가격: {currentPrice}G";
            }

            // 조합 정보 업데이트
            UpdateCombinationInfo();
        }

        /// <summary>
        /// 조합 정보 업데이트
        /// </summary>
        private void UpdateCombinationInfo()
        {
            if (combinationPanel == null) return;

            var cardData = currentCard.CardData;
            bool hasComboInfo = cardData.RequiredCombinations != null && cardData.RequiredCombinations.Length > 0;

            combinationPanel.SetActive(hasComboInfo);

            if (hasComboInfo)
            {
                if (combinationText != null)
                {
                    var comboStrings = new System.Collections.Generic.List<string>();
                    foreach (var combo in cardData.RequiredCombinations)
                    {
                        comboStrings.Add(combo.combinationDescription);
                    }
                    combinationText.text = $"조합식: {string.Join(", ", comboStrings)}";
                }

                // 조합 결과 표시 (추후 구현)
                UpdateCombinationResults();
            }
        }

        /// <summary>
        /// 조합 결과 업데이트 (추후 구현)
        /// </summary>
        private void UpdateCombinationResults()
        {
            if (combinationContainer == null || combinationItemPrefab == null) return;

            // 기존 조합 아이템 제거
            foreach (Transform child in combinationContainer)
            {
                Destroy(child.gameObject);
            }

            // ComboManager를 통해 이 카드가 포함된 조합들 찾기
            var comboManager = ComboManager.Instance;
            if (comboManager != null)
            {
                // TODO: ComboManager에 FindCombinationsWithCard 메서드 구현 필요
                // var combinations = comboManager.FindCombinationsWithCard(currentCard.CardData);
                // foreach (var combo in combinations)
                // {
                //     CreateCombinationItem(combo);
                // }
            }
        }

        /// <summary>
        /// 조합 아이템 생성
        /// </summary>
        private void CreateCombinationItem(CardSO resultCard)
        {
            if (combinationItemPrefab == null || combinationContainer == null) return;

            GameObject item = Instantiate(combinationItemPrefab, combinationContainer);

            // 조합 아이템 UI 설정 (간단한 구현)
            Text itemText = item.GetComponentInChildren<Text>();
            if (itemText != null)
                itemText.text = resultCard.CardName;

            Image itemImage = item.GetComponentInChildren<Image>();
            if (itemImage != null && resultCard.Image != null)
                itemImage.sprite = resultCard.Image;
        }

        /// <summary>
        /// 카드 타입 문자열 반환
        /// </summary>
        private string GetCardTypeString(CardType cardType)
        {
            return cardType switch
            {
                CardType.Element => "속성",
                CardType.Active1 => "액티브1",
                CardType.Active2 => "액티브2",
                CardType.Combo => "조합",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// 화면에 맞게 위치 조정
        /// </summary>
        private Vector2 AdjustPositionToFitScreen(Vector2 position)
        {
            if (canvasRect == null || tooltipRect == null) return position;

            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 tooltipSize = tooltipRect.sizeDelta;

            // 오른쪽 경계 확인
            if (position.x + tooltipSize.x > canvasSize.x * 0.5f - screenMargin.x)
            {
                position.x = canvasSize.x * 0.5f - tooltipSize.x - screenMargin.x;
            }

            // 왼쪽 경계 확인
            if (position.x < -canvasSize.x * 0.5f + screenMargin.x)
            {
                position.x = -canvasSize.x * 0.5f + screenMargin.x;
            }

            // 상단 경계 확인
            if (position.y + tooltipSize.y > canvasSize.y * 0.5f - screenMargin.y)
            {
                position.y = canvasSize.y * 0.5f - tooltipSize.y - screenMargin.y;
            }

            // 하단 경계 확인
            if (position.y < -canvasSize.y * 0.5f + screenMargin.y)
            {
                position.y = -canvasSize.y * 0.5f + screenMargin.y;
            }

            return position;
        }

        #endregion

        #region Animation Coroutines

        /// <summary>
        /// 페이드 인 애니메이션
        /// </summary>
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            fadeCoroutine = null;
        }

        /// <summary>
        /// 페이드 아웃 애니메이션
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            fadeCoroutine = null;
        }

        #endregion

        #region Static Utility Methods

        /// <summary>
        /// 전역 카드 툴팁 표시
        /// </summary>
        public static void ShowCardTooltip(Card card, Vector3 position = default)
        {
            if (Instance != null)
                Instance.ShowCard(card, position);
        }

        /// <summary>
        /// 전역 카드 툴팁 숨김
        /// </summary>
        public static void HideCardTooltip()
        {
            if (Instance != null)
                Instance.Hide();
        }

        #endregion
    }
}