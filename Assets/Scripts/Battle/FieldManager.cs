using UnityEngine;
using System;
using System.Collections.Generic;
using Maglin.Cards;
using Maglin.Core;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투 필드의 속성 시스템과 속성 효과를 관리하는 매니저
    /// </summary>
    public class FieldManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static FieldManager _instance;

        public static FieldManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FieldManager>();

                    if (_instance == null)
                    {
                        GameObject fieldManagerObject = new GameObject("FieldManager");
                        _instance = fieldManagerObject.AddComponent<FieldManager>();
                        DontDestroyOnLoad(fieldManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 필드 속성 변경 이벤트 (새로운 필드 속성, 이전 필드 속성)
        /// </summary>
        public static event Action<ElementType, ElementType> OnFieldChanged;

        /// <summary>
        /// 필드 효과 적용 이벤트 (필드 효과 SO)
        /// </summary>
        public static event Action<FieldEffectSO> OnFieldEffectApplied;

        /// <summary>
        /// 필드 효과 제거 이벤트 (필드 효과 SO)
        /// </summary>
        public static event Action<FieldEffectSO> OnFieldEffectRemoved;
        #endregion

        #region Fields
        [Header("현재 필드 상태")]
        [SerializeField] private ElementType currentFieldElement = ElementType.None;
        [SerializeField] private FieldEffectSO currentFieldEffect;
        [SerializeField] private int remainingTurns = 0;

        [Header("속성 상성 설정")]
        [SerializeField] private float advantageMultiplier = 1.2f; // 상성 유리 시 데미지 배율
        [SerializeField] private float disadvantageMultiplier = 0.8f; // 상성 불리 시 데미지 배율

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 속성 상성 매핑 (공격자 -> 피해자 -> 배율)
        private Dictionary<ElementType, Dictionary<ElementType, float>> elementAdvantages;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 필드 속성
        /// </summary>
        public ElementType CurrentFieldElement => currentFieldElement;

        /// <summary>
        /// 현재 필드 효과
        /// </summary>
        public FieldEffectSO CurrentFieldEffect => currentFieldEffect;

        /// <summary>
        /// 필드 효과 남은 턴 수
        /// </summary>
        public int RemainingTurns => remainingTurns;

        /// <summary>
        /// 필드 효과가 활성화되어 있는지 여부
        /// </summary>
        public bool HasActiveFieldEffect => currentFieldEffect != null;

        /// <summary>
        /// 상성 유리 시 데미지 배율
        /// </summary>
        public float AdvantageMultiplier => advantageMultiplier;

        /// <summary>
        /// 상성 불리 시 데미지 배율
        /// </summary>
        public float DisadvantageMultiplier => disadvantageMultiplier;
        #endregion

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 설정
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

            Initialize();
        }

        private void Start()
        {
            SetupElementAdvantages();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 필드 속성 변경
        /// </summary>
        public void ChangeFieldElement(ElementType newElement)
        {
            ElementType previousElement = currentFieldElement;
            currentFieldElement = newElement;

            if (debugMode)
                Debug.Log($"[FieldManager] 필드 속성 변경: {previousElement} -> {newElement}");

            OnFieldChanged?.Invoke(newElement, previousElement);
        }

        /// <summary>
        /// 필드 효과 적용
        /// </summary>
        public void ApplyFieldEffect(FieldEffectSO fieldEffect)
        {
            if (fieldEffect == null)
            {
                Debug.LogWarning("[FieldManager] 적용하려는 필드 효과가 null입니다.");
                return;
            }

            // 기존 효과 제거
            if (currentFieldEffect != null)
            {
                RemoveFieldEffect();
            }

            currentFieldEffect = fieldEffect;
            currentFieldElement = fieldEffect.FieldElement;
            remainingTurns = fieldEffect.Duration;

            if (debugMode)
                Debug.Log($"[FieldManager] 필드 효과 적용: {fieldEffect.EffectName} ({fieldEffect.FieldElement}), 지속시간: {remainingTurns}");

            OnFieldEffectApplied?.Invoke(fieldEffect);
        }

        /// <summary>
        /// 필드 효과 제거
        /// </summary>
        public void RemoveFieldEffect()
        {
            if (currentFieldEffect == null) return;

            FieldEffectSO removedEffect = currentFieldEffect;
            currentFieldEffect = null;
            currentFieldElement = ElementType.None;
            remainingTurns = 0;

            if (debugMode)
                Debug.Log($"[FieldManager] 필드 효과 제거: {removedEffect.EffectName}");

            OnFieldEffectRemoved?.Invoke(removedEffect);
        }

        /// <summary>
        /// 턴 종료 시 필드 효과 지속시간 감소
        /// </summary>
        public void OnTurnEnd()
        {
            if (currentFieldEffect == null || currentFieldEffect.IsPermanent) return;

            remainingTurns--;

            if (debugMode)
                Debug.Log($"[FieldManager] 필드 효과 남은 턴: {remainingTurns}");

            if (remainingTurns <= 0)
            {
                RemoveFieldEffect();
            }
        }

        /// <summary>
        /// 속성 상성에 따른 데미지 배율 계산
        /// </summary>
        public float CalculateElementAdvantage(ElementType attackerElement, ElementType defenderElement)
        {
            if (attackerElement == ElementType.None || defenderElement == ElementType.None)
                return 1.0f;

            if (elementAdvantages.TryGetValue(attackerElement, out var defenderMap))
            {
                if (defenderMap.TryGetValue(defenderElement, out float multiplier))
                {
                    if (debugMode && multiplier != 1.0f)
                        Debug.Log($"[FieldManager] 속성 상성: {attackerElement} vs {defenderElement} = {multiplier}x");

                    return multiplier;
                }
            }

            return 1.0f; // 상성 없음
        }

        /// <summary>
        /// 필드 속성 보너스 계산 (공격자가 현재 필드와 같은 속성일 때)
        /// </summary>
        public float CalculateFieldBonus(ElementType attackerElement)
        {
            if (currentFieldElement == ElementType.None || attackerElement == ElementType.None)
                return 1.0f;

            if (currentFieldElement == attackerElement)
            {
                // 같은 속성일 때 약간의 보너스 (1.1배)
                float bonus = 1.1f;

                if (debugMode)
                    Debug.Log($"[FieldManager] 필드 보너스: {attackerElement} = {bonus}x");

                return bonus;
            }

            return 1.0f;
        }

        /// <summary>
        /// 종합 데미지 배율 계산 (상성 + 필드 보너스)
        /// </summary>
        public float CalculateTotalDamageMultiplier(ElementType attackerElement, ElementType defenderElement)
        {
            float advantageMultiplier = CalculateElementAdvantage(attackerElement, defenderElement);
            float fieldBonus = CalculateFieldBonus(attackerElement);

            float totalMultiplier = advantageMultiplier * fieldBonus;

            if (debugMode && totalMultiplier != 1.0f)
                Debug.Log($"[FieldManager] 총 데미지 배율: {attackerElement} -> {defenderElement} = {totalMultiplier}x");

            return totalMultiplier;
        }

        /// <summary>
        /// 필드 초기화 (전투 시작 시)
        /// </summary>
        public void ResetField()
        {
            currentFieldElement = ElementType.None;
            currentFieldEffect = null;
            remainingTurns = 0;

            if (debugMode)
                Debug.Log("[FieldManager] 필드 초기화");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            if (isInitialized) return;

            ResetField();
            isInitialized = true;

            if (debugMode)
                Debug.Log("[FieldManager] 초기화 완료");
        }

        /// <summary>
        /// 속성 상성 설정
        /// 불>풀>물>불, 빛<->어둠 상호 1.2배 데미지
        /// </summary>
        private void SetupElementAdvantages()
        {
            elementAdvantages = new Dictionary<ElementType, Dictionary<ElementType, float>>();

            // 각 속성별 상성 초기화
            foreach (ElementType attacker in System.Enum.GetValues(typeof(ElementType)))
            {
                if (attacker == ElementType.None) continue;
                elementAdvantages[attacker] = new Dictionary<ElementType, float>();
            }

            // 불 > 풀
            elementAdvantages[ElementType.Fire][ElementType.Grass] = advantageMultiplier;
            elementAdvantages[ElementType.Grass][ElementType.Fire] = disadvantageMultiplier;

            // 풀 > 물
            elementAdvantages[ElementType.Grass][ElementType.Water] = advantageMultiplier;
            elementAdvantages[ElementType.Water][ElementType.Grass] = disadvantageMultiplier;

            // 물 > 불
            elementAdvantages[ElementType.Water][ElementType.Fire] = advantageMultiplier;
            elementAdvantages[ElementType.Fire][ElementType.Water] = disadvantageMultiplier;

            // 빛 <-> 어둠 (상호 1.2배)
            elementAdvantages[ElementType.Light][ElementType.Dark] = advantageMultiplier;
            elementAdvantages[ElementType.Dark][ElementType.Light] = advantageMultiplier;

            if (debugMode)
                Debug.Log("[FieldManager] 속성 상성 시스템 설정 완료");
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 현재 필드 상태 로그 출력
        /// </summary>
        [ContextMenu("Debug Field Status")]
        public void DebugFieldStatus()
        {
            Debug.Log($"=== FieldManager 상태 ===");
            Debug.Log($"현재 필드 속성: {currentFieldElement}");
            Debug.Log($"현재 필드 효과: {(currentFieldEffect != null ? currentFieldEffect.EffectName : "없음")}");
            Debug.Log($"남은 턴: {remainingTurns}");
        }

        /// <summary>
        /// 속성 상성 테스트
        /// </summary>
        [ContextMenu("Test Element Advantages")]
        public void TestElementAdvantages()
        {
            Debug.Log("=== 속성 상성 테스트 ===");
            ElementType[] elements = { ElementType.Fire, ElementType.Water, ElementType.Grass, ElementType.Light, ElementType.Dark };

            foreach (var attacker in elements)
            {
                foreach (var defender in elements)
                {
                    float multiplier = CalculateElementAdvantage(attacker, defender);
                    if (multiplier != 1.0f)
                    {
                        Debug.Log($"{attacker} -> {defender}: {multiplier}x");
                    }
                }
            }
        }
        #endregion
    }
}