using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Maglin.Battle
{
    /// <summary>
    /// 카드 조합 실패 시 적용될 페널티를 정의하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ComboFallback", menuName = "Maglin/Battle/Combo Fallback", order = 4)]
    public class ComboFallbackSO : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string fallbackName;
        [SerializeField] private string fallbackDescription;
        [SerializeField] private Sprite fallbackIcon;

        [Header("페널티 설정")]
        [SerializeField] private FallbackPenalty[] penalties;
        [SerializeField] private bool useWeightedRandom = true;
        [SerializeField] private int maxPenaltyCount = 1;

        [Header("조건 설정")]
        [SerializeField] private FallbackCondition[] conditions;
        [SerializeField] private bool requireAllConditions = true;

        [Header("피드백 설정")]
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private string failureMessage = "조합에 실패했습니다!";
        [SerializeField] private Color failureColor = Color.red;
        [SerializeField] private float messageDuration = 2.0f;

        #region Properties
        /// <summary>
        /// 페널티 이름
        /// </summary>
        public string FallbackName => fallbackName;

        /// <summary>
        /// 페널티 설명
        /// </summary>
        public string Description => fallbackDescription;

        /// <summary>
        /// 페널티 아이콘
        /// </summary>
        public Sprite Icon => fallbackIcon;

        /// <summary>
        /// 페널티 목록
        /// </summary>
        public FallbackPenalty[] Penalties => penalties;

        /// <summary>
        /// 실패 사운드
        /// </summary>
        public AudioClip FailureSound => failureSound;

        /// <summary>
        /// 실패 메시지
        /// </summary>
        public string FailureMessage => failureMessage;

        /// <summary>
        /// 실패 색상
        /// </summary>
        public Color FailureColor => failureColor;

        /// <summary>
        /// 메시지 지속시간
        /// </summary>
        public float MessageDuration => messageDuration;
        #endregion

        #region Public Methods
        /// <summary>
        /// 조건에 맞는지 확인
        /// </summary>
        public bool MatchesConditions(ComboFailureContext context)
        {
            if (conditions == null || conditions.Length == 0)
                return true;

            if (requireAllConditions)
            {
                return conditions.All(condition => condition.Matches(context));
            }
            else
            {
                return conditions.Any(condition => condition.Matches(context));
            }
        }

        /// <summary>
        /// 랜덤하게 페널티 선택
        /// </summary>
        public List<FallbackPenalty> SelectPenalties()
        {
            var selectedPenalties = new List<FallbackPenalty>();

            if (penalties == null || penalties.Length == 0)
                return selectedPenalties;

            if (useWeightedRandom)
            {
                selectedPenalties = SelectWeightedPenalties();
            }
            else
            {
                selectedPenalties = SelectUniformPenalties();
            }

            // 최대 페널티 개수 제한
            if (selectedPenalties.Count > maxPenaltyCount)
            {
                selectedPenalties = selectedPenalties.Take(maxPenaltyCount).ToList();
            }

            return selectedPenalties;
        }

        /// <summary>
        /// 총 확률 계산
        /// </summary>
        public float GetTotalProbability()
        {
            if (penalties == null || penalties.Length == 0)
                return 0f;

            return penalties.Sum(p => p.probability);
        }

        /// <summary>
        /// 페널티 유효성 검증
        /// </summary>
        public bool ValidatePenalties(out string errorMessage)
        {
            errorMessage = "";

            if (penalties == null || penalties.Length == 0)
            {
                errorMessage = "페널티가 설정되지 않았습니다.";
                return false;
            }

            float totalProbability = GetTotalProbability();
            if (totalProbability <= 0f)
            {
                errorMessage = "총 확률이 0 이하입니다.";
                return false;
            }

            if (useWeightedRandom && totalProbability > 100f)
            {
                errorMessage = "가중치 랜덤 모드에서 총 확률이 100%를 초과했습니다.";
                return false;
            }

            foreach (var penalty in penalties)
            {
                if (!penalty.IsValid(out string penaltyError))
                {
                    errorMessage = $"페널티 '{penalty.penaltyName}': {penaltyError}";
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 가중치 기반 페널티 선택
        /// </summary>
        private List<FallbackPenalty> SelectWeightedPenalties()
        {
            var results = new List<FallbackPenalty>();
            float totalWeight = penalties.Sum(p => p.probability);

            for (int i = 0; i < maxPenaltyCount; i++)
            {
                float randomValue = UnityEngine.Random.Range(0f, totalWeight);
                float currentWeight = 0f;

                foreach (var penalty in penalties)
                {
                    currentWeight += penalty.probability;
                    if (randomValue <= currentWeight)
                    {
                        results.Add(penalty);
                        break;
                    }
                }
            }

            return results.Distinct().ToList();
        }

        /// <summary>
        /// 균등 확률 페널티 선택
        /// </summary>
        private List<FallbackPenalty> SelectUniformPenalties()
        {
            var results = new List<FallbackPenalty>();
            var availablePenalties = penalties.ToList();

            for (int i = 0; i < maxPenaltyCount && availablePenalties.Count > 0; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, availablePenalties.Count);
                results.Add(availablePenalties[randomIndex]);
                availablePenalties.RemoveAt(randomIndex);
            }

            return results;
        }
        #endregion

        #region Unity Methods
        private void OnValidate()
        {
            // Inspector에서 값이 변경될 때 유효성 검증
            if (penalties != null)
            {
                foreach (var penalty in penalties)
                {
                    penalty.ValidateInspector();
                }
            }

            // 조건 유효성 검증
            if (conditions != null)
            {
                foreach (var condition in conditions)
                {
                    condition.ValidateInspector();
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 개별 페널티 정의
    /// </summary>
    [Serializable]
    public class FallbackPenalty
    {
        [Header("기본 정보")]
        public string penaltyName;
        public string description;
        [Range(0f, 100f)]
        public float probability = 50f;

        [Header("페널티 타입")]
        public PenaltyType type = PenaltyType.HealthDamage;

        [Header("수치 설정")]
        public int minValue = 1;
        public int maxValue = 3;
        public bool usePercentage = false;

        [Header("추가 효과")]
        public bool disableNextTurn = false;
        public bool preventCombo = false;
        public int turnDuration = 1;

        /// <summary>
        /// 페널티 유효성 검증
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrEmpty(penaltyName))
            {
                errorMessage = "페널티 이름이 없습니다.";
                return false;
            }

            if (probability < 0f || probability > 100f)
            {
                errorMessage = "확률은 0-100 사이여야 합니다.";
                return false;
            }

            if (minValue > maxValue)
            {
                errorMessage = "최소값이 최대값보다 큽니다.";
                return false;
            }

            if (minValue < 0)
            {
                errorMessage = "값은 0 이상이어야 합니다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Inspector 유효성 검증
        /// </summary>
        public void ValidateInspector()
        {
            probability = Mathf.Clamp(probability, 0f, 100f);
            minValue = Mathf.Max(0, minValue);
            maxValue = Mathf.Max(minValue, maxValue);
            turnDuration = Mathf.Max(1, turnDuration);
        }

        /// <summary>
        /// 실제 페널티 값 계산
        /// </summary>
        public int GetPenaltyValue()
        {
            return UnityEngine.Random.Range(minValue, maxValue + 1);
        }
    }

    /// <summary>
    /// 페널티 적용 조건
    /// </summary>
    [Serializable]
    public class FallbackCondition
    {
        [Header("조건 타입")]
        public ConditionType conditionType = ConditionType.Always;

        [Header("값 조건")]
        public int targetValue;
        public ComparisonType comparison = ComparisonType.Equal;

        [Header("카드 조건")]
        public Cards.CardType requiredCardType = Cards.CardType.Element;
        public Cards.ElementType requiredElement = Cards.ElementType.Fire;

        /// <summary>
        /// 조건 매칭 확인
        /// </summary>
        public bool Matches(ComboFailureContext context)
        {
            switch (conditionType)
            {
                case ConditionType.Always:
                    return true;

                case ConditionType.PlayerHealth:
                    return CompareValue(context.PlayerHealth, targetValue);

                case ConditionType.PlayerMana:
                    return CompareValue(context.PlayerMana, targetValue);

                case ConditionType.TurnNumber:
                    return CompareValue(context.TurnNumber, targetValue);

                case ConditionType.CardCount:
                    return CompareValue(context.HandCardCount, targetValue);

                case ConditionType.CardType:
                    return context.HasCardType(requiredCardType);

                case ConditionType.Element:
                    return context.HasElement(requiredElement);

                default:
                    return true;
            }
        }

        /// <summary>
        /// 값 비교
        /// </summary>
        private bool CompareValue(int actual, int target)
        {
            switch (comparison)
            {
                case ComparisonType.Equal:
                    return actual == target;
                case ComparisonType.Greater:
                    return actual > target;
                case ComparisonType.GreaterOrEqual:
                    return actual >= target;
                case ComparisonType.Less:
                    return actual < target;
                case ComparisonType.LessOrEqual:
                    return actual <= target;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Inspector 유효성 검증
        /// </summary>
        public void ValidateInspector()
        {
            targetValue = Mathf.Max(0, targetValue);
        }
    }

    /// <summary>
    /// 조합 실패 컨텍스트
    /// </summary>
    public class ComboFailureContext
    {
        public int PlayerHealth { get; set; }
        public int PlayerMana { get; set; }
        public int TurnNumber { get; set; }
        public int HandCardCount { get; set; }
        public List<Cards.Card> HandCards { get; set; }
        public Cards.Card[] FailedCards { get; set; }
        public Cards.ElementType CurrentField { get; set; }

        public ComboFailureContext()
        {
            HandCards = new List<Cards.Card>();
            FailedCards = new Cards.Card[0];
            CurrentField = Cards.ElementType.None;
        }

        /// <summary>
        /// 특정 카드 타입 보유 확인
        /// </summary>
        public bool HasCardType(Cards.CardType cardType)
        {
            return HandCards?.Any(card => card.Type == cardType) ?? false;
        }

        /// <summary>
        /// 특정 속성 보유 확인
        /// </summary>
        public bool HasElement(Cards.ElementType element)
        {
            return HandCards?.Any(card => card.Element == element) ?? false;
        }
    }

    /// <summary>
    /// 페널티 타입 열거형
    /// </summary>
    public enum PenaltyType
    {
        HealthDamage,     // 체력 감소
        ManaDrain,        // 마나 소모
        CardDiscard,      // 카드 버리기
        TurnSkip,         // 턴 스킵
        ComboBlock,       // 조합 차단
        GoldLoss,         // 골드 손실
        DebuffApply       // 디버프 적용
    }

    /// <summary>
    /// 조건 타입 열거형
    /// </summary>
    public enum ConditionType
    {
        Always,           // 항상
        PlayerHealth,     // 플레이어 체력
        PlayerMana,       // 플레이어 마나
        TurnNumber,       // 턴 수
        CardCount,        // 카드 수
        CardType,         // 카드 타입
        Element           // 속성
    }

    /// <summary>
    /// 비교 타입 열거형
    /// </summary>
    public enum ComparisonType
    {
        Equal,            // 같음
        Greater,          // 초과
        GreaterOrEqual,   // 이상
        Less,             // 미만
        LessOrEqual       // 이하
    }
}