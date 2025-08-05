using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Maglin.Core;
using Maglin.Player;
using Maglin.Enemy;
using Maglin.Cards;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투 결과 타입
    /// </summary>
    public enum BattleResultType
    {
        Victory,        // 승리
        Defeat,         // 패배
        Draw,           // 무승부 (특수 상황)
        Timeout         // 시간 초과 (특수 상황)
    }

    /// <summary>
    /// 전투 통계 정보
    /// </summary>
    [System.Serializable]
    public class BattleStatistics
    {
        [Header("기본 정보")]
        public int floorNumber;
        public FloorType floorType;
        public float battleDuration;           // 전투 소요 시간 (초)
        public System.DateTime battleStartTime;
        public System.DateTime battleEndTime;

        [Header("플레이어 통계")]
        public int cardsUsed;                  // 사용한 카드 수
        public int damageDealt;                // 가한 피해량
        public int damageTaken;                // 받은 피해량
        public int healingReceived;            // 받은 치유량
        public int manaSpent;                  // 소모한 마나량
        public int combinationsAttempted;      // 시도한 조합 수
        public int successfulCombinations;     // 성공한 조합 수

        [Header("턴 통계")]
        public int totalTurns;                 // 총 턴 수
        public int playerTurns;                // 플레이어 턴 수
        public int enemyTurns;                 // 적 턴 수
        public float averageTurnTime;          // 평균 턴 시간

        [Header("효율성 지표")]
        public float damagePerCard;            // 카드당 평균 피해량
        public float damagePerTurn;            // 턴당 평균 피해량
        public float combinationSuccessRate;   // 조합 성공률 (%)

        /// <summary>
        /// 통계 초기화
        /// </summary>
        public void Initialize(int floor, FloorType type)
        {
            floorNumber = floor;
            floorType = type;
            battleStartTime = System.DateTime.Now;

            cardsUsed = 0;
            damageDealt = 0;
            damageTaken = 0;
            healingReceived = 0;
            manaSpent = 0;
            combinationsAttempted = 0;
            successfulCombinations = 0;
            totalTurns = 0;
            playerTurns = 0;
            enemyTurns = 0;
            averageTurnTime = 0f;
        }

        /// <summary>
        /// 통계 완료 처리
        /// </summary>
        public void Finalize()
        {
            battleEndTime = System.DateTime.Now;
            battleDuration = (float)(battleEndTime - battleStartTime).TotalSeconds;

            // 효율성 지표 계산
            damagePerCard = cardsUsed > 0 ? (float)damageDealt / cardsUsed : 0f;
            damagePerTurn = playerTurns > 0 ? (float)damageDealt / playerTurns : 0f;
            combinationSuccessRate = combinationsAttempted > 0 ?
                (float)successfulCombinations / combinationsAttempted * 100f : 0f;

            averageTurnTime = totalTurns > 0 ? battleDuration / totalTurns : 0f;
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        public string GetDebugString()
        {
            return $"Battle Stats - Floor: {floorNumber} ({floorType}), " +
                   $"Duration: {battleDuration:F1}s, Cards: {cardsUsed}, " +
                   $"Damage: {damageDealt}/{damageTaken}, " +
                   $"Combo Rate: {combinationSuccessRate:F1}%";
        }
    }

    /// <summary>
    /// 전투 결과 정보
    /// </summary>
    [System.Serializable]
    public class BattleResult
    {
        [Header("결과 정보")]
        public BattleResultType resultType;
        public bool isVictory;
        public string resultMessage;

        [Header("통계")]
        public BattleStatistics statistics;

        [Header("보상 정보")]
        public bool hasRewards;
        public RewardItem[] availableRewards;

        [Header("기타")]
        public float resultDelay = 2f;         // 결과 표시 지연 시간
        public bool shouldProceedToNext = true; // 다음 층으로 진행 여부

        public BattleResult(BattleResultType type, BattleStatistics stats)
        {
            resultType = type;
            statistics = stats;
            isVictory = (type == BattleResultType.Victory);

            GenerateResultMessage();
        }

        /// <summary>
        /// 결과 메시지 생성
        /// </summary>
        private void GenerateResultMessage()
        {
            switch (resultType)
            {
                case BattleResultType.Victory:
                    resultMessage = $"{statistics.floorNumber}층 전투 승리!";
                    hasRewards = (statistics.floorType != FloorType.Event &&
                                 statistics.floorType != FloorType.Shop);
                    shouldProceedToNext = true;
                    break;

                case BattleResultType.Defeat:
                    resultMessage = "전투 패배... 1층으로 돌아갑니다.";
                    hasRewards = false;
                    shouldProceedToNext = false;
                    break;

                case BattleResultType.Draw:
                    resultMessage = "무승부... 다시 시도하세요.";
                    hasRewards = false;
                    shouldProceedToNext = false;
                    break;

                case BattleResultType.Timeout:
                    resultMessage = "시간 초과! 자동으로 패배 처리됩니다.";
                    hasRewards = false;
                    shouldProceedToNext = false;
                    break;
            }
        }
    }

    /// <summary>
    /// 전투 결과 및 승리 조건을 관리하는 매니저
    /// </summary>
    public class BattleResultManager : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private bool enableStatistics = true;
        [SerializeField] private bool enableBattleTimer = false;
        [SerializeField] private float maxBattleTime = 300f; // 5분 제한
        [SerializeField] private bool debugMode = false;

        [Header("결과 표시 설정")]
        [SerializeField] private float victoryDelay = 1.5f;
        [SerializeField] private float defeatDelay = 2f;
        [SerializeField] private bool showDetailedStats = true;

        // 싱글톤
        public static BattleResultManager Instance { get; private set; }

        // 이벤트
        public System.Action<BattleResult> OnBattleResultDetermined;
        public System.Action<BattleStatistics> OnStatisticsUpdated;
        public System.Action<BattleResultType> OnResultProcessingStarted;
        public System.Action OnResultProcessingCompleted;

        // 현재 전투 상태
        private BattleStatistics currentStatistics;
        private bool isBattleActive = false;
        private bool isProcessingResult = false;
        private Coroutine battleTimerCoroutine;

        // 참조
        private PlayerManager playerManager;
        private EnemyManager enemyManager;
        private BattleManager battleManager;
        private TurnManager turnManager;

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeReferences();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// 참조 초기화
        /// </summary>
        private void InitializeReferences()
        {
            playerManager = PlayerManager.Instance;
            enemyManager = EnemyManager.Instance;
            battleManager = BattleManager.Instance;

            // TurnManager는 FindObjectOfType으로 찾기
            if (turnManager == null)
            {
                turnManager = FindObjectOfType<TurnManager>();
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // BattleManager 이벤트 (static)
            BattleManager.OnBattleStarted += OnBattleStarted;
            BattleManager.OnBattleEnded += OnBattleEnded;
            BattleManager.OnTurnChanged += OnTurnChanged;
            BattleManager.OnCardsUsed += OnCardsUsed;

            if (playerManager != null)
            {
                PlayerManager.OnHealthChanged += OnPlayerHealthChanged;
                PlayerManager.OnManaChanged += OnPlayerManaChanged;
            }

            if (enemyManager != null)
            {
                EnemyManager.OnAllEnemiesDead += OnAllEnemiesDead;
                EnemyManager.OnEnemyDied += OnEnemyDied;
            }

            // ComboManager 이벤트 (static)
            ComboManager.OnComboAttempted += OnComboAttempted;
            ComboManager.OnComboSuccess += OnComboSuccess;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // BattleManager 이벤트 (static)
            BattleManager.OnBattleStarted -= OnBattleStarted;
            BattleManager.OnBattleEnded -= OnBattleEnded;
            BattleManager.OnTurnChanged -= OnTurnChanged;
            BattleManager.OnCardsUsed -= OnCardsUsed;

            if (playerManager != null)
            {
                PlayerManager.OnHealthChanged -= OnPlayerHealthChanged;
                PlayerManager.OnManaChanged -= OnPlayerManaChanged;
            }

            if (enemyManager != null)
            {
                EnemyManager.OnAllEnemiesDead -= OnAllEnemiesDead;
                EnemyManager.OnEnemyDied -= OnEnemyDied;
            }

            // ComboManager 이벤트 (static)
            ComboManager.OnComboAttempted -= OnComboAttempted;
            ComboManager.OnComboSuccess -= OnComboSuccess;
        }

        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartBattle(int floorNumber, FloorType floorType)
        {
            if (isBattleActive)
            {
                Debug.LogWarning("[BattleResultManager] 이미 전투가 진행 중입니다.");
                return;
            }

            isBattleActive = true;
            isProcessingResult = false;

            // 통계 초기화
            if (enableStatistics)
            {
                currentStatistics = new BattleStatistics();
                currentStatistics.Initialize(floorNumber, floorType);
            }

            // 배틀 타이머 시작
            if (enableBattleTimer)
            {
                battleTimerCoroutine = StartCoroutine(BattleTimerCoroutine());
            }

            if (debugMode)
                Debug.Log($"[BattleResultManager] 전투 시작: {floorNumber}층 ({floorType})");
        }

        /// <summary>
        /// 승리 조건 확인
        /// </summary>
        public bool CheckVictoryConditions()
        {
            if (!isBattleActive) return false;

            // 기본 승리 조건: 모든 적 처치
            if (enemyManager != null && enemyManager.AllEnemiesDead)
            {
                ProcessBattleResult(BattleResultType.Victory);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 패배 조건 확인
        /// </summary>
        public bool CheckDefeatConditions()
        {
            if (!isBattleActive) return false;

            // 기본 패배 조건: 플레이어 사망
            if (playerManager != null && !playerManager.IsAlive)
            {
                ProcessBattleResult(BattleResultType.Defeat);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 전투 결과 처리
        /// </summary>
        public void ProcessBattleResult(BattleResultType resultType)
        {
            if (!isBattleActive || isProcessingResult) return;

            isProcessingResult = true;
            OnResultProcessingStarted?.Invoke(resultType);

            if (debugMode)
                Debug.Log($"[BattleResultManager] 전투 결과 처리: {resultType}");

            // 통계 완료
            if (enableStatistics && currentStatistics != null)
            {
                currentStatistics.Finalize();
                OnStatisticsUpdated?.Invoke(currentStatistics);
            }

            // 배틀 타이머 정지
            if (battleTimerCoroutine != null)
            {
                StopCoroutine(battleTimerCoroutine);
                battleTimerCoroutine = null;
            }

            // 결과 생성
            BattleResult result = new BattleResult(resultType, currentStatistics);

            // 보상 설정 (승리 시)
            if (resultType == BattleResultType.Victory && currentStatistics != null)
            {
                SetupVictoryRewards(result);
            }

            // 결과 처리 코루틴 시작
            StartCoroutine(ProcessResultCoroutine(result));
        }

        /// <summary>
        /// 승리 보상 설정
        /// </summary>
        private void SetupVictoryRewards(BattleResult result)
        {
            // RewardManager를 통해 보상 설정
            if (RewardManager.Instance != null)
            {
                result.hasRewards = true;
                result.shouldProceedToNext = true;
            }
        }

        /// <summary>
        /// 결과 처리 코루틴
        /// </summary>
        private IEnumerator ProcessResultCoroutine(BattleResult result)
        {
            // 결과 표시 지연
            float delay = result.isVictory ? victoryDelay : defeatDelay;
            yield return new WaitForSeconds(delay);

            // 결과 이벤트 발생
            OnBattleResultDetermined?.Invoke(result);

            // 상세 통계 출력 (디버그 모드)
            if (debugMode && showDetailedStats && currentStatistics != null)
            {
                Debug.Log($"[BattleResultManager] {currentStatistics.GetDebugString()}");
            }

            // 후속 처리
            yield return new WaitForSeconds(0.5f);

            if (result.isVictory)
            {
                HandleVictory(result);
            }
            else
            {
                HandleDefeat(result);
            }

            // 처리 완료
            isBattleActive = false;
            isProcessingResult = false;
            OnResultProcessingCompleted?.Invoke();
        }

        /// <summary>
        /// 승리 처리
        /// </summary>
        private void HandleVictory(BattleResult result)
        {
            if (debugMode)
                Debug.Log("[BattleResultManager] 승리 처리");

            // 보상 표시 (RewardManager를 통해)
            if (result.hasRewards && RewardManager.Instance != null && currentStatistics != null)
            {
                if (debugMode)
                    Debug.Log($"[BattleResultManager] 보상 표시 시작 - {currentStatistics.floorNumber}층 {currentStatistics.floorType}");

                RewardManager.Instance.ShowBattleRewards(currentStatistics.floorNumber, currentStatistics.floorType);
            }
            else
            {
                if (debugMode)
                    Debug.Log("[BattleResultManager] 보상이 없거나 RewardManager를 찾을 수 없음");

                // 보상이 없는 경우 바로 층 완료 처리
                if (FloorManager.Instance != null)
                {
                    FloorManager.Instance.OnContentCompleted();
                }
            }
        }

        /// <summary>
        /// 패배 처리
        /// </summary>
        private void HandleDefeat(BattleResult result)
        {
            if (debugMode)
                Debug.Log("[BattleResultManager] 패배 처리");

            // FloorManager에게 리셋 요청
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.ResetToFloor(1);
            }
        }

        /// <summary>
        /// 배틀 타이머 코루틴
        /// </summary>
        private IEnumerator BattleTimerCoroutine()
        {
            yield return new WaitForSeconds(maxBattleTime);

            if (isBattleActive)
            {
                Debug.LogWarning("[BattleResultManager] 전투 시간 초과!");
                ProcessBattleResult(BattleResultType.Timeout);
            }
        }

        /// <summary>
        /// 현재 전투 통계 반환
        /// </summary>
        public BattleStatistics GetCurrentStatistics()
        {
            return currentStatistics;
        }

        /// <summary>
        /// 전투 진행 중인지 확인
        /// </summary>
        public bool IsBattleActive()
        {
            return isBattleActive;
        }

        /// <summary>
        /// 결과 처리 중인지 확인
        /// </summary>
        public bool IsProcessingResult()
        {
            return isProcessingResult;
        }

        #region Event Handlers
        /// <summary>
        /// 전투 시작 이벤트 처리
        /// </summary>
        private void OnBattleStarted(BattleSO battleData)
        {
            if (!isBattleActive && FloorManager.Instance != null)
            {
                StartBattle(FloorManager.Instance.CurrentFloor, FloorManager.Instance.CurrentFloorType);
            }
        }

        /// <summary>
        /// 전투 종료 이벤트 처리
        /// </summary>
        private void OnBattleEnded(bool victory)
        {
            // BattleResultManager가 결과를 직접 관리하므로 
            // 여기서는 추가 검증만 수행
            if (isBattleActive && !isProcessingResult)
            {
                BattleResultType resultType = victory ? BattleResultType.Victory : BattleResultType.Defeat;
                ProcessBattleResult(resultType);
            }
        }

        /// <summary>
        /// 턴 변경 이벤트 처리
        /// </summary>
        private void OnTurnChanged(TurnType turnType, int turnNumber)
        {
            if (currentStatistics != null)
            {
                currentStatistics.totalTurns++;

                if (turnType == TurnType.Player)
                    currentStatistics.playerTurns++;
                else
                    currentStatistics.enemyTurns++;
            }
        }

        /// <summary>
        /// 카드 사용 이벤트 처리
        /// </summary>
        private void OnCardsUsed(CardSO[] cards)
        {
            if (currentStatistics != null && cards != null)
            {
                currentStatistics.cardsUsed += cards.Length;
            }
        }

        /// <summary>
        /// 플레이어 체력 변화 이벤트 처리
        /// </summary>
        private void OnPlayerHealthChanged(int oldHealth, int newHealth)
        {
            if (currentStatistics != null)
            {
                int change = newHealth - oldHealth;
                if (change < 0)
                {
                    currentStatistics.damageTaken += Mathf.Abs(change);
                }
                else if (change > 0)
                {
                    currentStatistics.healingReceived += change;
                }
            }

            // 패배 조건 확인
            CheckDefeatConditions();
        }

        /// <summary>
        /// 플레이어 마나 변화 이벤트 처리
        /// </summary>
        private void OnPlayerManaChanged(int oldMana, int newMana)
        {
            if (currentStatistics != null)
            {
                int change = oldMana - newMana; // 소모량 계산
                if (change > 0)
                {
                    currentStatistics.manaSpent += change;
                }
            }
        }

        /// <summary>
        /// 모든 적 사망 이벤트 처리
        /// </summary>
        private void OnAllEnemiesDead()
        {
            CheckVictoryConditions();
        }

        /// <summary>
        /// 적 사망 이벤트 처리
        /// </summary>
        private void OnEnemyDied(Enemy.Enemy enemy)
        {
            if (currentStatistics != null && enemy != null)
            {
                // 적에게 가한 피해량 추정 (적의 최대 체력)
                currentStatistics.damageDealt += enemy.EnemyData.MaxHealth;
            }
        }

        /// <summary>
        /// 조합 시도 이벤트 처리
        /// </summary>
        private void OnComboAttempted(CardCombination combination, bool success)
        {
            if (currentStatistics != null)
            {
                currentStatistics.combinationsAttempted++;
                if (success)
                {
                    currentStatistics.successfulCombinations++;
                }
            }
        }

        /// <summary>
        /// 조합 성공 이벤트 처리
        /// </summary>
        private void OnComboSuccess(CardCombination combination, CardSO resultCard)
        {
            // OnComboAttempted에서 이미 처리되므로 추가 로직이 필요한 경우에만 사용
            if (debugMode)
            {
                Debug.Log($"[BattleResultManager] 조합 성공: {resultCard?.CardName}");
            }
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 강제 승리 (테스트용)
        /// </summary>
        [ContextMenu("Force Victory")]
        public void ForceVictory()
        {
            if (isBattleActive)
            {
                ProcessBattleResult(BattleResultType.Victory);
            }
        }

        /// <summary>
        /// 강제 패배 (테스트용)
        /// </summary>
        [ContextMenu("Force Defeat")]
        public void ForceDefeat()
        {
            if (isBattleActive)
            {
                ProcessBattleResult(BattleResultType.Defeat);
            }
        }

        /// <summary>
        /// 현재 통계 출력
        /// </summary>
        [ContextMenu("Print Current Statistics")]
        public void PrintCurrentStatistics()
        {
            if (currentStatistics != null)
            {
                Debug.Log($"[BattleResultManager] {currentStatistics.GetDebugString()}");
            }
            else
            {
                Debug.Log("[BattleResultManager] 진행 중인 전투가 없습니다.");
            }
        }
        #endregion
    }
}