using UnityEngine;

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

    [CreateAssetMenu(fileName = "New Relic", menuName = "Maglin/Relics/RelicSO")]
    public class RelicSO : ScriptableObject
    {
        [Header("유물 기본 정보")]
        [SerializeField] private string relicName;
        [SerializeField] private RelicType relicType;
        [SerializeField] private string relicDescription;
        [SerializeField] private Sprite relicImage;

        [Header("유물 효과")]
        [SerializeField] private RelicEffectType effectType;
        [SerializeField] private float effectValue;        // 주 효과 수치 (%, 고정값 등)
        [SerializeField] private float secondaryValue;     // 보조 효과 수치 (횟수, 배수 등)
        [SerializeField] private bool isPercentage;        // effectValue가 퍼센트인지 고정값인지
        [SerializeField] private bool canStack = false;    // 중첩 가능 여부

        [Header("상점 정보")]
        [SerializeField] private int relicPrice;

        [Header("커스텀 효과 (CustomEffect인 경우)")]
        [TextArea(3, 5)]
        [SerializeField] private string customEffectDescription;

        // Properties
        public string RelicName => relicName;
        public RelicType Type => relicType;
        public string Description => relicDescription;
        public Sprite Image => relicImage;
        public RelicEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float SecondaryValue => secondaryValue;
        public bool IsPercentage => isPercentage;
        public bool CanStack => canStack;
        public int Price => relicPrice;
        public string CustomEffectDescription => customEffectDescription;

        /// <summary>
        /// 상점에서 판매 가능한 유물인지 확인 (보스 유물은 상점 판매 불가)
        /// </summary>
        public bool CanBeSold => relicType != RelicType.Boss;

        /// <summary>
        /// 효과값을 문자열로 반환 (UI 표시용)
        /// </summary>
        public string GetEffectValueString()
        {
            // 두 값이 모두 필요한 효과 타입들
            if (effectType == RelicEffectType.DrawCountAttack ||
                effectType == RelicEffectType.CardUseCountAttack)
            {
                string primaryStr = isPercentage ? $"{effectValue:F1}%" : effectValue.ToString("F0");
                string secondaryStr = secondaryValue.ToString("F0");

                if (effectType == RelicEffectType.DrawCountAttack)
                    return $"{secondaryStr}드로우마다 {primaryStr}데미지";
                else if (effectType == RelicEffectType.CardUseCountAttack)
                    return $"{secondaryStr}카드사용마다 모든 몬스터에게 {primaryStr}데미지";
            }

            // 반격 효과들
            if (effectType == RelicEffectType.CounterAttackSingle)
            {
                return $"공격받을 시 공격자에게 {effectValue:F0} 피해";
            }
            else if (effectType == RelicEffectType.CounterAttackAll)
            {
                return $"공격받을 시 모든 몬스터에게 {effectValue:F0} 피해";
            }

            // 단일 값만 필요한 효과들
            if (isPercentage)
            {
                return $"{effectValue:F1}%";
            }
            else
            {
                return effectValue.ToString("F0");
            }
        }
    }
}