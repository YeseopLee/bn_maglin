using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Maglin.Cards;
using Maglin.UI;
using DG.Tweening;

namespace Maglin.Battle
{
    /// <summary>
    /// 카드 드로우 애니메이션을 관리하는 매니저
    /// 싱글톤 패턴으로 구현되며, 하이어라키에서 직접 설정
    /// </summary>
    public class CardDrawAnimationManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static CardDrawAnimationManager _instance;

        public static CardDrawAnimationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CardDrawAnimationManager>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeManager();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 카드 드로우 애니메이션 시작 이벤트
        /// </summary>
        public static event System.Action<Card> OnCardDrawAnimationStarted;

        /// <summary>
        /// 개별 카드 드로우 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<Card, GameObject> OnCardDrawAnimationCompleted;

        /// <summary>
        /// 모든 카드 드로우 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<List<Card>> OnAllCardDrawAnimationsCompleted;

        /// <summary>
        /// 카드 재배치 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action OnCardArrangementCompleted;

        /// <summary>
        /// 카드 무덤 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<List<Card>> OnCardGraveAnimationCompleted;
        #endregion

        #region Serialized Fields
        [Header("UI 참조")]
        [SerializeField] private Transform deckArea;
        [SerializeField] private Transform handContent;
        [SerializeField] private Transform graveArea; // 무덤 영역
        [SerializeField] private GameObject cardUIPrefab;

        [Header("애니메이션 설정")]
        [SerializeField] private float cardDrawDuration = 0.2f; // 각 카드 뽑는 시간
        [SerializeField] private float cardInterval = 0.12f; // 카드 간 간격 (연속 뽑기 속도)
        [SerializeField] private Ease cardMoveEase = Ease.OutCubic; // 안정적인 움직임
        [SerializeField] private float cardSpawnScale = 0.3f; // 적당한 크기로 시작
        [SerializeField] private float cardRotationRange = 15f; // 적당한 회전

        [Header("위치 설정")]
        [SerializeField] private float cardWidth = 150f; // 카드 UI 너비
        [SerializeField] private float cardSpacing = 20f; // 카드 간 간격

        [Header("무덤 애니메이션 설정")]
        [SerializeField] private float graveAnimationDuration = 1.2f; // 무덤으로 이동하는 시간 (더 극적으로)
        [SerializeField] private float particleScaleSize = 0.15f; // 파티클 크기 (원래 크기의 배수) - 더 작게
        [SerializeField] private Ease graveAnimationEase = Ease.InOutQuad; // 무덤 애니메이션 곡선
        [SerializeField] private float particleDelay = 0.15f; // 카드별 파티클 딜레이 (약간 더 여유있게)

        [Header("효과 설정")]
        [SerializeField] private bool useCardFlip = true;
        [SerializeField] private bool useScaleEffect = true;
        [SerializeField] private bool useRotationEffect = true;
        [SerializeField] private bool useGlowEffect = true;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;
        #endregion

        #region Private Fields
        private bool isAnimating = false;

        /// <summary>
        /// 애니메이션 진행 중 여부 (외부 접근용)
        /// </summary>
        public bool IsAnimating => isAnimating;
        private List<GameObject> animatingCards = new List<GameObject>();
        private Queue<CardDrawRequest> drawQueue = new Queue<CardDrawRequest>();
        private bool isProcessingQueue = false;

        // UI 자동 탐지 필드
        private Transform autoDeckArea;
        private Transform autoHandContent;
        private GameObject autoCardUIPrefab;
        #endregion

        #region Initialization
        /// <summary>
        /// 매니저 초기화
        /// </summary>
        private void InitializeManager()
        {
            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] 초기화 시작");

            // UI 참조가 설정되지 않은 경우 자동 탐지
            AutoDetectUIReferences();

            // DOTween 초기화 (항상 안전하게 초기화)
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);

            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] 초기화 완료");
        }

        /// <summary>
        /// UI 참조 자동 탐지
        /// </summary>
        public void AutoDetectUIReferences()
        {
            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] UI 참조 자동 탐지 시작");

            // DeckArea 자동 탐지
            if (deckArea == null)
            {
                GameObject deckAreaObj = GameObject.Find("DeckArea");
                if (deckAreaObj != null)
                {
                    autoDeckArea = deckAreaObj.transform;
                    if (debugMode)
                        Debug.Log("[CardDrawAnimationManager] DeckArea 자동 탐지됨");
                }
                else
                {
                    // 대안: CardArea 하위에서 찾기
                    var cardArea = GameObject.Find("CardArea");
                    if (cardArea != null)
                    {
                        var deck = cardArea.transform.Find("DeckArea");
                        if (deck != null)
                        {
                            autoDeckArea = deck;
                            if (debugMode)
                                Debug.Log("[CardDrawAnimationManager] CardArea/DeckArea 자동 탐지됨");
                        }
                    }
                }
            }

            // HandContent 자동 탐지
            if (handContent == null)
            {
                GameObject handContentObj = GameObject.Find("HandContent");
                if (handContentObj != null)
                {
                    autoHandContent = handContentObj.transform;
                    if (debugMode)
                        Debug.Log("[CardDrawAnimationManager] HandContent 자동 탐지됨");
                }
                else
                {
                    // 대안: CardArea 하위에서 찾기
                    var cardArea = GameObject.Find("CardArea");
                    if (cardArea != null)
                    {
                        var hand = cardArea.transform.Find("HandContent");
                        if (hand != null)
                        {
                            autoHandContent = hand;
                            if (debugMode)
                                Debug.Log("[CardDrawAnimationManager] CardArea/HandContent 자동 탐지됨");
                        }
                    }
                }
            }

            // CardUIPrefab 자동 탐지 (BattleUIManager에서)
            if (cardUIPrefab == null && BattleUIManager.Instance != null)
            {
                // BattleUIManager의 cardUIPrefab을 리플렉션으로 가져오기
                var field = typeof(BattleUIManager).GetField("cardUIPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    autoCardUIPrefab = field.GetValue(BattleUIManager.Instance) as GameObject;
                    if (autoCardUIPrefab != null && debugMode)
                        Debug.Log("[CardDrawAnimationManager] CardUIPrefab 자동 탐지됨");
                }
            }

            // GraveArea 자동 탐지
            if (graveArea == null)
            {
                var graveObject = GameObject.Find("Grave");
                if (graveObject != null)
                {
                    graveArea = graveObject.transform;
                    if (debugMode)
                        Debug.Log("[CardDrawAnimationManager] Grave 자동 탐지됨");
                }
                else
                {
                    // 대안: CardArea 하위에서 찾기
                    var cardArea = GameObject.Find("CardArea");
                    if (cardArea != null)
                    {
                        var grave = cardArea.transform.Find("Grave");
                        if (grave != null)
                        {
                            graveArea = grave;
                            if (debugMode)
                                Debug.Log("[CardDrawAnimationManager] CardArea/Grave 자동 탐지됨");
                        }
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"[CardDrawAnimationManager] UI 참조 탐지 결과:");
                Debug.Log($"  - DeckArea: {GetDeckArea()?.name ?? "없음"}");
                Debug.Log($"  - HandContent: {GetHandContent()?.name ?? "없음"}");
                Debug.Log($"  - CardUIPrefab: {GetCardUIPrefab()?.name ?? "없음"}");
                Debug.Log($"  - GraveArea: {GetGraveArea()?.name ?? "없음"}");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// UI 참조 설정 (수동)
        /// </summary>
        public void SetUIReferences(Transform deckArea, Transform handContent, Transform graveArea, GameObject cardUIPrefab)
        {
            this.deckArea = deckArea;
            this.handContent = handContent;
            this.graveArea = graveArea;
            this.cardUIPrefab = cardUIPrefab;

            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] UI 참조가 수동으로 설정됨");
        }

        /// <summary>
        /// 카드 드로우 애니메이션 시작
        /// </summary>
        public void PlayCardDrawAnimation(List<Card> cardsToAnimate)
        {
            if (cardsToAnimate == null || cardsToAnimate.Count == 0) return;

            if (isAnimating)
            {
                // 현재 애니메이션 중이면 큐에 추가
                drawQueue.Enqueue(new CardDrawRequest(cardsToAnimate));
                ProcessDrawQueue();
                return;
            }

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 카드 드로우 애니메이션 시작: {cardsToAnimate.Count}장");

            StartCoroutine(ExecuteCardDrawAnimation(cardsToAnimate));
        }

        /// <summary>
        /// 새로운 카드만 드로우하는 애니메이션 (기존 손패는 유지)
        /// </summary>
        public void PlayNewCardDrawAnimation(List<Card> newCards)
        {
            if (newCards == null || newCards.Count == 0) return;

            if (isAnimating)
            {
                // 현재 애니메이션 중이면 큐에 추가
                drawQueue.Enqueue(new CardDrawRequest(newCards, true));
                ProcessDrawQueue();
                return;
            }

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 새로운 카드 드로우 애니메이션 시작: {newCards.Count}장");

            StartCoroutine(ExecuteNewCardDrawAnimation(newCards));
        }

        /// <summary>
        /// 단일 카드 드로우 애니메이션
        /// </summary>
        public void PlaySingleCardDrawAnimation(Card card)
        {
            if (card == null) return;
            PlayCardDrawAnimation(new List<Card> { card });
        }

        /// <summary>
        /// 현재 진행 중인 모든 애니메이션 중단
        /// </summary>
        public void StopAllAnimations()
        {
            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] 모든 애니메이션 중단");

            isAnimating = false;

            // 진행 중인 애니메이션 카드들 정리
            foreach (var card in animatingCards)
            {
                if (card != null)
                {
                    card.transform.DOKill();
                }
            }

            animatingCards.Clear();
            drawQueue.Clear();

            // TempCardParent 오브젝트들 정리
            ClearAllTempCardParents();
        }

        /// <summary>
        /// 모든 TempCardParent 오브젝트들 정리 (애니메이션 중단 시)
        /// </summary>
        private void ClearAllTempCardParents()
        {
            var handTransform = GetHandContent();
            if (handTransform == null) return;

            // handContent와 같은 부모 하위에서 TempCardParent 찾아서 제거
            Transform parentTransform = handTransform.parent;
            if (parentTransform != null)
            {
                var tempParents = new List<Transform>();
                for (int i = 0; i < parentTransform.childCount; i++)
                {
                    var child = parentTransform.GetChild(i);
                    if (child.name.StartsWith("TempCardParent"))
                    {
                        tempParents.Add(child);
                    }
                }

                foreach (var tempParent in tempParents)
                {
                    if (tempParent != null)
                    {
                        if (debugMode)
                            Debug.Log($"[CardDrawAnimationManager] TempCardParent 정리: {tempParent.name}");

                        // 하위의 카드 UI들의 DOTween 애니메이션도 중단
                        for (int i = 0; i < tempParent.childCount; i++)
                        {
                            var cardUI = tempParent.GetChild(i);
                            if (cardUI != null)
                            {
                                cardUI.DOKill();
                            }
                        }

                        Destroy(tempParent.gameObject);
                    }
                }

                if (tempParents.Count > 0 && debugMode)
                    Debug.Log($"[CardDrawAnimationManager] TempCardParent {tempParents.Count}개 정리 완료");
            }

            // handContent 직하위에 있을 수 있는 TempCardParent들도 정리
            var directTempParents = new List<Transform>();
            for (int i = 0; i < handTransform.childCount; i++)
            {
                var child = handTransform.GetChild(i);
                if (child.name.StartsWith("TempCardParent"))
                {
                    directTempParents.Add(child);
                }
            }

            foreach (var tempParent in directTempParents)
            {
                if (tempParent != null)
                {
                    if (debugMode)
                        Debug.Log($"[CardDrawAnimationManager] HandContent 직하위 TempCardParent 정리: {tempParent.name}");

                    // 하위의 카드 UI들의 DOTween 애니메이션도 중단
                    for (int i = 0; i < tempParent.childCount; i++)
                    {
                        var cardUI = tempParent.GetChild(i);
                        if (cardUI != null)
                        {
                            cardUI.DOKill();
                        }
                    }

                    Destroy(tempParent.gameObject);
                }
            }
        }

        /// <summary>
        /// 카드 재배치 애니메이션만 실행
        /// </summary>
        public void PlayCardArrangementAnimation()
        {
            if (GetHandContent() == null) return;

            StartCoroutine(ExecuteCardArrangementAnimation());
        }

        /// <summary>
        /// 카드들을 무덤으로 보내는 파티클 애니메이션
        /// </summary>
        public void PlayCardToGraveAnimation(List<Card> cards, List<GameObject> cardUIs)
        {
            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] PlayCardToGraveAnimation 호출됨 - 카드: {cards?.Count ?? 0}장, UI: {cardUIs?.Count ?? 0}개");

            if (cards == null || cardUIs == null || cards.Count == 0 || cardUIs.Count == 0)
            {
                if (debugMode)
                    Debug.LogWarning("[CardDrawAnimationManager] 무덤 애니메이션: 카드 데이터가 없습니다.");
                OnCardGraveAnimationCompleted?.Invoke(cards);
                return;
            }

            var graveTransform = GetGraveArea();
            if (graveTransform == null)
            {
                if (debugMode)
                    Debug.LogWarning("[CardDrawAnimationManager] 무덤 영역을 찾을 수 없어 애니메이션을 건너뜁니다.");
                OnCardGraveAnimationCompleted?.Invoke(cards);
                return;
            }

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 카드 무덤 애니메이션 시작: {cards.Count}장 → {graveTransform.name}");

            StartCoroutine(ExecuteCardToGraveAnimation(cards, cardUIs, graveTransform));
        }

        /// <summary>
        /// 현재 손패의 카드 수 가져오기
        /// </summary>
        private int GetCurrentHandCardCount()
        {
            var handTransform = GetHandContent();
            if (handTransform == null) return 0;

            return handTransform.childCount;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 카드 UI 생성 헬퍼 메서드
        /// </summary>
        private GameObject CreateCardUI(Card card, GameObject prefab, Transform parent)
        {
            GameObject cardUI = Instantiate(prefab, parent);

            // 카드 데이터 설정 (BattleUIManager와 동일한 방식)
            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null)
            {
                cardUIData = cardUI.AddComponent<CardUIData>();
            }
            cardUIData.CardInstance = card;

            // CardDraggable 컴포넌트 추가
            var cardDraggable = cardUI.GetComponent<CardDraggable>();
            if (cardDraggable == null)
            {
                cardDraggable = cardUI.AddComponent<CardDraggable>();
            }

            // CanvasGroup 컴포넌트 추가 (CardUI에서 필요)
            var canvasGroup = cardUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = cardUI.AddComponent<CanvasGroup>();
            }

            // CardUI 컴포넌트 추가 (hover 기능을 위해)
            var cardUIComponent = cardUI.GetComponent<CardUI>();
            if (cardUIComponent == null)
            {
                cardUIComponent = cardUI.AddComponent<CardUI>();
            }

            // Button 컴포넌트 추가
            var button = cardUI.GetComponent<Button>();
            if (button == null)
            {
                button = cardUI.AddComponent<Button>();
            }

            // 버튼 이벤트 설정 (BattleUIManager와 동일한 방식)
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                // BattleUIManager의 카드 클릭 이벤트 발생
                if (BattleUIManager.Instance != null)
                {
                    BattleUIManager.Instance.TriggerCardClicked(cardUI);
                }
            });

            // 카드 UI 정보 업데이트 (BattleUIManager의 UpdateCardUIInfo와 동일한 로직)
            UpdateCardUIInfo(cardUI, card);

            // BattleUIManager의 handCardUIs 리스트에 추가
            if (BattleUIManager.Instance != null)
            {
                var handCardUIsList = BattleUIManager.Instance.GetType()
                    .GetField("handCardUIs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(BattleUIManager.Instance) as List<GameObject>;
                handCardUIsList?.Add(cardUI);
            }

            return cardUI;
        }

        /// <summary>
        /// 카드 UI 정보 업데이트 (BattleUIManager와 동일한 로직)
        /// </summary>
        private void UpdateCardUIInfo(GameObject cardUI, Card card)
        {
            var nameText = cardUI.transform.Find("CardName")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = card.CardName;
            }

            var costText = cardUI.transform.Find("CardCost")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (costText != null)
            {
                costText.text = $"비용: {card.CurrentManaCost}";
            }

            var descText = cardUI.transform.Find("CardDescription")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = card.Description;
            }

            var infoText = cardUI.transform.Find("CardInfo")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (infoText != null)
            {
                infoText.text = $"{card.Element} | {card.Type}";
            }
        }

        // 더 이상 사용하지 않는 펼침 애니메이션 (바로 최종 위치로 뽑는 방식으로 변경됨)
        /*
        /// <summary>
        /// 카드 펼침 애니메이션 실행 (사용 안함)
        /// </summary>
        private IEnumerator ExecuteCardSpreadAnimation(List<GameObject> cardUIs, List<Card> cards)
        {
            // 이제 카드 드로우에서 바로 최종 위치로 이동하므로 별도 펼침 애니메이션 불필요
            yield break;
        }
        */
        #endregion

        #region Animation Execution
        /// <summary>
        /// 카드 드로우 애니메이션 실행 (한 지점으로 드로우)
        /// </summary>
        private IEnumerator ExecuteCardDrawAnimation(List<Card> cards)
        {
            isAnimating = true;
            animatingCards.Clear();

            var deckTransform = GetDeckArea();
            var handTransform = GetHandContent();
            var prefab = GetCardUIPrefab();

            if (deckTransform == null || handTransform == null || prefab == null)
            {
                Debug.LogWarning("[CardDrawAnimationManager] 필수 UI 참조가 없어 애니메이션을 건너뛰고 폴백 모드로 실행합니다");

                // 애니메이션 실패 시 직접 UI 생성으로 폴백
                isAnimating = false;
                CreateCardsDirectly(cards);
                yield break;
            }

            // 레이아웃 그룹 임시 비활성화 (수동 배치를 위해)
            var layoutGroup = handTransform.GetComponent<HorizontalLayoutGroup>();
            bool wasLayoutActive = false;
            if (layoutGroup != null)
            {
                wasLayoutActive = layoutGroup.enabled;
                layoutGroup.enabled = false;
            }

            List<GameObject> cardUIs = new List<GameObject>();

            // 손패 중앙 위치 계산 (모든 카드가 여기로 드로우됨)
            Vector3 handCenterPosition = Vector3.zero;

            for (int i = 0; i < cards.Count; i++)
            {
                // 임시 부모 생성 (애니메이션 동안 사용)
                GameObject tempParent = new GameObject($"TempCardParent_{i}");
                tempParent.transform.SetParent(handTransform, false);

                var tempRect = tempParent.AddComponent<RectTransform>();
                tempRect.anchoredPosition = Vector2.zero;
                tempRect.localScale = Vector3.one;

                // 카드 UI 생성 (임시 부모 하위에)
                GameObject cardUI = CreateCardUI(cards[i], prefab, tempParent.transform);
                cardUIs.Add(cardUI);
                animatingCards.Add(cardUI);

                // 나중에 드로우되는 카드가 위에 오도록 sibling index 설정
                tempParent.transform.SetAsLastSibling();

                // Canvas가 있으면 sortingOrder 조정하여 위에 렌더링되도록
                var canvas = cardUI.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = cardUI.AddComponent<Canvas>();
                    canvas.overrideSorting = true;
                }
                canvas.sortingOrder = 1000 + i; // 나중에 드로우되는 카드일수록 높은 sortingOrder

                // GraphicRaycaster 추가 (클릭 이벤트를 위해)
                if (cardUI.GetComponent<GraphicRaycaster>() == null)
                {
                    cardUI.AddComponent<GraphicRaycaster>();
                }

                // 덱 위치에서 시작
                cardUI.transform.position = deckTransform.position;
                cardUI.transform.localScale = Vector3.one * cardSpawnScale;

                // 손패 중앙으로 이동하는 애니메이션
                var moveSequence = DOTween.Sequence();
                moveSequence.Append(cardUI.transform.DOMove(handTransform.position, cardDrawDuration).SetEase(cardMoveEase));
                moveSequence.Join(cardUI.transform.DOScale(Vector3.one, cardDrawDuration).SetEase(Ease.OutBack));

                // 개별 카드 완료 이벤트
                int cardIndex = i;
                moveSequence.OnComplete(() =>
                {
                    // 실제 손패 영역으로 이동
                    cardUI.transform.SetParent(handTransform, false);

                    // GraphicRaycaster를 먼저 제거한 후 Canvas 제거 (의존성 해결)
                    var animRaycaster = cardUI.GetComponent<GraphicRaycaster>();
                    if (animRaycaster != null)
                    {
                        Destroy(animRaycaster);
                    }

                    var animCanvas = cardUI.GetComponent<Canvas>();
                    if (animCanvas != null)
                    {
                        Destroy(animCanvas);
                    }

                    // 임시 부모 제거
                    if (tempParent != null)
                    {
                        Destroy(tempParent);
                    }

                    // 레이아웃 강제 업데이트 (새 카드가 올바른 위치로 배치되도록)
                    if (layoutGroup != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(handTransform.GetComponent<RectTransform>());
                    }

                    // 완료 이벤트 발생
                    OnCardDrawAnimationCompleted?.Invoke(cards[cardIndex], cardUIs[cardIndex]);
                });

                OnCardDrawAnimationStarted?.Invoke(cards[i]);

                if (debugMode)
                    Debug.Log($"[CardDrawAnimationManager] 카드 {i + 1}/{cards.Count} 드로우: {cards[i].CardName} → 손패 중앙");

                // 다음 카드 드로우 전 딜레이
                yield return new WaitForSeconds(cardInterval);
            }

            // 마지막 카드 애니메이션 완료까지 대기
            yield return new WaitForSeconds(cardDrawDuration);

            // 모든 카드가 손패에 정착한 후 최종 레이아웃 정리
            if (layoutGroup != null)
            {
                layoutGroup.enabled = wasLayoutActive;
                if (wasLayoutActive)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(handTransform.GetComponent<RectTransform>());

                    if (debugMode)
                        Debug.Log("[CardDrawAnimationManager] 최종 레이아웃 정리 완료");
                }
            }

            isAnimating = false;

            // 완료 이벤트 발생
            OnAllCardDrawAnimationsCompleted?.Invoke(cards);

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 카드 드로우 애니메이션 완료: {cards.Count}장");

            // 대기 중인 애니메이션 처리
            ProcessDrawQueue();
        }

        /// <summary>
        /// 단일 카드 드로우 애니메이션
        /// </summary>
        private IEnumerator AnimateSingleCardDraw(Card card, int index, Transform deckPos, Transform handPos, GameObject prefab)
        {
            if (card == null) yield break;

            // 이벤트 발생
            OnCardDrawAnimationStarted?.Invoke(card);

            // 카드 UI 생성
            GameObject cardUI = Instantiate(prefab);
            animatingCards.Add(cardUI);

            // 카드 데이터 설정
            SetupCardUI(cardUI, card);

            // 덱 위치에서 시작
            cardUI.transform.SetParent(deckPos, false);
            cardUI.transform.localPosition = Vector3.zero;
            cardUI.transform.localScale = Vector3.one * cardSpawnScale;

            // 초기 회전 설정
            float randomRotation = Random.Range(-cardRotationRange, cardRotationRange);
            cardUI.transform.rotation = Quaternion.Euler(0, 0, randomRotation);

            // 애니메이션 시퀀스 생성
            Sequence drawSequence = DOTween.Sequence();

            // 1. 카드 스케일 증가
            if (useScaleEffect)
            {
                drawSequence.Append(cardUI.transform.DOScale(Vector3.one, cardDrawDuration * 0.3f).SetEase(Ease.OutQuad));
            }

            // 2. 손패 위치로 이동
            cardUI.transform.SetParent(handPos, true);
            Vector3 targetPosition = CalculateTemporaryPosition(index, handPos);

            drawSequence.Append(cardUI.transform.DOLocalMove(targetPosition, cardDrawDuration * 0.6f).SetEase(cardMoveEase));

            // 3. 회전 정리
            if (useRotationEffect)
            {
                drawSequence.Join(cardUI.transform.DORotateQuaternion(Quaternion.identity, cardDrawDuration * 0.4f).SetEase(Ease.OutQuad));
            }

            // 4. 글로우 효과 (선택적)
            if (useGlowEffect)
            {
                var image = cardUI.GetComponent<Image>();
                if (image != null)
                {
                    Color originalColor = image.color;
                    Color glowColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0.7f);

                    drawSequence.Insert(cardDrawDuration * 0.2f,
                        image.DOColor(glowColor, cardDrawDuration * 0.2f).SetEase(Ease.OutQuad));
                    drawSequence.Insert(cardDrawDuration * 0.4f,
                        image.DOColor(originalColor, cardDrawDuration * 0.3f).SetEase(Ease.InQuad));
                }
            }

            // 애니메이션 완료 대기
            yield return drawSequence.WaitForCompletion();

            // 완료 이벤트 발생
            OnCardDrawAnimationCompleted?.Invoke(card, cardUI);

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 카드 드로우 애니메이션 완료: {card.CardName}");
        }

        /// <summary>
        /// 새로운 카드만 드로우하는 애니메이션 실행 (기존 손패는 그대로 유지)
        /// </summary>
        private IEnumerator ExecuteNewCardDrawAnimation(List<Card> newCards)
        {
            isAnimating = true;
            animatingCards.Clear();

            var deckTransform = GetDeckArea();
            var handTransform = GetHandContent();
            var prefab = GetCardUIPrefab();

            if (deckTransform == null || handTransform == null || prefab == null)
            {
                if (debugMode)
                    Debug.LogWarning("[CardDrawAnimationManager] 필수 UI 참조가 없어 새 카드 애니메이션을 건너뛰고 폴백 모드로 실행합니다");

                isAnimating = false;
                CreateCardsDirectly(newCards);
                yield break;
            }

            // 기존 손패 카드 수 확인
            int existingCardCount = GetCurrentHandCardCount();

            // 레이아웃 그룹은 활성화 상태 유지 (비활성화하지 않음)
            var layoutGroup = handTransform.GetComponent<HorizontalLayoutGroup>();

            List<GameObject> newCardUIs = new List<GameObject>();

            // 새로운 카드들을 순차적으로 드로우
            for (int i = 0; i < newCards.Count; i++)
            {
                var newCard = newCards[i];

                // 새로운 카드 UI를 임시로 별도 부모에 생성 (레이아웃 영향 방지)
                GameObject tempParent = new GameObject("TempCardParent");
                tempParent.transform.SetParent(handTransform.parent, false);

                // 나중에 드로우되는 카드가 위에 오도록 sibling index 설정
                tempParent.transform.SetAsLastSibling();

                // 새로운 카드 UI 생성 (임시 부모에)
                GameObject cardUI = CreateCardUI(newCard, prefab, tempParent.transform);
                newCardUIs.Add(cardUI);
                animatingCards.Add(cardUI);

                // Canvas가 있으면 sortingOrder 조정하여 위에 렌더링되도록
                var canvas = cardUI.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = cardUI.AddComponent<Canvas>();
                    canvas.overrideSorting = true;
                }
                canvas.sortingOrder = 1000 + i; // 나중에 드로우되는 카드일수록 높은 sortingOrder

                // GraphicRaycaster 추가 (클릭 이벤트를 위해)
                if (cardUI.GetComponent<GraphicRaycaster>() == null)
                {
                    cardUI.AddComponent<GraphicRaycaster>();
                }

                // 덱 위치에서 시작
                cardUI.transform.position = deckTransform.position;
                cardUI.transform.localScale = Vector3.one * cardSpawnScale;

                // 손패 중앙 근처의 임시 위치 계산 (여러 카드가 겹치지 않도록)
                Vector3 tempPosition = handTransform.position; // 손패 영역의 월드 위치
                if (newCards.Count > 1)
                {
                    // 여러 장인 경우 살짝 다른 위치로 분산
                    float offset = (i - (newCards.Count - 1) * 0.5f) * 30f; // 30픽셀씩 간격
                    tempPosition += handTransform.right * offset;
                }

                // 손패 중앙(임시 위치)으로 이동하는 애니메이션
                var moveSequence = DOTween.Sequence();
                moveSequence.Append(cardUI.transform.DOMove(tempPosition, cardDrawDuration).SetEase(cardMoveEase));
                moveSequence.Join(cardUI.transform.DOScale(Vector3.one, cardDrawDuration).SetEase(Ease.OutBack));

                // 개별 카드 완료 이벤트 (애니메이션 완료 후 손패에 추가 + 이벤트 발생)
                int cardIndex = i;
                moveSequence.OnComplete(() =>
                {
                    // 실제 손패 영역으로 이동
                    cardUI.transform.SetParent(handTransform, false);

                    // GraphicRaycaster를 먼저 제거한 후 Canvas 제거 (의존성 해결)
                    var animRaycaster = cardUI.GetComponent<GraphicRaycaster>();
                    if (animRaycaster != null)
                    {
                        Destroy(animRaycaster);
                    }

                    var animCanvas = cardUI.GetComponent<Canvas>();
                    if (animCanvas != null)
                    {
                        Destroy(animCanvas);
                    }

                    // 임시 부모 제거
                    if (tempParent != null)
                    {
                        Destroy(tempParent);
                    }

                    // 레이아웃 강제 업데이트 (새 카드가 올바른 위치로 배치되도록)
                    if (layoutGroup != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(handTransform.GetComponent<RectTransform>());
                    }

                    // 완료 이벤트 발생
                    OnCardDrawAnimationCompleted?.Invoke(newCards[cardIndex], newCardUIs[cardIndex]);
                });

                OnCardDrawAnimationStarted?.Invoke(newCard);

                if (debugMode)
                    Debug.Log($"[CardDrawAnimationManager] 새 카드 {i + 1}/{newCards.Count} 드로우: {newCard.CardName} → 손패 중앙");

                // 다음 카드 드로우 전 딜레이
                yield return new WaitForSeconds(cardInterval);
            }

            // 마지막 카드 애니메이션 완료까지 대기
            yield return new WaitForSeconds(cardDrawDuration);

            // 모든 카드가 손패에 정착한 후 최종 레이아웃 정리
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(handTransform.GetComponent<RectTransform>());

                if (debugMode)
                    Debug.Log("[CardDrawAnimationManager] 최종 레이아웃 정리 완료");
            }

            isAnimating = false;

            // 완료 이벤트 발생
            OnAllCardDrawAnimationsCompleted?.Invoke(newCards);

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 새 카드 드로우 애니메이션 완료: {newCards.Count}장");

            // 대기 중인 애니메이션 처리
            ProcessDrawQueue();
        }

        /// <summary>
        /// 카드 재배치 애니메이션 실행
        /// </summary>
        private IEnumerator ExecuteCardArrangementAnimation()
        {
            var handTransform = GetHandContent();
            if (handTransform == null) yield break;

            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] 카드 재배치 애니메이션 시작");

            var cardUIs = new List<Transform>();
            for (int i = 0; i < handTransform.childCount; i++)
            {
                cardUIs.Add(handTransform.GetChild(i));
            }

            if (cardUIs.Count == 0) yield break;

            // 레이아웃 강제 업데이트
            var layoutGroup = handTransform.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(handTransform as RectTransform);
            }

            // 각 카드를 최종 위치로 애니메이션
            for (int i = 0; i < cardUIs.Count; i++)
            {
                var cardTransform = cardUIs[i];
                if (cardTransform == null) continue;

                // 최종 위치 계산 (레이아웃에 의해 결정됨)
                Vector3 targetPosition = cardTransform.localPosition;

                // 약간의 오프셋을 주고 애니메이션
                Vector3 startOffset = new Vector3(Random.Range(-50f, 50f), Random.Range(-20f, 20f), 0);
                cardTransform.localPosition = targetPosition + startOffset;

                // 애니메이션 실행 (새 파라미터 사용)
                cardTransform.DOLocalMove(targetPosition, cardDrawDuration)
    .SetEase(cardMoveEase)
    .SetDelay(i * 0.05f); // 짧은 딜레이로 순차 효과

                // 스케일 펀치 효과
                cardTransform.DOPunchScale(Vector3.one * 0.1f, cardDrawDuration * 0.5f, 1, 0.3f)
                    .SetDelay(i * 0.05f);
            }

            // 모든 애니메이션 완료 대기
            yield return new WaitForSeconds(cardDrawDuration + (cardUIs.Count * 0.05f));

            // 완료 이벤트 발생
            OnCardArrangementCompleted?.Invoke();

            if (debugMode)
                Debug.Log("[CardDrawAnimationManager] 카드 재배치 애니메이션 완료");
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 카드 UI 설정
        /// </summary>
        private void SetupCardUI(GameObject cardUI, Card card)
        {
            // BattleUIManager의 UpdateCardUIInfo 메서드 활용
            if (BattleUIManager.Instance != null)
            {
                var method = typeof(BattleUIManager).GetMethod("UpdateCardUIInfo",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(BattleUIManager.Instance, new object[] { cardUI, card });
                }
            }

            // CardUIData 컴포넌트 설정
            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null)
            {
                cardUIData = cardUI.AddComponent<CardUIData>();
            }
            cardUIData.CardInstance = card;
        }

        /// <summary>
        /// 임시 위치 계산 (재배치 전)
        /// </summary>
        private Vector3 CalculateTemporaryPosition(int index, Transform handParent)
        {
            // 손패 영역의 중앙에서 약간 분산된 위치
            float spreadRange = 100f;
            float xOffset = (index - 2.5f) * (spreadRange / 5f); // 최대 5장 기준
            float yOffset = Random.Range(-10f, 10f);

            return new Vector3(xOffset, yOffset, 0);
        }

        /// <summary>
        /// 드로우 큐 처리
        /// </summary>
        private void ProcessDrawQueue()
        {
            if (isProcessingQueue || drawQueue.Count == 0 || isAnimating) return;

            isProcessingQueue = true;
            var request = drawQueue.Dequeue();
            StartCoroutine(ProcessQueuedRequest(request));
        }

        /// <summary>
        /// 큐된 요청 처리
        /// </summary>
        private IEnumerator ProcessQueuedRequest(CardDrawRequest request)
        {
            yield return new WaitUntil(() => !isAnimating);

            if (request.IsNewCardOnly)
            {
                PlayNewCardDrawAnimation(request.Cards);
            }
            else
            {
                PlayCardDrawAnimation(request.Cards);
            }
            isProcessingQueue = false;
        }

        /// <summary>
        /// DeckArea 참조 가져오기
        /// </summary>
        private Transform GetDeckArea()
        {
            return deckArea != null ? deckArea : autoDeckArea;
        }

        /// <summary>
        /// HandContent 참조 가져오기
        /// </summary>
        private Transform GetHandContent()
        {
            return handContent != null ? handContent : autoHandContent;
        }

        /// <summary>
        /// 무덤 영역 참조 가져오기
        /// </summary>
        private Transform GetGraveArea()
        {
            if (graveArea != null) return graveArea;

            // 자동 탐지 시도
            var graveObject = GameObject.Find("Grave");
            if (graveObject != null)
            {
                return graveObject.transform;
            }

            // CardArea 하위에서 찾기
            var cardArea = GameObject.Find("CardArea");
            if (cardArea != null)
            {
                var grave = cardArea.transform.Find("Grave");
                if (grave != null)
                {
                    return grave;
                }
            }

            if (debugMode)
                Debug.LogWarning("[CardDrawAnimationManager] 무덤 영역을 찾을 수 없습니다. Inspector에서 graveArea를 설정해주세요.");

            return null;
        }

        /// <summary>
        /// CardUIPrefab 참조 가져오기
        /// </summary>
        private GameObject GetCardUIPrefab()
        {
            return cardUIPrefab != null ? cardUIPrefab : autoCardUIPrefab;
        }
        #endregion

        #region Data Classes
        /// <summary>
        /// 카드 드로우 요청 데이터
        /// </summary>
        private class CardDrawRequest
        {
            public List<Card> Cards { get; private set; }
            public bool IsNewCardOnly { get; private set; }

            public CardDrawRequest(List<Card> cards, bool isNewCardOnly = false)
            {
                Cards = new List<Card>(cards);
                IsNewCardOnly = isNewCardOnly;
            }
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Animation Info")]
        public void DebugAnimationInfo()
        {
            Debug.Log($"=== CardDrawAnimationManager Debug Info ===");
            Debug.Log($"애니메이션 진행 중: {isAnimating}");
            Debug.Log($"애니메이션 카드 수: {animatingCards.Count}");
            Debug.Log($"대기 중인 요청: {drawQueue.Count}");
            Debug.Log($"DeckArea: {GetDeckArea()?.name ?? "없음"}");
            Debug.Log($"HandContent: {GetHandContent()?.name ?? "없음"}");
            Debug.Log($"CardUIPrefab: {GetCardUIPrefab()?.name ?? "없음"}");
        }

        /// <summary>
        /// 애니메이션 실패 시 직접 카드 UI 생성 (폴백)
        /// </summary>
        private void CreateCardsDirectly(List<Card> cards)
        {
            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 폴백 모드로 카드 UI 직접 생성: {cards.Count}장");

            // BattleUIManager의 직접 업데이트 방식으로 폴백
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.UpdateHandUIWithoutAnimation(cards);
            }
        }

        /// <summary>
        /// 카드들을 무덤으로 보내는 파티클 애니메이션 실행
        /// </summary>
        private IEnumerator ExecuteCardToGraveAnimation(List<Card> cards, List<GameObject> cardUIs, Transform graveTransform)
        {
            if (cards.Count != cardUIs.Count)
            {
                Debug.LogWarning($"[CardDrawAnimationManager] 카드 데이터({cards.Count}장)와 UI 개수({cardUIs.Count}개)가 맞지 않지만 애니메이션을 계속 진행합니다.");
            }

            // null UI 제거
            cardUIs.RemoveAll(ui => ui == null);

            if (cardUIs.Count == 0)
            {
                Debug.LogWarning("[CardDrawAnimationManager] 유효한 카드 UI가 없어 무덤 애니메이션을 건너뜁니다.");
                OnCardGraveAnimationCompleted?.Invoke(cards);
                yield break;
            }

            Vector3 gravePosition = graveTransform.position;

            // 각 카드를 파티클로 변환하고 무덤으로 이동
            for (int i = 0; i < cardUIs.Count; i++)
            {
                GameObject cardUI = cardUIs[i];
                Card card = i < cards.Count ? cards[i] : null;

                if (cardUI == null) continue;

                Vector3 startPosition = cardUI.transform.position;
                Vector3 cardUIPosition = cardUI.transform.position;

                // 자연스러운 중간 지점 계산 (살짝 위로 올라가는 포물선)
                Vector3 midPoint = Vector3.Lerp(startPosition, gravePosition, 0.5f);
                midPoint.y += UnityEngine.Random.Range(30f, 50f); // 살짝만 위로 올라가는 효과

                // 자연스러운 애니메이션 시퀀스 생성
                var cardSequence = DOTween.Sequence();

                // 0단계: 카드 사용 후 살짝 뜨는 효과
                cardSequence.Append(cardUI.transform.DOScale(Vector3.one * 1.05f, graveAnimationDuration * 0.15f)
                    .SetEase(Ease.OutBack));

                // 1단계: 중간 지점으로 이동하면서 천천히 회전하고 작아지기
                var moveToMid = cardUI.transform.DOMove(midPoint, graveAnimationDuration * 0.6f)
                    .SetEase(Ease.OutQuart);

                // 회전을 적게 하고 자연스럽게
                var rotateUp = cardUI.transform.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(45f, 120f)), graveAnimationDuration * 0.6f, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutQuart);

                var scaleDown = cardUI.transform.DOScale(Vector3.one * 0.5f, graveAnimationDuration * 0.6f)
                    .SetEase(Ease.InOutQuart);

                cardSequence.Join(moveToMid);
                cardSequence.Join(rotateUp);
                cardSequence.Join(scaleDown);

                // 2단계: 무덤으로 부드럽게 하강
                var finalMove = cardUI.transform.DOMove(gravePosition, graveAnimationDuration * 0.3f)
                    .SetEase(Ease.InQuart);

                // 마지막 회전은 더 적게
                var finalRotate = cardUI.transform.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(180f, 270f)), graveAnimationDuration * 0.3f, RotateMode.FastBeyond360)
                    .SetEase(Ease.InQuart);

                // 더 작게 축소
                var finalScale = cardUI.transform.DOScale(Vector3.one * particleScaleSize, graveAnimationDuration * 0.25f)
                    .SetEase(Ease.InBack);

                cardSequence.Append(finalMove);
                cardSequence.Join(finalRotate);
                cardSequence.Join(finalScale);

                // 3단계: 투명도와 함께 자연스럽게 사라짐
                var image = cardUI.GetComponent<Image>();
                if (image != null)
                {
                    var fadeOut = image.DOFade(0f, graveAnimationDuration * 0.15f)
                        .SetEase(Ease.InQuart);
                    cardSequence.Join(fadeOut);
                }

                // 완전히 사라지기 (흔들림 효과 제거)
                cardSequence.Append(cardUI.transform.DOScale(Vector3.zero, graveAnimationDuration * 0.1f)
                    .SetEase(Ease.InBack));

                // 애니메이션 완료 시 오브젝트 제거
                cardSequence.OnComplete(() =>
                {
                    if (cardUI != null)
                    {
                        Destroy(cardUI);
                    }
                });

                // 순차적으로 시작하도록 딜레이
                cardSequence.SetDelay(i * particleDelay);

                if (debugMode && card != null)
                    Debug.Log($"[CardDrawAnimationManager] 극적인 카드 무덤 애니메이션: {card.CardName} → 무덤");
            }

            // 모든 애니메이션 완료까지 대기
            float totalDuration = graveAnimationDuration + (cardUIs.Count - 1) * particleDelay;
            yield return new WaitForSeconds(totalDuration);

            // 완료 이벤트 발생
            OnCardGraveAnimationCompleted?.Invoke(cards);

            if (debugMode)
                Debug.Log($"[CardDrawAnimationManager] 모든 카드 무덤 애니메이션 완료: {cards.Count}장");
        }
        #endregion
    }
}