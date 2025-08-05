using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Maglin.Core;
using Maglin.Player;
using Maglin.Cards;
using Maglin.Enemy;
using Maglin.UI;
using Maglin.Relics;
using TMPro;
using Core; // LoadingManager 사용을 위해 추가

namespace Maglin.Battle
{
    /// <summary>
    /// 전투 UI를 관리하는 매니저
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static BattleUIManager _instance;

        public static BattleUIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BattleUIManager>();

                    if (_instance == null)
                    {
                        GameObject uiManagerObject = new GameObject("BattleUIManager");
                        _instance = uiManagerObject.AddComponent<BattleUIManager>();
                        DontDestroyOnLoad(uiManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 카드 클릭 이벤트
        /// </summary>
        public static event System.Action<GameObject> OnCardClicked;

        /// <summary>
        /// 조합 실행 버튼 클릭 이벤트
        /// </summary>
        public static event System.Action OnExecuteComboClicked;

        /// <summary>
        /// 조합 초기화 버튼 클릭 이벤트
        /// </summary>
        public static event System.Action OnClearComboClicked;

        /// <summary>
        /// 턴 종료 버튼 클릭 이벤트
        /// </summary>
        public static event System.Action OnEndTurnClicked;

        /// <summary>
        /// 추가 드로우 버튼 클릭 이벤트
        /// </summary>
        public static event System.Action OnDrawCardClicked;
        #endregion

        #region UI References
        [Header("기본 UI")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI deckCountText;
        [SerializeField] private Transform handContent;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button drawButton;
        [SerializeField] private TextMeshProUGUI turnIndicator;

        [Header("조합 슬롯 UI")]
        [SerializeField] private Transform elementSlot;
        [SerializeField] private Transform active1Slot;
        [SerializeField] private Transform active2Slot;
        [SerializeField] private Button executeComboButton;
        [SerializeField] private Button clearComboButton;

        [Header("필드 효과 UI")]
        [SerializeField] private UnityEngine.UI.Image fieldAreaImage;
        [SerializeField] private TMPro.TextMeshProUGUI fieldEffectTurnsText;

        [Header("필드 슬롯")]
        [SerializeField] private Transform[] fieldSlots = new Transform[10];

        [Header("프리팹")]
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private GameObject loadingUIPrefab; // 로딩 UI 프리팹 (초기 블랙스크린용)

        [Header("보상 UI")]
        [SerializeField] private CanvasGroup rewardSelectionUI;
        [SerializeField] private Transform[] rewardSlots = new Transform[3];
        [SerializeField] private Button skipRewardButton;
        #endregion

        #region Private Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 생성된 UI 오브젝트들
        private List<GameObject> handCardUIs = new List<GameObject>();

        // 조합 슬롯에 배치된 카드들
        private Card elementSlotCard = null;
        private Card active1SlotCard = null;
        private Card active2SlotCard = null;
        private GameObject elementSlotUI = null;
        private GameObject active1SlotUI = null;
        private GameObject active2SlotUI = null;

        // 전투 상태
        private bool isBattleActive = false;
        private bool isPlayerTurn = true;

        // 현재 보상 목록
        private RewardItem[] currentRewards;

        // 초기 블랙스크린 관리
        private GameObject initialBlackScreen;
        private CanvasGroup initialBlackScreenCanvasGroup;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 초기 블랙스크린 먼저 생성 (다른 초기화보다 우선)
                CreateInitialBlackScreen();
                
                InitializeUIManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            // 이벤트 구독
            if (CardManager.Instance != null)
            {
                CardManager.OnHandCardsChanged += UpdateHandUI;
                CardManager.OnDrawCostChanged += UpdateDrawButton;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnHealthChanged += (current, max) => UpdateHealthUI();
                PlayerManager.OnManaChanged += (current, max) => UpdateManaUI();
                PlayerManager.OnGoldChanged += (gold) => UpdateGoldUI();
            }

            if (BattleManager.Instance != null)
            {
                BattleManager.OnTurnChanged += OnTurnChanged;
                BattleManager.OnPhaseChanged += OnPhaseChanged;
            }

            // LoadingManager 이벤트 구독
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.OnLoadingCompleted += OnLoadingCompleted;
            }

            // 자체 이벤트 구독
            OnCardClicked += HandleCardClicked;

            // 초기 UI 업데이트
            StartCoroutine(UpdateUIAfterDelay());
        }

        /// <summary>
        /// 지연 후 UI 업데이트 (다른 매니저들이 초기화된 후)
        /// </summary>
        private System.Collections.IEnumerator UpdateUIAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            UpdateAllUI();
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (CardManager.Instance != null)
            {
                CardManager.OnHandCardsChanged -= UpdateHandUI;
                CardManager.OnDrawCostChanged -= UpdateDrawButton;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnHealthChanged -= (current, max) => UpdateHealthUI();
                PlayerManager.OnManaChanged -= (current, max) => UpdateManaUI();
                PlayerManager.OnGoldChanged -= (gold) => UpdateGoldUI();
            }

            if (BattleManager.Instance != null)
            {
                BattleManager.OnTurnChanged -= OnTurnChanged;
                BattleManager.OnPhaseChanged -= OnPhaseChanged;
            }

            // LoadingManager 이벤트 구독 해제
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.OnLoadingCompleted -= OnLoadingCompleted;
            }

            // 자체 이벤트 구독 해제
            OnCardClicked -= HandleCardClicked;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// UI 매니저 초기화
        /// </summary>
        public void InitializeUIManager()
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] UI 매니저 초기화 시작");

            // 버튼 이벤트 연결
            SetupButtonEvents();

            // 조합 슬롯 자동 찾기
            FindComboSlots();

            // 필드 슬롯 찾기
            FindFieldSlots();

            // MonsterSpawnManager에 프리팹 전달 (Inspector에서 설정된 경우)
            if (monsterPrefab != null && MonsterSpawnManager.Instance != null)
            {
                MonsterSpawnManager.Instance.SetMonsterPrefab(monsterPrefab);
                if (debugMode)
                    Debug.Log("[BattleUIManager] MonsterSpawnManager에 몬스터 프리팹 전달 (초기화 시)");
            }
            else if (monsterPrefab == null && debugMode)
            {
                Debug.LogWarning("[BattleUIManager] 몬스터 프리팹이 Inspector에서 설정되지 않았습니다. BattleUIManager의 Monster Prefab 필드에 프리팹을 연결하세요.");
            }

            // 보상 UI 찾기 및 설정
            FindRewardUI();

            if (debugMode)
                Debug.Log("[BattleUIManager] UI 매니저 초기화 완료");
        }

        /// <summary>
        /// 초기 블랙스크린 생성
        /// </summary>
        private void CreateInitialBlackScreen()
        {
            if (loadingUIPrefab == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] loadingUIPrefab이 설정되지 않았습니다. Inspector에서 설정해주세요.");
                return;
            }

            // 로딩 UI 프리팹 인스턴스화
            initialBlackScreen = Instantiate(loadingUIPrefab);
            initialBlackScreen.name = "InitialBlackScreen";

            // Canvas의 sortingOrder를 낮게 설정 (실제 로딩 화면보다 뒤에)
            Canvas initialCanvas = initialBlackScreen.GetComponent<Canvas>();
            if (initialCanvas != null)
            {
                initialCanvas.sortingOrder = 999; // LoadingManager의 sortingOrder(1000)보다 낮게
                
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 초기 블랙스크린 Canvas sortingOrder: {initialCanvas.sortingOrder}");
            }

            // CanvasGroup 가져오기
            initialBlackScreenCanvasGroup = initialBlackScreen.GetComponent<CanvasGroup>();
            if (initialBlackScreenCanvasGroup == null)
            {
                initialBlackScreenCanvasGroup = initialBlackScreen.GetComponentInChildren<CanvasGroup>();
            }

            if (initialBlackScreenCanvasGroup != null)
            {
                // 완전 불투명하게 설정하여 화면 전체를 가림
                initialBlackScreenCanvasGroup.alpha = 1f;
                initialBlackScreenCanvasGroup.blocksRaycasts = true;
                initialBlackScreenCanvasGroup.interactable = false;

                if (debugMode)
                    Debug.Log("[BattleUIManager] 초기 블랙스크린 생성 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] 로딩 UI 프리팹에 CanvasGroup이 없습니다.");
            }

            // BackgroundPanel만 보이게 하고 나머지는 숨기기
            HideLoadingUIElements();
        }

        /// <summary>
        /// 로딩 UI의 다른 요소들 숨기기 (배경만 남기기)
        /// </summary>
        private void HideLoadingUIElements()
        {
            if (initialBlackScreen == null) return;

            // 로딩 컨테이너 숨기기 (텍스트, 진행률 바, 스피너 등)
            Transform loadingContainer = initialBlackScreen.transform.Find("LoadingContainer");
            if (loadingContainer != null)
            {
                loadingContainer.gameObject.SetActive(false);
                
                if (debugMode)
                    Debug.Log("[BattleUIManager] 로딩 UI 요소들 숨김 (배경만 유지)");
            }
        }

        /// <summary>
        /// 로딩 완료 시 초기 블랙스크린 제거
        /// </summary>
        private void OnLoadingCompleted()
        {
            if (initialBlackScreen != null)
            {
                StartCoroutine(FadeOutInitialBlackScreen());
            }
        }

        /// <summary>
        /// 초기 블랙스크린 페이드 아웃
        /// </summary>
        private System.Collections.IEnumerator FadeOutInitialBlackScreen()
        {
            if (initialBlackScreenCanvasGroup == null) yield break;

            if (debugMode)
                Debug.Log("[BattleUIManager] 초기 블랙스크린 페이드 아웃 시작");

            float fadeSpeed = 2f; // 페이드 속도
            float timer = 0f;

            while (timer < 1f)
            {
                timer += Time.unscaledDeltaTime * fadeSpeed;
                initialBlackScreenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer);
                yield return null;
            }

            // 완전히 투명해진 후 제거
            initialBlackScreenCanvasGroup.alpha = 0f;
            initialBlackScreenCanvasGroup.blocksRaycasts = false;

            // 오브젝트 제거
            if (initialBlackScreen != null)
            {
                Destroy(initialBlackScreen);
                initialBlackScreen = null;
                initialBlackScreenCanvasGroup = null;

                if (debugMode)
                    Debug.Log("[BattleUIManager] 초기 블랙스크린 제거 완료");
            }
        }

        /// <summary>
        /// UI 참조 설정 (BattleTestController에서 호출)
        /// </summary>
        public void SetUIReferences(
            TextMeshProUGUI healthText,
            TextMeshProUGUI manaText,
            TextMeshProUGUI goldText,
            TextMeshProUGUI deckCountText,
            Transform handContent,
            Button endTurnButton,
            Button drawButton,
            TextMeshProUGUI turnIndicator,
            Transform elementSlot,
            Transform active1Slot,
            Transform active2Slot,
            Button executeComboButton,
            Button clearComboButton,
            UnityEngine.UI.Image fieldAreaImage,
            TMPro.TextMeshProUGUI fieldEffectTurnsText,
            GameObject cardUIPrefab,
            GameObject monsterPrefab,
            GameObject loadingUIPrefab = null)
        {
            this.healthText = healthText;
            this.manaText = manaText;
            this.goldText = goldText;
            this.deckCountText = deckCountText;
            this.handContent = handContent;
            this.endTurnButton = endTurnButton;
            this.drawButton = drawButton;
            this.turnIndicator = turnIndicator;
            this.elementSlot = elementSlot;
            this.active1Slot = active1Slot;
            this.active2Slot = active2Slot;
            this.executeComboButton = executeComboButton;
            this.clearComboButton = clearComboButton;
            this.fieldAreaImage = fieldAreaImage;
            this.fieldEffectTurnsText = fieldEffectTurnsText;
            this.cardUIPrefab = cardUIPrefab;
            this.monsterPrefab = monsterPrefab;
            
            // 로딩 UI 프리팹 설정 (제공된 경우)
            if (loadingUIPrefab != null)
            {
                this.loadingUIPrefab = loadingUIPrefab;
            }

            if (debugMode)
                Debug.Log("[BattleUIManager] UI 참조 설정 완료");

            // UI 참조 설정 후 버튼 이벤트 연결
            SetupButtonEvents();

            // MonsterSpawnManager에 프리팹 전달
            if (MonsterSpawnManager.Instance != null)
            {
                MonsterSpawnManager.Instance.SetMonsterPrefab(monsterPrefab);
                if (debugMode)
                    Debug.Log("[BattleUIManager] MonsterSpawnManager에 몬스터 프리팹 전달");
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());

            if (drawButton != null)
                drawButton.onClick.AddListener(() => OnDrawCardClicked?.Invoke());

            if (executeComboButton != null)
                executeComboButton.onClick.AddListener(() => OnExecuteComboClicked?.Invoke());

            if (clearComboButton != null)
                clearComboButton.onClick.AddListener(() => OnClearComboClicked?.Invoke());
        }

        /// <summary>
        /// 조합 슬롯 자동 찾기
        /// </summary>
        private void FindComboSlots()
        {
            Transform comboSlotArea = transform.Find("Canvas/ComboSlotArea");
            if (comboSlotArea == null)
            {
                comboSlotArea = GameObject.Find("ComboSlotArea")?.transform;
            }

            if (comboSlotArea != null)
            {
                elementSlot = comboSlotArea.Find("ComboSlot_0");
                active1Slot = comboSlotArea.Find("ComboSlot_1");
                active2Slot = comboSlotArea.Find("ComboSlot_2");

                if (debugMode)
                {
                    Debug.Log($"[BattleUIManager] 조합 슬롯 찾기 완료:");
                    Debug.Log($"  - 속성 슬롯: {(elementSlot != null ? "찾음" : "없음")}");
                    Debug.Log($"  - 액티브1 슬롯: {(active1Slot != null ? "찾음" : "없음")}");
                    Debug.Log($"  - 액티브2 슬롯: {(active2Slot != null ? "찾음" : "없음")}");
                }

                // 각 슬롯에 드롭 존 컴포넌트 추가
                SetupDropZone(elementSlot, CardType.Element);
                SetupDropZone(active1Slot, CardType.Active1);
                SetupDropZone(active2Slot, CardType.Active2);

                // 조합 버튼들 찾기
                FindComboButtons();
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] ComboSlotArea를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 조합 버튼들 자동 찾기
        /// </summary>
        private void FindComboButtons()
        {
            Transform canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                canvas = GameObject.Find("Canvas")?.transform;
            }

            if (canvas != null)
            {
                if (executeComboButton == null)
                {
                    executeComboButton = FindButtonByName(canvas, "ExecuteComboButton") ??
                                       FindButtonByName(canvas, "UseComboButton") ??
                                       FindButtonByName(canvas, "ComboButton");
                }

                if (clearComboButton == null)
                {
                    clearComboButton = FindButtonByName(canvas, "ClearComboButton") ??
                                     FindButtonByName(canvas, "ResetComboButton") ??
                                     FindButtonByName(canvas, "ClearButton");
                }

                if (debugMode)
                {
                    Debug.Log($"[BattleUIManager] 조합 버튼 찾기:");
                    Debug.Log($"  - 실행 버튼: {(executeComboButton != null ? executeComboButton.name : "없음")}");
                    Debug.Log($"  - 초기화 버튼: {(clearComboButton != null ? clearComboButton.name : "없음")}");
                }
            }
        }

        /// <summary>
        /// 이름으로 버튼 찾기
        /// </summary>
        private Button FindButtonByName(Transform parent, string buttonName)
        {
            var button = parent.Find(buttonName)?.GetComponent<Button>();
            if (button != null) return button;

            for (int i = 0; i < parent.childCount; i++)
            {
                button = FindButtonByName(parent.GetChild(i), buttonName);
                if (button != null) return button;
            }

            return null;
        }

        /// <summary>
        /// 드롭 존 설정
        /// </summary>
        private void SetupDropZone(Transform slot, CardType acceptedType)
        {
            if (slot == null) return;

            var dropZone = slot.GetComponent<CardDropZone>();
            if (dropZone == null)
            {
                dropZone = slot.gameObject.AddComponent<CardDropZone>();
            }

            dropZone.Initialize(acceptedType, OnCardDroppedToSlot);
        }

        /// <summary>
        /// 필드 슬롯 자동 찾기
        /// </summary>
        private void FindFieldSlots()
        {
            Transform fieldArea = transform.Find("Canvas/FieldArea");
            if (fieldArea == null)
            {
                fieldArea = GameObject.Find("FieldArea")?.transform;
            }

            if (fieldArea != null)
            {
                Transform slotsContainer = fieldArea.Find("FieldSlots");
                if (slotsContainer != null)
                {
                    fieldSlots = new Transform[slotsContainer.childCount];
                    for (int i = 0; i < slotsContainer.childCount && i < fieldSlots.Length; i++)
                    {
                        fieldSlots[i] = slotsContainer.GetChild(i);
                    }

                    // MonsterSpawnManager에 필드 슬롯 전달
                    if (MonsterSpawnManager.Instance != null)
                    {
                        MonsterSpawnManager.Instance.SetFieldSlots(fieldSlots);
                        if (debugMode)
                            Debug.Log($"[BattleUIManager] MonsterSpawnManager에 필드 슬롯 전달: {fieldSlots.Length}개");
                    }
                }
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 슬롯 찾기 완료: {fieldSlots?.Length ?? 0}개");
        }

        /// <summary>
        /// 보상 UI 찾기 및 설정
        /// </summary>
        private void FindRewardUI()
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] FindRewardUI 시작");

            // RewardSelectionUI 찾기
            var rewardUIObject = GameObject.Find("RewardSelectionUI");
            if (rewardUIObject != null)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] RewardSelectionUI 발견: {rewardUIObject.name}");

                rewardSelectionUI = rewardUIObject.GetComponent<CanvasGroup>();
                if (rewardSelectionUI == null)
                {
                    Debug.LogError("[BattleUIManager] RewardSelectionUI에 CanvasGroup이 없습니다!");
                    return;
                }

                if (debugMode)
                    Debug.Log($"[BattleUIManager] CanvasGroup 설정 완료, 현재 alpha: {rewardSelectionUI.alpha}");

                // 보상 슬롯들 찾기
                var rewardSlotsArea = rewardUIObject.transform.Find("MainPanel/RewardSlotsArea");
                if (rewardSlotsArea != null)
                {
                    if (debugMode)
                        Debug.Log("[BattleUIManager] RewardSlotsArea 발견");

                    for (int i = 0; i < 3; i++)
                    {
                        var slot = rewardSlotsArea.Find($"RewardSlot_{i}");
                        if (slot != null && i < rewardSlots.Length)
                        {
                            rewardSlots[i] = slot;

                            if (debugMode)
                                Debug.Log($"[BattleUIManager] RewardSlot_{i} 설정 완료");

                            // 보상 슬롯 버튼 이벤트 연결
                            var button = slot.GetComponent<Button>();
                            if (button != null)
                            {
                                int slotIndex = i; // 클로저 캡처를 위한 로컬 변수
                                button.onClick.RemoveAllListeners(); // 기존 리스너 제거
                                button.onClick.AddListener(() => OnRewardSlotClicked(slotIndex));

                                if (debugMode)
                                    Debug.Log($"[BattleUIManager] RewardSlot_{i} 버튼 이벤트 연결 완료");
                            }
                            else if (debugMode)
                            {
                                Debug.LogWarning($"[BattleUIManager] RewardSlot_{i}에 Button 컴포넌트가 없습니다!");
                            }
                        }
                        else if (debugMode)
                        {
                            Debug.LogWarning($"[BattleUIManager] RewardSlot_{i}를 찾을 수 없습니다!");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[BattleUIManager] RewardSlotsArea를 찾을 수 없습니다!");
                }

                // 건너뛰기 버튼 찾기
                skipRewardButton = rewardUIObject.transform.Find("MainPanel/SkipButton")?.GetComponent<Button>();
                if (skipRewardButton != null)
                {
                    skipRewardButton.onClick.RemoveAllListeners(); // 기존 리스너 제거
                    skipRewardButton.onClick.AddListener(OnSkipRewardClicked);

                    if (debugMode)
                        Debug.Log("[BattleUIManager] SkipButton 이벤트 연결 완료");
                }
                else if (debugMode)
                {
                    Debug.LogWarning("[BattleUIManager] SkipButton을 찾을 수 없습니다!");
                }

                if (debugMode)
                    Debug.Log($"[BattleUIManager] 보상 UI 찾기 완료: 슬롯 {rewardSlots.Count(s => s != null)}개");
            }
            else
            {
                Debug.LogError("[BattleUIManager] RewardSelectionUI를 찾을 수 없습니다! Tools > Generate Rewards UI로 생성하세요.");
            }
        }
        #endregion

        #region Card UI Management
        /// <summary>
        /// 카드가 슬롯에 드롭되었을 때 호출
        /// </summary>
        private void OnCardDroppedToSlot(GameObject cardUI, CardType slotType)
        {
            if (!isPlayerTurn || !isBattleActive) return;

            var cardData = cardUI.GetComponent<CardUIData>();
            if (cardData == null || cardData.CardInstance == null) return;

            var card = cardData.CardInstance;

            if (card.Type != slotType)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 카드 타입 불일치: {card.Type} != {slotType}");
                return;
            }

            PlaceCardInSlot(card, cardUI, slotType);
            RemoveCardFromHand(cardUI);

            if (debugMode)
                Debug.Log($"[BattleUIManager] {card.CardName}을(를) {slotType} 슬롯에 배치");

            UpdateComboUI();
        }

        /// <summary>
        /// 카드를 슬롯에 배치
        /// </summary>
        private void PlaceCardInSlot(Card card, GameObject cardUI, CardType slotType)
        {
            Transform targetSlot = null;

            switch (slotType)
            {
                case CardType.Element:
                    if (elementSlotCard != null && elementSlotUI != null)
                    {
                        ReturnCardToHand(elementSlotCard, elementSlotUI);
                    }
                    elementSlotCard = card;
                    elementSlotUI = cardUI;
                    targetSlot = elementSlot;
                    break;

                case CardType.Active1:
                    if (active1SlotCard != null && active1SlotUI != null)
                    {
                        ReturnCardToHand(active1SlotCard, active1SlotUI);
                    }
                    active1SlotCard = card;
                    active1SlotUI = cardUI;
                    targetSlot = active1Slot;
                    break;

                case CardType.Active2:
                    if (active2SlotCard != null && active2SlotUI != null)
                    {
                        ReturnCardToHand(active2SlotCard, active2SlotUI);
                    }
                    active2SlotCard = card;
                    active2SlotUI = cardUI;
                    targetSlot = active2Slot;
                    break;
            }

            if (targetSlot != null)
            {
                cardUI.transform.SetParent(targetSlot, false);
                cardUI.transform.localPosition = Vector3.zero;
                cardUI.transform.localScale = Vector3.one * 0.8f;

                var image = cardUI.GetComponent<Image>();
                if (image != null)
                {
                    image.color = Color.cyan;
                }
            }
        }

        /// <summary>
        /// 카드를 손패로 되돌리기
        /// </summary>
        private void ReturnCardToHand(Card card, GameObject cardUI)
        {
            if (cardUI != null && handContent != null)
            {
                cardUI.transform.SetParent(handContent, false);
                cardUI.transform.localScale = Vector3.one;

                var image = cardUI.GetComponent<Image>();
                if (image != null)
                {
                    image.color = Color.white;
                }

                if (!handCardUIs.Contains(cardUI))
                {
                    handCardUIs.Add(cardUI);
                }
            }
        }

        /// <summary>
        /// 손패에서 카드 제거
        /// </summary>
        private void RemoveCardFromHand(GameObject cardUI)
        {
            if (handCardUIs.Contains(cardUI))
            {
                handCardUIs.Remove(cardUI);
            }
        }

        /// <summary>
        /// 조합 슬롯 정리
        /// </summary>
        public void ClearComboSlots()
        {
            if (elementSlotCard != null && elementSlotUI != null)
            {
                ReturnCardToHand(elementSlotCard, elementSlotUI);
                elementSlotCard = null;
                elementSlotUI = null;
            }

            if (active1SlotCard != null && active1SlotUI != null)
            {
                ReturnCardToHand(active1SlotCard, active1SlotUI);
                active1SlotCard = null;
                active1SlotUI = null;
            }

            if (active2SlotCard != null && active2SlotUI != null)
            {
                ReturnCardToHand(active2SlotCard, active2SlotUI);
                active2SlotCard = null;
                active2SlotUI = null;
            }

            UpdateComboUI();

            if (debugMode)
                Debug.Log("[BattleUIManager] 조합 슬롯 정리 완료");
        }

        /// <summary>
        /// 조합 슬롯 UI만 정리
        /// </summary>
        public void ClearComboSlotsUIOnly()
        {
            if (elementSlotCard != null && elementSlotUI != null)
            {
                if (elementSlotUI.transform.parent != handContent)
                {
                    Destroy(elementSlotUI);
                }
                elementSlotCard = null;
                elementSlotUI = null;
            }

            if (active1SlotCard != null && active1SlotUI != null)
            {
                if (active1SlotUI.transform.parent != handContent)
                {
                    Destroy(active1SlotUI);
                }
                active1SlotCard = null;
                active1SlotUI = null;
            }

            if (active2SlotCard != null && active2SlotUI != null)
            {
                if (active2SlotUI.transform.parent != handContent)
                {
                    Destroy(active2SlotUI);
                }
                active2SlotCard = null;
                active2SlotUI = null;
            }

            UpdateComboUI();

            if (debugMode)
                Debug.Log("[BattleUIManager] 조합 슬롯 UI만 정리 완료");
        }

        /// <summary>
        /// 조합 UI 업데이트
        /// </summary>
        private void UpdateComboUI()
        {
            if (executeComboButton != null)
            {
                int comboCardCount = 0;
                if (elementSlotCard != null) comboCardCount++;
                if (active1SlotCard != null) comboCardCount++;
                if (active2SlotCard != null) comboCardCount++;

                bool canExecute = comboCardCount > 0 && isPlayerTurn && isBattleActive;

                if (comboCardCount == 1)
                {
                    Card singleCard = elementSlotCard ?? active1SlotCard ?? active2SlotCard;
                    if (singleCard != null && !singleCard.CardData.CanUseSolo)
                    {
                        canExecute = false;
                    }
                }

                executeComboButton.interactable = canExecute;
            }

            if (clearComboButton != null)
            {
                int comboCardCount = 0;
                if (elementSlotCard != null) comboCardCount++;
                if (active1SlotCard != null) comboCardCount++;
                if (active2SlotCard != null) comboCardCount++;

                clearComboButton.interactable = comboCardCount > 0;
            }
        }

        /// <summary>
        /// 카드 클릭 이벤트 처리
        /// </summary>
        private void HandleCardClicked(GameObject cardUI)
        {
            if (!isPlayerTurn || !isBattleActive) return;

            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null || cardUIData.CardInstance == null) return;

            Card card = cardUIData.CardInstance;

            if (IsCardInHand(cardUI))
            {
                MoveCardToComboSlot(card, cardUI);
            }
            else if (IsCardInComboSlot(cardUI))
            {
                ReturnCardToHandFromSlot(card, cardUI);
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 카드 클릭: {card.CardName}");
        }

        /// <summary>
        /// 카드가 손패에 있는지 확인
        /// </summary>
        private bool IsCardInHand(GameObject cardUI)
        {
            return cardUI.transform.parent == handContent;
        }

        /// <summary>
        /// 카드가 조합 슬롯에 있는지 확인
        /// </summary>
        private bool IsCardInComboSlot(GameObject cardUI)
        {
            Transform parent = cardUI.transform.parent;
            return parent == elementSlot || parent == active1Slot || parent == active2Slot;
        }

        /// <summary>
        /// 손패 카드를 적절한 조합 슬롯으로 이동
        /// </summary>
        private void MoveCardToComboSlot(Card card, GameObject cardUI)
        {
            CardType cardType = card.Type;
            Transform targetSlot = null;

            switch (cardType)
            {
                case CardType.Element:
                    targetSlot = elementSlot;
                    break;
                case CardType.Active1:
                    targetSlot = active1Slot;
                    break;
                case CardType.Active2:
                    targetSlot = active2Slot;
                    break;
                default:
                    if (debugMode)
                        Debug.Log($"[BattleUIManager] 조합에 사용할 수 없는 카드 타입: {cardType}");
                    return;
            }

            if (targetSlot == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] 대상 슬롯을 찾을 수 없습니다.");
                return;
            }

            ReturnExistingCardFromSlot(cardType);
            OnCardDroppedToSlot(cardUI, cardType);
        }

        /// <summary>
        /// 해당 타입 슬롯에 있던 기존 카드를 손패로 되돌림
        /// </summary>
        private void ReturnExistingCardFromSlot(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.Element:
                    if (elementSlotCard != null && elementSlotUI != null)
                    {
                        ReturnCardToHand(elementSlotCard, elementSlotUI);
                    }
                    break;
                case CardType.Active1:
                    if (active1SlotCard != null && active1SlotUI != null)
                    {
                        ReturnCardToHand(active1SlotCard, active1SlotUI);
                    }
                    break;
                case CardType.Active2:
                    if (active2SlotCard != null && active2SlotUI != null)
                    {
                        ReturnCardToHand(active2SlotCard, active2SlotUI);
                    }
                    break;
            }
        }

        /// <summary>
        /// 슬롯에 있는 카드를 손패로 되돌림
        /// </summary>
        private void ReturnCardToHandFromSlot(Card card, GameObject cardUI)
        {
            if (elementSlotCard == card)
            {
                elementSlotCard = null;
                elementSlotUI = null;
            }
            else if (active1SlotCard == card)
            {
                active1SlotCard = null;
                active1SlotUI = null;
            }
            else if (active2SlotCard == card)
            {
                active2SlotCard = null;
                active2SlotUI = null;
            }

            ReturnCardToHand(card, cardUI);
            UpdateComboUI();

            if (debugMode)
                Debug.Log($"[BattleUIManager] {card.CardName}을(를) 슬롯에서 손패로 되돌림");
        }
        #endregion

        #region UI Updates
        /// <summary>
        /// 모든 UI 업데이트
        /// </summary>
        public void UpdateAllUI()
        {
            UpdateHealthUI();
            UpdateManaUI();
            UpdateGoldUI();
            UpdateDeckCountUI();
            UpdateTurnUI();
            UpdateButtonStates();
        }

        /// <summary>
        /// 체력 UI 업데이트
        /// </summary>
        private void UpdateHealthUI()
        {
            if (healthText != null && PlayerManager.Instance != null)
            {
                healthText.text = $"체력: {PlayerManager.Instance.CurrentHealth}/{PlayerManager.Instance.MaxHealth}";
            }
        }

        /// <summary>
        /// 마나 UI 업데이트
        /// </summary>
        private void UpdateManaUI()
        {
            if (manaText != null && PlayerManager.Instance != null)
            {
                manaText.text = $"마나: {PlayerManager.Instance.CurrentMana}/{PlayerManager.Instance.MaxMana}";
            }
        }

        /// <summary>
        /// 골드 UI 업데이트
        /// </summary>
        private void UpdateGoldUI()
        {
            if (goldText != null && PlayerManager.Instance != null)
            {
                goldText.text = $"골드: {PlayerManager.Instance.CurrentGold}";
            }
        }

        /// <summary>
        /// 덱 카운트 UI 업데이트
        /// </summary>
        private void UpdateDeckCountUI()
        {
            if (deckCountText != null && CardManager.Instance != null)
            {
                deckCountText.text = $"덱: {CardManager.Instance.MainDeckCount}";
            }
        }

        /// <summary>
        /// 턴 UI 업데이트
        /// </summary>
        private void UpdateTurnUI()
        {
            if (turnIndicator != null)
            {
                string turnText = isPlayerTurn ? "플레이어 턴" : "몬스터 턴";
                turnIndicator.text = $"{turnText} (턴 {BattleManager.Instance?.TurnNumber ?? 1})";
                turnIndicator.color = isPlayerTurn ? Color.green : Color.red;
            }
        }

        /// <summary>
        /// 손패 UI 업데이트
        /// </summary>
        private void UpdateHandUI(List<Card> handCards)
        {
            if (handContent == null) return;

            ClearHandCardUIs();

            foreach (var card in handCards)
            {
                GameObject cardUI = CreateCardUI(card);
                if (cardUI != null)
                {
                    handCardUIs.Add(cardUI);
                }
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 손패 UI 업데이트: {handCards.Count}장");
        }

        /// <summary>
        /// 카드 UI 생성
        /// </summary>
        private GameObject CreateCardUI(Card card)
        {
            GameObject cardUI;

            if (cardUIPrefab != null)
            {
                cardUI = Instantiate(cardUIPrefab, handContent);
            }
            else
            {
                cardUI = CreateBasicCardUI(card);
            }

            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null)
            {
                cardUIData = cardUI.AddComponent<CardUIData>();
            }
            cardUIData.CardInstance = card;

            var cardDraggable = cardUI.GetComponent<CardDraggable>();
            if (cardDraggable == null)
            {
                cardDraggable = cardUI.AddComponent<CardDraggable>();
            }

            var button = cardUI.GetComponent<Button>();
            if (button == null)
            {
                button = cardUI.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCardClicked(cardUI));

            UpdateCardUIInfo(cardUI, card);

            return cardUI;
        }

        /// <summary>
        /// 기본 카드 UI 생성
        /// </summary>
        private GameObject CreateBasicCardUI(Card card)
        {
            GameObject cardUI = new GameObject($"Card_{card.CardName}");
            cardUI.transform.SetParent(handContent, false);

            Image cardImage = cardUI.AddComponent<Image>();
            cardImage.color = Color.white;

            RectTransform rect = cardUI.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80, 120);

            cardUI.AddComponent<Button>();

            GameObject nameText = new GameObject("CardName");
            nameText.transform.SetParent(cardUI.transform, false);

            TextMeshProUGUI nameTMP = nameText.AddComponent<TextMeshProUGUI>();
            nameTMP.text = card.CardName;
            nameTMP.fontSize = 8;
            nameTMP.color = Color.black;
            nameTMP.alignment = TextAlignmentOptions.Center;

            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.8f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            return cardUI;
        }

        /// <summary>
        /// 카드 UI 정보 업데이트
        /// </summary>
        private void UpdateCardUIInfo(GameObject cardUI, Card card)
        {
            var nameText = cardUI.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = card.CardName;
            }

            var costText = cardUI.transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                costText.text = $"비용: {card.CurrentManaCost}";
            }

            var descText = cardUI.transform.Find("CardDescription")?.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = card.Description;
            }

            var infoText = cardUI.transform.Find("CardInfo")?.GetComponent<TextMeshProUGUI>();
            if (infoText != null)
            {
                infoText.text = $"{card.Element} | {card.Type}";
            }
        }

        /// <summary>
        /// 손패 카드 UI 정리
        /// </summary>
        private void ClearHandCardUIs()
        {
            foreach (var cardUI in handCardUIs)
            {
                if (cardUI != null)
                {
                    Destroy(cardUI);
                }
            }
            handCardUIs.Clear();
        }

        /// <summary>
        /// 드로우 버튼 업데이트
        /// </summary>
        private void UpdateDrawButton(int cost)
        {
            if (drawButton != null)
            {
                var buttonText = drawButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"드로우 ({cost})";
                }
            }
        }

        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        private void UpdateButtonStates()
        {
            bool canAct = isPlayerTurn && isBattleActive;

            if (endTurnButton != null)
                endTurnButton.interactable = canAct;

            if (drawButton != null)
                drawButton.interactable = canAct;

            UpdateComboUI();
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 턴 변경 이벤트 처리
        /// </summary>
        private void OnTurnChanged(TurnType turnType, int turnNumber)
        {
            isPlayerTurn = (turnType == TurnType.Player);
            UpdateTurnUI();
            UpdateButtonStates();
        }

        /// <summary>
        /// 페이즈 변경 이벤트 처리
        /// </summary>
        private void OnPhaseChanged(BattlePhase phase)
        {
            isBattleActive = (phase == BattlePhase.PlayerTurn || phase == BattlePhase.EnemyTurn);
            UpdateButtonStates();
        }
        #endregion

        #region Public API
        /// <summary>
        /// 전투 상태 설정
        /// </summary>
        public void SetBattleState(bool active, bool playerTurn)
        {
            isBattleActive = active;
            isPlayerTurn = playerTurn;
            UpdateButtonStates();
        }

        /// <summary>
        /// 조합 슬롯 카드들 가져오기
        /// </summary>
        public List<Card> GetComboSlotCards()
        {
            var cards = new List<Card>();
            if (elementSlotCard != null) cards.Add(elementSlotCard);
            if (active1SlotCard != null) cards.Add(active1SlotCard);
            if (active2SlotCard != null) cards.Add(active2SlotCard);
            return cards;
        }

        /// <summary>
        /// 조합 슬롯 초기화
        /// </summary>
        public void ResetComboSlots()
        {
            ClearComboSlots();
        }
        #endregion

        #region Field UI Management
        /// <summary>
        /// 필드 변경 이벤트 처리
        /// </summary>
        public void OnFieldChanged(ElementType newField, ElementType previousField)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 변경: {previousField} -> {newField}");

            UpdateFieldUI(newField);
            UpdateFieldEffectTurnsDisplay();
        }

        /// <summary>
        /// 필드 효과 적용 이벤트 처리
        /// </summary>
        public void OnFieldEffectApplied(FieldEffectSO fieldEffect)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 효과 적용: {fieldEffect?.EffectName}");

            ShowFieldEffectUI(fieldEffect);
        }

        /// <summary>
        /// 필드 효과 제거 이벤트 처리
        /// </summary>
        public void OnFieldEffectRemoved(FieldEffectSO fieldEffect)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 효과 제거: {fieldEffect?.EffectName}");

            HideFieldEffectUI();
        }

        /// <summary>
        /// 필드 UI 업데이트
        /// </summary>
        private void UpdateFieldUI(ElementType fieldElement)
        {
            if (fieldAreaImage == null) return;

            // 필드 속성에 따른 기본 색상 설정
            Color fieldColor = GetFieldColor(fieldElement);
            fieldAreaImage.color = fieldColor;

            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 UI 업데이트: {fieldElement}, 색상: {fieldColor}");
        }

        /// <summary>
        /// 필드 속성에 따른 기본 색상 반환
        /// </summary>
        private Color GetFieldColor(ElementType fieldElement)
        {
            switch (fieldElement)
            {
                case ElementType.Fire:
                    return new Color(1f, 0.2f, 0.2f, 0.3f); // 반투명 빨간색
                case ElementType.Water:
                    return new Color(0.2f, 0.2f, 1f, 0.3f); // 반투명 파란색
                case ElementType.Grass:
                    return new Color(0.2f, 1f, 0.2f, 0.3f); // 반투명 초록색
                case ElementType.Light:
                    return new Color(1f, 1f, 0.2f, 0.3f); // 반투명 노란색
                case ElementType.Dark:
                    return new Color(0.4f, 0.2f, 0.8f, 0.3f); // 반투명 보라색
                default:
                    return new Color(1f, 1f, 1f, 0.1f); // 거의 투명한 흰색 (기본)
            }
        }

        /// <summary>
        /// 필드 효과 UI 표시
        /// </summary>
        private void ShowFieldEffectUI(FieldEffectSO fieldEffect)
        {
            if (fieldEffect == null) return;

            // 필드 효과의 배경색 적용 (FieldEffectSO의 색상 사용)
            if (fieldAreaImage != null)
            {
                fieldAreaImage.color = fieldEffect.BackgroundColor;
            }

            // 필드 효과 턴수 표시
            UpdateFieldEffectTurnsDisplay();

            if (debugMode)
                Debug.Log($"[BattleUIManager] 필드 효과 UI 표시: {fieldEffect.EffectName}, 색상: {fieldEffect.BackgroundColor}");
        }

        /// <summary>
        /// 필드 효과 UI 숨기기
        /// </summary>
        private void HideFieldEffectUI()
        {
            // 필드 배경색을 기본으로 되돌리기
            if (fieldAreaImage != null)
            {
                fieldAreaImage.color = GetFieldColor(ElementType.None);
            }

            // 필드 효과 턴수 텍스트 숨기기
            if (fieldEffectTurnsText != null)
            {
                fieldEffectTurnsText.text = "";
                fieldEffectTurnsText.gameObject.SetActive(false);
            }

            if (debugMode)
                Debug.Log("[BattleUIManager] 필드 효과 UI 숨기기");
        }

        /// <summary>
        /// 필드 효과 턴수 표시 업데이트
        /// </summary>
        private void UpdateFieldEffectTurnsDisplay()
        {
            if (fieldEffectTurnsText == null || FieldManager.Instance == null) return;

            var currentEffect = FieldManager.Instance.CurrentFieldEffect;
            var remainingTurns = FieldManager.Instance.RemainingTurns;

            if (currentEffect != null && remainingTurns > 0)
            {
                fieldEffectTurnsText.gameObject.SetActive(true);
                fieldEffectTurnsText.text = $"{currentEffect.EffectName}\n{remainingTurns}턴 남음";
                fieldEffectTurnsText.color = Color.white;
            }
            else if (currentEffect != null && currentEffect.IsPermanent)
            {
                fieldEffectTurnsText.gameObject.SetActive(true);
                fieldEffectTurnsText.text = $"{currentEffect.EffectName}\n영구";
                fieldEffectTurnsText.color = Color.yellow;
            }
            else
            {
                fieldEffectTurnsText.gameObject.SetActive(false);
            }
        }
        #endregion

        #region Field Slots Management
        /// <summary>
        /// 필드 슬롯 설정
        /// </summary>
        public void SetFieldSlots(Transform[] slots)
        {
            if (slots != null && slots.Length > 0)
            {
                fieldSlots = new Transform[slots.Length];
                System.Array.Copy(slots, fieldSlots, slots.Length);

                if (debugMode)
                    Debug.Log($"[BattleUIManager] 필드 슬롯 설정 완료: {slots.Length}개");
            }
        }

        /// <summary>
        /// 필드 슬롯 배열 반환
        /// </summary>
        public Transform[] GetFieldSlots()
        {
            return fieldSlots;
        }

        /// <summary>
        /// 특정 인덱스의 필드 슬롯 반환
        /// </summary>
        public Transform GetFieldSlot(int index)
        {
            if (index >= 0 && index < fieldSlots.Length)
            {
                return fieldSlots[index];
            }
            return null;
        }

        /// <summary>
        /// 필드 슬롯 개수 반환
        /// </summary>
        public int GetFieldSlotCount()
        {
            return fieldSlots.Length;
        }

        /// <summary>
        /// 필드 슬롯의 월드 위치 계산
        /// </summary>
        public Vector3 GetFieldSlotWorldPosition(int index)
        {
            var slot = GetFieldSlot(index);
            if (slot == null) return Vector3.zero;

            // RectTransform을 월드 좌표로 변환
            RectTransform rectTransform = slot as RectTransform;
            if (rectTransform != null)
            {
                Canvas canvas = slot.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Vector3 worldPos;

                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        Camera cam = Camera.main;
                        if (cam == null) return Vector3.zero;

                        Vector3[] corners = new Vector3[4];
                        rectTransform.GetWorldCorners(corners);

                        Vector3 center = (corners[0] + corners[2]) * 0.5f;
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(cam, center);
                        worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
                    }
                    else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                    {
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
                        worldPos = canvas.worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, canvas.worldCamera.nearClipPlane + 1f));
                    }
                    else
                    {
                        worldPos = rectTransform.position;
                    }

                    return worldPos;
                }
            }

            return slot.position;
        }
        #endregion

        #region Reward UI Management
        /// <summary>
        /// 보상 UI 표시
        /// </summary>
        public void ShowRewardUI()
        {
            if (rewardSelectionUI != null)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 보상 UI 표시 시작 - 현재 alpha: {rewardSelectionUI.alpha}");

                rewardSelectionUI.alpha = 1f;
                rewardSelectionUI.interactable = true;
                rewardSelectionUI.blocksRaycasts = true;

                if (debugMode)
                    Debug.Log($"[BattleUIManager] 보상 UI 표시 완료 - 변경된 alpha: {rewardSelectionUI.alpha}");
            }
            else
            {
                Debug.LogError("[BattleUIManager] rewardSelectionUI가 null입니다! Tools > Generate Rewards UI를 실행하세요.");
            }
        }

        /// <summary>
        /// 보상 UI 숨김
        /// </summary>
        public void HideRewardUI()
        {
            if (rewardSelectionUI != null)
            {
                rewardSelectionUI.alpha = 0f;
                rewardSelectionUI.interactable = false;
                rewardSelectionUI.blocksRaycasts = false;
                if (debugMode)
                    Debug.Log("[BattleUIManager] 보상 UI 숨김");
            }
        }

        /// <summary>
        /// 보상 슬롯 클릭 이벤트 처리
        /// </summary>
        private void OnRewardSlotClicked(int slotIndex)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 보상 슬롯 {slotIndex} 클릭");

            // 보상 선택 로직 구현
            // 예: 선택된 보상을 저장하거나 효과를 적용
            // 이 부분은 BattleManager에서 처리해야 합니다.
            // 여기서는 단순히 클릭 이벤트를 발생시키고 보상 선택은 BattleManager에서 처리
            OnRewardSelected?.Invoke(slotIndex);
        }

        /// <summary>
        /// 건너뛰기 보상 클릭 이벤트 처리
        /// </summary>
        private void OnSkipRewardClicked()
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] 건너뛰기 보상 클릭");

            // 건너뛰기 로직 구현
            // 예: 보상을 건너뛰고 다음 턴으로 진행
            OnRewardSkipped?.Invoke();
        }

        /// <summary>
        /// 보상 선택 이벤트
        /// </summary>
        public static event System.Action<int> OnRewardSelected;

        /// <summary>
        /// 보상 건너뛰기 이벤트
        /// </summary>
        public static event System.Action OnRewardSkipped;
        #endregion

        /// <summary>
        /// 새로운 전투 보상 데이터로 UI 업데이트
        /// </summary>
        public void UpdateBattleRewardUI(BattleRewardData rewardData)
        {
            if (rewardData == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] 보상 데이터가 null입니다.");
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[BattleUIManager] 전투 보상 UI 업데이트:");
                Debug.Log($"  - 골드: {rewardData.goldAmount}");
                Debug.Log($"  - 카드 선택지: {rewardData.cardChoices?.Length ?? 0}개");
                Debug.Log($"  - 유물 선택지: {rewardData.relicChoices?.Length ?? 0}개");
            }

            // 카드와 유물 보상을 하나의 배열로 합치기
            var allRewards = new System.Collections.Generic.List<RewardItem>();

            if (rewardData.cardChoices != null && rewardData.cardChoices.Length > 0)
            {
                allRewards.AddRange(rewardData.cardChoices);
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 카드 보상 {rewardData.cardChoices.Length}개 추가");
            }

            if (rewardData.relicChoices != null && rewardData.relicChoices.Length > 0)
            {
                allRewards.AddRange(rewardData.relicChoices);
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 유물 보상 {rewardData.relicChoices.Length}개 추가");
            }

            // 기존의 UpdateRewardUI 메서드를 사용하여 실제 UI 업데이트
            if (allRewards.Count > 0)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 총 {allRewards.Count}개 보상으로 UI 업데이트 시작");

                UpdateRewardUI(allRewards.ToArray());
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] 표시할 보상이 없습니다.");

                // 빈 배열로 UI 클리어
                UpdateRewardUI(new RewardItem[0]);
            }
        }

        /// <summary>
        /// 보상 데이터로 UI 업데이트 (기존 메서드 - 호환성 유지)
        /// </summary>
        public void UpdateRewardUI(RewardItem[] rewards)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] UpdateRewardUI 호출됨 - 보상 {rewards?.Length ?? 0}개, 슬롯 {rewardSlots?.Count(s => s != null) ?? 0}개");

            currentRewards = rewards;

            if (rewards == null || rewards.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] 표시할 보상이 없습니다.");
                return;
            }

            if (rewardSlots == null || rewardSlots.All(s => s == null))
            {
                Debug.LogError("[BattleUIManager] 보상 슬롯이 없습니다! FindRewardUI가 제대로 실행되지 않았습니다.");
                return;
            }

            // 각 슬롯에 보상 정보 표시
            for (int i = 0; i < rewardSlots.Length && i < rewards.Length; i++)
            {
                if (rewardSlots[i] != null)
                {
                    if (debugMode)
                        Debug.Log($"[BattleUIManager] 슬롯 {i} 업데이트: {rewards[i].rewardType}");
                    UpdateRewardSlot(rewardSlots[i], rewards[i]);
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"[BattleUIManager] 슬롯 {i}이 null입니다.");
                }
            }

            // 사용하지 않는 슬롯 숨기기
            for (int i = rewards.Length; i < rewardSlots.Length; i++)
            {
                if (rewardSlots[i] != null)
                {
                    rewardSlots[i].gameObject.SetActive(false);
                    if (debugMode)
                        Debug.Log($"[BattleUIManager] 슬롯 {i} 숨김");
                }
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 보상 UI 업데이트 완료: {rewards.Length}개 보상");
        }

        /// <summary>
        /// 개별 보상 슬롯 업데이트
        /// </summary>
        private void UpdateRewardSlot(Transform slot, RewardItem reward)
        {
            if (slot == null || reward == null) return;

            slot.gameObject.SetActive(true);

            // 보상 아이콘 업데이트
            var iconImage = slot.Find("RewardIcon")?.GetComponent<Image>();
            var nameText = slot.Find("RewardName")?.GetComponent<TextMeshProUGUI>();
            var descText = slot.Find("RewardDescription")?.GetComponent<TextMeshProUGUI>();

            switch (reward.rewardType)
            {
                case RewardType.Card:
                    UpdateCardReward(iconImage, nameText, descText, reward);
                    break;

                case RewardType.Relic:
                    UpdateRelicReward(iconImage, nameText, descText, reward);
                    break;
            }
        }

        // 골드 보상 UI 업데이트 메서드 - 더 이상 사용 안함 (골드는 자동 지급)
        /*
        private void UpdateGoldReward(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI descText, RewardItem reward)
        {
            // 구 골드 보상 처리 코드
        }
        */

        /// <summary>
        /// 카드 보상 UI 업데이트
        /// </summary>
        private void UpdateCardReward(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI descText, RewardItem reward)
        {
            if (reward.cardReward == null) return;

            if (iconImage != null)
            {
                // 카드 스프라이트가 있으면 사용, 없으면 기본 색상
                if (reward.cardReward.Image != null)
                {
                    iconImage.sprite = reward.cardReward.Image;
                    iconImage.color = Color.white;
                }
                else
                {
                    // 카드 타입에 따른 기본 색상
                    iconImage.color = GetCardTypeColor(reward.cardReward.Type);
                    iconImage.sprite = null;
                }
            }

            if (nameText != null)
            {
                nameText.text = reward.cardReward.CardName;
                nameText.color = Color.white;
            }

            if (descText != null)
            {
                descText.text = reward.cardReward.Description;
            }
        }

        /// <summary>
        /// 유물 보상 UI 업데이트
        /// </summary>
        private void UpdateRelicReward(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI descText, RewardItem reward)
        {
            if (reward.relicReward == null) return;

            if (iconImage != null)
            {
                // 유물 스프라이트가 있으면 사용, 없으면 기본 색상
                if (reward.relicReward.Image != null)
                {
                    iconImage.sprite = reward.relicReward.Image;
                    iconImage.color = Color.white;
                }
                else
                {
                    // 유물 타입에 따른 기본 색상
                    iconImage.color = GetRelicTypeColor(reward.relicReward.Type);
                    iconImage.sprite = null;
                }
            }

            if (nameText != null)
            {
                nameText.text = reward.relicReward.RelicName;
                nameText.color = GetRelicTypeColor(reward.relicReward.Type);
            }

            if (descText != null)
            {
                descText.text = reward.relicReward.Description;
            }
        }

        // 경험치 보상 UI 업데이트 메서드 - 더 이상 사용 안함
        /*
        private void UpdateExperienceReward(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI descText, RewardItem reward)
        {
            // 구 경험치 보상 처리 코드
        }
        */

        /// <summary>
        /// 카드 타입에 따른 색상 반환
        /// </summary>
        private Color GetCardTypeColor(CardType cardType)
        {
            return cardType switch
            {
                CardType.Element => Color.green,
                CardType.Active1 => Color.red,
                CardType.Active2 => Color.blue,
                _ => Color.gray
            };
        }

        /// <summary>
        /// 유물 타입에 따른 색상 반환
        /// </summary>
        private Color GetRelicTypeColor(RelicType relicType)
        {
            return relicType switch
            {
                RelicType.Common => Color.white,
                RelicType.Boss => Color.yellow,
                _ => Color.gray
            };
        }

        /// <summary>
        /// 현재 보상 목록 반환
        /// </summary>
        public RewardItem[] GetCurrentRewards()
        {
            return currentRewards;
        }

        /// <summary>
        /// 선택된 보상 반환
        /// </summary>
        public RewardItem GetSelectedReward(int slotIndex)
        {
            if (currentRewards != null && slotIndex >= 0 && slotIndex < currentRewards.Length)
            {
                return currentRewards[slotIndex];
            }
            return null;
        }
    }

    /// <summary>
    /// 카드 UI 데이터 컴포넌트
    /// </summary>
    public class CardUIData : MonoBehaviour
    {
        public Card CardInstance { get; set; }
    }
}