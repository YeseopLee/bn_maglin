using UnityEngine;
using System.Collections.Generic;

namespace Maglin.Battle
{
    /// <summary>
    /// CC(Crowd Control) 효과 타입 열거형
    /// </summary>
    public enum CCEffectType
    {
        None,       // 효과 없음
        Burn,       // 화상 - 턴 시작 시 최대 체력의 5% 피해
        Stun,       // 기절 - 행동 불가
        Blind       // 암흑 - 일반공격 100% 빗나감
    }

    /// <summary>
    /// CC 효과 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct CCEffectData
    {
        public CCEffectType effectType;     // CC 효과 타입
        public int remainingTurns;          // 남은 턴 수
        public float effectValue;           // 효과 수치 (화상의 경우 데미지 비율 등)

        public CCEffectData(CCEffectType type, int turns, float value = 0f)
        {
            effectType = type;
            remainingTurns = turns;
            effectValue = value;
        }

        /// <summary>
        /// CC 효과가 유효한지 확인
        /// </summary>
        public bool IsValid => effectType != CCEffectType.None && remainingTurns > 0;

        /// <summary>
        /// 턴 감소 처리
        /// </summary>
        public CCEffectData DecrementTurn()
        {
            return new CCEffectData(effectType, Mathf.Max(0, remainingTurns - 1), effectValue);
        }
    }

    /// <summary>
    /// 몬스터 CC 효과 관리 컴포넌트
    /// </summary>
    public class MonsterCCEffects : MonoBehaviour
    {
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 현재 적용된 CC 효과들
        private Dictionary<CCEffectType, CCEffectData> activeEffects = new Dictionary<CCEffectType, CCEffectData>();

        // 이벤트
        public static event System.Action<Maglin.Enemy.Enemy, CCEffectType, int> OnCCEffectApplied;
        public static event System.Action<Maglin.Enemy.Enemy, CCEffectType> OnCCEffectRemoved;
        public static event System.Action<Maglin.Enemy.Enemy, CCEffectType, float> OnCCEffectTriggered;

        private Maglin.Enemy.Enemy monster;

        #region Unity Lifecycle
        private void Awake()
        {
            monster = GetComponent<Maglin.Enemy.Enemy>();
            if (monster == null)
            {
                Debug.LogError($"[MonsterCCEffects] {gameObject.name}에서 Enemy 컴포넌트를 찾을 수 없습니다!");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// CC 효과 적용
        /// </summary>
        public void ApplyCCEffect(CCEffectType effectType, int turns, float effectValue = 0f)
        {
            if (effectType == CCEffectType.None || turns <= 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterCCEffects] 유효하지 않은 CC 효과: {effectType}, 턴: {turns}");
                return;
            }

            // 기본 효과 값 설정
            if (effectValue <= 0f)
            {
                switch (effectType)
                {
                    case CCEffectType.Burn:
                        effectValue = 0.05f; // 5% 화상 피해
                        break;
                    case CCEffectType.Stun:
                    case CCEffectType.Blind:
                        effectValue = 1f; // 100% 효과
                        break;
                }
            }

            CCEffectData newEffect = new CCEffectData(effectType, turns, effectValue);

            // 기존 효과가 있으면 더 긴 지속시간으로 갱신
            if (activeEffects.ContainsKey(effectType))
            {
                CCEffectData existingEffect = activeEffects[effectType];
                if (newEffect.remainingTurns > existingEffect.remainingTurns)
                {
                    activeEffects[effectType] = newEffect;
                    if (debugMode)
                        Debug.Log($"[MonsterCCEffects] {monster.name}의 {effectType} 효과 갱신: {newEffect.remainingTurns}턴");
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[MonsterCCEffects] {monster.name}의 {effectType} 효과 유지: 기존 {existingEffect.remainingTurns}턴 > 새로운 {newEffect.remainingTurns}턴");
                    return;
                }
            }
            else
            {
                activeEffects[effectType] = newEffect;
                if (debugMode)
                    Debug.Log($"[MonsterCCEffects] {monster.name}에게 {effectType} 효과 적용: {newEffect.remainingTurns}턴");
            }

            // 이벤트 발생
            OnCCEffectApplied?.Invoke(monster, effectType, turns);
        }

        /// <summary>
        /// CC 효과 제거
        /// </summary>
        public void RemoveCCEffect(CCEffectType effectType)
        {
            if (activeEffects.ContainsKey(effectType))
            {
                activeEffects.Remove(effectType);
                OnCCEffectRemoved?.Invoke(monster, effectType);

                if (debugMode)
                    Debug.Log($"[MonsterCCEffects] {monster.name}의 {effectType} 효과 제거");
            }
        }

        /// <summary>
        /// 특정 CC 효과가 적용되어 있는지 확인
        /// </summary>
        public bool HasCCEffect(CCEffectType effectType)
        {
            return activeEffects.ContainsKey(effectType) && activeEffects[effectType].IsValid;
        }

        /// <summary>
        /// 특정 CC 효과의 남은 턴 수 반환
        /// </summary>
        public int GetRemainingTurns(CCEffectType effectType)
        {
            if (activeEffects.ContainsKey(effectType))
            {
                return activeEffects[effectType].remainingTurns;
            }
            return 0;
        }

        /// <summary>
        /// 모든 CC 효과 제거
        /// </summary>
        public void ClearAllCCEffects()
        {
            var effectTypes = new List<CCEffectType>(activeEffects.Keys);
            foreach (var effectType in effectTypes)
            {
                RemoveCCEffect(effectType);
            }

            if (debugMode)
                Debug.Log($"[MonsterCCEffects] {monster.name}의 모든 CC 효과 제거");
        }

        /// <summary>
        /// 턴 시작 시 CC 효과 처리 (코루틴)
        /// </summary>
        public System.Collections.IEnumerator ProcessTurnStartEffects()
        {
            if (debugMode)
                Debug.Log($"[MonsterCCEffects] {monster.name} 턴 시작 CC 효과 처리");

            var effectsToRemove = new List<CCEffectType>();
            var effectsToUpdate = new Dictionary<CCEffectType, CCEffectData>();

            // 현재 효과들의 복사본을 만들어서 안전하게 순회
            var currentEffects = new Dictionary<CCEffectType, CCEffectData>(activeEffects);

            foreach (var kvp in currentEffects)
            {
                CCEffectType effectType = kvp.Key;
                CCEffectData effectData = kvp.Value;

                if (!effectData.IsValid)
                {
                    effectsToRemove.Add(effectType);
                    continue;
                }

                // 화상 효과 처리 (턴 감소 전에 먼저 처리, 애니메이션 대기)
                if (effectType == CCEffectType.Burn)
                {
                    yield return StartCoroutine(ProcessBurnEffectCoroutine(effectData));
                }

                // 턴 감소 (효과 적용 후에 감소)
                CCEffectData updatedEffect = effectData.DecrementTurn();
                if (updatedEffect.IsValid)
                {
                    effectsToUpdate[effectType] = updatedEffect;
                    if (debugMode)
                        Debug.Log($"[MonsterCCEffects] {monster.name}의 {effectType} 효과 턴 감소: {updatedEffect.remainingTurns}턴 남음");
                }
                else
                {
                    effectsToRemove.Add(effectType);
                    if (debugMode)
                        Debug.Log($"[MonsterCCEffects] {monster.name}의 {effectType} 효과 만료됨");
                }
            }

            // 업데이트할 효과들 적용
            foreach (var kvp in effectsToUpdate)
            {
                activeEffects[kvp.Key] = kvp.Value;
            }

            // 만료된 효과 제거
            foreach (var effectType in effectsToRemove)
            {
                RemoveCCEffect(effectType);
            }
        }

        /// <summary>
        /// 기절 상태인지 확인
        /// </summary>
        public bool IsStunned()
        {
            return HasCCEffect(CCEffectType.Stun);
        }

        /// <summary>
        /// 암흑 상태인지 확인
        /// </summary>
        public bool IsBlinded()
        {
            return HasCCEffect(CCEffectType.Blind);
        }

        /// <summary>
        /// 현재 적용된 모든 CC 효과 반환
        /// </summary>
        public Dictionary<CCEffectType, CCEffectData> GetActiveEffects()
        {
            return new Dictionary<CCEffectType, CCEffectData>(activeEffects);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 화상 효과 처리 (코루틴)
        /// </summary>
        private System.Collections.IEnumerator ProcessBurnEffectCoroutine(CCEffectData burnEffect)
        {
            if (monster == null) yield break;

            // 최대 체력의 5% 피해 계산
            int maxHealth = monster.MaxHealth;
            int burnDamage = Mathf.RoundToInt(maxHealth * burnEffect.effectValue);
            burnDamage = Mathf.Max(1, burnDamage); // 최소 1 피해

            if (debugMode)
                Debug.Log($"[MonsterCCEffects] {monster.name} 화상 피해: {burnDamage} (최대 체력: {maxHealth})");

            // 피해 적용
            monster.TakeDamage(burnDamage);

            // 이벤트 발생
            OnCCEffectTriggered?.Invoke(monster, CCEffectType.Burn, burnDamage);

            // 실제 Hit 애니메이션 재생 및 완료 대기
            if (Maglin.Battle.MonsterBattleManager.Instance != null)
            {
                yield return StartCoroutine(Maglin.Battle.MonsterBattleManager.Instance.PlayHitAnimationAndWait(monster));
            }
            else
            {
                // MonsterBattleManager가 없는 경우 기본 대기 시간
                yield return new UnityEngine.WaitForSeconds(0.5f);
            }

            if (debugMode)
                Debug.Log($"[MonsterCCEffects] {monster.name} 화상 피해 애니메이션 완료");
        }

        /// <summary>
        /// 화상 효과 처리 (기존 메서드 - 호환성 유지)
        /// </summary>
        private void ProcessBurnEffect(CCEffectData burnEffect)
        {
            if (monster == null) return;

            // 최대 체력의 5% 피해 계산
            int maxHealth = monster.MaxHealth;
            int burnDamage = Mathf.RoundToInt(maxHealth * burnEffect.effectValue);
            burnDamage = Mathf.Max(1, burnDamage); // 최소 1 피해

            if (debugMode)
                Debug.Log($"[MonsterCCEffects] {monster.name} 화상 피해: {burnDamage} (최대 체력: {maxHealth})");

            // 피해 적용
            monster.TakeDamage(burnDamage);

            // 이벤트 발생
            OnCCEffectTriggered?.Invoke(monster, CCEffectType.Burn, burnDamage);
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug CC Effects")]
        public void DebugCCEffects()
        {
            Debug.Log($"=== {monster?.name ?? gameObject.name} CC Effects ===");
            if (activeEffects.Count == 0)
            {
                Debug.Log("적용된 CC 효과 없음");
                return;
            }

            foreach (var kvp in activeEffects)
            {
                CCEffectType effectType = kvp.Key;
                CCEffectData effectData = kvp.Value;
                Debug.Log($"{effectType}: {effectData.remainingTurns}턴 남음, 효과값: {effectData.effectValue}");
            }
        }
        #endregion
    }
}
