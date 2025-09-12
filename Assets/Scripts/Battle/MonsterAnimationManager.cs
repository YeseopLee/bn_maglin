using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 애니메이션 상태
    /// </summary>
    public enum MonsterAnimationState
    {
        Idle,
        Move,
        Attack,
        Hit,
        Death,
        Pattern
    }

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
        public static event System.Action<Maglin.Enemy.Enemy, MonsterAnimationState> OnMonsterAnimationChanged;

        /// <summary>
        /// 몬스터 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<Maglin.Enemy.Enemy, MonsterAnimationState> OnMonsterAnimationCompleted;
        #endregion

        #region Fields
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 몬스터별 애니메이션 상태 추적
        private Dictionary<Maglin.Enemy.Enemy, MonsterAnimationState> monsterStates = new Dictionary<Maglin.Enemy.Enemy, MonsterAnimationState>();
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
        public void SetMonsterAnimationState(Maglin.Enemy.Enemy monster, MonsterAnimationState newState, MonsterPatternSO pattern = null)
        {
            if (monster == null) return;

            // 죽은 몬스터는 Death 상태로만 변경 가능
            if (!monster.IsAlive && newState != MonsterAnimationState.Death)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] 죽은 몬스터 {monster.EnemyName}의 애니메이션 상태를 {newState}로 변경하려 했지만 무시됩니다.");
                return;
            }

            // Death 상태에서 다른 상태로 변경 시도 시 무시
            if (monsterStates.ContainsKey(monster) && monsterStates[monster] == MonsterAnimationState.Death && newState != MonsterAnimationState.Death)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] {monster.EnemyName}는 이미 Death 상태이므로 {newState} 상태 변경을 무시합니다.");
                return;
            }

            // 기존 애니메이션 중단
            StopMonsterAnimation(monster);

            // 새 상태 설정
            monsterStates[monster] = newState;

            // 패턴 애니메이션인 경우 패턴 저장
            if (newState == MonsterAnimationState.Pattern && pattern != null)
            {
                patternAnimations[monster] = pattern;
                if (debugMode)
                    Debug.Log($"[MonsterAnimationManager] 패턴 애니메이션 등록: {monster.EnemyName} - {pattern.PatternName} (스프라이트: {pattern.PatternSprites?.Length ?? 0}개)");
            }
            else
            {
                patternAnimations.Remove(monster);
                if (debugMode && newState == MonsterAnimationState.Pattern)
                    Debug.LogWarning($"[MonsterAnimationManager] 패턴 애니메이션 시도했지만 패턴이 null: {monster.EnemyName}");
            }

            // 애니메이션 시작
            StartMonsterAnimation(monster, newState);

            // 이벤트 발생
            OnMonsterAnimationChanged?.Invoke(monster, newState);

            if (debugMode)
                Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} 애니메이션 상태 변경: {newState}");
        }

        /// <summary>
        /// 몬스터의 현재 애니메이션 상태 가져오기
        /// </summary>
        public MonsterAnimationState GetMonsterAnimationState(Maglin.Enemy.Enemy monster)
        {
            if (monster == null) return MonsterAnimationState.Idle;
            return monsterStates.ContainsKey(monster) ? monsterStates[monster] : MonsterAnimationState.Idle;
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
            
            SetMonsterAnimationState(monster, MonsterAnimationState.Idle);
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
        private void StartMonsterAnimation(Maglin.Enemy.Enemy monster, MonsterAnimationState state)
        {
            if (monster == null || monster.EnemyData == null) return;

            Sprite[] sprites = GetSpritesForState(monster, state);
            if (sprites == null || sprites.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterAnimationManager] {monster.EnemyName}의 {state} 상태에 스프라이트가 없습니다.");
                return;
            }

            float animationSpeed = GetAnimationSpeed(monster, state);
            bool loop = ShouldLoop(state);

            // 애니메이션 코루틴 시작
            activeAnimations[monster] = StartCoroutine(PlaySpriteAnimation(monster, sprites, animationSpeed, loop, state));
        }

        /// <summary>
        /// 상태에 따른 스프라이트 배열 가져오기
        /// </summary>
        private Sprite[] GetSpritesForState(Maglin.Enemy.Enemy monster, MonsterAnimationState state)
        {
            EnemySO enemyData = monster.EnemyData;
            if (enemyData == null) return null;

            switch (state)
            {
                case MonsterAnimationState.Idle:
                    return enemyData.IdleSprites;
                case MonsterAnimationState.Move:
                    return enemyData.MoveSprites;
                case MonsterAnimationState.Attack:
                    return enemyData.AttackSprites;
                case MonsterAnimationState.Hit:
                    return enemyData.HitSprites;
                case MonsterAnimationState.Death:
                    return enemyData.DeathSprites;
                case MonsterAnimationState.Pattern:
                    if (patternAnimations.ContainsKey(monster))
                    {
                        var pattern = patternAnimations[monster];
                        return pattern.PatternSprites;
                    }
                    return null;
                default:
                    return enemyData.IdleSprites;
            }
        }

        /// <summary>
        /// 상태에 따른 애니메이션 속도 가져오기
        /// </summary>
        private float GetAnimationSpeed(Maglin.Enemy.Enemy monster, MonsterAnimationState state)
        {
            EnemySO enemyData = monster.EnemyData;
            if (enemyData == null) return 0.2f;

            if (state == MonsterAnimationState.Pattern && patternAnimations.ContainsKey(monster))
            {
                var pattern = patternAnimations[monster];
                return pattern.PatternAnimationSpeed;
            }

            return enemyData.AnimationSpeed;
        }

        /// <summary>
        /// 상태에 따른 루프 여부 결정
        /// </summary>
        private bool ShouldLoop(MonsterAnimationState state)
        {
            switch (state)
            {
                case MonsterAnimationState.Idle:
                case MonsterAnimationState.Move:
                    return true; // Idle과 Move는 루프
                case MonsterAnimationState.Attack:
                case MonsterAnimationState.Hit:
                case MonsterAnimationState.Death:
                case MonsterAnimationState.Pattern:
                    return false; // 한 번만 재생
                default:
                    return true;
            }
        }

        /// <summary>
        /// 스프라이트 애니메이션 재생 코루틴
        /// </summary>
        private IEnumerator PlaySpriteAnimation(Maglin.Enemy.Enemy monster, Sprite[] sprites, float frameTime, bool loop, MonsterAnimationState state)
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
        private void OnAnimationCompleted(Maglin.Enemy.Enemy monster, MonsterAnimationState state)
        {
            if (monster == null) return;

            // 애니메이션 완료 이벤트 발생
            OnMonsterAnimationCompleted?.Invoke(monster, state);

            if (debugMode)
                Debug.Log($"[MonsterAnimationManager] {monster.EnemyName} {state} 애니메이션 완료");

            // 상태별 완료 후 처리
            switch (state)
            {
                case MonsterAnimationState.Attack:
                case MonsterAnimationState.Hit:
                case MonsterAnimationState.Pattern:
                    // 일회성 애니메이션 완료 후 살아있는 몬스터만 Idle로 돌아가기
                    if (monster.IsAlive)
                    {
                        ReturnToIdle(monster);
                    }
                    break;
                case MonsterAnimationState.Death:
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
        public bool IsMonsterInState(Maglin.Enemy.Enemy monster, MonsterAnimationState state)
        {
            if (monster == null) return false;
            return monsterStates.ContainsKey(monster) && monsterStates[monster] == state;
        }
        #endregion
    }
}
