using UnityEngine;
using System;
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

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 계산된 스탯 (유물 효과 적용)
        private int calculatedMaxHealth;
        private int calculatedMaxMana;
        private int calculatedManaRecovery;
        private int calculatedMaxHandSize;

        // 초기화 관련
        private bool isInitialized = false;
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

            // 기본 스탯으로 초기화
            ResetToDefault();

            // GameManager 이벤트 구독
            GameManager.OnGameInitialized += OnGameInitialized;

            isInitialized = true;

            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 초기화 완료");
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

            // 이벤트 발생
            NotifyAllStatsChanged();

            if (debugMode)
                Debug.Log($"[PlayerManager] 리셋 완료 - 체력: {currentHealth}/{calculatedMaxHealth}");
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
            if (damage <= 0) return;

            int oldHealth = currentHealth;
            currentHealth = Mathf.Max(currentHealth - damage, 0);

            OnHealthChanged?.Invoke(currentHealth, calculatedMaxHealth);

            if (debugMode)
                Debug.Log($"[PlayerManager] 데미지: {oldHealth} -> {currentHealth} (-{damage})");

            // 사망 체크
            if (currentHealth <= 0 && oldHealth > 0)
            {
                HandlePlayerDeath();
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
        /// 플레이어 사망 처리
        /// </summary>
        private void HandlePlayerDeath()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 플레이어 사망");

            OnPlayerDeath?.Invoke();

            // GameManager에게 게임 오버 알림
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
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
                case RelicEffectType.HealthModifier:
                    if (relic.IsPercentage)
                        calculatedMaxHealth = Mathf.RoundToInt(calculatedMaxHealth * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxHealth += Mathf.RoundToInt(relic.EffectValue);
                    break;

                case RelicEffectType.ManaModifier:
                    if (relic.IsPercentage)
                        calculatedMaxMana = Mathf.RoundToInt(calculatedMaxMana * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxMana += Mathf.RoundToInt(relic.EffectValue);
                    break;

                case RelicEffectType.CardDrawModifier:
                    if (relic.IsPercentage)
                        calculatedMaxHandSize = Mathf.RoundToInt(calculatedMaxHandSize * (1f + relic.EffectValue / 100f));
                    else
                        calculatedMaxHandSize += Mathf.RoundToInt(relic.EffectValue);
                    break;

                // 다른 효과들은 해당 시스템에서 별도로 처리
                case RelicEffectType.DamageModifier:
                case RelicEffectType.GoldModifier:
                case RelicEffectType.ShopPriceModifier:
                case RelicEffectType.DeckSizeModifier:
                case RelicEffectType.FieldEffectModifier:
                case RelicEffectType.CustomEffect:
                    // 해당 시스템에서 GetRelicModifier 메서드를 통해 처리
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
        /// 모든 스탯 변경 이벤트 발생
        /// </summary>
        private void NotifyAllStatsChanged()
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

        #region Battle Management
        /// <summary>
        /// 전투 시작 시 호출
        /// </summary>
        public void OnBattleStart()
        {
            if (debugMode)
                Debug.Log("[PlayerManager] 전투 시작");

            // 전투 시작 시 필요한 초기화 작업
            RecoverManaForTurn();
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
            // 기본적으로 null을 반환하여 PlayerBattleManager에서 기본 스프라이트를 생성하도록 함
            // 나중에 실제 플레이어 스프라이트를 로드하는 로직을 추가할 수 있음
            return null;
        }
        #endregion
    }
}