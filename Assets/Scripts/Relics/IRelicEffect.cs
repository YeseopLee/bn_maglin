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
        public bool DebugMode { get; set; } = false;  // 디버그 모드 여부

        public RelicEffectContext(bool isApplying = true, float baseValue = 0f, object additionalData = null, bool debugMode = false)
        {
            IsApplying = isApplying;
            BaseValue = baseValue;
            AdditionalData = additionalData;
            DebugMode = debugMode;
        }
    }

    /// <summary>
    /// 스탯 수정 효과 (향후 확장용)
    /// </summary>
    public class StatModifierEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            // 향후 스탯 관련 유물 효과가 추가되면 여기에 구현
            Debug.Log($"[StatModifierEffect] {relic.RelicName} 스탯 효과 적용 (향후 구현 예정)");
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            // 향후 스탯 효과 제거 로직 구현 예정
            Debug.Log($"[StatModifierEffect] {relic.RelicName} 스탯 효과 제거 (향후 구현 예정)");
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            // 현재는 지원하지 않음 (향후 확장시 추가)
            return false;
        }
    }

    /// <summary>
    /// 경제 효과 (향후 확장용)
    /// </summary>
    public class EconomicEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            // 향후 경제 관련 유물 효과가 추가되면 여기에 구현
            Debug.Log($"[EconomicEffect] {relic.RelicName} 경제 효과 적용 (향후 구현 예정)");
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            // 향후 경제 효과 제거 로직 구현 예정
            Debug.Log($"[EconomicEffect] {relic.RelicName} 경제 효과 제거 (향후 구현 예정)");
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            // 현재는 지원하지 않음 (향후 확장시 추가)
            return false;
        }
    }

    /// <summary>
    /// 드로우 관련 특수 효과 처리
    /// </summary>
    public class DrawRelicEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            switch (relic.EffectType)
            {
                case RelicEffectType.DrawCostReduction:
                case RelicEffectType.FirstDrawFree:
                case RelicEffectType.DrawWithHealth:
                case RelicEffectType.DrawCountAttack:
                    // 드로우 관련 효과는 실시간으로 적용되므로 여기서는 등록만
                    if (RelicManager.Instance != null)
                    {
                        RelicManager.Instance.TriggerRelicEffect(relic, relic.EffectValue);
                    }
                    break;
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            // 드로우 효과는 실시간 계산이므로 특별한 제거 처리 불필요
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.DrawCostReduction ||
                   effectType == RelicEffectType.FirstDrawFree ||
                   effectType == RelicEffectType.DrawWithHealth ||
                   effectType == RelicEffectType.DrawCountAttack;
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

    /// <summary>
    /// 반격 효과 핸들러
    /// </summary>
    public class CounterAttackEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            // 반격 효과는 PlayerManager에서 TakeDamage 시점에 처리되므로 여기서는 로그만
            if (context.DebugMode)
            {
                string effectDescription = relic.EffectType == RelicEffectType.CounterAttackSingle
                    ? "단일 반격"
                    : "전체 반격";
                Debug.Log($"[CounterAttackEffect] {relic.RelicName} {effectDescription} 효과 활성화 (피해 시 자동 발동)");
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            if (context.DebugMode)
            {
                Debug.Log($"[CounterAttackEffect] {relic.RelicName} 반격 효과 제거");
            }
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.CounterAttackSingle ||
                   effectType == RelicEffectType.CounterAttackAll;
        }
    }

    /// <summary>
    /// 카드 사용 카운트 공격 효과 핸들러
    /// </summary>
    public class CardUseCountAttackEffect : IRelicEffect
    {
        public void ApplyEffect(RelicSO relic, RelicEffectContext context)
        {
            // 카드 사용 카운트 공격 효과는 PlayerManager에서 OnCardUsed 시점에 처리되므로 여기서는 로그만
            if (context.DebugMode)
            {
                Debug.Log($"[CardUseCountAttackEffect] {relic.RelicName} 카드 사용 카운트 공격 효과 활성화 (카드 사용 시 자동 발동)");
            }
        }

        public void RemoveEffect(RelicSO relic, RelicEffectContext context)
        {
            if (context.DebugMode)
            {
                Debug.Log($"[CardUseCountAttackEffect] {relic.RelicName} 카드 사용 카운트 공격 효과 제거");
            }
        }

        public bool SupportsEffectType(RelicEffectType effectType)
        {
            return effectType == RelicEffectType.CardUseCountAttack;
        }
    }
}