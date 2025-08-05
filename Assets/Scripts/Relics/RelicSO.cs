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
        // 전투 관련
        DamageModifier,         // 데미지 증감
        HealthModifier,         // 체력 관련
        ManaModifier,           // 마나 관련

        // 경제 관련
        GoldModifier,           // 골드 획득/소모 증감
        ShopPriceModifier,      // 상점 가격 증감

        // 카드 관련
        CardDrawModifier,       // 카드 드로우 관련
        DeckSizeModifier,       // 덱 크기 관련

        // 필드 관련
        FieldEffectModifier,    // 필드 효과 관련

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
        [SerializeField] private float effectValue;        // 효과 수치 (%, 고정값 등)
        [SerializeField] private bool isPercentage;        // 효과가 퍼센트인지 고정값인지
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