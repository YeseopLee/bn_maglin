using UnityEngine;
using Maglin.Cards;

namespace Maglin.Enemy
{
    /// <summary>
    /// 몬스터 타입
    /// </summary>
    public enum EnemyType
    {
        Normal,     // 일반 몬스터
        Elite,      // 엘리트 몬스터
        Boss        // 보스 몬스터
    }

    /// <summary>
    /// 공격 패턴 타입
    /// </summary>
    public enum AttackPatternType
    {
        Melee,              // 근접 공격 (바로 옆에서만)
        Ranged,             // 원거리 공격 (사정거리 내)
        Special             // 특수 공격 (보스 기믹 등)
    }

    /// <summary>
    /// 이동 패턴 타입
    /// </summary>
    public enum MovementPatternType
    {
        Forward,            // 앞으로 1칸씩 이동
        ForwardTwo,         // 앞으로 2칸씩 이동
        ForwardThree,         // 앞으로 3칸씩 이동
        Jump,               // 플레이어 바로 옆칸으로 점프, 단 해당 위치에 몬스터가 있으면 겹치지않게 가능한 위치까지만 점프
        Stay                // 이동하지 않음
    }

    [CreateAssetMenu(fileName = "New Enemy", menuName = "Maglin/Enemy/EnemySO")]
    public class EnemySO : ScriptableObject
    {
        [Header("몬스터 기본 정보")]
        [SerializeField] private string enemyName;
        [SerializeField] private EnemyType enemyType;
        [SerializeField] private Sprite enemySprite;
        [SerializeField] private Color enemyColor = Color.white;  // 스프라이트 색상 (기본값: 흰색 = 원본 색상)
        [SerializeField] private int maxHealth;
        [SerializeField] private ElementType enemyElement;

        [Header("공격 정보")]
        [SerializeField] private int attackDamage;
        [SerializeField] private AttackPatternType attackPattern;
        [SerializeField] private int attackRange = 1;     // 공격 사정거리

        [Header("이동 정보")]
        [SerializeField] private MovementPatternType movementPattern;
        [SerializeField] private int movementSpeed = 1;   // 이동 속도 (칸 수)

        [Header("크기 정보")]
        [SerializeField] private int sizeInTiles = 1;     // 차지하는 칸 수

        [Header("특수 능력")]
        [SerializeField] private bool canSummonObjects;   // 오브젝트 소환 가능 여부
        [SerializeField] private bool hasSpecialMechanics; // 특수 기믹 보유 여부
        [TextArea(2, 4)]
        [SerializeField] private string specialAbilityDescription;



        [Header("보상 정보")]
        [SerializeField] private int goldReward;
        [SerializeField] private int experienceReward;

        // Properties
        public string EnemyName => enemyName;
        public EnemyType Type => enemyType;
        public Sprite Sprite => enemySprite;
        public Color Color => enemyColor;
        public int MaxHealth => maxHealth;
        public ElementType Element => enemyElement;
        public int AttackDamage => attackDamage;
        public AttackPatternType AttackPattern => attackPattern;
        public int AttackRange => attackRange;
        public MovementPatternType MovementPattern => movementPattern;
        public int MovementSpeed => movementSpeed;
        public int SizeInTiles => sizeInTiles;
        public bool CanSummonObjects => canSummonObjects;
        public bool HasSpecialMechanics => hasSpecialMechanics;
        public string SpecialAbilityDescription => specialAbilityDescription;
        public int GoldReward => goldReward;
        public int ExperienceReward => experienceReward;

        /// <summary>
        /// 보스 몬스터인지 확인
        /// </summary>
        public bool IsBoss => enemyType == EnemyType.Boss;

        /// <summary>
        /// 근접 공격 몬스터인지 확인
        /// </summary>
        public bool IsMeleeAttacker => attackPattern == AttackPatternType.Melee;
    }
}