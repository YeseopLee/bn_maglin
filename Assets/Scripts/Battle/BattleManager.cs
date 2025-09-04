using UnityEngine;
using System;
using System.Collections.Generic;
using Maglin.Core;
using Maglin.Player;
using Maglin.Enemy;
using Maglin.Cards;

namespace Maglin.Battle
{
    /// <summary>
    /// 턴 타입
    /// </summary>
    public enum TurnType
    {
        Player,     // 플레이어 턴
        Enemy       // 적 턴
    }

    /// <summary>
    /// 전투 상태
    /// </summary>
    public enum BattlePhase
    {
        None,           // 전투 중이 아님
        Starting,       // 전투 시작 중
        PlayerTurn,     // 플레이어 턴
        EnemyTurn,      // 적 턴
        BattleEnd,      // 전투 종료 중
        Victory,        // 승리
        Defeat          // 패배
    }

    /// <summary>
    /// 전투 시스템을 총괄하는 매니저
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static BattleManager _instance;

        public static BattleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BattleManager>();

                    if (_instance == null)
                    {
                        GameObject battleManagerObject = new GameObject("BattleManager");
                        _instance = battleManagerObject.AddComponent<BattleManager>();
                        DontDestroyOnLoad(battleManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 전투 시작 이벤트
        /// </summary>
        public static event Action<BattleSO> OnBattleStarted;

        /// <summary>
        /// 전투 종료 이벤트 (승리 여부)
        /// </summary>
        public static event Action<bool> OnBattleEnded;

        /// <summary>
        /// 턴 변경 이벤트 (턴 타입, 턴 번호)
        /// </summary>
        public static event Action<TurnType, int> OnTurnChanged;

        /// <summary>
        /// 전투 페이즈 변경 이벤트
        /// </summary>
        public static event Action<BattlePhase> OnPhaseChanged;

        /// <summary>
        /// 적 처치 이벤트
        /// </summary>
        public static event Action<EnemyController> OnEnemyDefeated;

        /// <summary>
        /// 카드 사용 이벤트
        /// </summary>
        public static event Action<CardSO[]> OnCardsUsed;
        #endregion

        #region Fields
        [Header("전투 설정")]
        [SerializeField] private float turnTransitionDelay = 1f;
        [SerializeField] private float battleStartDelay = 2f;
        [SerializeField] private float battleEndDelay = 3f;

        [Header("현재 전투 상태")]
        [SerializeField] private BattlePhase currentPhase = BattlePhase.None;
        [SerializeField] private TurnType currentTurn = TurnType.Player;
        [SerializeField] private int turnNumber = 1;
        [SerializeField] private bool isProcessingTurn = false;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 현재 전투 정보
        private BattleSO currentBattle;
        private List<EnemyController> activeEnemies = new List<EnemyController>();
        private FieldEffectSO currentFieldEffect;

        // 전투 필드 관련
        private Transform battleField;
        private readonly int fieldSize = 10; // PRD에 따른 10칸 구조

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 전투 페이즈
        /// </summary>
        public BattlePhase CurrentPhase => currentPhase;

        /// <summary>
        /// 현재 턴 타입
        /// </summary>
        public TurnType CurrentTurn => currentTurn;

        /// <summary>
        /// 현재 턴 번호
        /// </summary>
        public int TurnNumber => turnNumber;

        /// <summary>
        /// 현재 전투 데이터
        /// </summary>
        public BattleSO CurrentBattle => currentBattle;

        /// <summary>
        /// 활성 적 목록
        /// </summary>
        public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies.AsReadOnly();

        /// <summary>
        /// 현재 필드 효과
        /// </summary>
        public FieldEffectSO CurrentFieldEffect => currentFieldEffect;

        /// <summary>
        /// 전투 중인지 여부
        /// </summary>
        public bool IsInBattle => currentPhase != BattlePhase.None && currentPhase != BattlePhase.BattleEnd;

        /// <summary>
        /// 턴 처리 중인지 여부
        /// </summary>
        public bool IsProcessingTurn => isProcessingTurn;

        /// <summary>
        /// 필드 크기
        /// </summary>
        public int FieldSize => fieldSize;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeBattleManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
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
        /// BattleManager 초기화
        /// </summary>
        private void InitializeBattleManager()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 전투 매니저 초기화 시작");

            // 기본 상태 설정
            currentPhase = BattlePhase.None;
            currentTurn = TurnType.Player;
            turnNumber = 1;
            isProcessingTurn = false;

            // 이벤트 구독
            if (FloorManager.Instance != null)
            {
                FloorManager.OnGameStateChanged += OnGameStateChanged;
            }

            // 플레이어 이벤트 구독
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerDeath += OnPlayerDeath;
            }

            isInitialized = true;

            if (debugMode)
                Debug.Log("[BattleManager] 전투 매니저 초기화 완료");
        }

        /// <summary>
        /// 게임 상태 변경 시 호출
        /// </summary>
        private void OnGameStateChanged(GameState previousState, GameState newState)
        {
            if (newState == GameState.Battle && previousState != GameState.Battle)
            {
                // 전투 상태로 진입 시 전투 준비
                PrepareBattle();
            }
            else if (previousState == GameState.Battle && newState != GameState.Battle)
            {
                // 전투 상태에서 벗어날 시 전투 정리
                CleanupBattle();
            }
        }

        /// <summary>
        /// 플레이어 사망 시 호출
        /// </summary>
        private void OnPlayerDeath()
        {
            if (IsInBattle)
            {
                EndBattle(false); // 패배로 전투 종료
            }
        }
        #endregion

        #region Battle Control
        /// <summary>
        /// 전투 준비
        /// </summary>
        private void PrepareBattle()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 전투 준비 시작");

            // TODO: 현재 층에 맞는 전투 데이터 로드
            // 현재는 임시로 null 처리
            currentBattle = null;

            if (currentBattle != null)
            {
                StartBattle(currentBattle);
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[BattleManager] 전투 데이터가 없습니다. 테스트 전투를 시작합니다.");

                // 테스트용 빈 전투 시작
                StartTestBattle();
            }
        }

        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartBattle(BattleSO battleData)
        {
            if (battleData == null)
            {
                Debug.LogError("[BattleManager] 전투 데이터가 null입니다.");
                return;
            }

            if (IsInBattle)
            {
                Debug.LogWarning("[BattleManager] 이미 전투 중입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleManager] 전투 시작: {battleData.BattleName}");

            currentBattle = battleData;
            ChangePhase(BattlePhase.Starting);

            // 전투 시작 코루틴 실행
            StartCoroutine(StartBattleSequence());
        }

        /// <summary>
        /// 테스트 전투 시작 (전투 데이터가 없을 때)
        /// </summary>
        private void StartTestBattle()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 테스트 전투 시작");

            ChangePhase(BattlePhase.Starting);
            StartCoroutine(StartTestBattleSequence());
        }

        /// <summary>
        /// 전투 시작 시퀀스
        /// </summary>
        private System.Collections.IEnumerator StartBattleSequence()
        {
            // 시작 딜레이
            yield return new WaitForSeconds(battleStartDelay);

            // 전투 필드 초기화
            InitializeBattleField();

            // 적 배치
            SpawnEnemies();

            // 필드 효과 적용
            ApplyInitialFieldEffect();

            // 전투 시작 이벤트 발생
            OnBattleStarted?.Invoke(currentBattle);

            // 플레이어 턴으로 시작
            StartPlayerTurn();
        }

        /// <summary>
        /// 테스트 전투 시작 시퀀스
        /// </summary>
        private System.Collections.IEnumerator StartTestBattleSequence()
        {
            yield return new WaitForSeconds(battleStartDelay);

            // 기본 전투 필드 초기화
            InitializeBattleField();

            // 테스트용 적 없이 시작
            activeEnemies.Clear();

            // 전투 시작 이벤트 발생 (null 데이터로)
            OnBattleStarted?.Invoke(null);

            // 플레이어 턴으로 시작
            StartPlayerTurn();
        }

        /// <summary>
        /// 전투 종료
        /// </summary>
        public void EndBattle(bool victory)
        {
            if (!IsInBattle)
            {
                Debug.LogWarning("[BattleManager] 전투 중이 아닙니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleManager] 전투 종료: {(victory ? "승리" : "패배")}");

            ChangePhase(victory ? BattlePhase.Victory : BattlePhase.Defeat);

            // 전투 종료 코루틴 실행
            StartCoroutine(EndBattleSequence(victory));
        }

        /// <summary>
        /// 전투 종료 시퀀스
        /// </summary>
        private System.Collections.IEnumerator EndBattleSequence(bool victory)
        {
            // 전투 종료 이벤트 발생
            OnBattleEnded?.Invoke(victory);

            // 종료 딜레이
            yield return new WaitForSeconds(battleEndDelay);

            // 전투 정리
            CleanupBattle();

            // 페이즈 변경
            ChangePhase(BattlePhase.BattleEnd);

            if (victory)
            {
                // 승리 시 FloorManager에게 층 완료 알림
                if (FloorManager.Instance != null)
                {
                    FloorManager.Instance.OnContentCompleted();
                }
            }
            else
            {
                // 패배 시 FloorManager에게 게임 리셋 요청
                if (FloorManager.Instance != null)
                {
                    FloorManager.Instance.ResetToFloor(1); // 1층으로 리셋
                }
                // 패배 시 게임 오버 (PlayerManager에서 이미 처리됨)
            }

            // 최종 정리
            ChangePhase(BattlePhase.None);
        }

        /// <summary>
        /// 전투 정리
        /// </summary>
        private void CleanupBattle()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 전투 정리");

            // 적 정리
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            activeEnemies.Clear();

            // 필드 효과 정리
            currentFieldEffect = null;

            // 전투 데이터 정리
            currentBattle = null;

            // 턴 정보 초기화
            turnNumber = 1;
            currentTurn = TurnType.Player;
            isProcessingTurn = false;
        }
        #endregion

        #region Turn Management
        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        public void StartPlayerTurn()
        {
            if (isProcessingTurn)
            {
                Debug.LogWarning("[BattleManager] 이미 턴을 처리 중입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleManager] 플레이어 턴 시작 (턴 {turnNumber})");

            currentTurn = TurnType.Player;
            ChangePhase(BattlePhase.PlayerTurn);

            // 플레이어 마나 회복
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RecoverManaForTurn();
            }

            // 카드 드로우 (CardManager 구현됨)
            if (CardManager.Instance != null)
            {
                // 손패와 임시무덤을 덱으로 복귀 후 셔플
                CardManager.Instance.EndTurnProcessing();

                // 최대 손패까지 드로우
                var drawnCards = CardManager.Instance.DrawCardsToMax();

                if (debugMode)
                    Debug.Log($"[BattleManager] {drawnCards.Count}장 드로우 완료");
            }

            // 턴 변경 이벤트 발생
            OnTurnChanged?.Invoke(currentTurn, turnNumber);
        }

        /// <summary>
        /// 플레이어 턴 종료
        /// </summary>
        public void EndPlayerTurn()
        {
            if (currentTurn != TurnType.Player || isProcessingTurn)
            {
                Debug.LogWarning("[BattleManager] 플레이어 턴이 아니거나 이미 처리 중입니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[BattleManager] 플레이어 턴 종료");

            StartCoroutine(ProcessTurnTransition(TurnType.Enemy));
        }

        /// <summary>
        /// 적 턴 시작
        /// </summary>
        private void StartEnemyTurn()
        {
            if (debugMode)
                Debug.Log($"[BattleManager] 적 턴 시작 (턴 {turnNumber})");

            currentTurn = TurnType.Enemy;
            ChangePhase(BattlePhase.EnemyTurn);

            // 턴 변경 이벤트 발생
            OnTurnChanged?.Invoke(currentTurn, turnNumber);

            // 적 행동 처리 시작
            StartCoroutine(ProcessEnemyActions());
        }

        /// <summary>
        /// 적 턴 종료
        /// </summary>
        private void EndEnemyTurn()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 적 턴 종료");

            // 턴 번호 증가
            turnNumber++;

            StartCoroutine(ProcessTurnTransition(TurnType.Player));
        }

        /// <summary>
        /// 턴 전환 처리
        /// </summary>
        private System.Collections.IEnumerator ProcessTurnTransition(TurnType nextTurn)
        {
            isProcessingTurn = true;

            // 전환 딜레이
            yield return new WaitForSeconds(turnTransitionDelay);

            // 승부 판정
            if (CheckBattleEndConditions())
            {
                isProcessingTurn = false;
                yield break;
            }

            // 다음 턴 시작
            if (nextTurn == TurnType.Player)
            {
                StartPlayerTurn();
            }
            else
            {
                StartEnemyTurn();
            }

            isProcessingTurn = false;
        }

        /// <summary>
        /// 적 행동 처리
        /// </summary>
        private System.Collections.IEnumerator ProcessEnemyActions()
        {
            // EnemyManager를 통해 적 턴 실행
            if (EnemyManager.Instance != null)
            {
                // 플레이어 위치 가져오기
                Vector2Int playerPosition = Vector2Int.zero;
                if (PlayerManager.Instance != null)
                {
                    // PlayerManager에 위치 정보가 있으면 사용, 없으면 기본값
                    playerPosition = new Vector2Int(0, 0);
                }

                // EnemyManager에서 몬스터 턴 처리
                EnemyManager.Instance.StartEnemyTurn(playerPosition);

                // EnemyManager의 턴이 끝날 때까지 대기
                while (EnemyManager.Instance.IsEnemyTurn)
                {
                    yield return null;
                }
            }
            else
            {
                // EnemyManager가 없는 경우 기존 로직 사용
                var sortedEnemies = new List<EnemyController>(activeEnemies);
                sortedEnemies.Sort((a, b) => a.Position.CompareTo(b.Position));

                foreach (var enemy in sortedEnemies)
                {
                    if (enemy != null && enemy.IsAlive)
                    {
                        // 적 이동
                        yield return StartCoroutine(enemy.ProcessMovement());

                        // 적 공격
                        yield return StartCoroutine(enemy.ProcessAttack());

                        // 각 적 행동 간 딜레이
                        yield return new WaitForSeconds(0.5f);
                    }
                }
            }

            // 적 턴 종료
            EndEnemyTurn();
        }
        #endregion

        #region Battle Field Management
        /// <summary>
        /// 전투 필드 초기화
        /// </summary>
        private void InitializeBattleField()
        {
            if (debugMode)
                Debug.Log("[BattleManager] 전투 필드 초기화");

            // 전투 필드 오브젝트 생성 또는 찾기
            if (battleField == null)
            {
                var battleFieldObject = GameObject.Find("BattleField");
                if (battleFieldObject == null)
                {
                    battleFieldObject = new GameObject("BattleField");
                }
                battleField = battleFieldObject.transform;
            }

            // TODO: 실제 필드 UI 및 그리드 시스템 구현
        }

        /// <summary>
        /// 적 스폰
        /// </summary>
        private void SpawnEnemies()
        {
            if (currentBattle == null || currentBattle.EnemyPlacements == null)
            {
                if (debugMode)
                    Debug.Log("[BattleManager] 적 배치 정보가 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleManager] {currentBattle.EnemyPlacements.Length}마리 적 스폰");

            foreach (var placement in currentBattle.EnemyPlacements)
            {
                if (placement.enemyData != null)
                {
                    SpawnEnemy(placement);
                }
            }
        }

        /// <summary>
        /// 개별 적 스폰
        /// </summary>
        private void SpawnEnemy(EnemyPlacement placement)
        {
            // TODO: 실제 EnemyController 프리팹 생성 및 배치
            // 현재는 기본 구조만 구현

            var enemyObject = new GameObject($"Enemy_{placement.enemyData.EnemyName}");
            if (battleField != null)
            {
                enemyObject.transform.SetParent(battleField);
            }

            var enemyController = enemyObject.AddComponent<EnemyController>();
            // TODO: enemyController.Initialize(placement);

            activeEnemies.Add(enemyController);

            if (debugMode)
                Debug.Log($"[BattleManager] 적 스폰: {placement.enemyData.EnemyName}");
        }

        /// <summary>
        /// 초기 필드 효과 적용
        /// </summary>
        private void ApplyInitialFieldEffect()
        {
            if (currentBattle != null && currentBattle.InitialFieldEffect != null)
            {
                SetFieldEffect(currentBattle.InitialFieldEffect);
            }
        }

        /// <summary>
        /// 필드 효과 설정
        /// </summary>
        public void SetFieldEffect(FieldEffectSO fieldEffect)
        {
            currentFieldEffect = fieldEffect;

            if (debugMode)
            {
                if (fieldEffect != null)
                    Debug.Log($"[BattleManager] 필드 효과 적용: {fieldEffect.EffectName}");
                else
                    Debug.Log("[BattleManager] 필드 효과 제거");
            }

            // TODO: 필드 효과 시각적 업데이트
        }
        #endregion

        #region Card Usage
        /// <summary>
        /// 카드 사용 처리
        /// </summary>
        public void UseCards(CardSO[] cards)
        {
            if (cards == null || cards.Length == 0)
            {
                Debug.LogWarning("[BattleManager] 사용할 카드가 없습니다.");
                return;
            }

            if (currentTurn != TurnType.Player)
            {
                Debug.LogWarning("[BattleManager] 플레이어 턴이 아닙니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[BattleManager] 카드 사용: {cards.Length}장");

            // 카드 사용 이벤트 발생
            OnCardsUsed?.Invoke(cards);

            // TODO: 실제 카드 효과 처리 (CardManager 구현 후)
        }
        #endregion

        #region Battle Conditions
        /// <summary>
        /// 전투 종료 조건 확인
        /// </summary>
        private bool CheckBattleEndConditions()
        {
            // BattleResultManager가 있으면 해당 시스템에서 처리
            if (BattleResultManager.Instance != null)
            {
                // 패배 조건 확인
                if (BattleResultManager.Instance.CheckDefeatConditions())
                {
                    return true;
                }

                // 승리 조건 확인
                if (BattleResultManager.Instance.CheckVictoryConditions())
                {
                    return true;
                }

                return false;
            }

            // 기존 방식 (Fallback)
            // 플레이어 사망 체크
            if (PlayerManager.Instance != null && !PlayerManager.Instance.IsAlive)
            {
                EndBattle(false);
                return true;
            }

            // 모든 적 처치 체크
            bool allEnemiesDefeated = true;
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    allEnemiesDefeated = false;
                    break;
                }
            }

            if (allEnemiesDefeated)
            {
                EndBattle(true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 적 처치 처리
        /// </summary>
        public void OnEnemyKilled(EnemyController enemy)
        {
            if (enemy == null) return;

            if (debugMode)
                Debug.Log($"[BattleManager] 적 처치: {enemy.name}");

            OnEnemyDefeated?.Invoke(enemy);

            // 다음 프레임에 승부 판정 (현재 처리 중인 적 행동 완료 후)
            StartCoroutine(DelayedBattleEndCheck());
        }

        /// <summary>
        /// 지연된 전투 종료 체크
        /// </summary>
        private System.Collections.IEnumerator DelayedBattleEndCheck()
        {
            yield return null; // 한 프레임 대기
            CheckBattleEndConditions();
        }
        #endregion

        #region Phase Management
        /// <summary>
        /// 전투 페이즈 변경
        /// </summary>
        private void ChangePhase(BattlePhase newPhase)
        {
            if (currentPhase == newPhase) return;

            var previousPhase = currentPhase;
            currentPhase = newPhase;

            OnPhaseChanged?.Invoke(currentPhase);

            if (debugMode)
                Debug.Log($"[BattleManager] 페이즈 변경: {previousPhase} -> {currentPhase}");
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== BattleManager Debug Info ===");
            Debug.Log($"Phase: {currentPhase}");
            Debug.Log($"Turn: {currentTurn} (#{turnNumber})");
            Debug.Log($"Processing Turn: {isProcessingTurn}");
            Debug.Log($"Active Enemies: {activeEnemies.Count}");
            Debug.Log($"Field Effect: {(currentFieldEffect != null ? currentFieldEffect.EffectName : "None")}");
            Debug.Log($"Battle Data: {(currentBattle != null ? currentBattle.BattleName : "None")}");
        }

        /// <summary>
        /// 강제 전투 종료 (디버그용)
        /// </summary>
        [ContextMenu("Force End Battle")]
        public void ForceEndBattle()
        {
            if (!debugMode)
            {
                Debug.LogWarning("[BattleManager] ForceEndBattle은 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            EndBattle(true);
        }
        #endregion
    }

    /// <summary>
    /// EnemyController 임시 클래스 (실제 구현은 나중에)
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        public int Position { get; set; } = 10; // 기본적으로 오른쪽 끝에서 시작
        public bool IsAlive { get; set; } = true;

        public System.Collections.IEnumerator ProcessMovement()
        {
            // TODO: 실제 이동 로직 구현
            yield return new WaitForSeconds(0.5f);
        }

        public System.Collections.IEnumerator ProcessAttack()
        {
            // TODO: 실제 공격 로직 구현
            yield return new WaitForSeconds(0.5f);
        }
    }
}