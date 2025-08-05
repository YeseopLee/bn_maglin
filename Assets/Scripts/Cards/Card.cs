using UnityEngine;
using System;
using Maglin.Player;
using Maglin.Relics;
using Maglin.Battle;

namespace Maglin.Cards
{
    /// <summary>
    /// 카드 인스턴스 클래스 - CardSO 데이터를 래핑하고 현재 효과값을 계산
    /// </summary>
    [Serializable]
    public class Card
    {
        #region Fields
        [SerializeField] private CardSO cardData;
        [SerializeField] private bool isUsable = true;
        [SerializeField] private bool isSelected = false;

        // 런타임 ID (인스턴스 구분용)
        private readonly int instanceId;
        private static int nextInstanceId = 1;

        // 캐시된 계산값
        private int? cachedDamage;
        private int? cachedManaCost;
        private int? cachedPrice;
        private bool isDirty = true;
        #endregion

        #region Properties
        /// <summary>
        /// 카드 데이터 참조
        /// </summary>
        public CardSO CardData => cardData;

        /// <summary>
        /// 인스턴스 ID
        /// </summary>
        public int InstanceId => instanceId;

        /// <summary>
        /// 카드 이름
        /// </summary>
        public string CardName => cardData != null ? cardData.CardName : "Unknown";

        /// <summary>
        /// 카드 타입
        /// </summary>
        public CardType Type => cardData != null ? cardData.Type : CardType.Element;

        /// <summary>
        /// 속성 타입
        /// </summary>
        public ElementType Element => cardData != null ? cardData.Element : ElementType.None;

        /// <summary>
        /// 대상 타입
        /// </summary>
        public TargetType Target => cardData != null ? cardData.Target : TargetType.SingleEnemy;

        /// <summary>
        /// 현재 데미지 (유물 효과 적용)
        /// </summary>
        public int CurrentDamage
        {
            get
            {
                if (isDirty || !cachedDamage.HasValue)
                {
                    cachedDamage = CalculateCurrentDamage();
                }
                return cachedDamage.Value;
            }
        }

        /// <summary>
        /// 현재 마나 비용 (유물 효과 적용)
        /// </summary>
        public int CurrentManaCost
        {
            get
            {
                if (isDirty || !cachedManaCost.HasValue)
                {
                    cachedManaCost = CalculateCurrentManaCost();
                }
                return cachedManaCost.Value;
            }
        }

        /// <summary>
        /// 현재 가격 (유물 효과 적용)
        /// </summary>
        public int CurrentPrice
        {
            get
            {
                if (isDirty || !cachedPrice.HasValue)
                {
                    cachedPrice = CalculateCurrentPrice();
                }
                return cachedPrice.Value;
            }
        }

        /// <summary>
        /// 기본 데미지 (유물 효과 적용 전)
        /// </summary>
        public int BaseDamage => cardData != null ? cardData.BaseDamage : 0;

        /// <summary>
        /// 기본 마나 비용 (유물 효과 적용 전)
        /// </summary>
        public int BaseManaCost => cardData != null ? cardData.ManaCost : 0;

        /// <summary>
        /// 기본 가격 (유물 효과 적용 전)
        /// </summary>
        public int BasePrice => cardData != null ? cardData.Price : 0;

        /// <summary>
        /// 카드 설명
        /// </summary>
        public string Description => cardData != null ? cardData.Description : "";

        /// <summary>
        /// 카드 이미지
        /// </summary>
        public Sprite CardImage => cardData != null ? cardData.Image : null;

        /// <summary>
        /// 카드 사운드
        /// </summary>
        public AudioClip CardSound => cardData != null ? cardData.Sound : null;

        /// <summary>
        /// 필드 효과
        /// </summary>
        public FieldEffectSO FieldEffect => cardData != null ? cardData.FieldEffect : null;

        /// <summary>
        /// 조합 요구사항 (조합 카드인 경우)
        /// </summary>
        public CardCombinationData[] RequiredCombinations => cardData != null ? cardData.RequiredCombinations : null;

        /// <summary>
        /// 카드 사용 가능 여부
        /// </summary>
        public bool IsUsable
        {
            get => isUsable;
            set => isUsable = value;
        }

        /// <summary>
        /// 카드 선택 상태
        /// </summary>
        public bool IsSelected
        {
            get => isSelected;
            set => isSelected = value;
        }

        /// <summary>
        /// 마나가 충분한지 여부
        /// </summary>
        public bool CanAfford => PlayerManager.Instance != null && PlayerManager.Instance.CurrentMana >= CurrentManaCost;

        /// <summary>
        /// 실제로 사용 가능한지 여부 (마나 + 상태)
        /// </summary>
        public bool CanUse => IsUsable && CanAfford;
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public Card()
        {
            instanceId = nextInstanceId++;
        }

        /// <summary>
        /// CardSO로 초기화하는 생성자
        /// </summary>
        public Card(CardSO cardSO) : this()
        {
            cardData = cardSO;
            MarkDirty();
        }

        /// <summary>
        /// 복사 생성자
        /// </summary>
        public Card(Card other) : this()
        {
            if (other != null)
            {
                cardData = other.cardData;
                isUsable = other.isUsable;
                MarkDirty();
            }
        }
        #endregion

        #region Calculation Methods
        /// <summary>
        /// 현재 데미지 계산 (유물 효과 적용)
        /// </summary>
        private int CalculateCurrentDamage()
        {
            if (cardData == null) return 0;

            float damage = cardData.BaseDamage;

            // PlayerManager가 있고 데미지가 0보다 큰 경우에만 유물 효과 적용
            if (PlayerManager.Instance != null && damage > 0)
            {
                // 데미지 수정자 유물 효과 적용
                float damageModifier = PlayerManager.Instance.GetRelicModifier(RelicEffectType.DamageModifier, true);
                damage *= damageModifier;

                // 속성별 데미지 보너스 등 추가 로직이 있다면 여기에 구현
                // 예: 특정 속성 데미지 증가 유물 등
            }

            return Mathf.RoundToInt(damage);
        }

        /// <summary>
        /// 현재 마나 비용 계산 (유물 효과 적용)
        /// </summary>
        private int CalculateCurrentManaCost()
        {
            if (cardData == null) return 0;

            float manaCost = cardData.ManaCost;

            // PlayerManager가 있는 경우 유물 효과 적용
            if (PlayerManager.Instance != null)
            {
                // 마나 비용 감소 유물 등의 효과 적용
                // 현재는 기본값 반환, 추후 마나 비용 관련 유물 추가 시 구현
            }

            return Mathf.Max(0, Mathf.RoundToInt(manaCost));
        }

        /// <summary>
        /// 현재 가격 계산 (유물 효과 적용)
        /// </summary>
        private int CalculateCurrentPrice()
        {
            if (cardData == null) return 0;

            float price = cardData.Price;

            // PlayerManager가 있는 경우 유물 효과 적용
            if (PlayerManager.Instance != null)
            {
                // 상점 가격 할인 유물 효과 적용
                float priceModifier = PlayerManager.Instance.GetRelicModifier(RelicEffectType.ShopPriceModifier, true);
                price *= priceModifier;
            }

            return Mathf.Max(0, Mathf.RoundToInt(price));
        }

        /// <summary>
        /// 대상에 대한 실제 데미지 계산 (속성 상성 + 필드 효과 포함)
        /// </summary>
        public int CalculateDamageAgainst(ElementType targetElement)
        {
            int baseDamage = CurrentDamage;
            if (baseDamage <= 0) return 0;

            // FieldManager를 통한 속성 상성 및 필드 보너스 계산
            if (FieldManager.Instance != null)
            {
                float multiplier = FieldManager.Instance.CalculateTotalDamageMultiplier(Element, targetElement);
                return Mathf.RoundToInt(baseDamage * multiplier);
            }

            return baseDamage;
        }

        /// <summary>
        /// 속성 상성 정보 반환 (UI 표시용)
        /// </summary>
        public string GetElementAdvantageInfo(ElementType targetElement)
        {
            if (FieldManager.Instance == null || Element == ElementType.None || targetElement == ElementType.None)
                return "";

            float advantage = FieldManager.Instance.CalculateElementAdvantage(Element, targetElement);
            float fieldBonus = FieldManager.Instance.CalculateFieldBonus(Element);

            if (advantage > 1.0f)
                return "효과적!";
            else if (advantage < 1.0f)
                return "별로...";
            else if (fieldBonus > 1.0f)
                return "필드 보너스!";

            return "";
        }
        #endregion

        #region State Management
        /// <summary>
        /// 캐시 무효화 (유물 변경 등으로 재계산 필요할 때)
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
            cachedDamage = null;
            cachedManaCost = null;
            cachedPrice = null;
        }

        /// <summary>
        /// 카드 상태 초기화
        /// </summary>
        public void ResetState()
        {
            isUsable = true;
            isSelected = false;
            MarkDirty();
        }

        /// <summary>
        /// 카드 사용 시 호출 (상태 업데이트)
        /// </summary>
        public void OnUsed()
        {
            isSelected = false;
            // 추가 사용 후 처리 로직이 있다면 여기에 구현
        }
        #endregion

        #region Validation
        /// <summary>
        /// 카드 데이터 유효성 검증
        /// </summary>
        public bool IsValid()
        {
            return cardData != null;
        }

        /// <summary>
        /// 조합 가능한 카드인지 확인
        /// </summary>
        public bool IsCombinable()
        {
            return IsValid() && Type != CardType.Element && RequiredCombinations != null && RequiredCombinations.Length > 0;
        }

        /// <summary>
        /// 특정 타입과 조합 가능한지 확인
        /// </summary>
        public bool CanCombineWith(CardType requiredType)
        {
            if (!IsCombinable()) return false;

            foreach (var combination in RequiredCombinations)
            {
                switch (requiredType)
                {
                    case CardType.Element:
                        if (combination.elementCard != null) return true;
                        break;
                    case CardType.Active1:
                        if (combination.active1Card != null) return true;
                        break;
                    case CardType.Active2:
                        if (combination.active2Card != null) return true;
                        break;
                }
            }
            return false;
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// 모든 카드 인스턴스의 캐시 무효화 (전역 유물 변경 시)
        /// </summary>
        public static void MarkAllDirty()
        {
            // 이 메서드는 CardManager에서 유물 변경 이벤트 시 호출될 예정
            // 현재는 정적 메서드로만 정의, 실제 구현은 CardManager에서
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 문자열 반환
        /// </summary>
        public string GetDebugInfo()
        {
            if (!IsValid()) return "Invalid Card";

            return $"{CardName} ({Type}) - DMG: {BaseDamage}→{CurrentDamage}, " +
                   $"Cost: {BaseManaCost}→{CurrentManaCost}, " +
                   $"Price: {BasePrice}→{CurrentPrice}, " +
                   $"Usable: {IsUsable}, CanUse: {CanUse}";
        }

        /// <summary>
        /// ToString 오버라이드
        /// </summary>
        public override string ToString()
        {
            return IsValid() ? $"Card({instanceId}): {CardName}" : $"Card({instanceId}): Invalid";
        }
        #endregion
    }
}