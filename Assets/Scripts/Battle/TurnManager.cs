// 레거시 코드

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Maglin.Player;
using Maglin.Enemy;
using Maglin.Cards;

namespace Maglin.Battle
{
    /// <summary>
    /// 턴 기반 전투 시스템을 관리하는 클래스
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static TurnManager _instance;

        public static TurnManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TurnManager>();

                    if (_instance == null)
                    {
                        GameObject turnManagerObject = new GameObject("TurnManager");
                        _instance = turnManagerObject.AddComponent<TurnManager>();
                        DontDestroyOnLoad(turnManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 턴 시작 이벤트 (턴 타입, 턴 번호)
        /// </summary>
        public static event Action<TurnType, int> OnTurnStarted;

        /// <summary>
        /// 턴 종료 이벤트 (턴 타입, 턴 번호)
        /// </summary>
        public static event Action<TurnType, int> OnTurnEnded;

        /// <summary>
        /// 플레이어 턴 시작 이벤트 (턴 번호)
        /// </summary>
        public static event Action<int> OnPlayerTurnStarted;

        /// <summary>
        /// 플레이어 턴 종료 이벤트 (턴 번호)
        /// </summary>
        public static event Action<int> OnPlayerTurnEnded;

        /// <summary>
        /// 몬스터 턴 시작 이벤트 (턴 번호)
        /// </summary>
        public static event Action<int> OnEnemyTurnStarted;

        /// <summary>
        /// 몬스터 턴 종료 이벤트 (턴 번호)
        /// </summary>
        public static event Action<int> OnEnemyTurnEnded;

        /// <summary>
        /// 전투 시작 이벤트
        /// </summary>
        public static event Action OnBattleStarted;

        /// <summary>
        /// 전투 종료 이벤트 (승리 여부)
        /// </summary>
        public static event Action<bool> OnBattleEnded;
        #endregion

        #region Fields
        [Header("턴 설정")]
        [SerializeField] private float turnTransitionDelay = 1f;    // 턴 전환 지연 시간
        [SerializeField] private float playerTurnTimeLimit = 30f;   // 플레이어 턴 제한 시간 (0이면 무제한)
        [SerializeField] private float enemyActionDelay = 0.5f;     // 몬스터 행동 간 지연

        [Header("플레이어 턴 설정")]
        [SerializeField] private int startingHandSize = 5;          // 시작 손패 크기
        [SerializeField] private int drawCardsPerTurn = 2;          // 턴마다 드로우할 카드 수

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 현재 상태
        private TurnType currentTurnType;
        private int currentTurnNumber;
        private bool isBattleActive;
        private bool isProcessingTurn;

        // 컴포넌트 참조
        private PlayerManager playerManager;
        private EnemyManager enemyManager;
        private CardManager cardManager;
        private BattleField battleField;

        // 코루틴 참조
        private Coroutine currentTurnCoroutine;
        private Coroutine playerTurnTimerCoroutine;

        // 플레이어 턴 관련
        private bool isPlayerTurnActive;
        private bool hasPlayerActed;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 턴 타입
        /// </summary>
        public TurnType CurrentTurnType => currentTurnType;

        /// <summary>
        /// 현재 턴 번호
        /// </summary>
        public int CurrentTurnNumber => currentTurnNumber;

        /// <summary>
        /// 전투가 활성화되어 있는지
        /// </summary>
        public bool IsBattleActive => isBattleActive;

        /// <summary>
        /// 턴 처리 중인지
        /// </summary>
        public bool IsProcessingTurn => isProcessingTurn;

        /// <summary>
        /// 플레이어 턴이 활성화되어 있는지
        /// </summary>
        public bool IsPlayerTurnActive => isPlayerTurnActive;

        /// <summary>
        /// 플레이어가 이번 턴에 행동했는지
        /// </summary>
        public bool HasPlayerActed => hasPlayerActed;
        #endregion

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 설정
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

            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartBattle()
        {
            if (isBattleActive)
            {
                Debug.LogWarning("[TurnManager] 이미 전투가 진행 중입니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[TurnManager] 전투 시작");

            isBattleActive = true;
            currentTurnNumber = 0;
            isProcessingTurn = false;

            // 플레이어와 몬스터를 필드에 배치
            SetupBattleField();

            // 시작 손패 드로우
            DrawStartingHand();

            OnBattleStarted?.Invoke();

            // 첫 번째 턴 시작 (플레이어부터)
            StartCoroutine(StartFirstTurn());
        }

        /// <summary>
        /// 전투 종료
        /// </summary>
        public void EndBattle(bool playerWon)
        {
            if (!isBattleActive) return;

            if (debugMode)
                Debug.Log($"[TurnManager] 전투 종료 - {(playerWon ? "승리" : "패배")}");

            isBattleActive = false;
            isProcessingTurn = false;
            isPlayerTurnActive = false;

            // 진행 중인 코루틴들 중지
            if (currentTurnCoroutine != null)
            {
                StopCoroutine(currentTurnCoroutine);
                currentTurnCoroutine = null;
            }

            if (playerTurnTimerCoroutine != null)
            {
                StopCoroutine(playerTurnTimerCoroutine);
                playerTurnTimerCoroutine = null;
            }

            OnBattleEnded?.Invoke(playerWon);
        }

        /// <summary>
        /// 플레이어 턴 종료 (수동)
        /// </summary>
        public void EndPlayerTurn()
        {
            if (!isBattleActive || currentTurnType != TurnType.Player || !isPlayerTurnActive)
            {
                if (debugMode)
                    Debug.LogWarning("[TurnManager] 플레이어 턴을 종료할 수 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[TurnManager] 플레이어가 턴을 종료합니다.");

            StartCoroutine(ProcessPlayerTurnEnd());
        }

        /// <summary>
        /// 카드 사용 (플레이어 턴에서)
        /// </summary>
        public bool UseCard(Card card, List<Maglin.Enemy.Enemy> targets = null)
        {
            if (!isPlayerTurnActive || card == null)
                return false;

            // 마나 확인
            if (playerManager.CurrentMana < card.CurrentManaCost)
            {
                if (debugMode)
                    Debug.LogWarning($"[TurnManager] 마나 부족: 필요 {card.CurrentManaCost}, 현재 {playerManager.CurrentMana}");
                return false;
            }

            // 카드 사용 처리
            if (ProcessCardUsage(card, targets))
            {
                hasPlayerActed = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 강제 턴 전환 (디버그용)
        /// </summary>
        [ContextMenu("Force Next Turn")]
        public void ForceNextTurn()
        {
            if (!isBattleActive) return;

            if (currentTurnType == TurnType.Player)
            {
                EndPlayerTurn();
            }
            else
            {
                // 몬스터 턴은 자동으로 진행되므로 강제 전환하지 않음
                if (debugMode)
                    Debug.Log("[TurnManager] 몬스터 턴은 자동으로 진행됩니다.");
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            // 컴포넌트 참조 획득
            playerManager = PlayerManager.Instance;
            enemyManager = EnemyManager.Instance;
            cardManager = CardManager.Instance;
            battleField = BattleField.Instance;

            // 초기 상태 설정
            currentTurnType = TurnType.Player;
            currentTurnNumber = 0;
            isBattleActive = false;
            isProcessingTurn = false;
            isPlayerTurnActive = false;
            hasPlayerActed = false;

            if (debugMode)
                Debug.Log("[TurnManager] 초기화 완료");
        }

        /// <summary>
        /// 전투 필드 설정
        /// </summary>
        private void SetupBattleField()
        {
            if (battleField == null) return;

            // 필드 초기화
            battleField.ResetField();

            // 플레이어 배치
            if (playerManager != null)
            {
                playerManager.PlaceOnBattleField();
            }

            if (debugMode)
                Debug.Log("[TurnManager] 전투 필드 설정 완료");
        }

        /// <summary>
        /// 시작 손패 드로우
        /// </summary>
        private void DrawStartingHand()
        {
            if (cardManager == null) return;

            cardManager.DrawCards(startingHandSize);

            if (debugMode)
                Debug.Log($"[TurnManager] 시작 손패 {startingHandSize}장 드로우 완료");
        }

        /// <summary>
        /// 첫 번째 턴 시작
        /// </summary>
        private IEnumerator StartFirstTurn()
        {
            yield return new WaitForSeconds(turnTransitionDelay);

            currentTurnNumber = 1;
            currentTurnType = TurnType.Player;

            StartPlayerTurn();
        }

        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        private void StartPlayerTurn()
        {
            if (debugMode)
                Debug.Log($"[TurnManager] 플레이어 턴 {currentTurnNumber} 시작");

            isPlayerTurnActive = true;
            hasPlayerActed = false;

            // 턴 시작 시 카드 드로우
            DrawCardsForTurn();

            // 마나 회복
            playerManager.RecoverManaForTurn();

            // 턴 시작 이벤트
            OnTurnStarted?.Invoke(TurnType.Player, currentTurnNumber);
            OnPlayerTurnStarted?.Invoke(currentTurnNumber);

            // 플레이어 턴 타이머 시작 (제한 시간이 있는 경우)
            if (playerTurnTimeLimit > 0)
            {
                playerTurnTimerCoroutine = StartCoroutine(PlayerTurnTimer());
            }
        }

        /// <summary>
        /// 플레이어 턴 종료 처리
        /// </summary>
        private IEnumerator ProcessPlayerTurnEnd()
        {
            if (debugMode)
                Debug.Log($"[TurnManager] 플레이어 턴 {currentTurnNumber} 종료");

            isPlayerTurnActive = false;

            // 턴 타이머 중지
            if (playerTurnTimerCoroutine != null)
            {
                StopCoroutine(playerTurnTimerCoroutine);
                playerTurnTimerCoroutine = null;
            }

            // 턴 종료 이벤트
            OnTurnEnded?.Invoke(TurnType.Player, currentTurnNumber);
            OnPlayerTurnEnded?.Invoke(currentTurnNumber);

            // 전투 종료 조건 확인
            if (CheckBattleEndConditions())
                yield break;

            yield return new WaitForSeconds(turnTransitionDelay);

            // 몬스터 턴 시작
            currentTurnType = TurnType.Enemy;
            StartEnemyTurn();
        }

        /// <summary>
        /// 몬스터 턴 시작
        /// </summary>
        private void StartEnemyTurn()
        {
            if (debugMode)
                Debug.Log($"[TurnManager] 몬스터 턴 {currentTurnNumber} 시작");

            isProcessingTurn = true;

            // 턴 시작 이벤트
            OnTurnStarted?.Invoke(TurnType.Enemy, currentTurnNumber);
            OnEnemyTurnStarted?.Invoke(currentTurnNumber);

            // 몬스터 턴 시작 알림 (플레이어 위치 전달)
            Vector2Int playerPosition = new Vector2Int(0, 0); // 플레이어는 항상 0번 위치
            enemyManager.StartEnemyTurn(playerPosition);

            // 몬스터 행동 처리
            currentTurnCoroutine = StartCoroutine(ProcessEnemyTurn());
        }

        /// <summary>
        /// 몬스터 턴 처리
        /// </summary>
        private IEnumerator ProcessEnemyTurn()
        {
            var activeEnemies = enemyManager.ActiveEnemies;

            if (debugMode)
                Debug.Log($"[TurnManager] {activeEnemies.Count}마리의 몬스터가 행동합니다.");

            // 왼쪽부터 순서대로 몬스터 행동
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.CurrentState == EnemyState.Dead) continue;

                if (debugMode)
                    Debug.Log($"[TurnManager] 몬스터 {i + 1}/{activeEnemies.Count} 행동: {enemy.EnemyData.EnemyName}");

                // 몬스터 행동 실행
                var enemyAI = enemy.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    Vector2Int playerPosition = new Vector2Int(0, 0); // 플레이어는 항상 0번 위치
                    var activeEnemiesList = new List<Maglin.Enemy.Enemy>(enemyManager.ActiveEnemies);
                    enemyAI.ExecuteTurn(playerPosition, activeEnemiesList);
                    yield return new WaitForSeconds(enemyActionDelay);
                }

                // 전투 종료 조건 확인 (플레이어가 죽었는지)
                if (CheckBattleEndConditions())
                    yield break;
            }

            // 몬스터 턴 종료
            StartCoroutine(ProcessEnemyTurnEnd());
        }

        /// <summary>
        /// 몬스터 턴 종료 처리
        /// </summary>
        private IEnumerator ProcessEnemyTurnEnd()
        {
            if (debugMode)
                Debug.Log($"[TurnManager] 몬스터 턴 {currentTurnNumber} 종료");

            isProcessingTurn = false;

            // 몬스터 턴 종료 알림
            enemyManager.EndEnemyTurn();

            // 턴 종료 이벤트
            OnTurnEnded?.Invoke(TurnType.Enemy, currentTurnNumber);
            OnEnemyTurnEnded?.Invoke(currentTurnNumber);

            // 전투 종료 조건 확인
            if (CheckBattleEndConditions())
                yield break;

            yield return new WaitForSeconds(turnTransitionDelay);

            // 다음 플레이어 턴 시작
            currentTurnNumber++;
            currentTurnType = TurnType.Player;
            StartPlayerTurn();
        }

        /// <summary>
        /// 턴마다 카드 드로우
        /// </summary>
        private void DrawCardsForTurn()
        {
            if (cardManager == null) return;

            cardManager.DrawCards(drawCardsPerTurn);

            if (debugMode)
                Debug.Log($"[TurnManager] 턴 시작 시 {drawCardsPerTurn}장 드로우");
        }

        /// <summary>
        /// 카드 사용 처리
        /// </summary>
        private bool ProcessCardUsage(Card card, List<Maglin.Enemy.Enemy> targets)
        {
            try
            {
                // 마나 소모
                playerManager.SpendMana(card.CurrentManaCost);

                // 카드 효과 적용
                ApplyCardEffect(card, targets);

                // 카드 사용 이벤트 발생 (정적 이벤트가 아닌 인스턴스 이벤트 사용)
                if (debugMode)
                    Debug.Log($"[TurnManager] 카드 사용 이벤트 발생: {card.CardData.CardName}");

                if (debugMode)
                    Debug.Log($"[TurnManager] 카드 사용: {card.CardData.CardName}");

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TurnManager] 카드 사용 중 오류: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 카드 효과 적용
        /// </summary>
        private void ApplyCardEffect(Card card, List<Maglin.Enemy.Enemy> targets)
        {
            // 타겟 타입에 따른 효과 적용
            switch (card.CardData.Target)
            {
                case TargetType.SingleEnemy:
                    if (targets != null && targets.Count > 0)
                    {
                        ApplyDamageToEnemy(targets[0], card);
                    }
                    break;

                case TargetType.AllEnemies:
                    var allEnemies = enemyManager.ActiveEnemies;
                    foreach (var enemy in allEnemies)
                    {
                        ApplyDamageToEnemy(enemy, card);
                    }
                    break;

                case TargetType.Self:
                    ApplyEffectToPlayer(card);
                    break;

                default:
                    if (debugMode)
                        Debug.LogWarning($"[TurnManager] 처리되지 않은 타겟 타입: {card.CardData.Target}");
                    break;
            }
        }

        /// <summary>
        /// 몬스터에게 데미지 적용
        /// </summary>
        private void ApplyDamageToEnemy(Maglin.Enemy.Enemy enemy, Card card)
        {
            if (enemy == null || enemy.CurrentState == EnemyState.Dead) return;

            int damage = card.CurrentDamage;

            // 속성 상성 및 필드 보너스 적용
            if (battleField != null)
            {
                damage = card.CalculateDamageAgainst(enemy.EnemyData.Element);
            }

            enemy.TakeDamage(damage);

            if (debugMode)
                Debug.Log($"[TurnManager] {enemy.EnemyData.EnemyName}에게 {damage} 데미지");
        }

        /// <summary>
        /// 플레이어에게 효과 적용
        /// </summary>
        private void ApplyEffectToPlayer(Card card)
        {
            // 힐링 카드 등의 효과 처리
            if (card.CardData.BaseDamage > 0)
            {
                playerManager.Heal(card.CurrentDamage);

                if (debugMode)
                    Debug.Log($"[TurnManager] 플레이어 {card.CurrentDamage} 힐링");
            }
        }

        /// <summary>
        /// 플레이어 턴 타이머
        /// </summary>
        private IEnumerator PlayerTurnTimer()
        {
            yield return new WaitForSeconds(playerTurnTimeLimit);

            if (isPlayerTurnActive)
            {
                if (debugMode)
                    Debug.Log("[TurnManager] 플레이어 턴 시간 초과");

                EndPlayerTurn();
            }
        }

        /// <summary>
        /// 전투 종료 조건 확인
        /// </summary>
        private bool CheckBattleEndConditions()
        {
            // 플레이어 죽음 확인
            if (playerManager.CurrentHealth <= 0)
            {
                EndBattle(false); // 패배
                return true;
            }

            // 모든 몬스터 처치 확인
            if (enemyManager.AllEnemiesDead)
            {
                EndBattle(true); // 승리
                return true;
            }

            return false;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (enemyManager != null)
            {
                EnemyManager.OnAllEnemiesDead += OnAllEnemiesDead;
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (enemyManager != null)
            {
                EnemyManager.OnAllEnemiesDead -= OnAllEnemiesDead;
            }
        }

        /// <summary>
        /// 모든 몬스터 처치 이벤트 처리
        /// </summary>
        private void OnAllEnemiesDead()
        {
            if (isBattleActive)
            {
                if (debugMode)
                    Debug.Log("[TurnManager] 모든 몬스터가 처치되었습니다!");

                EndBattle(true);
            }
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 턴 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug Turn Status")]
        public void DebugTurnStatus()
        {
            Debug.Log("=== TurnManager 상태 ===");
            Debug.Log($"전투 활성화: {isBattleActive}");
            Debug.Log($"현재 턴: {currentTurnType} 턴 {currentTurnNumber}");
            Debug.Log($"턴 처리 중: {isProcessingTurn}");
            Debug.Log($"플레이어 턴 활성화: {isPlayerTurnActive}");
            Debug.Log($"플레이어 행동 여부: {hasPlayerActed}");

            if (playerManager != null)
            {
                Debug.Log($"플레이어 체력: {playerManager.CurrentHealth}/{playerManager.MaxHealth}");
                Debug.Log($"플레이어 마나: {playerManager.CurrentMana}/{playerManager.MaxMana}");
            }

            if (enemyManager != null)
            {
                Debug.Log($"활성 몬스터 수: {enemyManager.ActiveEnemies.Count}");
            }
        }
        #endregion
    }
}