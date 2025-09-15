using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터의 스프라이트 애니메이션을 관리하는 클래스
    /// </summary>
    public class MonsterAnimationManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterAnimationManager _instance;

        public static MonsterAnimationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterAnimationManager>();
                    if (_instance == null)
                    {
                        GameObject managerObj = new GameObject("MonsterAnimationManager");
                        _instance = managerObj.AddComponent<MonsterAnimationManager>();
                        DontDestroyOnLoad(managerObj);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 애니메이션 상태 변경 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy, Maglin.Enemy.MonsterAnimationState> OnMonsterAnimationChanged;

        /// <summary>
        /// 몬스터 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy, Maglin.Enemy.MonsterAnimationState> OnMonsterAnimationCompleted;
        #endregion

        #region Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 몬스터별 애니메이션 상태 추적
        private Dictionary<Maglin.Enemy.Enemy, Maglin.Enemy.MonsterAnimationState> monsterStates = new Dictionary<Maglin.Enemy.Enemy, Maglin.Enemy.MonsterAnimationState>();
        private Dictionary<Maglin.Enemy.Enemy, Coroutine> activeAnimations = new Dictionary<Maglin.Enemy.Enemy, Coroutine>();
        private Dictionary<Maglin.Enemy.Enemy, MonsterPatternSO> patternAnimations = new Dictionary<Maglin.Enemy.Enemy, MonsterPatternSO>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
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

        #region Public Methods
        /// <summary>
        /// 몬스터의 애니메이션 상태 변경
        /// </summary>
        public void SetMonsterAnimationState(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState newState, MonsterPatternSO pattern = null, float? customDuration = null)
        {
            if (monster == null) return;

            // 죽은 몬스터는 Death 상태로만 변경 가능
            if (!monster.IsAlive && newState != Maglin.Enemy.MonsterAnimationState.Death)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] 죽은 몬스터 {monster.EnemyName}의 애니메이션 상태를 {newState}로 변경하려 했지만 무시됩니다.");
                return;
            }

            // Death 상태에서 다른 상태로 변경 시도 시 무시
            if (monsterStates.ContainsKey(monster) && monsterStates[monster] == Maglin.Enemy.MonsterAnimationState.Death && newState != Maglin.Enemy.MonsterAnimationState.Death)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] {monster.EnemyName}는 이미 Death 상태이므로 {newState} 상태 변경을 무시합니다.");
                return;
            }

            // 기존 애니메이션 중단
            StopMonsterAnimation(monster);

            // 새 상태 설정
            monsterStates[monster] = newState;

            // 패턴 애니메이션인 경우 패턴 저장 (Pattern 상태는 EnemySO에서 제거되었으므로 패턴이 있으면 저장)
            if (pattern != null)
            {
                patternAnimations[monster] = pattern;
                if (debugMode)
                    Debug.Log($"[MonsterAnimationManager] 패턴 애니메이션 등록: {monster.EnemyName} - {pattern.PatternName} (스프라이트: {pattern.PatternSprites?.Length ?? 0}개)");
            }
            else
            {
                patternAnimations.Remove(monster);
            }

            // 애니메이션 시작 (커스텀 지속 시간 포함)
            StartMonsterAnimation(monster, newState, customDuration);

            // 이벤트 발생
            OnMonsterAnimationChanged?.Invoke(monster, newState);

            if (debugMode)
                Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} 애니메이션 상태 변경: {newState}{(customDuration.HasValue ? $" (커스텀 지속시간: {customDuration.Value:F2}초)" : "")}");
        }

        /// <summary>
        /// 몬스터의 현재 애니메이션 상태 가져오기
        /// </summary>
        public Maglin.Enemy.MonsterAnimationState GetMonsterAnimationState(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return Maglin.Enemy.MonsterAnimationState.Idle;
            return monsterStates.ContainsKey(monster) ? monsterStates[monster] : Maglin.Enemy.MonsterAnimationState.Idle;
        }

        /// <summary>
        /// 몬스터 애니메이션 중단
        /// </summary>
        public void StopMonsterAnimation(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return;

            if (activeAnimations.ContainsKey(monster) && activeAnimations[monster] != null)
            {
                StopCoroutine(activeAnimations[monster]);
                activeAnimations[monster] = null;
            }
        }

        /// <summary>
        /// 몬스터를 Idle 상태로 되돌리기
        /// </summary>
        public void ReturnToIdle(Maglin.Enemy.Enemy monster)
        {
            if (monster == null || !monster.IsAlive)
            {
                if (debugMode && monster != null && !monster.IsAlive)
                    Debug.LogWarning($"[MonsterAnimationManager] 죽은 몬스터 {monster.EnemyName}를 Idle 상태로 되돌리려 했지만 무시됩니다.");
                return;
            }

            // 이미 Idle 상태이면 중복 호출 방지
            var currentState = GetMonsterAnimationState(monster);
            if (currentState == Maglin.Enemy.MonsterAnimationState.Idle)
            {
                if (debugMode)
                    Debug.Log($"[MonsterAnimationManager] {monster.EnemyName}는 이미 Idle 상태입니다. 중복 호출 방지");
                return;
            }

            SetMonsterAnimationState(monster, Maglin.Enemy.MonsterAnimationState.Idle);
        }

        /// <summary>
        /// 몬스터 제거 시 정리
        /// </summary>
        public void RemoveMonster(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return;

            StopMonsterAnimation(monster);
            monsterStates.Remove(monster);
            activeAnimations.Remove(monster);
            patternAnimations.Remove(monster);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 몬스터 애니메이션 시작
        /// </summary>
        private void StartMonsterAnimation(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state, float? customDuration = null)
        {
            if (monster == null || monster.EnemyData == null) return;

            Sprite[] sprites = GetSpritesForState(monster, state);
            if (sprites == null || sprites.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] {monster.EnemyName}의 {state} 상태에 스프라이트가 없습니다.");
                return;
            }

            float frameTime;
            if (customDuration.HasValue)
            {
                // 커스텀 지속 시간이 있으면 해당 시간에 맞춰 프레임 타이밍 계산
                frameTime = customDuration.Value / sprites.Length;
                if (debugMode)
                    Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} {state} 커스텀 애니메이션: 총 {customDuration.Value:F2}초, 프레임당 {frameTime:F3}초");
            }
            else
            {
                // 기본 프레임 레이트 사용
                float frameRate = GetAnimationFrameRate(monster, state);
                frameTime = 1f / Mathf.Max(0.1f, frameRate);
            }

            bool loop = ShouldLoop(monster, state);

            // 애니메이션 코루틴 시작
            activeAnimations[monster] = StartCoroutine(PlaySpriteAnimation(monster, sprites, frameTime, loop, state));
        }

        /// <summary>
        /// 상태에 따른 스프라이트 배열 가져오기
        /// </summary>
        private Sprite[] GetSpritesForState(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state)
        {
            EnemySO enemyData = monster.EnemyData;
            if (enemyData == null) return null;

            // 패턴 애니메이션이 있으면 우선 사용 (이전 패턴 시스템 호환성)
            if (patternAnimations.ContainsKey(monster))
            {
                var pattern = patternAnimations[monster];
                if (pattern?.PatternSprites != null && pattern.PatternSprites.Length > 0)
                {
                    return pattern.PatternSprites;
                }
            }

            // EnemySO의 새로운 메서드 사용
            return enemyData.GetSpritesForState(state);
        }

        /// <summary>
        /// 상태에 따른 애니메이션 프레임 레이트 가져오기
        /// </summary>
        private float GetAnimationFrameRate(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state)
        {
            EnemySO enemyData = monster.EnemyData;
            if (enemyData == null) return 8f; // 기본 프레임 레이트

            // 패턴 애니메이션이 있으면 패턴의 속도를 프레임 레이트로 변환
            if (patternAnimations.ContainsKey(monster))
            {
                var pattern = patternAnimations[monster];
                if (pattern?.PatternSprites != null && pattern.PatternSprites.Length > 0)
                {
                    // AnimationSpeed는 프레임 간격(초)이므로 프레임 레이트로 변환
                    return pattern.PatternFrameRate;
                }
            }

            // EnemySO의 새로운 메서드 사용
            return enemyData.GetFrameRateForState(state);
        }

        /// <summary>
        /// 상태에 따른 루프 여부 결정
        /// </summary>
        private bool ShouldLoop(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state)
        {
            EnemySO enemyData = monster.EnemyData;
            if (enemyData == null) return true;

            // 패턴 애니메이션이 있으면 기본적으로 한 번만 실행
            if (patternAnimations.ContainsKey(monster))
            {
                var pattern = patternAnimations[monster];
                if (pattern?.PatternSprites != null && pattern.PatternSprites.Length > 0)
                {
                    return false; // 패턴 애니메이션은 일반적으로 한 번만 실행
                }
            }

            // EnemySO의 새로운 메서드 사용
            return enemyData.ShouldLoopAnimation(state);
        }

        /// <summary>
        /// 스프라이트 애니메이션 재생 코루틴
        /// </summary>
        private IEnumerator PlaySpriteAnimation(Maglin.Enemy.Enemy monster, Sprite[] sprites, float frameTime, bool loop, Maglin.Enemy.MonsterAnimationState state)
        {
            if (sprites == null || sprites.Length == 0) yield break;

            SpriteRenderer spriteRenderer = monster.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) yield break;

            int currentFrame = 0;
            bool isFirstLoop = true;

            do
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null)
                    {
                        // 스프라이트가 실제로 바뀌는 경우에만 설정 (최적화)
                        if (spriteRenderer.sprite != sprites[i])
                        {
                            spriteRenderer.sprite = sprites[i];
                        }

                        // flipX는 첫 번째 프레임에서만 설정 (이후에는 변경되지 않음)
                        if (isFirstLoop && i == 0 && monster.EnemyData != null)
                        {
                            spriteRenderer.flipX = monster.EnemyData.FlipSpritesHorizontally;
                        }

                        currentFrame = i;

                        // 디버그 로그는 첫 번째 루프에서만 출력 (성능 최적화)
                        if (debugMode && (isFirstLoop || !loop))
                            Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} {state} 프레임 {i}/{sprites.Length - 1} (flipX: {spriteRenderer.flipX})");
                    }

                    yield return new WaitForSeconds(frameTime);

                    // 몬스터가 제거되었거나 상태가 변경된 경우 중단
                    if (monster == null || !monsterStates.ContainsKey(monster) || monsterStates[monster] != state)
                    {
                        yield break;
                    }
                }

                isFirstLoop = false;
            } while (loop);

            // 애니메이션 완료 처리
            OnAnimationCompleted(monster, state);
        }

        /// <summary>
        /// 애니메이션 완료 처리
        /// </summary>
        private void OnAnimationCompleted(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state)
        {
            if (monster == null) return;

            // 애니메이션 완료 이벤트 발생
            OnMonsterAnimationCompleted?.Invoke(monster, state);

            if (debugMode)
                Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} {state} 애니메이션 완료");

            // 상태별 완료 후 처리
            switch (state)
            {
                case Maglin.Enemy.MonsterAnimationState.Attack:
                case Maglin.Enemy.MonsterAnimationState.Hit:
                    // 일회성 애니메이션 완료 후 살아있는 몬스터만 Idle로 돌아가기
                    if (monster.IsAlive)
                    {
                        ReturnToIdle(monster);
                    }
                    break;
                case Maglin.Enemy.MonsterAnimationState.Death:
                    // 사망 애니메이션 완료 후 마지막 프레임 유지 (더 이상 애니메이션 없음)
                    if (debugMode)
                        Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} 사망 애니메이션 완료 - 마지막 프레임 유지");

                    // 애니메이션 시스템에서 몬스터 제거
                    RemoveMonster(monster);
                    return; // early return으로 더 이상 처리하지 않음
            }

            // 활성 애니메이션에서 제거
            if (activeAnimations.ContainsKey(monster))
            {
                activeAnimations[monster] = null;
            }
        }
        #endregion

        #region Public Utility Methods
        /// <summary>
        /// 몬스터가 애니메이션 중인지 확인
        /// </summary>
        public bool IsMonsterAnimating(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return false;
            return activeAnimations.ContainsKey(monster) && activeAnimations[monster] != null;
        }

        /// <summary>
        /// 몬스터가 특정 상태의 애니메이션 중인지 확인
        /// </summary>
        public bool IsMonsterInState(Maglin.Enemy.Enemy monster, Maglin.Enemy.MonsterAnimationState state)
        {
            if (monster == null) return false;
            return monsterStates.ContainsKey(monster) && monsterStates[monster] == state;
        }
        #endregion
    }
}
