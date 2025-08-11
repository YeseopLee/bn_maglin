using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Maglin.Cards;
using Maglin.Enemy;

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
        [SerializeField] private bool debugMode = false;
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

            if (targets == null || targets.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[VFXEffectManager] VFX 타겟을 찾을 수 없습니다: {card.CardData.CardName}");
                return;
            }

            // VFX 인스턴스 생성 및 실행
            CreateAndPlayVFX(vfxData, targets, card);
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
            var allEnemies = FindObjectsOfType<Enemy.Enemy>();

            // 위치 순으로 정렬 (X 좌표 기준)
            var sortedEnemies = new List<Enemy.Enemy>(allEnemies);
            sortedEnemies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            switch (targetType)
            {
                case TargetType.FrontN:
                    // 앞의 N명 (왼쪽부터)
                    for (int i = 0; i < Mathf.Min(targetCount, sortedEnemies.Count); i++)
                    {
                        if (sortedEnemies[i].CurrentState != EnemyState.Dead)
                        {
                            targets.Add(sortedEnemies[i].transform);
                        }
                    }
                    break;

                case TargetType.BackN:
                    // 뒤의 N명 (오른쪽부터)
                    for (int i = sortedEnemies.Count - 1; i >= Mathf.Max(0, sortedEnemies.Count - targetCount); i--)
                    {
                        if (sortedEnemies[i].CurrentState != EnemyState.Dead)
                        {
                            targets.Add(sortedEnemies[i].transform);
                        }
                    }
                    break;
            }

            return targets.ToArray();
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
                vfxInstance.effectData
            );

            OnVFXHit?.Invoke(hitEventArgs);
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
