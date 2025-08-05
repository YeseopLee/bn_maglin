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
    /// 프로덕션용 전투 컨트롤러
    /// FloorManager와 연동하여 실제 게임 플로우에서 동작
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Battle Data")]
        [SerializeField] private BattleStageSO currentBattleStage;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // 전투 상태
        private bool isBattleActive = false;
        private bool isPlayerTurn = true;
        private int currentTurn = 1;
        private bool isInputBlocked = false;

        // 현재 층 정보
        private int currentFloor = 1;
        private FloorType currentFloorType = FloorType.Normal;

        #region Unity Events
        private void Start()
        {
            // FloorManager 이벤트 구독
            SubscribeToFloorManagerEvents();

            // 자동 초기화는 하지 않음 - FloorManager가 호출할 때까지 대기
            if (debugMode)
                Debug.Log("[BattleController] 전투 컨트롤러 준비 완료, FloorManager 신호 대기 중");
        }

        private void Update()
        {
            HandleInput();
        }

        private void OnEnable()
        {
            // 매니저 이벤트 구독
            SubscribeToManagerEvents();
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
                    Debug.Log("[BattleController] FloorManager 이벤트 구독 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleController] FloorManager가 아직 없습니다. 나중에 구독 시도");
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
            if (TargetManager.Instance != null)
            {
                TargetManager.OnTargetChanged += OnTargetChanged;
                TargetManager.OnTargetDied += OnTargetDied;
            }

            // 보상 UI 이벤트 구독
            BattleUIManager.OnRewardSelected += OnRewardSelected;
            BattleUIManager.OnRewardSkipped += OnRewardSkipped;

            // RewardManager 이벤트 구독
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnBattleRewardsGenerated += OnBattleRewardsGenerated;
                RewardManager.Instance.OnRewardSelected += OnRewardApplied;
                RewardManager.Instance.OnGoldReceived += OnGoldReceived;
                RewardManager.Instance.OnRewardsCompleted += OnRewardsCompleted;

                if (debugMode)
                    Debug.Log("[BattleController] RewardManager 이벤트 구독 완료");
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
            if (TargetManager.Instance != null)
            {
                TargetManager.OnTargetChanged -= OnTargetChanged;
                TargetManager.OnTargetDied -= OnTargetDied;
            }

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

        #region Floor Manager Events
        /// <summary>
        /// 층 시작 이벤트 처리
        /// </summary>
        private void OnFloorStarted(FloorInfo floorInfo)
        {
            // 전투 관련 층만 처리
            if (floorInfo.floorType == FloorType.Normal ||
                floorInfo.floorType == FloorType.Elite ||
                floorInfo.floorType == FloorType.Boss ||
                floorInfo.floorType == FloorType.Start)
            {
                currentFloor = floorInfo.floorNumber;
                currentFloorType = floorInfo.floorType;

                if (debugMode)
                    Debug.Log($"[BattleController] {currentFloor}층 전투 시작: {floorInfo.floorType}");

                // 층에 맞는 배틀 스테이지 로드
                LoadBattleStageForFloor(currentFloor, currentFloorType);

                // 전투 초기화 및 시작
                InitializeBattle();
            }
        }

        /// <summary>
        /// 층 완료 이벤트 처리
        /// </summary>
        private void OnFloorCompleted(FloorInfo floorInfo)
        {
            if (debugMode)
                Debug.Log($"[BattleController] {floorInfo.floorNumber}층 완료: {floorInfo.floorType}");
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
                    // FloorManager의 GetBattleStageForFloor 메서드 사용
                    battleStage = FloorManager.Instance.GetBattleStageForFloor(floor, floorType);
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
            }

            if (battleStage != null)
            {
                currentBattleStage = battleStage;
                if (debugMode)
                    Debug.Log($"[BattleController] {floor}층 배틀 스테이지 로드 성공: {battleStage.name}");
            }
            else
            {
                Debug.LogWarning($"[BattleController] {floor}층용 배틀 스테이지를 찾을 수 없습니다. 기본 스테이지 사용");

                // 기본 배틀 스테이지 생성 (임시)
                CreateDefaultBattleStage(floor, floorType);
            }
        }

        /// <summary>
        /// 기본 배틀 스테이지 생성 (배틀 스테이지가 없을 때 임시용)
        /// </summary>
        private void CreateDefaultBattleStage(int floor, FloorType floorType)
        {
            // 런타임에서 임시 배틀 스테이지 생성
            var tempStage = ScriptableObject.CreateInstance<BattleStageSO>();
            tempStage.name = $"DefaultStage_F{floor}";

            // 기본 몬스터 설정 (층 타입에 따라)
            // 실제로는 Resources에서 몬스터를 로드해야 함

            currentBattleStage = tempStage;

            if (debugMode)
                Debug.Log($"[BattleController] 기본 배틀 스테이지 생성: {floor}층 {floorType}");
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

        #region Battle Initialization
        /// <summary>
        /// 전투 초기화
        /// </summary>
        private void InitializeBattle()
        {
            if (debugMode)
                Debug.Log("[BattleController] 전투 초기화 시작");

            // 매니저들이 이미 초기화되어 있는지 확인
            if (!VerifyManagersReady())
            {
                Debug.LogError("[BattleController] 필수 매니저들이 초기화되지 않았습니다!");
                return;
            }

            // 전투별 매니저 초기화 (씬별로 필요한 것들만)
            InitializeBattleSpecificManagers();

            // ComboManager 및 FieldManager 초기화
            InitializeComboManager();
            InitializeFieldManager();

            // 전투 시작
            StartBattle();
        }

        /// <summary>
        /// 필수 매니저들이 준비되었는지 확인
        /// </summary>
        private bool VerifyManagersReady()
        {
            var missingManagers = new List<string>();

            if (GameManager.Instance == null) missingManagers.Add("GameManager");
            if (FloorManager.Instance == null) missingManagers.Add("FloorManager");
            if (PlayerManager.Instance == null) missingManagers.Add("PlayerManager");
            if (CardManager.Instance == null) missingManagers.Add("CardManager");

            if (missingManagers.Count > 0)
            {
                Debug.LogError($"[BattleController] 누락된 매니저들: {string.Join(", ", missingManagers)}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 전투별 매니저 초기화 (씬에 특화된 것들만)
        /// </summary>
        private void InitializeBattleSpecificManagers()
        {
            // GridFieldManager 초기화
            if (GridFieldManager.Instance != null)
            {
                GridFieldManager.Instance.InitializeGridField();
                if (debugMode)
                    Debug.Log("[BattleController] GridFieldManager 초기화 완료");
            }

            // BattleUIManager 초기화
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.InitializeUIManager();
                if (debugMode)
                    Debug.Log("[BattleController] BattleUIManager 초기화 완료");
            }

            // PlayerBattleManager 초기화
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.InitializePlayerBattleManager();
                if (debugMode)
                    Debug.Log("[BattleController] PlayerBattleManager 초기화 완료");
            }

            // TargetManager 초기화
            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.InitializeTargetManager();
                Vector2Int playerPos = PlayerBattleManager.Instance != null ?
                    PlayerBattleManager.Instance.GetPlayerGridPosition() : Vector2Int.zero;
                TargetManager.Instance.SetPlayerPosition(playerPos);
                if (debugMode)
                    Debug.Log("[BattleController] TargetManager 초기화 완료");
            }

            // MonsterSpawnManager 초기화
            if (MonsterSpawnManager.Instance != null)
            {
                MonsterSpawnManager.Instance.InitializeSpawnManager();
                Vector2Int playerPos = PlayerBattleManager.Instance != null ?
                    PlayerBattleManager.Instance.GetPlayerGridPosition() : Vector2Int.zero;
                MonsterSpawnManager.Instance.SetPlayerPosition(playerPos);
                if (debugMode)
                    Debug.Log("[BattleController] MonsterSpawnManager 초기화 완료");
            }

            // RewardManager 초기화 (필요시)
            InitializeRewardManager();
        }

        /// <summary>
        /// RewardManager 초기화
        /// </summary>
        private void InitializeRewardManager()
        {
            if (RewardManager.Instance == null)
            {
                GameObject rewardManagerObj = new GameObject("RewardManager");
                rewardManagerObj.AddComponent<RewardManager>();
                if (debugMode)
                    Debug.Log("[BattleController] RewardManager 생성됨");
            }

            // RewardManager 설정
            if (RewardManager.Instance != null)
            {
                SetupRewardManagerData();
                if (debugMode)
                    Debug.Log("[BattleController] RewardManager 초기화 완료");
            }
        }

        /// <summary>
        /// RewardManager 데이터 설정
        /// </summary>
        private void SetupRewardManagerData()
        {
            var rewardConfig = Resources.Load<RewardConfigSO>("Rewards/RewardConfig");
            if (rewardConfig == null)
            {
                rewardConfig = Resources.Load<RewardConfigSO>("RewardConfig");
            }

            if (rewardConfig != null)
            {
                RewardManager.Instance.SetRewardConfig(rewardConfig);
                if (debugMode)
                    Debug.Log($"[BattleController] RewardConfig 로드 성공: {rewardConfig.name}");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleController] RewardConfig를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// ComboManager 강제 초기화
        /// </summary>
        private void InitializeComboManager()
        {
            if (ComboManager.Instance != null)
            {
                // ComboManager 디버그 모드 설정
                var debugField = typeof(ComboManager).GetField("debugMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (debugField != null)
                {
                    debugField.SetValue(ComboManager.Instance, debugMode);
                }

                // 조합 캐시 로드
                var methodInfo = typeof(ComboManager).GetMethod("LoadCombinationCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (methodInfo != null)
                {
                    methodInfo.Invoke(ComboManager.Instance, null);
                    if (debugMode)
                        Debug.Log("[BattleController] ComboManager 캐시 로드 완료");
                }
            }
        }

        /// <summary>
        /// FieldManager 초기화
        /// </summary>
        private void InitializeFieldManager()
        {
            if (FieldManager.Instance == null) return;

            // 필드 초기화
            FieldManager.Instance.ResetField();

            // 필드 변경 이벤트 구독
            FieldManager.OnFieldChanged += OnFieldChanged;
            FieldManager.OnFieldEffectApplied += OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved += OnFieldEffectRemoved;

            if (debugMode)
                Debug.Log("[BattleController] FieldManager 초기화 완료");
        }
        #endregion

        #region Battle Flow
        /// <summary>
        /// 전투 시작
        /// </summary>
        private void StartBattle()
        {
            if (debugMode)
                Debug.Log("[BattleController] 전투 시작");

            isBattleActive = true;
            isPlayerTurn = true;
            currentTurn = 1;

            // 매니저들에 전투 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 플레이어 위치 설정
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.SetPlayerGridPosition(new Vector2Int(0, 0));
            }

            // 몬스터 스폰 후 타겟 설정
            StartCoroutine(SpawnMonstersAndSetTarget());

            // 플레이어 턴 시작
            StartPlayerTurn();

            // UI 업데이트
            BattleUIManager.Instance?.UpdateAllUI();
        }

        /// <summary>
        /// 몬스터 스폰 후 타겟 설정 코루틴
        /// </summary>
        private IEnumerator SpawnMonstersAndSetTarget()
        {
            // MonsterSpawnManager를 통해 몬스터 스폰
            if (MonsterSpawnManager.Instance != null && currentBattleStage != null)
            {
                // BattleStage에 SpawnMonstersFromBattleStage 메서드가 있다면 사용
                var spawnMethod = typeof(MonsterSpawnManager).GetMethod("SpawnMonstersFromBattleStage");
                if (spawnMethod != null)
                {
                    spawnMethod.Invoke(MonsterSpawnManager.Instance, new object[] { currentBattleStage });
                }
                else
                {
                    // 폴백: 테스트 몬스터 스폰 메서드 사용
                    var testSpawnMethod = typeof(MonsterSpawnManager).GetMethod("SpawnTestMonsters");
                    if (testSpawnMethod != null)
                    {
                        testSpawnMethod.Invoke(MonsterSpawnManager.Instance, new object[] { currentBattleStage });
                        if (debugMode)
                            Debug.Log("[BattleController] 테스트 몬스터 스폰 메서드 사용");
                    }
                }
            }

            // 몬스터 스폰 완료 대기
            yield return new WaitForSeconds(1f);

            // 타겟 설정
            if (TargetManager.Instance != null)
            {
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                    if (debugMode)
                        Debug.Log("[BattleController] 초기 타겟 설정 완료");
                }
            }
        }

        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        private void StartPlayerTurn()
        {
            if (debugMode)
                Debug.Log($"[BattleController] 플레이어 턴 {currentTurn} 시작");

            isPlayerTurn = true;

            // 매니저들에 턴 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 카드 드로우
            if (CardManager.Instance != null)
            {
                var drawnCards = CardManager.Instance.DrawCardsToMax();
                if (debugMode)
                    Debug.Log($"[BattleController] {drawnCards.Count}장 드로우 완료");
            }

            // 조합 슬롯 초기화
            BattleUIManager.Instance?.ResetComboSlots();

            // 타겟 검증
            TargetManager.Instance?.ValidateTarget();
        }

        /// <summary>
        /// 플레이어 턴 종료
        /// </summary>
        public void EndPlayerTurn()
        {
            if (!isPlayerTurn || !isBattleActive) return;

            if (debugMode)
                Debug.Log("[BattleController] 플레이어 턴 종료");

            isPlayerTurn = false;

            // 매니저들에 턴 상태 알림
            BattleUIManager.Instance?.SetBattleState(isBattleActive, isPlayerTurn);

            // 손패 카드들 덱으로 복귀
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
                Debug.Log($"[BattleController] 몬스터 턴 시작");

            var aliveMonsters = TargetManager.Instance?.GetAliveEnemies() ?? new List<Maglin.Enemy.Enemy>();

            if (debugMode)
                Debug.Log($"[BattleController] 활성 몬스터 {aliveMonsters.Count}마리");

            foreach (var monster in aliveMonsters)
            {
                if (monster != null && monster.IsAlive)
                {
                    yield return StartCoroutine(ProcessMonsterAction(monster));
                    yield return new WaitForSeconds(0.5f);
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
                Debug.Log($"[BattleController] {monster.EnemyName} 행동 시작");

            // MonsterSpawnManager를 통해 몬스터 이동 및 공격
            if (MonsterSpawnManager.Instance != null)
            {
                yield return StartCoroutine(MonsterSpawnManager.Instance.ProcessMonsterMovement(monster));
                yield return StartCoroutine(MonsterSpawnManager.Instance.ProcessMonsterAttack(monster));
            }
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
                    Debug.Log($"[BattleController] 추가 드로우 성공: {drawnCard.CardName}");
                }
            }
        }

        /// <summary>
        /// 조합 실행
        /// </summary>
        public void ExecuteCombo()
        {
            if (!isPlayerTurn || !isBattleActive) return;

            var comboCards = BattleUIManager.Instance?.GetComboSlotCards() ?? new List<Card>();

            if (comboCards.Count == 0)
            {
                if (debugMode)
                    Debug.Log("[BattleController] 조합할 카드가 없습니다.");
                return;
            }

            ComboExecutionResult result = null;

            // 단독 사용 처리
            if (comboCards.Count == 1 && comboCards[0].CardData.CanUseSolo)
            {
                if (debugMode)
                    Debug.Log($"[BattleController] 단독 카드 사용: {comboCards[0].CardName}");

                if (CardManager.Instance != null)
                {
                    CardManager.Instance.UseCardInstances(comboCards.ToArray());
                }

                ExecuteCardEffect(comboCards[0].CardData);
            }
            // 조합 처리
            else if (comboCards.Count >= 2)
            {
                if (debugMode)
                    Debug.Log($"[BattleController] {comboCards.Count}장 카드 조합 실행");

                if (ComboManager.Instance != null)
                {
                    ElementType currentField = FieldManager.Instance != null ?
                        FieldManager.Instance.CurrentFieldElement : ElementType.None;

                    var context = new ComboExecutionContext(comboCards.ToArray(), currentField);
                    context.consumeCards = true;

                    result = ComboManager.Instance.ExecuteCombination(context);

                    if (debugMode)
                    {
                        Debug.Log($"[BattleController] 조합 결과: {(result.Success ? "성공" : "실패")}");
                    }

                    if (result.Success)
                    {
                        ExecuteCardEffect(result.ResultCardData);
                    }
                }
            }

            // 조합 완료 후 슬롯 정리
            if (result != null)
            {
                if (result.Success)
                {
                    BattleUIManager.Instance?.ClearComboSlotsUIOnly();
                }
                else
                {
                    BattleUIManager.Instance?.ClearComboSlots();
                }
            }
            else
            {
                BattleUIManager.Instance?.ClearComboSlotsUIOnly();
            }

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
        /// 카드 효과 실행
        /// </summary>
        private void ExecuteCardEffect(CardSO cardData)
        {
            if (cardData == null) return;

            if (debugMode)
                Debug.Log($"[BattleController] {cardData.CardName} 효과 실행");

            // 데미지 처리
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
                    Debug.Log($"[BattleController] 플레이어 힐: {cardData.BaseHeal}");
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

            float fieldBonus = FieldManager.Instance.CalculateFieldBonus(cardElement);
            int finalDamage = Mathf.RoundToInt(baseDamage * fieldBonus);

            if (debugMode && fieldBonus > 1.0f)
                Debug.Log($"[BattleController] 필드 보너스 적용: {baseDamage} -> {finalDamage}");

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
                Debug.Log($"[BattleController] 필드 효과 적용: {fieldEffect.EffectName}");
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
                Debug.Log($"[BattleController] 전투 종료: {(victory ? "승리" : "패배")}");

            isBattleActive = false;

            // 매니저들에 전투 종료 알림
            BattleUIManager.Instance?.SetBattleState(false, false);
            PlayerBattleManager.Instance?.OnBattleEnd();

            if (victory)
            {
                // 전투 승리 시 보상 표시
                ShowBattleRewards();
            }
            else
            {
                // 패배 시 게임 오버 처리
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GameOver();
                }
            }
        }

        /// <summary>
        /// 전투 보상 표시
        /// </summary>
        private void ShowBattleRewards()
        {
            if (RewardManager.Instance == null) return;

            if (debugMode)
                Debug.Log("[BattleController] 보상 생성 시작");

            // RewardManager를 통해 보상 생성 및 표시
            RewardManager.Instance.ShowBattleRewards(currentFloor, currentFloorType);
        }
        #endregion

        #region Reward Event Handlers
        /// <summary>
        /// 전투 보상이 생성되었을 때 호출
        /// </summary>
        private void OnBattleRewardsGenerated(BattleRewardData rewardData)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 보상 생성됨: 골드 {rewardData.goldAmount}, 카드 {rewardData.cardChoices?.Length ?? 0}개");

            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.UpdateBattleRewardUI(rewardData);
                BattleUIManager.Instance.ShowRewardUI();
            }
        }

        /// <summary>
        /// 보상 선택 이벤트
        /// </summary>
        private void OnRewardSelected(int slotIndex)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 보상 슬롯 {slotIndex} 선택됨");

            if (BattleUIManager.Instance == null || RewardManager.Instance == null)
                return;

            var selectedReward = BattleUIManager.Instance.GetSelectedReward(slotIndex);
            if (selectedReward != null)
            {
                RewardManager.Instance.SelectReward(selectedReward);
            }
        }

        /// <summary>
        /// 골드 획득 이벤트
        /// </summary>
        private void OnGoldReceived(int goldAmount)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 골드 {goldAmount} 획득");
        }

        /// <summary>
        /// 보상 적용 이벤트
        /// </summary>
        private void OnRewardApplied(RewardItem appliedReward)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 보상 적용됨: {appliedReward.rewardType}");
        }

        /// <summary>
        /// 보상 완료 이벤트
        /// </summary>
        private void OnRewardsCompleted()
        {
            if (debugMode)
                Debug.Log("[BattleController] 보상 선택 완료");

            // 보상 UI 숨기기
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.HideRewardUI();
            }

            // 전투 완료 후 다음 층으로 진행
            StartCoroutine(CompleteBattleAndProceed());
        }

        /// <summary>
        /// 전투 완료 후 다음 층 진행 처리
        /// </summary>
        private System.Collections.IEnumerator CompleteBattleAndProceed()
        {
            if (debugMode)
                Debug.Log("[BattleController] 전투 완료, 다음 층으로 진행 시작");

            // 잠시 대기
            yield return new WaitForSeconds(0.5f);

            // FloorManager에 현재 층 완료 알림
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.CompleteCurrentFloor();

                // 잠시 대기 후 다음 층으로 진행
                yield return new WaitForSeconds(0.5f);

                FloorManager.Instance.ProceedToNextFloor();
            }
            else
            {
                Debug.LogError("[BattleController] FloorManager가 없습니다!");

                // 폴백: GameManager로 직접 메인 메뉴 복귀
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ReturnToMainMenu();
                }
            }
        }

        /// <summary>
        /// 보상 건너뛰기 이벤트
        /// </summary>
        private void OnRewardSkipped()
        {
            if (debugMode)
                Debug.Log("[BattleController] 보상 건너뛰기");

            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.SkipRewards();
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 타겟 변경 이벤트 처리
        /// </summary>
        private void OnTargetChanged(Maglin.Enemy.Enemy enemy)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 타겟 변경: {enemy?.EnemyName ?? "없음"}");
        }

        /// <summary>
        /// 타겟 사망 이벤트 처리
        /// </summary>
        private void OnTargetDied(Maglin.Enemy.Enemy enemy)
        {
            if (debugMode)
                Debug.Log($"[BattleController] 타겟 사망: {enemy?.EnemyName ?? "Unknown"}");

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
                Debug.Log($"[BattleController] 필드 변경: {previousField} -> {newField}");

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
                Debug.Log($"[BattleController] 필드 효과 활성화: {fieldEffect.EffectName}");

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
                Debug.Log($"[BattleController] 필드 효과 제거: {fieldEffect.EffectName}");

            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnFieldEffectRemoved(fieldEffect);
            }
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Battle Info")]
        public void DebugBattleInfo()
        {
            Debug.Log($"=== BattleController Debug Info ===");
            Debug.Log($"전투 활성: {isBattleActive}");
            Debug.Log($"플레이어 턴: {isPlayerTurn}");
            Debug.Log($"현재 턴: {currentTurn}");
            Debug.Log($"현재 층: {currentFloor} ({currentFloorType})");
            Debug.Log($"현재 배틀스테이지: {currentBattleStage?.name ?? "없음"}");
            Debug.Log($"플레이어 위치: {PlayerBattleManager.Instance?.GetPlayerGridPosition() ?? Vector2Int.zero}");
            Debug.Log($"현재 타겟: {TargetManager.Instance?.CurrentTarget?.EnemyName ?? "없음"}");
            Debug.Log($"살아있는 몬스터: {TargetManager.Instance?.GetAliveEnemies().Count ?? 0}마리");
        }
        #endregion

        #region Input Management
        /// <summary>
        /// 플레이어 입력 차단/허용 설정
        /// </summary>
        /// <param name="blocked">true면 입력 차단, false면 허용</param>
        public void SetInputBlocked(bool blocked)
        {
            isInputBlocked = blocked;
            
            if (debugMode)
                Debug.Log($"[BattleController] 플레이어 입력 {(blocked ? "차단" : "허용")}");
            
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
            // 카드 영역 차단/허용
            var cardAreas = FindObjectsOfType<GraphicRaycaster>();
            foreach (var area in cardAreas)
            {
                area.enabled = interactable;
            }
            
            // EventSystem 차단/허용
            if (EventSystem.current != null)
            {
                EventSystem.current.enabled = interactable;
            }
            
            // 추가적인 UI 요소들 (버튼 등) 차단/허용
            var buttons = FindObjectsOfType<Button>();
            foreach (var button in buttons)
            {
                button.interactable = interactable;
            }
        }
        #endregion
    }
}