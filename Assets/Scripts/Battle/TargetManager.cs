using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Maglin.Enemy;
using Maglin.Core;
using Maglin.Cards;

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
        [SerializeField] private bool debugMode = false;

        [Header("전투 상태")]
        [SerializeField] private bool isBattleReady = false; // 전투 준비 완료 여부

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

        /// <summary>
        /// 전투 준비 완료 여부
        /// </summary>
        public bool IsBattleReady => isBattleReady;
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
            // 전투 준비가 완료되지 않은 경우 마커 처리 안함
            if (!isBattleReady)
            {
                return;
            }

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
            isBattleReady = false; // 전투 준비 상태 초기화

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

            // 새 타겟의 마커 활성화 (전투 준비가 완료된 경우에만)
            if (currentTarget != null && isBattleReady)
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
                Debug.Log($"[TargetManager] 타겟 설정: {currentTarget?.EnemyName ?? "없음"} (전투 준비: {isBattleReady})");

            // 이벤트 발생
            OnTargetChanged?.Invoke(currentTarget);
        }

        /// <summary>
        /// 타겟 마커 활성화/비활성화 (애니메이션 지원)
        /// </summary>
        private void SetTargetMarkerActive(Maglin.Enemy.Enemy enemy, bool active)
        {
            if (enemy == null) return;

            Transform targetMarker = enemy.transform.Find("TargetMarker");
            if (targetMarker != null)
            {
                targetMarker.gameObject.SetActive(active);

                // 마커가 활성화될 때 올바른 위치에 있는지 확인하고 애니메이션 시작
                if (active)
                {
                    UpdateTargetMarkerPosition(targetMarker, enemy);
                    StartTargetMarkerAnimation(targetMarker);
                }
                else
                {
                    StopTargetMarkerAnimation(targetMarker);
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
            float yOffset = 0.0f; // 기본값

            SpriteRenderer monsterRenderer = enemy.GetComponent<SpriteRenderer>();
            if (monsterRenderer != null && monsterRenderer.sprite != null)
            {
                // 스프라이트의 실제 크기를 고려하여 위쪽에 배치
                // float spriteHeight = monsterRenderer.bounds.size.y;
                // yOffset = spriteHeight * 0.6f; // 스프라이트 위쪽 60% 지점
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
            // if (debugMode && targetMarker.localPosition == newPosition)
            // {
            //     Debug.Log($"[TargetManager] {enemy.EnemyName} 타겟 마커 위치 업데이트: 로컬={targetMarker.localPosition}");
            // }
        }

        /// <summary>
        /// 타겟 마커 애니메이션 시작
        /// </summary>
        private void StartTargetMarkerAnimation(Transform targetMarker)
        {
            if (targetMarker == null) return;

            TargetMarkerAnimator markerAnimator = targetMarker.GetComponent<TargetMarkerAnimator>();
            if (markerAnimator != null)
            {
                markerAnimator.StartAnimation();

                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 마커 애니메이션 시작: {targetMarker.name}");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[TargetManager] {targetMarker.name}에서 TargetMarkerAnimator를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 타겟 마커 애니메이션 중지
        /// </summary>
        private void StopTargetMarkerAnimation(Transform targetMarker)
        {
            if (targetMarker == null) return;

            TargetMarkerAnimator markerAnimator = targetMarker.GetComponent<TargetMarkerAnimator>();
            if (markerAnimator != null)
            {
                markerAnimator.StopAnimation();

                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 마커 애니메이션 중지: {targetMarker.name}");
            }
        }

        /// <summary>
        /// 타겟 마커 애니메이션 프레임 설정 (런타임에서 스프라이트 배열 설정)
        /// </summary>
        public void SetTargetMarkerAnimationFrames(Sprite[] frames, float frameRate = 10f)
        {
            foreach (var monsterObj in spawnedMonsters)
            {
                if (monsterObj != null)
                {
                    Transform targetMarker = monsterObj.transform.Find("TargetMarker");
                    if (targetMarker != null)
                    {
                        TargetMarkerAnimator markerAnimator = targetMarker.GetComponent<TargetMarkerAnimator>();
                        if (markerAnimator != null)
                        {
                            markerAnimator.SetAnimationFrames(frames);
                            markerAnimator.SetFrameRate(frameRate);

                            if (debugMode)
                                Debug.Log($"[TargetManager] {monsterObj.name} 타겟 마커 애니메이션 프레임 설정: {frames?.Length ?? 0}개");
                        }
                    }
                }
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

            if (debugMode)
                Debug.Log($"[TargetManager] SetTargetToClosest 호출 - 살아있는 적 수: {aliveEnemies.Count}");

            if (aliveEnemies.Count == 0)
            {
                currentTarget = null;
                if (debugMode)
                    Debug.Log("[TargetManager] 살아있는 적이 없어 타겟을 null로 설정");
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



        /// <summary>
        /// 앞에서부터 뒤로 hits회 연속 공격 (앞이 죽으면 남은 횟수는 뒤에 몹으로)
        /// </summary>
        public void ChainHitsFromFront(int hits, int damage)
        {
            if (hits <= 0 || damage <= 0) return;

            int px = playerGridPosition.x;
            // 항상 가장 앞의 적부터 시작
            var frontList = GetAliveEnemies()
                .Where(e => e.GridPosition.x > px)
                .OrderBy(e => e.GridPosition.x - px)
                .ToList();
            if (frontList.Count == 0) return;

            var current = frontList[0];

            for (int h = 0; h < hits; h++)
            {
                if (current == null || !current.IsAlive)
                {
                    // 앞의 다음 적으로 이동
                    current = GetAliveEnemies()
                        .Where(e => e.GridPosition.x > px)
                        .OrderBy(e => e.GridPosition.x - px)
                        .FirstOrDefault();
                    if (current == null) break;
                }

                string name = "";
                try { name = current.EnemyName; } catch { name = "Unknown"; }

                current.TakeDamage(damage);
                if (debugMode)
                    Debug.Log($"[TargetManager] 체인 히트 {h + 1}/{hits} -> {name}에게 {damage} 데미지");

                // 살아있으면 같은 대상 계속 타격, 죽으면 다음 앞 적으로 넘어감 (다음 루프에서 갱신)
                if (!current.IsAlive)
                {
                    current = GetAliveEnemies()
                        .Where(e => e.GridPosition.x > px)
                        .OrderBy(e => e.GridPosition.x - px)
                        .FirstOrDefault();
                    if (current == null) break;
                }
            }
        }

        /// <summary>
        /// 앞의 적에게 한 번만 타격 (VFX 히트 타이밍용)
        /// </summary>
        public void ChainHitOnceFromFront(int damage)
        {
            if (damage <= 0) return;
            int px = playerGridPosition.x;
            var target = GetAliveEnemies()
                .Where(e => e.GridPosition.x > px)
                .OrderBy(e => e.GridPosition.x - px)
                .FirstOrDefault();
            if (target == null) return;

            string name = "";
            try { name = target.EnemyName; } catch { name = "Unknown"; }

            target.TakeDamage(damage);
            if (debugMode)
                Debug.Log($"[TargetManager] 체인 단일 히트 -> {name}에게 {damage} 데미지");
        }



        /// <summary>
        /// 플레이어 앞 range칸 라인(세로) 범위의 적 모두에게 데미지
        /// </summary>
        public void DamagePlayerFrontLine(int range, int damage)
        {
            if (range <= 0 || damage <= 0) return;
            int px = playerGridPosition.x;

            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x > px && e.GridPosition.x <= px + range)
                .ToList();

            foreach (var enemy in enemies)
            {
                enemy.TakeDamage(damage);
                if (debugMode)
                    Debug.Log($"[TargetManager] 플레이어 앞 라인 {range}칸 내 {enemy.EnemyName}에게 {damage} 데미지");
            }
        }

        /// <summary>
        /// 타겟 포함, 플레이어쪽으로 range칸 세로 스트립 공격
        /// </summary>
        public void DamageTargetFrontStrip(Maglin.Enemy.Enemy target, int range, int damage)
        {
            if (target == null || range <= 0 || damage <= 0) return;

            int tx = target.GridPosition.x;
            int px = playerGridPosition.x;

            int minX = Mathf.Max(px + 1, tx - (range - 1));
            int maxX = tx;

            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x >= minX && e.GridPosition.x <= maxX)
                .ToList();

            foreach (var enemy in enemies)
            {
                enemy.TakeDamage(damage);
                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 포함 앞 스트립 {range}칸: {enemy.EnemyName}에게 {damage} 데미지");
            }
        }

        /// <summary>
        /// 타겟 포함, 플레이어 반대쪽으로 range칸 세로 스트립 공격
        /// </summary>
        public void DamageTargetBackStrip(Maglin.Enemy.Enemy target, int range, int damage)
        {
            if (target == null || range <= 0 || damage <= 0) return;

            int tx = target.GridPosition.x;

            int minX = tx;
            int maxX = tx + (range - 1);

            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x >= minX && e.GridPosition.x <= maxX)
                .ToList();

            foreach (var enemy in enemies)
            {
                enemy.TakeDamage(damage);
                if (debugMode)
                    Debug.Log($"[TargetManager] 타겟 포함 뒤 스트립 {range}칸: {enemy.EnemyName}에게 {damage} 데미지");
            }
        }
        #endregion

        #region Monster Movement System
        /// <summary>
        /// 타겟 타입에 따라 몬스터 이동 효과 적용
        /// </summary>
        public void ApplyMovementEffect(TargetType targetType, MonsterMovementType movementType, int distance)
        {
            if (movementType == MonsterMovementType.None || distance <= 0) return;

            switch (targetType)
            {
                case TargetType.SingleEnemy:
                    if (currentTarget != null && currentTarget.IsAlive)
                    {
                        ApplyMovementToMonster(currentTarget, movementType, distance);
                    }
                    break;

                case TargetType.AllEnemies:
                case TargetType.AllIncludingSelf:
                    ApplyMovementToAllMonsters(movementType, distance);
                    break;

                case TargetType.ChainFrontHits:
                    ApplyMovementToFrontMonsters(1, movementType, distance); // ChainFrontHits는 한 번에 하나씩
                    break;

                case TargetType.PlayerFrontLine:
                    ApplyMovementToPlayerFrontLine(distance, movementType, distance);
                    break;

                case TargetType.TargetFrontStrip:
                    if (currentTarget != null && currentTarget.IsAlive)
                    {
                        ApplyMovementToTargetFrontStrip(currentTarget, distance, movementType, distance);
                    }
                    break;

                case TargetType.TargetBackStrip:
                    if (currentTarget != null && currentTarget.IsAlive)
                    {
                        ApplyMovementToTargetBackStrip(currentTarget, distance, movementType, distance);
                    }
                    break;
            }
        }

        /// <summary>
        /// 단일 몬스터에게 이동 효과 적용
        /// </summary>
        private void ApplyMovementToMonster(Maglin.Enemy.Enemy monster, MonsterMovementType movementType, int distance)
        {
            if (monster == null || !monster.IsAlive) return;

            switch (movementType)
            {
                case MonsterMovementType.TowardsPlayer:
                    MoveMonsterTowardsPlayer(monster, distance);
                    break;
                case MonsterMovementType.AwayFromPlayer:
                    MoveMonsterAwayFromPlayer(monster, distance);
                    break;
                case MonsterMovementType.Random:
                    MoveMonsterToRandomPosition(monster);
                    break;
            }
        }

        /// <summary>
        /// 모든 몬스터에게 이동 효과 적용
        /// </summary>
        private void ApplyMovementToAllMonsters(MonsterMovementType movementType, int distance)
        {
            var aliveEnemies = GetAliveEnemies();
            foreach (var enemy in aliveEnemies)
            {
                ApplyMovementToMonster(enemy, movementType, distance);
            }
        }

        /// <summary>
        /// 앞쪽 몬스터들에게 이동 효과 적용 (ChainFrontHits에 대응)
        /// </summary>
        private void ApplyMovementToFrontMonsters(int targetCount, MonsterMovementType movementType, int distance)
        {
            int px = playerGridPosition.x;
            var frontMonsters = GetAliveEnemies()
                .Where(e => e.GridPosition.x > px)
                .OrderBy(e => e.GridPosition.x - px)
                .Take(targetCount)
                .ToList();

            foreach (var monster in frontMonsters)
            {
                ApplyMovementToMonster(monster, movementType, distance);
            }
        }

        /// <summary>
        /// 플레이어 앞 라인의 몬스터들에게 이동 효과 적용
        /// </summary>
        private void ApplyMovementToPlayerFrontLine(int range, MonsterMovementType movementType, int distance)
        {
            int px = playerGridPosition.x;
            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x > px && e.GridPosition.x <= px + range)
                .ToList();

            foreach (var enemy in enemies)
            {
                ApplyMovementToMonster(enemy, movementType, distance);
            }
        }

        /// <summary>
        /// 타겟 포함 앞 스트립의 몬스터들에게 이동 효과 적용
        /// </summary>
        private void ApplyMovementToTargetFrontStrip(Maglin.Enemy.Enemy target, int range, MonsterMovementType movementType, int distance)
        {
            if (target == null) return;

            int tx = target.GridPosition.x;
            int px = playerGridPosition.x;
            int minX = Mathf.Max(px + 1, tx - (range - 1));
            int maxX = tx;

            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x >= minX && e.GridPosition.x <= maxX)
                .ToList();

            foreach (var enemy in enemies)
            {
                ApplyMovementToMonster(enemy, movementType, distance);
            }
        }

        /// <summary>
        /// 타겟 포함 뒤 스트립의 몬스터들에게 이동 효과 적용
        /// </summary>
        private void ApplyMovementToTargetBackStrip(Maglin.Enemy.Enemy target, int range, MonsterMovementType movementType, int distance)
        {
            if (target == null) return;

            int tx = target.GridPosition.x;
            int minX = tx;
            int maxX = tx + (range - 1);

            var enemies = GetAliveEnemies()
                .Where(e => e.GridPosition.x >= minX && e.GridPosition.x <= maxX)
                .ToList();

            foreach (var enemy in enemies)
            {
                ApplyMovementToMonster(enemy, movementType, distance);
            }
        }

        /// <summary>
        /// 몬스터를 플레이어쪽으로 n칸 이동 (겹치기 불가, 점프 불가)
        /// </summary>
        private void MoveMonsterTowardsPlayer(Maglin.Enemy.Enemy monster, int distance)
        {
            if (monster == null || distance <= 0) return;

            Vector2Int currentPos = monster.GridPosition;
            Vector2Int targetPos = currentPos;

            // 플레이어쪽으로 이동 (X축에서 감소 방향)
            for (int i = 0; i < distance; i++)
            {
                Vector2Int nextPos = new Vector2Int(targetPos.x - 1, targetPos.y);

                // 플레이어 위치까지는 이동하지 않음 (최소 1칸 떨어져 있어야 함)
                if (nextPos.x <= playerGridPosition.x) break;

                // 해당 위치가 유효하고 비어있는지 확인
                if (IsPositionValidAndEmpty(nextPos))
                {
                    targetPos = nextPos;
                }
                else
                {
                    // 길이 막혔으면 이동 중단 (점프 불가)
                    break;
                }
            }

            // 실제로 이동할 위치가 있으면 이동
            if (targetPos != currentPos)
            {
                MoveMonsterToPosition(monster, targetPos);

                if (debugMode)
                    Debug.Log($"[TargetManager] {monster.EnemyName} 플레이어쪽으로 이동: {currentPos} -> {targetPos}");
            }
        }

        /// <summary>
        /// 몬스터를 플레이어 반대쪽으로 n칸 이동 (겹치기 불가, 점프 불가)
        /// </summary>
        private void MoveMonsterAwayFromPlayer(Maglin.Enemy.Enemy monster, int distance)
        {
            if (monster == null || distance <= 0) return;

            Vector2Int currentPos = monster.GridPosition;
            Vector2Int targetPos = currentPos;

            // 플레이어 반대쪽으로 이동 (X축에서 증가 방향)
            for (int i = 0; i < distance; i++)
            {
                Vector2Int nextPos = new Vector2Int(targetPos.x + 1, targetPos.y);

                // 해당 위치가 유효하고 비어있는지 확인
                if (IsPositionValidAndEmpty(nextPos))
                {
                    targetPos = nextPos;
                }
                else
                {
                    // 길이 막혔으면 이동 중단 (점프 불가)
                    break;
                }
            }

            // 실제로 이동할 위치가 있으면 이동
            if (targetPos != currentPos)
            {
                MoveMonsterToPosition(monster, targetPos);

                if (debugMode)
                    Debug.Log($"[TargetManager] {monster.EnemyName} 플레이어 반대쪽으로 이동: {currentPos} -> {targetPos}");
            }
        }

        /// <summary>
        /// 몬스터를 무작위 위치로 이동 (겹치기 불가)
        /// </summary>
        private void MoveMonsterToRandomPosition(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return;

            Vector2Int currentPos = monster.GridPosition;
            List<Vector2Int> availablePositions = GetAvailablePositions();

            // 현재 위치는 제외
            availablePositions.Remove(currentPos);

            if (availablePositions.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
                Vector2Int targetPos = availablePositions[randomIndex];

                MoveMonsterToPosition(monster, targetPos);

                if (debugMode)
                    Debug.Log($"[TargetManager] {monster.EnemyName} 무작위 위치로 이동: {currentPos} -> {targetPos}");
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[TargetManager] {monster.EnemyName} 이동할 빈 공간이 없습니다.");
            }
        }

        /// <summary>
        /// 위치가 유효하고 비어있는지 확인
        /// </summary>
        private bool IsPositionValidAndEmpty(Vector2Int position)
        {
            // GridFieldManager를 통해 유효한 위치인지 확인
            if (GridFieldManager.Instance != null)
            {
                if (!GridFieldManager.Instance.IsValidGridPosition(position))
                    return false;
            }

            // 플레이어 위치가 아닌지 확인
            if (position == playerGridPosition)
                return false;

            // 다른 몬스터가 있는지 확인
            var aliveEnemies = GetAliveEnemies();
            foreach (var enemy in aliveEnemies)
            {
                if (enemy.GridPosition == position)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 사용 가능한 모든 위치 목록 반환
        /// </summary>
        private List<Vector2Int> GetAvailablePositions()
        {
            var availablePositions = new List<Vector2Int>();

            // GridFieldManager가 있으면 그리드 크기를 가져옴
            if (GridFieldManager.Instance != null)
            {
                // 그리드의 모든 위치를 확인 (임시로 -10 ~ 10 범위 사용)
                for (int x = -10; x <= 10; x++)
                {
                    for (int y = -5; y <= 5; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (IsPositionValidAndEmpty(pos))
                        {
                            availablePositions.Add(pos);
                        }
                    }
                }
            }
            else
            {
                // GridFieldManager가 없으면 기본 범위 사용
                for (int x = 1; x <= 10; x++) // 플레이어(0,0) 오른쪽만
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (IsPositionValidAndEmpty(pos))
                        {
                            availablePositions.Add(pos);
                        }
                    }
                }
            }

            return availablePositions;
        }

        /// <summary>
        /// 몬스터를 특정 위치로 실제 이동 (MonsterSpawnManager를 통해 통합 관리)
        /// </summary>
        private void MoveMonsterToPosition(Maglin.Enemy.Enemy monster, Vector2Int newGridPosition)
        {
            if (monster == null) return;

            // MonsterBattleManager를 통해 위치 이동 요청
            if (MonsterBattleManager.Instance != null)
            {
                MonsterBattleManager.Instance.RequestMonsterMovement(monster, newGridPosition);

                if (debugMode)
                    Debug.Log($"[TargetManager] MonsterBattleManager에게 이동 요청: {monster.EnemyName} -> {newGridPosition}");
            }
            else
            {
                // MonsterBattleManager가 없는 경우 폴백 (직접 처리)
                monster.SetPosition(newGridPosition);

                if (GridFieldManager.Instance != null)
                {
                    Vector3 worldPosition = GridFieldManager.Instance.GridToWorldPosition(newGridPosition);
                    monster.transform.position = worldPosition;
                }
                else
                {
                    Vector3 worldPosition = new Vector3(newGridPosition.x, newGridPosition.y, monster.transform.position.z);
                    monster.transform.position = worldPosition;
                }

                if (debugMode)
                    Debug.LogWarning($"[TargetManager] MonsterBattleManager가 없어 직접 이동 처리: {monster.EnemyName} -> {newGridPosition}");
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

                    // 현재 타겟이 없으면 이 몬스터를 타겟으로 설정
                    if (currentTarget == null)
                    {
                        SetTarget(enemy);
                    }
                }

                if (debugMode)
                    Debug.Log($"[TargetManager] 몬스터 추가: {enemy?.EnemyName ?? "Unknown"} (현재 타겟: {currentTarget?.EnemyName ?? "없음"})");
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

        /// <summary>
        /// 전투 준비 시작 (모든 마커 비활성화)
        /// </summary>
        public void StartBattleInitialization()
        {
            isBattleReady = false;

            // 모든 몬스터의 타겟 마커 비활성화
            foreach (var monsterObj in spawnedMonsters)
            {
                if (monsterObj != null)
                {
                    var enemy = monsterObj.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        SetTargetMarkerActive(enemy, false);
                    }
                }
            }

            if (debugMode)
                Debug.Log("[TargetManager] 전투 초기화 시작 - 모든 타겟 마커 비활성화");
        }

        /// <summary>
        /// 전투 준비 완료 (타겟 시스템 활성화)
        /// </summary>
        public void CompleteBattleInitialization()
        {
            isBattleReady = true;

            if (debugMode)
                Debug.Log($"[TargetManager] 전투 준비 완료 시작 - 스폰된 몬스터 수: {spawnedMonsters.Count}, 현재 타겟: {currentTarget?.EnemyName ?? "없음"}");

            // 현재 타겟이 있으면 마커 활성화 및 애니메이션 시작
            if (currentTarget != null && currentTarget.IsAlive)
            {
                SetTargetMarkerActive(currentTarget, true);

                // 마커 캐시 및 위치 설정
                cachedTargetMarker = currentTarget.transform.Find("TargetMarker");
                if (cachedTargetMarker != null && cachedTargetMarker.gameObject.activeInHierarchy)
                {
                    UpdateTargetMarkerPosition(cachedTargetMarker, currentTarget);
                    lastTargetPosition = currentTarget.transform.position;
                    markerPositionSet = true;

                    if (debugMode)
                        Debug.Log($"[TargetManager] 기존 타겟 마커 활성화 및 애니메이션 시작: {currentTarget.EnemyName}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning($"[TargetManager] {currentTarget.EnemyName}의 TargetMarker를 찾을 수 없습니다.");
                }
            }
            else
            {
                // 타겟이 없거나 죽어있으면 가장 가까운 몬스터로 설정
                if (debugMode)
                    Debug.Log("[TargetManager] 현재 타겟이 없거나 죽어있음, 가장 가까운 몬스터로 설정");

                SetTargetToClosest();

                if (currentTarget != null)
                {
                    if (debugMode)
                        Debug.Log($"[TargetManager] 새로운 타겟 설정됨: {currentTarget.EnemyName}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[TargetManager] 설정할 수 있는 타겟이 없습니다.");
                }
            }

            if (debugMode)
                Debug.Log($"[TargetManager] 전투 준비 완료 - 타겟 시스템 활성화 (최종 타겟: {currentTarget?.EnemyName ?? "없음"})");

            // 전투 준비 완료 후 타겟 변경 이벤트 발생 (UI 업데이트를 위해)
            OnTargetChanged?.Invoke(currentTarget);
        }
        #endregion
    }
}