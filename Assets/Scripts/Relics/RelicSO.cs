using UnityEngine;
using System.Collections.Generic;

namespace Maglin.Relics
{
    /// <summary>
    /// 유물 타입 (일반/보스 유물 구분)
    /// </summary>
    public enum RelicType
    {
        Common,     // 일반 유물
        Boss        // 보스 유물 (상점에서 등장하지 않음)
    }

    /// <summary>
    /// 유물 효과 타입
    /// </summary>
    public enum RelicEffectType
    {
        // 드로우 관련 효과들
        DrawCostReduction,      // 드로우 비용 감소
        FirstDrawFree,          // 첫 번째 드로우 무료
        DrawWithHealth,         // 체력으로 드로우
        DrawCountAttack,        // 드로우 횟수당 광역 공격

        // 이벤트 및 확률 관련
        EventChanceIncrease,    // 이벤트 씬 등장 확률 증가

        // 스탯 증가 효과들
        MaxHealthIncrease,      // 최대 체력 증가
        MaxManaIncrease,        // 최대 마나 증가
        MaxHandSizeIncrease,    // 최대 손패 수 증가

        // 반격 효과들
        CounterAttackSingle,    // 공격받을 시 공격한 몬스터에게 반격
        CounterAttackAll,       // 공격받을 시 모든 몬스터에게 반격

        // 전투 종료 효과들
        BattleEndHeal,          // 전투 종료 시 체력 회복

        // 방어 효과들
        DamageNegate,           // 전투당 첫 N회 피해 무효화

        // 추가 공격 효과들
        CardUseCountAttack,     // 카드 N회 사용 시 광역 공격

        // 상점 관련 효과들
        ShopHealCostReduction,  // 상점 회복 비용 감소
        ShopCardCostReduction,  // 상점 카드 구매 비용 감소
        ShopRemoveCostReduction, // 상점 카드 제거 비용 감소

        // 보상 관련 효과들
        GoldRewardIncrease,     // 전투 종료 시 골드 획득 증가

        // 기타
        CustomEffect           // 커스텀 효과
    }

    /// <summary>
    /// 유물의 개별 효과 데이터
    /// </summary>
    [System.Serializable]
    public class RelicEffect
    {
        [SerializeField] private RelicEffectType effectType;
        [SerializeField] private float effectValue;        // 주 효과 수치 (%, 고정값 등)
        [SerializeField] private float secondaryValue;     // 보조 효과 수치 (횟수, 배수 등)
        [SerializeField] private bool isPercentage;        // effectValue가 퍼센트인지 고정값인지

        [Header("커스텀 효과 (CustomEffect인 경우)")]
        [TextArea(3, 5)]
        [SerializeField] private string customEffectDescription;

        public RelicEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float SecondaryValue => secondaryValue;
        public bool IsPercentage => isPercentage;
        public string CustomEffectDescription => customEffectDescription;
    }

    [CreateAssetMenu(fileName = "New Relic", menuName = "Maglin/Relics/RelicSO")]
    public class RelicSO : ScriptableObject
    {
        [Header("유물 기본 정보")]
        [SerializeField] private string relicName;
        [SerializeField] private RelicType relicType;
        [SerializeField] private string relicDescription;
        [SerializeField] private Sprite relicImage;

        [Header("유물 효과들")]
        [SerializeField] private RelicEffect[] effects;    // 여러 효과를 배열로 관리
        [SerializeField] private bool canStack = false;    // 중첩 가능 여부

        [Header("상점 정보")]
        [SerializeField] private int relicPrice;

        // Properties
        public string RelicName => relicName;
        public RelicType Type => relicType;
        public string Description => relicDescription;
        public Sprite Image => relicImage;
        public RelicEffect[] Effects => effects; // 모든 효과 배열 반환
        public bool CanStack => canStack;
        public int Price => relicPrice;

        // 하위 호환성을 위한 속성들 (첫 번째 효과 기준)
        public RelicEffectType EffectType => effects != null && effects.Length > 0 ? effects[0].EffectType : RelicEffectType.CustomEffect;
        public float EffectValue => effects != null && effects.Length > 0 ? effects[0].EffectValue : 0f;
        public float SecondaryValue => effects != null && effects.Length > 0 ? effects[0].SecondaryValue : 0f;
        public bool IsPercentage => effects != null && effects.Length > 0 ? effects[0].IsPercentage : false;
        public string CustomEffectDescription => effects != null && effects.Length > 0 ? effects[0].CustomEffectDescription : "";

        /// <summary>
        /// 상점에서 판매 가능한 유물인지 확인 (보스 유물은 상점 판매 불가)
        /// </summary>
        public bool CanBeSold => relicType != RelicType.Boss;

        /// <summary>
        /// 효과값을 문자열로 반환 (UI 표시용) - 모든 효과를 표시
        /// </summary>
        public string GetEffectValueString()
        {
            if (effects == null || effects.Length == 0)
                return "";

            var effectStrings = new List<string>();

            foreach (var effect in effects)
            {
                string effectStr = GetSingleEffectString(effect);
                if (!string.IsNullOrEmpty(effectStr))
                    effectStrings.Add(effectStr);
            }

            return string.Join("\n", effectStrings);
        }

        /// <summary>
        /// 개별 효과를 문자열로 변환
        /// </summary>
        private string GetSingleEffectString(RelicEffect effect)
        {
            // 두 값이 모두 필요한 효과 타입들
            if (effect.EffectType == RelicEffectType.DrawCountAttack ||
                effect.EffectType == RelicEffectType.CardUseCountAttack)
            {
                string primaryStr = effect.IsPercentage ? $"{effect.EffectValue:F1}%" : effect.EffectValue.ToString("F0");
                string secondaryStr = effect.SecondaryValue.ToString("F0");

                if (effect.EffectType == RelicEffectType.DrawCountAttack)
                    return $"{secondaryStr}드로우마다 {primaryStr}데미지";
                else if (effect.EffectType == RelicEffectType.CardUseCountAttack)
                    return $"{secondaryStr}카드사용마다 모든 몬스터에게 {primaryStr}데미지";
            }

            // 반격 효과들
            if (effect.EffectType == RelicEffectType.CounterAttackSingle)
            {
                return $"공격받을 시 공격자에게 {effect.EffectValue:F0} 피해";
            }
            else if (effect.EffectType == RelicEffectType.CounterAttackAll)
            {
                return $"공격받을 시 모든 몬스터에게 {effect.EffectValue:F0} 피해";
            }

            // 단일 값만 필요한 효과들
            if (effect.IsPercentage)
            {
                return $"{effect.EffectValue:F1}%";
            }
            else
            {
                return effect.EffectValue.ToString("F0");
            }
        }

        /// <summary>
        /// 특정 효과 타입이 있는지 확인
        /// </summary>
        public bool HasEffect(RelicEffectType effectType)
        {
            if (effects == null) return false;
            foreach (var effect in effects)
            {
                if (effect.EffectType == effectType)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 특정 효과 타입의 효과들을 모두 반환
        /// </summary>
        public RelicEffect[] GetEffectsOfType(RelicEffectType effectType)
        {
            if (effects == null) return new RelicEffect[0];

            var result = new List<RelicEffect>();
            foreach (var effect in effects)
            {
                if (effect.EffectType == effectType)
                    result.Add(effect);
            }
            return result.ToArray();
        }
    }
}