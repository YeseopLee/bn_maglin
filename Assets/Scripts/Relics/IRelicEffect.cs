using UnityEngine;
using Maglin.Player;

namespace Maglin.Relics
{
    /// <summary>
    /// 유물 효과를 처리하는 인터페이스
    /// </summary>
    public interface IRelicEffect
    {
        /// <summary>
        /// 유물 효과를 적용
        /// </summary>
        /// <param name="relic">효과를 적용할 유물</param>
        /// <param name="context">효과 적용 컨텍스트</param>
        void ApplyEffect(RelicSO relic, RelicEffectContext context);

        /// <summary>
        /// 유물 효과를 제거
        /// </summary>
        /// <param name="relic">효과를 제거할 유물</param>
        /// <param name="context">효과 제거 컨텍스트</param>
        void RemoveEffect(RelicSO relic, RelicEffectContext context);

        /// <summary>
        /// 이 효과가 특정 유물 타입을 지원하는지 확인
        /// </summary>
        /// <param name="effectType">확인할 효과 타입</param>
        /// <returns>지원 여부</returns>
        bool SupportsEffectType(RelicEffectType effectType);
    }

    /// <summary>
    /// 유물 효과 적용 시 사용되는 컨텍스트 정보
    /// </summary>
    public class RelicEffectContext
    {
        public bool IsApplying { get; set; } = true;  // true: 적용, false: 제거
        public float BaseValue { get; set; } = 0f;    // 기본값 (데미지, 체력 등)
        public object AdditionalData { get; set; }    // 추가 데이터

        public RelicEffectContext(bool isApplying = true, float baseValue = 0f, object additionalData = null)
        {
            IsApplying = isApplying;
            BaseValue = baseValue;
            AdditionalData = additionalData;
        }
    }

    /// <summary>
    /// 스탯 수정 효과 (체력, 마나, 데미지 등)
    /// </summary>
    public class StatModifierEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            switch (relic.EffectType)
            {
                case RelicEffectType.DamageModifier:
                    ApplyDamageModifier(relic, context);
                    break;
                case RelicEffectType.HealthModifier:
                    ApplyHealthModifier(relic, context);
                    break;
                case RelicEffectType.ManaModifier:
                    ApplyManaModifier(relic, context);
                    break;
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            // 스탯 효과는 제거 시에는 재계산으로 처리
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RecalculateStatsPublic();
            }
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.DamageModifier ||
                   effectType == RelicEffectType.HealthModifier ||
                   effectType == RelicEffectType.ManaModifier;
        }

        private void ApplyDamageModifier(RelicSO relic, RelicEffectContext context)
        {
            // 데미지 수정자는 Card 클래스에서 실시간으로 계산됨
            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }

        private void ApplyHealthModifier(RelicSO relic, RelicEffectContext context)
        {
            // 체력 수정자는 PlayerManager의 RecalculateStats에서 처리됨
            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }

        private void ApplyManaModifier(RelicSO relic, RelicEffectContext context)
        {
            // 마나 수정자는 PlayerManager의 RecalculateStats에서 처리됨
            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }
    }

    /// <summary>
    /// 경제 효과 (골드, 상점 가격 등)
    /// </summary>
    public class EconomicEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            switch (relic.EffectType)
            {
                case RelicEffectType.GoldModifier:
                    ApplyGoldModifier(relic, context);
                    break;
                case RelicEffectType.ShopPriceModifier:
                    ApplyShopPriceModifier(relic, context);
                    break;
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            // 경제 효과는 실시간 계산이므로 특별한 제거 처리 불필요
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.GoldModifier ||
                   effectType == RelicEffectType.ShopPriceModifier;
        }

        private void ApplyGoldModifier(RelicSO relic, RelicEffectContext context)
        {
            // 골드 수정자는 골드 획득 시점에 적용됨
            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }

        private void ApplyShopPriceModifier(RelicSO relic, RelicEffectContext context)
        {
            // 상점 가격 수정자는 상점 진입 시 적용됨
            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }
    }

    /// <summary>
    /// 커스텀 효과 처리
    /// </summary>
    public class CustomEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            if (relic.EffectType != RelicEffectType.CustomEffect) return;

            // 커스텀 효과는 유물별로 개별 구현 필요
            // 이곳에서는 기본적인 로깅만 수행
            Debug.Log($"[CustomEffect] {relic.RelicName} 커스텀 효과 적용: {relic.CustomEffectDescription}");

            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            Debug.Log($"[CustomEffect] {relic.RelicName} 커스텀 효과 제거");
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.CustomEffect;
        }
    }
}