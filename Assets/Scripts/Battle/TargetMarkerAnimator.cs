using UnityEngine;

namespace Maglin.Battle
{
    /// <summary>
    /// 타겟 마커의 애니메이션을 관리하는 컴포넌트
    /// </summary>
    public class TargetMarkerAnimator : MonoBehaviour
    {
        [Header("애니메이션 설정")]
        [SerializeField] private Sprite[] animationFrames = new Sprite[0]; // 애니메이션 프레임들
        [SerializeField] private float frameRate = 10f; // 초당 프레임 수
        [SerializeField] private bool playOnEnable = true; // 활성화 시 자동 재생
        [SerializeField] private bool loop = true; // 반복 재생

        [Header("스케일 애니메이션")]
        [SerializeField] private bool enableScaleAnimation = true; // 스케일 애니메이션 활성화
        [SerializeField] private Vector3 minScale = new Vector3(0.8f, 0.8f, 1f); // 최소 스케일
        [SerializeField] private Vector3 maxScale = new Vector3(1.2f, 1.2f, 1f); // 최대 스케일
        [SerializeField] private float scaleSpeed = 2f; // 스케일 애니메이션 속도

        [Header("회전 애니메이션")]
        [SerializeField] private bool enableRotationAnimation = false; // 회전 애니메이션 활성화
        [SerializeField] private float rotationSpeed = 180f; // 초당 회전 각도

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 컴포넌트 참조
        private SpriteRenderer spriteRenderer;
        private Animator animator; // Unity Animator 사용 시를 위한 참조

        // 애니메이션 상태
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private bool isPlaying = false;
        private float scaleTimer = 0f;

        // 원본 값들
        private Vector3 originalScale;
        private Quaternion originalRotation;

        #region Unity Lifecycle
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            // 원본 값 저장
            originalScale = transform.localScale;
            originalRotation = transform.localRotation;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 초기화 완료");
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                StartAnimation();
            }
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        private void Update()
        {
            if (!isPlaying) return;

            // 스프라이트 애니메이션 업데이트
            UpdateSpriteAnimation();

            // 스케일 애니메이션 업데이트
            if (enableScaleAnimation)
            {
                UpdateScaleAnimation();
            }

            // 회전 애니메이션 업데이트
            if (enableRotationAnimation)
            {
                UpdateRotationAnimation();
            }
        }
        #endregion

        #region Animation Control
        /// <summary>
        /// 애니메이션 시작
        /// </summary>
        public void StartAnimation()
        {
            if (animationFrames == null || animationFrames.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[TargetMarkerAnimator] {gameObject.name}: 애니메이션 프레임이 설정되지 않았습니다.");
                return;
            }

            isPlaying = true;
            currentFrame = 0;
            frameTimer = 0f;
            scaleTimer = 0f;

            // 첫 번째 프레임 설정
            if (spriteRenderer != null && animationFrames.Length > 0)
            {
                spriteRenderer.sprite = animationFrames[0];
            }

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 애니메이션 시작 (프레임 수: {animationFrames.Length})");
        }

        /// <summary>
        /// 애니메이션 중지
        /// </summary>
        public void StopAnimation()
        {
            isPlaying = false;

            // 원본 상태로 복원
            transform.localScale = originalScale;
            transform.localRotation = originalRotation;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 애니메이션 중지");
        }

        /// <summary>
        /// 애니메이션 일시정지/재개
        /// </summary>
        public void TogglePause()
        {
            isPlaying = !isPlaying;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 애니메이션 {(isPlaying ? "재개" : "일시정지")}");
        }
        #endregion

        #region Animation Updates
        /// <summary>
        /// 스프라이트 애니메이션 업데이트
        /// </summary>
        private void UpdateSpriteAnimation()
        {
            if (animationFrames == null || animationFrames.Length == 0 || spriteRenderer == null) return;

            frameTimer += Time.deltaTime;

            if (frameTimer >= 1f / frameRate)
            {
                frameTimer = 0f;
                currentFrame++;

                if (currentFrame >= animationFrames.Length)
                {
                    if (loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = animationFrames.Length - 1;
                        isPlaying = false;
                        return;
                    }
                }

                spriteRenderer.sprite = animationFrames[currentFrame];
            }
        }

        /// <summary>
        /// 스케일 애니메이션 업데이트 (펄스 효과)
        /// </summary>
        private void UpdateScaleAnimation()
        {
            scaleTimer += Time.deltaTime * scaleSpeed;
            float scaleFactor = Mathf.Lerp(0f, 1f, (Mathf.Sin(scaleTimer) + 1f) / 2f);
            Vector3 currentScale = Vector3.Lerp(minScale, maxScale, scaleFactor);
            transform.localScale = Vector3.Scale(originalScale, currentScale);
        }

        /// <summary>
        /// 회전 애니메이션 업데이트
        /// </summary>
        private void UpdateRotationAnimation()
        {
            float rotationAmount = rotationSpeed * Time.deltaTime;
            transform.Rotate(0, 0, rotationAmount);
        }
        #endregion

        #region Public API
        /// <summary>
        /// 애니메이션 프레임 설정
        /// </summary>
        public void SetAnimationFrames(Sprite[] frames)
        {
            animationFrames = frames;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 애니메이션 프레임 설정: {frames?.Length ?? 0}개");
        }

        /// <summary>
        /// 프레임 레이트 설정
        /// </summary>
        public void SetFrameRate(float newFrameRate)
        {
            frameRate = Mathf.Max(0.1f, newFrameRate);

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 프레임 레이트 설정: {frameRate}");
        }

        /// <summary>
        /// 스케일 애니메이션 설정
        /// </summary>
        public void SetScaleAnimation(bool enabled, Vector3 min, Vector3 max, float speed)
        {
            enableScaleAnimation = enabled;
            minScale = min;
            maxScale = max;
            scaleSpeed = speed;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 스케일 애니메이션 설정: {enabled}");
        }

        /// <summary>
        /// 회전 애니메이션 설정
        /// </summary>
        public void SetRotationAnimation(bool enabled, float speed)
        {
            enableRotationAnimation = enabled;
            rotationSpeed = speed;

            if (debugMode)
                Debug.Log($"[TargetMarkerAnimator] {gameObject.name} 회전 애니메이션 설정: {enabled}, 속도: {speed}");
        }

        /// <summary>
        /// 애니메이션 재생 상태 확인
        /// </summary>
        public bool IsPlaying => isPlaying;

        /// <summary>
        /// 현재 프레임 인덱스
        /// </summary>
        public int CurrentFrame => currentFrame;

        /// <summary>
        /// 총 프레임 수
        /// </summary>
        public int FrameCount => animationFrames?.Length ?? 0;
        #endregion
    }
}
