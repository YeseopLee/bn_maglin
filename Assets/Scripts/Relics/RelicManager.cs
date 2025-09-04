using System.Collections.Generic;
using UnityEngine;
using Maglin.Core;
using Maglin.Player;

namespace Maglin.Relics
{
    /// <summary>
    /// 유물 시스템 전체를 관리하는 매니저 클래스
    /// </summary>
    public class RelicManager : MonoBehaviour
    {
        #region Singleton
        private static RelicManager instance;
        public static RelicManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<RelicManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("RelicManager");
                        instance = go.AddComponent<RelicManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        #endregion

        #region Events
        public System.Action<RelicSO> OnRelicObtained;
        public System.Action<RelicSO> OnRelicLost;
        public System.Action<RelicSO, float> OnRelicEffectTriggered;
        #endregion

        #region Fields
        [Header("유물 데이터베이스")]
        [SerializeField] private RelicSO[] allRelics;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // 런타임 데이터
        private Dictionary<RelicType, List<RelicSO>> relicsByType;
        private Dictionary<RelicEffectType, List<RelicSO>> relicsByEffect;

        // 효과 처리기들
        private Dictionary<RelicEffectType, IRelicEffect> effectHandlers;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[RelicManager] 중복된 RelicManager 감지됨. 삭제합니다.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeRelicDatabase();
        }

        private void OnEnable()
        {
            // PlayerManager 이벤트 구독
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnRelicAdded += HandleRelicAdded;
                PlayerManager.OnRelicRemoved += HandleRelicRemoved;
            }
        }

        private void OnDisable()
        {
            // PlayerManager 이벤트 구독 해제
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnRelicAdded -= HandleRelicAdded;
                PlayerManager.OnRelicRemoved -= HandleRelicRemoved;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 유물 데이터베이스 초기화
        /// </summary>
        private void InitializeRelicDatabase()
        {
            // Resources 폴더에서 모든 RelicSO 로드
            if (allRelics == null || allRelics.Length == 0)
            {
                allRelics = Resources.LoadAll<RelicSO>("Relics");
                if (debugMode)
                    Debug.Log($"[RelicManager] Resources에서 {allRelics.Length}개의 유물 로드됨");
            }

            // 타입별 분류
            relicsByType = new Dictionary<RelicType, List<RelicSO>>();
            relicsByEffect = new Dictionary<RelicEffectType, List<RelicSO>>();

            foreach (RelicType type in System.Enum.GetValues(typeof(RelicType)))
            {
                relicsByType[type] = new List<RelicSO>();
            }

            foreach (RelicEffectType effect in System.Enum.GetValues(typeof(RelicEffectType)))
            {
                relicsByEffect[effect] = new List<RelicSO>();
            }

            // 유물 분류
            foreach (var relic in allRelics)
            {
                if (relic != null)
                {
                    relicsByType[relic.Type].Add(relic);
                    relicsByEffect[relic.EffectType].Add(relic);
                }
            }

            // 효과 처리기 초기화
            InitializeEffectHandlers();

            if (debugMode)
            {
                Debug.Log($"[RelicManager] 유물 데이터베이스 초기화 완료");
                foreach (var kvp in relicsByType)
                {
                    Debug.Log($"[RelicManager] {kvp.Key}: {kvp.Value.Count}개");
                }
            }
        }

        /// <summary>
        /// 효과 처리기 초기화
        /// </summary>
        private void InitializeEffectHandlers()
        {
            effectHandlers = new Dictionary<RelicEffectType, IRelicEffect>();

            // 스탯 수정 효과 처리기 (현재 사용되지 않지만 향후 확장용)
            var statModifier = new StatModifierEffect();

            // 드로우 관련 특수 효과 처리기
            var drawEffect = new DrawRelicEffect();
            effectHandlers[RelicEffectType.DrawCostReduction] = drawEffect;
            effectHandlers[RelicEffectType.FirstDrawFree] = drawEffect;
            effectHandlers[RelicEffectType.DrawWithHealth] = drawEffect;
            effectHandlers[RelicEffectType.DrawCountAttack] = drawEffect;

            // 반격 효과 처리기
            var counterAttackEffect = new CounterAttackEffect();
            effectHandlers[RelicEffectType.CounterAttackSingle] = counterAttackEffect;
            effectHandlers[RelicEffectType.CounterAttackAll] = counterAttackEffect;

            // 카드 사용 카운트 공격 효과 처리기
            effectHandlers[RelicEffectType.CardUseCountAttack] = new CardUseCountAttackEffect();

            // 커스텀 효과
            effectHandlers[RelicEffectType.CustomEffect] = new CustomEffect();

            if (debugMode)
                Debug.Log($"[RelicManager] {effectHandlers.Count}개의 효과 처리기 초기화 완료");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 플레이어에게 유물 지급
        /// </summary>
        public bool GiveRelicToPlayer(RelicSO relic)
        {
            if (relic == null)
            {
                Debug.LogError("[RelicManager] null 유물을 지급하려고 시도했습니다.");
                return false;
            }

            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[RelicManager] PlayerManager가 없어서 유물을 지급할 수 없습니다.");
                return false;
            }

            PlayerManager.Instance.AddRelic(relic);
            OnRelicObtained?.Invoke(relic);

            if (debugMode)
                Debug.Log($"[RelicManager] 플레이어에게 유물 지급: {relic.RelicName}");

            return true;
        }

        /// <summary>
        /// 플레이어로부터 유물 제거
        /// </summary>
        public bool RemoveRelicFromPlayer(RelicSO relic)
        {
            if (relic == null) return false;

            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[RelicManager] PlayerManager가 없어서 유물을 제거할 수 없습니다.");
                return false;
            }

            bool removed = PlayerManager.Instance.RemoveRelic(relic);
            if (removed)
            {
                OnRelicLost?.Invoke(relic);

                if (debugMode)
                    Debug.Log($"[RelicManager] 플레이어로부터 유물 제거: {relic.RelicName}");
            }

            return removed;
        }

        /// <summary>
        /// 특정 타입의 랜덤 유물 반환
        /// </summary>
        public RelicSO GetRandomRelic(RelicType relicType)
        {
            if (!relicsByType.ContainsKey(relicType) || relicsByType[relicType].Count == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[RelicManager] {relicType} 타입의 유물이 없습니다.");
                return null;
            }

            var relics = relicsByType[relicType];
            return relics[Random.Range(0, relics.Count)];
        }

        /// <summary>
        /// 상점에서 판매 가능한 유물들 중 랜덤 선택
        /// </summary>
        public RelicSO[] GetRandomShopRelics(int count)
        {
            var shopRelics = new List<RelicSO>();

            foreach (var relic in allRelics)
            {
                if (relic != null && relic.CanBeSold)
                {
                    shopRelics.Add(relic);
                }
            }

            if (shopRelics.Count == 0) return new RelicSO[0];

            var result = new RelicSO[Mathf.Min(count, shopRelics.Count)];
            var tempList = new List<RelicSO>(shopRelics);

            for (int i = 0; i < result.Length; i++)
            {
                int randomIndex = Random.Range(0, tempList.Count);
                result[i] = tempList[randomIndex];
                tempList.RemoveAt(randomIndex);
            }

            return result;
        }

        /// <summary>
        /// 특정 효과 타입의 유물 목록 반환
        /// </summary>
        public List<RelicSO> GetRelicsByEffectType(RelicEffectType effectType)
        {
            if (relicsByEffect.ContainsKey(effectType))
            {
                return new List<RelicSO>(relicsByEffect[effectType]);
            }
            return new List<RelicSO>();
        }

        /// <summary>
        /// 플레이어가 보유한 특정 효과의 총 수정자 계산
        /// </summary>
        public float CalculatePlayerRelicModifier(RelicEffectType effectType, bool asMultiplier = false)
        {
            if (PlayerManager.Instance == null) return asMultiplier ? 1f : 0f;

            return PlayerManager.Instance.GetRelicModifier(effectType, asMultiplier);
        }

        /// <summary>
        /// 유물 효과 트리거 (이벤트 발생)
        /// </summary>
        public void TriggerRelicEffect(RelicSO relic, float value = 0f)
        {
            OnRelicEffectTriggered?.Invoke(relic, value);

            if (debugMode)
                Debug.Log($"[RelicManager] 유물 효과 트리거: {relic.RelicName}, 값: {value}");
        }
        #endregion

        #region Event Handlers
        private void HandleRelicAdded(RelicSO relic)
        {
            if (debugMode)
                Debug.Log($"[RelicManager] 유물 추가됨: {relic.RelicName}");

            // 효과 처리기를 통한 유물 효과 적용
            ProcessRelicEffect(relic, true);

            // 추가적인 유물 효과 처리 (예: 전역 이벤트 발생)
            TriggerRelicEffect(relic);
        }

        private void HandleRelicRemoved(RelicSO relic)
        {
            if (debugMode)
                Debug.Log($"[RelicManager] 유물 제거됨: {relic.RelicName}");

            // 효과 처리기를 통한 유물 효과 제거
            ProcessRelicEffect(relic, false);
        }

        /// <summary>
        /// 유물 효과 처리 (적용/제거)
        /// </summary>
        private void ProcessRelicEffect(RelicSO relic, bool isApplying)
        {
            if (relic == null || effectHandlers == null) return;

            if (effectHandlers.TryGetValue(relic.EffectType, out IRelicEffect handler))
            {
                var context = new RelicEffectContext(isApplying);

                if (isApplying)
                {
                    handler.ApplyEffect(relic, context);
                }
                else
                {
                    handler.RemoveEffect(relic, context);
                }
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning($"[RelicManager] {relic.EffectType}에 대한 효과 처리기가 없습니다.");
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 모든 유물 목록 반환
        /// </summary>
        public RelicSO[] GetAllRelics()
        {
            return allRelics;
        }

        /// <summary>
        /// 특정 이름의 유물 찾기
        /// </summary>
        public RelicSO FindRelicByName(string relicName)
        {
            foreach (var relic in allRelics)
            {
                if (relic != null && relic.RelicName == relicName)
                {
                    return relic;
                }
            }
            return null;
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        public void PrintDebugInfo()
        {
            Debug.Log("=== RelicManager Debug Info ===");
            Debug.Log($"총 유물 수: {allRelics.Length}");

            foreach (var kvp in relicsByType)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value.Count}개");
                foreach (var relic in kvp.Value)
                {
                    Debug.Log($"  - {relic.RelicName} ({relic.EffectType})");
                }
            }
        }
        #endregion
    }
}