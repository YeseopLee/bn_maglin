using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maglin.Enemy;
using Maglin.Core;
using Maglin.Player;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 전투 관리자 - 이동, 공격 등 전투 로직 담당
    /// </summary>
    public class MonsterBattleManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterBattleManager _instance;

        public static MonsterBattleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterBattleManager>();

                    if (_instance == null)
                    {
                        GameObject battleManagerObject = new GameObject("MonsterBattleManager");
                        _instance = battleManagerObject.AddComponent<MonsterBattleManager>();
                        DontDestroyOnLoad(battleManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 이동 완료 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy, Vector2Int> OnMonsterMoved;

        /// <summary>
        /// 몬스터 공격 완료 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy> OnMonsterAttacked;
        #endregion

        #region Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 플레이어 위치
        private Vector2Int playerGridPosition = new Vector2Int(0, 0);
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
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
        /// 플레이어 그리드 위치 설정
        /// </summary>
        public void SetPlayerPosition(Vector2Int gridPosition)
        {
            playerGridPosition = gridPosition;

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] 플레이어 그리드 위치: {gridPosition}");
        }

        /// <summary>
        /// 외부에서 몬스터 이동 요청 (카드 효과 등)
        /// </summary>
        public void RequestMonsterMovement(Maglin.Enemy.Enemy monster, Vector2Int newPosition)
        {
            if (monster == null) return;

            // 몬스터의 그리드 위치 업데이트
            SetMonsterGridPosition(monster, newPosition);

            // 부드러운 이동으로 월드 위치 업데이트
            UpdateMonsterPosition(monster);

            // 이번 턴에 AI 이동을 건너뛰도록 표시
            MarkMonsterMovedThisTurn(monster);

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] 외부 이동 요청 처리: {monster.EnemyName} -> {newPosition}");
        }

        /// <summary>
        /// 모든 몬스터의 이동 플래그 초기화 (턴 시작 시)
        /// </summary>
        public void ResetAllMonsterMoveFlags()
        {
            var allMonsters = GetAllMonsters();
            foreach (var monster in allMonsters)
            {
                if (monster != null)
                {
                    var moveFlag = monster.GetComponent<MonsterMoveFlag>();
                    if (moveFlag != null)
                    {
                        moveFlag.SetMovedThisTurn(false);
                    }
                }
            }

            if (debugMode)
                Debug.Log("[MonsterBattleManager] 모든 몬스터 이동 플래그 초기화");
        }

        /// <summary>
        /// 몬스터 이동 처리
        /// </summary>
        public IEnumerator ProcessMonsterMovement(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive) yield break;

            // 이번 턴에 이미 외부 요청으로 이동했다면 AI 이동 건너뛰기
            if (HasMonsterMovedThisTurn(monster))
            {
                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 이번 턴에 이미 이동함 - AI 이동 건너뛰기");
                yield return new WaitForSeconds(0.2f);
                yield break;
            }

            // 차징 중인 몬스터는 이동하지 않음
            if (MonsterPatternExecutor.Instance != null && MonsterPatternExecutor.Instance.IsMonsterCharging(monster))
            {
                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 차징 중으로 이동 건너뛰기");
                yield return new WaitForSeconds(0.2f);
                yield break;
            }

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 현재 다른 몬스터들의 위치 수집 (자기 자신 제외)
            var occupiedPositions = GetOccupiedPositions(monster);

            // AI가 충돌 고려한 이동 결정
            var newPosition = ai.DecideMovementWithCollision(playerGridPosition, occupiedPositions);

            if (newPosition != monster.GridPosition)
            {
                // Move 애니메이션 시작
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Move);
                }

                // 위치 이동
                SetMonsterGridPosition(monster, newPosition);
                UpdateMonsterPosition(monster);

                // 이동 완료 이벤트 발생
                OnMonsterMoved?.Invoke(monster, newPosition);

                if (debugMode)
                {
                    var movementPattern = monster.EnemyData?.MovementPattern.ToString() ?? "Unknown";
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} AI 위치 이동: {monster.GridPosition} -> {newPosition} (패턴: {movementPattern})");
                }

                // 부드러운 이동 완료까지 대기
                yield return new WaitForSeconds(0.4f);

                // 이동 완료 후 Idle 애니메이션으로 복귀
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
                }
            }
            else
            {
                // 이동하지 않은 경우 짧은 대기
                if (debugMode)
                {
                    var movementPattern = monster.EnemyData?.MovementPattern.ToString() ?? "Unknown";
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 이동 불가 (패턴: {movementPattern}, 현재 위치: {monster.GridPosition})");
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// 몬스터 공격 처리 (패턴 우선, 그 다음 일반 공격)
        /// </summary>
        public IEnumerator ProcessMonsterAttack(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive) yield break;
            if (monster.IsNeutralObject) yield break; // 중립 오브젝트는 공격하지 않음

            // 1. 패턴 실행 시도 (패턴이 있는 경우 우선 실행)
            var patternResult = ExecuteMonsterPatterns(monster);
            if (patternResult.executed)
            {
                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 패턴 실행: {patternResult.description}");

                // 패턴 애니메이션 재생
                if (MonsterAnimationManager.Instance != null)
                {
                    // 실행된 패턴 정보 가져오기
                    var executedPattern = GetCurrentExecutablePattern(monster);
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Pattern, executedPattern);
                }

                // 패턴 실행 애니메이션 대기
                float patternAnimationDuration = GetPatternAnimationDuration(monster);
                yield return new WaitForSeconds(patternAnimationDuration);

                // 패턴 완료 후 Idle로 복귀
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
                }

                // 패턴이 일반 행동을 차단하는 경우 여기서 종료
                if (patternResult.blockNormalActions)
                {
                    yield break;
                }
            }

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 1. 먼저 공격해야 할 중립 오브젝트가 있는지 확인
            var targetObject = FindAttackableNeutralObject(monster);
            if (targetObject != null)
            {
                var damage = monster.CurrentAttackDamage;

                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName}이 중립 오브젝트 {targetObject.EnemyName}를 공격! 데미지: {damage}");

                // Attack 애니메이션 재생
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Attack);
                }

                // 공격 애니메이션 완료까지 대기
                float attackAnimationDuration = GetAttackAnimationDuration(monster);
                yield return new WaitForSeconds(attackAnimationDuration);

                // 중립 오브젝트에게 데미지
                targetObject.TakeDamage(damage, monster.Element);

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

                // 공격 완료 후 Idle 애니메이션으로 복귀
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
                }

                // 공격 후 잠시 대기
                yield return new WaitForSeconds(0.2f);
                yield break;
            }

            // 2. 플레이어 공격 가능한지 확인
            if (ai.CanAttackPosition(playerGridPosition))
            {
                var damage = monster.CurrentAttackDamage;

                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName}이 플레이어를 공격! 데미지: {damage}");

                // Attack 애니메이션 재생
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Attack);
                }

                // 공격 애니메이션 완료까지 대기
                float attackAnimationDuration = GetAttackAnimationDuration(monster);
                yield return new WaitForSeconds(attackAnimationDuration);

                // 플레이어에게 데미지 (공격자 정보 포함)
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.TakeDamage(damage, monster);
                }

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

                // 공격 완료 후 Idle 애니메이션으로 복귀
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
                }

                // 공격 후 잠시 대기
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 공격 범위 밖");
            }
        }

        /// <summary>
        /// 새로운 몬스터 초기화 시 패턴 시스템에 등록
        /// </summary>
        public void InitializeMonsterPatterns(Maglin.Enemy.Enemy monster)
        {
            if (MonsterPatternExecutor.Instance != null)
            {
                MonsterPatternExecutor.Instance.InitializeMonsterPatterns(monster);
            }
        }

        /// <summary>
        /// 몬스터 사망 시 패턴 처리
        /// </summary>
        public void HandleMonsterDeathPatterns(Maglin.Enemy.Enemy deadMonster)
        {
            if (MonsterPatternExecutor.Instance != null)
            {
                MonsterPatternExecutor.Instance.HandleDeathPatterns(deadMonster);
            }
        }

        /// <summary>
        /// 턴 시작 시 모든 몬스터의 턴 카운터 증가
        /// </summary>
        public void IncrementMonsterTurnCounters()
        {
            if (MonsterPatternExecutor.Instance != null)
            {
                MonsterPatternExecutor.Instance.IncrementTurnCounters();
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 몬스터 패턴 실행
        /// </summary>
        private PatternExecutionResult ExecuteMonsterPatterns(Maglin.Enemy.Enemy monster)
        {
            if (MonsterPatternExecutor.Instance != null)
            {
                var result = MonsterPatternExecutor.Instance.ExecutePatterns(monster, playerGridPosition);

                // 패턴이 실행된 경우 몬스터에 표시
                if (result.executed)
                {
                    monster.MarkPatternExecuted();
                }

                return result;
            }

            return new PatternExecutionResult(false, false, "PatternExecutor 없음");
        }

        /// <summary>
        /// 모든 몬스터 가져오기 (MonsterSpawnManager에서)
        /// </summary>
        private List<Maglin.Enemy.Enemy> GetAllMonsters()
        {
            if (MonsterSpawnManager.Instance != null)
            {
                return MonsterSpawnManager.Instance.GetAllMonsters();
            }
            return new List<Maglin.Enemy.Enemy>();
        }

        /// <summary>
        /// 몬스터 그리드 위치 설정
        /// </summary>
        private void SetMonsterGridPosition(Maglin.Enemy.Enemy monster, Vector2Int position)
        {
            if (monster == null) return;

            // 리플렉션을 사용하여 private 필드에 접근
            var field = typeof(Maglin.Enemy.Enemy).GetField("gridPosition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(monster, position);
            }
        }

        /// <summary>
        /// 몬스터 위치 업데이트
        /// </summary>
        private void UpdateMonsterPosition(Maglin.Enemy.Enemy monster)
        {
            if (monster == null)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterBattleManager] UpdateMonsterPosition: monster가 null입니다.");
                return;
            }

            var gridPos = monster.GridPosition;

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] 몬스터 위치 업데이트: {monster.name} -> 그리드 위치 ({gridPos.x}, {gridPos.y})");

            // GridFieldManager를 통해 위치 업데이트
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                Vector3 targetWorldPos = GridFieldManager.Instance.GridToWorldPosition(gridPos);

                // 부드러운 이동으로 변경
                StartCoroutine(SmoothMoveToPosition(monster.gameObject, targetWorldPos, 0.5f));

                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] 몬스터 월드 위치 설정: {monster.name} -> {targetWorldPos}");
            }
            else
            {
                // GridFieldManager가 없는 경우 기본 계산
                Vector3 targetPos = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f);
                StartCoroutine(SmoothMoveToPosition(monster.gameObject, targetPos, 0.5f));

                if (debugMode)
                    Debug.LogWarning($"[MonsterBattleManager] GridFieldManager가 없어 기본 위치 계산 사용: {monster.name} -> {targetPos}");
            }
        }

        /// <summary>
        /// 몬스터가 이번 턴에 이미 이동했음을 표시
        /// </summary>
        private void MarkMonsterMovedThisTurn(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return;

            // 몬스터에 "이번 턴 이동함" 플래그 설정
            var moveFlag = monster.gameObject.GetComponent<MonsterMoveFlag>();
            if (moveFlag == null)
            {
                moveFlag = monster.gameObject.AddComponent<MonsterMoveFlag>();
            }
            moveFlag.SetMovedThisTurn(true);
        }

        /// <summary>
        /// 몬스터가 이번 턴에 이동했는지 확인
        /// </summary>
        private bool HasMonsterMovedThisTurn(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return false;

            var moveFlag = monster.gameObject.GetComponent<MonsterMoveFlag>();
            return moveFlag != null && moveFlag.HasMovedThisTurn();
        }

        /// <summary>
        /// 다른 몬스터들의 점유 위치 수집 (특정 몬스터 제외)
        /// </summary>
        private List<Vector2Int> GetOccupiedPositions(Maglin.Enemy.Enemy excludeMonster)
        {
            var occupiedPositions = new List<Vector2Int>();

            // 플레이어 위치 추가
            occupiedPositions.Add(playerGridPosition);

            // 다른 살아있는 몬스터들의 위치 수집
            var allMonsters = GetAllMonsters();
            foreach (var monster in allMonsters)
            {
                if (monster == null || !monster.IsAlive || monster == excludeMonster) continue;

                occupiedPositions.Add(monster.GridPosition);
            }

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] 점유된 위치들: {string.Join(", ", occupiedPositions)}");

            return occupiedPositions;
        }

        /// <summary>
        /// 부드러운 몬스터 이동
        /// </summary>
        private IEnumerator SmoothMoveToPosition(GameObject monster, Vector3 targetPosition, float duration)
        {
            if (monster == null) yield break;

            Vector3 startPosition = monster.transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (monster == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // Ease Out 효과 적용
                progress = 1f - (1f - progress) * (1f - progress);

                monster.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);

                yield return null;
            }

            // 최종 위치 보장
            if (monster != null)
            {
                monster.transform.position = targetPosition;
            }
        }

        /// <summary>
        /// 공격 가능한 중립 오브젝트 찾기
        /// </summary>
        private Maglin.Enemy.Enemy FindAttackableNeutralObject(Maglin.Enemy.Enemy attacker)
        {
            if (attacker == null || attacker.IsNeutralObject) return null;

            var allMonsters = GetAllMonsters();
            foreach (var enemy in allMonsters)
            {
                if (enemy == null || !enemy.IsAlive || enemy == attacker) continue;

                // 중립 오브젝트이고 몬스터가 공격할 수 있는 대상인지 확인
                if (enemy.IsNeutralObject && enemy.MonstersAttackThis)
                {
                    // 공격 범위 내에 있는지 확인
                    int distance = Mathf.Abs(attacker.GridPosition.x - enemy.GridPosition.x) +
                                   Mathf.Abs(attacker.GridPosition.y - enemy.GridPosition.y);

                    if (distance <= (attacker.EnemyData?.AttackRange ?? 1))
                    {
                        // 몬스터의 이동 경로를 막고 있는지 확인 (플레이어쪽으로 가는 경로)
                        if (enemy.BlocksMonsterMovement &&
                            enemy.GridPosition.x < attacker.GridPosition.x &&
                            enemy.GridPosition.y == attacker.GridPosition.y)
                        {
                            return enemy;
                        }
                    }
                }
            }

            return null;
        }

        #region Animation Helper Methods
        /// <summary>
        /// 공격 애니메이션 지속 시간 계산
        /// </summary>
        private float GetAttackAnimationDuration(Maglin.Enemy.Enemy monster)
        {
            if (monster?.EnemyData?.AttackSprites == null || monster.EnemyData.AttackSprites.Length == 0)
            {
                return 0.5f; // 기본 지속 시간
            }

            return monster.EnemyData.AttackSprites.Length * monster.EnemyData.AnimationSpeed;
        }

        /// <summary>
        /// 현재 실행 가능한 패턴 가져오기 (애니메이션용)
        /// </summary>
        private MonsterPatternSO GetCurrentExecutablePattern(Maglin.Enemy.Enemy monster)
        {
            if (monster?.EnemyData?.MonsterPatterns == null || monster.EnemyData.MonsterPatterns.Count == 0)
                return null;

            // 패턴 스프라이트가 있는 첫 번째 패턴을 반환
            foreach (var pattern in monster.EnemyData.MonsterPatterns)
            {
                if (pattern?.PatternSprites != null && pattern.PatternSprites.Length > 0)
                {
                    return pattern;
                }
            }

            // 스프라이트가 있는 패턴이 없으면 첫 번째 패턴 반환 (폴백)
            return monster.EnemyData.MonsterPatterns.FirstOrDefault();
        }

        /// <summary>
        /// 패턴 애니메이션 지속 시간 계산
        /// </summary>
        private float GetPatternAnimationDuration(Maglin.Enemy.Enemy monster)
        {
            // 현재 실행 가능한 패턴의 애니메이션 정보 사용
            var currentPattern = GetCurrentExecutablePattern(monster);
            if (currentPattern?.PatternSprites != null && currentPattern.PatternSprites.Length > 0)
            {
                return currentPattern.PatternSprites.Length * currentPattern.PatternAnimationSpeed;
            }

            // 패턴이 없거나 스프라이트가 없는 경우 기본 지속 시간
            return 0.3f;
        }

        /// <summary>
        /// 몬스터 Hit 애니메이션 재생
        /// </summary>
        public void PlayHitAnimation(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || MonsterAnimationManager.Instance == null) return;

            MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Hit);
            
            // Hit 애니메이션 후 Idle로 복귀 (코루틴으로 처리)
            StartCoroutine(ReturnToIdleAfterHit(monster));
        }

        /// <summary>
        /// Hit 애니메이션 후 Idle로 복귀
        /// </summary>
        private IEnumerator ReturnToIdleAfterHit(Maglin.Enemy.Enemy monster)
        {
            if (monster?.EnemyData?.HitSprites != null && monster.EnemyData.HitSprites.Length > 0)
            {
                float hitAnimationDuration = monster.EnemyData.HitSprites.Length * monster.EnemyData.AnimationSpeed;
                yield return new WaitForSeconds(hitAnimationDuration);
            }
            else
            {
                yield return new WaitForSeconds(0.3f); // 기본 지속 시간
            }

            // Idle 애니메이션으로 복귀
            if (monster != null && monster.IsAlive && MonsterAnimationManager.Instance != null)
            {
                MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
            }
        }

        /// <summary>
        /// 몬스터 Death 애니메이션 재생
        /// </summary>
        public void PlayDeathAnimation(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || MonsterAnimationManager.Instance == null) return;

            MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, MonsterAnimationState.Death);
        }
        #endregion
        #endregion
    }
}
