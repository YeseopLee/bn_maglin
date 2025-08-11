using UnityEngine;
using System;
using System.Collections.Generic;
using Maglin.Battle;

namespace Maglin.Cards
{
    /// <summary>
    /// 조합 실행 결과를 나타내는 클래스
    /// </summary>
    [Serializable]
    public class ComboExecutionResult
    {
        #region Fields
        [SerializeField] private bool success;
        [SerializeField] private CardCombination combinationKey;
        [SerializeField] private CardSO resultCardData;
        [SerializeField] private Card resultCardInstance;
        [SerializeField] private string errorMessage;
        [SerializeField] private float executionTime;
        [SerializeField] private ComboAnimationData animationData;
        #endregion

        #region Properties
        /// <summary>
        /// 조합 성공 여부
        /// </summary>
        public bool Success => success;

        /// <summary>
        /// 사용된 조합 키
        /// </summary>
        public CardCombination CombinationKey => combinationKey;

        /// <summary>
        /// 결과 카드 데이터 (성공 시)
        /// </summary>
        public CardSO ResultCardData => resultCardData;

        /// <summary>
        /// 결과 카드 인스턴스 (성공 시)
        /// </summary>
        public Card ResultCardInstance => resultCardInstance;

        /// <summary>
        /// 오류 메시지 (실패 시)
        /// </summary>
        public string ErrorMessage => errorMessage;

        /// <summary>
        /// 실행 시간 (성능 측정용)
        /// </summary>
        public float ExecutionTime => executionTime;

        /// <summary>
        /// 애니메이션 데이터
        /// </summary>
        public ComboAnimationData AnimationData => animationData;

        /// <summary>
        /// 유효한 결과인지 확인
        /// </summary>
        public bool IsValid => success && resultCardData != null && resultCardInstance != null;
        #endregion

        #region Constructors
        /// <summary>
        /// 성공 결과 생성자
        /// </summary>
        public ComboExecutionResult(CardCombination combination, CardSO cardData, Card cardInstance, float time, ComboAnimationData animation = null)
        {
            success = true;
            combinationKey = combination;
            resultCardData = cardData;
            resultCardInstance = cardInstance;
            errorMessage = "";
            executionTime = time;
            animationData = animation ?? new ComboAnimationData();
        }

        /// <summary>
        /// 실패 결과 생성자
        /// </summary>
        public ComboExecutionResult(CardCombination combination, string error, float time)
        {
            success = false;
            combinationKey = combination;
            resultCardData = null;
            resultCardInstance = null;
            errorMessage = error;
            executionTime = time;
            animationData = new ComboAnimationData();
        }

        /// <summary>
        /// 빈 실패 결과 생성자
        /// </summary>
        public ComboExecutionResult(string error)
        {
            success = false;
            combinationKey = CardCombination.Empty;
            resultCardData = null;
            resultCardInstance = null;
            errorMessage = error;
            executionTime = 0f;
            animationData = new ComboAnimationData();
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 결과 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            if (success)
            {
                return $"ComboResult[SUCCESS] {combinationKey} -> {resultCardData?.CardName} ({executionTime:F3}ms)";
            }
            else
            {
                return $"ComboResult[FAILED] {combinationKey} - {errorMessage} ({executionTime:F3}ms)";
            }
        }

        /// <summary>
        /// 상세 디버그 정보 반환
        /// </summary>
        public string GetDebugInfo()
        {
            var info = $"ComboExecutionResult[\n";
            info += $"  Success: {success}\n";
            info += $"  Combination: {combinationKey.GetDebugInfo()}\n";
            info += $"  Result Card: {(resultCardData != null ? resultCardData.CardName : "null")}\n";
            info += $"  Instance ID: {(resultCardInstance != null ? resultCardInstance.InstanceId.ToString() : "null")}\n";
            info += $"  Error: {errorMessage}\n";
            info += $"  Execution Time: {executionTime:F3}ms\n";
            info += $"  Animation: {animationData?.GetDebugInfo()}\n";
            info += $"]";
            return info;
        }
        #endregion
    }

    /// <summary>
    /// 조합 애니메이션 데이터
    /// </summary>
    [Serializable]
    public class ComboAnimationData
    {
        [Header("애니메이션 설정")]
        public bool enableAnimation = true;
        public float animationDuration = 1.0f;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1.2f);
        public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("이펙트 설정")]
        public VFXEffectSO comboEffect;
        public AudioClip comboSound;
        public Color glowColor = Color.yellow;
        public float glowIntensity = 2.0f;

        [Header("카메라 설정")]
        public bool enableCameraShake = true;
        public float shakeIntensity = 0.1f;
        public float shakeDuration = 0.3f;

        [Header("UI 피드백")]
        public bool showComboText = true;
        public string comboTextFormat = "COMBO!";
        public Color comboTextColor = Color.yellow;

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public ComboAnimationData()
        {
            enableAnimation = true;
            animationDuration = 1.0f;
            scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1.2f);
            alphaCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            glowColor = Color.yellow;
            glowIntensity = 2.0f;
            enableCameraShake = true;
            shakeIntensity = 0.1f;
            shakeDuration = 0.3f;
            showComboText = true;
            comboTextFormat = "COMBO!";
            comboTextColor = Color.yellow;
        }

        /// <summary>
        /// 디버그 정보 반환
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Animation[Enabled: {enableAnimation}, Duration: {animationDuration}s, Effect: {(comboEffect != null ? comboEffect.EffectName : "null")}]";
        }
    }

    /// <summary>
    /// 조합 실행 컨텍스트 - 실행 환경 정보 저장
    /// </summary>
    [Serializable]
    public class ComboExecutionContext
    {
        [Header("실행 환경")]
        public Card[] inputCards;
        public ElementType currentField;
        public Vector3 executionPosition;
        public Transform executionParent;

        [Header("실행 옵션")]
        public bool consumeCards = true;
        public bool playAnimation = true;
        public bool playSound = true;
        public bool triggerEvents = true;

        [Header("성능 설정")]
        public bool enableProfiling = false;
        public int maxRetryAttempts = 3;

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public ComboExecutionContext()
        {
            inputCards = new Card[0];
            currentField = ElementType.None;
            executionPosition = Vector3.zero;
            executionParent = null;
            consumeCards = true;
            playAnimation = true;
            playSound = true;
            triggerEvents = true;
            enableProfiling = false;
            maxRetryAttempts = 3;
        }

        /// <summary>
        /// 카드 배열 생성자
        /// </summary>
        public ComboExecutionContext(Card[] cards, ElementType field = ElementType.None)
        {
            inputCards = cards ?? new Card[0];
            currentField = field;
            executionPosition = Vector3.zero;
            executionParent = null;
            consumeCards = true;
            playAnimation = true;
            playSound = true;
            triggerEvents = true;
            enableProfiling = false;
            maxRetryAttempts = 3;
        }

        /// <summary>
        /// 유효한 컨텍스트인지 확인
        /// </summary>
        public bool IsValid()
        {
            return inputCards != null && inputCards.Length > 0;
        }

        /// <summary>
        /// 컨텍스트 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            return $"ComboContext[Cards: {inputCards?.Length ?? 0}, Field: {currentField}, ConsumeCards: {consumeCards}]";
        }
    }

    /// <summary>
    /// 조합 실패 결과
    /// </summary>
    [Serializable]
    public class ComboFailureResult
    {
        [Header("실패 정보")]
        [SerializeField] private ComboFailureContext failureContext;
        [SerializeField] private List<FallbackPenalty> appliedPenalties;
        [SerializeField] private float processingTime;
        [SerializeField] private bool penaltiesApplied;

        [Header("결과 데이터")]
        [SerializeField] private int totalHealthDamage;
        [SerializeField] private int totalManaDrain;
        [SerializeField] private int totalGoldLoss;
        [SerializeField] private int cardsDiscarded;
        [SerializeField] private bool turnSkipped;
        [SerializeField] private bool comboBlocked;

        #region Properties
        /// <summary>
        /// 실패 컨텍스트
        /// </summary>
        public ComboFailureContext FailureContext => failureContext;

        /// <summary>
        /// 적용된 페널티 목록
        /// </summary>
        public List<FallbackPenalty> AppliedPenalties => appliedPenalties;

        /// <summary>
        /// 처리 시간
        /// </summary>
        public float ProcessingTime => processingTime;

        /// <summary>
        /// 페널티 적용 여부
        /// </summary>
        public bool PenaltiesApplied => penaltiesApplied;

        /// <summary>
        /// 총 체력 피해
        /// </summary>
        public int TotalHealthDamage => totalHealthDamage;

        /// <summary>
        /// 총 마나 소모
        /// </summary>
        public int TotalManaDrain => totalManaDrain;

        /// <summary>
        /// 총 골드 손실
        /// </summary>
        public int TotalGoldLoss => totalGoldLoss;

        /// <summary>
        /// 버려진 카드 수
        /// </summary>
        public int CardsDiscarded => cardsDiscarded;

        /// <summary>
        /// 턴 스킵 여부
        /// </summary>
        public bool TurnSkipped => turnSkipped;

        /// <summary>
        /// 조합 차단 여부
        /// </summary>
        public bool ComboBlocked => comboBlocked;
        #endregion

        #region Constructors
        /// <summary>
        /// 생성자
        /// </summary>
        public ComboFailureResult(ComboFailureContext context, List<FallbackPenalty> penalties, float time)
        {
            failureContext = context;
            appliedPenalties = penalties ?? new List<FallbackPenalty>();
            processingTime = time;
            penaltiesApplied = false;

            // 초기값 설정
            totalHealthDamage = 0;
            totalManaDrain = 0;
            totalGoldLoss = 0;
            cardsDiscarded = 0;
            turnSkipped = false;
            comboBlocked = false;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 페널티 결과 기록
        /// </summary>
        public void RecordPenaltyResults(int healthDamage, int manaDrain, int goldLoss, int discardCount, bool skipTurn, bool blockCombo)
        {
            totalHealthDamage += healthDamage;
            totalManaDrain += manaDrain;
            totalGoldLoss += goldLoss;
            cardsDiscarded += discardCount;
            turnSkipped = turnSkipped || skipTurn;
            comboBlocked = comboBlocked || blockCombo;
            penaltiesApplied = true;
        }

        /// <summary>
        /// 결과 요약 문자열
        /// </summary>
        public string GetSummary()
        {
            var summary = "조합 실패 결과:\n";

            if (totalHealthDamage > 0)
                summary += $"- 체력 피해: {totalHealthDamage}\n";

            if (totalManaDrain > 0)
                summary += $"- 마나 소모: {totalManaDrain}\n";

            if (totalGoldLoss > 0)
                summary += $"- 골드 손실: {totalGoldLoss}\n";

            if (cardsDiscarded > 0)
                summary += $"- 카드 버림: {cardsDiscarded}장\n";

            if (turnSkipped)
                summary += "- 턴 스킵됨\n";

            if (comboBlocked)
                summary += "- 조합 차단됨\n";

            return summary;
        }

        /// <summary>
        /// 상세 디버그 정보
        /// </summary>
        public string GetDebugInfo()
        {
            var info = $"ComboFailureResult[\n";
            info += $"  Processing Time: {processingTime:F3}ms\n";
            info += $"  Penalties Applied: {penaltiesApplied}\n";
            info += $"  Applied Penalties: {appliedPenalties?.Count ?? 0}\n";
            info += $"  Health Damage: {totalHealthDamage}\n";
            info += $"  Mana Drain: {totalManaDrain}\n";
            info += $"  Gold Loss: {totalGoldLoss}\n";
            info += $"  Cards Discarded: {cardsDiscarded}\n";
            info += $"  Turn Skipped: {turnSkipped}\n";
            info += $"  Combo Blocked: {comboBlocked}\n";
            info += $"]";
            return info;
        }
        #endregion
    }
}