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
    /// 턴제 전투 테스트를 위한 메인 컨트롤러 (리팩토링 버전)
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        [Header("Test Data")]
        [SerializeField] private List<CardSO> testCards = new List<CardSO>();
        [SerializeField] private BattleStageSO testBattleStage;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // 전투 상태
        private bool isBattleActive = false;
        private bool isPlayerTurn = true;
        private int currentTurn = 1;

        #region Unity Events
        private void Start()
        {
            InitializeBattleTest();
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
            // FieldManager 이벤트 구독 해제
            FieldManager.OnFieldChanged -= OnFieldChanged;
            FieldManager.OnFieldEffectApplied -= OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved -= OnFieldEffectRemoved;
        }
        #endregion

        #region Event Subscriptions
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

            // 테스트 데이터 설정
            SetupTestData();

            // ComboManager 강제 초기화
            InitializeComboManager();

            // FieldManager 초기화
            InitializeFieldManager();

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

            // 모든 매니저 초기화 완료 후 이벤트 구독
            SubscribeToManagerEvents();

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

            // 필드 초기화 (전투 시작 시)
            FieldManager.Instance.ResetField();

            // 필드 변경 이벤트 구독
            FieldManager.OnFieldChanged += OnFieldChanged;
            FieldManager.OnFieldEffectApplied += OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved += OnFieldEffectRemoved;

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

            // 몬스터 스폰 후 타겟 설정
            StartCoroutine(SpawnTestMonstersAndSetTarget());

            // 플레이어 턴 시작
            StartPlayerTurn();

            // UI 업데이트
            BattleUIManager.Instance?.UpdateAllUI();
        }

        /// <summary>
        /// 몬스터 스폰 후 타겟 설정 코루틴
        /// </summary>
        private IEnumerator SpawnTestMonstersAndSetTarget()
        {
            // MonsterSpawnManager를 통해 몬스터 스폰 (테스트 배틀 스테이지 전달)
            MonsterSpawnManager.Instance?.SpawnTestMonsters(testBattleStage);

            // 모든 몬스터 스폰이 완료될 때까지 대기
            yield return new WaitForSeconds(1f);

            // 모든 스폰이 완료된 후 타겟 설정
            if (TargetManager.Instance != null)
            {
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                    if (debugMode)
                        Debug.Log("[BattleTestController] 모든 몬스터 스폰 완료 후 초기 타겟 설정");
                }
                else
                {
                    if (debugMode)
                        Debug.Log("[BattleTestController] 이미 타겟이 설정되어 있음");
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

            // 매니저들에 턴 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 카드 드로우
            if (CardManager.Instance != null)
            {
                // 손패 초기화 후 최대 손패까지 드로우
                var drawnCards = CardManager.Instance.DrawCardsToMax();

                if (debugMode)
                    Debug.Log($"[BattleTestController] {drawnCards.Count}장 드로우 완료");
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
                    yield return new WaitForSeconds(0.5f); // 몬스터 간 딜레이
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

            // MonsterSpawnManager를 통해 몬스터 이동
            yield return StartCoroutine(MonsterSpawnManager.Instance.ProcessMonsterMovement(monster));

            // MonsterSpawnManager를 통해 몬스터 공격
            yield return StartCoroutine(MonsterSpawnManager.Instance.ProcessMonsterAttack(monster));
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
            if (!isPlayerTurn || !isBattleActive) return;

            // BattleUIManager에서 조합 슬롯 카드들 가져오기
            var comboCards = BattleUIManager.Instance?.GetComboSlotCards() ?? new List<Card>();

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

                // CardManager를 통해 카드 사용
                if (CardManager.Instance != null)
                {
                    CardManager.Instance.UseCardInstances(comboCards.ToArray());
                }

                // 단독 카드 효과 실행
                ExecuteCardEffect(comboCards[0].CardData);

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

                    // ComboExecutionContext 생성 (카드 소모 활성화)
                    var context = new ComboExecutionContext(comboCards.ToArray(), currentField);
                    context.consumeCards = true;

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

            // 조합 완료 후 카드 무덤 애니메이션 및 슬롯 정리
            StartCoroutine(ProcessCardToGraveAnimation(comboCards, result));

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
        private IEnumerator ProcessCardToGraveAnimation(List<Card> usedCards, ComboExecutionResult result)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] ProcessCardToGraveAnimation 시작: {usedCards?.Count ?? 0}장");

            if (usedCards == null || usedCards.Count == 0)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 사용된 카드가 없어 무덤 애니메이션 건너뜀");
                yield break;
            }

            // 조합창에서 카드 UI들 가져오기
            var comboSlotCardUIs = new List<GameObject>();
            if (BattleUIManager.Instance != null)
            {
                // 조합창의 카드 UI들 수집
                var elementSlotUI = BattleUIManager.Instance.GetElementSlotUI();
                var active1SlotUI = BattleUIManager.Instance.GetActive1SlotUI();
                var active2SlotUI = BattleUIManager.Instance.GetActive2SlotUI();

                if (debugMode)
                {
                    Debug.Log($"[BattleTestController] 슬롯 UI 확인 - Element: {(elementSlotUI != null ? "있음" : "없음")}, Active1: {(active1SlotUI != null ? "있음" : "없음")}, Active2: {(active2SlotUI != null ? "있음" : "없음")}");
                }

                if (elementSlotUI != null) comboSlotCardUIs.Add(elementSlotUI);
                if (active1SlotUI != null) comboSlotCardUIs.Add(active1SlotUI);
                if (active2SlotUI != null) comboSlotCardUIs.Add(active2SlotUI);

                if (debugMode)
                    Debug.Log($"[BattleTestController] 수집된 조합창 UI: {comboSlotCardUIs.Count}개");
            }

            // 무덤 애니메이션 실행
            if (CardDrawAnimationManager.Instance != null && comboSlotCardUIs.Count > 0)
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
            }
        }

        /// <summary>
        /// 카드 효과 실행 (간단한 데미지/힐 처리)
        /// </summary>
        private void ExecuteCardEffect(CardSO cardData)
        {
            if (cardData == null) return;

            if (debugMode)
                Debug.Log($"[BattleTestController] {cardData.CardName} 효과 실행: 데미지={cardData.BaseDamage}, 힐={cardData.BaseHeal}, 대상={cardData.Target}");

            // 데미지 처리 (필드 보너스 포함)
            if (cardData.BaseDamage > 0)
            {
                int finalDamage = CalculateFinalDamage(cardData.BaseDamage, cardData.Element);

                switch (cardData.Target)
                {
                    case TargetType.SingleEnemy:
                        TargetManager.Instance?.DamageTarget(finalDamage);
                        break;
                    case TargetType.AllEnemies:
                        TargetManager.Instance?.DamageAllEnemies(finalDamage);
                        break;
                }
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

            // 모든 몬스터 처치 확인
            var aliveMonsters = TargetManager.Instance?.GetAliveEnemies() ?? new List<Maglin.Enemy.Enemy>();

            if (aliveMonsters.Count == 0)
            {
                EndBattle(true);
                return true;
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

            // PlayerBattleManager에 전투 종료 알림
            PlayerBattleManager.Instance?.OnBattleEnd();

            if (victory)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 전투 승리! 보상을 표시합니다.");

                // 전투 승리 시 보상 표시
                ShowBattleRewards();
            }
            else
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] 전투 패배! 게임 오버");
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

            // 현재 층과 층 타입 결정 (테스트용으로 기본값 사용)
            int currentFloor = 1;
            FloorType floorType = FloorType.Normal;

            // testBattleStage에서 층 정보를 가져올 수 있다면 사용
            if (testBattleStage != null)
            {
                // BattleStageSO에 층 정보가 있다면 여기서 설정
                // 현재는 테스트용으로 기본값 사용
                if (debugMode)
                    Debug.Log($"[BattleTestController] BattleStage: {testBattleStage.name}");
            }

            if (debugMode)
                Debug.Log($"[BattleTestController] {currentFloor}층 {floorType} 보상 생성 요청");

            // BattleUIManager의 보상 UI 상태 확인
            if (BattleUIManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[BattleTestController] BattleUIManager 확인됨");
            }
            else
            {
                Debug.LogError("[BattleTestController] BattleUIManager가 없습니다!");
                return;
            }

            // RewardManager를 통해 보상 생성 및 표시
            RewardManager.Instance.ShowBattleRewards(currentFloor, floorType);
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

            // 여기서 다음 단계로 진행 (예: 다음 층으로 이동, 메인 메뉴 복귀 등)
            // 현재는 테스트 환경이므로 간단히 로그만 출력
            if (debugMode)
                Debug.Log("[BattleTestController] 전투 완전 종료. 다음 단계로 진행 가능.");
        }

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
        /// 필드 변경 이벤트 핸들러
        /// </summary>
        private void OnFieldChanged(ElementType newField, ElementType previousField)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 필드 변경: {previousField} -> {newField}");

            // BattleUIManager에 필드 변경 이벤트 전달
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldChanged(newField, previousField);
            }
        }

        /// <summary>
        /// 필드 효과 적용 이벤트 핸들러
        /// </summary>
        private void OnFieldEffectApplied(FieldEffectSO fieldEffect)
        {
            if (debugMode)
                Debug.Log($"[BattleTestController] 필드 효과 활성화: {fieldEffect.EffectName}");

            // BattleUIManager에 필드 효과 적용 이벤트 전달
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldEffectApplied(fieldEffect);
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
        #endregion

        // 필드 UI 관련 메서드들은 BattleUIManager로 이전됨

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Battle Info")]
        public void DebugBattleInfo()
        {
            Debug.Log($"=== BattleTestController Debug Info ===");
            Debug.Log($"전투 활성: {isBattleActive}");
            Debug.Log($"플레이어 턴: {isPlayerTurn}");
            Debug.Log($"현재 턴: {currentTurn}");
            Debug.Log($"플레이어 위치: {PlayerBattleManager.Instance?.GetPlayerGridPosition() ?? Vector2Int.zero}");
            Debug.Log($"현재 타겟: {TargetManager.Instance?.CurrentTarget?.EnemyName ?? "없음"}");
            Debug.Log($"살아있는 몬스터: {TargetManager.Instance?.GetAliveEnemies().Count ?? 0}마리");
        }
        #endregion
    }
}