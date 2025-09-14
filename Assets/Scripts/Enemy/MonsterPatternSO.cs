using UnityEngine;
using System;

namespace Maglin.Enemy
{
    /// <summary>
    /// 몬스터 패턴 타입
    /// </summary>
    public enum MonsterPatternType
    {
        OnDeath_SpawnMonster,           // 사망시 특정 위치에 새로운 몬스터 소환
        Periodic_SpawnMonster,          // N턴마다 몬스터 바로 앞에 새로운 몬스터 소환
        Periodic_ChargeAttack,          // N턴마다 M턴동안 차지 이후 공격 (차징 중 모든 행동 중단)
        Periodic_DestroyAndAttack,      // N턴마다 특정 몬스터 파괴 후 특정 대상에게 공격
        Periodic_AttackAndMove          // N턴마다 공격 후 뒤로 이동
    }

    /// <summary>
    /// 공격/피해 대상 타입
    /// </summary>
    public enum AttackTargetType
    {
        Player,                         // 플레이어만
        AllMonsters,                    // 모든 몬스터만
        PlayerAndAllMonsters,           // 플레이어 포함 모든 몬스터
        Self                            // 자기자신 (패턴 실행하는 몬스터)
    }

    /// <summary>
    /// 몬스터 소환 위치 타입
    /// </summary>
    public enum SpawnLocationType
    {
        InFrontOfSelf,                  // 자신 바로 앞
        SpecificPosition,               // 특정 위치 (gridPosition 사용)
        RightmostPosition,              // 우측 끝 칸
        RandomEmpty,                    // 빈 공간 중 랜덤
        AtDeathPosition                 // 사망한 몬스터의 위치 (사망 시 소환 전용)
    }

    /// <summary>
    /// 이동 방향 타입
    /// </summary>
    public enum MoveDirectionType
    {
        BackwardN,                      // 뒤로 N칸 이동
        BackToEnd                       // 맨 뒤칸으로 이동
    }

    [CreateAssetMenu(fileName = "New Monster Pattern", menuName = "Maglin/Enemy/MonsterPatternSO")]
    public class MonsterPatternSO : ScriptableObject
    {
        [Header("패턴 기본 정보")]
        [SerializeField] private string patternName;
        [SerializeField] private MonsterPatternType patternType;
        [SerializeField] private int priority = 1;              // 우선순위 (높을수록 우선 실행)
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("트리거 조건")]
        [SerializeField] private int triggerInterval = 3;       // N턴마다 (0이면 조건부 트리거)
        [SerializeField] private int turnOffset = 1;            // 첫 트리거까지의 턴 오프셋
        [SerializeField] private bool blockNormalAttack = true; // 패턴 실행 시 기본 공격 차단 여부

        [Header("차지 공격 설정 (ChargeAttack 전용)")]
        [SerializeField] private int chargeDuration = 2;        // M턴 동안 차지
        [SerializeField] private int chargeDamage = 10;         // 차지 공격 데미지
        [SerializeField] private AttackTargetType chargeTarget = AttackTargetType.Player;
        [SerializeField] private Color chargeEffectColor = Color.red; // 차지 중 색상 효과

        [Header("몬스터 소환 설정 (SpawnMonster 전용)")]
        [SerializeField] private EnemySO spawnedMonsterData;    // 소환할 몬스터 데이터
        [SerializeField] private SpawnLocationType spawnLocation = SpawnLocationType.InFrontOfSelf;
        [SerializeField] private Vector2Int specificSpawnPosition = Vector2Int.zero; // SpecificPosition용
        [SerializeField] private bool skipSpawnAnimation = false; // 소환 애니메이션 스킵 여부 (사망 소환에만 적용)

        [Header("파괴 및 공격 설정 (DestroyAndAttack 전용)")]
        [SerializeField] private EnemySO targetDestroyType;     // 파괴할 몬스터 타입 (null이면 모든 중립 몬스터)
        [SerializeField] private int destructionDamage = 5;     // 파괴 시 주는 데미지
        [SerializeField] private AttackTargetType destructionTarget = AttackTargetType.PlayerAndAllMonsters;

        [Header("공격 후 이동 설정 (AttackAndMove 전용)")]
        [SerializeField] private int attackAndMoveDamage = 10;          // 공격 데미지
        [SerializeField] private AttackPatternType attackAndMoveType = AttackPatternType.Melee;  // 공격 타입 (근접/원거리)
        [SerializeField] private int attackAndMoveRange = 1;            // 공격 범위
        [SerializeField] private AttackTargetType attackAndMoveTarget = AttackTargetType.Player; // 공격 대상
        [SerializeField] private MoveDirectionType moveDirection = MoveDirectionType.BackwardN;  // 이동 방향
        [SerializeField] private int moveDistance = 1;                  // 이동 거리 (BackwardN 타입일 때)

        [Header("패턴 전용 애니메이션")]
        [SerializeField] private Sprite[] patternSprites;       // 패턴 실행 시 사용할 스프라이트
        [SerializeField] private float patternFrameRate = 8f;   // 패턴 애니메이션 프레임 레이트 (FPS)

        // Properties
        public string PatternName => patternName;
        public MonsterPatternType PatternType => patternType;
        public int Priority => priority;
        public string Description => description;
        public int TriggerInterval => triggerInterval;
        public int TurnOffset => turnOffset;
        public bool BlockNormalAttack => blockNormalAttack;
        public int ChargeDuration => chargeDuration;
        public int ChargeDamage => chargeDamage;
        public AttackTargetType ChargeTarget => chargeTarget;
        public Color ChargeEffectColor => chargeEffectColor;
        public EnemySO SpawnedMonsterData => spawnedMonsterData;
        public SpawnLocationType SpawnLocation => spawnLocation;
        public Vector2Int SpecificSpawnPosition => specificSpawnPosition;
        public bool SkipSpawnAnimation => skipSpawnAnimation;
        public EnemySO TargetDestroyType => targetDestroyType;
        public int DestructionDamage => destructionDamage;
        public AttackTargetType DestructionTarget => destructionTarget;
        public int AttackAndMoveDamage => attackAndMoveDamage;
        public AttackPatternType AttackAndMoveType => attackAndMoveType;
        public int AttackAndMoveRange => attackAndMoveRange;
        public AttackTargetType AttackAndMoveTarget => attackAndMoveTarget;
        public MoveDirectionType MoveDirection => moveDirection;
        public int MoveDistance => moveDistance;
        public Sprite[] PatternSprites => patternSprites;
        public float PatternFrameRate => patternFrameRate;
        
        /// <summary>
        /// 이전 버전 호환성을 위한 AnimationSpeed (Deprecated)
        /// </summary>
        [System.Obsolete("PatternAnimationSpeed는 더 이상 사용되지 않습니다. PatternFrameRate를 사용하세요.")]
        public float PatternAnimationSpeed => 1f / patternFrameRate;

        /// <summary>
        /// 패턴이 주기적 트리거인지 확인
        /// </summary>
        public bool IsPeriodicPattern => triggerInterval > 0;

        /// <summary>
        /// 특정 턴에 이 패턴이 트리거되는지 확인
        /// </summary>
        public bool ShouldTriggerOnTurn(int currentTurn)
        {
            if (!IsPeriodicPattern) return false;

            // 첫 트리거 턴 계산: turnOffset 이후 첫 번째 interval 턴
            int firstTriggerTurn = turnOffset + triggerInterval;

            if (currentTurn < firstTriggerTurn) return false;

            // 간격에 맞는지 확인
            return (currentTurn - firstTriggerTurn) % triggerInterval == 0;
        }

        /// <summary>
        /// 사망 시 트리거 패턴인지 확인
        /// </summary>
        public bool IsDeathTriggerPattern => patternType == MonsterPatternType.OnDeath_SpawnMonster;

        /// <summary>
        /// 차지 패턴인지 확인
        /// </summary>
        public bool IsChargePattern => patternType == MonsterPatternType.Periodic_ChargeAttack;

        /// <summary>
        /// 소환 패턴인지 확인
        /// </summary>
        public bool IsSpawnPattern =>
            patternType == MonsterPatternType.OnDeath_SpawnMonster ||
            patternType == MonsterPatternType.Periodic_SpawnMonster;

        /// <summary>
        /// 파괴 및 공격 패턴인지 확인
        /// </summary>
        public bool IsDestroyAndAttackPattern => patternType == MonsterPatternType.Periodic_DestroyAndAttack;

        /// <summary>
        /// 공격 후 이동 패턴인지 확인
        /// </summary>
        public bool IsAttackAndMovePattern => patternType == MonsterPatternType.Periodic_AttackAndMove;
    }
}
