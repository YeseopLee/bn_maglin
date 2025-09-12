using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Maglin.Relics;
using Maglin.Core;
using Maglin.Battle;

namespace Maglin.Player
{
    /// <summary>
    /// 플레이어 상태를 관리하는 매니저
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static PlayerManager _instance;

        public static PlayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerManager>();

                    if (_instance == null)
                    {
                        GameObject playerManagerObject = new GameObject("PlayerManager");
                        _instance = playerManagerObject.AddComponent<PlayerManager>();
                        DontDestroyOnLoad(playerManagerObject);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events
        /// <summary>
        /// 체력 변경 이벤트 (현재 체력, 최대 체력)
        /// </summary>
        public static event Action<int, int> OnHealthChanged;

        /// <summary>
        /// 마나 변경 이벤트 (현재 마나, 최대 마나)
        /// </summary>
        public static event Action<int, int> OnManaChanged;

        /// <summary>
        /// 골드 변경 이벤트 (현재 골드)
        /// </summary>
        public static event Action<int> OnGoldChanged;

        /// <summary>
        /// 유물 추가 이벤트
        /// </summary>
        public static event Action<RelicSO> OnRelicAdded;

        /// <summary>
        /// 유물 제거 이벤트
        /// </summary>
        public static event Action<RelicSO> OnRelicRemoved;

        /// <summary>
        /// 플레이어 사망 이벤트
        /// </summary>
        public static event Action OnPlayerDeath;

        /// <summary>
        /// 플레이어 애니메이션 상태 변경 이벤트
        /// </summary>
        public static event Action<PlayerAnimationState> OnPlayerAnimationChanged;
        #endregion

        #region Fields
        [Header("기본 스탯")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int maxMana = 30;
        [SerializeField] private int manaRecoveryPerTurn = 10;
        [SerializeField] private int maxHandSize = 5;

        [Header("현재 상태")]
        [SerializeField] private int currentHealth;
        [SerializeField] private int currentMana;
        [SerializeField] private int currentGold;

        [Header("유물")]
        [SerializeField] private List<RelicSO> currentRelics = new List<RelicSO>();

        [Header("플레이어 스프라이트 애니메이션")]
        [SerializeField] private Sprite[] idleSprites;
        [SerializeField] private Sprite[] walkSprites; // 이동 스프라이트 추가
        [SerializeField] private Sprite[] attackSprites1;
        [SerializeField] private Sprite[] attackSprites2;
        [SerializeField] private Sprite[] attackSprites3;
        [SerializeField] private Sprite[] hitSprites;
        [SerializeField] private Sprite[] deathSprites;

        [Header("애니메이션 설정")]
        [SerializeField] private float idleFrameRate = 8f;
        [SerializeField] private float walkFrameRate = 10f; // 이동 애니메이션 프레임 레이트 추가
        [SerializeField] private float attackFrameRate = 12f;
        [SerializeField] private float hitFrameRate = 15f;
        [SerializeField] private float deathFrameRate = 10f;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true; // 유물 디버그를 위해 활성화

        [Header("사망 연출 설정")]
        [SerializeField] private float deathSlowMotionScale = 0.3f; // 사망 시 슬로우 모션 배율
        [SerializeField] private float deathSlowMotionDuration = 6f; // 슬로우 모션 지속 시간

        // 계산된 스탯 (유물 효과 적용)
        private int calculatedMaxHealth;
        private int calculatedMaxMana;
        private int calculatedManaRecovery;
        private int calculatedMaxHandSize;

        // 초기화 관련
        private bool isInitialized = false;

        // 애니메이션 상태
        private PlayerAnimationState currentAnimationState = PlayerAnimationState.Idle;

        // 카드 사용 횟수 추적 (전투별)
        private int cardUseCountThisBattle = 0;
        private int currentFrameIndex = 0;
        private Coroutine animationCoroutine = null;
        private bool isPlayingAnimation = false;

        /// <summary>
        /// 플레이어 애니메이션 상태
        /// </summary>
        public enum PlayerAnimationState
        {
            Idle,
            Walk,  // 이동 애니메이션 상태 추가
            Attack1,
            Attack2,
            Attack3,
            Hit,
            Death
        }
        #endregion

        #region Properties
        /// <summary>
        /// 현재 체력
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// 최대 체력 (유물 효과 적용)
        /// </summary>
        public int MaxHealth
        {
            get
            {
                // calculatedMaxHealth가 0이면 강제로 재계산
                if (calculatedMaxHealth <= 0)
                {
                    if (debugMode)
                        Debug.LogWarning("[PlayerManager] calculatedMaxHealth가 0입니다. 재계산 중...");
                    RecalculateStats();
                }
                return calculatedMaxHealth;
            }
        }

        /// <summary>
        /// 현재 마나
        /// </summary>
        public int CurrentMana => currentMana;

        /// <summary>
        /// 최대 마나 (유물 효과 적용)
        /// </summary>
        public int MaxMana => calculatedMaxMana;

        /// <summary>
        /// 턴당 마나 회복량 (유물 효과 적용)
        /// </summary>
        public int ManaRecoveryPerTurn => calculatedManaRecovery;

        /// <summary>
        /// 최대 손패 크기 (유물 효과 적용)
        /// </summary>
        public int MaxHandSize => calculatedMaxHandSize;

        /// <summary>
        /// 현재 골드
        /// </summary>
        public int CurrentGold => currentGold;

        /// <summary>
        /// 보유 유물 목록
        /// </summary>
        public IReadOnlyList<RelicSO> CurrentRelics => currentRelics.AsReadOnly();

        /// <summary>
        /// 플레이어가 살아있는지 여부
        /// </summary>
        public bool IsAlive => currentHealth > 0;

        /// <summary>
        /// 체력 비율 (0.0 ~ 1.0)
        /// </summary>
        public float HealthRatio => calculatedMaxHealth > 0 ? (float)currentHealth / calculatedMaxHealth : 0f;

        /// <summary>
        /// 마나 비율 (0.0 ~ 1.0)
        /// </summary>
        public float ManaRatio => calculatedMaxMana > 0 ? (float)currentMana / calculatedMaxMana : 0f;

        /// <summary>
        /// 현재 애니메이션 상태
        /// </summary>
        public PlayerAnimationState CurrentAnimationState => currentAnimationState;

        /// <summary>
        /// 기본 최대 체력 (유물 효과 제외)
        /// </summary>
        public int BaseMaxHealth => maxHealth;

        /// <summary>
        /// 기본 최대 마나 (유물 효과 제외)
        /// </summary>
        public int BaseMaxMana => maxMana;

        /// <summary>
        /// 기본 마나 회복량 (유물 효과 제외)
        /// </summary>
        public int BaseManaRecoveryPerTurn => manaRecoveryPerTurn;

        /// <summary>
        /// 기본 최대 손패 수 (유물 효과 제외)
        /// </summary>
        public int BaseMaxHandSize => maxHandSize;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                // 계산된 스탯을 기본값으로 먼저 초기화
                calculatedMaxHealth = maxHealth;
                calculatedMaxMana = maxMana;
                calculatedManaRecovery = manaRecoveryPerTurn;
                calculatedMaxHandSize = maxHandSize;

                InitializePlayer();
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[PlayerManager] 중복된 PlayerManager 감지됨. 삭제합니다.");
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                // 애니메이션 정리
                StopCurrentAnimation();
                _instance = null;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 플레이어 초기화
        /// </summary>
        private void InitializePlayer()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 초기화 시작");

            // 기본 스프라이트 로드/생성
            LoadDefaultSprites();

            // 세이브 파일 존재 여부 확인
            bool hasSaveFile = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile;

            if (hasSaveFile)
            {
                if (debugMode)
                    Debug.Log("[PlayerManager] 세이브 파일 발견 - 기본값 초기화 건너뛰기");

                // 최소한의 초기화만 수행 (애니메이션 등)
                InitializeMinimal();
            }
            else
            {
                if (debugMode)
                    Debug.Log("[PlayerManager] 세이브 파일 없음 - 기본값으로 초기화");

                // 기본 스탯으로 초기화
                ResetToDefault();
            }

            // FloorManager 이벤트 구독
            FloorManager.OnGameInitialized += OnGameInitialized;

            isInitialized = true;

            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 초기화 완료");
        }

        /// <summary>
        /// 기본 스프라이트 로드/생성
        /// </summary>
        private void LoadDefaultSprites()
        {
            // Resources에서 플레이어 스프라이트 배열 로드 시도
            if (idleSprites == null || idleSprites.Length == 0)
            {
                idleSprites = Resources.LoadAll<Sprite>("Player/PlayerIdle");
                if (idleSprites == null || idleSprites.Length == 0)
                {
                    idleSprites = new Sprite[] { CreateDefaultSprite(Color.blue) };
                    if (debugMode)
                        Debug.Log("[PlayerManager] 기본 대기 스프라이트 생성");
                }
            }

            if (walkSprites == null || walkSprites.Length == 0)
            {
                walkSprites = Resources.LoadAll<Sprite>("Player/PlayerWalk");
                if (walkSprites == null || walkSprites.Length == 0)
                {
                    // Walk 스프라이트가 없으면 Idle 스프라이트 사용
                    walkSprites = idleSprites;
                    if (debugMode)
                        Debug.LogWarning("[PlayerManager] Walk 스프라이트가 Resources에서 찾을 수 없어 Idle 스프라이트 사용");
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[PlayerManager] Resources에서 Walk 스프라이트 로드: {walkSprites.Length}개");
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[PlayerManager] Inspector에서 할당된 Walk 스프라이트 사용: {walkSprites.Length}개");
            }

            if (attackSprites1 == null || attackSprites1.Length == 0)
            {
                attackSprites1 = Resources.LoadAll<Sprite>("Player/PlayerAttack1");
                if (attackSprites1 == null || attackSprites1.Length == 0)
                {
                    attackSprites1 = new Sprite[] { CreateDefaultSprite(Color.red) };
                    if (debugMode)
                        Debug.Log("[PlayerManager] 기본 공격1 스프라이트 생성");
                }
            }

            if (attackSprites2 == null || attackSprites2.Length == 0)
            {
                attackSprites2 = Resources.LoadAll<Sprite>("Player/PlayerAttack2");
                if (attackSprites2 == null || attackSprites2.Length == 0)
                {
                    attackSprites2 = new Sprite[] { CreateDefaultSprite(Color.yellow) };
                }
            }

            if (attackSprites3 == null || attackSprites3.Length == 0)
            {
                attackSprites3 = Resources.LoadAll<Sprite>("Player/PlayerAttack3");
                if (attackSprites3 == null || attackSprites3.Length == 0)
                {
                    attackSprites3 = new Sprite[] { CreateDefaultSprite(Color.green) };
                }
            }

            if (hitSprites == null || hitSprites.Length == 0)
            {
                hitSprites = Resources.LoadAll<Sprite>("Player/PlayerHit");
                if (hitSprites == null || hitSprites.Length == 0)
                {
                    hitSprites = new Sprite[] { CreateDefaultSprite(Color.magenta) };
                }
            }

            if (deathSprites == null || deathSprites.Length == 0)
            {
                deathSprites = Resources.LoadAll<Sprite>("Player/PlayerDeath");
                if (deathSprites == null || deathSprites.Length == 0)
                {
                    deathSprites = new Sprite[] { CreateDefaultSprite(Color.black) };
                    if (debugMode)
                        Debug.Log("[PlayerManager] Resources에서 Death 스프라이트를 찾을 수 없어 기본 스프라이트 생성");
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[PlayerManager] Resources에서 Death 스프라이트 로드: {deathSprites.Length}개");
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[PlayerManager] Inspector에서 할당된 Death 스프라이트 사용: {deathSprites.Length}개");
            }

            if (debugMode)
            {
                Debug.Log("[PlayerManager] 플레이어 스프라이트 배열 로드/생성 완료");
                Debug.Log($"  - Idle 스프라이트: {idleSprites?.Length ?? 0}개");
                Debug.Log($"  - Walk 스프라이트: {walkSprites?.Length ?? 0}개");
                Debug.Log($"  - Attack1 스프라이트: {attackSprites1?.Length ?? 0}개");
                Debug.Log($"  - Attack2 스프라이트: {attackSprites2?.Length ?? 0}개");
                Debug.Log($"  - Attack3 스프라이트: {attackSprites3?.Length ?? 0}개");
                Debug.Log($"  - Hit 스프라이트: {hitSprites?.Length ?? 0}개");
                Debug.Log($"  - Death 스프라이트: {deathSprites?.Length ?? 0}개");
            }
        }

        /// <summary>
        /// 기본 스프라이트 생성 (테스트용)
        /// </summary>
        private Sprite CreateDefaultSprite(Color color)
        {
            // 64x64 텍스처 생성
            Texture2D texture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // 스프라이트 생성
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 게임 초기화 완료 시 호출
        /// </summary>
        private void OnGameInitialized()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 게임 초기화 완료 신호 받음");

            // 추가 초기화 로직이 필요한 경우 여기에 구현
        }

        /// <summary>
        /// 최소한의 초기화 (세이브 로드 시)
        /// </summary>
        private void InitializeMinimal()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 최소한의 초기화 (세이브 로드 대기)");

            // 기본 계산된 스탯을 기본값으로 먼저 설정 (나중에 세이브 데이터로 덮어씀)
            calculatedMaxHealth = maxHealth;
            calculatedMaxMana = maxMana;
            calculatedManaRecovery = manaRecoveryPerTurn;
            calculatedMaxHandSize = maxHandSize;

            // 유물 리스트만 초기화 (나중에 세이브 데이터로 설정됨)
            currentRelics.Clear();

            // 애니메이션 상태만 설정 (세이브 데이터에서 덮어씀)
            SetAnimationState(PlayerAnimationState.Idle);

            if (debugMode)
                Debug.Log("[PlayerManager] 최소 초기화 완료 - 세이브 데이터 적용 대기");
        }

        /// <summary>
        /// 기본값으로 리셋 (새 게임 시작 시)
        /// </summary>
        public void ResetToDefault()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 기본값으로 리셋");

            // 기본 스탯 설정
            currentGold = 0;

            // 유물 초기화
            currentRelics.Clear();

            // 계산된 스탯 갱신 (먼저 실행)
            RecalculateStats();

            // 체력과 마나를 계산된 최대값으로 설정
            currentHealth = calculatedMaxHealth;
            currentMana = calculatedMaxMana;

            // 애니메이션 상태를 대기로 설정 (애니메이션 자동 시작)
            SetAnimationState(PlayerAnimationState.Idle);

            // 이벤트 발생
            NotifyAllStatsChanged();

            if (debugMode)
                Debug.Log($"[PlayerManager] 리셋 완료 - 체력: {currentHealth}/{calculatedMaxHealth}, 애니메이션 시작");
        }
        #endregion

        #region Health Management
        /// <summary>
        /// 체력 회복
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;

            int oldHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + amount, calculatedMaxHealth);

            if (currentHealth != oldHealth)
            {
                OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);

                if (debugMode)
                    Debug.Log($"[PlayerManager] 체력 회복: {oldHealth} -> {currentHealth} (+{amount})");
            }
        }

        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(int damage)
        {
            TakeDamage(damage, null);
        }

        /// <summary>
        /// 피해를 받고 반격 효과 처리
        /// </summary>
        public void TakeDamage(int damage, Maglin.Enemy.Enemy attacker)
        {
            if (damage <= 0) return;

            int oldHealth = currentHealth;
            currentHealth = Mathf.Max(currentHealth - damage, 0);

            OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);

            if (debugMode)
            {
                string attackerName = attacker != null ? attacker.EnemyName : "Unknown";
                Debug.Log($"[PlayerManager] 데미지: {oldHealth} -> {currentHealth} (-{damage}) 공격자: {attackerName}");
            }

            // 반격 효과 처리 (공격자가 있고 살아있을 때만)
            if (attacker != null && attacker.IsAlive)
            {
                ProcessCounterAttackEffects(attacker);
            }

            // 피격 애니메이션 실행
            if (currentHealth > 0)
            {
                PlayHitAnimation();
            }

            // 사망 체크
            if (currentHealth <= 0 && oldHealth > 0)
            {
                HandlePlayerDeath();
            }
        }

        /// <summary>
        /// 반격 효과 처리
        /// </summary>
        private void ProcessCounterAttackEffects(Maglin.Enemy.Enemy attacker)
        {
            if (currentRelics == null || currentRelics.Count == 0) return;

            foreach (var relic in currentRelics)
            {
                if (relic == null) continue;

                switch (relic.EffectType)
                {
                    case RelicEffectType.CounterAttackSingle:
                        // 공격한 몬스터에게만 반격
                        if (attacker != null && attacker.IsAlive)
                        {
                            int counterDamage = Mathf.RoundToInt(relic.EffectValue);
                            attacker.TakeDamage(counterDamage, Maglin.Cards.ElementType.None);

                            if (debugMode)
                                Debug.Log($"[PlayerManager] 단일 반격: {attacker.EnemyName}에게 {counterDamage} 피해");
                        }
                        break;

                    case RelicEffectType.CounterAttackAll:
                        // 모든 몬스터에게 반격
                        var allMonsters = FindAllAliveMonsters();
                        int aoeCounterDamage = Mathf.RoundToInt(relic.EffectValue);

                        foreach (var monster in allMonsters)
                        {
                            if (monster != null && monster.IsAlive)
                            {
                                monster.TakeDamage(aoeCounterDamage, Maglin.Cards.ElementType.None);
                            }
                        }

                        if (debugMode)
                            Debug.Log($"[PlayerManager] 전체 반격: 모든 몬스터({allMonsters.Count}마리)에게 {aoeCounterDamage} 피해");
                        break;
                }
            }
        }

        /// <summary>
        /// 살아있는 모든 몬스터 찾기
        /// </summary>
        private List<Maglin.Enemy.Enemy> FindAllAliveMonsters()
        {
            var aliveMonsters = new List<Maglin.Enemy.Enemy>();

            // MonsterSpawnManager를 통해 몬스터 찾기
            var monsterSpawnManager = FindObjectOfType<MonsterSpawnManager>();
            if (monsterSpawnManager != null)
            {
                var allMonsters = monsterSpawnManager.GetAllMonsters();
                foreach (var monster in allMonsters)
                {
                    if (monster != null && monster.IsAlive && !monster.IsNeutralObject)
                    {
                        aliveMonsters.Add(monster);
                    }
                }
            }

            return aliveMonsters;
        }

        /// <summary>
        /// 카드 사용 시 호출 (카드 효과 발동 시 카운트)
        /// </summary>
        public void OnCardUsed()
        {
            cardUseCountThisBattle++;

            if (debugMode)
                Debug.Log($"[PlayerManager] 카드 사용 카운트: {cardUseCountThisBattle}회");

            // 카드 사용 카운트 공격 효과 처리
            ProcessCardUseCountAttackEffects();
        }

        /// <summary>
        /// 카드 사용 카운트 공격 효과 처리
        /// </summary>
        private void ProcessCardUseCountAttackEffects()
        {
            if (currentRelics == null || currentRelics.Count == 0) return;

            foreach (var relic in currentRelics)
            {
                if (relic == null || relic.EffectType != RelicEffectType.CardUseCountAttack) continue;

                int attackInterval = Mathf.Max(1, Mathf.RoundToInt(relic.SecondaryValue));
                int attackDamage = Mathf.RoundToInt(relic.EffectValue);

                // N회마다 공격
                if (cardUseCountThisBattle % attackInterval == 0)
                {
                    var allMonsters = FindAllAliveMonsters();

                    foreach (var monster in allMonsters)
                    {
                        if (monster != null && monster.IsAlive)
                        {
                            monster.TakeDamage(attackDamage, Maglin.Cards.ElementType.None);
                        }
                    }

                    if (debugMode)
                        Debug.Log($"[PlayerManager] 카드 사용 카운트 공격: {attackInterval}회마다 모든 몬스터({allMonsters.Count}마리)에게 {attackDamage} 피해");
                }
            }
        }

        /// <summary>
        /// 최대 체력 설정 (유물 효과 등)
        /// </summary>
        public void SetMaxHealth(int newMaxHealth)
        {
            calculatedMaxHealth = Mathf.Max(1, newMaxHealth);

            // 현재 체력이 최대 체력을 초과하지 않도록 조정
            if (currentHealth > calculatedMaxHealth)
            {
                currentHealth = calculatedMaxHealth;
            }

            OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);

            if (debugMode)
                Debug.Log($"[PlayerManager] 최대 체력 변경: {newMaxHealth}");
        }

        /// <summary>
        /// 최대 체력 변경 (상대값, 이벤트 등에서 사용)
        /// </summary>
        public void ChangeMaxHealth(int amount)
        {
            int newMaxHealth = calculatedMaxHealth + amount;
            SetMaxHealth(newMaxHealth);

            if (debugMode)
                Debug.Log($"[PlayerManager] 최대 체력 변화: {amount} (현재: {calculatedMaxHealth})");
        }

        /// <summary>
        /// 현재 체력 직접 설정 (세이브 로드용)
        /// </summary>
        public void SetCurrentHealth(int health)
        {
            int oldHealth = currentHealth;
            currentHealth = Mathf.Clamp(health, 0, calculatedMaxHealth);

            OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);

            if (debugMode)
                Debug.Log($"[PlayerManager] 체력 직접 설정: {oldHealth} -> {currentHealth}");
        }

        /// <summary>
        /// 플레이어 사망 처리
        /// </summary>
        private void HandlePlayerDeath()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 사망");

            // 사망 애니메이션 실행
            PlayDeathAnimation();

            OnPlayerDeath?.Invoke();

            // FloorManager에게 게임 오버 알림
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.GameOver();
            }
        }
        #endregion

        #region Mana Management
        /// <summary>
        /// 마나 소모
        /// </summary>
        public bool SpendMana(int amount)
        {
            if (amount <= 0) return true;

            if (currentMana < amount)
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerManager] 마나 부족: 필요 {amount}, 보유 {currentMana}");
                return false;
            }

            int oldMana = currentMana;
            currentMana -= amount;

            OnManaChanged?.Invoke(currentMana, calculatedMaxMana);

            if (debugMode)
                Debug.Log($"[PlayerManager] 마나 소모: {oldMana} -> {currentMana} (-{amount})");

            return true;
        }

        /// <summary>
        /// 마나 회복
        /// </summary>
        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;

            int oldMana = currentMana;
            currentMana = Mathf.Min(currentMana + amount, calculatedMaxMana);

            if (currentMana != oldMana)
            {
                OnManaChanged?.Invoke(currentMana, calculatedMaxMana);

                if (debugMode)
                    Debug.Log($"[PlayerManager] 마나 회복: {oldMana} -> {currentMana} (+{amount})");
            }
        }

        /// <summary>
        /// 턴 시작 시 마나 회복
        /// </summary>
        public void RecoverManaForTurn()
        {
            RestoreMana(calculatedManaRecovery);
        }

        /// <summary>
        /// 최대 마나 설정 (유물 효과 등)
        /// </summary>
        public void SetMaxMana(int newMaxMana)
        {
            calculatedMaxMana = Mathf.Max(0, newMaxMana);

            // 현재 마나가 최대 마나를 초과하지 않도록 조정
            if (currentMana > calculatedMaxMana)
            {
                currentMana = calculatedMaxMana;
            }

            OnManaChanged?.Invoke(currentMana, calculatedMaxMana);

            if (debugMode)
                Debug.Log($"[PlayerManager] 최대 마나 변경: {newMaxMana}");
        }

        /// <summary>
        /// 현재 마나 직접 설정 (세이브 로드용)
        /// </summary>
        public void SetCurrentMana(int mana)
        {
            int oldMana = currentMana;
            currentMana = Mathf.Clamp(mana, 0, calculatedMaxMana);

            OnManaChanged?.Invoke(currentMana, calculatedMaxMana);

            if (debugMode)
                Debug.Log($"[PlayerManager] 마나 직접 설정: {oldMana} -> {currentMana}");
        }

        /// <summary>
        /// 기본 스탯 직접 설정 (세이브 로드용)
        /// </summary>
        public void SetBaseStats(int baseMaxHealth, int baseMaxMana, int baseManaRecovery, int baseMaxHandSize)
        {
            maxHealth = Mathf.Max(1, baseMaxHealth);
            maxMana = Mathf.Max(0, baseMaxMana);
            manaRecoveryPerTurn = Mathf.Max(0, baseManaRecovery);
            maxHandSize = Mathf.Max(1, baseMaxHandSize);

            // 스탯 재계산 (유물 효과 포함)
            RecalculateStats();

            if (debugMode)
            {
                Debug.Log($"[PlayerManager] 기본 스탯 설정:");
                Debug.Log($"  - 기본 체력: {maxHealth} -> 계산된 체력: {calculatedMaxHealth}");
                Debug.Log($"  - 기본 마나: {maxMana} -> 계산된 마나: {calculatedMaxMana}");
                Debug.Log($"  - 기본 마나 회복: {manaRecoveryPerTurn} -> 계산된 회복: {calculatedManaRecovery}");
                Debug.Log($"  - 기본 손패 크기: {maxHandSize} -> 계산된 손패: {calculatedMaxHandSize}");
            }
        }
        #endregion

        #region Gold Management
        /// <summary>
        /// 골드 획득
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;

            int oldGold = currentGold;
            currentGold += amount;

            OnGoldChanged?.Invoke(currentGold);

            if (debugMode)
                Debug.Log($"[PlayerManager] 골드 획득: {oldGold} -> {currentGold} (+{amount})");
        }

        /// <summary>
        /// 골드 소모
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;

            if (currentGold < amount)
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerManager] 골드 부족: 필요 {amount}, 보유 {currentGold}");
                return false;
            }

            int oldGold = currentGold;
            currentGold -= amount;

            OnGoldChanged?.Invoke(currentGold);

            if (debugMode)
                Debug.Log($"[PlayerManager] 골드 소모: {oldGold} -> {currentGold} (-{amount})");

            return true;
        }

        /// <summary>
        /// 골드 직접 설정 (세이브 로드용)
        /// </summary>
        public void SetGold(int amount)
        {
            int oldGold = currentGold;
            currentGold = Mathf.Max(0, amount);

            OnGoldChanged?.Invoke(currentGold);

            if (debugMode)
                Debug.Log($"[PlayerManager] 골드 직접 설정: {oldGold} -> {currentGold}");
        }
        #endregion

        #region Relic Management
        /// <summary>
        /// 유물 추가
        /// </summary>
        public void AddRelic(RelicSO relic)
        {
            if (relic == null)
            {
                Debug.LogError("[PlayerManager] null 유물을 추가하려고 시도했습니다.");
                return;
            }

            // 중복 체크 (스택 불가능한 유물인 경우)
            if (!relic.CanStack && HasRelic(relic))
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerManager] 이미 보유한 유물입니다: {relic.RelicName}");
                return;
            }

            currentRelics.Add(relic);
            OnRelicAdded?.Invoke(relic);

            // 스탯 재계산
            RecalculateStats();

            if (debugMode)
                Debug.Log($"[PlayerManager] 유물 추가: {relic.RelicName}");
        }

        /// <summary>
        /// 유물 제거
        /// </summary>
        public bool RemoveRelic(RelicSO relic)
        {
            if (relic == null) return false;

            bool removed = currentRelics.Remove(relic);

            if (removed)
            {
                OnRelicRemoved?.Invoke(relic);

                // 스탯 재계산
                RecalculateStats();

                if (debugMode)
                    Debug.Log($"[PlayerManager] 유물 제거: {relic.RelicName}");
            }

            return removed;
        }

        /// <summary>
        /// 특정 유물 보유 여부 확인
        /// </summary>
        public bool HasRelic(RelicSO relic)
        {
            return currentRelics.Contains(relic);
        }

        /// <summary>
        /// 특정 타입의 유물 개수 반환
        /// </summary>
        public int GetRelicCount(RelicType relicType)
        {
            int count = 0;
            foreach (var relic in currentRelics)
            {
                if (relic.Type == relicType)
                    count++;
            }
            return count;
        }
        #endregion

        #region Stat Calculation
        /// <summary>
        /// 유물 효과를 적용하여 스탯 재계산 (public 버전)
        /// </summary>
        public void RecalculateStatsPublic()
        {
            RecalculateStats();
        }

        /// <summary>
        /// 유물 효과를 적용하여 스탯 재계산
        /// </summary>
        private void RecalculateStats()
        {
            // 기본값으로 시작
            calculatedMaxHealth = maxHealth;
            calculatedMaxMana = maxMana;
            calculatedManaRecovery = manaRecoveryPerTurn;
            calculatedMaxHandSize = maxHandSize;

            // 유물 효과 적용
            foreach (var relic in currentRelics)
            {
                ApplyRelicEffect(relic);
            }

            // 최소값 보장
            calculatedMaxHealth = Mathf.Max(1, calculatedMaxHealth);
            calculatedMaxMana = Mathf.Max(0, calculatedMaxMana);
            calculatedManaRecovery = Mathf.Max(0, calculatedManaRecovery);
            calculatedMaxHandSize = Mathf.Max(1, calculatedMaxHandSize);

            if (debugMode)
                Debug.Log($"[PlayerManager] 스탯 재계산 완료 - HP: {calculatedMaxHealth}, MP: {calculatedMaxMana}, 회복: {calculatedManaRecovery}, 손패: {calculatedMaxHandSize}");
        }

        /// <summary>
        /// 개별 유물 효과 적용
        /// </summary>
        private void ApplyRelicEffect(RelicSO relic)
        {
            switch (relic.EffectType)
            {
                case RelicEffectType.MaxHealthIncrease:
                    if (relic.IsPercentage)
                        calculatedMaxHealth = Mathf.RoundToInt(calculatedMaxHealth * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxHealth += Mathf.RoundToInt(relic.EffectValue);
                    break;

                case RelicEffectType.MaxManaIncrease:
                    if (relic.IsPercentage)
                        calculatedMaxMana = Mathf.RoundToInt(calculatedMaxMana * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxMana += Mathf.RoundToInt(relic.EffectValue);
                    break;

                case RelicEffectType.MaxHandSizeIncrease:
                    if (relic.IsPercentage)
                        calculatedMaxHandSize = Mathf.RoundToInt(calculatedMaxHandSize * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxHandSize += Mathf.RoundToInt(relic.EffectValue);
                    break;

                // 다른 효과들은 GetRelicModifier 메서드를 통해 처리
                default:
                    break;
            }
        }

        /// <summary>
        /// 특정 효과 타입의 유물 수정자 반환
        /// </summary>
        public float GetRelicModifier(RelicEffectType effectType, bool asMultiplier = false)
        {
            float totalModifier = asMultiplier ? 1f : 0f;

            foreach (var relic in currentRelics)
            {
                if (relic.EffectType == effectType)
                {
                    if (asMultiplier)
                    {
                        if (relic.IsPercentage)
                            totalModifier *= (1f + relic.EffectValue / 100f);
                        else
                            totalModifier *= (1f + relic.EffectValue);
                    }
                    else
                    {
                        totalModifier += relic.EffectValue;
                    }
                }
            }

            return totalModifier;
        }

        /// <summary>
        /// 드로우 비용 감소 효과 계산
        /// </summary>
        public int GetDrawCostReduction()
        {
            return Mathf.RoundToInt(GetRelicModifier(RelicEffectType.DrawCostReduction));
        }

        /// <summary>
        /// 첫 번째 드로우 무료 여부 확인
        /// </summary>
        public bool HasFirstDrawFree()
        {
            float modifier = GetRelicModifier(RelicEffectType.FirstDrawFree);
            bool hasEffect = modifier > 0;

            if (debugMode)
            {
                Debug.Log($"[PlayerManager] FirstDrawFree 유물 확인: modifier={modifier}, hasEffect={hasEffect}");
                Debug.Log($"[PlayerManager] 현재 보유 유물 수: {currentRelics.Count}");
                foreach (var relic in currentRelics)
                {
                    Debug.Log($"[PlayerManager] 유물: {relic.RelicName}, 타입: {relic.EffectType}, 값: {relic.EffectValue}");
                }
            }

            return hasEffect;
        }

        /// <summary>
        /// 체력으로 드로우 가능 여부 및 배수 반환
        /// </summary>
        public float GetDrawWithHealthMultiplier()
        {
            return GetRelicModifier(RelicEffectType.DrawWithHealth);
        }

        /// <summary>
        /// 드로우 횟수당 공격 데미지 계산
        /// </summary>
        public int GetDrawCountAttackDamage()
        {
            return Mathf.RoundToInt(GetRelicModifier(RelicEffectType.DrawCountAttack));
        }

        /// <summary>
        /// 모든 스탯 변경 이벤트 발생
        /// </summary>
        public void NotifyAllStatsChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);
            OnManaChanged?.Invoke(currentMana, calculatedMaxMana);
            OnGoldChanged?.Invoke(currentGold);
        }

        /// <summary>
        /// 플레이어를 BattleField에 배치
        /// </summary>
        public void PlaceOnBattleField()
        {
            if (BattleField.Instance != null)
            {
                // 플레이어는 항상 0번 위치 (가장 왼쪽)
                BattleField.Instance.PlaceObjectAtPosition(gameObject, 0, PositionObjectType.Player);

                if (debugMode)
                    Debug.Log("[PlayerManager] 플레이어를 BattleField 0번 위치에 배치");
            }
        }

        /// <summary>
        /// 플레이어의 현재 BattleField 위치 반환
        /// </summary>
        public int GetBattleFieldPosition()
        {
            if (BattleField.Instance != null)
            {
                return BattleField.Instance.GetObjectPosition(gameObject);
            }
            return -1;
        }

        /// <summary>
        /// 플레이어가 공격할 수 있는 위치들 반환
        /// </summary>
        public List<int> GetAttackablePositions(int range = 1)
        {
            if (BattleField.Instance == null) return new List<int>();

            int playerPosition = GetBattleFieldPosition();
            if (playerPosition == -1) return new List<int>();

            return BattleField.Instance.GetPositionsInRange(playerPosition, range);
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== PlayerManager Debug Info ===");
            Debug.Log($"Health: {currentHealth}/{calculatedMaxHealth} (base: {maxHealth})");
            Debug.Log($"Mana: {currentMana}/{calculatedMaxMana} (base: {maxMana})");
            Debug.Log($"Gold: {currentGold}");
            Debug.Log($"Hand Size: {calculatedMaxHandSize} (base: {maxHandSize})");
            Debug.Log($"Mana Recovery: {calculatedManaRecovery} (base: {manaRecoveryPerTurn})");
            Debug.Log($"Relics: {currentRelics.Count}");
            foreach (var relic in currentRelics)
            {
                Debug.Log($"  - {relic.RelicName} ({relic.EffectType}: {relic.GetEffectValueString()})");
            }
        }
        #endregion

        #region Animation Management
        /// <summary>
        /// 애니메이션 상태 변경
        /// </summary>
        public void SetAnimationState(PlayerAnimationState newState)
        {
            if (currentAnimationState != newState)
            {
                // 기존 애니메이션 중지
                StopCurrentAnimation();

                currentAnimationState = newState;
                currentFrameIndex = 0;

                // 새 애니메이션 시작
                StartCurrentAnimation();

                OnPlayerAnimationChanged?.Invoke(currentAnimationState);

                // if (debugMode)
                Debug.Log($"[PlayerManager] 플레이어 애니메이션 상태 변경: {newState}");
            }
        }

        /// <summary>
        /// 현재 애니메이션 시작
        /// </summary>
        private void StartCurrentAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            Sprite[] sprites = GetCurrentSpriteArray();
            if (sprites != null && sprites.Length > 0)
            {
                if (sprites.Length > 1)
                {
                    // 멀티 프레임 애니메이션
                    isPlayingAnimation = true; // 코루틴 시작 전에 먼저 설정
                    float frameRate = GetFrameRateForCurrentState();
                    animationCoroutine = StartCoroutine(PlayAnimationFrames(sprites, frameRate));

                    if (debugMode)
                        Debug.Log($"[PlayerManager] 멀티프레임 애니메이션 시작: {currentAnimationState} ({sprites.Length} 프레임)");
                }
                else
                {
                    // 단일 프레임
                    currentFrameIndex = 0;
                    isPlayingAnimation = false;
                    OnPlayerAnimationChanged?.Invoke(currentAnimationState);

                    if (debugMode)
                        Debug.Log($"[PlayerManager] 단일프레임 설정: {currentAnimationState}");
                }
            }
            else
            {
                isPlayingAnimation = false;
                if (debugMode)
                    Debug.LogWarning($"[PlayerManager] {currentAnimationState} 상태에 스프라이트가 없습니다.");
            }
        }

        /// <summary>
        /// 현재 애니메이션 중지
        /// </summary>
        private void StopCurrentAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            isPlayingAnimation = false;
        }

        /// <summary>
        /// 현재 상태의 프레임 레이트 반환
        /// </summary>
        private float GetFrameRateForCurrentState()
        {
            switch (currentAnimationState)
            {
                case PlayerAnimationState.Idle:
                    return idleFrameRate;
                case PlayerAnimationState.Walk:
                    return walkFrameRate;
                case PlayerAnimationState.Attack1:
                case PlayerAnimationState.Attack2:
                case PlayerAnimationState.Attack3:
                    return attackFrameRate;
                case PlayerAnimationState.Hit:
                    return hitFrameRate;
                case PlayerAnimationState.Death:
                    return deathFrameRate;
                default:
                    return idleFrameRate;
            }
        }

        /// <summary>
        /// 애니메이션 프레임 재생 코루틴
        /// </summary>
        private System.Collections.IEnumerator PlayAnimationFrames(Sprite[] sprites, float frameRate)
        {
            if (sprites == null || sprites.Length == 0) yield break;

            float frameDuration = 1f / frameRate;
            bool isLooping = ShouldLoopAnimation(currentAnimationState);
            bool isDeathAnimation = currentAnimationState == PlayerAnimationState.Death;

            if (debugMode)
                Debug.Log($"[PlayerManager] 애니메이션 시작: {currentAnimationState}, 프레임 수: {sprites.Length}, 프레임레이트: {frameRate}, 루프: {isLooping}");

            do
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (!isPlayingAnimation) break; // 애니메이션이 중지되면 바로 종료

                    int previousFrameIndex = currentFrameIndex;
                    currentFrameIndex = i;
                    
                    // 프레임이 실제로 바뀐 경우에만 이벤트 발생 (최적화)
                    if (previousFrameIndex != currentFrameIndex)
                    {
                        OnPlayerAnimationChanged?.Invoke(currentAnimationState);
                    }

                    if (debugMode && isDeathAnimation)
                        Debug.Log($"[PlayerManager] 사망 애니메이션 프레임: [{i}/{sprites.Length - 1}]");

                    // 프레임 시간 대기
                    yield return new WaitForSeconds(frameDuration);
                }
            } while (isLooping && isPlayingAnimation);

            // 루프하지 않는 애니메이션이 끝나면 마지막 프레임에서 정지
            if (!isLooping)
            {
                currentFrameIndex = sprites.Length - 1;
                OnPlayerAnimationChanged?.Invoke(currentAnimationState);

                if (debugMode)
                    Debug.Log($"[PlayerManager] 애니메이션 완료: {currentAnimationState}, 마지막 프레임: {currentFrameIndex}");
            }
        }

        /// <summary>
        /// 해당 상태가 루프 애니메이션인지 확인
        /// </summary>
        private bool ShouldLoopAnimation(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Idle:
                    return true; // 대기 애니메이션은 루프
                case PlayerAnimationState.Walk:
                    return true; // 이동 애니메이션은 루프
                case PlayerAnimationState.Attack1:
                case PlayerAnimationState.Attack2:
                case PlayerAnimationState.Attack3:
                    return false; // 공격 애니메이션은 한 번만
                case PlayerAnimationState.Hit:
                    return false; // 피격 애니메이션은 한 번만
                case PlayerAnimationState.Death:
                    return false; // 사망 애니메이션은 한 번만 (마지막 프레임에서 정지)
                default:
                    return true;
            }
        }

        /// <summary>
        /// 현재 상태에 맞는 스프라이트 반환 (현재 프레임)
        /// </summary>
        public Sprite GetCurrentSprite()
        {
            Sprite[] sprites = GetCurrentSpriteArray();
            if (sprites != null && sprites.Length > 0)
            {
                int frameIndex = Mathf.Clamp(currentFrameIndex, 0, sprites.Length - 1);
                return sprites[frameIndex];
            }

            // 폴백: 기본 스프라이트 생성
            return CreateDefaultSprite(GetDefaultColorForState(currentAnimationState));
        }

        /// <summary>
        /// 현재 상태에 맞는 스프라이트 배열 반환
        /// </summary>
        private Sprite[] GetCurrentSpriteArray()
        {
            switch (currentAnimationState)
            {
                case PlayerAnimationState.Idle:
                    return idleSprites;
                case PlayerAnimationState.Walk:
                    return walkSprites;
                case PlayerAnimationState.Attack1:
                    return attackSprites1;
                case PlayerAnimationState.Attack2:
                    return attackSprites2;
                case PlayerAnimationState.Attack3:
                    return attackSprites3;
                case PlayerAnimationState.Hit:
                    return hitSprites;
                case PlayerAnimationState.Death:
                    return deathSprites;
                default:
                    return idleSprites;
            }
        }

        /// <summary>
        /// 상태별 기본 색상 반환
        /// </summary>
        private Color GetDefaultColorForState(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Idle:
                    return Color.blue;
                case PlayerAnimationState.Attack1:
                    return Color.red;
                case PlayerAnimationState.Attack2:
                    return Color.yellow;
                case PlayerAnimationState.Attack3:
                    return Color.green;
                case PlayerAnimationState.Hit:
                    return Color.magenta;
                case PlayerAnimationState.Death:
                    return Color.black;
                default:
                    return Color.blue;
            }
        }

        /// <summary>
        /// 특정 상태의 스프라이트 반환 (첫 번째 프레임)
        /// </summary>
        public Sprite GetSprite(PlayerAnimationState state)
        {
            Sprite[] sprites = null;
            switch (state)
            {
                case PlayerAnimationState.Idle:
                    sprites = idleSprites;
                    break;
                case PlayerAnimationState.Attack1:
                    sprites = attackSprites1;
                    break;
                case PlayerAnimationState.Attack2:
                    sprites = attackSprites2;
                    break;
                case PlayerAnimationState.Attack3:
                    sprites = attackSprites3;
                    break;
                case PlayerAnimationState.Hit:
                    sprites = hitSprites;
                    break;
                case PlayerAnimationState.Death:
                    sprites = deathSprites;
                    break;
                default:
                    sprites = idleSprites;
                    break;
            }

            if (sprites != null && sprites.Length > 0)
            {
                return sprites[0]; // 첫 번째 프레임 반환
            }

            return CreateDefaultSprite(GetDefaultColorForState(state));
        }

        /// <summary>
        /// 스프라이트 배열 설정
        /// </summary>
        public void SetSpriteArrays(Sprite[] idle, Sprite[] attack1, Sprite[] attack2 = null, Sprite[] attack3 = null, Sprite[] hit = null, Sprite[] death = null)
        {
            idleSprites = idle;
            attackSprites1 = attack1;
            if (attack2 != null) attackSprites2 = attack2;
            if (attack3 != null) attackSprites3 = attack3;
            if (hit != null) hitSprites = hit;
            if (death != null) deathSprites = death;

            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 스프라이트 배열 설정 완료");

            // 현재 상태의 스프라이트로 업데이트 이벤트 발생
            OnPlayerAnimationChanged?.Invoke(currentAnimationState);
        }

        /// <summary>
        /// 단일 스프라이트 설정 (이전 버전 호환용)
        /// </summary>
        public void SetSprites(Sprite idle, Sprite attack1, Sprite attack2 = null, Sprite attack3 = null, Sprite hit = null, Sprite death = null)
        {
            idleSprites = idle != null ? new Sprite[] { idle } : null;
            attackSprites1 = attack1 != null ? new Sprite[] { attack1 } : null;
            if (attack2 != null) attackSprites2 = new Sprite[] { attack2 };
            if (attack3 != null) attackSprites3 = new Sprite[] { attack3 };
            if (hit != null) hitSprites = new Sprite[] { hit };
            if (death != null) deathSprites = new Sprite[] { death };

            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 스프라이트 설정 완료 (단일 프레임)");

            // 현재 상태의 스프라이트로 업데이트 이벤트 발생
            OnPlayerAnimationChanged?.Invoke(currentAnimationState);
        }

        /// <summary>
        /// 이동 애니메이션 재생 (루프)
        /// </summary>
        public void PlayWalkAnimation()
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerManager] 이동 애니메이션 실행 요청 - 현재 상태: {currentAnimationState}");
                Debug.Log($"[PlayerManager] Walk 스프라이트 배열: {walkSprites?.Length ?? 0}개");
                if (walkSprites != null && walkSprites.Length > 0)
                {
                    Debug.Log($"[PlayerManager] Walk 스프라이트 [0]: {(walkSprites[0] != null ? walkSprites[0].name : "null")}");
                }
            }

            SetAnimationState(PlayerAnimationState.Walk);

            if (debugMode)
                Debug.Log($"[PlayerManager] 이동 애니메이션 설정 완료 - 새 상태: {currentAnimationState}");
        }

        /// <summary>
        /// 공격 애니메이션 실행 (일정 시간 후 대기 상태로 복귀)
        /// </summary>
        public void PlayAttackAnimation(int attackIndex = 1, float duration = 0.5f)
        {
            PlayerAnimationState attackState = PlayerAnimationState.Attack1;
            switch (attackIndex)
            {
                case 2:
                    attackState = PlayerAnimationState.Attack2;
                    break;
                case 3:
                    attackState = PlayerAnimationState.Attack3;
                    break;
                default:
                    attackState = PlayerAnimationState.Attack1;
                    break;
            }

            SetAnimationState(attackState);

            if (debugMode)
                Debug.Log($"[PlayerManager] 공격 애니메이션 실행: {attackState}");

            // 일정 시간 후 대기 상태로 복귀
            StartCoroutine(ReturnToIdleAfterDelay(duration));
        }

        /// <summary>
        /// 피격 애니메이션 실행 (일정 시간 후 대기 상태로 복귀)
        /// </summary>
        public void PlayHitAnimation(float duration = 0.3f)
        {
            SetAnimationState(PlayerAnimationState.Hit);

            if (debugMode)
                Debug.Log("[PlayerManager] 피격 애니메이션 실행");

            // 일정 시간 후 대기 상태로 복귀
            StartCoroutine(ReturnToIdleAfterDelay(duration));
        }

        /// <summary>
        /// 사망 애니메이션 실행 (슬로우 모션 효과 포함)
        /// </summary>
        public void PlayDeathAnimation()
        {
            if (debugMode)
            {
                Debug.Log("[PlayerManager] 사망 애니메이션 실행 (슬로우 모션 포함)");
                Debug.Log($"[PlayerManager] Death 스프라이트 확인: {deathSprites?.Length ?? 0}개");
                if (deathSprites != null)
                {
                    for (int i = 0; i < deathSprites.Length; i++)
                    {
                        if (deathSprites[i] != null)
                        {
                            Debug.Log($"[PlayerManager] Death 스프라이트 [{i}]: {deathSprites[i].name} (유효함)");
                        }
                        else
                        {
                            Debug.Log($"[PlayerManager] Death 스프라이트 [{i}]: null");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[PlayerManager] deathSprites 배열이 null입니다!");
                }

                // 다른 스프라이트들과 비교
                Debug.Log($"[PlayerManager] 비교 - Idle 스프라이트: {idleSprites?.Length ?? 0}개");
                Debug.Log($"[PlayerManager] 비교 - Walk 스프라이트: {walkSprites?.Length ?? 0}개");
            }

            SetAnimationState(PlayerAnimationState.Death);

            // 슬로우 모션 효과 시작
            StartCoroutine(ExecuteDeathSlowMotionEffect());
        }

        /// <summary>
        /// 사망 시 슬로우 모션 효과 실행
        /// </summary>
        private System.Collections.IEnumerator ExecuteDeathSlowMotionEffect()
        {
            if (debugMode)
                Debug.Log($"[PlayerManager] 사망 연출 시작: 슬로우 모션 {deathSlowMotionScale}x 속도, {deathSlowMotionDuration}초 지속");

            // 원래 시간 스케일 저장
            float originalTimeScale = Time.timeScale;

            // 슬로우 모션을 먼저 적용
            Time.timeScale = deathSlowMotionScale;

            if (debugMode)
                Debug.Log($"[PlayerManager] 슬로우 모션 적용: {deathSlowMotionScale}x");

            // 슬로우 모션이 적용된 상태에서 카메라 효과와 시각 효과 시작
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.StartDeathCameraEffect();
                PlayerBattleManager.Instance.StartDeathVisualEffect();
            }

            // 슬로우 모션 지속 시간 대기 (슬로우 모션이 적용된 시간 기준으로 기다림)
            // 실제로는 deathSlowMotionDuration / deathSlowMotionScale 만큼의 실제 시간이 걸림
            yield return new WaitForSeconds(deathSlowMotionDuration);

            if (debugMode)
                Debug.Log("[PlayerManager] 슬로우 모션 지속 시간 완료, 시간 스케일 복구 시작");

            // 시간 스케일 복구
            Time.timeScale = originalTimeScale;

            if (debugMode)
                Debug.Log("[PlayerManager] 슬로우 모션 종료, 시간 스케일 복구 완료");

            // 슬로우 모션이 완전히 끝난 후 카메라 줌 아웃과 시각 효과 종료
            if (PlayerBattleManager.Instance != null)
            {
                PlayerBattleManager.Instance.EndDeathCameraEffect();
                PlayerBattleManager.Instance.EndDeathVisualEffect();
            }
        }

        /// <summary>
        /// 대기 상태로 복귀
        /// </summary>
        public void ReturnToIdle()
        {
            // 강제로 Idle 애니메이션 재시작 (이미 Idle 상태여도 루프 애니메이션 재시작)
            ForceSetAnimationState(PlayerAnimationState.Idle);
        }

        /// <summary>
        /// 일정 시간 후 대기 상태로 복귀하는 코루틴
        /// </summary>
        private System.Collections.IEnumerator ReturnToIdleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // 죽은 상태가 아닐 때만 대기 상태로 복귀
            if (IsAlive && currentAnimationState != PlayerAnimationState.Death)
            {
                ReturnToIdle();
            }
        }
        #endregion

        #region Battle Management
        /// <summary>
        /// 전투 시작 시 호출
        /// </summary>
        public void OnBattleStart()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 전투 시작");

            // 카드 사용 횟수 초기화
            cardUseCountThisBattle = 0;

            // 전투 시작 시 필요한 초기화 작업
            RecoverManaForTurn();

            // 강제로 대기 상태 애니메이션 시작 (상태가 같아도 재시작)
            ForceSetAnimationState(PlayerAnimationState.Idle);
        }

        /// <summary>
        /// 애니메이션 상태 강제 설정 (상태가 같아도 재시작)
        /// </summary>
        public void ForceSetAnimationState(PlayerAnimationState newState)
        {
            if (debugMode)
                Debug.Log($"[PlayerManager] 애니메이션 강제 설정: {newState}");

            // 기존 애니메이션 중지
            StopCurrentAnimation();

            currentAnimationState = newState;
            currentFrameIndex = 0;

            // 새 애니메이션 시작
            StartCurrentAnimation();

            OnPlayerAnimationChanged?.Invoke(currentAnimationState);

            if (debugMode)
                Debug.Log($"[PlayerManager] 애니메이션 강제 설정 완료: {newState}");
        }

        /// <summary>
        /// 전투 종료 시 호출
        /// </summary>
        public void OnBattleEnd()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 전투 종료");

            // 전투 종료 시 필요한 정리 작업
        }

        /// <summary>
        /// 플레이어 위치 설정
        /// </summary>
        public void SetPosition(Vector2Int position)
        {
            if (debugMode)
                Debug.Log($"[PlayerManager] 플레이어 위치 설정: {position}");
        }

        /// <summary>
        /// 플레이어 스프라이트 반환
        /// </summary>
        public Sprite GetPlayerSprite()
        {
            // 현재 상태에 맞는 스프라이트 반환
            return GetCurrentSprite();
        }
        #endregion
    }
}