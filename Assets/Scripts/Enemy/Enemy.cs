using UnityEngine;
using System;
using Maglin.Cards;
using Maglin.Battle;

namespace Maglin.Enemy
{
    /// <summary>
    /// 몬스터 상태
    /// </summary>
    public enum EnemyState
    {
        Idle,           // 대기
        Moving,         // 이동 중
        Attacking,      // 공격 중
        Stunned,        // 기절
        Dead            // 사망
    }

    /// <summary>
    /// 몬스터 런타임 인스턴스
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        #region Events
        /// <summary>
        /// 체력 변경 이벤트 (현재 체력, 최대 체력)
        /// </summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>
        /// 위치 변경 이벤트 (이전 위치, 새 위치)
        /// </summary>
        public event Action<Vector2Int, Vector2Int> OnPositionChanged;

        /// <summary>
        /// 상태 변경 이벤트 (이전 상태, 새 상태)
        /// </summary>
        public event Action<EnemyState, EnemyState> OnStateChanged;

        /// <summary>
        /// 사망 이벤트
        /// </summary>
        public event Action<Enemy> OnDeath;

        /// <summary>
        /// 데미지 받음 이벤트 (데미지량, 속성)
        /// </summary>
        public event Action<int, ElementType> OnDamageTaken;
        #endregion

        #region Fields
        [Header("몬스터 데이터")]
        [SerializeField] private EnemySO enemyData;

        [Header("현재 상태")]
        [SerializeField] private int currentHealth;
        [SerializeField] private EnemyState currentState = EnemyState.Idle;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private bool isStunned = false;
        [SerializeField] private int stunDuration = 0;

        [Header("전투 상태")]
        [SerializeField] private bool hasActedThisTurn = false;
        [SerializeField] private int turnsSinceLastAttack = 0;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 참조
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private MonsterHitEffect hitEffect;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 몬스터 데이터
        /// </summary>
        public EnemySO EnemyData => enemyData;

        /// <summary>
        /// 현재 체력
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// 최대 체력
        /// </summary>
        public int MaxHealth => enemyData?.MaxHealth ?? 0;

        /// <summary>
        /// 현재 상태
        /// </summary>
        public EnemyState CurrentState => currentState;

        /// <summary>
        /// 그리드 위치
        /// </summary>
        public Vector2Int GridPosition => gridPosition;

        /// <summary>
        /// 살아있는지 여부
        /// </summary>
        public bool IsAlive => currentState != EnemyState.Dead && currentHealth > 0;

        /// <summary>
        /// 기절 상태인지 여부
        /// </summary>
        public bool IsStunned => isStunned;

        /// <summary>
        /// 이번 턴에 행동했는지 여부
        /// </summary>
        public bool HasActedThisTurn => hasActedThisTurn;

        /// <summary>
        /// 몬스터 이름
        /// </summary>
        public string EnemyName => enemyData?.EnemyName ?? "Unknown";

        /// <summary>
        /// 몬스터 속성
        /// </summary>
        public ElementType Element => enemyData?.Element ?? ElementType.None;

        /// <summary>
        /// 공격력 (필드 효과 포함)
        /// </summary>
        public int CurrentAttackDamage
        {
            get
            {
                if (enemyData == null) return 0;

                float damage = enemyData.AttackDamage;

                // 필드 효과 적용 (추후 구현)
                // if (FieldManager.Instance != null)
                // {
                //     float fieldBonus = FieldManager.Instance.CalculateFieldBonus(Element);
                //     damage *= fieldBonus;
                // }

                return Mathf.RoundToInt(damage);
            }
        }

        /// <summary>
        /// 차지하는 칸 수
        /// </summary>
        public int SizeInTiles => enemyData?.SizeInTiles ?? 1;

        /// <summary>
        /// 중립 오브젝트인지 여부
        /// </summary>
        public bool IsNeutralObject => enemyData?.IsNeutralObject ?? false;

        /// <summary>
        /// 몬스터 이동을 막는지 여부
        /// </summary>
        public bool BlocksMonsterMovement => enemyData?.BlocksMonsterMovement ?? false;

        /// <summary>
        /// 몬스터가 이 오브젝트를 공격하는지 여부
        /// </summary>
        public bool MonstersAttackThis => enemyData?.MonstersAttackThis ?? false;
        #endregion

        #region Unity Events
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            hitEffect = GetComponent<MonsterHitEffect>();

            // MonsterHitEffect가 없으면 자동으로 추가
            if (hitEffect == null)
            {
                hitEffect = gameObject.AddComponent<MonsterHitEffect>();
            }
        }

        private void Start()
        {
            if (!isInitialized && enemyData != null)
            {
                Initialize(enemyData);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 몬스터 초기화
        /// </summary>
        public void Initialize(EnemySO data, Vector2Int startPosition = default)
        {
            if (data == null)
            {
                Debug.LogError("[Enemy] EnemySO 데이터가 null입니다.");
                return;
            }

            enemyData = data;
            currentHealth = data.MaxHealth;
            gridPosition = startPosition;
            currentState = EnemyState.Idle;
            hasActedThisTurn = false;
            turnsSinceLastAttack = 0;
            isStunned = false;
            stunDuration = 0;

            // 스프라이트 및 색상 설정
            if (spriteRenderer != null)
            {
                if (data.Sprite != null)
                {
                    spriteRenderer.sprite = data.Sprite;
                }
                spriteRenderer.color = data.Color; // EnemySO의 색상 설정 적용
            }

            isInitialized = true;

            if (debugMode)
                Debug.Log($"[Enemy] {data.EnemyName} 초기화 완료 - 위치: {startPosition}");

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(int damage, ElementType attackerElement = ElementType.None)
        {
            if (!IsAlive) return;

            // 속성 상성 적용
            float finalDamage = damage;
            if (Battle.FieldManager.Instance != null && attackerElement != ElementType.None)
            {
                float multiplier = Battle.FieldManager.Instance.CalculateElementAdvantage(attackerElement, Element);
                finalDamage = damage * multiplier;

                if (debugMode && multiplier != 1.0f)
                    Debug.Log($"[Enemy] 속성 상성 적용: {attackerElement} vs {Element} = {multiplier}x");
            }

            int actualDamage = Mathf.RoundToInt(finalDamage);
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName}이 {actualDamage} 데미지를 받음 ({currentHealth}/{MaxHealth})");

            OnDamageTaken?.Invoke(actualDamage, attackerElement);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            // 히트 효과 재생
            if (hitEffect != null && actualDamage > 0)
            {
                hitEffect.PlayHitEffect(actualDamage);
            }

            // 사망 처리
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 체력 회복
        /// </summary>
        public void Heal(int amount)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName}이 {amount} 체력 회복 ({currentHealth}/{MaxHealth})");

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// 위치 변경
        /// </summary>
        public void SetPosition(Vector2Int newPosition)
        {
            Vector2Int oldPosition = gridPosition;
            gridPosition = newPosition;

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName} 위치 변경: {oldPosition} -> {newPosition}");

            OnPositionChanged?.Invoke(oldPosition, newPosition);
        }

        /// <summary>
        /// 상태 변경
        /// </summary>
        public void SetState(EnemyState newState)
        {
            EnemyState oldState = currentState;
            currentState = newState;

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName} 상태 변경: {oldState} -> {newState}");

            OnStateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// 기절 상태 설정
        /// </summary>
        public void SetStunned(int duration)
        {
            isStunned = duration > 0;
            stunDuration = duration;

            if (isStunned)
                SetState(EnemyState.Stunned);
            else if (currentState == EnemyState.Stunned)
                SetState(EnemyState.Idle);

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName} 기절 상태: {isStunned} (지속: {stunDuration}턴)");
        }

        /// <summary>
        /// 턴 종료 처리
        /// </summary>
        public void OnTurnEnd()
        {
            hasActedThisTurn = false;
            turnsSinceLastAttack++;

            // 기절 상태 처리
            if (isStunned)
            {
                stunDuration--;
                if (stunDuration <= 0)
                {
                    SetStunned(0);
                }
            }

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName} 턴 종료 - 기절: {isStunned}({stunDuration}), 마지막 공격: {turnsSinceLastAttack}턴 전");
        }

        /// <summary>
        /// 행동 완료 표시
        /// </summary>
        public void MarkActionComplete()
        {
            hasActedThisTurn = true;
            turnsSinceLastAttack = 0;
        }

        /// <summary>
        /// 공격 가능한지 확인
        /// </summary>
        public bool CanAttack(Vector2Int targetPosition)
        {
            if (!IsAlive || isStunned || hasActedThisTurn) return false;

            int distance = Mathf.Abs(gridPosition.x - targetPosition.x) + Mathf.Abs(gridPosition.y - targetPosition.y);
            return distance <= enemyData.AttackRange;
        }

        /// <summary>
        /// 이동 가능한지 확인
        /// </summary>
        public bool CanMove()
        {
            return IsAlive && !isStunned && !hasActedThisTurn;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 사망 처리
        /// </summary>
        private void Die()
        {
            SetState(EnemyState.Dead);

            if (debugMode)
                Debug.Log($"[Enemy] {EnemyName} 사망");

            OnDeath?.Invoke(this);
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 몬스터 상태 로그 출력
        /// </summary>
        [ContextMenu("Debug Enemy Status")]
        public void DebugEnemyStatus()
        {
            Debug.Log($"=== {EnemyName} 상태 ===");
            Debug.Log($"체력: {currentHealth}/{MaxHealth}");
            Debug.Log($"위치: {gridPosition}");
            Debug.Log($"상태: {currentState}");
            Debug.Log($"기절: {isStunned} ({stunDuration}턴)");
            Debug.Log($"이번 턴 행동: {hasActedThisTurn}");
            Debug.Log($"마지막 공격: {turnsSinceLastAttack}턴 전");
        }
        #endregion
    }
}