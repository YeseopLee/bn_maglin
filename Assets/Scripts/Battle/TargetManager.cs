using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Maglin.Enemy;
using Maglin.Core;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투에서 타겟 선택과 관리를 담당하는 매니저
    /// </summary>
    public class TargetManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static TargetManager _instance;

        public static TargetManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TargetManager>();

                    if (_instance == null)
                    {
                        GameObject targetManagerObject = new GameObject("TargetManager");
                        _instance = targetManagerObject.AddComponent<TargetManager>();
                        DontDestroyOnLoad(targetManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 타겟 변경 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy> OnTargetChanged;

        /// <summary>
        /// 타겟 사망 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy> OnTargetDied;
        #endregion

        #region Fields
        [Header("타겟 설정")]
        [SerializeField] private Vector2Int playerGridPosition = new Vector2Int(0, 0);

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 현재 타겟
        private Maglin.Enemy.Enemy currentTarget = null;

        // 스폰된 몬스터들
        private List<GameObject> spawnedMonsters = new List<GameObject>();

        // 최적화를 위한 캐시 변수들
        private Transform cachedTargetMarker = null;
        private Vector3 lastTargetPosition = Vector3.zero;
        private bool markerPositionSet = false;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 타겟
        /// </summary>
        public Maglin.Enemy.Enemy CurrentTarget => currentTarget;

        /// <summary>
        /// 플레이어 그리드 위치
        /// </summary>
        public Vector2Int PlayerGridPosition => playerGridPosition;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTargetManager();
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

        private void Update()
        {
            // 타겟이 없거나 죽었으면 캐시 초기화
            if (currentTarget == null || !currentTarget.IsAlive)
            {
                ResetMarkerCache();
                return;
            }

            // 마커가 있고 활성화되어 있을 때만 위치 체크
            if (cachedTargetMarker != null && cachedTargetMarker.gameObject.activeInHierarchy)
            {
                // 타겟의 위치가 변경되었거나 아직 위치가 설정되지 않았을 때만 업데이트
                Vector3 currentTargetPosition = currentTarget.transform.position;
                if (!markerPositionSet || Vector3.Distance(lastTargetPosition, currentTargetPosition) > 0.01f)
                {
                    UpdateTargetMarkerPosition(cachedTargetMarker, currentTarget);
                    lastTargetPosition = currentTargetPosition;
                    markerPositionSet = true;
                }
            }
            else
            {
                // 마커를 다시 찾아서 캐시
                cachedTargetMarker = currentTarget.transform.Find("TargetMarker");
                if (cachedTargetMarker != null && cachedTargetMarker.gameObject.activeInHierarchy)
                {
                    UpdateTargetMarkerPosition(cachedTargetMarker, currentTarget);
                    lastTargetPosition = currentTarget.transform.position;
                    markerPositionSet = true;
                }
            }
        }

        /// <summary>
        /// 마커 캐시 리셋
        /// </summary>
        private void ResetMarkerCache()
        {
            cachedTargetMarker = null;
            lastTargetPosition = Vector3.zero;
            markerPositionSet = false;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 타겟 매니저 초기화
        /// </summary>
        public void InitializeTargetManager()
        {
            if (debugMode)
                Debug.Log("[TargetManager] 타겟 매니저 초기화 시작");

            // 초기 상태 설정
            currentTarget = null;
            spawnedMonsters.Clear();

            if (debugMode)
                Debug.Log("[TargetManager] 타겟 매니저 초기화 완료");
        }
        #endregion

        #region Target Management
        /// <summary>
        /// 타겟 순환 (Tab 키)
        /// </summary>
        public void CycleTarget()
        {
            var aliveEnemies = GetAliveEnemies();
            if (aliveEnemies.Count == 0) return;

            if (currentTarget == null)
            {
                // 타겟이 없으면 첫 번째(가장 가까운) 몬스터로 설정
                SetTarget(aliveEnemies[0]);
            }
            else
            {
                // 현재 타겟 다음의 몬스터로 변경
                int currentIndex = aliveEnemies.IndexOf(currentTarget);
                if (currentIndex == -1)
                {
                    // 현재 타겟이 리스트에 없으면 첫 번째로 설정
                    SetTarget(aliveEnemies[0]);
                }
                else
                {
                    int nextIndex = (currentIndex + 1) % aliveEnemies.Count;
                    SetTarget(aliveEnemies[nextIndex]);
                }
            }
        }

        /// <summary>
        /// 특정 몬스터를 타겟으로 설정
        /// </summary>
        public void SetTarget(Maglin.Enemy.Enemy enemy)
        {
            // 이전 타겟의 마커 비활성화
            if (currentTarget != null)
            {
                SetTargetMarkerActive(currentTarget, false);
            }

            // 새 타겟 설정
            currentTarget = enemy;

            // 마커 캐시 리셋 (새로운 타겟이므로)
            ResetMarkerCache();

            // 새 타겟의 마커 활성화
            if (currentTarget != null)
            {
                SetTargetMarkerActive(currentTarget, true);

                // 새 타겟의 마커를 캐시하고 즉시 위치 설정
                cachedTargetMarker = currentTarget.transform.Find("TargetMarker");
                if (cachedTargetMarker != null && cachedTargetMarker.gameObject.activeInHierarchy)
                {
                    UpdateTargetMarkerPosition(cachedTargetMarker, currentTarget);
                    lastTargetPosition = currentTarget.transform.position;
                    markerPositionSet = true;
                }
            }

            if (debugMode)
                Debug.Log($"[TargetManager] 타겟 설정: {currentTarget?.EnemyName ?? "없음"}");

            // 이벤트 발생
            OnTargetChanged?.Invoke(currentTarget);
        }

        /// <summary>
        /// 타겟 마커 활성화/비활성화 (프리팹의 기존 UI 사용)
        /// </summary>
        private void SetTargetMarkerActive(Maglin.Enemy.Enemy enemy, bool active)
        {
            if (enemy == null) return;

            Transform targetMarker = enemy.transform.Find("TargetMarker");
            if (targetMarker != null)
            {
                targetMarker.gameObject.SetActive(active);

                // 마커가 활성화될 때 올바른 위치에 있는지 확인
                if (active)
                {
                    UpdateTargetMarkerPosition(targetMarker, enemy);
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[TargetManager] {enemy.EnemyName}에서 TargetMarker를 찾을 수 없습니다. 프리팹에 TargetMarker가 있는지 확인하세요.");
            }
        }

        /// <summary>
        /// 타겟 마커 위치 업데이트 (최적화됨)
        /// </summary>
        private void UpdateTargetMarkerPosition(Transform targetMarker, Maglin.Enemy.Enemy enemy)
        {
            if (targetMarker == null || enemy == null) return;

            // 몬스터의 SpriteRenderer 크기를 고려하여 마커 위치 계산 (캐시 활용)
            float yOffset = 0.7f; // 기본값

            SpriteRenderer monsterRenderer = enemy.GetComponent<SpriteRenderer>();
            if (monsterRenderer != null && monsterRenderer.sprite != null)
            {
                // 스프라이트의 실제 크기를 고려하여 위쪽에 배치
                float spriteHeight = monsterRenderer.bounds.size.y;
                yOffset = spriteHeight * 0.6f; // 스프라이트 위쪽 60% 지점
            }

            // 마커가 몬스터의 자식이므로 localPosition을 사용
            Vector3 newPosition = new Vector3(0, yOffset, 0);
            if (targetMarker.localPosition != newPosition)
            {
                targetMarker.localPosition = newPosition;
            }

            // 스케일이 이미 설정되어 있지 않다면 설정
            Vector3 targetScale = new Vector3(3f, 3f, 1f);
            if (targetMarker.localScale != targetScale)
            {
                targetMarker.localScale = targetScale;
            }

            // 마커가 다른 요소들보다 위에 그려지도록 sorting order 설정 (한 번만)
            SpriteRenderer markerRenderer = targetMarker.GetComponent<SpriteRenderer>();
            if (markerRenderer != null && markerRenderer.sortingOrder != 10)
            {
                markerRenderer.sortingOrder = 10; // 높은 값으로 설정
            }

            // 디버그 로그는 위치가 실제로 변경될 때만 출력
            if (debugMode && targetMarker.localPosition == newPosition)
            {
                Debug.Log($"[TargetManager] {enemy.EnemyName} 타겟 마커 위치 업데이트: 로컬={targetMarker.localPosition}");
            }
        }

        /// <summary>
        /// 살아있는 몬스터 목록 반환 (거리순 정렬)
        /// </summary>
        public List<Maglin.Enemy.Enemy> GetAliveEnemies()
        {
            var enemies = new List<Maglin.Enemy.Enemy>();

            // spawnedMonsters 리스트를 안전하게 순회
            for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
            {
                var monster = spawnedMonsters[i];

                // null이거나 파괴된 몬스터는 리스트에서 제거
                if (monster == null || monster.gameObject == null)
                {
                    spawnedMonsters.RemoveAt(i);
                    continue;
                }

                // 비활성화된 몬스터도 제거
                if (!monster.activeInHierarchy)
                {
                    spawnedMonsters.RemoveAt(i);
                    continue;
                }

                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();

                // Enemy 컴포넌트가 없거나 죽은 적은 제외
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                enemies.Add(enemy);
            }

            // 거리순으로 정렬
            enemies = enemies.OrderBy(e => Vector2.Distance(playerGridPosition, e.GridPosition)).ToList();

            if (debugMode && enemies.Count > 0)
            {
                Debug.Log($"[TargetManager] 살아있는 몬스터들 (거리순):");
                for (int i = 0; i < enemies.Count; i++)
                {
                    var distance = Vector2.Distance(playerGridPosition, enemies[i].GridPosition);
                    Debug.Log($"  {i}: {enemies[i].EnemyName} at {enemies[i].GridPosition} (거리: {distance:F2})");
                }
            }

            return enemies;
        }

        /// <summary>
        /// 타겟 검증 (타겟이 죽었거나 없어진 경우 처리)
        /// </summary>
        public void ValidateTarget()
        {
            // null 체크와 IsAlive 체크를 안전하게 수행
            if (currentTarget == null || currentTarget.gameObject == null || !currentTarget.IsAlive)
            {
                // 타겟이 없거나 죽었으면 null로 설정 후 가장 가까운 몬스터로 재설정
                currentTarget = null;
                SetTargetToClosest();
            }
        }

        /// <summary>
        /// 타겟에게 데미지
        /// </summary>
        public void DamageTarget(int damage)
        {
            // 타겟 검증
            ValidateTarget();

            // currentTarget이 여전히 유효한지 다시 한번 체크
            if (currentTarget != null && currentTarget.gameObject != null && currentTarget.IsAlive)
            {
                // 현재 타겟의 이름을 미리 저장 (파괴되기 전에)
                string targetName = "";
                try
                {
                    targetName = currentTarget.EnemyName;
                }
                catch (System.Exception)
                {
                    targetName = "Unknown";
                }

                currentTarget.TakeDamage(damage);

                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 {targetName}에게 {damage} 데미지");

                // 타겟이 죽었는지 안전하게 확인
                try
                {
                    if (currentTarget != null && currentTarget.gameObject != null && !currentTarget.IsAlive)
                    {
                        if (debugMode)
                            Debug.Log($"[TargetManager] 타겟 {targetName} 사망");

                        // 사망 이벤트 발생 (null 체크 후)
                        var deadTarget = currentTarget;
                        OnTargetDied?.Invoke(deadTarget);

                        // 사망한 타겟을 즉시 null로 설정하여 중복 처리 방지
                        currentTarget = null;

                        // 다음 타겟 설정 (별도 프레임에서 처리)
                        StartCoroutine(DelayedTargetReset());
                    }
                }
                catch (System.Exception ex)
                {
                    if (debugMode)
                        Debug.LogWarning($"[TargetManager] 타겟 사망 처리 중 오류: {ex.Message}");

                    // 오류 발생 시 안전하게 타겟 리셋
                    currentTarget = null;
                    StartCoroutine(DelayedTargetReset());
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[TargetManager] 공격할 타겟이 없습니다.");
            }
        }

        /// <summary>
        /// 지연된 타겟 리셋 (race condition 방지)
        /// </summary>
        private System.Collections.IEnumerator DelayedTargetReset()
        {
            yield return null; // 한 프레임 대기
            SetTargetToClosest();
        }

        /// <summary>
        /// 가장 가까운 몬스터를 타겟으로 설정
        /// </summary>
        public void SetTargetToClosest()
        {
            var aliveEnemies = GetAliveEnemies();
            if (aliveEnemies.Count == 0)
            {
                currentTarget = null;
                return;
            }

            SetTarget(aliveEnemies[0]);

            if (debugMode)
            {
                Debug.Log($"[TargetManager] 플레이어 위치: {playerGridPosition}");
                Debug.Log($"[TargetManager] 가장 가까운 몬스터: {aliveEnemies[0].EnemyName} at {aliveEnemies[0].GridPosition}");
                Debug.Log($"[TargetManager] 거리: {Vector2.Distance(playerGridPosition, aliveEnemies[0].GridPosition)}");
            }
        }

        /// <summary>
        /// 모든 적에게 데미지
        /// </summary>
        public void DamageAllEnemies(int damage)
        {
            var aliveEnemies = spawnedMonsters
                .Where(m => m != null && m.activeInHierarchy)
                .Select(m => m.GetComponent<Maglin.Enemy.Enemy>())
                .Where(e => e != null && e.IsAlive)
                .ToList();

            foreach (var enemy in aliveEnemies)
            {
                enemy.TakeDamage(damage);

                if (debugMode)
                    Debug.Log($"[TargetManager] {enemy.EnemyName}에게 {damage} 데미지");
            }

            if (aliveEnemies.Count == 0 && debugMode)
            {
                Debug.Log("[TargetManager] 공격할 적이 없습니다.");
            }
        }
        #endregion

        #region Monster Management
        /// <summary>
        /// 몬스터 추가
        /// </summary>
        public void AddMonster(GameObject monster)
        {
            if (monster != null && !spawnedMonsters.Contains(monster))
            {
                spawnedMonsters.Add(monster);

                // 몬스터 사망 이벤트 구독
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    enemy.OnDeath += OnMonsterDeath;
                }

                if (debugMode)
                    Debug.Log($"[TargetManager] 몬스터 추가: {enemy?.EnemyName ?? "Unknown"}");
            }
        }

        /// <summary>
        /// 몬스터 제거
        /// </summary>
        public void RemoveMonster(GameObject monster)
        {
            if (monster != null && spawnedMonsters.Contains(monster))
            {
                // 몬스터 사망 이벤트 구독 해제
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    enemy.OnDeath -= OnMonsterDeath;

                    // 현재 타겟이 제거되는 몬스터라면 즉시 새로운 타겟 설정
                    if (currentTarget == enemy)
                    {
                        SetTargetMarkerActive(enemy, false);
                        currentTarget = null;

                        if (debugMode)
                            Debug.Log($"[TargetManager] 타겟 마커 즉시 비활성화: {enemy.EnemyName}");

                        // 즉시 새로운 타겟 설정 (가장 가까운 살아있는 몬스터)
                        var aliveEnemies = GetAliveEnemies();
                        if (aliveEnemies.Count > 0)
                        {
                            SetTarget(aliveEnemies[0]);
                            if (debugMode)
                                Debug.Log($"[TargetManager] 몬스터 제거 후 새로운 타겟 자동 설정: {aliveEnemies[0].EnemyName}");
                        }
                        else
                        {
                            if (debugMode)
                                Debug.Log("[TargetManager] 살아있는 몬스터가 없어 타겟을 설정할 수 없습니다.");
                        }
                    }
                }

                spawnedMonsters.Remove(monster);

                if (debugMode)
                    Debug.Log($"[TargetManager] 몬스터 제거: {enemy?.EnemyName ?? "Unknown"}");
            }
        }

        /// <summary>
        /// 모든 몬스터 정리
        /// </summary>
        public void ClearAllMonsters()
        {
            foreach (var monster in spawnedMonsters)
            {
                if (monster != null)
                {
                    var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        enemy.OnDeath -= OnMonsterDeath;
                    }
                }
            }

            spawnedMonsters.Clear();
            currentTarget = null;

            if (debugMode)
                Debug.Log("[TargetManager] 모든 몬스터 정리 완료");
        }

        /// <summary>
        /// 몬스터 사망 이벤트 처리
        /// </summary>
        private void OnMonsterDeath(Maglin.Enemy.Enemy enemy)
        {
            if (enemy == null) return;

            string enemyName = "";
            try
            {
                enemyName = enemy.EnemyName;
            }
            catch (System.Exception)
            {
                enemyName = "Unknown";
            }

            if (debugMode)
                Debug.Log($"[TargetManager] {enemyName} 사망 처리");

            // 타겟이었던 몬스터가 죽으면 즉시 새로운 타겟 설정
            if (currentTarget == enemy)
            {
                SetTargetMarkerActive(enemy, false);
                currentTarget = null;

                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 사망으로 인한 마커 비활성화: {enemyName}");

                // 별도 프레임에서 새로운 타겟 설정 (race condition 방지)
                StartCoroutine(DelayedTargetSelection());
            }
        }

        /// <summary>
        /// 지연된 타겟 선택 (race condition 방지)
        /// </summary>
        private System.Collections.IEnumerator DelayedTargetSelection()
        {
            yield return null; // 한 프레임 대기

            var aliveEnemies = GetAliveEnemies();
            if (aliveEnemies.Count > 0)
            {
                SetTarget(aliveEnemies[0]);
                if (debugMode)
                    Debug.Log($"[TargetManager] 새로운 타겟 자동 설정: {aliveEnemies[0].EnemyName}");
            }
            else
            {
                if (debugMode)
                    Debug.Log("[TargetManager] 살아있는 몬스터가 없어 타겟을 설정할 수 없습니다.");
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
                Debug.Log($"[TargetManager] 플레이어 그리드 위치: {gridPosition}");
        }

        /// <summary>
        /// 현재 타겟이 유효한지 확인
        /// </summary>
        public bool IsTargetValid()
        {
            return currentTarget != null && currentTarget.IsAlive;
        }

        /// <summary>
        /// 타겟 초기화
        /// </summary>
        public void ResetTarget()
        {
            if (currentTarget != null)
            {
                SetTargetMarkerActive(currentTarget, false);
            }
            currentTarget = null;
        }
        #endregion
    }
}