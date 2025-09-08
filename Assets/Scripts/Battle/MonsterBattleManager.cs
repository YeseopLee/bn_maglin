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

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 현재 다른 몬스터들의 위치 수집 (자기 자신 제외)
            var occupiedPositions = GetOccupiedPositions(monster);

            // AI가 충돌 고려한 이동 결정
            var newPosition = ai.DecideMovementWithCollision(playerGridPosition, occupiedPositions);

            if (newPosition != monster.GridPosition)
            {
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
        /// 몬스터 공격 처리 (플레이어 및 중립 오브젝트)
        /// </summary>
        public IEnumerator ProcessMonsterAttack(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive) yield break;
            if (monster.IsNeutralObject) yield break; // 중립 오브젝트는 공격하지 않음

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 1. 먼저 공격해야 할 중립 오브젝트가 있는지 확인
            var targetObject = FindAttackableNeutralObject(monster);
            if (targetObject != null)
            {
                var damage = monster.CurrentAttackDamage;

                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName}이 중립 오브젝트 {targetObject.EnemyName}를 공격! 데미지: {damage}");

                // 오브젝트 공격 애니메이션
                yield return StartCoroutine(PerformObjectAttack(monster, targetObject.GridPosition));

                // 중립 오브젝트에게 데미지
                targetObject.TakeDamage(damage, monster.Element);

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

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

                // 공격 타입에 따라 다른 애니메이션 실행
                if (monster.EnemyData.AttackPattern == Maglin.Enemy.AttackPatternType.Melee)
                {
                    // 근접 공격: 돌격 애니메이션
                    yield return StartCoroutine(PerformChargeAttack(monster, playerGridPosition));
                }
                else if (monster.EnemyData.AttackPattern == Maglin.Enemy.AttackPatternType.Ranged)
                {
                    // 원거리 공격: 제자리에서 살짝 움직이는 애니메이션
                    yield return StartCoroutine(PerformRangedAttack(monster, playerGridPosition));
                }
                else
                {
                    // 특수 공격: 기본 애니메이션
                    yield return StartCoroutine(PerformSpecialAttack(monster, playerGridPosition));
                }

                // 플레이어에게 데미지 (공격자 정보 포함)
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.TakeDamage(damage, monster);
                }

                // 공격 완료 이벤트 발생
                OnMonsterAttacked?.Invoke(monster);

                // 공격 후 잠시 대기
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 공격 범위 밖");
            }
        }
        #endregion

        #region Private Methods
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

        #region Attack Animations
        /// <summary>
        /// 돌격 공격 애니메이션 실행
        /// </summary>
        private IEnumerator PerformChargeAttack(Maglin.Enemy.Enemy monster, Vector2Int targetPosition)
        {
            if (monster == null) yield break;

            Vector3 originalPosition = monster.transform.position;
            Vector3 targetWorldPos = GridFieldManager.Instance != null
                ? GridFieldManager.Instance.GridToWorldPosition(targetPosition)
                : new Vector3(targetPosition.x + 0.5f, targetPosition.y + 0.5f, 0f);

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 돌격 공격 시작: {originalPosition} -> {targetWorldPos}");

            // 1. 뒤로 물러나기 (준비 자세)
            Vector3 backPosition = originalPosition + Vector3.right * 0.3f; // 뒤로 0.3f 이동
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, backPosition, 0.1f));

            // 2. 잠시 대기 (돌격 준비)
            yield return new WaitForSeconds(0.1f);

            // 3. 플레이어에게 돌격
            Vector3 chargeTarget = new Vector3(targetWorldPos.x, targetWorldPos.y, -1f);
            yield return StartCoroutine(SmoothChargeToPosition(monster.gameObject, chargeTarget, 0.1f));

            // 4. 공격 효과 (약간의 흔들림)
            yield return StartCoroutine(ShakeEffect(monster.gameObject, 0.2f, 0.15f));

            // 5. 원래 위치로 돌아가기
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, originalPosition, 0.2f));

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 돌격 공격 완료");
        }

        /// <summary>
        /// 빠른 돌격 이동 (돌격용)
        /// </summary>
        private IEnumerator SmoothChargeToPosition(GameObject monster, Vector3 targetPosition, float duration)
        {
            if (monster == null) yield break;

            Vector3 startPosition = monster.transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (monster == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // 돌격용 Ease In 효과 (빠르게 시작해서 빠르게 끝남)
                progress = progress * progress;

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
        /// 원거리 공격 애니메이션 실행
        /// </summary>
        private IEnumerator PerformRangedAttack(Maglin.Enemy.Enemy monster, Vector2Int targetPosition)
        {
            if (monster == null) yield break;

            Vector3 originalPosition = monster.transform.position;
            Vector3 targetWorldPos = GridFieldManager.Instance != null
                ? GridFieldManager.Instance.GridToWorldPosition(targetPosition)
                : new Vector3(targetPosition.x + 0.5f, targetPosition.y + 0.5f, 0f);

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 원거리 공격 시작: {originalPosition}");

            // 1. 플레이어 방향으로 더 많이 이동 (준비 자세)
            Vector3 leanPosition = originalPosition + Vector3.left * 0.15f; // 플레이어 방향으로 0.15f 이동
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, leanPosition, 0.2f));

            // 2. 잠시 대기 (공격 준비)
            yield return new WaitForSeconds(0.15f);

            // 3. 공격 효과 (약간의 흔들림, duration: 0.15f, intensity: 0.1f)
            yield return StartCoroutine(ShakeEffect(monster.gameObject, 0.15f, 0.1f));

            // 4. 원래 위치로 돌아가기
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, originalPosition, 0.2f));

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 원거리 공격 완료");
        }

        /// <summary>
        /// 특수 공격 애니메이션 실행
        /// </summary>
        private IEnumerator PerformSpecialAttack(Maglin.Enemy.Enemy monster, Vector2Int targetPosition)
        {
            if (monster == null) yield break;

            Vector3 originalPosition = monster.transform.position;

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 특수 공격 시작");

            // 1. 위로 살짝 올라가기 (특수 공격 준비)
            Vector3 upPosition = originalPosition + Vector3.up * 0.3f;
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, upPosition, 0.2f));

            // 2. 잠시 대기 (특수 효과 준비)
            yield return new WaitForSeconds(0.2f);

            // 3. 강한 흔들림 효과 (특수 공격 임팩트)
            yield return StartCoroutine(ShakeEffect(monster.gameObject, 0.3f, 0.15f));

            // 4. 원래 위치로 돌아가기
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, originalPosition, 0.3f));

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 특수 공격 완료");
        }

        /// <summary>
        /// 오브젝트 공격 애니메이션 실행
        /// </summary>
        private IEnumerator PerformObjectAttack(Maglin.Enemy.Enemy monster, Vector2Int targetPosition)
        {
            if (monster == null) yield break;

            Vector3 originalPosition = monster.transform.position;
            Vector3 targetWorldPos = GridFieldManager.Instance != null
                ? GridFieldManager.Instance.GridToWorldPosition(targetPosition)
                : new Vector3(targetPosition.x + 0.5f, targetPosition.y + 0.5f, 0f);

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 오브젝트 공격 시작: {originalPosition} -> {targetWorldPos}");

            // 1. 오브젝트 방향으로 살짝 이동 (준비 자세)
            Vector3 attackPosition = originalPosition + Vector3.left * 0.2f; // 오브젝트 방향으로 0.2f 이동
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, attackPosition, 0.15f));

            // 2. 잠시 대기 (공격 준비)
            yield return new WaitForSeconds(0.1f);

            // 3. 공격 효과 (약간의 흔들림)
            yield return StartCoroutine(ShakeEffect(monster.gameObject, 0.2f, 0.12f));

            // 4. 원래 위치로 돌아가기
            yield return StartCoroutine(SmoothMoveToPosition(monster.gameObject, originalPosition, 0.2f));

            if (debugMode)
                Debug.Log($"[MonsterBattleManager] {monster.EnemyName} 오브젝트 공격 완료");
        }

        /// <summary>
        /// 흔들림 효과
        /// </summary>
        private IEnumerator ShakeEffect(GameObject target, float duration, float intensity)
        {
            if (target == null) yield break;

            Vector3 originalPosition = target.transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (target == null) yield break;

                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // 흔들림 강도가 시간이 지날수록 줄어듦
                float currentIntensity = intensity * (1f - progress);

                // 랜덤한 방향으로 흔들림
                Vector3 shakeOffset = new Vector3(
                    Random.Range(-currentIntensity, currentIntensity),
                    Random.Range(-currentIntensity, currentIntensity),
                    0f
                );

                target.transform.position = originalPosition + shakeOffset;

                yield return null;
            }

            // 원래 위치로 복원
            if (target != null)
            {
                target.transform.position = originalPosition;
            }
        }
        #endregion
        #endregion
    }
}
