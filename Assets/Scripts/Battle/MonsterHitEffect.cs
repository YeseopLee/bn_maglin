using UnityEngine;
using System.Collections;
using DG.Tweening;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 히트 효과를 처리하는 컴포넌트
    /// </summary>
    public class MonsterHitEffect : MonoBehaviour
    {
        [Header("히트 효과 설정")]
        [SerializeField] private Color hitColor = Color.red;
        [SerializeField] private float hitDuration = 0.3f;
        [SerializeField] private int flashCount = 3;
        [SerializeField] private bool useShake = true;
        [SerializeField] private float shakeStrength = 0.1f;
        [SerializeField] private float shakeDuration = 0.2f;

        [Header("스케일 효과")]
        [SerializeField] private bool useScaleEffect = true;
        [SerializeField] private float scaleMultiplier = 1.1f;
        [SerializeField] private float scaleDuration = 0.15f;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 컴포넌트 참조
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Vector3 originalPosition;
        private Vector3 originalScale;

        // 상태 관리
        private bool isPlayingEffect = false;
        private Sequence currentSequence;

        #region Unity Events
        private void Awake()
        {
            // SpriteRenderer 찾기
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                Debug.LogWarning($"[MonsterHitEffect] {gameObject.name}에서 SpriteRenderer를 찾을 수 없습니다.");
                return;
            }

            // 초기 값 저장
            originalPosition = transform.position;
            originalScale = transform.localScale;

            if (debugMode)
                Debug.Log($"[MonsterHitEffect] {gameObject.name} 초기화 완료");
        }

        private void Start()
        {
            // Enemy 컴포넌트에서 원래 색상 가져오기
            var enemy = GetComponent<Maglin.Enemy.Enemy>();
            if (enemy != null && enemy.EnemyData != null)
            {
                originalColor = enemy.EnemyData.Color;
                if (debugMode)
                    Debug.Log($"[MonsterHitEffect] {gameObject.name} 원래 색상 설정: {originalColor}");
            }
            else
            {
                // Enemy가 없으면 현재 색상을 원래 색상으로 설정
                originalColor = spriteRenderer.color;
                if (debugMode)
                    Debug.Log($"[MonsterHitEffect] {gameObject.name} 현재 색상을 원래 색상으로 설정: {originalColor}");
            }
        }

        private void OnDestroy()
        {
            // DOTween 시퀀스 정리
            if (currentSequence != null)
            {
                currentSequence.Kill();
                currentSequence = null;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 현재 위치를 원래 위치로 업데이트 (몬스터가 이동했을 때 호출)
        /// </summary>
        public void UpdateOriginalPosition()
        {
            if (!isPlayingEffect)
            {
                originalPosition = transform.position;
                originalScale = transform.localScale;

                if (debugMode)
                    Debug.Log($"[MonsterHitEffect] {gameObject.name} 원래 위치 업데이트: {originalPosition}");
            }
        }

        /// <summary>
        /// 히트 효과 재생
        /// </summary>
        public void PlayHitEffect(int damage = 0)
        {
            if (spriteRenderer == null) return;

            // 이미 재생 중이면 현재 효과를 중지하고 새로운 효과 시작
            if (isPlayingEffect)
            {
                StopEffect();
            }

            // 현재 위치를 원래 위치로 업데이트 (히트 효과 시작 전)
            UpdateOriginalPosition();

            if (debugMode)
                Debug.Log($"[MonsterHitEffect] {gameObject.name} 히트 효과 재생 (데미지: {damage}) at {originalPosition}");

            StartCoroutine(PlayHitEffectCoroutine(damage));
        }

        /// <summary>
        /// 커스텀 히트 효과 재생
        /// </summary>
        public void PlayCustomHitEffect(Color customColor, float customDuration, int customFlashCount = 3)
        {
            if (spriteRenderer == null || isPlayingEffect) return;

            if (debugMode)
                Debug.Log($"[MonsterHitEffect] {gameObject.name} 커스텀 히트 효과 재생");

            StartCoroutine(PlayCustomHitEffectCoroutine(customColor, customDuration, customFlashCount));
        }

        /// <summary>
        /// 현재 재생 중인 효과 중지
        /// </summary>
        public void StopEffect()
        {
            if (currentSequence != null)
            {
                currentSequence.Kill(true); // true를 전달하여 트윈 완료 상태로 설정
                currentSequence = null;
            }

            isPlayingEffect = false;

            // 원래 상태로 확실히 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            // 위치와 스케일을 즉시 복원
            transform.position = originalPosition;
            transform.localScale = originalScale;

            if (debugMode)
                Debug.Log($"[MonsterHitEffect] {gameObject.name} 효과 중지 및 상태 복원");
        }

        /// <summary>
        /// 히트 효과가 재생 중인지 확인
        /// </summary>
        public bool IsPlayingEffect => isPlayingEffect;
        #endregion

        #region Private Methods
        /// <summary>
        /// 히트 효과 코루틴
        /// </summary>
        private IEnumerator PlayHitEffectCoroutine(int damage)
        {
            isPlayingEffect = true;

            // DOTween 시퀀스 생성
            currentSequence = DOTween.Sequence();

            // 색상 점멸 효과
            var colorTween = CreateColorFlashTween();
            currentSequence.Append(colorTween);

            // 쉐이크 효과 (병렬 실행)
            if (useShake)
            {
                var shakeTween = CreateShakeTween();
                currentSequence.Join(shakeTween);
            }

            // 스케일 효과 (병렬 실행)
            if (useScaleEffect)
            {
                var scaleTween = CreateScaleTween();
                currentSequence.Join(scaleTween);
            }

            // 완료 콜백
            currentSequence.OnComplete(() =>
            {
                isPlayingEffect = false;
                currentSequence = null;

                if (debugMode)
                    Debug.Log($"[MonsterHitEffect] {gameObject.name} 히트 효과 완료");
            });

            // 시퀀스 재생
            currentSequence.Play();

            yield return currentSequence.WaitForCompletion();
        }

        /// <summary>
        /// 커스텀 히트 효과 코루틴
        /// </summary>
        private IEnumerator PlayCustomHitEffectCoroutine(Color customColor, float customDuration, int customFlashCount)
        {
            isPlayingEffect = true;

            // DOTween 시퀀스 생성
            currentSequence = DOTween.Sequence();

            // 커스텀 색상 점멸 효과
            var colorTween = CreateCustomColorFlashTween(customColor, customDuration, customFlashCount);
            currentSequence.Append(colorTween);

            // 쉐이크 효과 (병렬 실행)
            if (useShake)
            {
                var shakeTween = CreateShakeTween();
                currentSequence.Join(shakeTween);
            }

            // 완료 콜백
            currentSequence.OnComplete(() =>
            {
                isPlayingEffect = false;
                currentSequence = null;
            });

            // 시퀀스 재생
            currentSequence.Play();

            yield return currentSequence.WaitForCompletion();
        }

        /// <summary>
        /// 색상 점멸 트윈 생성
        /// </summary>
        private Tween CreateColorFlashTween()
        {
            var sequence = DOTween.Sequence();

            float singleFlashDuration = hitDuration / flashCount;

            for (int i = 0; i < flashCount; i++)
            {
                // 히트 색상으로 변경
                sequence.Append(spriteRenderer.DOColor(hitColor, singleFlashDuration * 0.5f));
                // 원래 색상으로 복원
                sequence.Append(spriteRenderer.DOColor(originalColor, singleFlashDuration * 0.5f));
            }

            return sequence;
        }

        /// <summary>
        /// 커스텀 색상 점멸 트윈 생성
        /// </summary>
        private Tween CreateCustomColorFlashTween(Color customColor, float duration, int flashCount)
        {
            var sequence = DOTween.Sequence();

            float singleFlashDuration = duration / flashCount;

            for (int i = 0; i < flashCount; i++)
            {
                // 커스텀 색상으로 변경
                sequence.Append(spriteRenderer.DOColor(customColor, singleFlashDuration * 0.5f));
                // 원래 색상으로 복원
                sequence.Append(spriteRenderer.DOColor(originalColor, singleFlashDuration * 0.5f));
            }

            return sequence;
        }

        /// <summary>
        /// 쉐이크 트윈 생성
        /// </summary>
        private Tween CreateShakeTween()
        {
            // 현재 위치를 새로운 원점으로 업데이트
            Vector3 currentPos = transform.position;

            return transform.DOShakePosition(shakeDuration, shakeStrength, 10, 90, false, true)
                           .OnComplete(() =>
                           {
                               // 쉐이크 전 위치로 정확히 복원
                               transform.position = currentPos;
                           });
        }

        /// <summary>
        /// 스케일 트윈 생성
        /// </summary>
        private Tween CreateScaleTween()
        {
            var sequence = DOTween.Sequence();

            // 크기 증가
            sequence.Append(transform.DOScale(originalScale * scaleMultiplier, scaleDuration * 0.5f));
            // 원래 크기로 복원
            sequence.Append(transform.DOScale(originalScale, scaleDuration * 0.5f));

            return sequence;
        }
        #endregion

        #region Editor Methods
#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 테스트용 히트 효과 재생
        /// </summary>
        [ContextMenu("Test Hit Effect")]
        private void TestHitEffect()
        {
            if (Application.isPlaying)
            {
                PlayHitEffect(100);
            }
            else
            {
                Debug.Log("[MonsterHitEffect] 플레이 모드에서만 테스트 가능합니다.");
            }
        }

        /// <summary>
        /// 에디터에서 커스텀 히트 효과 테스트
        /// </summary>
        [ContextMenu("Test Custom Hit Effect")]
        private void TestCustomHitEffect()
        {
            if (Application.isPlaying)
            {
                PlayCustomHitEffect(Color.yellow, 0.5f, 5);
            }
            else
            {
                Debug.Log("[MonsterHitEffect] 플레이 모드에서만 테스트 가능합니다.");
            }
        }
#endif
        #endregion
    }
}
