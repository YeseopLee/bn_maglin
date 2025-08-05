using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using Maglin.Cards;

namespace Maglin.Enemy
{
    /// <summary>
    /// AI 행동 결정 결과
    /// </summary>
    public struct AIActionResult
    {
        public bool shouldAct;
        public Vector2Int targetPosition;
        public AIActionType actionType;
        public string description;

        public AIActionResult(bool act, Vector2Int target, AIActionType type, string desc = "")
        {
            shouldAct = act;
            targetPosition = target;
            actionType = type;
            description = desc;
        }
    }

    /// <summary>
    /// AI 행동 타입
    /// </summary>
    public enum AIActionType
    {
        None,       // 행동 없음
        Move,       // 이동
        Attack,     // 공격
        Special     // 특수 행동
    }

    /// <summary>
    /// 몬스터의 AI 로직을 담당하는 클래스
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        #region Events
        /// <summary>
        /// AI 행동 결정 이벤트 (몬스터, 행동 결과)
        /// </summary>
        public static event Action<Enemy, AIActionResult> OnAIActionDecided;
        #endregion

        #region Fields
        [Header("AI 설정")]
        [SerializeField] private float thinkingDelay = 0.5f;  // AI 사고 시간
        [SerializeField] private bool enablePathfinding = true;  // 경로 탐색 활성화
        [SerializeField] private int maxPathfindingSteps = 10;  // 최대 경로 탐색 단계

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool showAIThoughts = false;

        // 참조
        private Enemy enemy;
        private EnemySO enemyData;

        // AI 상태
        private bool isThinking = false;
        private Vector2Int lastKnownPlayerPosition = Vector2Int.zero;
        private List<Vector2Int> plannedPath = new List<Vector2Int>();
        private int pathIndex = 0;

        // 전투 필드 정보 (10x4 그리드)
        private const int FIELD_WIDTH = 10;
        private const int FIELD_HEIGHT = 4;
        private const int PLAYER_START_X = 0;  // 플레이어 시작 위치 X
        #endregion

        #region Properties
        /// <summary>
        /// AI가 사고 중인지 여부
        /// </summary>
        public bool IsThinking => isThinking;

        /// <summary>
        /// 현재 계획된 경로
        /// </summary>
        public List<Vector2Int> PlannedPath => new List<Vector2Int>(plannedPath);
        #endregion

        #region Unity Events
        private void Awake()
        {
            enemy = GetComponent<Enemy>();
        }

        private void Start()
        {
            if (enemy != null && enemy.EnemyData != null)
            {
                enemyData = enemy.EnemyData;
                Initialize();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// AI 초기화
        /// </summary>
        public void Initialize()
        {
            if (enemy == null || enemyData == null)
            {
                Debug.LogError("[EnemyAI] Enemy 또는 EnemyData가 null입니다.");
                return;
            }

            plannedPath.Clear();
            pathIndex = 0;
            lastKnownPlayerPosition = Vector2Int.zero;

            if (debugMode)
                Debug.Log($"[EnemyAI] {enemy.EnemyName} AI 초기화 완료");
        }

        /// <summary>
        /// AI 턴 실행
        /// </summary>
        public void ExecuteTurn(Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            if (enemy == null || !enemy.IsAlive || enemy.IsStunned || enemy.HasActedThisTurn)
            {
                if (debugMode)
                    Debug.Log($"[EnemyAI] {enemy?.EnemyName} 행동 불가 - 살아있음:{enemy?.IsAlive}, 기절:{enemy?.IsStunned}, 행동완료:{enemy?.HasActedThisTurn}");
                return;
            }

            if (isThinking) return;

            StartCoroutine(ThinkAndAct(playerPosition, allEnemies));
        }

        /// <summary>
        /// 강제로 AI 행동 중단
        /// </summary>
        public void StopAI()
        {
            StopAllCoroutines();
            isThinking = false;
        }
        #endregion

        #region Private Methods - AI Core
        /// <summary>
        /// AI 사고 및 행동 실행
        /// </summary>
        private System.Collections.IEnumerator ThinkAndAct(Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            isThinking = true;
            lastKnownPlayerPosition = playerPosition;

            if (showAIThoughts)
                Debug.Log($"[EnemyAI] {enemy.EnemyName} 사고 시작...");

            yield return new WaitForSeconds(thinkingDelay);

            // AI 행동 결정
            AIActionResult decision = DecideAction(playerPosition, allEnemies);

            if (showAIThoughts)
                Debug.Log($"[EnemyAI] {enemy.EnemyName} 결정: {decision.actionType} - {decision.description}");

            // 행동 실행
            if (decision.shouldAct)
            {
                ExecuteAction(decision);
            }

            OnAIActionDecided?.Invoke(enemy, decision);
            isThinking = false;
        }

        /// <summary>
        /// AI 행동 결정
        /// </summary>
        private AIActionResult DecideAction(Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            // 1. 공격 가능한지 확인
            if (CanAttackPlayer(playerPosition))
            {
                return new AIActionResult(true, playerPosition, AIActionType.Attack,
                    $"플레이어 공격 (거리: {GetDistanceToPlayer(playerPosition)})");
            }

            // 2. 이동 결정
            Vector2Int moveTarget = DecideMovement(playerPosition, allEnemies);
            if (moveTarget != enemy.GridPosition)
            {
                return new AIActionResult(true, moveTarget, AIActionType.Move,
                    $"이동: {enemy.GridPosition} -> {moveTarget}");
            }

            // 3. 행동 없음
            return new AIActionResult(false, enemy.GridPosition, AIActionType.None, "대기");
        }

        /// <summary>
        /// 행동 실행
        /// </summary>
        private void ExecuteAction(AIActionResult action)
        {
            switch (action.actionType)
            {
                case AIActionType.Move:
                    ExecuteMovement(action.targetPosition);
                    break;
                case AIActionType.Attack:
                    ExecuteAttack(action.targetPosition);
                    break;
                case AIActionType.Special:
                    ExecuteSpecialAction(action.targetPosition);
                    break;
            }

            enemy.MarkActionComplete();
        }
        #endregion

        #region Private Methods - Movement
        /// <summary>
        /// 이동 결정
        /// </summary>
        private Vector2Int DecideMovement(Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            Vector2Int currentPos = enemy.GridPosition;

            switch (enemyData.MovementPattern)
            {
                case MovementPatternType.Forward:
                    return MoveForward(currentPos, 1, allEnemies);

                case MovementPatternType.ForwardTwo:
                    return MoveForward(currentPos, 2, allEnemies);

                case MovementPatternType.Jump:
                    return MoveJumpToPlayer(currentPos, playerPosition, allEnemies);

                case MovementPatternType.Stay:
                default:
                    return currentPos;  // 이동하지 않음
            }
        }

        /// <summary>
        /// 앞으로 이동 (왼쪽으로)
        /// </summary>
        private Vector2Int MoveForward(Vector2Int currentPos, int steps, List<Enemy> allEnemies)
        {
            Vector2Int targetPos = currentPos;

            for (int i = 0; i < steps; i++)
            {
                Vector2Int nextPos = new Vector2Int(targetPos.x - 1, targetPos.y);

                // 경계 확인
                if (nextPos.x < 0) break;

                // 충돌 확인
                if (IsPositionOccupied(nextPos, allEnemies)) break;

                targetPos = nextPos;
            }

            return targetPos;
        }

        /// <summary>
        /// 플레이어 근처로 점프 이동
        /// </summary>
        private Vector2Int MoveJumpToPlayer(Vector2Int currentPos, Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            // 플레이어 주변의 빈 자리 찾기
            List<Vector2Int> candidatePositions = new List<Vector2Int>();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;  // 플레이어 위치 제외

                    Vector2Int candidatePos = playerPosition + new Vector2Int(dx, dy);

                    // 경계 확인
                    if (!IsValidPosition(candidatePos)) continue;

                    // 충돌 확인
                    if (IsPositionOccupied(candidatePos, allEnemies)) continue;

                    candidatePositions.Add(candidatePos);
                }
            }

            // 가장 가까운 위치 선택
            if (candidatePositions.Count > 0)
            {
                candidatePositions.Sort((a, b) =>
                    Vector2Int.Distance(currentPos, a).CompareTo(Vector2Int.Distance(currentPos, b)));
                return candidatePositions[0];
            }

            return currentPos;  // 이동할 수 없음
        }

        /// <summary>
        /// 이동 실행
        /// </summary>
        private void ExecuteMovement(Vector2Int targetPosition)
        {
            if (targetPosition != enemy.GridPosition)
            {
                enemy.SetState(EnemyState.Moving);
                enemy.SetPosition(targetPosition);

                if (debugMode)
                    Debug.Log($"[EnemyAI] {enemy.EnemyName} 이동 완료: {targetPosition}");

                enemy.SetState(EnemyState.Idle);
            }
        }
        #endregion

        #region Private Methods - Combat
        /// <summary>
        /// 플레이어 공격 가능 여부 확인
        /// </summary>
        private bool CanAttackPlayer(Vector2Int playerPosition)
        {
            if (!enemy.CanAttack(playerPosition)) return false;

            int distance = GetDistanceToPlayer(playerPosition);

            switch (enemyData.AttackPattern)
            {
                case AttackPatternType.Melee:
                    return distance <= 1;  // 인접한 칸

                case AttackPatternType.Ranged:
                    return distance <= enemyData.AttackRange;

                case AttackPatternType.Special:
                    return CanUseSpecialAttack(playerPosition);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 특수 공격 사용 가능 여부
        /// </summary>
        private bool CanUseSpecialAttack(Vector2Int playerPosition)
        {
            // 보스 몬스터의 특수 공격 로직
            if (enemyData.IsBoss)
            {
                // 예: 3턴마다 특수 공격 등
                return true;
            }

            return false;
        }

        /// <summary>
        /// 공격 실행
        /// </summary>
        private void ExecuteAttack(Vector2Int targetPosition)
        {
            enemy.SetState(EnemyState.Attacking);

            int damage = enemy.CurrentAttackDamage;

            if (debugMode)
                Debug.Log($"[EnemyAI] {enemy.EnemyName}이 플레이어를 {damage} 데미지로 공격!");

            // 실제 플레이어 데미지 처리는 BattleManager에서 수행
            // 여기서는 공격 의도만 전달

            enemy.SetState(EnemyState.Idle);
        }

        /// <summary>
        /// 특수 행동 실행
        /// </summary>
        private void ExecuteSpecialAction(Vector2Int targetPosition)
        {
            if (debugMode)
                Debug.Log($"[EnemyAI] {enemy.EnemyName} 특수 행동 실행");

            // 보스 기믹 등 특수 행동 구현
            enemy.SetState(EnemyState.Idle);
        }
        #endregion

        #region Private Methods - Utility
        /// <summary>
        /// 플레이어와의 거리 계산 (맨하탄 거리)
        /// </summary>
        private int GetDistanceToPlayer(Vector2Int playerPosition)
        {
            return Mathf.Abs(enemy.GridPosition.x - playerPosition.x) +
                   Mathf.Abs(enemy.GridPosition.y - playerPosition.y);
        }

        /// <summary>
        /// 위치가 유효한지 확인
        /// </summary>
        private bool IsValidPosition(Vector2Int position)
        {
            return position.x >= 0 && position.x < FIELD_WIDTH &&
                   position.y >= 0 && position.y < FIELD_HEIGHT;
        }

        /// <summary>
        /// 위치에 다른 몬스터가 있는지 확인
        /// </summary>
        private bool IsPositionOccupied(Vector2Int position, List<Enemy> allEnemies)
        {
            foreach (var otherEnemy in allEnemies)
            {
                if (otherEnemy == enemy || !otherEnemy.IsAlive) continue;

                // 몬스터 크기 고려
                Vector2Int otherPos = otherEnemy.GridPosition;
                int otherSize = otherEnemy.SizeInTiles;

                for (int i = 0; i < otherSize; i++)
                {
                    if (otherPos.x + i == position.x && otherPos.y == position.y)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 두 위치 사이에 장애물이 있는지 확인
        /// </summary>
        private bool HasLineOfSight(Vector2Int from, Vector2Int to, List<Enemy> allEnemies)
        {
            // 간단한 직선 경로 확인
            Vector2Int direction = new Vector2Int(
                Mathf.Clamp(to.x - from.x, -1, 1),
                Mathf.Clamp(to.y - from.y, -1, 1)
            );

            Vector2Int current = from + direction;

            while (current != to)
            {
                if (IsPositionOccupied(current, allEnemies))
                    return false;

                current += direction;
            }

            return true;
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// AI 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug AI Status")]
        public void DebugAIStatus()
        {
            Debug.Log($"=== {enemy?.EnemyName} AI 상태 ===");
            Debug.Log($"사고 중: {isThinking}");
            Debug.Log($"이동 패턴: {enemyData?.MovementPattern}");
            Debug.Log($"공격 패턴: {enemyData?.AttackPattern}");
            Debug.Log($"마지막 플레이어 위치: {lastKnownPlayerPosition}");
            Debug.Log($"계획된 경로: {string.Join(" -> ", plannedPath)}");
        }

        /// <summary>
        /// 현재 상황 분석 출력
        /// </summary>
        public void DebugSituation(Vector2Int playerPosition, List<Enemy> allEnemies)
        {
            Debug.Log($"=== {enemy.EnemyName} 상황 분석 ===");
            Debug.Log($"몬스터 위치: {enemy.GridPosition}");
            Debug.Log($"플레이어 위치: {playerPosition}");
            Debug.Log($"플레이어와 거리: {GetDistanceToPlayer(playerPosition)}");
            Debug.Log($"공격 가능: {CanAttackPlayer(playerPosition)}");
            Debug.Log($"이동 가능: {enemy.CanMove()}");
            Debug.Log($"다른 몬스터 수: {allEnemies.Count(e => e != enemy && e.IsAlive)}");
        }

        /// <summary>
        /// 이동 결정 (BattleTestController용 간단 버전 - 충돌 체크 없음)
        /// </summary>
        public Vector2Int DecideMovement(Vector2Int playerPosition)
        {
            if (enemy == null || !enemy.CanMove() || enemy.EnemyData == null)
                return enemy?.GridPosition ?? Vector2Int.zero;

            var currentPos = enemy.GridPosition;
            var movementPattern = enemy.EnemyData.MovementPattern;

            // 충돌 체크 없이 간단한 이동 (하위 호환용)
            switch (movementPattern)
            {
                case MovementPatternType.Forward:
                    return MoveForwardSimple(currentPos, playerPosition, 1);

                case MovementPatternType.ForwardTwo:
                    return MoveForwardSimple(currentPos, playerPosition, 2);

                case MovementPatternType.ForwardThree:
                    return MoveForwardSimple(currentPos, playerPosition, 3);

                case MovementPatternType.Jump:
                    return MoveJumpSimple(currentPos, playerPosition);

                case MovementPatternType.Stay:
                default:
                    return currentPos;
            }
        }

        /// <summary>
        /// 간단한 앞으로 이동 (충돌 체크 없음)
        /// </summary>
        private Vector2Int MoveForwardSimple(Vector2Int currentPos, Vector2Int playerPosition, int moveDistance)
        {
            if (currentPos.x > playerPosition.x + 1)
            {
                int targetX = Mathf.Max(1, currentPos.x - moveDistance);
                return new Vector2Int(targetX, currentPos.y);
            }
            return currentPos;
        }

        /// <summary>
        /// 간단한 점프 이동 (충돌 체크 없음)
        /// </summary>
        private Vector2Int MoveJumpSimple(Vector2Int currentPos, Vector2Int playerPosition)
        {
            return new Vector2Int(playerPosition.x + 1, playerPosition.y);
        }

        /// <summary>
        /// 다른 몬스터들과 겹치지 않는 이동 결정 (BattleTestController용)
        /// </summary>
        public Vector2Int DecideMovementWithCollision(Vector2Int playerPosition, List<Vector2Int> occupiedPositions)
        {
            if (enemy == null || !enemy.CanMove() || enemy.EnemyData == null)
                return enemy?.GridPosition ?? Vector2Int.zero;

            var currentPos = enemy.GridPosition;
            var movementPattern = enemy.EnemyData.MovementPattern;

            // 이동 패턴에 따른 처리
            switch (movementPattern)
            {
                case MovementPatternType.Forward:
                    return MoveForward(currentPos, playerPosition, occupiedPositions, 1);

                case MovementPatternType.ForwardTwo:
                    return MoveForward(currentPos, playerPosition, occupiedPositions, 2);

                case MovementPatternType.ForwardThree:
                    return MoveForward(currentPos, playerPosition, occupiedPositions, 3);

                case MovementPatternType.Jump:
                    return MoveJump(currentPos, playerPosition, occupiedPositions);

                case MovementPatternType.Stay:
                default:
                    return currentPos; // 제자리
            }
        }

        /// <summary>
        /// 앞으로 이동 (1칸, 2칸, 3칸)
        /// </summary>
        private Vector2Int MoveForward(Vector2Int currentPos, Vector2Int playerPosition, List<Vector2Int> occupiedPositions, int moveDistance)
        {
            // 플레이어 쪽으로 moveDistance 칸 이동, 단 1번 슬롯까지만
            if (currentPos.x > playerPosition.x + 1)
            {
                // 목표 위치 계산 (1번 슬롯을 넘지 않도록)
                int targetX = Mathf.Max(1, currentPos.x - moveDistance);
                var targetPos = new Vector2Int(targetX, currentPos.y);

                // 겹치지 않는 위치 찾기 (목표에서 역순으로)
                for (int x = targetX; x < currentPos.x; x++)
                {
                    var candidatePos = new Vector2Int(x, currentPos.y);
                    if (!occupiedPositions.Contains(candidatePos))
                    {
                        return candidatePos;
                    }
                }
            }

            return currentPos; // 이동할 수 없는 경우
        }

        /// <summary>
        /// 점프 이동 (플레이어 바로 옆칸으로)
        /// </summary>
        private Vector2Int MoveJump(Vector2Int currentPos, Vector2Int playerPosition, List<Vector2Int> occupiedPositions)
        {
            // 플레이어 바로 옆 위치 (1번 슬롯)
            var jumpTarget = new Vector2Int(playerPosition.x + 1, playerPosition.y);

            // 점프 목표 위치가 비어있으면 점프
            if (!occupiedPositions.Contains(jumpTarget))
            {
                return jumpTarget;
            }

            // 목표가 막혀있으면 가능한 가까운 위치 찾기
            for (int x = playerPosition.x + 1; x < 10; x++) // 1번부터 9번 슬롯까지
            {
                var candidatePos = new Vector2Int(x, playerPosition.y);
                if (!occupiedPositions.Contains(candidatePos))
                {
                    return candidatePos;
                }
            }

            return currentPos; // 점프할 수 없는 경우
        }

        /// <summary>
        /// 공격 가능 여부 확인 (BattleTestController용)
        /// </summary>
        public bool CanAttackPosition(Vector2Int playerPosition)
        {
            if (enemy == null || !enemy.IsAlive) return false;

            var distance = Mathf.Abs(enemy.GridPosition.x - playerPosition.x) +
              Mathf.Abs(enemy.GridPosition.y - playerPosition.y);

            return distance <= (enemy.EnemyData?.AttackRange ?? 1);
        }
        #endregion
    }
}