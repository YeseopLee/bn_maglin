using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maglin.Battle;
using Maglin.Player;

namespace Maglin.Enemy
{
    /// <summary>
    /// 패턴 실행 결과
    /// </summary>
    public struct PatternExecutionResult
    {
        public bool executed;               // 패턴이 실행되었는지
        public bool blockNormalActions;     // 일반 행동(이동/공격)을 차단하는지
        public string description;          // 실행 결과 설명

        public PatternExecutionResult(bool exec, bool block, string desc)
        {
            executed = exec;
            blockNormalActions = block;
            description = desc;
        }
    }

    /// <summary>
    /// 몬스터 패턴 실행 담당 클래스
    /// </summary>
    public class MonsterPatternExecutor : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterPatternExecutor _instance;

        public static MonsterPatternExecutor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterPatternExecutor>();

                    if (_instance == null)
                    {
                        GameObject executorObject = new GameObject("MonsterPatternExecutor");
                        _instance = executorObject.AddComponent<MonsterPatternExecutor>();
                        DontDestroyOnLoad(executorObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 패턴 실행 상태 추적
        private Dictionary<Enemy, Dictionary<MonsterPatternSO, int>> patternTurnCounters =
            new Dictionary<Enemy, Dictionary<MonsterPatternSO, int>>();

        private Dictionary<Enemy, Dictionary<MonsterPatternSO, int>> chargeStates =
            new Dictionary<Enemy, Dictionary<MonsterPatternSO, int>>();
        #endregion

        #region Unity Lifecycle
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// 몬스터의 패턴을 확인하고 실행
        /// </summary>
        public PatternExecutionResult ExecutePatterns(Enemy monster, Vector2Int playerPosition)
        {
            if (monster == null || monster.EnemyData == null)
                return new PatternExecutionResult(false, false, "몬스터 데이터 없음");

            // 몬스터의 패턴 리스트 가져오기
            var patterns = monster.EnemyData.MonsterPatterns;
            if (patterns == null || patterns.Count == 0)
            {
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}에 패턴이 없음 (패턴 수: {patterns?.Count ?? 0})");
                return new PatternExecutionResult(false, false, "패턴 없음");
            }

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName} 패턴 실행 시도 - 패턴 수: {patterns.Count}");

            // 턴 카운터 초기화 (필요시)
            InitializePatternCounters(monster, patterns);

            // 우선순위별로 정렬하여 실행 가능한 패턴 찾기
            var sortedPatterns = patterns.OrderByDescending(p => p.Priority).ToList();

            foreach (var pattern in sortedPatterns)
            {
                var result = TryExecutePattern(monster, pattern, playerPosition);
                if (result.executed)
                {
                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}의 패턴 '{pattern.PatternName}' 실행: {result.description}");

                    return result;
                }
            }

            return new PatternExecutionResult(false, false, "실행 가능한 패턴 없음");
        }

        /// <summary>
        /// 몬스터 사망 시 패턴 처리
        /// </summary>
        public void HandleDeathPatterns(Enemy deadMonster)
        {
            if (deadMonster == null || deadMonster.EnemyData == null) return;

            var patterns = deadMonster.EnemyData.MonsterPatterns;
            if (patterns == null) return;

            var deathPatterns = patterns.Where(p => p.IsDeathTriggerPattern).ToList();

            foreach (var pattern in deathPatterns.OrderByDescending(p => p.Priority))
            {
                ExecuteDeathPattern(deadMonster, pattern);
            }

            // 사망한 몬스터의 패턴 상태 정리
            CleanupMonsterPatterns(deadMonster);
        }

        /// <summary>
        /// 몬스터의 패턴 상태 초기화 (전투 시작 시)
        /// </summary>
        public void InitializeMonsterPatterns(Enemy monster)
        {
            if (monster == null || monster.EnemyData == null) return;

            var patterns = monster.EnemyData.MonsterPatterns;
            if (patterns == null) return;

            InitializePatternCounters(monster, patterns);

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}의 패턴 상태 초기화 완료");
        }

        /// <summary>
        /// 몬스터가 차징 중인지 확인
        /// </summary>
        public bool IsMonsterCharging(Enemy monster)
        {
            if (!chargeStates.ContainsKey(monster)) return false;

            return chargeStates[monster].Values.Any(chargeTime => chargeTime > 0);
        }

        /// <summary>
        /// 모든 몬스터의 턴 카운터 증가
        /// </summary>
        public void IncrementTurnCounters()
        {
            foreach (var monsterEntry in patternTurnCounters.Keys.ToList())
            {
                if (monsterEntry == null || !monsterEntry.IsAlive)
                {
                    CleanupMonsterPatterns(monsterEntry);
                    continue;
                }

                foreach (var patternEntry in patternTurnCounters[monsterEntry].Keys.ToList())
                {
                    patternTurnCounters[monsterEntry][patternEntry]++;

                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {monsterEntry.EnemyName}의 패턴 '{patternEntry.PatternName}' 턴 카운터: {patternTurnCounters[monsterEntry][patternEntry]}");
                }
            }

            // 차징 상태 업데이트
            UpdateChargeStates();

            if (debugMode)
                Debug.Log("[MonsterPatternExecutor] 모든 몬스터 턴 카운터 증가");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 패턴 카운터 초기화
        /// </summary>
        private void InitializePatternCounters(Enemy monster, List<MonsterPatternSO> patterns)
        {
            if (!patternTurnCounters.ContainsKey(monster))
            {
                patternTurnCounters[monster] = new Dictionary<MonsterPatternSO, int>();
                chargeStates[monster] = new Dictionary<MonsterPatternSO, int>();
            }

            foreach (var pattern in patterns)
            {
                if (!patternTurnCounters[monster].ContainsKey(pattern))
                {
                    // 패턴 카운터를 1로 시작 (첫 몬스터 턴이 1턴)
                    patternTurnCounters[monster][pattern] = 1;
                    chargeStates[monster][pattern] = 0;

                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}의 패턴 '{pattern.PatternName}' 카운터 초기화: 1턴");
                }
            }
        }

        /// <summary>
        /// 패턴 실행 시도
        /// </summary>
        private PatternExecutionResult TryExecutePattern(Enemy monster, MonsterPatternSO pattern, Vector2Int playerPosition)
        {
            if (!patternTurnCounters.ContainsKey(monster) || !patternTurnCounters[monster].ContainsKey(pattern))
                return new PatternExecutionResult(false, false, "패턴 카운터 없음");

            int currentTurn = patternTurnCounters[monster][pattern];

            // 차징 중인 패턴 처리
            if (chargeStates[monster][pattern] > 0)
            {
                return HandleChargingPattern(monster, pattern);
            }

            // 패턴 트리거 조건 확인
            if (!pattern.ShouldTriggerOnTurn(currentTurn))
            {
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}의 패턴 '{pattern.PatternName}' 트리거 조건 불충족 - 현재턴: {currentTurn}, 간격: {pattern.TriggerInterval}, 오프셋: {pattern.TurnOffset}");
                return new PatternExecutionResult(false, false, "트리거 조건 불충족");
            }

            // 패턴 타입별 실행
            switch (pattern.PatternType)
            {
                case MonsterPatternType.Periodic_SpawnMonster:
                    return ExecuteSpawnMonsterPattern(monster, pattern);

                case MonsterPatternType.Periodic_ChargeAttack:
                    return StartChargeAttackPattern(monster, pattern);

                case MonsterPatternType.Periodic_DestroyAndAttack:
                    return ExecuteDestroyAndAttackPattern(monster, pattern, playerPosition);

                default:
                    return new PatternExecutionResult(false, false, "알 수 없는 패턴 타입");
            }
        }

        /// <summary>
        /// 차징 패턴 처리
        /// </summary>
        private PatternExecutionResult HandleChargingPattern(Enemy monster, MonsterPatternSO pattern)
        {
            chargeStates[monster][pattern]--;

            if (chargeStates[monster][pattern] <= 0)
            {
                // 차징 완료 - 공격 실행
                return ExecuteChargeAttack(monster, pattern);
            }
            else
            {
                // 차징 중 - 패턴 설정에 따라 일반 행동 차단
                int remainingTurns = chargeStates[monster][pattern];
                return new PatternExecutionResult(true, pattern.BlockNormalAttack, $"차징 중... (남은 턴: {remainingTurns})");
            }
        }

        /// <summary>
        /// 차지 공격 시작
        /// </summary>
        private PatternExecutionResult StartChargeAttackPattern(Enemy monster, MonsterPatternSO pattern)
        {
            chargeStates[monster][pattern] = pattern.ChargeDuration;

            return new PatternExecutionResult(true, pattern.BlockNormalAttack, $"차지 공격 준비 시작 ({pattern.ChargeDuration}턴)");
        }

        /// <summary>
        /// 차지 공격 실행
        /// </summary>
        private PatternExecutionResult ExecuteChargeAttack(Enemy monster, MonsterPatternSO pattern)
        {
            int damage = pattern.ChargeDamage;

            // 대상에 따라 데미지 처리
            switch (pattern.ChargeTarget)
            {
                case AttackTargetType.Player:
                    DamagePlayer(damage, monster);
                    break;

                case AttackTargetType.AllMonsters:
                    DamageAllMonsters(damage, monster);
                    break;

                case AttackTargetType.PlayerAndAllMonsters:
                    DamagePlayer(damage, monster);
                    DamageAllMonsters(damage, monster);
                    break;
            }

            return new PatternExecutionResult(true, pattern.BlockNormalAttack, $"차지 공격 완료! 데미지: {damage}");
        }

        /// <summary>
        /// 몬스터 소환 패턴 실행 (N턴마다 새로운 몬스터 소환)
        /// </summary>
        private PatternExecutionResult ExecuteSpawnMonsterPattern(Enemy monster, MonsterPatternSO pattern)
        {
            if (pattern.SpawnedMonsterData == null)
                return new PatternExecutionResult(false, false, "소환할 몬스터 데이터 없음");

            // 소환 위치 계산
            Vector2Int spawnPosition = CalculateSpawnPosition(monster, pattern);

            // 위치가 유효한지 확인
            if (!IsValidSpawnPosition(spawnPosition))
            {
                // 랜덤 위치가 아닌 경우, 다른 빈 위치 시도
                if (pattern.SpawnLocation != SpawnLocationType.RandomEmpty)
                {
                    spawnPosition = FindRandomEmptyPosition();
                    if (!IsValidSpawnPosition(spawnPosition))
                    {
                        if (debugMode)
                            Debug.LogWarning($"[MonsterPatternExecutor] {monster.EnemyName}의 소환 패턴: 빈 공간이 없어 소환 불가");
                        return new PatternExecutionResult(false, false, "소환 가능한 빈 공간 없음");
                    }
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning($"[MonsterPatternExecutor] {monster.EnemyName}의 소환 패턴: 빈 공간이 없어 소환 불가");
                    return new PatternExecutionResult(false, false, "소환 가능한 빈 공간 없음");
                }
            }

            // 몬스터 소환
            var spawnedMonster = SpawnMonster(pattern.SpawnedMonsterData, spawnPosition);

            if (spawnedMonster != null)
            {
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}이 {pattern.SpawnedMonsterData.EnemyName}을 {spawnPosition}에 소환!");

                return new PatternExecutionResult(true, pattern.BlockNormalAttack, $"{pattern.SpawnedMonsterData.EnemyName} 소환 성공");
            }
            else
            {
                return new PatternExecutionResult(false, false, "몬스터 소환 실패");
            }
        }

        /// <summary>
        /// 파괴 및 공격 패턴 실행
        /// </summary>
        private PatternExecutionResult ExecuteDestroyAndAttackPattern(Enemy monster, MonsterPatternSO pattern, Vector2Int playerPosition)
        {
            // 구현 예정 - 현재는 기본 구조만
            return new PatternExecutionResult(false, pattern.BlockNormalAttack, "파괴 및 공격 패턴 구현 예정");
        }

        /// <summary>
        /// 사망 패턴 실행
        /// </summary>
        private void ExecuteDeathPattern(Enemy deadMonster, MonsterPatternSO pattern)
        {
            if (pattern.PatternType == MonsterPatternType.OnDeath_SpawnMonster)
            {
                if (pattern.SpawnedMonsterData != null)
                {
                    Vector2Int spawnPosition = CalculateSpawnPosition(deadMonster, pattern);
                    SpawnMonster(pattern.SpawnedMonsterData, spawnPosition);

                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {deadMonster.EnemyName} 사망 시 {pattern.SpawnedMonsterData.EnemyName} 소환");
                }
            }
        }

        /// <summary>
        /// 소환 위치 계산
        /// </summary>
        private Vector2Int CalculateSpawnPosition(Enemy monster, MonsterPatternSO pattern)
        {
            // GridFieldManager에서 실제 그리드 크기 가져오기
            int gridWidth = 10;

            if (GridFieldManager.Instance != null)
            {
                gridWidth = GridFieldManager.Instance.GridWidth;
            }

            switch (pattern.SpawnLocation)
            {
                case SpawnLocationType.InFrontOfSelf:
                    // 몬스터 앞(왼쪽)에 소환, 1차원 전투는 y=0 고정
                    return new Vector2Int(monster.GridPosition.x - 1, 0);

                case SpawnLocationType.SpecificPosition:
                    // 특정 위치도 y=0으로 강제 조정
                    return new Vector2Int(pattern.SpecificSpawnPosition.x, 0);

                case SpawnLocationType.RightmostPosition:
                    // 우측 끝 칸
                    return new Vector2Int(gridWidth - 1, 0);

                case SpawnLocationType.RandomEmpty:
                    return FindRandomEmptyPosition();

                default:
                    // 기본값도 y=0으로 조정
                    return new Vector2Int(monster.GridPosition.x, 0);
            }
        }

        /// <summary>
        /// 유효한 소환 위치인지 확인
        /// </summary>
        private bool IsValidSpawnPosition(Vector2Int position)
        {
            // GridFieldManager에서 실제 그리드 크기 가져오기
            int gridWidth = 10;
            int gridHeight = 1;

            if (GridFieldManager.Instance != null)
            {
                gridWidth = GridFieldManager.Instance.GridWidth;
                gridHeight = GridFieldManager.Instance.GridHeight;
            }

            // 경계 확인 (1차원 전투는 y=0만 유효)
            if (position.x < 0 || position.x >= gridWidth || position.y != 0)
                return false;

            // 위치가 점유되어 있는지 확인
            return !IsPositionOccupied(position);
        }

        /// <summary>
        /// 위치가 점유되어 있는지 확인
        /// </summary>
        private bool IsPositionOccupied(Vector2Int position)
        {
            // MonsterSpawnManager를 통해 확인
            if (MonsterSpawnManager.Instance != null)
            {
                return MonsterSpawnManager.Instance.IsPositionOccupied(position);
            }
            return false;
        }

        /// <summary>
        /// 랜덤한 빈 위치 찾기
        /// </summary>
        private Vector2Int FindRandomEmptyPosition()
        {
            var emptyPositions = new List<Vector2Int>();

            // GridFieldManager에서 실제 그리드 크기 가져오기
            int gridWidth = 10;

            if (GridFieldManager.Instance != null)
            {
                gridWidth = GridFieldManager.Instance.GridWidth;
            }

            // 1차원 전투는 y=0만 검사
            for (int x = 0; x < gridWidth; x++)
            {
                Vector2Int pos = new Vector2Int(x, 0);
                if (IsValidSpawnPosition(pos))
                {
                    emptyPositions.Add(pos);
                }
            }

            if (emptyPositions.Count > 0)
            {
                return emptyPositions[Random.Range(0, emptyPositions.Count)];
            }

            if (debugMode)
                Debug.LogWarning("[MonsterPatternExecutor] 소환 가능한 빈 위치를 찾을 수 없습니다!");

            return Vector2Int.zero; // 실패 시 기본 위치
        }

        /// <summary>
        /// 몬스터 소환
        /// </summary>
        private Enemy SpawnMonster(EnemySO monsterData, Vector2Int position)
        {
            if (MonsterSpawnManager.Instance != null)
            {
                return MonsterSpawnManager.Instance.SpawnMonsterForPattern(monsterData, position);
            }
            return null;
        }

        /// <summary>
        /// 플레이어에게 데미지
        /// </summary>
        private void DamagePlayer(int damage, Enemy attacker)
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.TakeDamage(damage, attacker);
            }
        }

        /// <summary>
        /// 모든 몬스터에게 데미지
        /// </summary>
        private void DamageAllMonsters(int damage, Enemy attacker)
        {
            if (MonsterSpawnManager.Instance != null)
            {
                var allMonsters = MonsterSpawnManager.Instance.GetAllMonsters();
                foreach (var monster in allMonsters)
                {
                    if (monster != null && monster.IsAlive && monster != attacker)
                    {
                        monster.TakeDamage(damage, attacker.Element);
                    }
                }
            }
        }

        /// <summary>
        /// 차징 상태 업데이트
        /// </summary>
        private void UpdateChargeStates()
        {
            foreach (var monsterEntry in chargeStates.Keys.ToList())
            {
                if (monsterEntry == null || !monsterEntry.IsAlive)
                {
                    chargeStates.Remove(monsterEntry);
                    continue;
                }

                foreach (var patternEntry in chargeStates[monsterEntry].Keys.ToList())
                {
                    // 차징 카운터는 HandleChargingPattern에서 감소
                }
            }
        }

        /// <summary>
        /// 몬스터 패턴 상태 정리
        /// </summary>
        private void CleanupMonsterPatterns(Enemy monster)
        {
            if (monster == null) return;

            patternTurnCounters.Remove(monster);
            chargeStates.Remove(monster);

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}의 패턴 상태 정리 완료");
        }
        #endregion
    }
}
