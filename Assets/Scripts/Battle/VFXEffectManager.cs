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

        public VFXInstance(GameObject effectObject, VFXEffectSO effectData, Transform[] targets, Card sourceCard)
        {
            this.effectObject = effectObject;
            this.effectData = effectData;
            this.targets = targets;
            this.sourceCard = sourceCard;
            this.startTime = Time.time;
            this.isActive = true;
            this.currentHitIndex = 0;
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

        public VFXHitEventArgs(Card sourceCard, Transform[] targets, HitTiming hitTiming, VFXEffectSO effectData)
        {
            this.sourceCard = sourceCard;
            this.targets = targets;
            this.hitTiming = hitTiming;
            this.effectData = effectData;
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
        #endregion

        #region Fields
        [Header("설정")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private Transform vfxParent; // VFX 오브젝트들의 부모 Transform

        [Header("레이어 설정")]
        [SerializeField] private string vfxSortingLayer = "VFX"; // VFX용 Sorting Layer
        [SerializeField] private int vfxOrderInLayer = 10; // VFX의 Order in Layer

        // 활성 VFX 인스턴스들
        private List<VFXInstance> activeVFXInstances = new List<VFXInstance>();

        // 컴포넌트 참조
        private TargetManager targetManager;
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

            // 타겟 결정
            Transform[] targets = DetermineTargets(card, vfxData, specificTargets);

            // PlayerFrontLine: ShowVFXPerTarget이 꺼진 경우에만 앞칸 고정 위치 1회 재생
            var targetTypeForVfx = vfxData.GetTargetType(card.CardData.Target);
            if (targetTypeForVfx == TargetType.PlayerFrontLine && !card.CardData.ShowVFXPerTarget)
            {
                Vector3 anchor = GetPlayerFrontAnchorWorldPosition();
                PlayVFXAtPosition(vfxData, anchor, card);
                return;
            }

            if (targets == null || targets.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] VFX 타겟을 찾을 수 없습니다: {card.CardData.CardName}");
                return;
            }

            // ChainFrontHits는 히트마다 현재 앞 적 위치에 임팩트 스폰 방식으로 처리
            var ttype = vfxData.GetTargetType(card.CardData.Target);
            if (ttype == TargetType.ChainFrontHits)
            {
                // 독립 히트 스케줄: 각 히트 타이밍마다 현재 앞 적 위치에서 별도의 VFX를 스폰하고, 그 시점에 데미지를 1회 적용
                var timings = vfxData.HasValidHitTimings ? vfxData.HitTimings : null;
                int plannedHits = Mathf.Min(card.CardData.TargetCount, timings != null ? timings.Length : 0);
                for (int i = 0; i < plannedHits; i++)
                {
                    StartCoroutine(ExecuteChainFrontHitAfterDelay(timings[i].Delay, timings[i], card, vfxData));
                }
                return;
            }

            // VFX 인스턴스 생성 및 실행 (일반)
            // 카드 옵션에 따라 타겟마다 하나씩 vs 대표 위치 하나만
            if (card.CardData.ShowVFXPerTarget)
            {
                CreateAndPlayVFX(vfxData, targets, card);
            }
            else
            {
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

                case TargetType.FrontN:
                case TargetType.BackN:
                    targets.AddRange(GetPositionalTargets(targetType, targetCount));
                    break;

                case TargetType.ChainFrontHits:
                    // 가장 앞의 적 1명만 타겟으로 VFX 생성 (히트마다 데미지는 별도 처리)
                    targets.AddRange(GetPositionalTargets(TargetType.FrontN, 1));
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

                case TargetType.PullFrontmostForward:
                    // 가장 앞의 적 1명 기준 VFX (이동 연출용)
                    targets.AddRange(GetPositionalTargets(TargetType.FrontN, 1));
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
        /// 위치 기반 타겟팅 (앞의 N칸, 뒤의 N칸)
        /// </summary>
        private Transform[] GetPositionalTargets(TargetType targetType, int targetCount)
        {
            var targets = new List<Transform>();
            var allEnemies = FindObjectsOfType<Enemy.Enemy>()
                .Where(e => e.CurrentState != EnemyState.Dead)
                .ToList();

            // 1D 그리드 기준: 플레이어 X보다 큰 적만 고려
            int px = targetManager != null ? targetManager.PlayerGridPosition.x : 0;
            var frontList = allEnemies
                .Where(e => e.GridPosition.x > px)
                .OrderBy(e => e.GridPosition.x - px)
                .ToList();

            switch (targetType)
            {
                case TargetType.FrontN:
                    for (int i = 0; i < Mathf.Min(targetCount, frontList.Count); i++)
                    {
                        targets.Add(frontList[i].transform);
                    }
                    break;

                case TargetType.BackN:
                    // 뒤의 N명 (멀리 있는 순서)
                    var backList = frontList.OrderByDescending(e => e.GridPosition.x - px).ToList();
                    for (int i = 0; i < Mathf.Min(targetCount, backList.Count); i++)
                        targets.Add(backList[i].transform);
                    break;
            }

            return targets.ToArray();
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
        /// VFX 인스턴스 생성 및 실행
        /// </summary>
        private VFXInstance CreateAndPlayVFX(VFXEffectSO vfxData, Transform[] targets, Card sourceCard)
        {
            if (targets.Length == 0) return null;

            // 각 타겟에 대해 VFX 생성
            foreach (var target in targets)
            {
                // VFX 오브젝트 생성
                Vector3 position = target.position + vfxData.PositionOffset;
                Quaternion rotation = Quaternion.Euler(vfxData.RotationOffset);

                GameObject vfxObject = Instantiate(vfxData.EffectPrefab, position, rotation, vfxParent);
                vfxObject.transform.localScale = vfxData.Scale;

                // VFX 레이어 설정
                SetVFXLayer(vfxObject);

                // VFX 인스턴스 생성
                var vfxInstance = new VFXInstance(vfxObject, vfxData, new Transform[] { target }, sourceCard);
                activeVFXInstances.Add(vfxInstance);

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
                    Debug.Log($"[VFXEffectManager] VFX 시작: {vfxData.EffectName} at {target.name}");
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

                // 체인형은 독립 스폰 방식으로 처리하므로 여기서 리타겟하지 않음

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
            // 체인형: 독립 스케줄 방식으로 처리하므로 여기서는 스킵
            var cardData = vfxInstance.sourceCard?.CardData;
            if (cardData != null && vfxInstance.effectData != null)
            {
                var tType = vfxInstance.effectData.GetTargetType(cardData.Target);
                if (tType == TargetType.ChainFrontHits) return;
            }

            var hitEventArgs = new VFXHitEventArgs(
                vfxInstance.sourceCard,
                vfxInstance.targets,
                hitTiming,
                vfxInstance.effectData
            );

            OnVFXHit?.Invoke(hitEventArgs);
        }

        private Transform GetFrontEnemyTransform()
        {
            if (targetManager == null) return null;
            int px = targetManager.PlayerGridPosition.x;
            Enemy.Enemy front = FindObjectsOfType<Enemy.Enemy>()
                .Where(e => e.CurrentState != EnemyState.Dead && e.GridPosition.x > px)
                .OrderBy(e => e.GridPosition.x - px)
                .FirstOrDefault();
            return front != null ? front.transform : null;
        }

        private IEnumerator ExecuteChainFrontHitAfterDelay(float delay, HitTiming timing, Card sourceCard, VFXEffectSO vfxData)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // 현재 가장 앞 적 취득
            Transform front = GetFrontEnemyTransform();
            if (front == null) yield break;

            // 즉시 임팩트 VFX 스폰 (독립 오브젝트)
            Vector3 pos = front.position + vfxData.PositionOffset;
            Quaternion rot = Quaternion.Euler(vfxData.RotationOffset);
            GameObject vfxObject = Instantiate(vfxData.EffectPrefab, pos, rot, vfxParent);
            vfxObject.transform.localScale = vfxData.Scale;
            SetVFXLayer(vfxObject);
            if (vfxData.SoundEffect != null)
            {
                PlayVFXSound(vfxData.SoundEffect, pos);
            }
            if (vfxData.AutoDestroy)
            {
                Destroy(vfxObject, vfxData.EffectDuration);
            }

            // VFX 자체의 첫 히트 타이밍에 맞춰 데미지 발생
            float innerDelay = 0f;
            if (vfxData.HasValidHitTimings && vfxData.HitTimings.Length > 0)
            {
                innerDelay = vfxData.HitTimings[0].Delay;
            }
            if (innerDelay > 0f) yield return new WaitForSeconds(innerDelay);

            // 데미지 이벤트(컨트롤러에서 처리)
            var hitArgs = new VFXHitEventArgs(sourceCard, new Transform[] { front }, timing, vfxData);
            OnVFXHit?.Invoke(hitArgs);
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


