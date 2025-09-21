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
            RequestMonsterMovement(monster, newPosition, null);
        }

        /// <summary>
        /// 외부에서 몬스터 이동 요청 (패턴별 커스텀 속도 지원)
        /// </summary>
        public void RequestMonsterMovement(Maglin.Enemy.Enemy monster, Vector2Int newPosition, float? customMoveSpeed)
        {
            if (monster == null) return;

            // 이전 위치 저장
            Vector2Int previousPosition = monster.GridPosition;

            // 이동 시간 계산 (커스텀 속도 고려)
            int moveDistance = Mathf.Abs(newPosition.x - previousPosition.x) + Mathf.Abs(newPosition.y - previousPosition.y);
            float totalMoveTime = customMoveSpeed.HasValue ?
                CalculateMoveTime(monster, moveDistance, customMoveSpeed.Value) :
                CalculateMoveTime(monster, moveDistance);

            // Move 애니메이션 시작 (계산된 이동 시간에 맞춰)
            if (MonsterAnimationManager.Instance != null)
            {
                MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Move, null, totalMoveTime);
            }

            // 몬스터의 그리드 위치 업데이트
            SetMonsterGridPosition(monster, newPosition);

            // 부드러운 이동으로 월드 위치 업데이트 (이전 위치 정보 및 계산된 시간 전달)
            UpdateMonsterPosition(monster, previousPosition, totalMoveTime);

            // 이번 턴에 AI 이동을 건너뛰도록 표시
            MarkMonsterMovedThisTurn(monster);

            // 외부 요청 이동 완료 후 휴식 시작
            monster.StartMovementRest();

            if (debugMode)
            {
                string speedInfo = customMoveSpeed.HasValue ? $" (커스텀 속도: {customMoveSpeed.Value})" : "";
                Debug.Log($"[MonsterBattleManager] 외부 이동 요청 처리: {monster.EnemyName} {previousPosition} -> {newPosition} (시간: {totalMoveTime:F2}초{speedInfo})");
            }
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
                // 이전 위치 저장
                Vector2Int previousPosition = monster.GridPosition;

                // 이동 거리 계산하여 적절한 시간으로 이동
                int moveDistance = Mathf.Abs(newPosition.x - previousPosition.x) + Mathf.Abs(newPosition.y - previousPosition.y);
                float totalMoveTime = CalculateMoveTime(monster, moveDistance);

                // Move 애니메이션 시작 (이동 시간에 맞춰)
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Move, null, totalMoveTime);
                }

                // 위치 이동
                SetMonsterGridPosition(monster, newPosition);

                UpdateMonsterPosition(monster, previousPosition);

                // 이동 완료 이벤트 발생
                OnMonsterMoved?.Invoke(monster, newPosition);

                // 이동 완료 후 휴식 시작
                monster.StartMovementRest();

                if (debugMode)
                {
                    var movementPattern = monster.EnemyData?.MovementPattern.ToString() ?? "Unknown";
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} AI 위치 이동: {previousPosition} -> {newPosition} (패턴: {movementPattern}, 거리: {moveDistance}칸, 시간: {totalMoveTime:F2}초)");
                }

                // 계산된 이동 시간만큼 대기
                yield return new WaitForSeconds(totalMoveTime);

                // 이동 완료 후 Idle 애니메이션으로 복귀
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Idle);
                }

                // 이동 후 다음 행동 사이의 딜레이 (0.2초)
                yield return new WaitForSeconds(0.2f);
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
                    // 실행된 패턴 정보 사용 (PatternExecutionResult에서 가져옴)
                    var executedPattern = patternResult.executedPattern;

                    // 차지 패턴의 경우 패턴 스프라이트를 사용하지 않음 (StartChargeEffects에서 처리)
                    if (executedPattern != null && executedPattern.IsChargePattern)
                    {
                        // 차지 패턴은 StartChargeEffects에서 애니메이션 처리하므로 여기서는 아무것도 하지 않음
                        if (debugMode)
                            Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 차지 패턴 애니메이션은 StartChargeEffects에서 처리됨");
                    }
                    else
                    {
                        // 일반 패턴은 Attack 상태로 패턴 애니메이션 실행
                        MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Attack, executedPattern);
                    }
                }

                // 패턴 실행 애니메이션 대기
                if (patternResult.executedPattern == null || !patternResult.executedPattern.IsChargePattern)
                {
                    // 차지 패턴이 아닌 경우에만 애니메이션 대기
                    float patternAnimationDuration = GetPatternAnimationDuration(patternResult.executedPattern);
                    yield return new WaitForSeconds(patternAnimationDuration);
                }
                else
                {
                    // 차지 패턴의 경우 짧은 대기만 (StartChargeEffects 처리 시간)
                    yield return new WaitForSeconds(0.1f);
                }

                // 패턴 완료 후 Idle로 복귀 (차지 패턴은 제외 - StartChargeEffects에서 처리)
                if (MonsterAnimationManager.Instance != null &&
                    (patternResult.executedPattern == null || !patternResult.executedPattern.IsChargePattern))
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Idle);
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

                // Attack 애니메이션 재생 후 완료 대기
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Attack);

                    // 애니메이션 완료까지 대기 (실제 애니메이션 이벤트 기반)
                    yield return StartCoroutine(WaitForAttackAnimationComplete(monster));
                }

                // 중립 오브젝트에게 데미지
                targetObject.TakeDamage(damage, monster.Element);

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

                // 공격 완료 후 Idle 애니메이션으로 복귀 (MonsterAnimationManager에서 자동 처리)
                // MonsterAnimationManager.OnAnimationCompleted에서 Attack 완료 시 자동으로 Idle로 복귀

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

                // Attack 애니메이션 재생 후 완료 대기
                if (MonsterAnimationManager.Instance != null)
                {
                    MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Attack);

                    // 애니메이션 완료까지 대기 (실제 애니메이션 이벤트 기반)
                    yield return StartCoroutine(WaitForAttackAnimationComplete(monster));
                }

                // 플레이어에게 데미지 (공격자 정보 포함)
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.TakeDamage(damage, monster);
                }

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

                // 공격 완료 후 Idle 애니메이션으로 복귀 (MonsterAnimationManager에서 자동 처리)
                // MonsterAnimationManager.OnAnimationCompleted에서 Attack 완료 시 자동으로 Idle로 복귀

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
        /// 몬스터 위치 업데이트 (이동 거리와 속도 고려)
        /// </summary>
        private void UpdateMonsterPosition(Maglin.Enemy.Enemy monster, Vector2Int previousPosition = default)
        {
            UpdateMonsterPosition(monster, previousPosition, null);
        }

        /// <summary>
        /// 몬스터 위치 업데이트 (이동 거리와 속도 고려, 커스텀 시간 지원)
        /// </summary>
        private void UpdateMonsterPosition(Maglin.Enemy.Enemy monster, Vector2Int previousPosition, float? customMoveTime)
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

            // 이동 거리 계산
            Vector2Int startPos = (previousPosition == default) ? gridPos : previousPosition;
            int moveDistance = Mathf.Abs(gridPos.x - startPos.x) + Mathf.Abs(gridPos.y - startPos.y);

            // 이동 시간 계산 (커스텀 시간이 있으면 사용, 없으면 계산)
            float totalMoveTime = customMoveTime ?? CalculateMoveTime(monster, moveDistance);

            if (debugMode)
            {
                float monsterMoveSpeed = monster.EnemyData?.MoveSpeed ?? 1.0f;
                string timeInfo = customMoveTime.HasValue ? " (커스텀 시간 사용)" : "";
                Debug.Log($"[MonsterBattleManager] 이동 계산: {monster.name} - 거리: {moveDistance}칸, 속도: {monsterMoveSpeed}, 시간: {totalMoveTime:F2}초{timeInfo}");
            }

            // GridFieldManager를 통해 위치 업데이트 (X만 변경, Y는 현재 위치 유지)
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                Vector3 targetWorldPos = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(monster.gameObject, gridPos);
                
                // 현재 Y 위치 유지 (물리 시뮬레이션 결과 보존)
                Vector3 currentPos = monster.transform.position;
                Vector3 finalTargetPos = new Vector3(targetWorldPos.x, currentPos.y, targetWorldPos.z);

                // 부드러운 이동으로 변경 (X축만 이동, Y는 물리 유지)
                StartCoroutine(SmoothMoveToPositionXOnly(monster.gameObject, finalTargetPos, totalMoveTime));

                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] 몬스터 이동: X={targetWorldPos.x} (그리드), Y={currentPos.y} (물리 유지)");
            }
            else
            {
                // GridFieldManager가 없는 경우 X만 변경하고 Y는 현재 위치 유지
                Vector3 currentPos = monster.transform.position;
                Vector3 targetPos = new Vector3(gridPos.x + 0.5f, currentPos.y, currentPos.z);
                StartCoroutine(SmoothMoveToPositionXOnly(monster.gameObject, targetPos, totalMoveTime));

                if (debugMode)
                    Debug.LogWarning($"[MonsterBattleManager] 폴백 이동: X={gridPos.x + 0.5f}, Y={currentPos.y} (유지)");
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
        /// 부드러운 몬스터 이동 (X축만, Y축은 물리 유지)
        /// </summary>
        private IEnumerator SmoothMoveToPositionXOnly(GameObject monster, Vector3 targetPosition, float duration)
        {
            if (monster == null) yield break;

            Vector3 startPosition = monster.transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (monster == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // 자연스러운 이동 곡선 적용
                float smoothedProgress = EaseInOutQuad(progress);

                // X축만 보간, Y와 Z는 현재 위치 유지 (물리 시뮬레이션 결과 보존)
                Vector3 currentPos = monster.transform.position;
                float newX = Mathf.Lerp(startPosition.x, targetPosition.x, smoothedProgress);
                monster.transform.position = new Vector3(newX, currentPos.y, currentPos.z);

                yield return null;
            }

            // 최종 X 위치 보장 (Y는 물리에 맡김)
            if (monster != null)
            {
                Vector3 finalPos = monster.transform.position;
                monster.transform.position = new Vector3(targetPosition.x, finalPos.y, finalPos.z);
            }
        }

        /// <summary>
        /// 부드러운 몬스터 이동 (다중 칸 이동 지원)
        /// </summary>
        private IEnumerator SmoothMoveToPosition(GameObject monster, Vector3 targetPosition, float duration)
        {
            if (monster == null) yield break;

            Vector3 startPosition = monster.transform.position;

            // 이동 거리 계산
            float distance = Vector3.Distance(startPosition, targetPosition);
            int tiles = Mathf.RoundToInt(distance);

            if (tiles <= 1)
            {
                // 1칸 이동은 기존 방식 사용
                yield return StartCoroutine(SimpleSmoothMove(monster, startPosition, targetPosition, duration));
            }
            else
            {
                // 다중 칸 이동은 각 칸마다 균등한 시간으로 이동
                yield return StartCoroutine(MultiTileSmoothMove(monster, startPosition, targetPosition, duration, tiles));
            }
        }

        /// <summary>
        /// 1칸 이동용 부드러운 이동
        /// </summary>
        private IEnumerator SimpleSmoothMove(GameObject monster, Vector3 startPosition, Vector3 targetPosition, float duration)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (monster == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // 자연스러운 이동 곡선 적용 (다중 칸 이동과 일관성 유지)
                float smoothedProgress = EaseInOutQuad(progress);

                monster.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothedProgress);

                yield return null;
            }

            // 최종 위치 보장
            if (monster != null)
            {
                monster.transform.position = targetPosition;
            }
        }

        /// <summary>
        /// 다중 칸 이동용 부드러운 이동 (자연스러운 연속 이동)
        /// </summary>
        private IEnumerator MultiTileSmoothMove(GameObject monster, Vector3 startPosition, Vector3 targetPosition, float totalDuration, int tiles)
        {
            float elapsedTime = 0f;

            while (elapsedTime < totalDuration)
            {
                if (monster == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / totalDuration;

                // 자연스러운 이동 곡선 적용 (시작은 빠르게, 끝에 약간 감속)
                float smoothedProgress = EaseInOutQuad(progress);

                // 직선적으로 부드럽게 이동
                monster.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothedProgress);

                yield return null;
            }

            // 최종 위치 보장
            if (monster != null)
            {
                monster.transform.position = targetPosition;
            }
        }

        /// <summary>
        /// 자연스러운 이동을 위한 Ease In-Out Quad 함수
        /// </summary>
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        /// <summary>
        /// 이동 거리와 몬스터 속도에 따른 이동 시간 계산
        /// </summary>
        private float CalculateMoveTime(Maglin.Enemy.Enemy monster, int moveDistance)
        {
            float baseTimePerTile = 0.4f;
            float monsterMoveSpeed = monster.EnemyData?.MoveSpeed ?? 1.0f;

            // 다중 칸 이동 시 시간 보정 (너무 오래 걸리지 않도록)
            float distanceMultiplier = moveDistance > 1 ?
                Mathf.Lerp(1.0f, 0.8f, (moveDistance - 1) / 3.0f) : 1.0f;

            float totalMoveTime = (moveDistance * baseTimePerTile * distanceMultiplier) / monsterMoveSpeed;

            // 최소/최대 이동 시간 보장
            return Mathf.Clamp(totalMoveTime, 0.2f, 2.0f);
        }

        /// <summary>
        /// 이동 거리와 커스텀 속도에 따른 이동 시간 계산 (패턴 전용)
        /// </summary>
        private float CalculateMoveTime(Maglin.Enemy.Enemy monster, int moveDistance, float customMoveSpeed)
        {
            float baseTimePerTile = 0.4f;

            // 다중 칸 이동 시 시간 보정 (너무 오래 걸리지 않도록)
            float distanceMultiplier = moveDistance > 1 ?
                Mathf.Lerp(1.0f, 0.8f, (moveDistance - 1) / 3.0f) : 1.0f;

            float totalMoveTime = (moveDistance * baseTimePerTile * distanceMultiplier) / customMoveSpeed;

            // 최소/최대 이동 시간 보장
            return Mathf.Clamp(totalMoveTime, 0.2f, 2.0f);
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
        /// 공격 애니메이션 지속 시간 계산 (새로운 프레임 레이트 시스템 사용)
        /// </summary>
        private float GetAttackAnimationDuration(Maglin.Enemy.Enemy monster)
        {
            if (monster?.EnemyData?.AttackSprites == null || monster.EnemyData.AttackSprites.Length == 0)
            {
                return 0.5f; // 기본 지속 시간
            }

            // 새로운 프레임 레이트 시스템 사용
            float frameRate = monster.EnemyData.GetFrameRateForState(Maglin.Enemy.MonsterAnimationState.Attack);
            float frameTime = 1f / Mathf.Max(0.1f, frameRate);

            // 전체 애니메이션 지속 시간 = 프레임 수 × 프레임 간격
            return monster.EnemyData.AttackSprites.Length * frameTime;
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
        private float GetPatternAnimationDuration(MonsterPatternSO executedPattern)
        {
            // 실행된 패턴의 애니메이션 정보 사용
            if (executedPattern?.PatternSprites != null && executedPattern.PatternSprites.Length > 0)
            {
                return executedPattern.PatternSprites.Length / executedPattern.PatternFrameRate;
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

            MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Hit);

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
                // 새로운 프레임 레이트 시스템 사용
                float frameRate = monster.EnemyData.GetFrameRateForState(Maglin.Enemy.MonsterAnimationState.Hit);
                float frameTime = 1f / Mathf.Max(0.1f, frameRate);
                float hitAnimationDuration = monster.EnemyData.HitSprites.Length * frameTime;
                yield return new WaitForSeconds(hitAnimationDuration);
            }
            else
            {
                yield return new WaitForSeconds(0.3f); // 기본 지속 시간
            }

            // Idle 애니메이션으로 복귀
            if (monster != null && monster.IsAlive && MonsterAnimationManager.Instance != null)
            {
                MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Idle);
            }
        }

        /// <summary>
        /// 몬스터 Death 애니메이션 재생
        /// </summary>
        public void PlayDeathAnimation(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || MonsterAnimationManager.Instance == null) return;

            MonsterAnimationManager.Instance.SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Death);
        }

        /// <summary>
        /// 공격 애니메이션 완료까지 대기하는 코루틴 (이벤트 기반)
        /// </summary>
        private IEnumerator WaitForAttackAnimationComplete(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || MonsterAnimationManager.Instance == null)
            {
                yield return new WaitForSeconds(0.5f); // 폴백 시간
                yield break;
            }

            bool animationCompleted = false;

            // 애니메이션 완료 이벤트 리스너 등록
            System.Action<Maglin.Enemy.Enemy, Maglin.Enemy.MonsterAnimationState> onAnimationCompleted =
                (animatedMonster, state) =>
                {
                    if (animatedMonster == monster && state == Maglin.Enemy.MonsterAnimationState.Attack)
                    {
                        animationCompleted = true;
                        if (debugMode)
                            Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 공격 애니메이션 완료 감지");
                    }
                };

            // 이벤트 구독
            MonsterAnimationManager.OnMonsterAnimationCompleted += onAnimationCompleted;

            // 애니메이션 완료까지 대기 (최대 5초 타임아웃)
            float timeout = 5f;
            float elapsedTime = 0f;

            while (!animationCompleted && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;
            }

            // 이벤트 구독 해제
            MonsterAnimationManager.OnMonsterAnimationCompleted -= onAnimationCompleted;

            // 타임아웃된 경우 경고
            if (!animationCompleted && debugMode)
            {
                Debug.LogWarning($"[MonsterBattleManager] {monster.EnemyName} 공격 애니메이션 대기 타임아웃 ({timeout}초)");
            }
        }
        #endregion
        #endregion
    }
}
