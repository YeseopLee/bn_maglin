using UnityEngine;
using System.Collections.Generic;
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

    /// <summary>
    /// 몬스터 애니메이션 상태
    /// </summary>
    public enum MonsterAnimationState
    {
        Idle,       // 기본 상태
        Move,       // 이동 상태
        Attack,     // 공격 상태
        Hit,        // 피격 상태
        Death       // 사망 상태
    }

    [CreateAssetMenu(fileName = "New Enemy", menuName = "Maglin/Enemy/EnemySO")]
    public class EnemySO : ScriptableObject
    {
        [Header("몬스터 기본 정보")]
        [SerializeField] private string enemyName;
        [SerializeField] private EnemyType enemyType;
        [SerializeField] private Color enemyColor = Color.white;  // 스프라이트 색상 (기본값: 흰색 = 원본 색상)
        [SerializeField] private int maxHealth;
        [SerializeField] private ElementType enemyElement;

        [Header("애니메이션 스프라이트")]
        [SerializeField] private Sprite[] idleSprites;        // 기본 상태 스프라이트
        [SerializeField] private Sprite[] moveSprites;        // 이동 애니메이션 스프라이트
        [SerializeField] private Sprite[] attackSprites;      // 공격 애니메이션 스프라이트
        [SerializeField] private Sprite[] hitSprites;         // 피격 애니메이션 스프라이트
        [SerializeField] private Sprite[] deathSprites;       // 사망 애니메이션 스프라이트

        [Header("애니메이션 설정")]
        [SerializeField] private float idleFrameRate = 8f;     // 기본 상태 애니메이션 프레임 레이트
        [SerializeField] private float moveFrameRate = 10f;    // 이동 애니메이션 프레임 레이트
        [SerializeField] private float attackFrameRate = 12f;  // 공격 애니메이션 프레임 레이트
        [SerializeField] private float hitFrameRate = 15f;     // 피격 애니메이션 프레임 레이트
        [SerializeField] private float deathFrameRate = 8f;    // 사망 애니메이션 프레임 레이트
        [SerializeField] private bool flipSpritesHorizontally = false; // 모든 스프라이트 좌우 반전

        [Header("공격 정보")]
        [SerializeField] private int attackDamage;
        [SerializeField] private AttackPatternType attackPattern;
        [SerializeField] private int attackRange = 1;     // 공격 사정거리

        [Header("이동 정보")]
        [SerializeField] private MovementPatternType movementPattern;
        [SerializeField] private float moveSpeed = 1.0f;   // 이동 속도 배율 (1.0 = 기본 속도, 2.0 = 2배 빠름, 0.5 = 절반 속도)
        [SerializeField] private int movementRestTurns = 0;  // 이동 후 쉬는 턴 수 (0 = 매턴 이동 가능)

        [Header("크기 정보")]
        [SerializeField] private int sizeInTiles = 1;     // 차지하는 칸 수

        [Header("Collider 설정")]
        [SerializeField] private Vector3 colliderCenter = Vector3.zero;    // BoxCollider Center 설정 (기본값: 0,0,0)
        [SerializeField] private Vector3 colliderSize = Vector3.one;       // BoxCollider Size 설정 (기본값: 1,1,1)

        [Header("특수 능력")]
        [SerializeField] private bool canSummonObjects;   // 오브젝트 소환 가능 여부
        [SerializeField] private bool hasSpecialMechanics; // 특수 기믹 보유 여부
        [TextArea(2, 4)]
        [SerializeField] private string specialAbilityDescription;

        [Header("중립 오브젝트 설정")]
        [SerializeField] private bool isNeutralObject = false;  // 중립 오브젝트 여부
        [SerializeField] private bool blocksMonsterMovement = false;  // 몬스터 이동을 막는지 여부
        [SerializeField] private bool monstersAttackThis = false;  // 몬스터가 이 오브젝트를 공격하는지 여부
        [TextArea(2, 3)]
        [SerializeField] private string objectDescription = "";  // 오브젝트 설명



        [Header("몬스터 패턴")]
        [SerializeField] private List<MonsterPatternSO> monsterPatterns = new List<MonsterPatternSO>();

        [Header("보상 정보")]
        [SerializeField] private int goldReward;
        [SerializeField] private int experienceReward;

        // Properties
        public string EnemyName => enemyName;
        public EnemyType Type => enemyType;
        public Color Color => enemyColor;
        public int MaxHealth => maxHealth;
        public ElementType Element => enemyElement;

        // 애니메이션 스프라이트 Properties
        public Sprite[] IdleSprites => idleSprites;
        public Sprite[] MoveSprites => moveSprites;
        public Sprite[] AttackSprites => attackSprites;
        public Sprite[] HitSprites => hitSprites;
        public Sprite[] DeathSprites => deathSprites;

        // 애니메이션 프레임 레이트 Properties
        public float IdleFrameRate => idleFrameRate;
        public float MoveFrameRate => moveFrameRate;
        public float AttackFrameRate => attackFrameRate;
        public float HitFrameRate => hitFrameRate;
        public float DeathFrameRate => deathFrameRate;
        public bool FlipSpritesHorizontally => flipSpritesHorizontally;

        // 이전 버전 호환성을 위한 AnimationSpeed (Deprecated)
        [System.Obsolete("AnimationSpeed는 더 이상 사용되지 않습니다. 각 상태별 FrameRate를 사용하세요.")]
        public float AnimationSpeed => 1f / idleFrameRate; // 기본적으로 Idle 프레임 레이트 기준

        // 호환성을 위한 기본 스프라이트 (Idle의 첫 번째 프레임)
        public Sprite Sprite => (idleSprites != null && idleSprites.Length > 0) ? idleSprites[0] : null;
        public int AttackDamage => attackDamage;
        public AttackPatternType AttackPattern => attackPattern;
        public int AttackRange => attackRange;
        public MovementPatternType MovementPattern => movementPattern;
        public float MoveSpeed => moveSpeed;
        public int MovementRestTurns => movementRestTurns;
        public int SizeInTiles => sizeInTiles;
        public bool CanSummonObjects => canSummonObjects;
        public bool HasSpecialMechanics => hasSpecialMechanics;
        public string SpecialAbilityDescription => specialAbilityDescription;
        public int GoldReward => goldReward;
        public int ExperienceReward => experienceReward;
        public List<MonsterPatternSO> MonsterPatterns => monsterPatterns;

        // 중립 오브젝트 관련 Properties
        public bool IsNeutralObject => isNeutralObject;
        public bool BlocksMonsterMovement => blocksMonsterMovement;
        public bool MonstersAttackThis => monstersAttackThis;
        public string ObjectDescription => objectDescription;

        // Collider 관련 Properties
        public Vector3 ColliderCenter => colliderCenter;
        public Vector3 ColliderSize => colliderSize;

        /// <summary>
        /// 보스 몬스터인지 확인
        /// </summary>
        public bool IsBoss => enemyType == EnemyType.Boss;

        /// <summary>
        /// 근접 공격 몬스터인지 확인
        /// </summary>
        public bool IsMeleeAttacker => attackPattern == AttackPatternType.Melee;

        /// <summary>
        /// 특정 애니메이션 상태의 프레임 레이트 반환
        /// </summary>
        public float GetFrameRateForState(MonsterAnimationState state)
        {
            switch (state)
            {
                case MonsterAnimationState.Idle:
                    return idleFrameRate;
                case MonsterAnimationState.Move:
                    return moveFrameRate;
                case MonsterAnimationState.Attack:
                    return attackFrameRate;
                case MonsterAnimationState.Hit:
                    return hitFrameRate;
                case MonsterAnimationState.Death:
                    return deathFrameRate;
                default:
                    return idleFrameRate;
            }
        }

        /// <summary>
        /// 특정 애니메이션 상태의 스프라이트 배열 반환
        /// </summary>
        public Sprite[] GetSpritesForState(MonsterAnimationState state)
        {
            switch (state)
            {
                case MonsterAnimationState.Idle:
                    return idleSprites;
                case MonsterAnimationState.Move:
                    return moveSprites;
                case MonsterAnimationState.Attack:
                    return attackSprites;
                case MonsterAnimationState.Hit:
                    return hitSprites;
                case MonsterAnimationState.Death:
                    return deathSprites;
                default:
                    return idleSprites;
            }
        }

        /// <summary>
        /// 특정 애니메이션 상태가 루프 애니메이션인지 확인
        /// </summary>
        public bool ShouldLoopAnimation(MonsterAnimationState state)
        {
            switch (state)
            {
                case MonsterAnimationState.Idle:
                    return true;  // 기본 상태는 루프
                case MonsterAnimationState.Move:
                    return true;  // 이동 상태는 루프
                case MonsterAnimationState.Attack:
                    return false; // 공격은 한 번만
                case MonsterAnimationState.Hit:
                    return false; // 피격은 한 번만
                case MonsterAnimationState.Death:
                    return false; // 사망은 한 번만 (마지막 프레임에서 정지)
                default:
                    return true;
            }
        }
    }
}