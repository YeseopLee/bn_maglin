using UnityEngine;
using UnityEngine.Audio;
using Maglin.Cards;
using Maglin.Battle;

namespace Maglin.Cards
{
    /// <summary>
    /// 카드의 종류 (속성/액티브1/액티브2/조합 카드 구분)
    /// </summary>
    public enum CardType
    {
        Element,    // 속성 카드 (단독 사용 불가능)
        Active1,    // 액티브1 카드 (단독 또는 조합 사용 가능)
        Active2,    // 액티브2 카드 (단독 또는 조합 사용 가능)
        Combo       // 조합 카드 (조합으로만 사용 가능)
    }

    /// <summary>
    /// 속성 종류 (불, 물, 풀, 빛, 어둠)
    /// </summary>
    public enum ElementType
    {
        None,       // 속성 없음
        Fire,       // 불
        Water,      // 물
        Grass,      // 풀
        Light,      // 빛
        Dark        // 어둠
    }

    /// <summary>
    /// 카드 대상 지정 종류
    /// </summary>
    public enum TargetType
    {
        SingleEnemy,        // 적 1인 (타겟 마커 기반)
        Self,               // 본인(플레이어)
        AllEnemies,         // 적 전체 (타겟 마커 무관)
        AllIncludingSelf,   // 나 포함 적 전체 (타겟 마커 무관)
        FrontN,             // 앞의 N명 (플레이어로부터 가까운 순 N명, 타겟 마커 무관)
        BackN,              // 뒤의 N명 (플레이어로부터 먼 순 N명, 타겟 마커 무관)

        // === 추가 타입 (기존 값 뒤에만 추가: Unity 직렬화 호환) ===
        ChainFrontHits,     // 가장 앞 몬스터부터 뒤로 targetCount회 연속 타격 (남는 횟수는 다음 몬스터로)
        PullFrontmostForward, // 가장 앞 몬스터를 플레이어 쪽으로 targetCount칸 강제 전진
        PlayerFrontLine,    // 플레이어 앞 targetCount칸 라인(세로) 범위 공격 (같은 X, +Y 방향)
        TargetFrontStrip,   // 타겟 포함 플레이어쪽으로 targetCount칸 세로 스트립 공격 (타겟 마커 기반)
        TargetBackStrip     // 타겟 포함 플레이어 반대쪽으로 targetCount칸 세로 스트립 공격 (타겟 마커 기반)
    }

    /// <summary>
    /// 조합 실패 시 강제 효과 종류
    /// </summary>
    public enum ComboFailureType
    {
        UseDefault,        // 기본 페널티 적용
        ForceThisCardOnly, // 반드시 이 카드의 효과만 발생
        PreventUse         // 사용 방지
    }

    [CreateAssetMenu(fileName = "New Card", menuName = "Maglin/Cards/CardSO")]
    public class CardSO : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string cardName;
        [SerializeField] private CardType cardType;
        [SerializeField] private ElementType elementType;

        [Header("효과 정보")]
        [SerializeField] private int baseDamage;
        [SerializeField] private int baseHeal;
        [SerializeField] private int manaCost;
        [SerializeField] private TargetType targetType;
        [SerializeField] private int targetCount = 1; // FrontN, BackN일 때 사용

        [Header("UI 정보")]
        [SerializeField] private string cardDescription;
        [SerializeField] private Sprite cardImage;

        [Header("효과 및 사운드")]
        [SerializeField] private VFXEffectSO cardEffect;
        [SerializeField] private AudioClip cardSound;
        [SerializeField] private bool showVFXPerTarget = true; // 여러 타겟일 때 VFX를 각 타겟마다 보여줄지 여부

        [Header("조합 및 특수 효과")]
        [SerializeField] private ComboFailureType comboFailureType;
        [SerializeField] private int cardPrice;
        [SerializeField] private FieldEffectSO fieldEffectToApply;

        [Header("조합식 (조합 카드인 경우)")]
        [SerializeField] private CardCombinationData[] requiredCombinations;

        // Properties
        public string CardName => cardName;
        public CardType Type => cardType;
        public ElementType Element => elementType;
        public int BaseDamage => baseDamage;
        public int BaseHeal => baseHeal;
        public int ManaCost => manaCost;
        public TargetType Target => targetType;
        public int TargetCount => targetCount;
        public string Description => cardDescription;
        public Sprite Image => cardImage;
        public VFXEffectSO Effect => cardEffect;
        public AudioClip Sound => cardSound;
        public bool ShowVFXPerTarget => showVFXPerTarget;
        public ComboFailureType FailureType => comboFailureType;
        public int Price => cardPrice;
        public FieldEffectSO FieldEffect => fieldEffectToApply;
        public CardCombinationData[] RequiredCombinations => requiredCombinations;

        /// <summary>
        /// 단독 사용이 가능한 카드인지 확인
        /// </summary>
        public bool CanUseSolo => cardType == CardType.Active1 || cardType == CardType.Active2;

        /// <summary>
        /// 상점에서 판매 가능한 카드인지 확인 (조합 카드는 판매 불가)
        /// </summary>
        public bool CanBeSold => cardType != CardType.Combo;
    }

    /// <summary>
    /// 카드 조합 데이터 구조
    /// </summary>
    [System.Serializable]
    public class CardCombinationData
    {
        [Header("조합 요구사항")]
        public CardSO elementCard;      // 필요한 속성 카드
        public CardSO active1Card;     // 필요한 액티브1 카드
        public CardSO active2Card;     // 필요한 액티브2 카드
        public FieldEffectSO requiredField; // 필요한 필드 효과

        [Header("조합 설명")]
        public string combinationDescription;
    }
}