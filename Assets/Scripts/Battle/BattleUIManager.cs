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

        [Header("슬라이더 UI")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider manaSlider;

        [Header("타겟 UI")]
        [SerializeField] private GameObject targetEnemyPanel;
        [SerializeField] private Image targetEnemySprite;
        [SerializeField] private Slider targetEnemyHealthBar;
        [SerializeField] private TextMeshProUGUI targetEnemyHealthText;

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
        [SerializeField] private GameObject relicPrefab; // 유물 프리팹
        [SerializeField] private GameObject monsterPrefab;

        [Header("보상 UI")]
        [SerializeField] private CanvasGroup rewardSelectionUI;
        [SerializeField] private Transform[] rewardSlots = new Transform[3];
        [SerializeField] private Button skipRewardButton;
        #endregion

        #region Private Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

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
        private int lastHandCardCount = 0; // 이전 손패 카드 수 추적

        // 현재 보상 목록
        private RewardItem[] currentRewards;

        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                InitializeUIManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Update()
        {
            // 타겟 UI 실시간 업데이트 (체력 변화 반영)
            UpdateTargetUIRealtime();
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

            // TargetManager 이벤트 구독
            TargetManager.OnTargetChanged += OnTargetChanged;


            // CardDrawAnimationManager 이벤트 구독
            if (CardDrawAnimationManager.Instance != null)
            {
                CardDrawAnimationManager.OnCardDrawAnimationCompleted += OnCardDrawAnimationCompleted;
                CardDrawAnimationManager.OnAllCardDrawAnimationsCompleted += OnAllCardDrawAnimationsCompleted;
                CardDrawAnimationManager.OnCardArrangementCompleted += OnCardArrangementCompleted;
            }

            // 자체 이벤트 구독
            OnCardClicked += HandleCardClicked;

            // 주의: 초기 UI 업데이트는 SetUIReferences에서 ForceUpdatePlayerUI로 즉시 처리됨
        }

        /// <summary>
        /// 지연 후 UI 업데이트 (더 이상 사용하지 않음 - ForceUpdatePlayerUI로 대체됨)
        /// </summary>
        private System.Collections.IEnumerator UpdateUIAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            // 더 이상 사용하지 않음 - SetUIReferences에서 ForceUpdatePlayerUI로 즉시 처리
            // UpdateAllUI();
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

            // TargetManager 이벤트 구독 해제
            TargetManager.OnTargetChanged -= OnTargetChanged;


            // CardDrawAnimationManager 이벤트 구독 해제
            if (CardDrawAnimationManager.Instance != null)
            {
                CardDrawAnimationManager.OnCardDrawAnimationCompleted -= OnCardDrawAnimationCompleted;
                CardDrawAnimationManager.OnAllCardDrawAnimationsCompleted -= OnAllCardDrawAnimationsCompleted;
                CardDrawAnimationManager.OnCardArrangementCompleted -= OnCardArrangementCompleted;
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
            GameObject relicPrefab = null,
            GameObject monsterPrefab = null,
            Slider healthSlider = null,
            Slider manaSlider = null,
            GameObject targetEnemyPanel = null,
            Image targetEnemySprite = null,
            Slider targetEnemyHealthBar = null,
            TextMeshProUGUI targetEnemyHealthText = null)
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

            // 유물 프리팹 설정 (제공된 경우)
            if (relicPrefab != null)
            {
                this.relicPrefab = relicPrefab;
            }

            // 슬라이더 설정 (제공된 경우)
            if (healthSlider != null)
            {
                this.healthSlider = healthSlider;
            }

            if (manaSlider != null)
            {
                this.manaSlider = manaSlider;
            }

            // 타겟 UI 설정 (제공된 경우)
            if (targetEnemyPanel != null)
            {
                this.targetEnemyPanel = targetEnemyPanel;
                // 초기에는 패널을 비활성화
                this.targetEnemyPanel.SetActive(false);
            }

            if (targetEnemySprite != null)
            {
                this.targetEnemySprite = targetEnemySprite;
            }

            if (targetEnemyHealthBar != null)
            {
                this.targetEnemyHealthBar = targetEnemyHealthBar;
            }

            if (targetEnemyHealthText != null)
            {
                this.targetEnemyHealthText = targetEnemyHealthText;
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

            // ★ 중요: UI 참조 설정 직후 즉시 플레이어 정보 동기화
            ForceUpdatePlayerUI();
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            // 기존 리스너 제거 후 새로 등록하여 중복 방지
            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());
            }

            if (drawButton != null)
            {
                drawButton.onClick.RemoveAllListeners();
                drawButton.onClick.AddListener(() => OnDrawCardClicked?.Invoke());
            }

            if (executeComboButton != null)
            {
                executeComboButton.onClick.RemoveAllListeners();
                executeComboButton.onClick.AddListener(() =>
                {
                    if (debugMode)
                        Debug.Log("[BattleUIManager] 조합 실행 버튼 클릭됨");
                    OnExecuteComboClicked?.Invoke();
                });
            }

            if (clearComboButton != null)
            {
                clearComboButton.onClick.RemoveAllListeners();
                clearComboButton.onClick.AddListener(() => OnClearComboClicked?.Invoke());
            }
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
                // 크기 변경 제거: cardUI.transform.localScale = Vector3.one * 0.8f;

                // 색상 변경 제거:
                // var image = cardUI.GetComponent<Image>();
                // if (image != null)
                // {
                //     image.color = Color.cyan;
                // }
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
                // 크기 초기화는 유지 (원래 크기로 되돌리기 위해)
                cardUI.transform.localScale = Vector3.one;

                // 색상 초기화 제거 (원본 색상 유지):
                // var image = cardUI.GetComponent<Image>();
                // if (image != null)
                // {
                //     image.color = Color.white;
                // }

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
        /// 조합 슬롯 참조만 정리 (UI는 유지, 애니메이션용)
        /// </summary>
        public void ClearComboSlotReferences()
        {
            elementSlotCard = null;
            elementSlotUI = null;
            active1SlotCard = null;
            active1SlotUI = null;
            active2SlotCard = null;
            active2SlotUI = null;

            UpdateComboUI();

            if (debugMode)
                Debug.Log("[BattleUIManager] 조합 슬롯 참조만 정리 완료 (UI 유지)");
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
        /// 외부에서 카드 클릭 이벤트를 발생시키는 공개 메서드
        /// </summary>
        public void TriggerCardClicked(GameObject cardUI)
        {
            OnCardClicked?.Invoke(cardUI);
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

        #region Force UI Synchronization
        /// <summary>
        /// 플레이어 UI 강제 업데이트 (씬 로드 직후 즉시 동기화용)
        /// </summary>
        public void ForceUpdatePlayerUI()
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] 플레이어 UI 강제 업데이트 시작");

            // 플레이어 정보 즉시 동기화
            UpdateHealthUI();
            UpdateManaUI();
            UpdateGoldUI();
            UpdateDeckCountUI();
            UpdateTurnUI();
            UpdateButtonStates();

            // 타겟 UI 초기화 (약간의 지연을 두어 TargetManager 초기화 완료 대기)
            StartCoroutine(DelayedTargetUIUpdate());

            if (debugMode)
                Debug.Log("[BattleUIManager] 플레이어 UI 강제 업데이트 완료");
        }

        /// <summary>
        /// 지연된 타겟 UI 업데이트 (TargetManager 초기화 완료 대기)
        /// </summary>
        private System.Collections.IEnumerator DelayedTargetUIUpdate()
        {
            // TargetManager가 전투 준비를 완료할 때까지 대기
            float maxWaitTime = 2f; // 최대 2초 대기
            float waitTime = 0f;

            while (waitTime < maxWaitTime)
            {
                if (TargetManager.Instance != null && TargetManager.Instance.IsBattleReady && TargetManager.Instance.CurrentTarget != null)
                {
                    if (debugMode)
                        Debug.Log("[BattleUIManager] TargetManager 전투 준비 완료 감지, 타겟 UI 업데이트");

                    UpdateTargetUI();
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }

            // 타임아웃된 경우에도 한 번 시도
            if (debugMode)
                Debug.LogWarning("[BattleUIManager] DelayedTargetUIUpdate 타임아웃, 최종 시도");

            UpdateTargetUI();
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
            if (PlayerManager.Instance != null)
            {
                // 텍스트 업데이트
                if (healthText != null)
                {
                    healthText.text = $"{PlayerManager.Instance.CurrentHealth}/{PlayerManager.Instance.MaxHealth}";
                }

                // 슬라이더 업데이트
                if (healthSlider != null)
                {
                    float maxHealth = PlayerManager.Instance.MaxHealth;
                    float currentHealth = PlayerManager.Instance.CurrentHealth;

                    if (maxHealth > 0)
                    {
                        float healthRatio = currentHealth / maxHealth;
                        healthSlider.value = healthRatio;
                    }
                    else
                    {
                        healthSlider.value = 0f;
                    }
                }
            }
            else
            {
                // PlayerManager가 아직 초기화되지 않은 경우 기본값 표시
                if (healthText != null)
                {
                    healthText.text = "100/100"; // 기본값
                }
                if (healthSlider != null)
                {
                    healthSlider.value = 1f; // 100%
                }
            }
        }

        /// <summary>
        /// 마나 UI 업데이트
        /// </summary>
        private void UpdateManaUI()
        {
            if (PlayerManager.Instance != null)
            {
                // 텍스트 업데이트
                if (manaText != null)
                {
                    manaText.text = $"{PlayerManager.Instance.CurrentMana}/{PlayerManager.Instance.MaxMana}";
                }

                // 슬라이더 업데이트
                if (manaSlider != null)
                {
                    float maxMana = PlayerManager.Instance.MaxMana;
                    float currentMana = PlayerManager.Instance.CurrentMana;

                    if (maxMana > 0)
                    {
                        float manaRatio = currentMana / maxMana;
                        manaSlider.value = manaRatio;
                    }
                    else
                    {
                        manaSlider.value = 0f;
                    }
                }
            }
            else
            {
                // PlayerManager가 아직 초기화되지 않은 경우 기본값 표시
                if (manaText != null)
                {
                    manaText.text = "3/3"; // 기본값
                }
                if (manaSlider != null)
                {
                    manaSlider.value = 1f; // 100%
                }
            }
        }

        /// <summary>
        /// 골드 UI 업데이트
        /// </summary>
        private void UpdateGoldUI()
        {
            if (goldText != null)
            {
                if (PlayerManager.Instance != null)
                {
                    goldText.text = $"{PlayerManager.Instance.CurrentGold}";
                }
                else
                {
                    // PlayerManager가 아직 초기화되지 않은 경우 기본값 표시
                    goldText.text = "100"; // 기본값
                }
            }
        }

        /// <summary>
        /// 덱 카운트 UI 업데이트
        /// </summary>
        private void UpdateDeckCountUI()
        {
            if (deckCountText != null)
            {
                if (CardManager.Instance != null)
                {
                    deckCountText.text = $"덱: {CardManager.Instance.MainDeckCount}";
                }
                else
                {
                    // CardManager가 아직 초기화되지 않은 경우 기본값 표시
                    deckCountText.text = "덱: 30"; // 기본값
                }
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
                int turnNumber = BattleManager.Instance?.TurnNumber ?? 1;
                turnIndicator.text = $"{turnText} (턴 {turnNumber})";
                turnIndicator.color = isPlayerTurn ? Color.green : Color.red;
            }
        }

        /// <summary>
        /// 손패 UI 업데이트
        /// </summary>
        private void UpdateHandUI(List<Card> handCards)
        {
            UpdateHandUI(handCards, false);
        }

        /// <summary>
        /// 손패 UI 업데이트 (애니메이션 강제 비활성화 옵션 포함)
        /// </summary>
        private void UpdateHandUI(List<Card> handCards, bool forceNoAnimation)
        {
            if (handContent == null) return;

            // 카드 수 변화 감지 (드로우 vs 사용/제거)
            bool isCardIncrease = handCards.Count > lastHandCardCount;
            int cardCountDiff = handCards.Count - lastHandCardCount;
            lastHandCardCount = handCards.Count;

            // 강제로 애니메이션 없이 업데이트하거나, 애니메이션 매니저가 없거나 카드가 없는 경우
            // 또는 카드 수가 감소한 경우 (카드 사용/제거시)
            if (forceNoAnimation || CardDrawAnimationManager.Instance == null || handCards.Count == 0 || !isCardIncrease)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 손패 UI 업데이트 (직접 모드): {handCards.Count}장 (증가: {isCardIncrease})");

                UpdateHandUIWithoutAnimation(handCards);
                return;
            }

            // 이미 애니메이션이 진행 중이면 건너뛰기 (카드 사용 후 재실행 방지)
            if (CardDrawAnimationManager.Instance.IsAnimating)
            {
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 애니메이션 진행 중이므로 손패 UI 업데이트 건너뜀");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 손패 UI 업데이트 (애니메이션 모드): {handCards.Count}장, 증가분: {cardCountDiff}");

            // 카드가 1-2장 추가된 경우 (드로우 버튼 등) 새로운 카드만 애니메이션
            if (isCardIncrease && cardCountDiff <= 2 && handCardUIs.Count > 0)
            {
                // 새로 추가된 카드들만 가져오기
                var newCards = handCards.GetRange(handCards.Count - cardCountDiff, cardCountDiff);

                if (debugMode)
                    Debug.Log($"[BattleUIManager] 새 카드 드로우 애니메이션: {newCards.Count}장");

                // 덱 카운트 및 버튼 상태 업데이트
                UpdateDeckCountUI();
                UpdateButtonStates();

                // 새로운 카드만 드로우 애니메이션 실행
                CardDrawAnimationManager.Instance.PlayNewCardDrawAnimation(newCards);
            }
            else
            {
                // 전체 손패 다시 드로우 (게임 시작, 턴 시작 등)
                if (debugMode)
                    Debug.Log($"[BattleUIManager] 전체 카드 드로우 애니메이션: {handCards.Count}장");

                // 기존 UI 정리
                ClearHandCardUIs();

                // 덱 카운트 및 버튼 상태 업데이트
                UpdateDeckCountUI();
                UpdateButtonStates();

                // 전체 카드 드로우 애니메이션 실행
                CardDrawAnimationManager.Instance.PlayCardDrawAnimation(handCards);
            }
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

            // TempCardParent 오브젝트들도 정리 (드로우 애니메이션 중단 시 남아있을 수 있음)
            ClearTempCardParents();
        }

        /// <summary>
        /// TempCardParent 오브젝트들 정리 (드로우 애니메이션 중단 시 남아있을 수 있는 임시 부모들)
        /// </summary>
        private void ClearTempCardParents()
        {
            if (handContent == null) return;

            // handContent와 같은 부모 하위에서 TempCardParent로 시작하는 오브젝트들 찾아서 제거
            Transform parentTransform = handContent.parent;
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
                            Debug.Log($"[BattleUIManager] TempCardParent 정리: {tempParent.name}");
                        Destroy(tempParent.gameObject);
                    }
                }

                if (tempParents.Count > 0 && debugMode)
                    Debug.Log($"[BattleUIManager] TempCardParent {tempParents.Count}개 정리 완료");
            }

            // handContent 직하위에 있을 수 있는 TempCardParent들도 정리
            var directTempParents = new List<Transform>();
            for (int i = 0; i < handContent.childCount; i++)
            {
                var child = handContent.GetChild(i);
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
                        Debug.Log($"[BattleUIManager] HandContent 직하위 TempCardParent 정리: {tempParent.name}");
                    Destroy(tempParent.gameObject);
                }
            }
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

        /// <summary>
        /// 타겟 UI 업데이트
        /// </summary>
        private void UpdateTargetUI()
        {
            // TargetManager에서 현재 타겟 가져오기
            var currentTarget = TargetManager.Instance?.CurrentTarget;

            if (currentTarget != null && currentTarget.IsAlive)
            {
                // 전체 패널 활성화 (TargetManager의 전투 준비가 완료된 경우에만)
                bool shouldShowPanel = TargetManager.Instance != null && TargetManager.Instance.IsBattleReady;

                if (targetEnemyPanel != null && shouldShowPanel)
                {
                    targetEnemyPanel.SetActive(true);
                }

                // 타겟 스프라이트 업데이트
                if (targetEnemySprite != null)
                {
                    // 항상 Idle의 0번 스프라이트 사용
                    if (currentTarget.EnemyData != null)
                    {
                        Sprite spriteToShow = null;
                        
                        // Idle 스프라이트의 첫 번째 프레임 사용
                        if (currentTarget.EnemyData.IdleSprites != null && 
                            currentTarget.EnemyData.IdleSprites.Length > 0 && 
                            currentTarget.EnemyData.IdleSprites[0] != null)
                        {
                            spriteToShow = currentTarget.EnemyData.IdleSprites[0];
                        }
                        else if (currentTarget.EnemyData.Sprite != null)
                        {
                            // 호환성을 위해 기본 스프라이트 사용
                            spriteToShow = currentTarget.EnemyData.Sprite;
                        }

                        if (spriteToShow != null)
                        {
                            targetEnemySprite.sprite = spriteToShow;
                            
                            // 좌우반전 설정 적용
                            var rectTransform = targetEnemySprite.rectTransform;
                            if (rectTransform != null)
                            {
                                Vector3 scale = rectTransform.localScale;
                                scale.x = currentTarget.EnemyData.FlipSpritesHorizontally ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                                rectTransform.localScale = scale;
                            }
                            
                            // EnemySO의 색상 설정 반영
                            targetEnemySprite.color = currentTarget.EnemyData.Color;
                            
                            if (debugMode)
                                Debug.Log($"[BattleUIManager] 타겟 스프라이트 업데이트: {spriteToShow.name} (flipX: {currentTarget.EnemyData.FlipSpritesHorizontally})");
                        }
                    }
                    else
                    {
                        // EnemyData가 없는 경우 폴백
                        var spriteRenderer = currentTarget.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null && spriteRenderer.sprite != null)
                        {
                            targetEnemySprite.sprite = spriteRenderer.sprite;
                            targetEnemySprite.color = spriteRenderer.color;
                        }
                    }
                }

                // 타겟 체력 정보 업데이트
                float maxHealth = currentTarget.MaxHealth;
                float currentHealth = currentTarget.CurrentHealth;

                // 체력바 업데이트
                if (targetEnemyHealthBar != null && maxHealth > 0)
                {
                    float healthRatio = currentHealth / maxHealth;
                    targetEnemyHealthBar.value = healthRatio;
                }

                // 체력 텍스트 업데이트
                if (targetEnemyHealthText != null)
                {
                    targetEnemyHealthText.text = $"{(int)currentHealth}/{(int)maxHealth}";
                }

                if (debugMode)
                    Debug.Log($"[BattleUIManager] 타겟 UI 업데이트: {currentTarget.EnemyName} (체력: {currentHealth}/{maxHealth}) 패널 표시: {shouldShowPanel}");
            }
            else
            {
                // 타겟이 없거나 죽은 경우 전체 패널 숨기기
                if (targetEnemyPanel != null)
                {
                    targetEnemyPanel.SetActive(false);
                }

                if (debugMode)
                    Debug.Log("[BattleUIManager] 타겟이 없어서 타겟 패널 숨김");
            }
        }

        /// <summary>
        /// 타겟 UI 실시간 업데이트 (체력 변화만)
        /// </summary>
        private void UpdateTargetUIRealtime()
        {
            // TargetManager에서 현재 타겟 가져오기
            var currentTarget = TargetManager.Instance?.CurrentTarget;

            if (currentTarget != null && currentTarget.IsAlive && targetEnemyPanel != null && targetEnemyPanel.activeInHierarchy)
            {
                // 체력 정보 가져오기
                float maxHealth = currentTarget.MaxHealth;
                float currentHealth = currentTarget.CurrentHealth;

                if (maxHealth > 0)
                {
                    // 체력바 실시간 업데이트 (변화가 있을 때만)
                    if (targetEnemyHealthBar != null)
                    {
                        float healthRatio = currentHealth / maxHealth;
                        if (Mathf.Abs(targetEnemyHealthBar.value - healthRatio) > 0.01f)
                        {
                            targetEnemyHealthBar.value = healthRatio;
                        }
                    }

                    // 체력 텍스트 실시간 업데이트 (변화가 있을 때만)
                    if (targetEnemyHealthText != null)
                    {
                        string newHealthText = $"{(int)currentHealth}/{(int)maxHealth}";
                        if (targetEnemyHealthText.text != newHealthText)
                        {
                            targetEnemyHealthText.text = newHealthText;
                        }
                    }
                }
            }
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

        /// <summary>
        /// 타겟 변경 이벤트 처리
        /// </summary>
        private void OnTargetChanged(Maglin.Enemy.Enemy target)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 타겟 변경: {target?.EnemyName ?? "없음"}");

            UpdateTargetUI();
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
        /// 조합 슬롯 UI 가져오기 (무덤 애니메이션용)
        /// </summary>
        public GameObject GetElementSlotUI() { return elementSlotUI; }
        public GameObject GetActive1SlotUI() { return active1SlotUI; }
        public GameObject GetActive2SlotUI() { return active2SlotUI; }

        /// <summary>
        /// 조합 슬롯 초기화
        /// </summary>
        public void ResetComboSlots()
        {
            ClearComboSlots();
        }

        /// <summary>
        /// 타겟 UI 강제 업데이트 (외부에서 호출 가능)
        /// </summary>
        public void ForceUpdateTargetUI()
        {
            UpdateTargetUI();
        }

        /// <summary>
        /// 타겟 UI 표시/숨김
        /// </summary>
        public void SetTargetUIVisible(bool visible)
        {
            if (targetEnemyPanel != null)
            {
                targetEnemyPanel.SetActive(visible);
            }

            if (debugMode)
                Debug.Log($"[BattleUIManager] 타겟 UI 패널 표시/숨김: {visible}");
        }

        /// <summary>
        /// TempCardParent 정리 (외부에서 호출 가능)
        /// </summary>
        public void ClearTempCardParentsPublic()
        {
            ClearTempCardParents();
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
        /// 개별 보상 슬롯 업데이트 (프리팹 사용)
        /// </summary>
        private void UpdateRewardSlot(Transform slot, RewardItem reward)
        {
            if (slot == null || reward == null) return;

            slot.gameObject.SetActive(true);

            // 기존 자식 오브젝트들 정리
            ClearRewardSlotContent(slot);

            GameObject rewardUI = null;

            switch (reward.rewardType)
            {
                case RewardType.Card:
                    rewardUI = CreateCardRewardUI(slot, reward);
                    break;

                case RewardType.Relic:
                    rewardUI = CreateRelicRewardUI(slot, reward);
                    break;
            }

            if (rewardUI != null && debugMode)
            {
                Debug.Log($"[BattleUIManager] 보상 UI 생성 완료: {reward.rewardType} - {reward.GetRewardName()}");
            }
        }

        /// <summary>
        /// 보상 슬롯 내용 정리
        /// </summary>
        private void ClearRewardSlotContent(Transform slot)
        {
            // CardUIPrefab(Clone)이나 RelicPrefab(Clone) 같은 프리팹 인스턴스만 제거
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var child = slot.GetChild(i);
                if (child.name.Contains("Prefab") || child.name.Contains("Clone"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 카드 보상 UI 생성 (CardUIPrefab 사용)
        /// </summary>
        private GameObject CreateCardRewardUI(Transform slot, RewardItem reward)
        {
            if (cardUIPrefab == null || reward.cardReward == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] CardUIPrefab이 없거나 카드 보상이 null입니다.");
                return null;
            }

            // CardUIPrefab 인스턴스화
            GameObject cardUI = Instantiate(cardUIPrefab, slot);

            // 카드 데이터 설정
            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null)
            {
                cardUIData = cardUI.AddComponent<CardUIData>();
            }

            // Card 인스턴스 생성 (보상용)
            var cardInstance = new Card(reward.cardReward);
            cardUIData.CardInstance = cardInstance;

            // 카드 정보 업데이트
            UpdateCardUIInfo(cardUI, cardInstance);

            // 보상 선택 버튼 이벤트 설정
            var button = cardUI.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnRewardCardClicked(reward));
            }

            // 크기 조정 (보상 슬롯에 맞게)
            var rectTransform = cardUI.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            return cardUI;
        }

        /// <summary>
        /// 유물 보상 UI 생성 (RelicPrefab 사용)
        /// </summary>
        private GameObject CreateRelicRewardUI(Transform slot, RewardItem reward)
        {
            if (relicPrefab == null || reward.relicReward == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleUIManager] RelicPrefab이 없거나 유물 보상이 null입니다.");
                return null;
            }

            // RelicPrefab 인스턴스화
            GameObject relicUI = Instantiate(relicPrefab, slot);

            // 유물 정보 업데이트
            UpdateRelicUIInfo(relicUI, reward.relicReward);

            // 보상 선택 버튼 이벤트 설정 (RelicPanel에 Button 컴포넌트 추가)
            var relicPanel = relicUI.transform.Find("RelicPanel");
            if (relicPanel != null)
            {
                var button = relicPanel.GetComponent<Button>();
                if (button == null)
                {
                    button = relicPanel.gameObject.AddComponent<Button>();
                    button.targetGraphic = relicPanel.GetComponent<Image>();
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnRewardRelicClicked(reward));
            }

            // 크기 조정 (보상 슬롯에 맞게)
            var rectTransform = relicUI.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one * 0.8f; // 유물은 약간 작게
            }

            return relicUI;
        }

        /// <summary>
        /// 유물 UI 정보 업데이트
        /// </summary>
        private void UpdateRelicUIInfo(GameObject relicUI, RelicSO relicData)
        {
            if (relicUI == null || relicData == null) return;

            // RelicImage 업데이트
            var relicImage = relicUI.transform.Find("RelicPanel/RelicImage")?.GetComponent<Image>();
            if (relicImage != null && relicData.Image != null)
            {
                relicImage.sprite = relicData.Image;
                relicImage.color = Color.white;
            }

            // RelicDescription 업데이트
            var relicDesc = relicUI.transform.Find("RelicPanel/RelicDescription")?.GetComponent<TextMeshProUGUI>();
            if (relicDesc != null)
            {
                relicDesc.text = relicData.Description;
            }
        }

        /// <summary>
        /// 카드 보상 클릭 이벤트
        /// </summary>
        private void OnRewardCardClicked(RewardItem reward)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 카드 보상 선택: {reward.cardReward?.CardName}");

            OnRewardSelected?.Invoke(GetRewardSlotIndex(reward));
        }

        /// <summary>
        /// 유물 보상 클릭 이벤트
        /// </summary>
        private void OnRewardRelicClicked(RewardItem reward)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 유물 보상 선택: {reward.relicReward?.RelicName}");

            OnRewardSelected?.Invoke(GetRewardSlotIndex(reward));
        }

        /// <summary>
        /// 보상의 슬롯 인덱스 찾기
        /// </summary>
        private int GetRewardSlotIndex(RewardItem reward)
        {
            if (currentRewards != null)
            {
                for (int i = 0; i < currentRewards.Length; i++)
                {
                    if (currentRewards[i] == reward)
                        return i;
                }
            }
            return -1;
        }

        // 골드 보상 UI 업데이트 메서드 - 더 이상 사용 안함 (골드는 자동 지급)
        /*
        private void UpdateGoldReward(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI descText, RewardItem reward)
        {
            // 구 골드 보상 처리 코드
        }
        */



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

        #region Card Draw Animation Event Handlers
        /// <summary>
        /// 개별 카드 드로우 애니메이션 완료 이벤트 핸들러
        /// </summary>
        private void OnCardDrawAnimationCompleted(Card card, GameObject cardUI)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 카드 드로우 애니메이션 완료: {card?.CardName}");

            // 카드 UI를 손패 리스트에 추가
            if (cardUI != null && !handCardUIs.Contains(cardUI))
            {
                handCardUIs.Add(cardUI);
            }

            // 손패 레이아웃 업데이트 (spacing 포함) - 개별 카드 추가 시마다
            UpdateHandUILayout();

            // 필요시 추가 UI 업데이트
            UpdateDeckCountUI();
        }

        /// <summary>
        /// 모든 카드 드로우 애니메이션 완료 이벤트 핸들러
        /// </summary>
        private void OnAllCardDrawAnimationsCompleted(List<Card> cards)
        {
            if (debugMode)
                Debug.Log($"[BattleUIManager] 모든 카드 드로우 애니메이션 완료: {cards?.Count ?? 0}장");

            // 손패 레이아웃 업데이트 (spacing 포함)
            UpdateHandUILayout();

            // 전체 UI 업데이트
            UpdateAllUI();
        }

        /// <summary>
        /// 카드 재배치 애니메이션 완료 이벤트 핸들러
        /// </summary>
        private void OnCardArrangementCompleted()
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] 카드 재배치 애니메이션 완료");

            // 손패 UI 최종 정리
            UpdateHandUILayout();
        }

        /// <summary>
        /// 손패 UI 레이아웃 업데이트
        /// </summary>
        private void UpdateHandUILayout()
        {
            if (handContent == null) return;

            // 레이아웃 강제 업데이트
            var layoutGroup = handContent.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                // 손패 카드 수에 따른 동적 spacing 조정
                UpdateHandSpacing(layoutGroup);

                LayoutRebuilder.ForceRebuildLayoutImmediate(handContent as RectTransform);
            }

            // 버튼 상태 업데이트
            UpdateButtonStates();
        }

        /// <summary>
        /// 손패 카드 수에 따른 spacing 동적 조정
        /// </summary>
        private void UpdateHandSpacing(HorizontalLayoutGroup layoutGroup)
        {
            if (layoutGroup == null) return;

            int cardCount = handCardUIs.Count;

            // 기본 spacing: 5장일 때 -30
            // 카드가 늘어날 때마다 -30씩 추가: 6장(-60), 7장(-90), ...
            float baseSpacing = -30f; // 5장 기준
            float additionalSpacing = -20f; // 추가 카드당 spacing

            float newSpacing;
            if (cardCount <= 5)
            {
                newSpacing = baseSpacing;
            }
            else
            {
                int additionalCards = cardCount - 5;
                newSpacing = baseSpacing + (additionalSpacing * additionalCards);
            }

            layoutGroup.spacing = newSpacing;

            if (debugMode)
                Debug.Log($"[BattleUIManager] 손패 spacing 업데이트: {cardCount}장 -> spacing: {newSpacing}");
        }

        /// <summary>
        /// 애니메이션 없이 손패 UI 업데이트 (폴백용)
        /// </summary>
        public void UpdateHandUIWithoutAnimation(List<Card> handCards)
        {
            if (debugMode)
                Debug.Log("[BattleUIManager] 애니메이션 없이 손패 UI 업데이트");

            // 진행 중인 드로우 애니메이션 중단
            if (CardDrawAnimationManager.Instance != null)
            {
                CardDrawAnimationManager.Instance.StopAllAnimations();
            }

            // 기존 카드 UI 정리 (TempCardParent 포함)
            ClearHandCardUIs();

            // 새로운 카드 UI 생성
            foreach (var card in handCards)
            {
                var cardUI = CreateCardUI(card);
                handCardUIs.Add(cardUI);
            }

            // UI 업데이트 (spacing 포함)
            UpdateHandUILayout();
        }

        /// <summary>
        /// 애니메이션 없이 직접 손패 UI 업데이트 (폴백용)
        /// </summary>
        private void UpdateHandUIDirectly(List<Card> handCards)
        {
            if (handContent == null) return;

            if (debugMode)
                Debug.Log($"[BattleUIManager] 손패 UI 직접 업데이트: {handCards.Count}장");

            ClearHandCardUIs();

            foreach (var card in handCards)
            {
                GameObject cardUI = CreateCardUI(card);
                if (cardUI != null)
                {
                    handCardUIs.Add(cardUI);
                }
            }

            UpdateDeckCountUI();
        }

        /// <summary>
        /// 카드 사용 후 손패 UI 업데이트 (애니메이션 없이)
        /// </summary>
        public void UpdateHandUIAfterCardUsage()
        {
            if (CardManager.Instance != null)
            {
                var handCards = CardManager.Instance.HandCards.ToList();
                UpdateHandUI(handCards, true); // 강제로 애니메이션 없이 업데이트
            }
        }
        #endregion
    }

    /// <summary>
    /// 카드 UI 데이터 컴포넌트
    /// </summary>
    public class CardUIData : MonoBehaviour
    {
        public Card CardInstance { get; set; }
    }
}