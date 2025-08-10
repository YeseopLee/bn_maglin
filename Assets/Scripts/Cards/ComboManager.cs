using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Maglin.Core;
using Maglin.Player;
using Maglin.Audio;
using Maglin.Battle;
using Maglin.Cards;

namespace Maglin.Cards
{
    /// <summary>
    /// 카드 조합을 관리하는 매니저 - 조합 검색 및 실행을 담당
    /// </summary>
    public class ComboManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static ComboManager _instance;

        public static ComboManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ComboManager>();

                    if (_instance == null)
                    {
                        GameObject comboManagerObject = new GameObject("ComboManager");
                        _instance = comboManagerObject.AddComponent<ComboManager>();
                        DontDestroyOnLoad(comboManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 조합 시도 이벤트 (조합키, 성공여부)
        /// </summary>
        public static event Action<CardCombination, bool> OnComboAttempted;

        /// <summary>
        /// 조합 성공 이벤트 (조합키, 결과카드)
        /// </summary>
        public static event Action<CardCombination, CardSO> OnComboSuccess;

        /// <summary>
        /// 조합 실패 이벤트 (조합키, 실패사유)
        /// </summary>
        public static event Action<CardCombination, string> OnComboFailed;

        /// <summary>
        /// 조합 캐시 로드 완료 이벤트
        /// </summary>
        public static event Action OnComboCacheLoaded;

        /// <summary>
        /// 조합 실행 완료 이벤트 (완전한 결과 포함)
        /// </summary>
        public static event Action<ComboExecutionResult> OnComboExecuted;

        /// <summary>
        /// 카드 소모 이벤트 (조합으로 인한)
        /// </summary>
        public static event Action<Card[]> OnCardsConsumed;

        /// <summary>
        /// 조합 애니메이션 시작 이벤트
        /// </summary>
        public static event Action<ComboAnimationData, ComboExecutionContext> OnComboAnimationStarted;

        /// <summary>
        /// 조합 실패 페널티 적용 이벤트
        /// </summary>
        public static event Action<ComboFailureContext, List<FallbackPenalty>> OnComboFailurePenalty;

        /// <summary>
        /// 조합 실패 처리 완료 이벤트
        /// </summary>
        public static event Action<ComboFailureContext, ComboFailureResult> OnComboFailureProcessed;
        #endregion

        #region Fields
        [Header("조합 설정")]
        [SerializeField] private bool enableFieldEffects = true;
        [SerializeField] private bool strictValidation = true;
        [SerializeField] private int maxCacheSize = 10000;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool logCacheStatistics = false;

        [Header("실패 처리 설정")]
        [SerializeField] private ComboFallbackSO[] fallbackPenalties;
        [SerializeField] private bool enableFailurePenalties = true;
        [SerializeField] private float failureProcessingDelay = 0.5f;

        // 조합 캐시 - 핵심 데이터 구조
        private Dictionary<CardCombination, CardSO> combinationCache;

        // 로드된 모든 카드 데이터
        private List<CardSO> allCards;

        // 조합 가능한 카드들만 필터링
        private List<CardSO> combinableCards;

        // 통계 및 성능 모니터링
        private int cacheHits = 0;
        private int cacheMisses = 0;
        private int totalCombinationAttempts = 0;

        // 초기화 상태
        private bool isInitialized = false;
        private bool isCacheLoaded = false;
        #endregion

        #region Properties
        /// <summary>
        /// 초기화 완료 여부
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// 캐시 로드 완료 여부
        /// </summary>
        public bool IsCacheLoaded => isCacheLoaded;

        /// <summary>
        /// 현재 캐시된 조합 수
        /// </summary>
        public int CachedCombinationCount => combinationCache?.Count ?? 0;

        /// <summary>
        /// 로드된 전체 카드 수
        /// </summary>
        public int TotalCardCount => allCards?.Count ?? 0;

        /// <summary>
        /// 조합 가능한 카드 수
        /// </summary>
        public int CombinableCardCount => combinableCards?.Count ?? 0;

        /// <summary>
        /// 캐시 히트율 (0.0 ~ 1.0)
        /// </summary>
        public float CacheHitRate => totalCombinationAttempts > 0 ? (float)cacheHits / totalCombinationAttempts : 0f;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeComboManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // GameManager 초기화 완료 대기 후 캐시 로드
            if (GameManager.Instance != null && GameManager.Instance.IsGameInitialized)
            {
                LoadCombinationCache();
            }
            else
            {
                GameManager.OnGameInitialized += LoadCombinationCache;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// ComboManager 초기화
        /// </summary>
        private void InitializeComboManager()
        {
            if (debugMode)
                Debug.Log("[ComboManager] 초기화 시작");

            // 컬렉션 초기화
            combinationCache = new Dictionary<CardCombination, CardSO>();
            allCards = new List<CardSO>();
            combinableCards = new List<CardSO>();

            // 통계 초기화
            ResetStatistics();

            // 실패 처리 시스템 초기화
            InitializeFailureSystem();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[ComboManager] 초기화 완료");
        }

        /// <summary>
        /// 조합 캐시 로드 (게임 시작 시 호출)
        /// </summary>
        public void LoadCombinationCache()
        {
            if (isCacheLoaded)
            {
                if (debugMode)
                    Debug.Log("[ComboManager] 캐시가 이미 로드되어 있습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[ComboManager] 조합 캐시 로드 시작");

            try
            {
                // 모든 카드 데이터 로드
                LoadAllCards();

                // 조합 가능한 카드 필터링
                FilterCombinableCards();

                // 조합 캐시 생성
                BuildCombinationCache();

                isCacheLoaded = true;

                // 이벤트 발생
                OnComboCacheLoaded?.Invoke();

                // 통계 출력
                if (logCacheStatistics)
                {
                    LogCacheStatistics();
                }

                if (debugMode)
                    Debug.Log($"[ComboManager] 조합 캐시 로드 완료 - {CachedCombinationCount}개 조합 캐시됨");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComboManager] 조합 캐시 로드 실패: {ex.Message}");
                isCacheLoaded = false;
            }
        }

        /// <summary>
        /// 모든 카드 데이터 로드
        /// </summary>
        private void LoadAllCards()
        {
            allCards.Clear();

            // Resources 폴더에서 모든 CardSO 로드
            CardSO[] loadedCards = Resources.LoadAll<CardSO>("");

            Debug.Log($"[ComboManager] {loadedCards.Length}개의 카드 로드됨");

            if (loadedCards == null || loadedCards.Length == 0)
            {
                Debug.LogWarning("[ComboManager] Resources 폴더에서 CardSO를 찾을 수 없습니다.");
                return;
            }

            allCards.AddRange(loadedCards);

            if (debugMode)
                Debug.Log($"[ComboManager] {allCards.Count}개의 카드 로드됨");
        }

        /// <summary>
        /// 조합 가능한 카드들만 필터링
        /// </summary>
        private void FilterCombinableCards()
        {
            combinableCards.Clear();

            foreach (var card in allCards)
            {
                if (card == null) continue;

                // 조합 카드 (결과물)이거나 조합에 필요한 카드들 포함
                if (card.Type == CardType.Combo ||
                    card.Type == CardType.Element ||
                    card.Type == CardType.Active1 ||
                    card.Type == CardType.Active2)
                {
                    combinableCards.Add(card);
                }
            }

            if (debugMode)
                Debug.Log($"[ComboManager] {combinableCards.Count}개의 조합 가능한 카드 식별됨");
        }

        /// <summary>
        /// 조합 캐시 구축
        /// </summary>
        private void BuildCombinationCache()
        {
            combinationCache.Clear();

            // 조합 카드들의 요구사항을 기반으로 캐시 구축
            foreach (var comboCard in combinableCards.Where(c => c.Type == CardType.Combo))
            {
                if (comboCard.RequiredCombinations == null || comboCard.RequiredCombinations.Length == 0)
                    continue;

                foreach (var requirement in comboCard.RequiredCombinations)
                {
                    try
                    {
                        var combinationKey = CreateCombinationKey(requirement);

                        if (debugMode)
                            Debug.Log($"[ComboManager] {comboCard.CardName} 조합 키 생성: {combinationKey}, 필드: {requirement.requiredField?.EffectName ?? "None"}");

                        if (combinationKey.IsValid)
                        {
                            // 중복 조합 체크
                            if (combinationCache.ContainsKey(combinationKey))
                            {
                                if (strictValidation)
                                {
                                    Debug.LogWarning($"[ComboManager] 중복 조합 발견: {combinationKey} -> {comboCard.CardName} (기존: {combinationCache[combinationKey].CardName})");
                                    continue; // 기존 조합 유지
                                }
                            }

                            combinationCache[combinationKey] = comboCard;

                            if (debugMode)
                                Debug.Log($"[ComboManager] 조합 캐시 추가: {combinationKey} -> {comboCard.CardName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[ComboManager] 무효한 조합 요구사항: {comboCard.CardName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ComboManager] 조합 캐시 구축 중 오류 ({comboCard.CardName}): {ex.Message}");
                    }
                }
            }

            // 캐시 크기 제한 체크
            if (combinationCache.Count > maxCacheSize)
            {
                Debug.LogWarning($"[ComboManager] 캐시 크기가 제한을 초과했습니다: {combinationCache.Count}/{maxCacheSize}");
            }
        }

        /// <summary>
        /// CardCombinationData로부터 조합 키 생성
        /// </summary>
        private CardCombination CreateCombinationKey(CardCombinationData requirement)
        {
            if (requirement == null)
                throw new ArgumentNullException(nameof(requirement));

            // Element 타입 추출
            ElementType elementType = requirement.elementCard != null ? requirement.elementCard.Element : ElementType.None;

            // 필드 효과 처리
            ElementType fieldType = ElementType.None;
            if (enableFieldEffects && requirement.requiredField != null)
            {
                fieldType = requirement.requiredField.FieldElement;
            }

            return CardCombination.CreateNormalized(
                elementType,
                requirement.active1Card,
                requirement.active2Card,
                fieldType
            );
        }

        /// <summary>
        /// 최적의 조합 찾기 (필드 조건 우선)
        /// </summary>
        private CardSO FindBestCombination(CardCombination targetKey)
        {
            // 1단계: 정확한 매칭 (필드 포함) 찾기
            if (combinationCache.TryGetValue(targetKey, out CardSO exactMatch))
            {
                if (debugMode)
                    Debug.Log($"[ComboManager] 정확한 필드 매칭 발견: {targetKey} -> {exactMatch.CardName}");
                return exactMatch;
            }

            // 2단계: 필드 없는 버전 찾기 (기본 조합)
            if (targetKey.Field != ElementType.None)
            {
                var fallbackKey = CardCombination.CreateNormalized(
                    targetKey.Element,
                    targetKey.Active1,
                    targetKey.Active2,
                    ElementType.None
                );

                if (combinationCache.TryGetValue(fallbackKey, out CardSO fallbackMatch))
                {
                    if (debugMode)
                        Debug.Log($"[ComboManager] 기본 조합 매칭 발견: {fallbackKey} -> {fallbackMatch.CardName}");
                    return fallbackMatch;
                }
            }

            // 3단계: 매칭 실패
            if (debugMode)
                Debug.Log($"[ComboManager] 조합 매칭 실패: {targetKey}");
            return null;
        }
        #endregion

        #region Public API
        /// <summary>
        /// 완전한 조합 실행 - 컨텍스트 기반
        /// </summary>
        public ComboExecutionResult ExecuteCombination(ComboExecutionContext context)
        {
            if (!isCacheLoaded)
            {
                return new ComboExecutionResult("캐시가 로드되지 않았습니다.");
            }

            if (context == null || !context.IsValid())
            {
                return new ComboExecutionResult("유효하지 않은 실행 컨텍스트입니다.");
            }

            var startTime = Time.realtimeSinceStartup;

            try
            {
                // 조합 키 생성
                var combinationKey = CardCombination.CreateFromCards(context.inputCards, context.currentField);

                // 조합 시도
                var result = ExecuteCombinationInternal(combinationKey, context);

                // 실행 시간 계산
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f; // ms 단위

                if (result.Success)
                {
                    // 성공 시 추가 처리
                    if (context.consumeCards)
                    {
                        ConsumeInputCards(context.inputCards);
                    }

                    if (context.playAnimation && result.AnimationData.enableAnimation)
                    {
                        TriggerComboAnimation(result.AnimationData, context);
                    }

                    if (context.triggerEvents)
                    {
                        OnComboAttempted?.Invoke(combinationKey, true);
                        OnComboSuccess?.Invoke(combinationKey, result.ResultCardData);
                        OnComboExecuted?.Invoke(result);
                    }
                }
                else
                {
                    // 실패 시 추가 처리
                    if (context.triggerEvents)
                    {
                        OnComboAttempted?.Invoke(combinationKey, false);
                        OnComboFailed?.Invoke(combinationKey, result.ErrorMessage);
                    }

                    // 실패 페널티 처리
                    if (enableFailurePenalties && context.triggerEvents)
                    {
                        ProcessComboFailure(context, combinationKey, result.ErrorMessage);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                var errorResult = new ComboExecutionResult(CardCombination.Empty, ex.Message, executionTime);

                if (context.triggerEvents)
                {
                    OnComboFailed?.Invoke(CardCombination.Empty, ex.Message);
                }

                Debug.LogError($"[ComboManager] 조합 실행 중 예외 발생: {ex.Message}");
                return errorResult;
            }
        }

        /// <summary>
        /// 조합 시도 - 카드 배열로부터 (기존 호환성 유지)
        /// </summary>
        public CardSO AttemptCombination(Card[] cards, ElementType currentField = ElementType.None)
        {
            if (!isCacheLoaded)
            {
                Debug.LogError("[ComboManager] 캐시가 로드되지 않았습니다.");
                return null;
            }

            totalCombinationAttempts++;

            try
            {
                // 조합 키 생성
                var combinationKey = CardCombination.CreateFromCards(cards, currentField);

                return AttemptCombination(combinationKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComboManager] 조합 시도 중 오류: {ex.Message}");
                OnComboFailed?.Invoke(CardCombination.Empty, ex.Message);
                cacheMisses++;
                return null;
            }
        }

        /// <summary>
        /// 조합 시도 - 조합 키로부터
        /// </summary>
        public CardSO AttemptCombination(CardCombination combinationKey)
        {
            if (!isCacheLoaded)
            {
                Debug.LogError("[ComboManager] 캐시가 로드되지 않았습니다.");
                return null;
            }

            totalCombinationAttempts++;

            // 조합 키 유효성 검증
            if (!combinationKey.IsValid)
            {
                string error = "무효한 조합 키입니다.";
                OnComboFailed?.Invoke(combinationKey, error);
                cacheMisses++;
                return null;
            }

            // 추가 검증
            if (strictValidation)
            {
                if (!combinationKey.Validate(out string validationError))
                {
                    OnComboFailed?.Invoke(combinationKey, validationError);
                    cacheMisses++;
                    return null;
                }
            }

            // 캐시에서 조합 검색
            if (combinationCache.TryGetValue(combinationKey, out CardSO resultCard))
            {
                // 조합 성공
                cacheHits++;
                OnComboAttempted?.Invoke(combinationKey, true);
                OnComboSuccess?.Invoke(combinationKey, resultCard);

                if (debugMode)
                    Debug.Log($"[ComboManager] 조합 성공: {combinationKey} -> {resultCard.CardName}");

                return resultCard;
            }
            else
            {
                // 조합 실패
                cacheMisses++;
                string error = "일치하는 조합을 찾을 수 없습니다.";
                Debug.Log($"[ComboManager] 조합 실패: {combinationKey}");
                OnComboAttempted?.Invoke(combinationKey, false);
                OnComboFailed?.Invoke(combinationKey, error);

                // if (debugMode)

                return null;
            }
        }

        /// <summary>
        /// 특정 조합이 가능한지 확인 (실제 실행하지 않음)
        /// </summary>
        public bool CanCombine(Card[] cards, ElementType currentField = ElementType.None)
        {
            if (!isCacheLoaded) return false;

            try
            {
                var combinationKey = CardCombination.CreateFromCards(cards, currentField);
                return combinationCache.ContainsKey(combinationKey);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 특정 조합이 가능한지 확인 (조합 키 기준)
        /// </summary>
        public bool CanCombine(CardCombination combinationKey)
        {
            if (!isCacheLoaded || !combinationKey.IsValid) return false;

            return combinationCache.ContainsKey(combinationKey);
        }

        /// <summary>
        /// 특정 카드가 포함된 모든 조합 검색
        /// </summary>
        public List<CardCombination> FindCombinationsContaining(CardSO card)
        {
            var results = new List<CardCombination>();

            if (!isCacheLoaded || card == null) return results;

            foreach (var kvp in combinationCache)
            {
                if (kvp.Key.ContainsCard(card))
                {
                    results.Add(kvp.Key);
                }
            }

            return results;
        }

        /// <summary>
        /// 특정 속성이 포함된 모든 조합 검색
        /// </summary>
        public List<CardCombination> FindCombinationsWithElement(ElementType elementType)
        {
            var results = new List<CardCombination>();

            if (!isCacheLoaded || elementType == ElementType.None) return results;

            foreach (var kvp in combinationCache)
            {
                if (kvp.Key.ContainsElement(elementType))
                {
                    results.Add(kvp.Key);
                }
            }

            return results;
        }
        #endregion

        #region Internal Execution Methods
        /// <summary>
        /// 내부 조합 실행 로직
        /// </summary>
        private ComboExecutionResult ExecuteCombinationInternal(CardCombination combinationKey, ComboExecutionContext context)
        {
            var startTime = Time.realtimeSinceStartup;

            // 조합 키 유효성 검증
            if (!combinationKey.IsValid)
            {
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                return new ComboExecutionResult(combinationKey, "무효한 조합 키입니다.", executionTime);
            }

            // 추가 검증
            if (strictValidation)
            {
                if (!combinationKey.Validate(out string validationError))
                {
                    var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                    return new ComboExecutionResult(combinationKey, validationError, executionTime);
                }
            }

            // 캐시에서 조합 검색 (필드 조건 우선)
            CardSO resultCardData = FindBestCombination(combinationKey);

            if (resultCardData != null)
            {
                // 조합 성공 - 카드 인스턴스 생성
                var resultCard = CreateCardInstance(resultCardData);
                var animationData = CreateAnimationData(resultCardData, context);
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;

                cacheHits++;
                totalCombinationAttempts++;

                if (debugMode)
                    Debug.Log($"[ComboManager] 조합 성공: {combinationKey} -> {resultCardData.CardName} ({executionTime:F3}ms)");

                return new ComboExecutionResult(combinationKey, resultCardData, resultCard, executionTime, animationData);
            }
            else
            {
                // 조합 실패
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                cacheMisses++;
                totalCombinationAttempts++;

                if (debugMode)
                    Debug.Log($"[ComboManager] 조합 실패: {combinationKey} ({executionTime:F3}ms)");

                return new ComboExecutionResult(combinationKey, "일치하는 조합을 찾을 수 없습니다.", executionTime);
            }
        }

        /// <summary>
        /// 카드 인스턴스 생성
        /// </summary>
        private Card CreateCardInstance(CardSO cardData)
        {
            if (cardData == null) return null;

            var cardInstance = new Card(cardData);

            // 추가 초기화 로직이 필요한 경우 여기에 구현
            // 예: 특수 효과, 임시 버프 등

            return cardInstance;
        }

        /// <summary>
        /// 애니메이션 데이터 생성
        /// </summary>
        private ComboAnimationData CreateAnimationData(CardSO resultCard, ComboExecutionContext context)
        {
            var animationData = new ComboAnimationData();

            // 결과 카드에 따른 애니메이션 설정 조정
            if (resultCard != null && resultCard.Effect != null)
            {
                animationData.comboEffect = resultCard.Effect;
            }

            if (resultCard != null && resultCard.Sound != null)
            {
                animationData.comboSound = resultCard.Sound;
            }

            // 컨텍스트에 따른 추가 설정
            animationData.enableAnimation = context.playAnimation;

            return animationData;
        }

        /// <summary>
        /// 입력 카드 소모 처리
        /// </summary>
        private void ConsumeInputCards(Card[] inputCards)
        {
            if (inputCards == null) return;

            // 조합창의 카드들은 조합 실행 후 자동으로 임시무덤으로 이동됩니다.
            // CardManager에서 처리하므로 여기서는 이벤트만 발생시킵니다.

            if (debugMode)
                Debug.Log($"[ComboManager] 카드 소모 처리: {inputCards.Length}장 - {string.Join(", ", inputCards.Select(c => c.CardName))}");

            // 카드 소모 이벤트 발생
            OnCardsConsumed?.Invoke(inputCards);
        }

        /// <summary>
        /// 조합 애니메이션 트리거
        /// </summary>
        private void TriggerComboAnimation(ComboAnimationData animationData, ComboExecutionContext context)
        {
            if (animationData == null || !animationData.enableAnimation) return;

            // 애니메이션 시작 이벤트 발생
            OnComboAnimationStarted?.Invoke(animationData, context);

            try
            {
                // 이펙트 재생
                if (animationData.comboEffect != null)
                {
                    var effectPosition = context.executionPosition;
                    var effectParent = context.executionParent;

                    GameObject.Instantiate(animationData.comboEffect, effectPosition, Quaternion.identity, effectParent);
                }

                // 사운드 재생
                if (animationData.comboSound != null && context.playSound)
                {
                    // AudioManager를 통한 사운드 재생
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(animationData.comboSound);
                    }
                }

                // 카메라 쉐이크
                if (animationData.enableCameraShake)
                {
                    // 카메라 쉐이크 로직 (추후 구현)
                    if (debugMode)
                        Debug.Log($"[ComboManager] 카메라 쉐이크 트리거: 강도 {animationData.shakeIntensity}, 지속시간 {animationData.shakeDuration}s");
                }

                // UI 피드백
                if (animationData.showComboText)
                {
                    // UI 텍스트 표시 로직 (추후 구현)
                    if (debugMode)
                        Debug.Log($"[ComboManager] 콤보 텍스트 표시: {animationData.comboTextFormat}");
                }

                if (debugMode)
                    Debug.Log($"[ComboManager] 조합 애니메이션 재생: {animationData.GetDebugInfo()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComboManager] 애니메이션 재생 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 배치 조합 실행 (여러 조합을 한 번에 처리)
        /// </summary>
        public List<ComboExecutionResult> ExecuteBatchCombinations(List<ComboExecutionContext> contexts)
        {
            var results = new List<ComboExecutionResult>();

            if (contexts == null || contexts.Count == 0)
            {
                return results;
            }

            if (debugMode)
                Debug.Log($"[ComboManager] 배치 조합 실행 시작: {contexts.Count}개 조합");

            foreach (var context in contexts)
            {
                var result = ExecuteCombination(context);
                results.Add(result);

                // 실패 시 배치 처리 중단 옵션 (설정 가능)
                if (!result.Success && strictValidation)
                {
                    if (debugMode)
                        Debug.LogWarning($"[ComboManager] 배치 조합 중단: {result.ErrorMessage}");
                    break;
                }
            }

            if (debugMode)
                Debug.Log($"[ComboManager] 배치 조합 실행 완료: {results.Count}개 결과");

            return results;
        }

        /// <summary>
        /// 조합 시뮬레이션 (실제 실행하지 않음)
        /// </summary>
        public ComboExecutionResult SimulateCombination(ComboExecutionContext context)
        {
            if (!isCacheLoaded)
            {
                return new ComboExecutionResult("캐시가 로드되지 않았습니다.");
            }

            if (context == null || !context.IsValid())
            {
                return new ComboExecutionResult("유효하지 않은 실행 컨텍스트입니다.");
            }

            var startTime = Time.realtimeSinceStartup;

            try
            {
                // 조합 키 생성
                var combinationKey = CardCombination.CreateFromCards(context.inputCards, context.currentField);

                // 시뮬레이션용 컨텍스트 생성 (실제 소모/애니메이션 없음)
                var simContext = new ComboExecutionContext(context.inputCards, context.currentField)
                {
                    consumeCards = false,
                    playAnimation = false,
                    playSound = false,
                    triggerEvents = false
                };

                return ExecuteCombinationInternal(combinationKey, simContext);
            }
            catch (Exception ex)
            {
                var executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                return new ComboExecutionResult(CardCombination.Empty, ex.Message, executionTime);
            }
        }
        #endregion

        #region Failure Processing Methods
        /// <summary>
        /// 조합 실패 처리 메인 로직
        /// </summary>
        private void ProcessComboFailure(ComboExecutionContext context, CardCombination failedCombination, string errorMessage)
        {
            if (!enableFailurePenalties || fallbackPenalties == null || fallbackPenalties.Length == 0)
            {
                if (debugMode)
                    Debug.Log("[ComboManager] 실패 페널티가 비활성화되어 있거나 설정되지 않았습니다.");
                return;
            }

            var startTime = Time.realtimeSinceStartup;

            try
            {
                // 실패 컨텍스트 생성
                var failureContext = CreateFailureContext(context, failedCombination, errorMessage);

                // 적용 가능한 페널티 찾기
                var applicableFallbacks = FindApplicableFallbacks(failureContext);

                if (applicableFallbacks.Count == 0)
                {
                    if (debugMode)
                        Debug.Log("[ComboManager] 조건에 맞는 페널티가 없습니다.");
                    return;
                }

                // 페널티 실행
                if (failureProcessingDelay > 0f)
                {
                    StartCoroutine(ProcessFailureWithDelay(failureContext, applicableFallbacks, startTime));
                }
                else
                {
                    ExecuteFailurePenalties(failureContext, applicableFallbacks, startTime);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComboManager] 실패 처리 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 실패 컨텍스트 생성
        /// </summary>
        private ComboFailureContext CreateFailureContext(ComboExecutionContext executionContext, CardCombination failedCombination, string errorMessage)
        {
            var failureContext = new ComboFailureContext();

            // 플레이어 상태 수집
            if (PlayerManager.Instance != null)
            {
                failureContext.PlayerHealth = PlayerManager.Instance.CurrentHealth;
                failureContext.PlayerMana = PlayerManager.Instance.CurrentMana;
            }

            // 배틀 상태 수집
            if (BattleManager.Instance != null)
            {
                failureContext.TurnNumber = BattleManager.Instance.TurnNumber;
            }

            // 카드 상태 수집
            if (CardManager.Instance != null)
            {
                // CardManager.Hand는 IReadOnlyList<CardSO>이므로 Card 객체로 변환 필요
                var handCardSOs = CardManager.Instance.Hand;
                failureContext.HandCards = new List<Card>();

                if (handCardSOs != null)
                {
                    foreach (var cardSO in handCardSOs)
                    {
                        var cardInstance = new Card(cardSO);
                        failureContext.HandCards.Add(cardInstance);
                    }
                }

                failureContext.HandCardCount = failureContext.HandCards?.Count ?? 0;
            }

            // 실행 컨텍스트에서 정보 복사
            failureContext.FailedCards = executionContext.inputCards;
            failureContext.CurrentField = executionContext.currentField;

            return failureContext;
        }

        /// <summary>
        /// 적용 가능한 페널티 찾기
        /// </summary>
        private List<ComboFallbackSO> FindApplicableFallbacks(ComboFailureContext context)
        {
            var applicableFallbacks = new List<ComboFallbackSO>();

            foreach (var fallback in fallbackPenalties)
            {
                if (fallback != null && fallback.MatchesConditions(context))
                {
                    // 페널티 유효성 검증
                    if (fallback.ValidatePenalties(out string error))
                    {
                        applicableFallbacks.Add(fallback);
                    }
                    else
                    {
                        Debug.LogWarning($"[ComboManager] 페널티 '{fallback.FallbackName}' 유효성 검증 실패: {error}");
                    }
                }
            }

            return applicableFallbacks;
        }

        /// <summary>
        /// 지연된 실패 처리
        /// </summary>
        private System.Collections.IEnumerator ProcessFailureWithDelay(ComboFailureContext context, List<ComboFallbackSO> fallbacks, float startTime)
        {
            yield return new WaitForSeconds(failureProcessingDelay);
            ExecuteFailurePenalties(context, fallbacks, startTime);
        }

        /// <summary>
        /// 실패 페널티 실행
        /// </summary>
        private void ExecuteFailurePenalties(ComboFailureContext context, List<ComboFallbackSO> applicableFallbacks, float startTime)
        {
            var selectedPenalties = new List<FallbackPenalty>();

            // 각 적용 가능한 페널티에서 랜덤 선택
            foreach (var fallback in applicableFallbacks)
            {
                var penalties = fallback.SelectPenalties();
                selectedPenalties.AddRange(penalties);

                // 실패 사운드 재생
                if (fallback.FailureSound != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(fallback.FailureSound);
                }
            }

            var processingTime = (Time.realtimeSinceStartup - startTime) * 1000f;
            var failureResult = new ComboFailureResult(context, selectedPenalties, processingTime);

            // 페널티 적용
            ApplyPenalties(selectedPenalties, failureResult);

            // 이벤트 발생
            OnComboFailurePenalty?.Invoke(context, selectedPenalties);
            OnComboFailureProcessed?.Invoke(context, failureResult);

            if (debugMode)
            {
                Debug.Log($"[ComboManager] 조합 실패 처리 완료: {selectedPenalties.Count}개 페널티 적용 ({processingTime:F3}ms)");
                Debug.Log(failureResult.GetSummary());
            }
        }

        /// <summary>
        /// 페널티 적용
        /// </summary>
        private void ApplyPenalties(List<FallbackPenalty> penalties, ComboFailureResult result)
        {
            int totalHealthDamage = 0;
            int totalManaDrain = 0;
            int totalGoldLoss = 0;
            int cardsDiscarded = 0;
            bool turnSkipped = false;
            bool comboBlocked = false;

            foreach (var penalty in penalties)
            {
                try
                {
                    var penaltyValue = penalty.GetPenaltyValue();

                    switch (penalty.type)
                    {
                        case PenaltyType.HealthDamage:
                            totalHealthDamage += penaltyValue;
                            if (PlayerManager.Instance != null)
                            {
                                PlayerManager.Instance.TakeDamage(penaltyValue);
                            }
                            break;

                        case PenaltyType.ManaDrain:
                            totalManaDrain += penaltyValue;
                            if (PlayerManager.Instance != null)
                            {
                                PlayerManager.Instance.SpendMana(penaltyValue);
                            }
                            break;

                        case PenaltyType.GoldLoss:
                            totalGoldLoss += penaltyValue;
                            if (PlayerManager.Instance != null)
                            {
                                PlayerManager.Instance.SpendGold(penaltyValue);
                            }
                            break;

                        case PenaltyType.CardDiscard:
                            cardsDiscarded += ApplyCardDiscardPenalty(penaltyValue);
                            break;

                        case PenaltyType.TurnSkip:
                            turnSkipped = true;
                            ApplyTurnSkipPenalty(penalty);
                            break;

                        case PenaltyType.ComboBlock:
                            comboBlocked = true;
                            ApplyComboBlockPenalty(penalty);
                            break;

                        case PenaltyType.DebuffApply:
                            ApplyDebuffPenalty(penalty, penaltyValue);
                            break;
                    }

                    if (debugMode)
                        Debug.Log($"[ComboManager] 페널티 적용: {penalty.penaltyName} (값: {penaltyValue})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ComboManager] 페널티 '{penalty.penaltyName}' 적용 중 오류: {ex.Message}");
                }
            }

            // 결과 기록
            result.RecordPenaltyResults(totalHealthDamage, totalManaDrain, totalGoldLoss,
                                      cardsDiscarded, turnSkipped, comboBlocked);
        }

        /// <summary>
        /// 카드 버리기 페널티 적용
        /// </summary>
        private int ApplyCardDiscardPenalty(int discardCount)
        {
            if (CardManager.Instance == null) return 0;

            // 실제 구현에서는 CardManager의 적절한 메서드 호출
            // 예: return CardManager.Instance.DiscardRandomCards(discardCount);

            if (debugMode)
                Debug.Log($"[ComboManager] 카드 버리기 페널티: {discardCount}장");

            return discardCount; // 임시 반환값
        }

        /// <summary>
        /// 턴 스킵 페널티 적용
        /// </summary>
        private void ApplyTurnSkipPenalty(FallbackPenalty penalty)
        {
            if (BattleManager.Instance == null) return;

            // 실제 구현에서는 BattleManager의 턴 스킵 메서드 호출
            // 예: BattleManager.Instance.SkipPlayerTurn(penalty.turnDuration);

            if (debugMode)
                Debug.Log($"[ComboManager] 턴 스킵 페널티: {penalty.turnDuration}턴");
        }

        /// <summary>
        /// 조합 차단 페널티 적용
        /// </summary>
        private void ApplyComboBlockPenalty(FallbackPenalty penalty)
        {
            // 실제 구현에서는 조합 차단 상태 설정
            // 예: ComboBlockedTurns = penalty.turnDuration;

            if (debugMode)
                Debug.Log($"[ComboManager] 조합 차단 페널티: {penalty.turnDuration}턴");
        }

        /// <summary>
        /// 디버프 적용 페널티
        /// </summary>
        private void ApplyDebuffPenalty(FallbackPenalty penalty, int value)
        {
            // 실제 구현에서는 디버프 시스템과 연동
            // 예: DebuffManager.Instance.ApplyDebuff(penalty.debuffType, value, penalty.turnDuration);

            if (debugMode)
                Debug.Log($"[ComboManager] 디버프 페널티: {penalty.description} (값: {value}, 지속: {penalty.turnDuration}턴)");
        }

        /// <summary>
        /// 실패 페널티 시스템 초기화
        /// </summary>
        private void InitializeFailureSystem()
        {
            if (fallbackPenalties == null)
            {
                fallbackPenalties = new ComboFallbackSO[0];
            }

            // 모든 페널티 유효성 검증
            foreach (var fallback in fallbackPenalties)
            {
                if (fallback != null && !fallback.ValidatePenalties(out string error))
                {
                    Debug.LogWarning($"[ComboManager] 페널티 '{fallback.FallbackName}' 설정 오류: {error}");
                }
            }

            if (debugMode)
                Debug.Log($"[ComboManager] 실패 처리 시스템 초기화 완료: {fallbackPenalties.Length}개 페널티 설정");
        }

        /// <summary>
        /// 페널티 시스템 활성화/비활성화
        /// </summary>
        public void SetFailurePenaltyEnabled(bool enabled)
        {
            enableFailurePenalties = enabled;

            if (debugMode)
                Debug.Log($"[ComboManager] 실패 페널티 시스템 {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 페널티 설정 추가
        /// </summary>
        public void AddFallbackPenalty(ComboFallbackSO fallback)
        {
            if (fallback == null) return;

            var fallbackList = new List<ComboFallbackSO>(fallbackPenalties ?? new ComboFallbackSO[0]);
            fallbackList.Add(fallback);
            fallbackPenalties = fallbackList.ToArray();

            if (debugMode)
                Debug.Log($"[ComboManager] 페널티 추가: {fallback.FallbackName}");
        }

        /// <summary>
        /// 페널티 설정 제거
        /// </summary>
        public void RemoveFallbackPenalty(ComboFallbackSO fallback)
        {
            if (fallback == null || fallbackPenalties == null) return;

            var fallbackList = new List<ComboFallbackSO>(fallbackPenalties);
            fallbackList.Remove(fallback);
            fallbackPenalties = fallbackList.ToArray();

            if (debugMode)
                Debug.Log($"[ComboManager] 페널티 제거: {fallback.FallbackName}");
        }
        #endregion

        #region Cache Management
        /// <summary>
        /// 캐시 재로드
        /// </summary>
        public void ReloadCache()
        {
            if (debugMode)
                Debug.Log("[ComboManager] 캐시 재로드 시작");

            isCacheLoaded = false;
            LoadCombinationCache();
        }

        /// <summary>
        /// 캐시 클리어
        /// </summary>
        public void ClearCache()
        {
            combinationCache?.Clear();
            allCards?.Clear();
            combinableCards?.Clear();
            ResetStatistics();
            isCacheLoaded = false;

            if (debugMode)
                Debug.Log("[ComboManager] 캐시 클리어됨");
        }

        /// <summary>
        /// 통계 초기화
        /// </summary>
        private void ResetStatistics()
        {
            cacheHits = 0;
            cacheMisses = 0;
            totalCombinationAttempts = 0;
        }
        #endregion

        #region Debug & Statistics
        /// <summary>
        /// 캐시 통계 로그 출력
        /// </summary>
        public void LogCacheStatistics()
        {
            Debug.Log($"=== ComboManager 캐시 통계 ===");
            Debug.Log($"전체 카드 수: {TotalCardCount}");
            Debug.Log($"조합 가능한 카드 수: {CombinableCardCount}");
            Debug.Log($"캐시된 조합 수: {CachedCombinationCount}");
            Debug.Log($"총 조합 시도 횟수: {totalCombinationAttempts}");
            Debug.Log($"캐시 히트 수: {cacheHits}");
            Debug.Log($"캐시 미스 수: {cacheMisses}");
            Debug.Log($"캐시 히트율: {CacheHitRate:P2}");
        }

        /// <summary>
        /// 모든 캐시된 조합 출력 (디버그용)
        /// </summary>
        [ContextMenu("Debug All Combinations")]
        public void DebugAllCombinations()
        {
            if (!isCacheLoaded)
            {
                Debug.Log("캐시가 로드되지 않았습니다.");
                return;
            }

            Debug.Log($"=== 캐시된 조합 목록 ({CachedCombinationCount}개) ===");
            foreach (var kvp in combinationCache)
            {
                Debug.Log($"{kvp.Key.GetDebugInfo()} -> {kvp.Value.CardName}");
            }
        }

        /// <summary>
        /// 캐시 상태 정보 반환
        /// </summary>
        public string GetCacheStatus()
        {
            return $"ComboManager[Loaded: {isCacheLoaded}, Combinations: {CachedCombinationCount}, " +
                   $"Cards: {TotalCardCount}, HitRate: {CacheHitRate:P2}]";
        }
        #endregion
    }
}