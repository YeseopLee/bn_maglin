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

        // 진행 중인 사망 소환 패턴 추적
        private int activeDeathSpawnPatterns = 0;
        #endregion

        #region Events
        /// <summary>
        /// 모든 사망 소환 패턴이 완료되었을 때 발생하는 이벤트
        /// </summary>
        public static event System.Action OnAllDeathSpawnPatternsCompleted;
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
        /// 현재 진행 중인 사망 소환 패턴이 있는지 확인
        /// </summary>
        public bool HasActiveDeathSpawnPatterns()
        {
            return activeDeathSpawnPatterns > 0;
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
            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}이(가) 파괴 및 공격 패턴 실행 시작");

            // 1. 파괴할 몬스터들 찾기
            var monstersToDestroy = GetMonstersToDestroy(monster, pattern);
            
            if (monstersToDestroy.Count == 0)
            {
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] 파괴할 몬스터가 없음");
                return new PatternExecutionResult(false, pattern.BlockNormalAttack, "파괴할 몬스터가 없음");
            }

            // 2. 몬스터들 파괴
            foreach (var targetMonster in monstersToDestroy)
            {
                if (targetMonster != null && targetMonster.IsAlive)
                {
                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {targetMonster.EnemyName} 파괴");
                    
                    // 몬스터를 즉시 죽임 (사망 애니메이션과 효과 포함)
                    targetMonster.TakeDamage(targetMonster.CurrentHealth);
                }
            }

            // 3. 공격 대상에게 데미지 적용
            ApplyDestructionDamage(monster, pattern, playerPosition);

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] 파괴 및 공격 패턴 실행 완료");

            return new PatternExecutionResult(true, pattern.BlockNormalAttack, $"{monstersToDestroy.Count}개 몬스터 파괴 후 {pattern.DestructionDamage} 데미지 적용");
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
                    // 진행 중인 사망 소환 패턴 카운터 증가
                    activeDeathSpawnPatterns++;
                    
                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] 사망 소환 패턴 시작: {deadMonster.EnemyName} (진행 중: {activeDeathSpawnPatterns})");

                    // 사망 애니메이션 완료 후 소환하도록 코루틴 시작
                    StartCoroutine(ExecuteDeathSpawnAfterAnimation(deadMonster, pattern));
                }
            }
        }

        /// <summary>
        /// 사망 애니메이션 완료 후 몬스터 소환
        /// </summary>
        private System.Collections.IEnumerator ExecuteDeathSpawnAfterAnimation(Enemy deadMonster, MonsterPatternSO pattern)
        {
            if (deadMonster == null || pattern?.SpawnedMonsterData == null)
            {
                // 실패시에도 카운터 감소
                activeDeathSpawnPatterns--;
                yield break;
            }

            Vector2Int deadMonsterPosition = deadMonster.GridPosition;

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {deadMonster.EnemyName} 사망 시 소환 패턴 시작 - 사망 애니메이션 대기 중...");

            // 사망 애니메이션 완료 대기
            yield return WaitForDeathAnimationComplete(deadMonster);

            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] {deadMonster.EnemyName} 사망 애니메이션 완료 - 소환 시작");

            // 소환 위치 계산 (사망한 몬스터의 위치 정보 전달)
            Vector2Int spawnPosition = CalculateDeathSpawnPosition(deadMonsterPosition, pattern);

            // 위치가 유효한지 확인
            if (!IsValidSpawnPosition(spawnPosition))
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterPatternExecutor] {deadMonster.EnemyName}의 사망 소환 패턴: 위치 {spawnPosition}이 유효하지 않음");

                // 대체 위치 찾기
                spawnPosition = FindRandomEmptyPosition();
                if (!IsValidSpawnPosition(spawnPosition))
                {
                    if (debugMode)
                        Debug.LogWarning($"[MonsterPatternExecutor] {deadMonster.EnemyName}의 사망 소환 패턴: 소환 가능한 빈 공간 없음");
                    
                    // 실패시에도 카운터 감소
                    activeDeathSpawnPatterns--;
                    if (activeDeathSpawnPatterns <= 0)
                    {
                        NotifyDeathSpawnPatternsCompleted();
                    }
                    yield break;
                }
            }

            // 몬스터 소환
            var spawnedMonster = SpawnMonster(pattern.SpawnedMonsterData, spawnPosition);

            if (spawnedMonster != null)
            {
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {deadMonster.EnemyName} 사망 시 {pattern.SpawnedMonsterData.EnemyName}을 {spawnPosition}에 소환 완료!");

                // 스폰 애니메이션 완료까지 대기
                yield return WaitForSpawnAnimationComplete(spawnedMonster);
                
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {spawnedMonster.EnemyName} 스폰 애니메이션 완료 - 패턴 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterPatternExecutor] {deadMonster.EnemyName} 사망 시 몬스터 소환 실패");
            }

            // 스폰이 완전히 완료된 후 카운터 감소
            activeDeathSpawnPatterns--;
            
            if (debugMode)
                Debug.Log($"[MonsterPatternExecutor] 사망 소환 패턴 완료: {deadMonster.EnemyName} (남은 진행 중: {activeDeathSpawnPatterns})");

            // 모든 사망 소환 패턴이 완료되었으면 전투 종료 조건 재확인
            if (activeDeathSpawnPatterns <= 0)
            {
                if (debugMode)
                    Debug.Log("[MonsterPatternExecutor] 모든 사망 소환 패턴 완료 - 전투 종료 조건 재확인 요청");

                // BattleTestController에게 전투 종료 조건 재확인 요청
                NotifyDeathSpawnPatternsCompleted();
            }
        }

        /// <summary>
        /// 사망 애니메이션 완료 대기
        /// </summary>
        private System.Collections.IEnumerator WaitForDeathAnimationComplete(Enemy deadMonster)
        {
            if (deadMonster?.EnemyData?.DeathSprites != null && deadMonster.EnemyData.DeathSprites.Length > 0)
            {
                // Death 스프라이트 애니메이션 시간 계산
                float deathAnimationDuration = deadMonster.EnemyData.DeathSprites.Length * deadMonster.EnemyData.AnimationSpeed;
                
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] 사망 애니메이션 대기: {deathAnimationDuration:F2}초");

                yield return new WaitForSeconds(deathAnimationDuration);
            }
            else
            {
                // Death 스프라이트가 없으면 기본 대기 시간
                yield return new WaitForSeconds(0.5f);
            }

            // 추가 여유 시간 (안전장치)
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// 스폰 애니메이션 완료 대기
        /// </summary>
        private System.Collections.IEnumerator WaitForSpawnAnimationComplete(Enemy spawnedMonster)
        {
            if (spawnedMonster == null) yield break;

            // MonsterSpawnAnimationManager에서 스폰 애니메이션을 사용하는지 확인
            if (MonsterSpawnManager.Instance != null && MonsterSpawnAnimationManager.Instance != null)
            {
                bool animationCompleted = false;
                
                // 스폰 애니메이션 완료 이벤트 구독 (일회성)
                System.Action<GameObject> onSpawnAnimationCompleted = null;
                onSpawnAnimationCompleted = (completedMonster) =>
                {
                    if (completedMonster == spawnedMonster.gameObject)
                    {
                        animationCompleted = true;
                        // 이벤트 구독 해제
                        MonsterSpawnAnimationManager.OnSpawnAnimationCompleted -= onSpawnAnimationCompleted;
                        
                        if (debugMode)
                            Debug.Log($"[MonsterPatternExecutor] {spawnedMonster.EnemyName} 스폰 애니메이션 완료 이벤트 수신");
                    }
                };

                MonsterSpawnAnimationManager.OnSpawnAnimationCompleted += onSpawnAnimationCompleted;

                // 스폰 애니메이션 완료까지 대기 (최대 5초)
                float waitTime = 0f;
                while (!animationCompleted && waitTime < 5f)
                {
                    yield return new WaitForSeconds(0.1f);
                    waitTime += 0.1f;
                }

                // 이벤트 구독 해제 (안전장치)
                MonsterSpawnAnimationManager.OnSpawnAnimationCompleted -= onSpawnAnimationCompleted;

                if (debugMode)
                {
                    if (animationCompleted)
                        Debug.Log($"[MonsterPatternExecutor] {spawnedMonster.EnemyName} 스폰 애니메이션 대기 완료 (대기시간: {waitTime:F1}초)");
                    else
                        Debug.LogWarning($"[MonsterPatternExecutor] {spawnedMonster.EnemyName} 스폰 애니메이션 대기 시간 초과 (대기시간: {waitTime:F1}초)");
                }
            }
            else
            {
                // 스폰 애니메이션 매니저가 없으면 기본 대기 시간
                yield return new WaitForSeconds(0.5f);
                
                if (debugMode)
                    Debug.Log($"[MonsterPatternExecutor] {spawnedMonster.EnemyName} 스폰 애니메이션 매니저 없음 - 기본 대기 완료");
            }

            // 추가 안전 여유 시간
            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>
        /// 사망 시 소환 위치 계산 (사망한 몬스터의 위치 기반)
        /// </summary>
        private Vector2Int CalculateDeathSpawnPosition(Vector2Int deadMonsterPosition, MonsterPatternSO pattern)
        {
            // GridFieldManager에서 실제 그리드 크기 가져오기
            int gridWidth = 10;

            if (GridFieldManager.Instance != null)
            {
                gridWidth = GridFieldManager.Instance.GridWidth;
            }

            switch (pattern.SpawnLocation)
            {
                case SpawnLocationType.AtDeathPosition:
                    // 사망한 몬스터의 정확한 위치에 소환
                    return deadMonsterPosition;

                case SpawnLocationType.InFrontOfSelf:
                    // 사망한 몬스터 앞(왼쪽)에 소환
                    return new Vector2Int(deadMonsterPosition.x - 1, 0);

                case SpawnLocationType.SpecificPosition:
                    // 특정 위치로 소환
                    return new Vector2Int(pattern.SpecificSpawnPosition.x, 0);

                case SpawnLocationType.RightmostPosition:
                    // 우측 끝 칸
                    return new Vector2Int(gridWidth - 1, 0);

                case SpawnLocationType.RandomEmpty:
                    return FindRandomEmptyPosition();

                default:
                    // 기본값: 사망한 위치
                    return deadMonsterPosition;
            }
        }

        /// <summary>
        /// 소환 위치 계산 (생존 몬스터 기반)
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

                case SpawnLocationType.AtDeathPosition:
                    // 사망 시 소환 전용이므로 일반 패턴에서는 사용하지 않음
                    if (debugMode)
                        Debug.LogWarning($"[MonsterPatternExecutor] AtDeathPosition은 사망 시 소환 패턴에서만 사용 가능합니다. 기본 위치로 대체합니다.");
                    return new Vector2Int(monster.GridPosition.x, 0);

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
        /// 사망 소환 패턴 완료 알림
        /// </summary>
        private void NotifyDeathSpawnPatternsCompleted()
        {
            OnAllDeathSpawnPatternsCompleted?.Invoke();
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

        /// <summary>
        /// 파괴할 몬스터들을 찾기
        /// </summary>
        private List<Enemy> GetMonstersToDestroy(Enemy executingMonster, MonsterPatternSO pattern)
        {
            var monstersToDestroy = new List<Enemy>();
            
            if (MonsterSpawnManager.Instance == null) return monstersToDestroy;
            
            var allMonsters = MonsterSpawnManager.Instance.GetAllMonsters();
            
            foreach (var monster in allMonsters)
            {
                if (monster == null || !monster.IsAlive) continue;
                if (monster == executingMonster) continue; // 자기 자신은 제외
                
                // 타겟 타입 확인
                if (pattern.TargetDestroyType == null)
                {
                    // null이면 모든 중립 몬스터 (IsNeutralObject인 몬스터들)
                    if (monster.IsNeutralObject)
                    {
                        monstersToDestroy.Add(monster);
                    }
                }
                else
                {
                    // 특정 타입의 몬스터만
                    if (monster.EnemyData == pattern.TargetDestroyType)
                    {
                        monstersToDestroy.Add(monster);
                    }
                }
            }
            
            return monstersToDestroy;
        }

        /// <summary>
        /// 파괴 후 공격 데미지 적용
        /// </summary>
        private void ApplyDestructionDamage(Enemy executingMonster, MonsterPatternSO pattern, Vector2Int playerPosition)
        {
            int damage = pattern.DestructionDamage;
            if (damage <= 0) return;

            switch (pattern.DestructionTarget)
            {
                case AttackTargetType.Player:
                    // 플레이어에게만 데미지
                    if (PlayerManager.Instance != null)
                    {
                        if (debugMode)
                            Debug.Log($"[MonsterPatternExecutor] 플레이어에게 {damage} 파괴 데미지");
                        PlayerManager.Instance.TakeDamage(damage, executingMonster);
                    }
                    break;

                case AttackTargetType.AllMonsters:
                    // 모든 몬스터에게 데미지 (자기자신 제외)
                    ApplyDamageToAllMonsters(executingMonster, damage);
                    break;

                case AttackTargetType.PlayerAndAllMonsters:
                    // 플레이어와 모든 몬스터에게 데미지
                    if (PlayerManager.Instance != null)
                    {
                        if (debugMode)
                            Debug.Log($"[MonsterPatternExecutor] 플레이어에게 {damage} 파괴 데미지");
                        PlayerManager.Instance.TakeDamage(damage, executingMonster);
                    }
                    ApplyDamageToAllMonsters(executingMonster, damage);
                    break;

                case AttackTargetType.Self:
                    // 자기자신에게만 데미지
                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {executingMonster.EnemyName}이(가) 자기자신에게 {damage} 파괴 데미지");
                    executingMonster.TakeDamage(damage);
                    break;
            }
        }

        /// <summary>
        /// 모든 몬스터에게 데미지 적용 (실행하는 몬스터 제외)
        /// </summary>
        private void ApplyDamageToAllMonsters(Enemy executingMonster, int damage)
        {
            if (MonsterSpawnManager.Instance == null) return;
            
            var allMonsters = MonsterSpawnManager.Instance.GetAllMonsters();
            foreach (var monster in allMonsters)
            {
                if (monster != null && monster.IsAlive && monster != executingMonster)
                {
                    if (debugMode)
                        Debug.Log($"[MonsterPatternExecutor] {monster.EnemyName}에게 {damage} 파괴 데미지");
                    monster.TakeDamage(damage);
                }
            }
        }
        #endregion
    }
}
