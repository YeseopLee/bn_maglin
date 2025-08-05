using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Maglin.Cards;
using Maglin.Battle;

namespace Maglin.Enemy
{
    /// <summary>
    /// 전투에서 모든 몬스터를 관리하는 매니저
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static EnemyManager _instance;

        public static EnemyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EnemyManager>();

                    if (_instance == null)
                    {
                        GameObject enemyManagerObject = new GameObject("EnemyManager");
                        _instance = enemyManagerObject.AddComponent<EnemyManager>();
                        DontDestroyOnLoad(enemyManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 생성 이벤트
        /// </summary>
        public static event Action<Enemy> OnEnemySpawned;

        /// <summary>
        /// 몬스터 사망 이벤트
        /// </summary>
        public static event Action<Enemy> OnEnemyDied;

        /// <summary>
        /// 모든 몬스터 사망 이벤트
        /// </summary>
        public static event Action OnAllEnemiesDead;

        /// <summary>
        /// 몬스터 턴 시작 이벤트
        /// </summary>
        public static event Action OnEnemyTurnStarted;

        /// <summary>
        /// 몬스터 턴 종료 이벤트
        /// </summary>
        public static event Action OnEnemyTurnEnded;
        #endregion

        #region Fields
        [Header("몬스터 관리")]
        [SerializeField] private GameObject enemyPrefab;  // 기본 몬스터 프리팹
        [SerializeField] private Transform enemyContainer;  // 몬스터들의 부모 오브젝트
        [SerializeField] private int maxEnemiesPerBattle = 5;

        [Header("스폰 설정")]
        [SerializeField] private Vector2Int[] spawnPositions;  // 가능한 스폰 위치들
        [SerializeField] private float spawnDelay = 0.5f;  // 스폰 간격

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 몬스터 리스트
        private List<Enemy> activeEnemies = new List<Enemy>();
        private List<Enemy> deadEnemies = new List<Enemy>();

        // 턴 관리
        private bool isEnemyTurn = false;
        private int currentEnemyIndex = 0;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 활성화된 몬스터 리스트 (읽기 전용)
        /// </summary>
        public IReadOnlyList<Enemy> ActiveEnemies => activeEnemies.AsReadOnly();

        /// <summary>
        /// 사망한 몬스터 리스트 (읽기 전용)
        /// </summary>
        public IReadOnlyList<Enemy> DeadEnemies => deadEnemies.AsReadOnly();

        /// <summary>
        /// 총 몬스터 수 (활성 + 사망)
        /// </summary>
        public int TotalEnemyCount => activeEnemies.Count + deadEnemies.Count;

        /// <summary>
        /// 살아있는 몬스터 수
        /// </summary>
        public int AliveEnemyCount => activeEnemies.Count(e => e.IsAlive);

        /// <summary>
        /// 모든 몬스터가 사망했는지 여부
        /// </summary>
        public bool AllEnemiesDead => AliveEnemyCount == 0;

        /// <summary>
        /// 현재 몬스터 턴인지 여부
        /// </summary>
        public bool IsEnemyTurn => isEnemyTurn;
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

        private void Start()
        {
            if (enemyContainer == null)
            {
                GameObject container = new GameObject("EnemyContainer");
                container.transform.SetParent(transform);
                enemyContainer = container.transform;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 몬스터 생성
        /// </summary>
        public Enemy SpawnEnemy(EnemySO enemyData, Vector2Int position)
        {
            if (enemyData == null)
            {
                Debug.LogError("[EnemyManager] EnemySO 데이터가 null입니다.");
                return null;
            }

            if (activeEnemies.Count >= maxEnemiesPerBattle)
            {
                Debug.LogWarning("[EnemyManager] 최대 몬스터 수에 도달했습니다.");
                return null;
            }

            // 위치가 점유되어 있는지 확인
            if (IsPositionOccupied(position))
            {
                Debug.LogWarning($"[EnemyManager] 위치 {position}이 이미 점유되어 있습니다.");
                return null;
            }

            // 몬스터 생성
            GameObject enemyObject = CreateEnemyObject(enemyData, position);
            if (enemyObject == null) return null;

            Enemy enemy = enemyObject.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError("[EnemyManager] Enemy 컴포넌트가 없습니다.");
                Destroy(enemyObject);
                return null;
            }

            // 몬스터 초기화
            enemy.Initialize(enemyData, position);

            // 이벤트 구독
            enemy.OnDeath += OnEnemyDeathHandler;

            // 리스트에 추가
            activeEnemies.Add(enemy);

            // BattleField에 몬스터 배치
            if (BattleField.Instance != null)
            {
                int positionIndex = position.x; // Vector2Int.x를 위치 인덱스로 사용
                BattleField.Instance.PlaceObjectAtPosition(enemyObject, positionIndex, PositionObjectType.Enemy);
            }

            if (debugMode)
                Debug.Log($"[EnemyManager] {enemyData.EnemyName} 생성 완료 - 위치: {position}");

            OnEnemySpawned?.Invoke(enemy);
            return enemy;
        }

        /// <summary>
        /// 여러 몬스터 생성
        /// </summary>
        public List<Enemy> SpawnEnemies(List<EnemySO> enemyDataList, List<Vector2Int> positions = null)
        {
            if (enemyDataList == null || enemyDataList.Count == 0)
            {
                Debug.LogWarning("[EnemyManager] 생성할 몬스터 데이터가 없습니다.");
                return new List<Enemy>();
            }

            List<Enemy> spawnedEnemies = new List<Enemy>();

            for (int i = 0; i < enemyDataList.Count; i++)
            {
                Vector2Int spawnPos;

                if (positions != null && i < positions.Count)
                {
                    spawnPos = positions[i];
                }
                else
                {
                    spawnPos = GetRandomSpawnPosition();
                    if (spawnPos == Vector2Int.one * -1) // 유효하지 않은 위치
                    {
                        Debug.LogWarning($"[EnemyManager] {enemyDataList[i].EnemyName}의 스폰 위치를 찾을 수 없습니다.");
                        continue;
                    }
                }

                Enemy spawnedEnemy = SpawnEnemy(enemyDataList[i], spawnPos);
                if (spawnedEnemy != null)
                {
                    spawnedEnemies.Add(spawnedEnemy);
                }
            }

            return spawnedEnemies;
        }

        /// <summary>
        /// 몬스터 제거
        /// </summary>
        public void RemoveEnemy(Enemy enemy)
        {
            if (enemy == null) return;

            // 이벤트 구독 해제
            enemy.OnDeath -= OnEnemyDeathHandler;

            // 리스트에서 제거
            activeEnemies.Remove(enemy);

            if (debugMode)
                Debug.Log($"[EnemyManager] {enemy.EnemyName} 제거");

            Destroy(enemy.gameObject);
        }

        /// <summary>
        /// 모든 몬스터 제거
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in activeEnemies.ToList())
            {
                RemoveEnemy(enemy);
            }

            deadEnemies.Clear();

            if (debugMode)
                Debug.Log("[EnemyManager] 모든 몬스터 제거 완료");
        }

        /// <summary>
        /// 몬스터 턴 시작
        /// </summary>
        public void StartEnemyTurn(Vector2Int playerPosition)
        {
            if (isEnemyTurn)
            {
                Debug.LogWarning("[EnemyManager] 이미 몬스터 턴이 진행 중입니다.");
                return;
            }

            isEnemyTurn = true;
            currentEnemyIndex = 0;

            if (debugMode)
                Debug.Log($"[EnemyManager] 몬스터 턴 시작 - 활성 몬스터: {AliveEnemyCount}마리");

            OnEnemyTurnStarted?.Invoke();

            // 첫 번째 몬스터부터 턴 실행
            StartCoroutine(ExecuteEnemyTurns(playerPosition));
        }

        /// <summary>
        /// 몬스터 턴 강제 종료
        /// </summary>
        public void EndEnemyTurn()
        {
            if (!isEnemyTurn) return;

            StopAllCoroutines();
            isEnemyTurn = false;

            // 모든 몬스터 턴 종료 처리
            foreach (var enemy in activeEnemies.Where(e => e.IsAlive))
            {
                enemy.OnTurnEnd();
            }

            if (debugMode)
                Debug.Log("[EnemyManager] 몬스터 턴 종료");

            OnEnemyTurnEnded?.Invoke();
        }

        /// <summary>
        /// 특정 위치의 몬스터 찾기
        /// </summary>
        public Enemy GetEnemyAtPosition(Vector2Int position)
        {
            return activeEnemies.FirstOrDefault(e => e.IsAlive && e.GridPosition == position);
        }

        /// <summary>
        /// 플레이어를 공격할 수 있는 몬스터들 찾기
        /// </summary>
        public List<Enemy> GetEnemiesInAttackRange(Vector2Int playerPosition)
        {
            return activeEnemies.Where(e => e.IsAlive && e.CanAttack(playerPosition)).ToList();
        }

        /// <summary>
        /// 위치가 점유되어 있는지 확인
        /// </summary>
        public bool IsPositionOccupied(Vector2Int position)
        {
            return activeEnemies.Any(e => e.IsAlive && e.GridPosition == position);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            if (isInitialized) return;

            activeEnemies.Clear();
            deadEnemies.Clear();
            isEnemyTurn = false;
            currentEnemyIndex = 0;

            // 기본 스폰 위치 설정 (10x4 그리드의 오른쪽 절반)
            if (spawnPositions == null || spawnPositions.Length == 0)
            {
                SetupDefaultSpawnPositions();
            }

            isInitialized = true;

            if (debugMode)
                Debug.Log("[EnemyManager] 초기화 완료");
        }

        /// <summary>
        /// 기본 스폰 위치 설정
        /// </summary>
        private void SetupDefaultSpawnPositions()
        {
            List<Vector2Int> positions = new List<Vector2Int>();

            // 그리드 오른쪽 절반(x: 5-9)에 스폰 위치 설정
            for (int x = 5; x < 10; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    positions.Add(new Vector2Int(x, y));
                }
            }

            spawnPositions = positions.ToArray();
        }

        /// <summary>
        /// 몬스터 오브젝트 생성
        /// </summary>
        private GameObject CreateEnemyObject(EnemySO enemyData, Vector2Int position)
        {
            GameObject enemyObject;

            if (enemyPrefab != null)
            {
                enemyObject = Instantiate(enemyPrefab, enemyContainer);
            }
            else
            {
                // 기본 몬스터 오브젝트 생성
                enemyObject = new GameObject($"Enemy_{enemyData.EnemyName}");
                enemyObject.transform.SetParent(enemyContainer);

                // 필수 컴포넌트 추가
                enemyObject.AddComponent<SpriteRenderer>();
                enemyObject.AddComponent<Enemy>();
                enemyObject.AddComponent<EnemyAI>();
            }

            // 위치 설정 (그리드 좌표를 월드 좌표로 변환)
            enemyObject.transform.position = new Vector3(position.x, position.y, 0);

            return enemyObject;
        }

        /// <summary>
        /// 랜덤 스폰 위치 가져오기
        /// </summary>
        private Vector2Int GetRandomSpawnPosition()
        {
            if (spawnPositions == null || spawnPositions.Length == 0)
            {
                Debug.LogError("[EnemyManager] 설정된 스폰 위치가 없습니다.");
                return Vector2Int.one * -1;
            }

            // 점유되지 않은 위치 찾기
            var availablePositions = spawnPositions.Where(pos => !IsPositionOccupied(pos)).ToList();

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning("[EnemyManager] 사용 가능한 스폰 위치가 없습니다.");
                return Vector2Int.one * -1;
            }

            return availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];
        }

        /// <summary>
        /// 모든 몬스터 턴 실행
        /// </summary>
        private System.Collections.IEnumerator ExecuteEnemyTurns(Vector2Int playerPosition)
        {
            var aliveEnemies = activeEnemies.Where(e => e.IsAlive).ToList();

            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                if (!isEnemyTurn) break;  // 턴이 중단된 경우

                var enemy = aliveEnemies[i];
                var enemyAI = enemy.GetComponent<EnemyAI>();

                if (enemy.IsAlive && !enemy.IsStunned && enemyAI != null)
                {
                    currentEnemyIndex = i;

                    if (debugMode)
                        Debug.Log($"[EnemyManager] {enemy.EnemyName} 턴 실행 중...");

                    // AI 턴 실행
                    enemyAI.ExecuteTurn(playerPosition, aliveEnemies);

                    // AI가 사고하는 동안 대기
                    while (enemyAI.IsThinking && isEnemyTurn)
                    {
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.1f);  // 짧은 대기
                }
            }

            // 모든 몬스터 턴 종료
            EndEnemyTurn();
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 몬스터 사망 이벤트 처리
        /// </summary>
        private void OnEnemyDeathHandler(Enemy deadEnemy)
        {
            if (deadEnemy == null) return;

            // 활성 리스트에서 제거하고 사망 리스트에 추가
            if (activeEnemies.Remove(deadEnemy))
            {
                deadEnemies.Add(deadEnemy);

                if (debugMode)
                    Debug.Log($"[EnemyManager] {deadEnemy.EnemyName} 사망 - 남은 몬스터: {AliveEnemyCount}마리");

                OnEnemyDied?.Invoke(deadEnemy);

                // 모든 몬스터가 사망했는지 확인
                if (AllEnemiesDead)
                {
                    if (debugMode)
                        Debug.Log("[EnemyManager] 모든 몬스터 사망!");

                    OnAllEnemiesDead?.Invoke();
                }
            }
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 몬스터 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug Enemy Status")]
        public void DebugEnemyStatus()
        {
            Debug.Log($"=== EnemyManager 상태 ===");
            Debug.Log($"활성 몬스터: {activeEnemies.Count}마리");
            Debug.Log($"사망 몬스터: {deadEnemies.Count}마리");
            Debug.Log($"살아있는 몬스터: {AliveEnemyCount}마리");
            Debug.Log($"몬스터 턴: {isEnemyTurn}");
            Debug.Log($"현재 몬스터 인덱스: {currentEnemyIndex}");

            foreach (var enemy in activeEnemies)
            {
                Debug.Log($"- {enemy.EnemyName}: {enemy.GridPosition}, HP: {enemy.CurrentHealth}/{enemy.MaxHealth}");
            }
        }
        #endregion
    }
}