using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Maglin.Core;
using Maglin.Player;
using Maglin.Cards;
using Maglin.Enemy;
using Maglin.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace Maglin.Battle
{
    /// <summary>
    /// 통합 전투 컨트롤러 (프로덕션 + 테스트)
    /// FloorManager와 연동하여 실제 게임 플로우에서 동작하며, 테스트 모드도 지원
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        [Header("Battle Data")]
        [SerializeField] private BattleStageSO currentBattleStage;

        [Header("Test Data (테스트 모드용)")]
        [SerializeField] private List<CardSO> testCards = new List<CardSO>();
        [SerializeField] private BattleStageSO testBattleStage;
        [SerializeField] private bool useTestMode = false; // 테스트 모드 활성화 여부

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // 전투 상태
        private bool isBattleActive = false;
        private bool isPlayerTurn = true;
        private int currentTurn = 1;
        private bool isInputBlocked = false;
        private bool pendingBattleEnd = false; // 사망 애니메이션 대기 중인 전투 종료
        private bool isCardInteractionBlocked = false; // 카드 상호작용 차단 여부

        // 현재 층 정보
        private int currentFloor = 1;
        private FloorType currentFloorType = FloorType.Normal;

        #region Unity Events
        private void Start()
        {
            if (useTestMode)
            {
                // 테스트 모드: 기존 테스트 초기화
                InitializeBattleTest();
            }
            else
            {
                // 프로덕션 모드: FloorManager 이벤트 구독 후 대기
                SubscribeToFloorManagerEvents();

                if (debugMode)
                {
                    Debug.Log("[BattleTestController] 프로덕션 모드 - FloorManager 신호 대기 중");
                    Debug.Log($"[BattleTestController] FloorManager 상태: {(FloorManager.Instance != null ? "존재함" : "null")}");
                    if (FloorManager.Instance != null)
                    {
                        Debug.Log($"[BattleTestController] 현재 층: {FloorManager.Instance.CurrentFloor}");
                        Debug.Log($"[BattleTestController] 현재 층 타입: {FloorManager.Instance.CurrentFloorType}");
                        Debug.Log($"[BattleTestController] 현재 배틀스테이지: {FloorManager.Instance.GetCurrentBattleStage()?.name ?? "null"}");
                    }
                }

                // 프로덕션 모드에서 FloorManager가 이미 층을 시작했는지 확인
                StartCoroutine(CheckFloorManagerState());
            }
        }

        private void Update()
        {
            HandleInput();
        }

        private void OnEnable()
        {
            // 매니저 이벤트 구독은 InitializeManagers 이후에 수행
            // SubscribeToManagerEvents();
        }

        private void OnDisable()
        {
            // 매니저 이벤트 구독 해제
            UnsubscribeFromManagerEvents();
        }

        private void OnDestroy()
        {
            // FloorManager 이벤트 구독 해제
            UnsubscribeFromFloorManagerEvents();

            // FieldManager 이벤트 구독 해제
            if (FieldManager.Instance != null)
            {
                FieldManager.OnFieldChanged -= OnFieldChanged;
                FieldManager.OnFieldEffectApplied -= OnFieldEffectApplied;
                FieldManager.OnFieldEffectRemoved -= OnFieldEffectRemoved;
            }

            // VFX 이벤트 구독 해제
            VFXEffectManager.OnVFXHit -= OnVFXHit;
            VFXEffectManager.OnCardAttackSequenceCompleted -= OnCardAttackSequenceCompleted;

            // MonsterDeathAnimationManager 이벤트 구독 해제
            if (MonsterDeathAnimationManager.Instance != null)
            {
                MonsterDeathAnimationManager.OnAllDeathAnimationsCompleted -= OnAllDeathAnimationsCompleted;
            }

            // PlayerManager 애니메이션 이벤트 구독 해제
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerAnimationChanged -= OnPlayerAnimationChanged;
            }
        }
        #endregion

        #region Card Interaction Control
        /// <summary>
        /// 모든 손패 카드의 드래그 기능 활성화/비활성화
        /// </summary>
        private void SetCardInteractionEnabled(bool enabled)
        {
            isCardInteractionBlocked = !enabled;

            if (debugMode)
                Debug.Log($"[BattleTestController] 카드 상호작용 {(enabled ? "활성화" : "비활성화")}");

            // BattleUIManager를 통해 손패의 모든 카드에 적용
            if (BattleUIManager.Instance != null)
            {
                SetHandCardDragEnabled(enabled);
            }
        }

        /// <summary>
        /// 손패 카드들의 드래그 기능 제어
        /// </summary>
        private void SetHandCardDragEnabled(bool enabled)
        {
            // BattleUIManager의 손패 카드 UI들에 접근
            var handCardUIs = GetHandCardUIs();

            if (handCardUIs != null)
            {
                foreach (var cardUI in handCardUIs)
                {
                    if (cardUI != null)
                    {
                        // CardDraggable 컴포넌트 찾아서 드래그 설정
                        var cardDraggable = cardUI.GetComponent<CardDraggable>();
                        if (cardDraggable != null)
                        {
                            cardDraggable.SetDragEnabled(enabled);
                        }

                        // Button 컴포넌트도 비활성화 (클릭 방지)
                        var button = cardUI.GetComponent<Button>();
                        if (button != null)
                        {
                            button.interactable = enabled;
                        }
                    }
                }

                if (debugMode)
                    Debug.Log($"[BattleTestController] {handCardUIs.Count}개 카드의 드래그 기능 {(enabled ? "활성화" : "비활성화")}");
            }
        }

        /// <summary>
        /// BattleUIManager에서 손패 카드 UI 리스트 가져오기 (리플렉션 사용)
        /// </summary>
        private List<GameObject> GetHandCardUIs()
        {
            if (BattleUIManager.Instance == null) return null;

            // 리플렉션을 사용하여 private 필드에 접근
            var field = typeof(BattleUIManager).GetField("handCardUIs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                return field.GetValue(BattleUIManager.Instance) as List<GameObject>;
            }

            return null;
        }
        #endregion

        #region Event Subscriptions
        /// <summary>
        /// FloorManager 이벤트 구독
        /// </summary>
        private void SubscribeToFloorManagerEvents()
        {
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnFloorStarted += OnFloorStarted;
                FloorManager.Instance.OnFloorCompleted += OnFloorCompleted;

                if (debugMode)
                    Debug.Log("[BattleTestController] FloorManager 이벤트 구독 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleTestController] FloorManager가 아직 없습니다. 나중에 구독 시도");
            }
        }

        /// <summary>
        /// FloorManager 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromFloorManagerEvents()
        {
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnFloorStarted -= OnFloorStarted;
                FloorManager.Instance.OnFloorCompleted -= OnFloorCompleted;
            }
        }

        /// <summary>
        /// 매니저 이벤트 구독
        /// </summary>
        private void SubscribeToManagerEvents()
        {
            // BattleUIManager 이벤트 구독
            BattleUIManager.OnExecuteComboClicked += ExecuteCombo;
            BattleUIManager.OnClearComboClicked += ClearComboSlots;
            BattleUIManager.OnEndTurnClicked += EndPlayerTurn;
            BattleUIManager.OnDrawCardClicked += DrawAdditionalCard;

            // TargetManager 이벤트 구독
            TargetManager.OnTargetChanged += OnTargetChanged;
            TargetManager.OnTargetDied += OnTargetDied;

            // 보상 UI 이벤트 구독
            BattleUIManager.OnRewardSelected += OnRewardSelected;
            BattleUIManager.OnRewardSkipped += OnRewardSkipped;

            // RewardManager 이벤트 구독 (필요시)
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnBattleRewardsGenerated += OnBattleRewardsGenerated;
                RewardManager.Instance.OnRewardSelected += OnRewardApplied;
                RewardManager.Instance.OnGoldReceived += OnGoldReceived;
                RewardManager.Instance.OnRewardsCompleted += OnRewardsCompleted;

                if (debugMode)
                    Debug.Log("[BattleTestController] RewardManager 이벤트 구독 완료");
            }
            else if (debugMode)
            {
                Debug.LogWarning("[BattleTestController] RewardManager가 없어서 이벤트 구독 실패");
            }
        }

        /// <summary>
        /// 매니저 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromManagerEvents()
        {
            // BattleUIManager 이벤트 구독 해제
            BattleUIManager.OnExecuteComboClicked -= ExecuteCombo;
            BattleUIManager.OnClearComboClicked -= ClearComboSlots;
            BattleUIManager.OnEndTurnClicked -= EndPlayerTurn;
            BattleUIManager.OnDrawCardClicked -= DrawAdditionalCard;

            // TargetManager 이벤트 구독 해제
            TargetManager.OnTargetChanged -= OnTargetChanged;
            TargetManager.OnTargetDied -= OnTargetDied;

            // 보상 UI 이벤트 구독 해제
            BattleUIManager.OnRewardSelected -= OnRewardSelected;
            BattleUIManager.OnRewardSkipped -= OnRewardSkipped;

            // RewardManager 이벤트 구독 해제
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnBattleRewardsGenerated -= OnBattleRewardsGenerated;
                RewardManager.Instance.OnRewardSelected -= OnRewardApplied;
                RewardManager.Instance.OnGoldReceived -= OnGoldReceived;
                RewardManager.Instance.OnRewardsCompleted -= OnRewardsCompleted;
            }
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// 입력 처리
        /// </summary>
        private void HandleInput()
        {
            if (!isBattleActive) return;

            // Tab 키로 타겟 변경
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TargetManager.Instance?.CycleTarget();
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 전투 테스트 초기화
        /// </summary>
        private void InitializeBattleTest()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 테스트 초기화 시작");

            // 매니저들 초기화
            InitializeManagers();

            // 테스트 모드에서만 테스트 데이터 설정
            if (useTestMode)
            {
                SetupTestData();
            }

            // ComboManager 강제 초기화
            InitializeComboManager();

            // 전투 시작
            StartBattle();
        }

        /// <summary>
        /// 매니저들 초기화
        /// </summary>
        private void InitializeManagers()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 매니저 초기화 시작");


            // GridFieldManager 초기화 (가장 먼저)
            if (GridFieldManager.Instance != null)
            {
                GridFieldManager.Instance.InitializeGridField();

                if (debugMode)
                    Debug.Log("[BattleTestController] GridFieldManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] GridFieldManager를 찾을 수 없습니다. GameMapGenerator를 사용하여 맵을 생성해주세요.");
            }

            // RewardManager 초기화 (BattleUIManager보다 먼저)
            InitializeRewardManager();

            // BattleUIManager 초기화
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.InitializeUIManager();

                if (debugMode)
                    Debug.Log("[BattleTestController] BattleUIManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] BattleUIManager.Instance가 null입니다!");
            }

            // PlayerBattleManager 초기화
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.InitializePlayerBattleManager();

                if (debugMode)
                    Debug.Log("[BattleTestController] PlayerBattleManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] PlayerBattleManager.Instance가 null입니다!");
            }

            // TargetManager 초기화
            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.InitializeTargetManager();
                // PlayerBattleManager에서 플레이어 그리드 위치 가져오기
                Vector2Int playerPos = PlayerBattleManager.Instance != null ?
                    PlayerBattleManager.Instance.GetPlayerGridPosition() : Vector2Int.zero;
                TargetManager.Instance.SetPlayerPosition(playerPos);

                if (debugMode)
                    Debug.Log("[BattleTestController] TargetManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] TargetManager.Instance가 null입니다!");
            }

            // MonsterSpawnManager 초기화
            if (MonsterSpawnManager.Instance != null)
            {
                MonsterSpawnManager.Instance.InitializeSpawnManager();
                // PlayerBattleManager에서 플레이어 그리드 위치 가져오기
                Vector2Int playerPos = PlayerBattleManager.Instance != null ?
                    PlayerBattleManager.Instance.GetPlayerGridPosition() : Vector2Int.zero;
                MonsterSpawnManager.Instance.SetPlayerPosition(playerPos);

                if (debugMode)
                    Debug.Log("[BattleTestController] MonsterSpawnManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] MonsterSpawnManager.Instance가 null입니다!");
            }

            // MonsterBattleManager 초기화
            if (MonsterBattleManager.Instance != null)
            {
                // PlayerBattleManager에서 플레이어 그리드 위치 가져오기
                Vector2Int playerPos = PlayerBattleManager.Instance != null ?
                    PlayerBattleManager.Instance.GetPlayerGridPosition() : Vector2Int.zero;
                MonsterBattleManager.Instance.SetPlayerPosition(playerPos);

                if (debugMode)
                    Debug.Log("[BattleTestController] MonsterBattleManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] MonsterBattleManager.Instance가 null입니다!");
            }

            // CardManager 초기화
            if (CardManager.Instance != null)
            {
                // CardManager는 자동으로 초기화되므로 덱 초기화만 수행
                CardManager.Instance.InitializeDeckForNewGame();

                if (debugMode)
                    Debug.Log("[BattleTestController] CardManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] CardManager.Instance가 null입니다!");
            }

            // PlayerBattleManager 초기화
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.InitializePlayerBattleManager();

                if (debugMode)
                    Debug.Log("[BattleTestController] PlayerBattleManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] PlayerBattleManager.Instance가 null입니다!");
            }

            // FieldManager 초기화 (프로덕션/테스트 모드 공통)
            InitializeFieldManager();

            // 모든 매니저 초기화 완료 후 이벤트 구독
            SubscribeToManagerEvents();

            // PlayerManager 애니메이션 이벤트 구독
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerAnimationChanged += OnPlayerAnimationChanged;
                if (debugMode)
                    Debug.Log("[BattleTestController] PlayerManager 애니메이션 이벤트 구독 완료");
            }

            // 씬 전환 후 UI 참조 재설정 (DontDestroyOnLoad로 인한 참조 무효화 방지)
            RefreshUIManagerReferences();
            RefreshCardDrawAnimationManagerReferences();

            if (debugMode)
                Debug.Log("[BattleTestController] 모든 매니저 초기화 완료");
        }

        /// <summary>
        /// RewardManager 초기화 및 설정
        /// </summary>
        private void InitializeRewardManager()
        {
            // RewardManager가 없으면 생성
            if (RewardManager.Instance == null)
            {
                GameObject rewardManagerObj = new GameObject("RewardManager");
                rewardManagerObj.AddComponent<RewardManager>();

                if (debugMode)
                    Debug.Log("[BattleTestController] RewardManager 생성됨");
            }

            // RewardManager 설정
            if (RewardManager.Instance != null)
            {
                SetupRewardManagerData();

                if (debugMode)
                    Debug.Log("[BattleTestController] RewardManager 초기화 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] RewardManager 생성 실패!");
            }
        }

        /// <summary>
        /// RewardManager 데이터 설정
        /// </summary>
        private void SetupRewardManagerData()
        {
            // Resources/Rewards에서 RewardConfig 로드
            var rewardConfig = Resources.Load<RewardConfigSO>("Rewards/RewardConfig");
            if (rewardConfig == null)
            {
                // ScriptableObjects/Rewards에서도 시도
                rewardConfig = Resources.Load<RewardConfigSO>("RewardConfig");
            }

            if (rewardConfig == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleTestController] RewardConfig를 Resources에서 찾을 수 없습니다. 기본 설정 사용");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleTestController] RewardConfig 로드 성공: {rewardConfig.name}");

            // RewardManager에 RewardConfig 설정
            RewardManager.Instance.SetRewardConfig(rewardConfig);



            if (debugMode)
                Debug.Log("[BattleTestController] RewardManager 데이터 설정 완료");
        }

        /// <summary>
        /// 기본 테스트 보상 데이터 설정
        /// </summary>
        private void SetupDefaultRewardData()
        {
            // Resources에서 기본 카드들 로드
            var defaultCards = Resources.LoadAll<CardSO>("Cards");

            if (defaultCards.Length > 0)
            {
                // 리플렉션을 사용하여 normalCardPool 설정
                var rewardManagerType = typeof(RewardManager);
                var normalCardPoolField = rewardManagerType.GetField("normalCardPool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var debugModeField = rewardManagerType.GetField("debugMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (normalCardPoolField != null)
                {
                    normalCardPoolField.SetValue(RewardManager.Instance, defaultCards);
                }
                if (debugModeField != null)
                {
                    debugModeField.SetValue(RewardManager.Instance, debugMode);
                }

                if (debugMode)
                    Debug.Log($"[BattleTestController] 기본 카드 풀 설정 완료: {defaultCards.Length}개");
            }
            else
            {
                Debug.LogWarning("[BattleTestController] 사용할 카드를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 기본 테스트 카드 생성
        /// </summary>
        private void CreateDefaultTestCards()
        {
            // Resources에서 기본 카드들을 로드하거나 더미 카드 생성
            var defaultCards = Resources.LoadAll<CardSO>("Cards");

            if (defaultCards.Length > 0)
            {
                // Resources에서 로드된 카드들을 덱에 추가
                foreach (var card in defaultCards)
                {
                    CardManager.Instance.AddCardToDeck(card);
                    if (debugMode)
                        Debug.Log($"[BattleTestController] 기본 카드 추가: {card.CardName}");
                }
            }
            else
            {
                // Resources에 카드가 없으면 더미 카드 생성
                CreateDummyTestCards();
            }
        }

        /// <summary>
        /// 더미 테스트 카드 생성 (Resources에 카드가 없을 때)
        /// </summary>
        private void CreateDummyTestCards()
        {
            // 더미 카드 데이터 생성 (실제로는 CardSO 에셋을 만들어야 함)
            if (debugMode)
                Debug.Log("[BattleTestController] Resources에 카드가 없으므로 더미 카드 생성");

            // TODO: 실제 CardSO 에셋을 생성하거나 기본 카드들을 Resources에 추가
            // 현재는 빈 덱으로 시작
        }

        /// <summary>
        /// 테스트 데이터 설정
        /// </summary>
        private void SetupTestData()
        {
            // CardManager에 테스트 카드 설정
            if (CardManager.Instance != null)
            {
                if (testCards.Count > 0)
                {
                    // 테스트 카드를 덱에 추가
                    foreach (var card in testCards)
                    {
                        CardManager.Instance.AddCardToDeck(card);
                        if (debugMode)
                            Debug.Log($"[BattleTestController] 덱에 카드 추가: {card.CardName}");
                    }

                    if (debugMode)
                        Debug.Log($"[BattleTestController] 총 {testCards.Count}장의 테스트 카드가 덱에 추가됨");
                }
                else
                {
                    // 기본 카드 생성 및 추가
                    CreateDefaultTestCards();
                    if (debugMode)
                        Debug.Log("[BattleTestController] 기본 테스트 카드 생성 및 추가");
                }

                // 카드 인스턴스 시스템 재동기화 (중요!)
                CardManager.Instance.InitializeCardInstanceSystem();
            }
            else
            {
                Debug.LogError("[BattleTestController] CardManager.Instance가 null입니다! 카드 드로우가 불가능합니다.");
            }
        }

        /// <summary>
        /// ComboManager 강제 초기화 (테스트용)
        /// </summary>
        private void InitializeComboManager()
        {
            if (ComboManager.Instance != null)
            {
                // ComboManager 디버그 모드 활성화
                var debugField = typeof(ComboManager).GetField("debugMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (debugField != null)
                {
                    debugField.SetValue(ComboManager.Instance, true);
                    if (debugMode)
                        Debug.Log("[BattleTestController] ComboManager 디버그 모드 활성화");
                }

                // 리플렉션을 사용하여 private 메서드 호출
                var methodInfo = typeof(ComboManager).GetMethod("LoadCombinationCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (methodInfo != null)
                {
                    methodInfo.Invoke(ComboManager.Instance, null);

                    if (debugMode)
                        Debug.Log("[BattleTestController] ComboManager 캐시 강제 로드 완료");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[BattleTestController] LoadCombinationCache 메서드를 찾을 수 없습니다.");
                }
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleTestController] ComboManager 인스턴스가 없습니다.");
            }
        }

        /// <summary>
        /// FieldManager 초기화
        /// </summary>
        private void InitializeFieldManager()
        {
            if (FieldManager.Instance == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleTestController] FieldManager 인스턴스가 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleTestController] FieldManager 초기화 시작 - 모드: {(useTestMode ? "테스트" : "프로덕션")}");

            // 필드 초기화 (전투 시작 시)
            FieldManager.Instance.ResetField();

            // 필드 변경 이벤트 구독
            FieldManager.OnFieldChanged += OnFieldChanged;
            FieldManager.OnFieldEffectApplied += OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved += OnFieldEffectRemoved;

            // VFX 이벤트 구독
            VFXEffectManager.OnVFXHit += OnVFXHit;
            VFXEffectManager.OnCardAttackSequenceCompleted += OnCardAttackSequenceCompleted;

            // MonsterDeathAnimationManager 이벤트 구독
            if (MonsterDeathAnimationManager.Instance != null)
            {
                MonsterDeathAnimationManager.OnAllDeathAnimationsCompleted += OnAllDeathAnimationsCompleted;
                if (debugMode)
                    Debug.Log("[BattleTestController] MonsterDeathAnimationManager 이벤트 구독 완료");
            }

            if (debugMode)
            {
                Debug.Log("[BattleTestController] FieldManager 이벤트 구독 완료");
                Debug.Log($"  - 현재 필드 상태: {FieldManager.Instance.CurrentFieldElement}");
            }

            if (debugMode)
                Debug.Log("[BattleTestController] FieldManager 초기화 완료");
        }
        #endregion

        #region Battle Flow
        /// <summary>
        /// 전투 시작
        /// </summary>
        private void StartBattle()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 시작");

            isBattleActive = true;
            isPlayerTurn = true;
            currentTurn = 1;

            // 매니저들에 전투 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 플레이어 위치 설정 (PlayerBattleManager를 통해)
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.SetPlayerGridPosition(new Vector2Int(0, 0));
            }

            // BattleInitializationSequence 시작
            StartCoroutine(ExecuteBattleInitialization());
        }

        /// <summary>
        /// 전투 초기화 시퀀스 실행
        /// </summary>
        private System.Collections.IEnumerator ExecuteBattleInitialization()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 초기화 시퀀스 시작");

            // 0단계: 플레이어 입장 애니메이션 (화면 왼쪽에서 걸어오기)
            if (PlayerBattleManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 플레이어 입장 애니메이션 시작");

                yield return StartCoroutine(PlayerBattleManager.Instance.PlayPlayerEntranceAnimation());

                if (debugMode)
                    Debug.Log("[BattleTestController] 플레이어 입장 애니메이션 완료");
            }

            // 1단계: 몬스터 스폰 (애니메이션 없이 생성만)
            yield return StartCoroutine(SpawnMonstersOnly());

            // 2단계: BattleInitializationSequence 실행 (몬스터 애니메이션 처리)
            if (BattleInitializationSequence.Instance != null)
            {
                // 초기화 시퀀스 실행
                var initTask = BattleInitializationSequence.Instance.InitializeBattleSequence();

                // Task 완료까지 대기
                while (!initTask.IsCompleted)
                {
                    yield return null;
                }

                if (debugMode)
                    Debug.Log("[BattleTestController] BattleInitializationSequence 완료");
            }
            else
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] BattleInitializationSequence가 없음 - 직접 초기화 진행");

                // 애니메이션 직접 처리
                if (MonsterSpawnManager.Instance != null)
                {
                    MonsterSpawnManager.Instance.ProcessPendingAnimations();
                    yield return new WaitForSeconds(1f);
                }
            }

            // 3단계: 타겟 설정 및 전투 준비
            SetupTargetsAndBattleReady();

            // 플레이어 턴 시작
            StartPlayerTurn();

            // UI 업데이트
            BattleUIManager.Instance?.UpdateAllUI();

            if (debugMode)
                Debug.Log("[BattleTestController] 전투 초기화 및 시작 완료");
        }

        /// <summary>
        /// 몬스터 스폰만 실행 (애니메이션 없이)
        /// </summary>
        private IEnumerator SpawnMonstersOnly()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 몬스터 스폰만 실행 시작");

            // 사용할 배틀 스테이지 결정 (프로덕션 vs 테스트 모드)
            BattleStageSO battleStageToUse = useTestMode ? testBattleStage : currentBattleStage;

            if (debugMode)
            {
                Debug.Log($"[BattleTestController] 사용할 배틀스테이지: {battleStageToUse?.name ?? "null"}");
                Debug.Log($"[BattleTestController] 모드: {(useTestMode ? "테스트" : "프로덕션")}");
                Debug.Log($"[BattleTestController] MonsterSpawnManager 상태: {(MonsterSpawnManager.Instance != null ? "존재함" : "null")}");
            }

            // MonsterSpawnManager를 통해 몬스터 스폰
            if (MonsterSpawnManager.Instance != null && battleStageToUse != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 몬스터 스폰 실행 중...");

                // BattleStage에 SpawnMonstersFromBattleStage 메서드가 있다면 사용
                var spawnMethod = typeof(MonsterSpawnManager).GetMethod("SpawnMonstersFromBattleStage");
                if (spawnMethod != null && !useTestMode)
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 프로덕션 몬스터 스폰 메서드 사용");
                    spawnMethod.Invoke(MonsterSpawnManager.Instance, new object[] { battleStageToUse });
                }
                else
                {
                    // 폴백: 테스트 몬스터 스폰 메서드 사용
                    if (debugMode)
                        Debug.Log("[BattleTestController] 테스트 몬스터 스폰 메서드 사용");
                    MonsterSpawnManager.Instance.SpawnTestMonsters(battleStageToUse);
                }
            }
            else
            {
                Debug.LogError($"[BattleTestController] 몬스터 스폰 실패! MonsterSpawnManager: {(MonsterSpawnManager.Instance != null ? "OK" : "NULL")}, BattleStage: {(battleStageToUse != null ? "OK" : "NULL")}");
            }

            // 스폰 완료까지 잠시 대기
            yield return new WaitForSeconds(0.1f);

            if (debugMode)
                Debug.Log("[BattleTestController] 몬스터 스폰만 실행 완료");
        }

        /// <summary>
        /// 타겟 설정 및 전투 준비
        /// </summary>
        private void SetupTargetsAndBattleReady()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 타겟 설정 및 전투 준비");

            // 타겟 설정
            if (TargetManager.Instance != null)
            {
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                }

                if (debugMode && TargetManager.Instance.CurrentTarget != null)
                {
                    Debug.Log($"[BattleTestController] 타겟 설정됨: {TargetManager.Instance.CurrentTarget.EnemyName}");
                }
            }

            if (debugMode)
                Debug.Log("[BattleTestController] 타겟 설정 및 전투 준비 완료");
        }

        /// <summary>
        /// 몬스터 스폰 후 타겟 설정 코루틴 (기존 메서드)
        /// </summary>
        private IEnumerator SpawnMonstersAndSetTarget()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 몬스터 스폰 시작");

            // MonsterSpawnAnimationManager의 모든 스폰 완료 이벤트 구독
            bool spawnCompleted = false;
            System.Action onSpawnCompleted = () => { spawnCompleted = true; };

            if (MonsterSpawnAnimationManager.Instance != null)
            {
                MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted += onSpawnCompleted;
            }

            // 사용할 배틀 스테이지 결정 (프로덕션 vs 테스트 모드)
            BattleStageSO battleStageToUse = useTestMode ? testBattleStage : currentBattleStage;

            if (debugMode)
            {
                Debug.Log($"[BattleTestController] 사용할 배틀스테이지: {battleStageToUse?.name ?? "null"}");
                Debug.Log($"[BattleTestController] 모드: {(useTestMode ? "테스트" : "프로덕션")}");
                Debug.Log($"[BattleTestController] MonsterSpawnManager 상태: {(MonsterSpawnManager.Instance != null ? "존재함" : "null")}");
            }

            // MonsterSpawnManager를 통해 몬스터 스폰
            if (MonsterSpawnManager.Instance != null && battleStageToUse != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 몬스터 스폰 실행 중...");

                // BattleStage에 SpawnMonstersFromBattleStage 메서드가 있다면 사용
                var spawnMethod = typeof(MonsterSpawnManager).GetMethod("SpawnMonstersFromBattleStage");
                if (spawnMethod != null && !useTestMode)
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 프로덕션 몬스터 스폰 메서드 사용");
                    spawnMethod.Invoke(MonsterSpawnManager.Instance, new object[] { battleStageToUse });
                }
                else
                {
                    // 폴백: 테스트 몬스터 스폰 메서드 사용
                    if (debugMode)
                        Debug.Log("[BattleTestController] 테스트 몬스터 스폰 메서드 사용");
                    MonsterSpawnManager.Instance.SpawnTestMonsters(battleStageToUse);
                }
            }
            else
            {
                Debug.LogError($"[BattleTestController] 몬스터 스폰 실패! MonsterSpawnManager: {(MonsterSpawnManager.Instance != null ? "OK" : "NULL")}, BattleStage: {(battleStageToUse != null ? "OK" : "NULL")}");
            }

            // 몬스터 스폰 애니메이션 완료 대기
            if (MonsterSpawnAnimationManager.Instance != null)
            {
                yield return new WaitUntil(() => spawnCompleted);
                MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted -= onSpawnCompleted;

                if (debugMode)
                    Debug.Log("[BattleTestController] 몬스터 스폰 애니메이션 완료");
            }
            else
            {
                // 애니메이션 매니저가 없으면 기본 대기
                yield return new WaitForSeconds(1f);
            }

            // 타겟 설정
            if (TargetManager.Instance != null)
            {
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                    if (debugMode)
                        Debug.Log("[BattleTestController] 초기 타겟 설정 완료");
                }
            }
        }

        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        private void StartPlayerTurn()
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 플레이어 턴 {currentTurn} 시작");

            isPlayerTurn = true;

            // 몬스터 이동 플래그 초기화 (새로운 플레이어 턴 시작)
            if (MonsterBattleManager.Instance != null)
            {
                MonsterBattleManager.Instance.ResetAllMonsterMoveFlags();
            }

            // 매니저들에 턴 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 첫 번째 턴 처리
            if (currentTurn == 1)
            {
                // 이벤트 전투인 경우 초기화 시퀀스 실행
                bool isEventBattle = FloorManager.Instance != null && FloorManager.Instance.IsEventBattleMode;
                if (isEventBattle && BattleInitializationSequence.Instance != null)
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 이벤트 전투 첫 턴 - 초기화 시퀀스 실행");

                    // 초기화 시퀀스를 실행 (몬스터 스폰 애니메이션 + 카드 드로우)
                    _ = BattleInitializationSequence.Instance.InitializeBattleSequence();
                    return; // 초기화 시퀀스가 완료되면 자동으로 턴이 계속됨
                }
                else if (debugMode)
                {
                    Debug.Log("[BattleTestController] 첫 번째 턴이므로 카드 드로우 생략 (초기화 시퀀스에서 처리됨)");
                }
            }
            // 첫 번째 턴이 아닐 때만 카드 드로우
            else if (CardManager.Instance != null)
            {
                // PlayerManager의 MaxHandSize까지 드로우
                var drawnCards = CardManager.Instance.DrawCardsToMax();

                if (debugMode)
                {
                    int maxHandSize = PlayerManager.Instance?.MaxHandSize ?? 5;
                    Debug.Log($"[BattleTestController] {drawnCards.Count}장 드로우 완료 (최대 손패: {maxHandSize}장)");
                }
            }

            // 조합 슬롯 초기화
            BattleUIManager.Instance?.ResetComboSlots();

            // 타겟 검증 (죽은 몬스터가 타겟인 경우 재설정)
            TargetManager.Instance?.ValidateTarget();
        }

        /// <summary>
        /// 플레이어 턴 종료
        /// </summary>
        public void EndPlayerTurn()
        {
            if (!isPlayerTurn || !isBattleActive)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 플레이어 턴이 아니거나 전투가 활성화되지 않음");
                return;
            }

            if (debugMode)
                Debug.Log("[BattleTestController] 플레이어 턴 종료");

            isPlayerTurn = false;

            // 매니저들에 턴 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 손패 카드들 덱으로 복귀 및 셔플
            if (CardManager.Instance != null)
            {
                CardManager.Instance.EndTurnProcessing();
            }

            // 필드 효과 턴 종료 처리
            if (FieldManager.Instance != null)
            {
                FieldManager.Instance.OnTurnEnd();
            }

            // 몬스터 턴 시작
            StartCoroutine(ProcessMonsterTurn());
        }

        /// <summary>
        /// 몬스터 턴 처리
        /// </summary>
        private IEnumerator ProcessMonsterTurn()
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 몬스터 턴 시작");

            // 몬스터 이동 플래그 초기화 (새로운 턴 시작)
            if (MonsterBattleManager.Instance != null)
            {
                MonsterBattleManager.Instance.ResetAllMonsterMoveFlags();

                // 패턴 시스템: 모든 몬스터의 턴 카운터 증가
                MonsterBattleManager.Instance.IncrementMonsterTurnCounters();
            }

            // 살아있는 몬스터들 가져오기
            var aliveMonsters = TargetManager.Instance?.GetAliveEnemies() ?? new List<Maglin.Enemy.Enemy>();

            if (debugMode)
                Debug.Log($"[BattleTestController] 활성 몬스터 {aliveMonsters.Count}마리");

            // 각 몬스터가 순차적으로 행동
            foreach (var monster in aliveMonsters)
            {
                if (monster != null && monster.IsAlive)
                {
                    yield return StartCoroutine(ProcessMonsterAction(monster));
                    yield return new WaitForSeconds(0.2f); // 몬스터 간 딜레이
                }
            }

            // 승부 판정
            if (CheckBattleEnd())
            {
                yield break;
            }

            // 다음 플레이어 턴 시작
            currentTurn++;
            StartPlayerTurn();
        }

        /// <summary>
        /// 개별 몬스터 행동 처리
        /// </summary>
        private IEnumerator ProcessMonsterAction(Maglin.Enemy.Enemy monster)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] {monster.EnemyName} 행동 시작");

            // MonsterBattleManager를 통해 몬스터 이동
            yield return StartCoroutine(MonsterBattleManager.Instance.ProcessMonsterMovement(monster));

            // MonsterBattleManager를 통해 몬스터 공격
            yield return StartCoroutine(MonsterBattleManager.Instance.ProcessMonsterAttack(monster));
        }
        #endregion

        #region Card Management
        /// <summary>
        /// 추가 카드 드로우
        /// </summary>
        public void DrawAdditionalCard()
        {
            if (!isPlayerTurn || !isBattleActive) return;

            if (CardManager.Instance != null)
            {
                var drawnCard = CardManager.Instance.DrawAdditionalCardWithMana();

                if (drawnCard != null && debugMode)
                {
                    Debug.Log($"[BattleTestController] 추가 드로우 성공: {drawnCard.CardName}");
                }
            }
        }

        /// <summary>
        /// 조합 실행
        /// </summary>
        public void ExecuteCombo()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] ========== ExecuteCombo 시작 ==========");

            if (!isPlayerTurn || !isBattleActive)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] 조합 실행 중단 - 플레이어 턴: {isPlayerTurn}, 전투 활성: {isBattleActive}");
                return;
            }

            // 중복 실행 방지를 위해 즉시 플레이어 턴 종료
            isPlayerTurn = false;

            if (debugMode)
                Debug.Log("[BattleTestController] 플레이어 턴 종료 (중복 실행 방지)");

            // BattleUIManager에서 조합 슬롯 카드들과 UI들을 미리 가져오기 (애니메이션용)
            var comboCards = BattleUIManager.Instance?.GetComboSlotCards() ?? new List<Card>();
            var comboSlotCardUIs = new List<GameObject>();

            if (BattleUIManager.Instance != null)
            {
                var elementSlotUI = BattleUIManager.Instance.GetElementSlotUI();
                var active1SlotUI = BattleUIManager.Instance.GetActive1SlotUI();
                var active2SlotUI = BattleUIManager.Instance.GetActive2SlotUI();

                if (elementSlotUI != null) comboSlotCardUIs.Add(elementSlotUI);
                if (active1SlotUI != null) comboSlotCardUIs.Add(active1SlotUI);
                if (active2SlotUI != null) comboSlotCardUIs.Add(active2SlotUI);

                if (debugMode)
                    Debug.Log($"[BattleTestController] 애니메이션용 UI 미리 수집: {comboSlotCardUIs.Count}개");

                // 즉시 슬롯 변수들을 null로 만들어서 턴 종료 시 중복 처리 방지
                BattleUIManager.Instance.ClearComboSlotReferences();
            }

            if (comboCards.Count == 0)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 조합할 카드가 없습니다.");
                return;
            }

            ComboExecutionResult result = null;

            // 단독 사용 가능한 카드만 있는 경우 처리
            if (comboCards.Count == 1 && comboCards[0].CardData.CanUseSolo)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] 단독 카드 사용: {comboCards[0].CardName}");

                // 조합창의 카드들은 나중에 CardManager.EndTurn()에서 자동으로 임시무덤으로 이동됩니다.
                // 여기서는 효과만 실행합니다.

                // 단독 카드 효과 실행
                ExecuteCardEffect(comboCards[0]);

                if (debugMode)
                    Debug.Log($"[BattleTestController] {comboCards[0].CardName} 단독 사용 효과 실행 완료");
            }
            else if (comboCards.Count >= 2)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] {comboCards.Count}장 카드 조합 실행");

                // ComboManager를 통해 조합 실행
                if (ComboManager.Instance != null)
                {
                    // 현재 필드 효과 가져오기
                    ElementType currentField = FieldManager.Instance != null ?
                        FieldManager.Instance.CurrentFieldElement : ElementType.None;

                    if (debugMode)
                    {
                        Debug.Log($"[BattleTestController] 조합 카드 목록:");
                        foreach (var card in comboCards)
                        {
                            Debug.Log($"  - {card.CardName} ({card.Type}) - Element: {card.Element} - CardData: {card.CardData?.name ?? "null"}");
                        }
                    }

                    // ComboExecutionContext 생성 (카드 소모 비활성화 - CardManager.EndTurn()에서 처리)
                    var context = new ComboExecutionContext(comboCards.ToArray(), currentField);
                    context.consumeCards = false;

                    result = ComboManager.Instance.ExecuteCombination(context);

                    if (debugMode)
                    {
                        Debug.Log($"[BattleTestController] 조합 결과: {(result.Success ? "성공" : "실패")}");
                        if (result.Success && result.ResultCardData != null)
                        {
                            Debug.Log($"[BattleTestController] 결과 카드: {result.ResultCardData.CardName}");
                        }
                        else if (!result.Success)
                        {
                            Debug.Log($"[BattleTestController] 조합 실패 사유: {result.ErrorMessage}");
                        }
                    }

                    // 조합 성공 시 추가 처리
                    if (result.Success)
                    {
                        // 실제 카드 효과 실행 (데미지, 힐 등)
                        ExecuteCardEffect(result.ResultCardData);

                        if (debugMode)
                            Debug.Log($"[BattleTestController] {result.ResultCardData.CardName} 효과 실행 완료");
                    }
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 조합 불가능한 카드입니다.");
                return;
            }

            // 조합 완료 후 카드 무덤 애니메이션 먼저 실행 (애니메이션 후 슬롯 정리)
            StartCoroutine(ProcessCardToGraveAnimation(comboCards, comboSlotCardUIs, result));

            if (debugMode)
                Debug.Log("[BattleTestController] ========== ExecuteCombo 완료 ==========");

            // 승부 판정
            CheckBattleEnd();
        }

        /// <summary>
        /// 조합 슬롯 정리
        /// </summary>
        public void ClearComboSlots()
        {
            BattleUIManager.Instance?.ClearComboSlots();
        }

        /// <summary>
        /// 사용된 카드들을 무덤으로 보내는 애니메이션 처리
        /// </summary>
        private IEnumerator ProcessCardToGraveAnimation(List<Card> usedCards, List<GameObject> comboSlotCardUIs, ComboExecutionResult result)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] ProcessCardToGraveAnimation 시작: {usedCards?.Count ?? 0}장");

            if (usedCards == null || usedCards.Count == 0)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 사용된 카드가 없어 무덤 애니메이션 건너뜀");

                // 애니메이션이 없어도 슬롯 정리는 해야 함
                BattleUIManager.Instance?.ClearComboSlotsUIOnly();

                // 플레이어 턴 재시작
                if (!CheckBattleEnd())
                {
                    isPlayerTurn = true;
                    if (debugMode)
                        Debug.Log("[BattleTestController] 플레이어 턴 재시작 (사용된 카드 없음)");
                }
                yield break;
            }

            // 미리 수집된 조합창 카드 UI들 사용
            if (debugMode)
                Debug.Log($"[BattleTestController] 전달받은 조합창 UI: {comboSlotCardUIs?.Count ?? 0}개");

            // 무덤 애니메이션 실행
            if (CardDrawAnimationManager.Instance != null && comboSlotCardUIs != null && comboSlotCardUIs.Count > 0)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] 카드 무덤 애니메이션 시작: {usedCards.Count}장");

                CardDrawAnimationManager.Instance.PlayCardToGraveAnimation(usedCards, comboSlotCardUIs);

                // 애니메이션 완료까지 대기
                bool animationCompleted = false;
                System.Action<List<Card>> onAnimationComplete = (cards) => animationCompleted = true;
                CardDrawAnimationManager.OnCardGraveAnimationCompleted += onAnimationComplete;

                yield return new WaitUntil(() => animationCompleted);

                CardDrawAnimationManager.OnCardGraveAnimationCompleted -= onAnimationComplete;

                // 애니메이션 완료 후 슬롯 정리
                if (debugMode)
                    Debug.Log("[BattleTestController] 무덤 애니메이션 완료 후 슬롯 정리");
                BattleUIManager.Instance?.ClearComboSlotsUIOnly();

                // 플레이어 턴 재시작 (중복 실행 방지가 해제됨)
                if (!CheckBattleEnd())
                {
                    isPlayerTurn = true;
                    if (debugMode)
                        Debug.Log("[BattleTestController] 플레이어 턴 재시작");
                }
            }
            else
            {
                // 애니메이션이 없으면 바로 슬롯 정리
                if (debugMode)
                    Debug.Log("[BattleTestController] 무덤 애니메이션 없이 바로 슬롯 정리");

                // 조합 결과에 따른 슬롯 정리
                if (result != null)
                {
                    // 조합 시도 후 처리
                    if (result.Success)
                    {
                        // 성공 시: 카드가 이미 소모되었으므로 UI만 정리
                        BattleUIManager.Instance?.ClearComboSlotsUIOnly();
                    }
                    else
                    {
                        // 실패 시: 카드를 손패로 되돌림
                        BattleUIManager.Instance?.ClearComboSlots();
                    }
                }
                else
                {
                    // 단독 카드 사용 시: 카드가 이미 소모되었으므로 UI만 정리
                    BattleUIManager.Instance?.ClearComboSlotsUIOnly();
                }

                // 플레이어 턴 재시작 (중복 실행 방지가 해제됨)
                if (!CheckBattleEnd())
                {
                    isPlayerTurn = true;
                    if (debugMode)
                        Debug.Log("[BattleTestController] 플레이어 턴 재시작 (애니메이션 없음)");
                }
            }
        }

        /// <summary>
        /// 카드 효과 실행 (간단한 데미지/힐 처리) - CardSO 버전
        /// </summary>
        private void ExecuteCardEffect(CardSO cardData)
        {
            if (cardData == null) return;

            if (debugMode)
                Debug.Log($"[BattleTestController] {cardData.CardName} 효과 실행: 데미지={cardData.BaseDamage}, 힐={cardData.BaseHeal}, 대상={cardData.Target}");

            // 카드 사용 카운트 증가 (카드 효과 발동 시)
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnCardUsed();
            }

            // 카드 상호작용 차단
            SetCardInteractionEnabled(false);

            // 공격/힐 카드인 경우 플레이어 공격 애니메이션 실행
            if ((cardData.BaseDamage > 0 || cardData.BaseHeal > 0) && PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PlayAttackAnimation(1, 0.5f);
                if (debugMode)
                    Debug.Log($"[BattleTestController] 플레이어 공격 애니메이션 실행");
            }

            // VFX 이펙트 실행 (CardSO만 있는 경우 임시 Card 인스턴스 생성)
            if (cardData.Effect != null && VFXEffectManager.Instance != null)
            {
                var tempCard = new Card(cardData);
                VFXEffectManager.Instance.PlayCardVFX(tempCard);

                if (debugMode)
                    Debug.Log($"[BattleTestController] VFX 실행: {cardData.Effect.EffectName}");
            }
            else
            {
                // VFX가 없는 경우 즉시 효과 적용
                ApplyCardEffectWithoutVFX(cardData);
            }
        }

        /// <summary>
        /// 카드 효과 실행 (Card 인스턴스 버전) - VFX 지원
        /// </summary>
        private void ExecuteCardEffect(Card card)
        {
            if (card?.CardData == null) return;

            var cardData = card.CardData;

            if (debugMode)
                Debug.Log($"[BattleTestController] {cardData.CardName} 효과 실행: 데미지={cardData.BaseDamage}, 힐={cardData.BaseHeal}, 대상={cardData.Target}");

            // 카드 사용 카운트 증가 (카드 효과 발동 시)
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnCardUsed();
            }

            // 카드 상호작용 차단
            SetCardInteractionEnabled(false);

            // 공격/힐 카드인 경우 플레이어 공격 애니메이션 실행
            if ((cardData.BaseDamage > 0 || cardData.BaseHeal > 0) && PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PlayAttackAnimation(1, 0.5f);
                if (debugMode)
                    Debug.Log($"[BattleTestController] 플레이어 공격 애니메이션 실행");
            }

            // VFX 이펙트 실행
            if (cardData.Effect != null && VFXEffectManager.Instance != null)
            {
                VFXEffectManager.Instance.PlayCardVFX(card);

                if (debugMode)
                    Debug.Log($"[BattleTestController] VFX 실행: {cardData.Effect.EffectName}");
            }
            else
            {
                // VFX가 없는 경우 즉시 효과 적용
                ApplyCardEffectWithoutVFX(cardData);
            }
        }

        /// <summary>
        /// VFX 없이 카드 효과 즉시 적용
        /// </summary>
        private void ApplyCardEffectWithoutVFX(CardSO cardData)
        {
            // 데미지/이동 처리 (필드 보너스 포함)
            int finalDamage = CalculateFinalDamage(cardData.BaseDamage, cardData.Element);

            switch (cardData.Target)
            {
                case TargetType.SingleEnemy:
                    if (cardData.BaseDamage > 0)
                        TargetManager.Instance?.DamageTarget(finalDamage);
                    break;
                case TargetType.AllEnemies:
                    if (cardData.BaseDamage > 0)
                        TargetManager.Instance?.DamageAllEnemies(finalDamage);
                    break;
                case TargetType.AllIncludingSelf:
                    if (cardData.BaseDamage > 0)
                    {
                        TargetManager.Instance?.DamageAllEnemies(finalDamage);
                        PlayerManager.Instance?.TakeDamage(finalDamage);
                    }
                    break;

                case TargetType.ChainFrontHits:
                    if (cardData.BaseDamage > 0)
                        TargetManager.Instance?.ChainHitsFromFront(cardData.TargetCount, finalDamage);
                    break;

                case TargetType.PlayerFrontLine:
                    if (cardData.BaseDamage > 0)
                        TargetManager.Instance?.DamagePlayerFrontLine(cardData.TargetCount, finalDamage);
                    break;
                case TargetType.TargetFrontStrip:
                    if (cardData.BaseDamage > 0 && TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                        TargetManager.Instance.DamageTargetFrontStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                    break;
                case TargetType.TargetBackStrip:
                    if (cardData.BaseDamage > 0 && TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                        TargetManager.Instance.DamageTargetBackStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                    break;
                case TargetType.Self:
                    if (cardData.BaseDamage > 0)
                        PlayerManager.Instance?.TakeDamage(finalDamage);
                    break;
            }

            // 힐 처리
            if (cardData.BaseHeal > 0 && PlayerManager.Instance != null)
            {
                PlayerManager.Instance.Heal(cardData.BaseHeal);
                if (debugMode)
                    Debug.Log($"[BattleTestController] 플레이어 힐: {cardData.BaseHeal}");
            }

            // 필드 효과 적용
            if (cardData.FieldEffect != null)
            {
                ApplyFieldEffect(cardData.FieldEffect);
            }

            // 몬스터 이동 효과 적용
            if (cardData.MovementType != MonsterMovementType.None && TargetManager.Instance != null)
            {
                TargetManager.Instance.ApplyMovementEffect(cardData.Target, cardData.MovementType, cardData.MovementDistance);

                if (debugMode)
                    Debug.Log($"[BattleTestController] 몬스터 이동 효과 적용: {cardData.MovementType}, 거리: {cardData.MovementDistance}");
            }

            // 몬스터 소환 효과 적용
            if (cardData.EnableMonsterSummon && cardData.MonsterToSummon != null && MonsterSpawnManager.Instance != null)
            {
                StartCoroutine(SummonMonsterFromCard(cardData));
            }

            // VFX 없는 카드 효과 적용 후 VFX 완료 알림 (VFX가 없으므로 즉시 완료)
            if (VFXEffectManager.Instance != null)
            {
                var tempCard = new Card(cardData);
                VFXEffectManager.Instance.NotifyCardWithoutVFXCompleted(tempCard);
            }

            // 전투 종료 조건 확인
            CheckBattleEnd();
        }

        /// <summary>
        /// 카드에서 몬스터 소환 처리
        /// </summary>
        private IEnumerator SummonMonsterFromCard(CardSO cardData)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 몬스터 소환 시작: {cardData.MonsterToSummon.EnemyName}, 위치: {cardData.SummonPosition}");

            // MonsterSpawnManager를 통해 몬스터 소환
            bool summonSuccess = false;
            Vector2Int? targetPosition = null;

            // 타겟 위치 소환인 경우 타겟 위치 전달
            if (cardData.SummonPosition == MonsterSummonPosition.AtTargetPosition)
            {
                if (TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                {
                    targetPosition = TargetManager.Instance.CurrentTarget.GridPosition;
                }
                else
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 유효한 타겟이 없어 소환 실패");
                    yield break;
                }
            }

            // MonsterSpawnManager의 SummonMonsterFromCard 메서드 사용
            summonSuccess = MonsterSpawnManager.Instance.SummonMonsterFromCard(cardData, targetPosition);

            if (debugMode)
                Debug.Log($"[BattleTestController] 몬스터 소환 결과: {(summonSuccess ? "성공" : "실패")}");

            yield return null;
        }

        /// <summary>
        /// 필드 보너스를 고려한 최종 데미지 계산
        /// </summary>
        private int CalculateFinalDamage(int baseDamage, ElementType cardElement)
        {
            if (FieldManager.Instance == null) return baseDamage;

            // 필드 보너스 계산 (같은 속성일 때 10% 추가)
            float fieldBonus = FieldManager.Instance.CalculateFieldBonus(cardElement);

            int finalDamage = Mathf.RoundToInt(baseDamage * fieldBonus);

            if (debugMode && fieldBonus > 1.0f)
                Debug.Log($"[BattleTestController] 필드 보너스 적용: {baseDamage} -> {finalDamage} ({fieldBonus:F1}x)");

            return finalDamage;
        }

        /// <summary>
        /// 필드 효과 적용
        /// </summary>
        private void ApplyFieldEffect(FieldEffectSO fieldEffect)
        {
            if (fieldEffect == null || FieldManager.Instance == null) return;

            FieldManager.Instance.ApplyFieldEffect(fieldEffect);

            if (debugMode)
                Debug.Log($"[BattleTestController] 필드 효과 적용: {fieldEffect.EffectName} ({fieldEffect.FieldElement}), 지속시간: {fieldEffect.Duration}턴");
        }

        /// <summary>
        /// VFX 히트 이벤트 처리 (데미지 적용)
        /// </summary>
        private void OnVFXHit(VFXHitEventArgs hitArgs)
        {
            if (hitArgs == null || hitArgs.sourceCard?.CardData == null)
            {
                if (debugMode)
                    Debug.LogWarning("[BattleTestController] VFX 히트 이벤트에 유효하지 않은 데이터");
                return;
            }

            var cardData = hitArgs.sourceCard.CardData;
            var hitTiming = hitArgs.hitTiming;

            if (debugMode)
                Debug.Log($"[BattleTestController] VFX 히트: {cardData.CardName} - 타이밍: {hitTiming.Delay}초, 데미지 배율: {hitTiming.DamageMultiplier}, VFX 인덱스: {hitArgs.vfxIndex}/{hitArgs.totalVFXCount}");

            // 히트 타이밍이 데미지를 주는 히트인지 확인
            if (!hitTiming.IsDamageHit)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] 데미지가 없는 히트 타이밍");
                return;
            }

            // showVFXPerTarget이 true이고 여러 VFX가 있는 경우, 첫 번째 VFX에서만 데미지 적용
            bool shouldApplyDamage = true;
            if (cardData.ShowVFXPerTarget && hitArgs.totalVFXCount > 1)
            {
                shouldApplyDamage = (hitArgs.vfxIndex == 0); // 첫 번째 VFX에서만 데미지 적용

                if (debugMode)
                    Debug.Log($"[BattleTestController] 다중 VFX 데미지 제어: {(shouldApplyDamage ? "적용" : "건너뜀")} (VFX {hitArgs.vfxIndex}/{hitArgs.totalVFXCount})");
            }

            // 기본 데미지 계산
            int baseDamage = cardData.BaseDamage;
            if (baseDamage <= 0 && cardData.BaseHeal <= 0) return;

            // 데미지 처리 (첫 번째 VFX에서만 또는 단일 VFX인 경우)
            if (baseDamage > 0 && shouldApplyDamage)
            {
                // 필드 보너스 적용
                int finalDamage = CalculateFinalDamage(baseDamage, cardData.Element);

                // 히트 타이밍 배율 적용
                finalDamage = Mathf.RoundToInt(finalDamage * hitTiming.DamageMultiplier);

                // 타겟별 데미지 적용 (타입별로 TargetManager 직접 호출)
                switch (cardData.Target)
                {
                    case TargetType.SingleEnemy:
                        TargetManager.Instance?.DamageTarget(finalDamage);
                        break;
                    case TargetType.AllEnemies:
                        TargetManager.Instance?.DamageAllEnemies(finalDamage);
                        break;
                    case TargetType.AllIncludingSelf:
                        TargetManager.Instance?.DamageAllEnemies(finalDamage);
                        PlayerManager.Instance?.TakeDamage(finalDamage);
                        break;

                    case TargetType.ChainFrontHits:
                        // VFX 히트 한 번마다 앞의 적에게 한 번만 타격
                        TargetManager.Instance?.ChainHitOnceFromFront(finalDamage);
                        break;
                    case TargetType.PlayerFrontLine:
                        TargetManager.Instance?.DamagePlayerFrontLine(cardData.TargetCount, finalDamage);
                        break;
                    case TargetType.TargetFrontStrip:
                        if (TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                            TargetManager.Instance.DamageTargetFrontStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                        break;
                    case TargetType.TargetBackStrip:
                        if (TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                            TargetManager.Instance.DamageTargetBackStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                        break;
                    case TargetType.Self:
                        PlayerManager.Instance?.TakeDamage(finalDamage);
                        break;
                }

                if (debugMode)
                    Debug.Log($"[BattleTestController] VFX 데미지 적용: {finalDamage} (기본: {baseDamage}, 배율: {hitTiming.DamageMultiplier})");
            }

            // 힐 처리 (플레이어 대상인 경우, 첫 번째 VFX에서만)
            if (cardData.BaseHeal > 0 && cardData.Target == TargetType.Self && PlayerManager.Instance != null && shouldApplyDamage)
            {
                int finalHeal = Mathf.RoundToInt(cardData.BaseHeal * hitTiming.DamageMultiplier);
                PlayerManager.Instance.Heal(finalHeal);

                if (debugMode)
                    Debug.Log($"[BattleTestController] VFX 힐 적용: {finalHeal}");
            }

            // 몬스터 이동 효과 적용 (첫 번째 VFX에서만)
            if (shouldApplyDamage && cardData.MovementType != MonsterMovementType.None && TargetManager.Instance != null)
            {
                TargetManager.Instance.ApplyMovementEffect(cardData.Target, cardData.MovementType, cardData.MovementDistance);

                if (debugMode)
                    Debug.Log($"[BattleTestController] VFX 몬스터 이동 효과 적용: {cardData.MovementType}, 거리: {cardData.MovementDistance}");
            }

            // 몬스터 소환 효과 적용 (첫 번째 VFX에서만)
            if (shouldApplyDamage && cardData.EnableMonsterSummon && cardData.MonsterToSummon != null && MonsterSpawnManager.Instance != null)
            {
                StartCoroutine(SummonMonsterFromCard(cardData));
            }

            // VFX 히트 후 전투 종료 조건 확인 (첫 번째 VFX에서만)
            if (shouldApplyDamage)
            {
                CheckBattleEnd();
            }
        }

        /// <summary>
        /// 카드의 모든 공격 시퀀스 완료 이벤트 처리
        /// </summary>
        private void OnCardAttackSequenceCompleted(Card card)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 카드 {card.CardName} 모든 공격 시퀀스 완료 - 카드 상호작용 재활성화");

            // 카드 상호작용 재활성화
            SetCardInteractionEnabled(true);

            // 전투 종료 조건 확인
            CheckBattleEnd();
        }

        /// <summary>
        /// VFX 타겟들에게 데미지 적용
        /// </summary>
        private void ApplyVFXDamageToTargets(CardSO cardData, Transform[] targets, int finalDamage)
        {
            if (targets == null || targets.Length == 0) return;

            switch (cardData.Target)
            {
                case TargetType.SingleEnemy:
                    // VFX 타겟 배열 대신 현재 타겟 기준으로 처리
                    TargetManager.Instance?.DamageTarget(finalDamage);
                    break;
                case TargetType.AllEnemies:
                    TargetManager.Instance?.DamageAllEnemies(finalDamage);
                    break;


                case TargetType.Self:
                    // 플레이어에게 데미지 (공격 카드가 자기 자신을 타겟하는 경우)
                    if (PlayerManager.Instance != null)
                    {
                        PlayerManager.Instance.TakeDamage(finalDamage);

                        if (debugMode)
                            Debug.Log($"[BattleTestController] 플레이어에게 VFX 데미지: {finalDamage}");
                    }
                    break;

                case TargetType.AllIncludingSelf:
                    TargetManager.Instance?.DamageAllEnemies(finalDamage);
                    PlayerManager.Instance?.TakeDamage(finalDamage);
                    break;

                case TargetType.ChainFrontHits:
                    TargetManager.Instance?.ChainHitsFromFront(cardData.TargetCount, finalDamage);
                    break;

                case TargetType.PlayerFrontLine:
                    TargetManager.Instance?.DamagePlayerFrontLine(cardData.TargetCount, finalDamage);
                    break;
                case TargetType.TargetFrontStrip:
                    if (TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                        TargetManager.Instance.DamageTargetFrontStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                    break;
                case TargetType.TargetBackStrip:
                    if (TargetManager.Instance != null && TargetManager.Instance.IsTargetValid())
                        TargetManager.Instance.DamageTargetBackStrip(TargetManager.Instance.CurrentTarget, cardData.TargetCount, finalDamage);
                    break;
            }
        }
        #endregion



        #region Battle End Conditions
        /// <summary>
        /// 전투 종료 조건 확인
        /// </summary>
        private bool CheckBattleEnd()
        {
            // 플레이어 사망 확인
            if (PlayerManager.Instance != null && !PlayerManager.Instance.IsAlive)
            {
                EndBattle(false);
                return true;
            }

            // 모든 적 몬스터 처치 확인 (중립 오브젝트 제외)
            var aliveMonsters = TargetManager.Instance?.GetAliveEnemies() ?? new List<Maglin.Enemy.Enemy>();

            // 중립 오브젝트가 아닌 적 몬스터만 필터링
            var aliveEnemyMonsters = aliveMonsters.Where(monster =>
                monster != null &&
                monster.EnemyData != null &&
                !monster.EnemyData.IsNeutralObject).ToList();

            if (debugMode)
            {
                Debug.Log($"[BattleTestController] 전투 종료 확인:");
                Debug.Log($"  - 전체 살아있는 몬스터: {aliveMonsters.Count}마리");
                Debug.Log($"  - 적 몬스터 (중립 제외): {aliveEnemyMonsters.Count}마리");

                foreach (var monster in aliveMonsters)
                {
                    if (monster?.EnemyData != null)
                    {
                        Debug.Log($"  - {monster.EnemyName}: {(monster.EnemyData.IsNeutralObject ? "중립 오브젝트" : "적 몬스터")}");
                    }
                }
            }

            // 적 몬스터가 모두 죽었으면 전투 승리 (사망 애니메이션 대기)
            if (aliveEnemyMonsters.Count == 0)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 모든 적 몬스터 처치 완료! 사망 애니메이션 대기 중...");

                // 사망 애니메이션이 진행 중인지 확인
                if (MonsterDeathAnimationManager.Instance != null &&
                    MonsterDeathAnimationManager.Instance.HasActiveDeathAnimations())
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 사망 애니메이션 진행 중, 전투 종료 대기");

                    pendingBattleEnd = true;
                    return false; // 아직 전투 종료하지 않음
                }
                else
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 사망 애니메이션 없음, 즉시 전투 승리");

                    EndBattle(true);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 전투 종료
        /// </summary>
        private void EndBattle(bool victory)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 전투 종료: {(victory ? "승리" : "패배")}");

            isBattleActive = false;

            // 매니저들에 전투 종료 알림
            BattleUIManager.Instance?.SetBattleState(false, false);

            // PlayerBattleManager에 전투 종료 알림 (플레이어 오브젝트는 유지)
            PlayerBattleManager.Instance?.OnBattleEnd();

            if (victory)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 전투 승리!");

                // 테스트 모드에서만 직접 보상 표시, 프로덕션 모드에서는 FloorManager가 처리
                if (useTestMode)
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 테스트 모드 - 보상을 표시합니다.");
                    ShowBattleRewards();
                }
                else
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 프로덕션 모드 - FloorManager에게 층 완료 알림");

                    // 프로덕션 모드: FloorManager에게 현재 층 완료 알림
                    if (FloorManager.Instance != null)
                    {
                        Debug.Log($"[BattleTestController] FloorManager.Instance 존재, CompleteCurrentFloor 호출");
                        FloorManager.Instance.CompleteCurrentFloor();
                        Debug.Log($"[BattleTestController] CompleteCurrentFloor 호출 완료");
                    }
                    else
                    {
                        Debug.LogError("[BattleTestController] FloorManager.Instance가 null입니다! 테스트 모드로 폴백");
                        ShowBattleRewards();
                    }
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 전투 패배! 게임 오버");

                // 패배 시에는 플레이어 정리 (게임 오버이므로)
                if (PlayerBattleManager.Instance != null)
                {
                    PlayerBattleManager.Instance.CleanupOnSceneTransition();
                }
            }
        }

        /// <summary>
        /// 전투 보상 표시
        /// </summary>
        private void ShowBattleRewards()
        {
            if (RewardManager.Instance == null)
            {
                Debug.LogError("[BattleTestController] RewardManager가 없습니다!");
                return;
            }

            if (debugMode)
                Debug.Log("[BattleTestController] RewardManager 확인됨, 보상 생성 시작");

            // RewardManager를 통해 보상 생성 및 표시
            RewardManager.Instance.ShowBattleRewards(currentFloor, currentFloorType);
        }

        #region Reward Event Handlers
        /// <summary>
        /// 전투 보상이 생성되었을 때 호출
        /// </summary>
        private void OnBattleRewardsGenerated(BattleRewardData rewardData)
        {
            if (debugMode)
            {
                Debug.Log($"[BattleTestController] OnBattleRewardsGenerated 호출됨:");
                Debug.Log($"  - 골드: {rewardData.goldAmount}");
                Debug.Log($"  - 카드 선택지: {rewardData.cardChoices?.Length ?? 0}개");
                Debug.Log($"  - 유물 선택지: {rewardData.relicChoices?.Length ?? 0}개");
            }

            if (BattleUIManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] BattleUIManager에 보상 UI 업데이트 요청");

                // 보상 UI가 준비되어 있는지 확인
                if (debugMode)
                {
                    // BattleUIManager의 FindRewardUI를 다시 호출하여 UI가 준비되었는지 확인
                    var findRewardUIMethod = typeof(BattleUIManager).GetMethod("FindRewardUI",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (findRewardUIMethod != null)
                    {
                        findRewardUIMethod.Invoke(BattleUIManager.Instance, null);
                        Debug.Log("[BattleTestController] FindRewardUI 재호출 완료");
                    }
                }

                // 보상 UI 업데이트 및 표시 (새로운 구조)
                BattleUIManager.Instance.UpdateBattleRewardUI(rewardData);
                BattleUIManager.Instance.ShowRewardUI();

                if (debugMode)
                    Debug.Log("[BattleTestController] 보상 UI 표시 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] BattleUIManager가 null입니다!");
            }
        }

        /// <summary>
        /// 보상 슬롯이 선택되었을 때 호출 (UI에서)
        /// </summary>
        private void OnRewardSelected(int slotIndex)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 보상 슬롯 {slotIndex} 선택됨");

            if (BattleUIManager.Instance == null || RewardManager.Instance == null)
                return;

            // 선택된 보상 가져오기
            var selectedReward = BattleUIManager.Instance.GetSelectedReward(slotIndex);

            if (selectedReward != null)
            {
                // RewardManager를 통해 보상 적용
                RewardManager.Instance.SelectReward(selectedReward);
            }
            else
            {
                Debug.LogWarning($"[BattleTestController] 슬롯 {slotIndex}에 유효한 보상이 없습니다.");
            }
        }

        /// <summary>
        /// 골드가 지급되었을 때 호출
        /// </summary>
        private void OnGoldReceived(int goldAmount)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 골드 {goldAmount} 획득");
        }

        /// <summary>
        /// 보상이 적용되었을 때 호출 (RewardManager에서)
        /// </summary>
        private void OnRewardApplied(RewardItem appliedReward)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 보상 적용됨: {appliedReward.rewardType}");

            // 추가적인 보상 적용 후 처리가 필요하면 여기에
        }

        /// <summary>
        /// 보상 선택 과정이 완료되었을 때 호출
        /// </summary>
        private void OnRewardsCompleted()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 보상 선택 완료");

            // 보상 UI 숨기기
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.HideRewardUI();
            }

            // 보상 선택 완료 후 정리 작업만 수행
            // (다음 층 진행은 FloorManager.CompleteCurrentFloor()에서 자동으로 처리됨)
            if (debugMode)
                Debug.Log("[BattleTestController] 보상 선택 완료 - 정리 작업 수행");

            // 추가 정리 작업이 필요하면 여기에 추가
            SetInputBlocked(true); // 입력 차단 유지
        }

        /// <summary>
        /// 전투 완료 후 다음 층 진행 처리 (프로덕션 모드)
        /// 현재 사용하지 않음 - FloorManager에서 자동 진행 처리
        /// </summary>
        /*
        private System.Collections.IEnumerator CompleteBattleAndProceed()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 완료, 다음 층으로 진행 시작");

            // UI 정리 작업
            BattleUIManager.Instance?.HideRewardUI();
            SetInputBlocked(true); // 입력 다시 막기

            // 잠시 대기
            yield return new WaitForSeconds(1f);

            // 전투 완료 정리 작업만 수행
            // (다음 층 진행은 FloorManager.CompleteCurrentFloor()에서 자동으로 처리됨)
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 완료 정리 작업 완료");
            
            // 추가 정리 작업이 필요하면 여기에 추가
        }
        */

        /// <summary>
        /// 보상 건너뛰기가 선택되었을 때 호출
        /// </summary>
        private void OnRewardSkipped()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 보상 건너뛰기 선택됨");

            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.SkipRewards();
            }
        }
        #endregion
        #endregion

        #region Event Handlers
        /// <summary>
        /// 타겟 변경 이벤트 처리
        /// </summary>
        private void OnTargetChanged(Maglin.Enemy.Enemy enemy)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 타겟 변경: {enemy?.EnemyName ?? "없음"}");
        }

        /// <summary>
        /// 타겟 사망 이벤트 처리
        /// </summary>
        private void OnTargetDied(Maglin.Enemy.Enemy enemy)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 타겟 사망: {enemy?.EnemyName ?? "Unknown"}");

            // 모든 몬스터가 죽었는지 확인
            StartCoroutine(CheckBattleEndAfterDelay());
        }

        /// <summary>
        /// 지연 후 전투 종료 확인
        /// </summary>
        private IEnumerator CheckBattleEndAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);
            CheckBattleEnd();
        }

        /// <summary>
        /// 모든 사망 애니메이션 완료 이벤트 처리
        /// </summary>
        private void OnAllDeathAnimationsCompleted()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 모든 사망 애니메이션 완료됨");

            // 대기 중인 전투 종료가 있으면 실행
            if (pendingBattleEnd)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 대기 중이던 전투 종료 실행");

                pendingBattleEnd = false;
                EndBattle(true);
            }
        }

        #region Floor Manager Events
        /// <summary>
        /// 층 시작 이벤트 처리
        /// </summary>
        private void OnFloorStarted(FloorInfo floorInfo)
        {
            // 이벤트 전투 모드 확인
            if (FloorManager.Instance != null && FloorManager.Instance.IsEventBattleMode)
            {
                if (debugMode)
                    Debug.Log($"[BattleTestController] 이벤트 전투 모드 감지");

                // 이벤트 전투 처리
                HandleEventBattle();
                return;
            }

            // 전투 관련 층만 처리
            if (floorInfo.floorType == FloorType.Normal ||
                floorInfo.floorType == FloorType.Elite ||
                floorInfo.floorType == FloorType.Boss ||
                floorInfo.floorType == FloorType.Start)
            {
                currentFloor = floorInfo.floorNumber;
                currentFloorType = floorInfo.floorType;

                if (debugMode)
                    Debug.Log($"[BattleTestController] {currentFloor}층 전투 시작: {floorInfo.floorType}");

                // 층에 맞는 배틀 스테이지 로드 및 전투 초기화
                LoadBattleStageForFloor(currentFloor, currentFloorType);
            }
        }

        /// <summary>
        /// 층 완료 이벤트 처리
        /// </summary>
        private void OnFloorCompleted(FloorInfo floorInfo)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] {floorInfo.floorNumber}층 완료: {floorInfo.floorType}");
        }
        #endregion

        #region Battle Stage Loading
        /// <summary>
        /// 층에 맞는 배틀 스테이지 로드
        /// </summary>
        private void LoadBattleStageForFloor(int floor, FloorType floorType)
        {
            BattleStageSO battleStage = null;

            // 먼저 FloorManager에서 현재 배틀 스테이지를 가져옴
            if (FloorManager.Instance != null)
            {
                battleStage = FloorManager.Instance.GetCurrentBattleStage();

                if (battleStage == null)
                {
                    // FloorManager의 GetBattleStageForFloor 메서드 사용하여 BattleStage 선택
                    battleStage = FloorManager.Instance.GetBattleStageForFloor(floor, floorType);

                    // FloorManager에서 선택된 BattleStage를 즉시 설정
                    if (battleStage != null)
                    {
                        FloorManager.Instance.SetCurrentBattleStage(battleStage);
                        if (debugMode)
                            Debug.Log($"[BattleTestController] FloorManager에서 BattleStage 선택 및 설정: {battleStage.name}");
                    }
                }
            }

            // FloorManager에서 못 가져왔으면 직접 리소스에서 로드
            if (battleStage == null)
            {
                string stageResourcePath = $"BattleStage/F{floor:D2}";
                battleStage = Resources.Load<BattleStageSO>(stageResourcePath);

                if (battleStage == null)
                {
                    stageResourcePath = $"F{floor:D2}";
                    battleStage = Resources.Load<BattleStageSO>(stageResourcePath);
                }

                // 리소스에서 로드한 경우에도 FloorManager에 설정
                if (battleStage != null && FloorManager.Instance != null)
                {
                    FloorManager.Instance.SetCurrentBattleStage(battleStage);
                    if (debugMode)
                        Debug.Log($"[BattleTestController] 리소스에서 로드한 BattleStage FloorManager에 설정: {battleStage.name}");
                }
            }

            if (battleStage != null)
            {
                currentBattleStage = battleStage;

                // BattleStage에서 실제 BattleSO 선택 (FloorManager에 BattleStage가 설정되지 않은 경우만)
                if (FloorManager.Instance != null && FloorManager.Instance.GetCurrentBattle() == null)
                {
                    BattleSO selectedBattle = battleStage.GetRandomBattle(floor);
                    if (selectedBattle != null)
                    {
                        FloorManager.Instance.SetCurrentBattle(selectedBattle);
                        if (debugMode)
                            Debug.Log($"[BattleTestController] 선택된 Battle: {selectedBattle.BattleName}");
                    }
                }

                if (debugMode)
                    Debug.Log($"[BattleTestController] {floor}층 배틀 스테이지 로드 성공: {battleStage.name}");
            }
            else
            {
                Debug.LogWarning($"[BattleTestController] {floor}층용 배틀 스테이지를 찾을 수 없습니다. 테스트 스테이지 사용");

                // 테스트 스테이지 사용 (폴백)
                if (testBattleStage != null)
                {
                    currentBattleStage = testBattleStage;

                    // FloorManager에 테스트 BattleStage 정보 업데이트
                    if (FloorManager.Instance != null)
                    {
                        FloorManager.Instance.SetCurrentBattleStage(testBattleStage);

                        // 테스트 BattleStage에서도 실제 BattleSO 선택
                        BattleSO selectedBattle = testBattleStage.GetRandomBattle(floor);
                        if (selectedBattle != null)
                        {
                            FloorManager.Instance.SetCurrentBattle(selectedBattle);
                            if (debugMode)
                                Debug.Log($"[BattleTestController] 테스트에서 선택된 Battle: {selectedBattle.BattleName}");
                        }
                    }

                    if (debugMode)
                        Debug.Log($"[BattleTestController] 테스트 배틀 스테이지 사용: {testBattleStage.name}");
                }
                else
                {
                    // 기본 배틀 스테이지 생성 (임시)
                    CreateDefaultBattleStage(floor, floorType);
                }
            }

            // 배틀 스테이지가 설정되었으면 전투 초기화
            if (currentBattleStage != null && !useTestMode)
            {
                InitializeBattle();
            }
        }

        /// <summary>
        /// 기본 배틀 스테이지 생성 (배틀 스테이지가 없을 때 임시용)
        /// </summary>
        private void CreateDefaultBattleStage(int floor, FloorType floorType)
        {
            // 런타임에서 임시 배틀 스테이지 생성
            var tempStage = ScriptableObject.CreateInstance<BattleStageSO>();
            tempStage.name = $"DefaultStage_F{floor}_{floorType}";

            // 기본 전투 데이터 생성 (층 타입에 따라 다르게)
            if (floorType == FloorType.Boss)
            {
                // 보스층 설정
            }
            else if (floorType == FloorType.Elite)
            {
                // 엘리트층 설정
            }
            else
            {
                // 일반층 설정
            }

            currentBattleStage = tempStage;

            // FloorManager에 기본 생성된 BattleStage 정보 업데이트
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.SetCurrentBattleStage(tempStage);
                if (debugMode)
                    Debug.Log($"[BattleTestController] FloorManager에 기본 스테이지 설정됨");
            }

            if (debugMode)
                Debug.Log($"[BattleTestController] 기본 배틀 스테이지 생성: {floor}층 {floorType}");
        }

        /// <summary>
        /// 프로덕션 모드용 전투 초기화
        /// </summary>
        private void InitializeBattle()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 프로덕션 전투 초기화 시작");

            // 매니저들이 이미 초기화되어 있는지 확인
            if (!VerifyManagersReady())
            {
                Debug.LogError("[BattleTestController] 필수 매니저들이 초기화되지 않았습니다!");
                return;
            }

            // 기존 InitializeManagers와 동일한 초기화 수행
            InitializeManagers();

            // 프로덕션 모드 전용 초기화
            InitializeProductionMode();

            // 전투 시작
            StartBattle();
        }

        /// <summary>
        /// FloorManager 상태 확인 (프로덕션 모드용)
        /// </summary>
        private System.Collections.IEnumerator CheckFloorManagerState()
        {
            // 잠시 대기 후 FloorManager 상태 확인
            yield return new WaitForSeconds(1f);

            if (FloorManager.Instance != null)
            {
                if (debugMode)
                {
                    Debug.Log("[BattleTestController] FloorManager 상태 재확인:");
                    Debug.Log($"  - 현재 층: {FloorManager.Instance.CurrentFloor}");
                    Debug.Log($"  - 현재 층 타입: {FloorManager.Instance.CurrentFloorType}");
                    Debug.Log($"  - 현재 배틀스테이지: {FloorManager.Instance.GetCurrentBattleStage()?.name ?? "null"}");
                }

                // FloorManager가 이미 층을 시작했다면 강제로 전투 시작
                if (FloorManager.Instance.CurrentFloor > 0)
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] FloorManager가 이미 층을 시작함 - 강제 전투 시작");

                    // 현재 층 정보로 전투 시작
                    OnFloorStarted(new FloorInfo(
                        FloorManager.Instance.CurrentFloor,
                        FloorManager.Instance.CurrentFloorType
                    ));
                }
            }
            else
            {
                Debug.LogError("[BattleTestController] FloorManager가 없습니다! 테스트 모드로 폴백");
                useTestMode = true;
                InitializeBattleTest();
            }
        }

        /// <summary>
        /// 프로덕션 모드 전용 초기화
        /// </summary>
        private void InitializeProductionMode()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 프로덕션 모드 전용 초기화 시작");

            // 카드 시스템 확실히 초기화
            if (CardManager.Instance != null)
            {
                // 새 게임용 덱 초기화 (기본 스타터 카드로)
                CardManager.Instance.InitializeDeckForNewGame();

                // 카드 인스턴스 시스템 초기화
                CardManager.Instance.InitializeCardInstanceSystem();

                if (debugMode)
                {
                    Debug.Log("[BattleTestController] 프로덕션 모드: CardManager 재초기화 완료");
                    // CardManager의 덱/손패 상태 확인 (public 메서드 사용)
                    var deckField = typeof(CardManager).GetField("currentDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var handField = typeof(CardManager).GetField("hand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (deckField != null && handField != null)
                    {
                        var deck = deckField.GetValue(CardManager.Instance) as System.Collections.Generic.List<CardSO>;
                        var hand = handField.GetValue(CardManager.Instance) as System.Collections.Generic.List<CardSO>;
                        Debug.Log($"[BattleTestController] 현재 덱 카드 수: {deck?.Count ?? 0}");
                        Debug.Log($"[BattleTestController] 현재 손패 카드 수: {hand?.Count ?? 0}");
                    }
                }
            }

            // 플레이어 상태 초기화
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnBattleStart();
                if (debugMode)
                    Debug.Log("[BattleTestController] 프로덕션 모드: PlayerManager 전투 시작 처리 완료");
            }

            // 추가 프로덕션 초기화 로직이 필요하면 여기에 추가

            if (debugMode)
                Debug.Log("[BattleTestController] 프로덕션 모드 전용 초기화 완료");
        }

        /// <summary>
        /// 필수 매니저들이 준비되었는지 확인
        /// </summary>
        private bool VerifyManagersReady()
        {
            var missingManagers = new List<string>();

            // GameManager는 더 이상 필요하지 않음 (FloorManager로 통합됨)
            if (FloorManager.Instance == null) missingManagers.Add("FloorManager");
            if (PlayerManager.Instance == null) missingManagers.Add("PlayerManager");
            if (CardManager.Instance == null) missingManagers.Add("CardManager");

            if (missingManagers.Count > 0)
            {
                Debug.LogError($"[BattleTestController] 누락된 매니저들: {string.Join(", ", missingManagers)}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 씬 전환 후 BattleUIManager의 UI 참조 재설정
        /// </summary>
        private void RefreshUIManagerReferences()
        {
            if (BattleUIManager.Instance == null)
            {
                Debug.LogError("[BattleTestController] BattleUIManager가 null입니다!");
                return;
            }

            if (debugMode)
                Debug.Log("[BattleTestController] BattleUIManager UI 참조 재설정 시작");

            // 현재 씬에서 UI 요소들을 찾아서 BattleUIManager에 전달
            var healthText = FindUIComponent<TextMeshProUGUI>("HealthText");
            var manaText = FindUIComponent<TextMeshProUGUI>("ManaText");
            var goldText = FindUIComponent<TextMeshProUGUI>("GoldText");
            var deckCountText = FindUIComponent<TextMeshProUGUI>("DeckCountText");
            var handContent = FindUIComponent<Transform>("HandContent");
            var endTurnButton = FindUIComponent<Button>("EndTurnButton");
            var drawButton = FindUIComponent<Button>("DrawButton");
            var turnIndicator = FindUIComponent<TextMeshProUGUI>("TurnIndicator");
            var elementSlot = FindUIComponent<Transform>("ElementSlot");
            var active1Slot = FindUIComponent<Transform>("Active1Slot");
            var active2Slot = FindUIComponent<Transform>("Active2Slot");
            var executeComboButton = FindUIComponent<Button>("ExecuteComboButton");
            var clearComboButton = FindUIComponent<Button>("ClearComboButton");
            var fieldAreaImage = FindUIComponent<UnityEngine.UI.Image>("FieldArea");
            var fieldEffectTurnsText = FindUIComponent<TextMeshProUGUI>("FieldEffectTurnsText");
            var cardUIPrefab = Resources.Load<GameObject>("Prefabs/CardUIPrefab");
            var relicPrefab = Resources.Load<GameObject>("Prefabs/RelicPrefab");
            var monsterPrefab = Resources.Load<GameObject>("Prefabs/MonsterPrefab");

            // BattleUIManager에 참조 전달
            BattleUIManager.Instance.SetUIReferences(
                healthText, manaText, goldText, deckCountText,
                handContent, endTurnButton, drawButton, turnIndicator,
                elementSlot, active1Slot, active2Slot,
                executeComboButton, clearComboButton,
                fieldAreaImage, fieldEffectTurnsText,
                cardUIPrefab, relicPrefab, monsterPrefab
            );

            if (debugMode)
                Debug.Log("[BattleTestController] BattleUIManager UI 참조 재설정 완료");
        }

        /// <summary>
        /// UI 컴포넌트를 이름으로 찾는 헬퍼 메서드
        /// </summary>
        private T FindUIComponent<T>(string componentName) where T : Component
        {
            // 전체 씬에서 해당 이름의 GameObject를 찾기
            var allObjects = FindObjectsOfType<GameObject>();

            foreach (var obj in allObjects)
            {
                if (obj.name == componentName)
                {
                    var component = obj.GetComponent<T>();
                    if (component != null)
                    {
                        if (debugMode)
                            Debug.Log($"[BattleTestController] UI 컴포넌트 찾음: {componentName} ({typeof(T).Name})");
                        return component;
                    }
                }
            }

            if (debugMode)
                Debug.LogWarning($"[BattleTestController] UI 컴포넌트를 찾을 수 없음: {componentName} ({typeof(T).Name})");

            return null;
        }

        /// <summary>
        /// 씬 전환 후 CardDrawAnimationManager의 UI 참조 재설정
        /// </summary>
        private void RefreshCardDrawAnimationManagerReferences()
        {
            if (CardDrawAnimationManager.Instance == null)
            {
                Debug.LogError("[BattleTestController] CardDrawAnimationManager가 null입니다!");
                return;
            }

            if (debugMode)
                Debug.Log("[BattleTestController] CardDrawAnimationManager UI 참조 재설정 시작");

            // 현재 씬에서 UI 요소들을 찾아서 CardDrawAnimationManager에 전달
            var deckArea = FindUIComponent<Transform>("DeckArea");
            var handContent = FindUIComponent<Transform>("HandContent");
            var graveArea = FindUIComponent<Transform>("Grave");
            var cardUIPrefab = Resources.Load<GameObject>("Prefabs/CardUIPrefab");

            // CardArea 하위에서 찾기 (대안)
            if (deckArea == null)
            {
                var cardArea = GameObject.Find("CardArea");
                if (cardArea != null)
                {
                    deckArea = cardArea.transform.Find("DeckArea");
                }
            }

            if (handContent == null)
            {
                var cardArea = GameObject.Find("CardArea");
                if (cardArea != null)
                {
                    handContent = cardArea.transform.Find("HandContent");
                }
            }

            if (graveArea == null)
            {
                var cardArea = GameObject.Find("CardArea");
                if (cardArea != null)
                {
                    graveArea = cardArea.transform.Find("Grave");
                }
            }

            // CardDrawAnimationManager에 참조 전달
            CardDrawAnimationManager.Instance.SetUIReferences(deckArea, handContent, graveArea, cardUIPrefab);

            // UI 참조 자동 탐지 강제 실행
            CardDrawAnimationManager.Instance.AutoDetectUIReferences();

            if (debugMode)
                Debug.Log("[BattleTestController] CardDrawAnimationManager UI 참조 재설정 완료");
        }

        /// <summary>
        /// 이벤트 전투 처리
        /// </summary>
        private void HandleEventBattle()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 이벤트 전투 처리 시작");

            // FloorManager에서 현재 전투 데이터 가져오기
            if (FloorManager.Instance != null && FloorManager.Instance.GetCurrentBattle() != null)
            {
                var battleData = FloorManager.Instance.GetCurrentBattle();

                if (debugMode)
                    Debug.Log($"[BattleTestController] 이벤트 전투 데이터: {battleData.BattleName}");

                // 임시 BattleStage 생성 (이벤트 전투용)
                var tempStage = CreateEventBattleStage(battleData);
                currentBattleStage = tempStage;

                // 매니저들 초기화
                InitializeManagers();

                // 이벤트 전투용 카드 시스템 초기화 (중요!)
                InitializeEventBattleCardSystem();

                // 이벤트 전투 시작
                StartEventBattleDirectly(battleData);
            }
            else
            {
                Debug.LogError("[BattleTestController] 이벤트 전투 데이터를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// 이벤트 전투용 카드 시스템 초기화
        /// </summary>
        private void InitializeEventBattleCardSystem()
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 이벤트 전투용 카드 시스템 초기화 시작");

            if (CardManager.Instance != null)
            {
                // 스타터 덱을 현재 덱으로 복사하여 초기화 (일반 전투와 동일)
                CardManager.Instance.InitializeDeckForNewGame();

                // Card 인스턴스 시스템 초기화
                CardManager.Instance.InitializeCardInstanceSystem();

                if (debugMode)
                {
                    Debug.Log("[BattleTestController] 이벤트 전투: CardManager 초기화 완료");

                    // 덱 상태 확인
                    var deckManagerInfo = CardManager.Instance.GetDeckManagerInfo();
                    Debug.Log($"[BattleTestController] 이벤트 전투 덱 상태:");
                    Debug.Log($"  - 메인 덱: {deckManagerInfo.MainDeckCount}장");
                    Debug.Log($"  - 손패: {deckManagerInfo.HandCardCount}장");
                    Debug.Log($"  - 임시무덤: {deckManagerInfo.TempGraveyardCount}장");
                }
            }
            else
            {
                Debug.LogError("[BattleTestController] CardManager.Instance가 null입니다! 이벤트 전투 카드 초기화 실패");
            }
        }

        /// <summary>
        /// 이벤트 전투 직접 시작 (BattleSO 사용)
        /// </summary>
        private void StartEventBattleDirectly(BattleSO battleData)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 이벤트 전투 직접 시작: {battleData.BattleName}");

            isBattleActive = true;
            isPlayerTurn = true;
            currentTurn = 1;
            currentFloor = FloorManager.Instance?.CurrentFloor ?? 1;
            currentFloorType = FloorType.Normal; // 이벤트 전투는 일반 전투로 취급

            // 매니저들에 전투 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 플레이어 위치 설정 (PlayerBattleManager를 통해)
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.SetPlayerGridPosition(new Vector2Int(0, 0));
            }

            // 이벤트 전투 초기화 시퀀스 시작 (플레이어 입장 애니메이션 포함)
            StartCoroutine(ExecuteEventBattleInitialization(battleData));
        }

        /// <summary>
        /// 이벤트 전투 초기화 시퀀스 (플레이어 입장 애니메이션 포함)
        /// </summary>
        private System.Collections.IEnumerator ExecuteEventBattleInitialization(BattleSO battleData)
        {
            if (debugMode)
                Debug.Log("[BattleTestController] 이벤트 전투 초기화 시퀀스 시작");

            // 0단계: 플레이어 입장 애니메이션 (화면 왼쪽에서 걸어오기)
            if (PlayerBattleManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 이벤트 전투 플레이어 입장 애니메이션 시작");

                yield return StartCoroutine(PlayerBattleManager.Instance.PlayPlayerEntranceAnimation());

                if (debugMode)
                    Debug.Log("[BattleTestController] 이벤트 전투 플레이어 입장 애니메이션 완료");
            }

            // 1단계: BattleSO에서 직접 몬스터 스폰
            yield return StartCoroutine(SpawnEventBattleMonstersAndSetTarget(battleData));

            // 2단계: 플레이어 턴 시작
            StartPlayerTurn();

            // 3단계: UI 업데이트
            BattleUIManager.Instance?.UpdateAllUI();

            if (debugMode)
                Debug.Log("[BattleTestController] 이벤트 전투 초기화 시퀀스 완료");
        }

        /// <summary>
        /// 이벤트 전투 몬스터 스폰 및 타겟 설정
        /// </summary>
        private IEnumerator SpawnEventBattleMonstersAndSetTarget(BattleSO battleData)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 이벤트 전투 몬스터 스폰 시작: {battleData.BattleName}");

            // MonsterSpawnAnimationManager의 모든 스폰 완료 이벤트 구독
            bool spawnCompleted = false;
            System.Action onSpawnCompleted = () => { spawnCompleted = true; };

            if (MonsterSpawnAnimationManager.Instance != null)
            {
                MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted += onSpawnCompleted;
            }

            // MonsterSpawnManager를 통해 이벤트 전투 몬스터 스폰
            if (MonsterSpawnManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 이벤트 전투 몬스터 스폰 실행 중...");

                // BattleSO에서 직접 몬스터 스폰 (리플렉션 사용)
                var spawnMethod = typeof(MonsterSpawnManager).GetMethod("SpawnMonstersFromBattle",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (spawnMethod != null)
                {
                    spawnMethod.Invoke(MonsterSpawnManager.Instance, new object[] { battleData });

                    if (debugMode)
                        Debug.Log("[BattleTestController] 이벤트 전투 몬스터 스폰 완료");
                }
                else
                {
                    Debug.LogError("[BattleTestController] SpawnMonstersFromBattle 메서드를 찾을 수 없습니다!");
                }
            }
            else
            {
                Debug.LogError("[BattleTestController] MonsterSpawnManager가 null입니다!");
            }

            // 몬스터 스폰 애니메이션 완료 대기
            if (MonsterSpawnAnimationManager.Instance != null)
            {
                yield return new WaitUntil(() => spawnCompleted);
                MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted -= onSpawnCompleted;

                if (debugMode)
                    Debug.Log("[BattleTestController] 이벤트 전투 몬스터 스폰 애니메이션 완료");
            }
            else
            {
                // 애니메이션 매니저가 없으면 기본 대기
                yield return new WaitForSeconds(1f);
            }

            // 타겟 설정
            if (TargetManager.Instance != null)
            {
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                    if (debugMode)
                        Debug.Log("[BattleTestController] 이벤트 전투 초기 타겟 설정 완료");
                }
            }
        }

        /// <summary>
        /// 이벤트 전투용 임시 BattleStage 생성
        /// </summary>
        private BattleStageSO CreateEventBattleStage(BattleSO battleData)
        {
            var tempStage = ScriptableObject.CreateInstance<BattleStageSO>();
            tempStage.name = $"EventBattle_{battleData.BattleName}";

            if (debugMode)
                Debug.Log($"[BattleTestController] 이벤트 전투용 임시 BattleStage 생성: {tempStage.name}");

            return tempStage;
        }
        #endregion

        /// <summary>
        /// 필드 변경 이벤트 핸들러
        /// </summary>
        private void OnFieldChanged(ElementType newField, ElementType previousField)
        {
            if (debugMode)
            {
                Debug.Log($"[BattleTestController] 필드 변경 이벤트 수신: {previousField} -> {newField}");
                Debug.Log($"[BattleTestController] BattleUIManager 상태: {(BattleUIManager.Instance != null ? "존재함" : "null")}");
            }

            // BattleUIManager에 필드 변경 이벤트 전달
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldChanged(newField, previousField);

                if (debugMode)
                    Debug.Log("[BattleTestController] BattleUIManager에 필드 변경 이벤트 전달 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] BattleUIManager가 null입니다! 필드 변경 이벤트를 전달할 수 없습니다.");
            }
        }

        /// <summary>
        /// 필드 효과 적용 이벤트 핸들러
        /// </summary>
        private void OnFieldEffectApplied(FieldEffectSO fieldEffect)
        {
            if (debugMode)
            {
                Debug.Log($"[BattleTestController] 필드 효과 적용 이벤트 수신: {fieldEffect.EffectName} ({fieldEffect.FieldElement})");
                Debug.Log($"[BattleTestController] BattleUIManager 상태: {(BattleUIManager.Instance != null ? "존재함" : "null")}");
            }

            // BattleUIManager에 필드 효과 적용 이벤트 전달
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldEffectApplied(fieldEffect);

                if (debugMode)
                    Debug.Log("[BattleTestController] BattleUIManager에 필드 효과 적용 이벤트 전달 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] BattleUIManager가 null입니다! 필드 효과 적용 이벤트를 전달할 수 없습니다.");
            }
        }

        /// <summary>
        /// 필드 효과 제거 이벤트 핸들러
        /// </summary>
        private void OnFieldEffectRemoved(FieldEffectSO fieldEffect)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 필드 효과 제거: {fieldEffect.EffectName}");

            // BattleUIManager에 필드 효과 제거 이벤트 전달
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldEffectRemoved(fieldEffect);
            }
        }

        /// <summary>
        /// 플레이어 애니메이션 변경 이벤트 핸들러
        /// </summary>
        private void OnPlayerAnimationChanged(PlayerManager.PlayerAnimationState newState)
        {
            if (debugMode)
                // Debug.Log($"[BattleTestController] 플레이어 애니메이션 변경: {newState}");

                // PlayerBattleManager에 애니메이션 변경 알림
                if (PlayerBattleManager.Instance != null)
                {
                    PlayerBattleManager.Instance.UpdatePlayerSprite();
                }
        }
        #endregion

        // 필드 UI 관련 메서드들은 BattleUIManager로 이전됨

        #region Input Management
        /// <summary>
        /// 플레이어 입력 차단/허용 설정
        /// </summary>
        /// <param name="blocked">true면 입력 차단, false면 허용</param>
        public void SetInputBlocked(bool blocked)
        {
            isInputBlocked = blocked;

            if (debugMode)
                Debug.Log($"[BattleTestController] 플레이어 입력 {(blocked ? "차단" : "허용")}");

            // UI 요소들의 상호작용 차단/허용
            SetUIInteractable(!blocked);
        }

        /// <summary>
        /// 현재 입력이 차단되어 있는지 확인
        /// </summary>
        public bool IsInputBlocked => isInputBlocked;

        /// <summary>
        /// UI 요소들의 상호작용 설정
        /// </summary>
        private void SetUIInteractable(bool interactable)
        {
            if (debugMode)
            {
                Debug.Log($"[BattleTestController] SetUIInteractable: {interactable}");
                Debug.Log($"[BattleTestController] EventSystem.current 상태: {(EventSystem.current != null ? $"존재함 (enabled: {EventSystem.current.enabled})" : "null")}");
            }

            // EventSystem 차단/허용 (가장 중요)
            if (EventSystem.current != null)
            {
                EventSystem.current.enabled = interactable;

                if (debugMode)
                    Debug.Log($"[BattleTestController] EventSystem.enabled = {interactable} 설정 완료");
            }
            else
            {
                Debug.LogError("[BattleTestController] EventSystem.current가 null입니다!");

                // EventSystem을 찾아서 활성화 시도
                var eventSystem = FindObjectOfType<EventSystem>();
                if (eventSystem != null)
                {
                    eventSystem.enabled = interactable;
                    if (debugMode)
                        Debug.Log($"[BattleTestController] 찾은 EventSystem에 enabled = {interactable} 설정");
                }
            }

            // 카드 영역 차단/허용
            var cardAreas = FindObjectsOfType<GraphicRaycaster>();
            if (debugMode)
                Debug.Log($"[BattleTestController] GraphicRaycaster 개수: {cardAreas.Length}");

            foreach (var area in cardAreas)
            {
                area.enabled = interactable;
            }

            // 추가적인 UI 요소들 (버튼 등) 차단/허용
            var buttons = FindObjectsOfType<Button>();
            if (debugMode)
                Debug.Log($"[BattleTestController] Button 개수: {buttons.Length}");

            foreach (var button in buttons)
            {
                button.interactable = interactable;
            }

            if (debugMode)
                Debug.Log($"[BattleTestController] SetUIInteractable 완료");
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Battle Info")]
        public void DebugBattleInfo()
        {
            Debug.Log($"=== BattleTestController Debug Info ===");
            Debug.Log($"모드: {(useTestMode ? "테스트" : "프로덕션")}");
            Debug.Log($"전투 활성: {isBattleActive}");
            Debug.Log($"플레이어 턴: {isPlayerTurn}");
            Debug.Log($"현재 턴: {currentTurn}");
            Debug.Log($"현재 층: {currentFloor} ({currentFloorType})");
            Debug.Log($"현재 배틀스테이지: {currentBattleStage?.name ?? "없음"}");
            Debug.Log($"테스트 배틀스테이지: {testBattleStage?.name ?? "없음"}");
            Debug.Log($"플레이어 위치: {PlayerBattleManager.Instance?.GetPlayerGridPosition() ?? Vector2Int.zero}");
            Debug.Log($"현재 타겟: {TargetManager.Instance?.CurrentTarget?.EnemyName ?? "없음"}");
            Debug.Log($"살아있는 몬스터: {TargetManager.Instance?.GetAliveEnemies().Count ?? 0}마리");
        }
        #endregion
    }
}