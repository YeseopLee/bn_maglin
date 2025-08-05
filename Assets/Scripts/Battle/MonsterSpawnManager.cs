using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maglin.Enemy;
using Maglin.Core;
using Maglin.Player;
using TMPro;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투에서 몬스터 스폰과 관리를 담당하는 매니저
    /// </summary>
    public class MonsterSpawnManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterSpawnManager _instance;

        public static MonsterSpawnManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterSpawnManager>();

                    if (_instance == null)
                    {
                        GameObject spawnManagerObject = new GameObject("MonsterSpawnManager");
                        _instance = spawnManagerObject.AddComponent<MonsterSpawnManager>();
                        DontDestroyOnLoad(spawnManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 스폰 완료 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy> OnMonsterSpawned;

        /// <summary>
        /// 몬스터 사망 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy> OnMonsterDied;

        /// <summary>
        /// 모든 몬스터 스폰 완료 이벤트
        /// </summary>
        public static event System.Action OnAllMonstersSpawned;
        #endregion

        #region Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 스폰된 몬스터들
        private List<GameObject> spawnedMonsters = new List<GameObject>();

        // 플레이어 위치
        private Vector2Int playerGridPosition = new Vector2Int(0, 0);

        // 프리팹 참조
        private GameObject monsterPrefab;
        #endregion

        #region Properties
        /// <summary>
        /// 스폰된 몬스터 목록
        /// </summary>
        public IReadOnlyList<GameObject> SpawnedMonsters => spawnedMonsters.AsReadOnly();

        /// <summary>
        /// 살아있는 몬스터 수
        /// </summary>
        public int AliveMonsterCount => spawnedMonsters.Count(m => m != null && m.activeInHierarchy && m.GetComponent<Maglin.Enemy.Enemy>()?.IsAlive == true);
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSpawnManager();
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
        /// 스폰 매니저 초기화
        /// </summary>
        public void InitializeSpawnManager()
        {
            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 스폰 매니저 초기화 시작");

            // GridFieldManager 초기화 대기
            StartCoroutine(InitializeAfterGridReady());
        }

        /// <summary>
        /// GridFieldManager 초기화 후 스폰 매니저 초기화
        /// </summary>
        private IEnumerator InitializeAfterGridReady()
        {
            // GridFieldManager가 초기화될 때까지 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return null;
            }

            if (debugMode)
                Debug.Log("[MonsterSpawnManager] GridFieldManager 연결 완료");

            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 스폰 매니저 초기화 완료");
        }

        /// <summary>
        /// 몬스터 프리팹 설정 (BattleUIManager에서 호출)
        /// </summary>
        public void SetMonsterPrefab(GameObject prefab)
        {
            monsterPrefab = prefab;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 몬스터 프리팹 설정: {prefab?.name ?? "null"}");
        }

        /// <summary>
        /// 필드 슬롯 설정 (BattleUIManager에서 호출)
        /// </summary>
        /// <summary>
        /// 레거시 메서드 - Grid 시스템에서는 더 이상 사용하지 않음
        /// </summary>
        [System.Obsolete("Grid 시스템으로 변경되어 더 이상 사용하지 않습니다.")]
        public void SetFieldSlots(Transform[] slots)
        {
            if (debugMode)
                Debug.LogWarning("[MonsterSpawnManager] SetFieldSlots는 Grid 시스템으로 인해 더 이상 사용되지 않습니다.");
        }
        #endregion

        #region Animation Integration
        /// <summary>
        /// 스폰된 몬스터들의 리스트 (애니메이션 시스템용)
        /// </summary>
        private List<GameObject> pendingAnimationMonsters = new List<GameObject>();

        /// <summary>
        /// 애니메이션 완료 대기 중인 몬스터 수
        /// </summary>
        private int waitingForAnimationCount = 0;

        /// <summary>
        /// 몬스터 스폰 애니메이션 활성화 여부
        /// </summary>
        [Header("애니메이션 설정")]
        [SerializeField] private bool enableSpawnAnimations = true;
        [SerializeField] private bool useSequentialSpawn = true;
        #endregion

        #region Monster Spawning
        /// <summary>
        /// 테스트 몬스터들 스폰 (BattleTestController에서 호출)
        /// </summary>
        public void SpawnTestMonsters(BattleStageSO battleStage)
        {
            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 테스트 몬스터 스폰 시작");

            // 기존 몬스터 정리
            ClearSpawnedMonsters();

            // 현재 층에서 가능한 전투 확인
            List<BattleSO> availableBattles = GetAvailableBattlesForFloor(1, battleStage);

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 사용 가능한 배틀 수: {availableBattles.Count}, battleStage: {battleStage?.name ?? "null"}");

            if (availableBattles.Count > 0)
            {
                // 가중치 기반으로 배틀 선택
                var selectedBattle = SelectBattleByWeight(battleStage);
                if (selectedBattle != null)
                {
                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 선택된 배틀: {selectedBattle.name}");
                    SpawnMonstersFromBattle(selectedBattle);
                }
                else
                {
                    // 가중치 선택 실패 시 기본 테스트 몬스터
                    if (debugMode)
                        Debug.Log("[MonsterSpawnManager] 배틀 선택 실패, 기본 테스트 몬스터 스폰");
                    SpawnDefaultTestMonster();
                }
            }
            else
            {
                // 기본 테스트 몬스터 스폰
                if (debugMode)
                    Debug.Log("[MonsterSpawnManager] 사용 가능한 배틀 없음, 기본 테스트 몬스터 스폰");
                SpawnDefaultTestMonster();
            }

            // 애니메이션 시스템과 통합 (자동 실행하지 않고 대기)
            if (enableSpawnAnimations && pendingAnimationMonsters.Count > 0)
            {
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {pendingAnimationMonsters.Count}마리 몬스터가 애니메이션 대기 중");
                // ProcessSpawnAnimations(); // 자동 실행하지 않음
            }

            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 테스트 몬스터 스폰 완료");
        }

        /// <summary>
        /// 현재 층에서 가능한 전투 목록 가져오기
        /// </summary>
        private List<BattleSO> GetAvailableBattlesForFloor(int floor, BattleStageSO battleStage)
        {
            var availableBattles = new List<BattleSO>();

            if (battleStage != null && battleStage.AvailableBattles != null)
            {
                foreach (var battleData in battleStage.AvailableBattles)
                {
                    if (battleData.battleData != null)
                    {
                        availableBattles.Add(battleData.battleData);
                    }
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[MonsterSpawnManager] battleStage가 null이므로 기본 몬스터 스폰");
            }

            return availableBattles;
        }

        /// <summary>
        /// 가중치를 기반으로 배틀 선택
        /// </summary>
        private BattleSO SelectBattleByWeight(BattleStageSO battleStage)
        {
            if (battleStage == null || battleStage.AvailableBattles == null || battleStage.AvailableBattles.Length == 0)
                return null;

            // 전체 가중치 합계 계산
            float totalWeight = 0f;
            foreach (var battleData in battleStage.AvailableBattles)
            {
                if (battleData.battleData != null)
                {
                    totalWeight += battleData.spawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterSpawnManager] 모든 배틀의 가중치가 0 이하입니다. 첫 번째 배틀을 선택합니다.");
                return battleStage.AvailableBattles[0].battleData;
            }

            // 랜덤 값 생성 (0 ~ totalWeight)
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            // 가중치 기반 선택
            foreach (var battleData in battleStage.AvailableBattles)
            {
                if (battleData.battleData != null)
                {
                    currentWeight += battleData.spawnWeight;
                    if (randomValue <= currentWeight)
                    {
                        if (debugMode)
                            Debug.Log($"[MonsterSpawnManager] 가중치 선택: {battleData.battleData.name} (가중치: {battleData.spawnWeight}, 랜덤값: {randomValue:F2}/{totalWeight:F2})");

                        return battleData.battleData;
                    }
                }
            }

            // 혹시 모를 경우를 대비한 fallback (첫 번째 배틀)
            if (debugMode)
                Debug.LogWarning("[MonsterSpawnManager] 가중치 선택 실패, 첫 번째 배틀을 선택합니다.");
            return battleStage.AvailableBattles[0].battleData;
        }

        /// <summary>
        /// BattleSO에서 몬스터들 스폰
        /// </summary>
        private void SpawnMonstersFromBattle(BattleSO battleData)
        {
            if (battleData.EnemyPlacements == null) return;

            foreach (var placement in battleData.EnemyPlacements)
            {
                if (placement.enemyData != null)
                {
                    // 몬스터는 1번 슬롯 이상에서만 스폰 가능
                    var spawnPosition = Mathf.Max(1, placement.startingPosition);
                    var position = new Vector2Int(spawnPosition, 0);

                    // 겹치지 않는 위치 찾기
                    position = FindAvailableSpawnPosition(position);

                    if (debugMode && placement.startingPosition < 1)
                        Debug.LogWarning($"[MonsterSpawnManager] 몬스터 {placement.enemyData.EnemyName}의 스폰 위치를 {placement.startingPosition}에서 {position.x}으로 조정");

                    SpawnMonster(placement.enemyData, position);
                }
            }

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {battleData.BattleName}에서 {battleData.EnemyPlacements.Length}마리 몬스터 스폰");
        }

        /// <summary>
        /// 기본 테스트 몬스터 스폰
        /// </summary>
        private void SpawnDefaultTestMonster()
        {
            // 더미 몬스터 스폰
            SpawnDummyMonster();
        }

        /// <summary>
        /// 더미 몬스터 스폰 (ScriptableObject 없이)
        /// </summary>
        private void SpawnDummyMonster()
        {
            // 겹치지 않는 위치 찾기
            var position = FindAvailableSpawnPosition(new Vector2Int(8, 0));
            GameObject monsterObj = CreateMonsterGameObject("테스트 몬스터", position, null);

            if (monsterObj != null)
            {
                spawnedMonsters.Add(monsterObj);

                // 애니메이션 대기 리스트에 추가 (더미 몬스터도 애니메이션 적용)
                if (enableSpawnAnimations)
                {
                    monsterObj.SetActive(false);
                    pendingAnimationMonsters.Add(monsterObj);
                    
                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 더미 테스트 몬스터 애니메이션 대기 리스트에 추가: {position}");
                }
                else
                {
                    // TargetManager에 몬스터 추가 (더미 몬스터도 타겟 가능)
                    if (TargetManager.Instance != null)
                    {
                        TargetManager.Instance.AddMonster(monsterObj);
                        
                        // 첫 번째 몬스터라면 타겟으로 설정
                        var enemy = monsterObj.GetComponent<Maglin.Enemy.Enemy>();
                        if (enemy != null && TargetManager.Instance.CurrentTarget == null)
                        {
                            TargetManager.Instance.SetTarget(enemy);
                        }
                    }

                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 더미 테스트 몬스터 스폰 완료 (애니메이션 없음): {position}");
                }
            }
        }

        /// <summary>
        /// 몬스터 스폰 (UI 준비 후 실행)
        /// </summary>
        private void SpawnMonster(EnemySO enemyData, Vector2Int position)
        {
            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 몬스터 스폰 시작: {enemyData?.EnemyName ?? "Unknown"} at {position}");

            // UI가 준비된 후에 몬스터 생성
            StartCoroutine(CreateMonsterAfterUIReady(enemyData, position));
        }

        /// <summary>
        /// Grid 시스템 준비 후 몬스터 생성
        /// </summary>
        private IEnumerator CreateMonsterAfterUIReady(EnemySO enemyData, Vector2Int gridPosition)
        {
            // GridFieldManager 준비 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return null;
            }

            // 그리드 위치가 유효한지 확인
            if (!GridFieldManager.Instance.IsValidGridPosition(gridPosition))
            {
                // 유효하지 않은 위치면 가장 가까운 빈 자리 찾기
                gridPosition = GridFieldManager.Instance.FindNearestEmptyGridPosition(gridPosition);

                if (gridPosition.x == -1) // 빈 자리가 없음
                {
                    if (debugMode)
                        Debug.LogWarning($"[MonsterSpawnManager] 몬스터를 배치할 빈 그리드 위치를 찾을 수 없습니다.");
                    yield break;
                }
            }

            // 이제 안전하게 몬스터 생성
            GameObject monsterObj = CreateMonsterGameObject(enemyData.EnemyName, gridPosition, enemyData);

            if (monsterObj != null)
            {
                var enemy = monsterObj.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    // Enemy의 debugMode 설정 (리플렉션 사용)
                    var debugField = typeof(Maglin.Enemy.Enemy).GetField("debugMode",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (debugField != null)
                    {
                        debugField.SetValue(enemy, debugMode);
                    }

                    enemy.Initialize(enemyData, gridPosition);

                    // 몬스터 체력 이벤트 구독
                    SetupMonsterHealthEvents(enemy);
                }

                spawnedMonsters.Add(monsterObj);

                // 애니메이션 대기 리스트에 추가
                if (enableSpawnAnimations)
                {
                    // 몬스터를 초기에 비활성화하고 애니메이션 리스트에 추가
                    monsterObj.SetActive(false);
                    pendingAnimationMonsters.Add(monsterObj);
                    
                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] {enemyData.EnemyName} 애니메이션 대기 리스트에 추가: {gridPosition}");
                }
                else
                {
                    // 애니메이션을 사용하지 않는 경우 즉시 활성화
                    // TargetManager에 몬스터 추가
                    if (TargetManager.Instance != null)
                    {
                        TargetManager.Instance.AddMonster(monsterObj);
                        
                        // 첫 번째 몬스터라면 타겟으로 설정
                        if (TargetManager.Instance.CurrentTarget == null)
                        {
                            TargetManager.Instance.SetTarget(enemy);
                        }
                    }

                    // 이벤트 발생
                    OnMonsterSpawned?.Invoke(enemy);

                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] {enemyData.EnemyName} 스폰 완료 (애니메이션 없음): {gridPosition}");
                }
            }
        }

        /// <summary>
        /// 몬스터 게임오브젝트 생성 (Grid 기반)
        /// </summary>
        private GameObject CreateMonsterGameObject(string monsterName, Vector2Int gridPosition, EnemySO enemyData = null)
        {
            GameObject monsterObj;

            // 생성 시 잠시 비활성화 (위치 설정 완료 후 활성화)
            bool shouldActivateAfterSetup = false;

            if (monsterPrefab != null)
            {
                // 기존 프리팹 사용
                monsterObj = Instantiate(monsterPrefab);
                monsterObj.name = $"Monster_{monsterName}";

                // 위치 설정 중 깜빡임 방지를 위해 잠시 비활성화
                monsterObj.SetActive(false);
                shouldActivateAfterSetup = true;

                // 스프라이트 설정 (EnemySO에서 가져오기)
                var spriteRenderer = monsterObj.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && enemyData != null && enemyData.Sprite != null)
                {
                    spriteRenderer.sprite = enemyData.Sprite;
                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] EnemySO 스프라이트 적용: {enemyData.Sprite.name}");
                }

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 프리팹 몬스터 생성: {monsterName}");
            }
            else
            {
                // 프리팹이 없을 때만 기본 생성 (비상용)
                monsterObj = new GameObject($"Monster_{monsterName}");
                monsterObj.SetActive(false);
                shouldActivateAfterSetup = true;

                var spriteRenderer = monsterObj.AddComponent<SpriteRenderer>();
                spriteRenderer.color = Color.red;
                spriteRenderer.sortingOrder = 10;

                if (enemyData != null && enemyData.Sprite != null)
                {
                    spriteRenderer.sprite = enemyData.Sprite;
                }
                else
                {
                    spriteRenderer.sprite = CreateDefaultSprite();
                }

                monsterObj.AddComponent<Maglin.Enemy.Enemy>();
                monsterObj.AddComponent<EnemyAI>();

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 기본 몬스터 생성 (프리팹 없음): {monsterName}");
            }

            // GridFieldManager를 통해 그리드 위치에 배치
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                // GridFieldManager를 통해 몬스터를 그리드에 배치
                bool placed = GridFieldManager.Instance.PlaceObjectAtGrid(monsterObj, gridPosition, true);

                if (placed)
                {
                    // Enemy 컴포넌트에 그리드 위치 설정
                    var enemy = monsterObj.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        SetMonsterGridPosition(enemy, gridPosition);
                    }

                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 몬스터를 그리드 위치 {gridPosition}에 배치: {monsterName} -> {monsterObj.transform.position}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning($"[MonsterSpawnManager] 몬스터를 그리드 위치 {gridPosition}에 배치할 수 없습니다: {monsterName}");
                }
            }
            else
            {
                // GridFieldManager가 없는 경우 기본 위치
                monsterObj.transform.SetParent(null, true);
                monsterObj.transform.position = new Vector3(gridPosition.x, gridPosition.y + 0.5f, 0f);

                if (debugMode)
                    Debug.LogWarning($"[MonsterSpawnManager] GridFieldManager가 없어 기본 위치 사용: {monsterName}");
            }

            // 위치 설정 완료 후 활성화 (애니메이션 사용시 제외)
            if (shouldActivateAfterSetup && !enableSpawnAnimations)
            {
                monsterObj.SetActive(true);
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 몬스터 활성화: {monsterName}");
            }
            else if (enableSpawnAnimations && shouldActivateAfterSetup)
            {
                // 애니메이션 사용시 비활성화 상태로 유지 (애니메이션이 활성화할 예정)
                monsterObj.SetActive(false);
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 몬스터 애니메이션 대기 상태: {monsterName}");
            }

            return monsterObj;
        }

        /// <summary>
        /// 기본 스프라이트 생성
        /// </summary>
        private Sprite CreateDefaultSprite()
        {
            // 간단한 사각형 텍스처 생성
            Texture2D texture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];

            // 빨간색 사각형으로 채우기
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.red;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // 스프라이트 생성
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

            return sprite;
        }

        /// <summary>
        /// 간단한 체력 텍스트 생성
        /// </summary>
        private void CreateSimpleHealthText(GameObject parent)
        {
            GameObject healthTextObj = new GameObject("HealthText");
            healthTextObj.transform.SetParent(parent.transform);
            healthTextObj.transform.localPosition = new Vector3(0, 1, 0);

            var textMesh = healthTextObj.AddComponent<TextMesh>();
            textMesh.text = "100/100";
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
        }
        #endregion

        #region Monster Health Management
        /// <summary>
        /// 몬스터 체력 이벤트 구독 설정
        /// </summary>
        private void SetupMonsterHealthEvents(Maglin.Enemy.Enemy enemy)
        {
            if (enemy == null) return;

            // 체력 변경 이벤트 구독 (기존 UI 업데이트)
            enemy.OnHealthChanged += (currentHealth, maxHealth) => UpdateMonsterHealthBar(enemy, currentHealth, maxHealth);

            // 사망 이벤트 구독
            enemy.OnDeath += OnMonsterDeath;

            // 초기 체력바 업데이트
            UpdateMonsterHealthBar(enemy, enemy.CurrentHealth, enemy.MaxHealth);

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 체력 이벤트 구독 완료");
        }

        // 프리팹에 이미 UI가 있으므로 UI 생성 메서드들은 제거됨

        /// <summary>
        /// 몬스터 체력바 업데이트 (프리팹의 기존 UI 사용)
        /// </summary>
        private void UpdateMonsterHealthBar(Maglin.Enemy.Enemy enemy, int currentHealth, int maxHealth)
        {
            if (enemy == null) return;

            // 프리팹의 HealthBarCanvas 찾기
            Transform healthBarCanvas = enemy.transform.Find("HealthBarCanvas");
            if (healthBarCanvas == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterSpawnManager] {enemy.EnemyName}에서 HealthBarCanvas를 찾을 수 없습니다.");
                return;
            }

            // Canvas가 카메라를 바라보도록 설정 (WorldSpace Canvas의 경우)
            Canvas canvas = healthBarCanvas.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // 체력바가 항상 카메라를 바라보도록 설정
                    Vector3 directionToCamera = mainCamera.transform.position - healthBarCanvas.position;
                    directionToCamera.y = 0; // Y축 회전만 적용
                    if (directionToCamera != Vector3.zero)
                    {
                        healthBarCanvas.rotation = Quaternion.LookRotation(-directionToCamera);
                    }
                }
            }

            // HealthBarBackground 하위의 HealthBar 찾기
            Transform healthBarBG = healthBarCanvas.Find("HealthBarBackground");
            if (healthBarBG == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterSpawnManager] {enemy.EnemyName}에서 HealthBarBackground를 찾을 수 없습니다.");
                return;
            }

            // 체력 바 이미지 업데이트
            var healthBar = healthBarBG.Find("HealthBar")?.GetComponent<UnityEngine.UI.Image>();
            if (healthBar != null)
            {
                float healthPercentage = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
                healthBar.fillAmount = healthPercentage;

                // 체력에 따라 색상 변경 (더 부드러운 색상 전환)
                if (healthPercentage > 0.6f)
                    healthBar.color = Color.Lerp(Color.yellow, Color.green, (healthPercentage - 0.6f) / 0.4f);
                else if (healthPercentage > 0.3f)
                    healthBar.color = Color.Lerp(Color.red, Color.yellow, (healthPercentage - 0.3f) / 0.3f);
                else
                    healthBar.color = Color.red;
            }

            // 체력 텍스트 업데이트
            var healthText = healthBarBG.Find("HealthText")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (healthText != null)
            {
                healthText.text = currentHealth.ToString();
            }

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 체력바 업데이트: {currentHealth}/{maxHealth} ({(maxHealth > 0 ? (float)currentHealth / maxHealth * 100f : 0f):F1}%)");
        }

        /// <summary>
        /// 몬스터 사망 이벤트 처리
        /// </summary>
        private void OnMonsterDeath(Maglin.Enemy.Enemy enemy)
        {
            if (enemy == null) return;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 사망 처리");

            // 사망 애니메이션 실행
            if (MonsterDeathAnimationManager.Instance != null)
            {
                MonsterDeathAnimationManager.Instance.PlayDeathAnimation(enemy.gameObject);
            }

            // 몬스터 제거 처리
            StartCoroutine(RemoveDeadMonster(enemy));

            // 이벤트 발생
            OnMonsterDied?.Invoke(enemy);
        }

        /// <summary>
        /// 죽은 몬스터 제거
        /// </summary>
        private IEnumerator RemoveDeadMonster(Maglin.Enemy.Enemy enemy)
        {
            if (enemy == null) yield break;

            GameObject monsterObj = enemy.gameObject;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 제거 시작");

            // 1. spawnedMonsters 리스트에서 제거
            if (spawnedMonsters.Contains(monsterObj))
            {
                spawnedMonsters.Remove(monsterObj);
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} spawnedMonsters 리스트에서 제거됨");
            }

            // 2. TargetManager에서 제거
            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.RemoveMonster(monsterObj);
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} TargetManager에서 제거됨");
            }

            // 3. 사망 애니메이션 완료 대기
            if (MonsterDeathAnimationManager.Instance != null)
            {
                bool animationCompleted = false;
                
                // 사망 애니메이션 완료 이벤트 구독
                System.Action<GameObject> onAnimationCompleted = (obj) => {
                    if (obj == monsterObj)
                    {
                        animationCompleted = true;
                    }
                };
                
                MonsterDeathAnimationManager.OnDeathAnimationCompleted += onAnimationCompleted;
                
                // 애니메이션 완료까지 대기 (최대 3초)
                float waitTime = 0f;
                while (!animationCompleted && waitTime < 3f)
                {
                    yield return new WaitForSeconds(0.1f);
                    waitTime += 0.1f;
                }
                
                // 이벤트 구독 해제
                MonsterDeathAnimationManager.OnDeathAnimationCompleted -= onAnimationCompleted;
                
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 사망 애니메이션 대기 완료 (대기시간: {waitTime:F1}초)");
            }
            else
            {
                // 사망 애니메이션 매니저가 없으면 기본 대기시간
                yield return new WaitForSeconds(0.5f);
            }

            // 4. 게임오브젝트 제거
            if (monsterObj != null)
            {
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 게임오브젝트 제거");

                Destroy(monsterObj);
            }

            // 5. 살아있는 몬스터 수 확인
            int aliveCount = AliveMonsterCount;
            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 살아있는 몬스터 수: {aliveCount}");

            // 6. 모든 몬스터가 죽었는지 확인
            if (aliveCount <= 0)
            {
                if (debugMode)
                    Debug.Log("[MonsterSpawnManager] 모든 몬스터가 제거됨 - 전투 승리 조건 확인");
            }
        }
        #endregion

        #region Position Management
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
                    Debug.LogWarning("[MonsterSpawnManager] UpdateMonsterPosition: monster가 null입니다.");
                return;
            }

            var gridPos = monster.GridPosition;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 몬스터 위치 업데이트: {monster.name} -> 그리드 위치 ({gridPos.x}, {gridPos.y})");

            // GridFieldManager를 통해 위치 업데이트
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                Vector3 targetWorldPos = GridFieldManager.Instance.GridToWorldPosition(gridPos);

                // 부드러운 이동으로 변경
                StartCoroutine(SmoothMoveToPosition(monster.gameObject, targetWorldPos, 0.5f));

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 몬스터 월드 위치 설정: {monster.name} -> {targetWorldPos}");
            }
            else
            {
                // GridFieldManager가 없는 경우 기본 계산
                Vector3 targetPos = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f);
                StartCoroutine(SmoothMoveToPosition(monster.gameObject, targetPos, 0.5f));

                if (debugMode)
                    Debug.LogWarning($"[MonsterSpawnManager] GridFieldManager가 없어 기본 위치 계산 사용: {monster.name} -> {targetPos}");
            }
        }

        /// <summary>
        /// UI 슬롯의 월드 위치 계산
        /// </summary>
        private Vector3 GetSlotWorldPosition(Transform slot)
        {
            if (slot == null)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterSpawnManager] GetSlotWorldPosition: slot이 null입니다.");
                return Vector3.zero;
            }

            // RectTransform을 월드 좌표로 변환
            RectTransform rectTransform = slot as RectTransform;
            if (rectTransform != null)
            {
                Canvas canvas = slot.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Vector3 worldPos;

                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        // Screen Space Overlay: UI 좌표를 월드 좌표로 변환
                        Camera cam = Camera.main;
                        if (cam == null)
                        {
                            if (debugMode)
                                Debug.LogWarning("[MonsterSpawnManager] Camera.main이 null입니다.");
                            return Vector3.zero;
                        }

                        Vector3[] corners = new Vector3[4];
                        rectTransform.GetWorldCorners(corners);

                        // UI 중심점 계산
                        Vector3 center = (corners[0] + corners[2]) * 0.5f;

                        // 화면 좌표로 변환 후 월드 좌표로 변환
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(cam, center);
                        worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f)); // Z=10으로 카메라 앞에 배치
                    }
                    else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                    {
                        // Screen Space Camera: 해당 카메라 기준으로 변환
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
                        worldPos = canvas.worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, canvas.worldCamera.nearClipPlane + 1f));
                    }
                    else
                    {
                        // World Space: 직접 사용
                        worldPos = rectTransform.position;
                    }

                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 슬롯 월드 위치 계산: {slot.name} -> {worldPos}");

                    return worldPos;
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning($"[MonsterSpawnManager] {slot.name}의 Canvas를 찾을 수 없습니다.");
                }
            }

            return slot.position;
        }

        /// <summary>
        /// 몬스터 체력바 위치 업데이트
        /// </summary>
        private void UpdateMonsterHealthBarPosition(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return;

            // WorldSpace Canvas 체력바 찾기 (프리팹에서 생성된 경우)
            Transform healthBarCanvas = monster.transform.Find("HealthBarCanvas");
            if (healthBarCanvas != null)
            {
                // Canvas가 이미 부모-자식 관계로 설정되어 있으므로 localPosition 사용
                // 몬스터의 스프라이트 크기를 고려한 위치 조정
                SpriteRenderer spriteRenderer = monster.GetComponent<SpriteRenderer>();
                float spriteHeight = spriteRenderer != null && spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.bounds.size.y * monster.transform.localScale.y
                    : 1f;

                // 체력바를 스프라이트 아래쪽에 배치
                healthBarCanvas.localPosition = new Vector3(0, -(spriteHeight * 0.5f + 0.3f), 0);

                // Canvas가 카메라를 바라보도록 설정
                Canvas canvas = healthBarCanvas.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        Vector3 directionToCamera = mainCamera.transform.position - healthBarCanvas.position;
                        directionToCamera.y = 0; // Y축 회전만 적용하여 위아래로 기울어지지 않게 함
                        if (directionToCamera != Vector3.zero)
                        {
                            healthBarCanvas.rotation = Quaternion.LookRotation(-directionToCamera);
                        }
                    }
                }

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] WorldSpace 체력바 위치 업데이트: {monster.name} -> 로컬위치: {healthBarCanvas.localPosition}, 월드위치: {healthBarCanvas.position}");
            }

            // TextMesh 체력 텍스트 찾기 (기본 생성된 경우 - 하위 호환성)
            Transform healthText = monster.transform.Find("HealthText");
            if (healthText != null)
            {
                // 이미 부모-자식 관계로 연결되어 있으므로 localPosition으로 조정
                healthText.localPosition = new Vector3(0, 1.2f, 0);

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] TextMesh 체력 텍스트 위치 업데이트: {monster.name} -> {healthText.position}");
            }
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

                // 체력바도 함께 이동
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    UpdateMonsterHealthBarPosition(enemy);
                }

                yield return null;
            }

            // 최종 위치 보장
            if (monster != null)
            {
                monster.transform.position = targetPosition;
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    UpdateMonsterHealthBarPosition(enemy);
                }
            }
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
            foreach (var monsterObj in spawnedMonsters)
            {
                if (monsterObj == null) continue;

                var monster = monsterObj.GetComponent<Maglin.Enemy.Enemy>();
                if (monster == null || !monster.IsAlive || monster == excludeMonster) continue;

                occupiedPositions.Add(monster.GridPosition);
            }

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 점유된 위치들: {string.Join(", ", occupiedPositions)}");

            return occupiedPositions;
        }

        /// <summary>
        /// 겹치지 않는 스폰 위치 찾기
        /// </summary>
        private Vector2Int FindAvailableSpawnPosition(Vector2Int preferredPosition)
        {
            // 현재 점유된 위치들 수집 (스폰 시이므로 제외할 몬스터 없음)
            var occupiedPositions = GetOccupiedPositions(null);

            // 선호 위치가 비어있으면 그대로 사용
            if (!occupiedPositions.Contains(preferredPosition))
            {
                return preferredPosition;
            }

            // GridFieldManager를 통해 유효한 그리드 위치 찾기
            int maxWidth = GridFieldManager.Instance?.GridWidth ?? 10;

            // 1번 위치부터 마지막 위치까지 비어있는 곳 찾기
            for (int x = 1; x < maxWidth; x++)
            {
                var candidatePos = new Vector2Int(x, 0);
                if (!occupiedPositions.Contains(candidatePos))
                {
                    if (debugMode)
                        Debug.Log($"[MonsterSpawnManager] 대체 스폰 위치 찾음: {candidatePos}");
                    return candidatePos;
                }
            }

            // 모든 위치가 점유된 경우 (거의 없을 상황이지만) 원래 위치 반환
            if (debugMode)
                Debug.LogWarning($"[MonsterSpawnManager] 사용 가능한 스폰 위치를 찾을 수 없음, 원래 위치 사용: {preferredPosition}");
            return preferredPosition;
        }

        /// <summary>
        /// 스폰 애니메이션 처리
        /// </summary>
        private void ProcessSpawnAnimations()
        {
            if (pendingAnimationMonsters.Count == 0) return;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 스폰 애니메이션 처리 시작: {pendingAnimationMonsters.Count}마리");

            // 애니메이션 매니저 초기화 확인
            if (MonsterSpawnAnimationManager.Instance == null)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterSpawnManager] MonsterSpawnAnimationManager가 없어 애니메이션 없이 진행");
                
                // 애니메이션 없이 모든 몬스터 활성화
                FallbackActivateAllMonsters();
                return;
            }

            // 애니메이션 이벤트 구독
            MonsterSpawnAnimationManager.OnSpawnAnimationCompleted += OnMonsterSpawnAnimationCompleted;
            MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted += OnAllSpawnAnimationsCompleted;

            waitingForAnimationCount = pendingAnimationMonsters.Count;

            if (useSequentialSpawn)
            {
                // 순차적 스폰 애니메이션
                MonsterSpawnAnimationManager.Instance.PlaySequentialSpawnAnimations(new List<GameObject>(pendingAnimationMonsters));
            }
            else
            {
                // 동시 스폰 애니메이션
                foreach (var monster in pendingAnimationMonsters)
                {
                    if (monster != null)
                    {
                        monster.SetActive(true);
                        MonsterSpawnAnimationManager.Instance.PlaySpawnAnimation(monster);
                    }
                }
            }

            pendingAnimationMonsters.Clear();
        }

        /// <summary>
        /// 개별 몬스터 스폰 애니메이션 완료 처리
        /// </summary>
        private void OnMonsterSpawnAnimationCompleted(GameObject monster)
        {
            if (monster == null) return;

            var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
            if (enemy != null)
            {
                // TargetManager에 몬스터 추가
                if (TargetManager.Instance != null)
                {
                    TargetManager.Instance.AddMonster(monster);
                    
                    // 첫 번째 몬스터라면 타겟으로 설정
                    if (TargetManager.Instance.CurrentTarget == null)
                    {
                        TargetManager.Instance.SetTarget(enemy);
                    }
                }

                // 몬스터 스폰 이벤트 발생
                OnMonsterSpawned?.Invoke(enemy);

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 스폰 애니메이션 완료");
            }

            waitingForAnimationCount--;
        }

        /// <summary>
        /// 모든 몬스터 스폰 애니메이션 완료 처리
        /// </summary>
        private void OnAllSpawnAnimationsCompleted()
        {
            // 이벤트 구독 해제
            MonsterSpawnAnimationManager.OnSpawnAnimationCompleted -= OnMonsterSpawnAnimationCompleted;
            MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted -= OnAllSpawnAnimationsCompleted;

            waitingForAnimationCount = 0;

            // TargetManager 업데이트 (타겟 마커 표시 등)
            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.ValidateTarget();
                
                // 타겟이 설정되지 않았다면 가장 가까운 몬스터를 타겟으로 설정
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                }

                // 전투 준비 완료 (타겟 마커 활성화)
                TargetManager.Instance.CompleteBattleInitialization();
            }

            // 모든 몬스터 스폰 완료 이벤트 발생
            OnAllMonstersSpawned?.Invoke();

            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 모든 몬스터 스폰 애니메이션 완료");
        }

        /// <summary>
        /// 애니메이션 매니저가 없을 때 대체 활성화
        /// </summary>
        private void FallbackActivateAllMonsters()
        {
            foreach (var monster in pendingAnimationMonsters)
            {
                if (monster != null)
                {
                    monster.SetActive(true);

                    var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        // TargetManager에 몬스터 추가
                        if (TargetManager.Instance != null)
                        {
                            TargetManager.Instance.AddMonster(monster);
                        }

                        // 몬스터 스폰 이벤트 발생
                        OnMonsterSpawned?.Invoke(enemy);
                    }
                }
            }

            pendingAnimationMonsters.Clear();

            // TargetManager 전투 준비 완료
            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.ValidateTarget();
                
                if (TargetManager.Instance.CurrentTarget == null)
                {
                    TargetManager.Instance.SetTargetToClosest();
                }

                TargetManager.Instance.CompleteBattleInitialization();
            }

            OnAllMonstersSpawned?.Invoke();
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
                Debug.Log($"[MonsterSpawnManager] 플레이어 그리드 위치: {gridPosition}");
        }

        /// <summary>
        /// 스폰된 몬스터들 정리
        /// </summary>
        public void ClearSpawnedMonsters()
        {
            // 진행 중인 애니메이션 중단
            if (MonsterSpawnAnimationManager.Instance != null)
            {
                MonsterSpawnAnimationManager.Instance.StopAllSpawnAnimations();
            }

            // 애니메이션 이벤트 구독 해제
            MonsterSpawnAnimationManager.OnSpawnAnimationCompleted -= OnMonsterSpawnAnimationCompleted;
            MonsterSpawnAnimationManager.OnAllSpawnAnimationsCompleted -= OnAllSpawnAnimationsCompleted;

            // 대기 중인 몬스터들 정리
            pendingAnimationMonsters.Clear();
            waitingForAnimationCount = 0;

            foreach (var monster in spawnedMonsters)
            {
                if (monster != null)
                {
                    // 이벤트 구독 해제
                    var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        if (debugMode)
                            Debug.Log($"[MonsterSpawnManager] {enemy.EnemyName} 정리 중...");
                    }

                    Destroy(monster);
                }
            }
            spawnedMonsters.Clear();

            if (debugMode)
                Debug.Log("[MonsterSpawnManager] 모든 몬스터 정리 완료");
        }

        /// <summary>
        /// 스폰 애니메이션 설정 변경
        /// </summary>
        public void SetSpawnAnimationEnabled(bool enabled)
        {
            enableSpawnAnimations = enabled;
            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 스폰 애니메이션 {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 순차 스폰 설정 변경
        /// </summary>
        public void SetSequentialSpawnEnabled(bool enabled)
        {
            useSequentialSpawn = enabled;
            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] 순차 스폰 {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 대기 중인 애니메이션 강제 처리 (BattleInitializationSequence에서 호출)
        /// </summary>
        public void ProcessPendingAnimations()
        {
            if (pendingAnimationMonsters.Count > 0)
            {
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] 대기 중인 애니메이션 강제 처리: {pendingAnimationMonsters.Count}마리");
                ProcessSpawnAnimations();
            }
        }

        /// <summary>
        /// 애니메이션 대기 중인지 확인
        /// </summary>
        public bool IsWaitingForAnimations => waitingForAnimationCount > 0 || pendingAnimationMonsters.Count > 0;

        /// <summary>
        /// 몬스터 이동 처리
        /// </summary>
        public IEnumerator ProcessMonsterMovement(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive) yield break;

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 현재 다른 몬스터들의 위치 수집 (자기 자신 제외)
            var occupiedPositions = GetOccupiedPositions(monster);

            // AI가 충돌 고려한 이동 결정
            var newPosition = ai.DecideMovementWithCollision(playerGridPosition, occupiedPositions);

            if (newPosition != monster.GridPosition)
            {
                // 위치 이동 (SetPosition 메서드 사용)
                SetMonsterGridPosition(monster, newPosition);
                UpdateMonsterPosition(monster);

                if (debugMode)
                {
                    var movementPattern = monster.EnemyData?.MovementPattern.ToString() ?? "Unknown";
                    Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 위치 이동: {monster.GridPosition} -> {newPosition} (패턴: {movementPattern})");
                }

                // 부드러운 이동 완료까지 대기
                yield return new WaitForSeconds(0.6f);
            }
            else
            {
                // 이동하지 않은 경우 짧은 대기
                if (debugMode)
                {
                    var movementPattern = monster.EnemyData?.MovementPattern.ToString() ?? "Unknown";
                    Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 이동 불가 (패턴: {movementPattern}, 현재 위치: {monster.GridPosition})");
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// 몬스터 공격 처리
        /// </summary>
        public IEnumerator ProcessMonsterAttack(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive) yield break;

            var ai = monster.GetComponent<EnemyAI>();
            if (ai == null) yield break;

            // 플레이어 공격 가능한지 확인
            if (ai.CanAttackPosition(playerGridPosition))
            {
                var damage = monster.CurrentAttackDamage;

                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {monster.EnemyName}이 플레이어를 공격! 데미지: {damage}");

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

                // 플레이어에게 데미지
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.TakeDamage(damage);
                }

                // 공격 후 잠시 대기
                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 공격 범위 밖");
            }
        }

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
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 돌격 공격 시작: {originalPosition} -> {targetWorldPos}");

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
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 돌격 공격 완료");
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

                // 체력바도 함께 이동
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    UpdateMonsterHealthBarPosition(enemy);
                }

                yield return null;
            }

            // 최종 위치 보장
            if (monster != null)
            {
                monster.transform.position = targetPosition;
                var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                if (enemy != null)
                {
                    UpdateMonsterHealthBarPosition(enemy);
                }
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
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 원거리 공격 시작: {originalPosition}");

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
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 원거리 공격 완료");
        }

        /// <summary>
        /// 특수 공격 애니메이션 실행
        /// </summary>
        private IEnumerator PerformSpecialAttack(Maglin.Enemy.Enemy monster, Vector2Int targetPosition)
        {
            if (monster == null) yield break;

            Vector3 originalPosition = monster.transform.position;

            if (debugMode)
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 특수 공격 시작");

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
                Debug.Log($"[MonsterSpawnManager] {monster.EnemyName} 특수 공격 완료");
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
    }
}