using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maglin.Cards;
using Maglin.Enemy;
using Maglin.Player;

namespace Maglin.Battle
{
    /// <summary>
    /// VFX 이펙트 인스턴스 정보
    /// </summary>
    public class VFXInstance
    {
        public GameObject effectObject;
        public VFXEffectSO effectData;
        public Transform[] targets;
        public Card sourceCard;
        public float startTime;
        public bool isActive;
        public int currentHitIndex;
        public int vfxIndex; // 동일 카드에서 몇 번째 VFX인지 (0부터 시작)
        public int totalVFXCount; // 동일 카드의 전체 VFX 개수

        public VFXInstance(GameObject effectObject, VFXEffectSO effectData, Transform[] targets, Card sourceCard, int vfxIndex = 0, int totalVFXCount = 1)
        {
            this.effectObject = effectObject;
            this.effectData = effectData;
            this.targets = targets;
            this.sourceCard = sourceCard;
            this.startTime = Time.time;
            this.isActive = true;
            this.currentHitIndex = 0;
            this.vfxIndex = vfxIndex;
            this.totalVFXCount = totalVFXCount;
        }
    }

    /// <summary>
    /// VFX 히트 이벤트 데이터
    /// </summary>
    public class VFXHitEventArgs : EventArgs
    {
        public Card sourceCard;
        public Transform[] targets;
        public HitTiming hitTiming;
        public VFXEffectSO effectData;
        public int vfxIndex; // 동일 카드에서 몇 번째 VFX인지
        public int totalVFXCount; // 동일 카드의 전체 VFX 개수

        public VFXHitEventArgs(Card sourceCard, Transform[] targets, HitTiming hitTiming, VFXEffectSO effectData, int vfxIndex = 0, int totalVFXCount = 1)
        {
            this.sourceCard = sourceCard;
            this.targets = targets;
            this.hitTiming = hitTiming;
            this.effectData = effectData;
            this.vfxIndex = vfxIndex;
            this.totalVFXCount = totalVFXCount;
        }
    }

    /// <summary>
    /// VFX 이펙트를 관리하는 매니저
    /// </summary>
    public class VFXEffectManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static VFXEffectManager _instance;

        public static VFXEffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VFXEffectManager>();

                    if (_instance == null)
                    {
                        GameObject vfxManagerObject = new GameObject("VFXEffectManager");
                        _instance = vfxManagerObject.AddComponent<VFXEffectManager>();
                        DontDestroyOnLoad(vfxManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// VFX 히트 이벤트 (데미지 적용 시점)
        /// </summary>
        public static event Action<VFXHitEventArgs> OnVFXHit;

        /// <summary>
        /// VFX 시작 이벤트
        /// </summary>
        public static event Action<VFXInstance> OnVFXStarted;

        /// <summary>
        /// VFX 종료 이벤트
        /// </summary>
        public static event Action<VFXInstance> OnVFXEnded;

        /// <summary>
        /// 카드의 모든 VFX와 공격이 완료되었을 때 발생하는 이벤트
        /// </summary>
        public static event Action<Card> OnCardAttackSequenceCompleted;
        #endregion

        #region Fields
        [Header("설정")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private Transform vfxParent; // VFX 오브젝트들의 부모 Transform

        [Header("레이어 설정")]
        [SerializeField] private string vfxSortingLayer = "VFX"; // VFX용 Sorting Layer
        [SerializeField] private int vfxOrderInLayer = 10; // VFX의 Order in Layer

        // 활성 VFX 인스턴스들
        private List<VFXInstance> activeVFXInstances = new List<VFXInstance>();

        // 카드별 진행중인 VFX 추적
        private Dictionary<Card, List<VFXInstance>> cardVFXTracker = new Dictionary<Card, List<VFXInstance>>();

        // 카드별 체인 공격 진행 추적 (ChainFrontHits용)
        private Dictionary<Card, bool> cardChainAttackInProgress = new Dictionary<Card, bool>();

        // 컴포넌트 참조
        private TargetManager targetManager;

        // 투사체 관련
        private List<ProjectileController> activeProjectiles = new List<ProjectileController>();
        private Dictionary<Card, List<ProjectileController>> cardProjectileTracker = new Dictionary<Card, List<ProjectileController>>();
        #endregion

        #region Public Methods
        /// <summary>
        /// VFX가 없는 카드의 공격 시퀀스 즉시 완료 처리
        /// </summary>
        public void NotifyCardWithoutVFXCompleted(Card card)
        {
            if (card == null) return;

            if (debugMode)
                Debug.Log($"[VFXEffectManager] VFX 없는 카드 {card.CardName} 즉시 완료 처리");

            OnCardAttackSequenceCompleted?.Invoke(card);
        }
        #endregion

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 컴포넌트 참조 획득
            targetManager = TargetManager.Instance;

            // VFX 부모 오브젝트 설정
            if (vfxParent == null)
            {
                GameObject vfxParentObj = new GameObject("VFX_Parent");
                vfxParent = vfxParentObj.transform;
                vfxParentObj.transform.SetParent(transform);
            }
        }

        private void Update()
        {
            // 활성 VFX 인스턴스들 업데이트
            UpdateActiveVFXInstances();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 카드의 VFX 이펙트 실행
        /// </summary>
        public void PlayCardVFX(Card card, Transform[] specificTargets = null)
        {
            if (card?.CardData?.Effect == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] 카드에 VFX 이펙트가 없습니다: {card?.CardData?.CardName}");
                return;
            }

            var vfxData = card.CardData.Effect;

            if (!vfxData.IsValid)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] 유효하지 않은 VFX 데이터: {vfxData.EffectName}");
                return;
            }

            // 투사체인 경우 특별 처리
            if (vfxData.IsProjectile)
            {
                PlayProjectileVFX(card, vfxData, specificTargets);
                return;
            }

            // 타겟 결정
            Transform[] targets = DetermineTargets(card, vfxData, specificTargets);

            var targetTypeForVfx = vfxData.GetTargetType(card.CardData.Target);

            // ChainFrontHits 특별 처리: 단일 VFX를 순차적으로 반복 재생
            if (targetTypeForVfx == TargetType.ChainFrontHits)
            {
                PlayChainFrontHitsVFX(card, vfxData);
                return;
            }

            // PlayerFrontLine: ShowVFXPerTarget이 꺼진 경우에만 앞칸 고정 위치 1회 재생
            if (targetTypeForVfx == TargetType.PlayerFrontLine && !card.CardData.ShowVFXPerTarget)
            {
                Vector3 anchor = GetPlayerFrontAnchorWorldPosition();
                PlayVFXAtPosition(vfxData, anchor, card);
                return;
            }

            // TargetCenteredRange: ShowVFXPerTarget이 꺼진 경우 실제 타겟 중심에 VFX 재생
            if (targetTypeForVfx == TargetType.TargetCenteredRange && !card.CardData.ShowVFXPerTarget)
            {
                if (targetManager != null && targetManager.IsTargetValid())
                {
                    Transform centerTarget = targetManager.CurrentTarget.transform;
                    CreateAndPlayVFX(vfxData, new Transform[] { centerTarget }, card);
                    return;
                }
            }

            if (targets == null || targets.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] VFX 타겟을 찾을 수 없습니다: {card.CardData.CardName}");
                return;
            }

            // VFX 인스턴스 생성 및 실행
            // 카드 옵션에 따라 타겟마다 하나씩 vs 대표 위치 하나만
            if (card.CardData.ShowVFXPerTarget)
            {
                CreateAndPlayVFX(vfxData, targets, card);
            }
            else
            {
                // 대표 위치: 
                // - SingleEnemy: 첫 타겟
                // - TargetFront/BackStrip: 현재 타겟 위치
                // - PlayerFrontLine: 플레이어 앞 첫 칸 (또는 가장 앞 적 위치가 있으면 그곳)
                Transform rep = null;
                if (targets != null && targets.Length > 0)
                {
                    rep = targets[0];
                }
                else if (targetManager != null && targetManager.IsTargetValid())
                {
                    rep = targetManager.CurrentTarget.transform;
                }

                if (rep != null)
                {
                    CreateAndPlayVFX(vfxData, new Transform[] { rep }, card);
                }
            }
        }

        /// <summary>
        /// 특정 위치에 VFX 이펙트 실행
        /// </summary>
        public void PlayVFXAtPosition(VFXEffectSO vfxData, Vector3 position, Card sourceCard = null)
        {
            if (vfxData == null || !vfxData.IsValid) return;

            // 임시 타겟 오브젝트 생성
            GameObject tempTarget = new GameObject("TempVFXTarget");
            tempTarget.transform.position = position;

            Transform[] targets = { tempTarget.transform };

            var vfxInstance = CreateAndPlayVFX(vfxData, targets, sourceCard);

            // VFX 종료 시 임시 오브젝트 삭제
            StartCoroutine(DestroyTempTargetAfterVFX(tempTarget, vfxData.EffectDuration));
        }

        /// <summary>
        /// 활성 VFX 모두 정리
        /// </summary>
        public void ClearAllVFX()
        {
            foreach (var vfxInstance in activeVFXInstances)
            {
                if (vfxInstance.effectObject != null)
                {
                    Destroy(vfxInstance.effectObject);
                }
            }
            activeVFXInstances.Clear();

            if (debugMode)
                Debug.Log("[VFXEffectManager] 모든 VFX 정리 완료");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            if (debugMode)
                Debug.Log("[VFXEffectManager] VFX 이펙트 매니저 초기화");
        }

        /// <summary>
        /// VFX 타겟 결정
        /// </summary>
        private Transform[] DetermineTargets(Card card, VFXEffectSO vfxData, Transform[] specificTargets = null)
        {
            // 특정 타겟이 지정된 경우 우선 사용
            if (specificTargets != null && specificTargets.Length > 0)
                return specificTargets;

            // VFX 데이터에서 타겟팅 오버라이드하는 경우
            TargetType targetType = vfxData.GetTargetType(card.CardData.Target);
            int targetCount = vfxData.GetTargetCount(card.CardData.TargetCount);

            // TargetManager를 통해 타겟 결정
            if (targetManager != null)
            {
                return GetTargetsFromTargetManager(targetType, targetCount);
            }

            // TargetManager가 없는 경우 기본 타겟팅 로직
            return GetDefaultTargets(targetType, targetCount);
        }

        /// <summary>
        /// TargetManager를 통한 타겟 결정
        /// </summary>
        private Transform[] GetTargetsFromTargetManager(TargetType targetType, int targetCount)
        {
            var targets = new List<Transform>();

            switch (targetType)
            {
                case TargetType.SingleEnemy:
                    if (targetManager.CurrentTarget != null)
                    {
                        targets.Add(targetManager.CurrentTarget.transform);
                    }
                    break;

                case TargetType.AllEnemies:
                    var allEnemies = FindObjectsOfType<Enemy.Enemy>();
                    foreach (var enemy in allEnemies)
                    {
                        if (enemy.CurrentState != EnemyState.Dead)
                        {
                            targets.Add(enemy.transform);
                        }
                    }
                    break;

                case TargetType.AllIncludingSelf:
                    // 모든 적 + 플레이어
                    foreach (var enemy in FindObjectsOfType<Enemy.Enemy>())
                    {
                        if (enemy.CurrentState != EnemyState.Dead)
                            targets.Add(enemy.transform);
                    }
                    var playerInc = FindObjectOfType<Maglin.Player.PlayerManager>();
                    if (playerInc != null) targets.Add(playerInc.transform);
                    break;

                case TargetType.Self:
                    var player = FindObjectOfType<Maglin.Player.PlayerManager>();
                    if (player != null)
                    {
                        targets.Add(player.transform);
                    }
                    break;

                case TargetType.ChainFrontHits:
                    // 가장 앞의 적 1명만 타겟으로 VFX 생성 (히트마다 데미지는 별도 처리)
                    var frontmostEnemy = GetFrontmostEnemyTransform();
                    if (frontmostEnemy != null) targets.Add(frontmostEnemy);
                    break;

                case TargetType.PlayerFrontLine:
                    // 플레이어 앞 range칸 내 모든 적
                    targets.AddRange(GetLineTargetsFromPlayer(targetCount));
                    break;

                case TargetType.TargetFrontStrip:
                    // 타겟 포함 왼쪽으로 range칸
                    targets.AddRange(GetStripFromTarget(includeFront: true, range: targetCount));
                    break;

                case TargetType.TargetBackStrip:
                    // 타겟 포함 오른쪽으로 range칸
                    targets.AddRange(GetStripFromTarget(includeFront: false, range: targetCount));
                    break;

                case TargetType.TargetCenteredRange:
                    // 타겟 중심으로 앞뒤로 range칸씩
                    targets.AddRange(GetCenteredRangeFromTarget(targetCount));
                    break;

            }

            return targets.ToArray();
        }

        /// <summary>
        /// 기본 타겟팅 로직
        /// </summary>
        private Transform[] GetDefaultTargets(TargetType targetType, int targetCount)
        {
            var targets = new List<Transform>();

            switch (targetType)
            {
                case TargetType.AllEnemies:
                    var allEnemies = FindObjectsOfType<Enemy.Enemy>();
                    foreach (var enemy in allEnemies)
                    {
                        if (enemy.CurrentState != EnemyState.Dead)
                        {
                            targets.Add(enemy.transform);
                        }
                    }
                    break;

                case TargetType.Self:
                    var player = FindObjectOfType<Maglin.Player.PlayerManager>();
                    if (player != null)
                    {
                        targets.Add(player.transform);
                    }
                    break;
            }

            return targets.ToArray();
        }

        /// <summary>
        /// 가장 앞 몬스터의 Transform 반환
        /// </summary>
        private Transform GetFrontmostEnemyTransform()
        {
            if (targetManager == null) return null;

            var aliveEnemies = targetManager.GetAliveEnemies();
            if (aliveEnemies.Count == 0) return null;

            // 가장 앞의 적 (X 좌표가 가장 작은 적)
            return aliveEnemies[0].transform;
        }

        private Vector3 GetPlayerFrontAnchorWorldPosition()
        {
            // 기준: 플레이어 Transform + (1, 0) 오프셋 (셀 크기 1 가정)
            var player = FindObjectOfType<PlayerManager>();
            if (player != null)
            {
                var pos = player.transform.position;
                return new Vector3(pos.x + 1f, pos.y, pos.z);
            }

            // 폴백: (0,0) 기준 앞칸
            return new Vector3(1f, 0f, 0f);
        }

        // 플레이어 앞 라인 범위 내 적 타겟
        private IEnumerable<Transform> GetLineTargetsFromPlayer(int range)
        {
            var result = new List<Transform>();
            int px = targetManager != null ? targetManager.PlayerGridPosition.x : 0;
            foreach (var enemy in FindObjectsOfType<Enemy.Enemy>())
            {
                if (enemy.CurrentState == EnemyState.Dead) continue;
                if (enemy.GridPosition.x > px && enemy.GridPosition.x <= px + range)
                    result.Add(enemy.transform);
            }
            return result;
        }

        // 타겟 기준 스트립 (왼쪽/오른쪽 포함)
        private IEnumerable<Transform> GetStripFromTarget(bool includeFront, int range)
        {
            var result = new List<Transform>();
            if (targetManager == null || !targetManager.IsTargetValid()) return result;

            int tx = targetManager.CurrentTarget.GridPosition.x;
            int px = targetManager.PlayerGridPosition.x;

            int minX, maxX;
            if (includeFront)
            {
                minX = Mathf.Max(px + 1, tx - (range - 1));
                maxX = tx;
            }
            else
            {
                minX = tx;
                maxX = tx + (range - 1);
            }

            foreach (var enemy in FindObjectsOfType<Enemy.Enemy>())
            {
                if (enemy.CurrentState == EnemyState.Dead) continue;
                if (enemy.GridPosition.x >= minX && enemy.GridPosition.x <= maxX)
                    result.Add(enemy.transform);
            }

            return result;
        }

        /// <summary>
        /// 타겟 중심으로 앞뒤로 range칸씩 범위의 적들을 가져옴
        /// </summary>
        private IEnumerable<Transform> GetCenteredRangeFromTarget(int range)
        {
            var result = new List<Transform>();
            if (targetManager == null || !targetManager.IsTargetValid()) return result;

            int tx = targetManager.CurrentTarget.GridPosition.x;
            int minX = tx - range;
            int maxX = tx + range;

            foreach (var enemy in FindObjectsOfType<Enemy.Enemy>())
            {
                if (enemy.CurrentState == EnemyState.Dead) continue;
                if (enemy.GridPosition.x >= minX && enemy.GridPosition.x <= maxX)
                    result.Add(enemy.transform);
            }

            return result;
        }

        /// <summary>
        /// 투사체 VFX 실행
        /// </summary>
        private void PlayProjectileVFX(Card card, VFXEffectSO vfxData, Transform[] specificTargets = null)
        {
            if (card == null || vfxData == null) return;

            // 플레이어 위치 가져오기
            Transform playerTransform = GetPlayerTransform();
            if (playerTransform == null)
            {
                if (debugMode)
                    Debug.LogError("[VFXEffectManager] 플레이어 Transform을 찾을 수 없습니다!");
                return;
            }

            // 타겟 결정
            Transform[] targets = DetermineTargets(card, vfxData, specificTargets);
            if (targets == null || targets.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] 투사체 타겟을 찾을 수 없습니다: {card.CardData.CardName}");
                return;
            }

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 VFX 시작: {card.CardData.CardName} -> {targets.Length}개 타겟");

            // 카드별 투사체 추적 초기화
            if (!cardProjectileTracker.ContainsKey(card))
            {
                cardProjectileTracker[card] = new List<ProjectileController>();
            }

            // 투사체 설정 배열이 있는 경우 각 설정대로 투사체 생성
            if (vfxData.HasValidProjectileConfigs)
            {
                var projectileConfigs = vfxData.ProjectileConfigs;

                // 각 타겟에 대해 모든 투사체 설정으로 투사체 생성 (딜레이 적용)
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    for (int configIndex = 0; configIndex < projectileConfigs.Length; configIndex++)
                    {
                        var config = projectileConfigs[configIndex];
                        if (config.LaunchDelay > 0f)
                        {
                            // 딜레이가 있는 경우 코루틴으로 지연 발사
                            StartCoroutine(LaunchProjectileWithDelay(playerTransform, targets[targetIndex], vfxData, card, configIndex, config.LaunchDelay));
                        }
                        else
                        {
                            // 딜레이가 없는 경우 즉시 발사 (솟아오르는 애니메이션 적용)
                            if (config.EnableSpawnAnimation)
                            {
                                StartCoroutine(CreateAndLaunchProjectileWithSpawnAnimation(playerTransform, targets[targetIndex], vfxData, card, configIndex));
                            }
                            else
                            {
                                CreateAndLaunchProjectile(playerTransform, targets[targetIndex], vfxData, card, configIndex);
                            }
                        }
                    }
                }
            }
            else
            {
                // 레거시 지원: 단일 투사체 설정
                for (int i = 0; i < targets.Length; i++)
                {
                    CreateAndLaunchProjectile(playerTransform, targets[i], vfxData, card, 0);
                }
            }
        }

        /// <summary>
        /// 딜레이 후 투사체 발사 코루틴
        /// </summary>
        private System.Collections.IEnumerator LaunchProjectileWithDelay(Transform startPos, Transform target, VFXEffectSO vfxData, Card sourceCard, int configIndex, float delay)
        {
            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 딜레이 대기 중: {delay}초");

            yield return new WaitForSeconds(delay);

            // 딜레이 후에도 타겟이 유효한지 확인
            if (target != null && sourceCard != null)
            {
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 딜레이 완료, 발사 시작");

                // 솟아오르는 애니메이션 적용해서 투사체 생성
                var projectileConfig = vfxData.GetProjectileConfig(configIndex);
                if (projectileConfig.EnableSpawnAnimation)
                {
                    StartCoroutine(CreateAndLaunchProjectileWithSpawnAnimation(startPos, target, vfxData, sourceCard, configIndex));
                }
                else
                {
                    CreateAndLaunchProjectile(startPos, target, vfxData, sourceCard, configIndex);
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[VFXEffectManager] 투사체 {configIndex} 딜레이 완료 후 타겟 또는 카드가 유효하지 않음");
            }
        }

        /// <summary>
        /// 솟아오르는 애니메이션과 함께 투사체 생성 및 발사
        /// </summary>
        private System.Collections.IEnumerator CreateAndLaunchProjectileWithSpawnAnimation(Transform startPos, Transform target, VFXEffectSO vfxData, Card sourceCard, int configIndex)
        {
            if (vfxData?.EffectPrefab == null || target == null) yield break;

            var projectileConfig = vfxData.GetProjectileConfig(configIndex);

            // 최종 목표 위치 계산 (StartOffset + SpawnHeightOffset 모두 적용)
            Vector3 finalPosition = startPos.position;
            if (projectileConfig.StartOffset != Vector3.zero)
            {
                finalPosition += projectileConfig.StartOffset;
            }
            // SpawnHeightOffset도 최종 위치에 포함 (발사 시작점이 됨)
            if (projectileConfig.SpawnHeightOffset != 0f)
            {
                finalPosition += Vector3.up * projectileConfig.SpawnHeightOffset;
            }

            // 시작 위치 계산 (최종 위치에서 추가로 아래에서 시작하여 솟아오름)
            Vector3 startPosition = finalPosition - Vector3.up * Mathf.Abs(projectileConfig.SpawnHeightOffset);

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 솟아오르는 애니메이션 시작: {startPosition} -> {finalPosition}");

            // 투사체 오브젝트 생성 (시작 위치에서)
            Quaternion spawnRotation = Quaternion.Euler(vfxData.RotationOffset);
            GameObject projectileObj = Instantiate(vfxData.EffectPrefab, startPosition, spawnRotation, vfxParent);

            // 투사체 스케일 적용 (애니메이션 시작 전에)
            projectileObj.transform.localScale = projectileConfig.Scale;

            // Gabriel Productions ProjectileMoveScript에서 정보 추출
            GameObject muzzlePrefab = null;
            GameObject hitPrefab = null;
            ExtractProjectileMoveScriptData(projectileObj, out muzzlePrefab, out hitPrefab);

            // 투사체 초기 투명도 설정 (페이드인을 위해)
            SetProjectileAlpha(projectileObj, 0.3f);

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 스케일 적용: {projectileConfig.Scale}");

            // VFX 레이어 설정
            SetVFXLayer(projectileObj);

            // ProjectileController 컴포넌트 추가 및 설정
            ProjectileController projectileController = projectileObj.GetComponent<ProjectileController>();
            if (projectileController == null)
            {
                projectileController = projectileObj.AddComponent<ProjectileController>();
            }

            // Collider 설정 - 기존 Collider가 있으면 Trigger로 설정, 없으면 추가
            Collider existingCollider3D = projectileObj.GetComponent<Collider>();
            Collider2D existingCollider2D = projectileObj.GetComponent<Collider2D>();

            if (existingCollider3D != null)
            {
                // 기존 3D Collider가 있으면 Trigger로 설정
                existingCollider3D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 3D Collider를 Trigger로 설정: {existingCollider3D.GetType().Name}");
            }
            else if (existingCollider2D != null)
            {
                // 기존 2D Collider가 있으면 Trigger로 설정
                existingCollider2D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 2D Collider를 Trigger로 설정: {existingCollider2D.GetType().Name}");
            }
            else
            {
                // Collider가 없으면 2D Collider 추가 (몬스터가 2D이므로)
                CircleCollider2D collider2D = projectileObj.AddComponent<CircleCollider2D>();
                collider2D.isTrigger = true;
                collider2D.radius = 0.5f;
                if (debugMode)
                    Debug.Log("[VFXEffectManager] 새로운 2D Collider 추가");
            }

            // 투사체 히트 이벤트 등록
            projectileController.OnProjectileHit += OnProjectileHit;

            // 카드별 투사체 추적에 추가
            if (!cardProjectileTracker.ContainsKey(sourceCard))
            {
                cardProjectileTracker[sourceCard] = new List<ProjectileController>();
            }
            cardProjectileTracker[sourceCard].Add(projectileController);
            activeProjectiles.Add(projectileController);

            // 솟아오르는 애니메이션 실행
            float elapsedTime = 0f;
            float duration = projectileConfig.SpawnAnimationDuration;

            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                float smoothProgress = Mathf.SmoothStep(0.3f, 1f, progress);

                // 위치 보간 (부드럽게 위로 이동)
                Vector3 currentPosition = Vector3.Lerp(startPosition, finalPosition, smoothProgress);
                projectileObj.transform.position = currentPosition;

                // 투명도 보간 (페이드인)
                SetProjectileAlpha(projectileObj, smoothProgress);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 최종 위치와 투명도 설정
            projectileObj.transform.position = finalPosition;
            SetProjectileAlpha(projectileObj, 1f);

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 솟아오르는 애니메이션 완료");

            // Muzzle 이펙트 생성 (실제 발사 시점에, 최종 투사체 위치에서)
            if (muzzlePrefab != null)
            {
                CreateMuzzleEffect(startPos, vfxData, muzzlePrefab, projectileConfig);
            }

            // 사운드 재생
            if (vfxData.SoundEffect != null)
            {
                PlayVFXSound(vfxData.SoundEffect, finalPosition);
            }

            // 투사체 발사 (애니메이션 완료 후)
            projectileController.LaunchProjectile(projectileObj.transform, target, vfxData, sourceCard, OnProjectileComplete, hitPrefab, configIndex);

            if (debugMode)
            {
                Debug.Log($"[VFXEffectManager] 투사체 발사: {sourceCard.CardData.CardName} -> {target.name} (설정 {configIndex})");
                Debug.Log($"[VFXEffectManager] 투사체 최종 위치: {finalPosition}, 타겟 위치: {target.position}");
                Debug.Log($"[VFXEffectManager] 솟아오르는 애니메이션: {projectileConfig.EnableSpawnAnimation} (지속시간: {duration}초)");
            }
        }

        /// <summary>
        /// 투사체 생성 및 발사
        /// </summary>
        private void CreateAndLaunchProjectile(Transform startPos, Transform target, VFXEffectSO vfxData, Card sourceCard, int configIndex = 0)
        {
            if (vfxData?.EffectPrefab == null || target == null) return;

            // 해당 인덱스의 투사체 설정 가져오기
            var projectileConfig = vfxData.GetProjectileConfig(configIndex);

            // 투사체 오브젝트 생성 (개별 투사체 StartOffset + SpawnHeightOffset 적용)
            Vector3 spawnPosition = startPos.position;
            if (projectileConfig.StartOffset != Vector3.zero)
            {
                spawnPosition += projectileConfig.StartOffset;
            }
            if (projectileConfig.SpawnHeightOffset != 0f)
            {
                spawnPosition += Vector3.up * projectileConfig.SpawnHeightOffset;
            }
            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 {configIndex} 생성 위치: {startPos.position} + StartOffset({projectileConfig.StartOffset}) + HeightOffset({projectileConfig.SpawnHeightOffset}) = {spawnPosition}");
            Quaternion spawnRotation = Quaternion.Euler(vfxData.RotationOffset);

            GameObject projectileObj = Instantiate(vfxData.EffectPrefab, spawnPosition, spawnRotation, vfxParent);
            // 투사체는 ProjectileController에서 개별 ProjectileScale을 적용

            // Gabriel Productions ProjectileMoveScript에서 정보 추출
            GameObject muzzlePrefab = null;
            GameObject hitPrefab = null;
            ExtractProjectileMoveScriptData(projectileObj, out muzzlePrefab, out hitPrefab);

            // Muzzle 이펙트 생성 (projectileStartOffset 위치에)
            if (muzzlePrefab != null)
            {
                CreateMuzzleEffect(startPos, vfxData, muzzlePrefab, projectileConfig);
            }

            // ProjectileController 컴포넌트 추가 또는 가져오기
            ProjectileController projectileController = projectileObj.GetComponent<ProjectileController>();
            if (projectileController == null)
            {
                projectileController = projectileObj.AddComponent<ProjectileController>();
            }

            // Collider 설정 - 기존 Collider가 있으면 Trigger로 설정, 없으면 추가
            Collider existingCollider3D = projectileObj.GetComponent<Collider>();
            Collider2D existingCollider2D = projectileObj.GetComponent<Collider2D>();

            if (existingCollider3D != null)
            {
                // 기존 3D Collider가 있으면 Trigger로 설정
                existingCollider3D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 3D Collider를 Trigger로 설정: {existingCollider3D.GetType().Name}");
            }
            else if (existingCollider2D != null)
            {
                // 기존 2D Collider가 있으면 Trigger로 설정
                existingCollider2D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 2D Collider를 Trigger로 설정: {existingCollider2D.GetType().Name}");
            }
            else
            {
                // Collider가 없으면 2D Collider 추가 (몬스터가 2D이므로)
                CircleCollider2D collider2D = projectileObj.AddComponent<CircleCollider2D>();
                collider2D.isTrigger = true;
                collider2D.radius = 0.5f;
                if (debugMode)
                    Debug.Log("[VFXEffectManager] 새로운 2D Collider 추가");
            }

            // VFX 레이어 설정
            SetVFXLayer(projectileObj);

            // 투사체 히트 이벤트 등록
            if (projectileController != null)
            {
                projectileController.OnProjectileHit += OnProjectileHit;
            }
            else
            {
                Debug.LogError("[VFXEffectManager] ProjectileController가 null입니다!");
                return;
            }

            // 투사체 발사 (개별 투사체 설정과 함께)
            projectileController.LaunchProjectile(projectileObj.transform, target, vfxData, sourceCard, OnProjectileComplete, hitPrefab, configIndex);

            // 투사체 추적에 추가
            if (activeProjectiles != null)
            {
                activeProjectiles.Add(projectileController);
            }

            if (cardProjectileTracker != null && cardProjectileTracker.ContainsKey(sourceCard))
            {
                cardProjectileTracker[sourceCard].Add(projectileController);
            }

            // 사운드 재생
            if (vfxData.SoundEffect != null)
            {
                PlayVFXSound(vfxData.SoundEffect, spawnPosition);
            }

            if (debugMode)
            {
                Debug.Log($"[VFXEffectManager] 투사체 발사: {sourceCard.CardData.CardName} -> {target.name} (설정 {configIndex})");
                Debug.Log($"[VFXEffectManager] 투사체 위치: {spawnPosition}, 타겟 위치: {target.position}");
                Debug.Log($"[VFXEffectManager] 투사체 오브젝트: {projectileObj.name}");
                Debug.Log($"[VFXEffectManager] 투사체 설정: 딜레이={projectileConfig.LaunchDelay}초, 속도={projectileConfig.Speed}, 스케일={projectileConfig.Scale}");
                Debug.Log($"[VFXEffectManager] 타겟 오브젝트 컴포넌트: Enemy={target.GetComponent<Maglin.Enemy.Enemy>() != null}, Collider2D={target.GetComponent<Collider2D>() != null}");
            }
        }

        /// <summary>
        /// 투사체 히트 이벤트 처리
        /// </summary>
        private void OnProjectileHit(ProjectileController projectile, Transform target)
        {
            if (projectile?.SourceCard == null || projectile.VFXData == null) return;

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 히트: {projectile.SourceCard.CardData.CardName} -> {target?.name}");

            // 투사체가 타겟에 도달했으므로 즉시 데미지 적용
            var hitEventArgs = new VFXHitEventArgs(
                projectile.SourceCard,
                new Transform[] { target },
                new HitTiming(0f, true, 1.0f), // 투사체는 즉시 히트, 데미지 배율 1.0
                projectile.VFXData,
                0,
                1
            );

            // 즉시 히트 이벤트 발생하여 데미지 처리
            OnVFXHit?.Invoke(hitEventArgs);

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 히트 이벤트 발생 완료: 카드={projectile.SourceCard.CardData.CardName}, 타겟={target?.name}");
        }

        /// <summary>
        /// 투사체 완료 처리
        /// </summary>
        private void OnProjectileComplete(ProjectileController projectile)
        {
            if (projectile == null) return;

            // 투사체 추적에서 제거
            activeProjectiles.Remove(projectile);

            if (projectile.SourceCard != null && cardProjectileTracker.ContainsKey(projectile.SourceCard))
            {
                cardProjectileTracker[projectile.SourceCard].Remove(projectile);

                // 카드의 모든 투사체가 완료되었는지 확인
                CheckCardAttackSequenceCompletion(projectile.SourceCard);
            }

            if (debugMode)
                Debug.Log($"[VFXEffectManager] 투사체 완료: {projectile.SourceCard?.CardData?.CardName}");
        }

        /// <summary>
        /// 플레이어 Transform 가져오기
        /// </summary>
        private Transform GetPlayerTransform()
        {
            // PlayerBattleManager에서 플레이어 게임오브젝트 가져오기 (우선순위 1)
            if (PlayerBattleManager.Instance != null)
            {
                GameObject playerGameObject = PlayerBattleManager.Instance.GetPlayerGameObject();
                if (playerGameObject != null)
                {
                    if (debugMode)
                        Debug.Log("[VFXEffectManager] PlayerBattleManager에서 플레이어 Transform 가져옴");
                    return playerGameObject.transform;
                }
            }

            // PlayerManager에서 가져오기 (우선순위 2)
            if (PlayerManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[VFXEffectManager] PlayerManager에서 플레이어 Transform 가져옴");
                return PlayerManager.Instance.transform;
            }

            // 태그로 찾기 (마지막 수단)
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && debugMode)
                Debug.Log("[VFXEffectManager] 태그로 플레이어 Transform 가져옴");

            return playerObj?.transform;
        }

        /// <summary>
        /// VFX 인스턴스 생성 및 실행
        /// </summary>
        private VFXInstance CreateAndPlayVFX(VFXEffectSO vfxData, Transform[] targets, Card sourceCard)
        {
            if (targets.Length == 0) return null;

            int totalVFXCount = targets.Length;

            // 각 타겟에 대해 VFX 생성
            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];

                // VFX 오브젝트 생성
                Vector3 position = target.position + vfxData.PositionOffset;
                Quaternion rotation = Quaternion.Euler(vfxData.RotationOffset);

                GameObject vfxObject = Instantiate(vfxData.EffectPrefab, position, rotation, vfxParent);
                vfxObject.transform.localScale = vfxData.Scale;

                // VFX 레이어 설정
                SetVFXLayer(vfxObject);

                // VFX 인스턴스 생성 (인덱스 정보 포함)
                var vfxInstance = new VFXInstance(vfxObject, vfxData, new Transform[] { target }, sourceCard, i, totalVFXCount);
                activeVFXInstances.Add(vfxInstance);

                // 카드별 VFX 추적에 등록
                if (sourceCard != null)
                {
                    if (!cardVFXTracker.ContainsKey(sourceCard))
                    {
                        cardVFXTracker[sourceCard] = new List<VFXInstance>();
                    }
                    cardVFXTracker[sourceCard].Add(vfxInstance);
                }

                // 사운드 재생
                if (vfxData.SoundEffect != null)
                {
                    PlayVFXSound(vfxData.SoundEffect, position);
                }

                // 자동 삭제 설정
                if (vfxData.AutoDestroy)
                {
                    StartCoroutine(DestroyVFXAfterDuration(vfxInstance, vfxData.EffectDuration));
                }

                // 이벤트 발생
                OnVFXStarted?.Invoke(vfxInstance);

                if (debugMode)
                    Debug.Log($"[VFXEffectManager] VFX 시작: {vfxData.EffectName} at {target.name} (인덱스: {i}/{totalVFXCount})");
            }

            return activeVFXInstances.Count > 0 ? activeVFXInstances[activeVFXInstances.Count - 1] : null;
        }

        /// <summary>
        /// 활성 VFX 인스턴스들 업데이트
        /// </summary>
        private void UpdateActiveVFXInstances()
        {
            for (int i = activeVFXInstances.Count - 1; i >= 0; i--)
            {
                var vfxInstance = activeVFXInstances[i];

                if (!vfxInstance.isActive || vfxInstance.effectObject == null)
                {
                    activeVFXInstances.RemoveAt(i);
                    continue;
                }

                // 히트 타이밍 처리
                ProcessHitTimings(vfxInstance);
            }
        }

        /// <summary>
        /// 히트 타이밍 처리
        /// </summary>
        private void ProcessHitTimings(VFXInstance vfxInstance)
        {
            if (!vfxInstance.effectData.HasValidHitTimings) return;

            float elapsedTime = Time.time - vfxInstance.startTime;
            var hitTimings = vfxInstance.effectData.HitTimings;

            // 다음 히트 타이밍 확인
            for (int i = vfxInstance.currentHitIndex; i < hitTimings.Length; i++)
            {
                var hitTiming = hitTimings[i];

                if (elapsedTime >= hitTiming.Delay)
                {
                    // 히트 발생
                    ExecuteHit(vfxInstance, hitTiming);
                    vfxInstance.currentHitIndex = i + 1;

                    if (debugMode)
                        Debug.Log($"[VFXEffectManager] 히트 발생: {vfxInstance.effectData.EffectName} - 타이밍 {hitTiming.Delay}초");
                }
                else
                {
                    break; // 아직 시간이 안 됨
                }
            }
        }

        /// <summary>
        /// 히트 실행
        /// </summary>
        private void ExecuteHit(VFXInstance vfxInstance, HitTiming hitTiming)
        {
            var hitEventArgs = new VFXHitEventArgs(
                vfxInstance.sourceCard,
                vfxInstance.targets,
                hitTiming,
                vfxInstance.effectData,
                vfxInstance.vfxIndex,
                vfxInstance.totalVFXCount
            );

            OnVFXHit?.Invoke(hitEventArgs);
        }



        /// <summary>
        /// ChainFrontHits 전용 VFX 재생: 단일 VFX를 순차적으로 반복
        /// </summary>
        private void PlayChainFrontHitsVFX(Card card, VFXEffectSO vfxData)
        {
            int chainCount = card.CardData.TargetCount;
            if (chainCount <= 0) return;

            if (debugMode)
                Debug.Log($"[VFXEffectManager] ChainFrontHits VFX 시작: {chainCount}회 연속 재생");

            // 체인 공격 진행 상태 설정
            cardChainAttackInProgress[card] = true;

            // 코루틴으로 순차적 재생 시작
            StartCoroutine(PlayChainVFXSequence(card, vfxData, chainCount));
        }

        /// <summary>
        /// ChainFrontHits VFX 순차 재생 코루틴
        /// </summary>
        private System.Collections.IEnumerator PlayChainVFXSequence(Card card, VFXEffectSO vfxData, int chainCount)
        {
            for (int i = 0; i < chainCount; i++)
            {
                // 현재 가장 앞 몬스터 찾기
                Transform currentTarget = GetCurrentFrontmostTarget();

                if (currentTarget == null)
                {
                    if (debugMode)
                        Debug.Log($"[VFXEffectManager] ChainFrontHits {i + 1}/{chainCount}: 타겟 없음, 중단");
                    break;
                }

                if (debugMode)
                    Debug.Log($"[VFXEffectManager] ChainFrontHits {i + 1}/{chainCount}: {currentTarget.name}에 VFX 재생");

                // 해당 위치에 VFX 재생
                PlayVFXAtPosition(vfxData, currentTarget.position, card);

                // VFX 지속시간만큼 대기 (다음 VFX가 겹치지 않도록)
                yield return new WaitForSeconds(vfxData.EffectDuration);
            }

            // 체인 공격 완료 상태 업데이트
            cardChainAttackInProgress[card] = false;

            if (debugMode)
                Debug.Log("[VFXEffectManager] ChainFrontHits VFX 시퀀스 완료");

            // 카드 공격 시퀀스 완료 검사
            CheckCardAttackSequenceCompletion(card);
        }

        /// <summary>
        /// 현재 가장 앞 몬스터 타겟 반환
        /// </summary>
        private Transform GetCurrentFrontmostTarget()
        {
            if (targetManager == null) return null;

            var aliveEnemies = targetManager.GetAliveEnemies();
            if (aliveEnemies.Count == 0) return null;

            // 가장 앞의 적 (X 좌표가 가장 작은 적)
            return aliveEnemies[0].transform;
        }

        /// <summary>
        /// 카드의 모든 공격 시퀀스가 완료되었는지 검사
        /// </summary>
        private void CheckCardAttackSequenceCompletion(Card card)
        {
            if (card == null) return;

            // 체인 공격이 진행 중인지 확인
            if (cardChainAttackInProgress.ContainsKey(card) && cardChainAttackInProgress[card])
            {
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 카드 {card.CardName} 체인 공격 아직 진행 중");
                return;
            }

            // 해당 카드의 VFX가 모두 완료되었는지 확인
            if (cardVFXTracker.ContainsKey(card))
            {
                var cardVFXList = cardVFXTracker[card];
                bool allVFXCompleted = cardVFXList.All(vfx => !vfx.isActive);

                if (!allVFXCompleted)
                {
                    if (debugMode)
                        Debug.Log($"[VFXEffectManager] 카드 {card.CardName} VFX 아직 진행 중: {cardVFXList.Count(vfx => vfx.isActive)}개 남음");
                    return;
                }

                // 모든 VFX가 완료되었으므로 추적에서 제거
                cardVFXTracker.Remove(card);
            }

            // 해당 카드의 투사체가 모두 완료되었는지 확인
            if (cardProjectileTracker.ContainsKey(card))
            {
                var cardProjectileList = cardProjectileTracker[card];
                if (cardProjectileList.Count > 0)
                {
                    if (debugMode)
                        Debug.Log($"[VFXEffectManager] 카드 {card.CardName} 투사체 아직 진행 중: {cardProjectileList.Count}개 남음");
                    return;
                }

                // 모든 투사체가 완료되었으므로 추적에서 제거
                cardProjectileTracker.Remove(card);
            }

            // 체인 공격 추적에서도 제거
            if (cardChainAttackInProgress.ContainsKey(card))
            {
                cardChainAttackInProgress.Remove(card);
            }

            // 카드의 모든 공격 시퀀스가 완료됨
            if (debugMode)
                Debug.Log($"[VFXEffectManager] 카드 {card.CardName} 모든 공격 시퀀스 완료!");

            OnCardAttackSequenceCompleted?.Invoke(card);
        }

        /// <summary>
        /// VFX 사운드 재생
        /// </summary>
        private void PlayVFXSound(AudioClip sound, Vector3 position)
        {
            if (sound == null) return;

            // AudioSource.PlayClipAtPoint 사용 또는 AudioManager 호출
            AudioSource.PlayClipAtPoint(sound, position);
        }

        /// <summary>
        /// 지정된 시간 후 VFX 삭제
        /// </summary>
        private IEnumerator DestroyVFXAfterDuration(VFXInstance vfxInstance, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (vfxInstance.effectObject != null)
            {
                OnVFXEnded?.Invoke(vfxInstance);
                Destroy(vfxInstance.effectObject);
                vfxInstance.isActive = false;

                if (debugMode)
                    Debug.Log($"[VFXEffectManager] VFX 종료: {vfxInstance.effectData.EffectName}");

                // 카드별 VFX 완료 검사
                CheckCardAttackSequenceCompletion(vfxInstance.sourceCard);
            }
        }

        /// <summary>
        /// VFX 오브젝트의 레이어 설정
        /// </summary>
        private void SetVFXLayer(GameObject vfxObject)
        {
            // 모든 SpriteRenderer 컴포넌트에 레이어 설정 적용
            var spriteRenderers = vfxObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in spriteRenderers)
            {
                sr.sortingLayerName = vfxSortingLayer;
                sr.sortingOrder = vfxOrderInLayer;
            }

            // ParticleSystemRenderer도 설정
            var particleRenderers = vfxObject.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var pr in particleRenderers)
            {
                pr.sortingLayerName = vfxSortingLayer;
                pr.sortingOrder = vfxOrderInLayer;
            }

            if (debugMode)
                Debug.Log($"[VFXEffectManager] VFX 레이어 설정: {vfxSortingLayer}, Order: {vfxOrderInLayer}");
        }

        /// <summary>
        /// 임시 타겟 오브젝트 삭제
        /// </summary>
        private IEnumerator DestroyTempTargetAfterVFX(GameObject tempTarget, float delay)
        {
            yield return new WaitForSeconds(delay + 0.5f); // VFX보다 조금 더 기다림
            if (tempTarget != null)
            {
                Destroy(tempTarget);
            }
        }
        #endregion

        #region ProjectileMoveScript Integration
        /// <summary>
        /// Gabriel Productions ProjectileMoveScript에서 muzzle과 hit prefab 정보 추출
        /// </summary>
        private void ExtractProjectileMoveScriptData(GameObject projectileObj, out GameObject muzzlePrefab, out GameObject hitPrefab)
        {
            muzzlePrefab = null;
            hitPrefab = null;

            // ProjectileMoveScript 컴포넌트 찾기
            var projectileMoveScript = projectileObj.GetComponent<ProjectileMoveScript>();
            if (projectileMoveScript != null)
            {
                muzzlePrefab = projectileMoveScript.muzzlePrefab;
                hitPrefab = projectileMoveScript.hitPrefab;

                if (debugMode)
                {
                    Debug.Log($"[VFXEffectManager] ProjectileMoveScript 정보 추출:");
                    Debug.Log($"  - Muzzle Prefab: {(muzzlePrefab != null ? muzzlePrefab.name : "null")}");
                    Debug.Log($"  - Hit Prefab: {(hitPrefab != null ? hitPrefab.name : "null")}");
                }
            }
            else if (debugMode)
            {
                Debug.Log("[VFXEffectManager] ProjectileMoveScript 컴포넌트를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// Muzzle 이펙트 생성 (projectileStartOffset + spawnHeightOffset 위치에)
        /// </summary>
        private void CreateMuzzleEffect(Transform startPos, VFXEffectSO vfxData, GameObject muzzlePrefab, ProjectileConfig? projectileConfig = null)
        {
            if (muzzlePrefab == null || startPos == null) return;

            // Muzzle 이펙트 위치 계산 (플레이어 위치 + projectileStartOffset + spawnHeightOffset)
            Vector3 muzzlePosition = startPos.position;
            
            // ProjectileConfig가 제공된 경우 개별 설정 사용
            if (projectileConfig.HasValue)
            {
                var config = projectileConfig.Value;
                if (config.StartOffset != Vector3.zero)
                {
                    muzzlePosition += config.StartOffset;
                }
                if (config.SpawnHeightOffset != 0f)
                {
                    muzzlePosition += Vector3.up * config.SpawnHeightOffset;
                }
            }
            // 레거시 지원: VFXEffectSO의 전역 오프셋 사용
            else if (vfxData != null && vfxData.ProjectileStartOffset != Vector3.zero)
            {
                muzzlePosition += vfxData.ProjectileStartOffset;
            }

            // Muzzle 이펙트 생성
            GameObject muzzleEffect = Instantiate(muzzlePrefab, muzzlePosition, Quaternion.identity, vfxParent);

            // VFX 레이어 설정
            SetVFXLayer(muzzleEffect);

            if (debugMode)
            {
                Debug.Log($"[VFXEffectManager] Muzzle 이펙트 생성: {muzzleEffect.name} at {muzzlePosition}");
                Debug.Log($"  - 시작 위치: {startPos.position}");
                if (projectileConfig.HasValue)
                {
                    var config = projectileConfig.Value;
                    Debug.Log($"  - StartOffset: {config.StartOffset}");
                    Debug.Log($"  - SpawnHeightOffset: {config.SpawnHeightOffset}");
                }
                else
                {
                    Debug.Log($"  - 레거시 오프셋: {vfxData?.ProjectileStartOffset ?? Vector3.zero}");
                }
            }

            // Muzzle 이펙트 자동 삭제 (일반적으로 짧은 시간)
            StartCoroutine(DestroyMuzzleEffect(muzzleEffect, 2f));
        }

        /// <summary>
        /// Muzzle 이펙트 삭제
        /// </summary>
        private System.Collections.IEnumerator DestroyMuzzleEffect(GameObject muzzleEffect, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (muzzleEffect != null)
            {
                Destroy(muzzleEffect);
            }
        }

        /// <summary>
        /// 투사체의 투명도 설정 (SpriteRenderer, ParticleSystemRenderer 등)
        /// </summary>
        private void SetProjectileAlpha(GameObject projectileObj, float alpha)
        {
            if (projectileObj == null) return;

            // SpriteRenderer의 투명도 설정
            var spriteRenderers = projectileObj.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in spriteRenderers)
            {
                Color color = sr.color;
                color.a = alpha;
                sr.color = color;
            }

            // ParticleSystemRenderer의 투명도 설정
            var particleSystems = projectileObj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                var startColor = main.startColor;

                if (startColor.mode == ParticleSystemGradientMode.Color)
                {
                    Color color = startColor.color;
                    color.a = alpha;
                    main.startColor = color;
                }
                else if (startColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    Color colorMin = startColor.colorMin;
                    Color colorMax = startColor.colorMax;
                    colorMin.a = alpha;
                    colorMax.a = alpha;
                    main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
                }
            }

            // CanvasGroup이 있으면 alpha 설정
            var canvasGroup = projectileObj.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }

        /// <summary>
        /// 투사체 Collider 설정
        /// </summary>
        private void SetupProjectileCollider(GameObject projectileObj)
        {
            // Collider 설정 - 기존 Collider가 있으면 Trigger로 설정, 없으면 추가
            Collider existingCollider3D = projectileObj.GetComponent<Collider>();
            Collider2D existingCollider2D = projectileObj.GetComponent<Collider2D>();

            if (existingCollider3D != null)
            {
                existingCollider3D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 3D Collider를 Trigger로 설정: {existingCollider3D.GetType().Name}");
            }
            else if (existingCollider2D != null)
            {
                existingCollider2D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[VFXEffectManager] 기존 2D Collider를 Trigger로 설정: {existingCollider2D.GetType().Name}");
            }
            else
            {
                // Collider가 없으면 2D Collider 추가 (몬스터가 2D이므로)
                CircleCollider2D collider2D = projectileObj.AddComponent<CircleCollider2D>();
                collider2D.isTrigger = true;
                collider2D.radius = 0.5f;
                if (debugMode)
                    Debug.Log("[VFXEffectManager] 새로운 2D Collider 추가");
            }
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug VFX Info")]
        public void PrintDebugInfo()
        {
            Debug.Log("=== VFXEffectManager Debug Info ===");
            Debug.Log($"활성 VFX 수: {activeVFXInstances.Count}");

            foreach (var vfx in activeVFXInstances)
            {
                Debug.Log($"- {vfx.effectData.EffectName} (경과시간: {Time.time - vfx.startTime:F2}s, 히트인덱스: {vfx.currentHitIndex})");
            }
        }
        #endregion
    }
}
