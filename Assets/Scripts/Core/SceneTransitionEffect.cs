using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Threading.Tasks;

namespace Maglin.Core
{
    /// <summary>
    /// 전환 효과 스타일
    /// </summary>
    public enum TransitionStyle
    {
        Solid,      // 기본 단색
        Gradient,   // 그라데이션
        Stripes     // 스트라이프
    }

    /// <summary>
    /// 씬 전환 시 자연스러운 화면 전환 효과를 제공하는 클래스
    /// 그라데이션, 스트라이프 등 다양한 전환 스타일 지원
    /// </summary>
    public class SceneTransitionEffect : MonoBehaviour
    {
        #region Fields
        [Header("전환 효과 설정")]
        [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.Gradient;
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private Ease transitionEase = Ease.OutExpo;
        [SerializeField] private Color transitionColor = Color.black;
        [SerializeField] private int canvasSortingOrder = 10000; // 가장 위에 렌더링

        [Header("그라데이션 설정")]
        [SerializeField] private int gradientSteps = 20; // 그라데이션 세분화 정도
        [SerializeField] private float gradientSmoothness = 2f; // 그라데이션 부드러움 정도

        [Header("스트라이프 설정")]
        [SerializeField] private int stripeCount = 8; // 스트라이프 개수
        [SerializeField] private float stripeDelay = 0.05f; // 각 스트라이프 간 딜레이
        [SerializeField] private float maxStaggerTime = 0.3f; // 전체 스트라이프 시차의 최대 시간

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // UI 요소들
        private Canvas transitionCanvas;
        private GameObject transitionContainer;
        private bool isTransitioning = false;

        // 스타일별 UI 요소들
        private Image solidImage;
        private RectTransform solidRect;
        private Image[] gradientImages;
        private RectTransform[] gradientRects;
        private Image[] stripeImages;
        private RectTransform[] stripeRects;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CreateTransitionUI();
        }

        private void Start()
        {
            // 시작할 때는 전환 효과 숨김
            HideTransition();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 전환 효과 강제 리셋 (같은 씬으로의 전환 시 사용)
        /// </summary>
        public void ForceReset()
        {
            if (debugMode)
                Debug.Log("[SceneTransitionEffect] 전환 효과 강제 리셋");

            isTransitioning = false;
            HideTransition();
        }

        /// <summary>
        /// 전환 시작 - 오른쪽에서 왼쪽으로 덮기
        /// </summary>
        public async Task StartTransitionIn()
        {
            if (isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning("[SceneTransitionEffect] 이미 전환 중입니다. 강제 리셋 후 재시작합니다.");
                ForceReset();
                await Task.Delay(50); // 짧은 딜레이 후 재시작
            }

            isTransitioning = true;

            if (debugMode)
                Debug.Log($"[SceneTransitionEffect] 전환 시작 - {transitionStyle} 스타일");

            // 전환 UI 활성화
            if (transitionCanvas != null)
                transitionCanvas.gameObject.SetActive(true);

            // 스타일에 따른 전환 애니메이션
            switch (transitionStyle)
            {
                case TransitionStyle.Solid:
                    await StartSolidTransitionIn();
                    break;
                case TransitionStyle.Gradient:
                    await StartGradientTransitionIn();
                    break;
                case TransitionStyle.Stripes:
                    await StartStripesTransitionIn();
                    break;
            }

            if (debugMode)
                Debug.Log("[SceneTransitionEffect] 전환 덮기 완료");
        }

        /// <summary>
        /// 전환 완료 - 왼쪽으로 걷어내기
        /// </summary>
        public async Task StartTransitionOut()
        {
            if (!isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning("[SceneTransitionEffect] 전환이 시작되지 않았습니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[SceneTransitionEffect] 전환 완료 - {transitionStyle} 스타일 걷어내기");

            // 스타일에 따른 전환 애니메이션
            switch (transitionStyle)
            {
                case TransitionStyle.Solid:
                    await StartSolidTransitionOut();
                    break;
                case TransitionStyle.Gradient:
                    await StartGradientTransitionOut();
                    break;
                case TransitionStyle.Stripes:
                    await StartStripesTransitionOut();
                    break;
            }

            // 전환 UI 비활성화
            HideTransition();
            isTransitioning = false;

            if (debugMode)
                Debug.Log("[SceneTransitionEffect] 전환 걷어내기 완료");
        }

        /// <summary>
        /// 즉시 전환 효과 숨김
        /// </summary>
        public void HideTransition()
        {
            if (transitionCanvas != null)
                transitionCanvas.gameObject.SetActive(false);

            isTransitioning = false;
        }

        /// <summary>
        /// 전환 중인지 확인
        /// </summary>
        public bool IsTransitioning => isTransitioning;
        #endregion

        #region Private Methods
        /// <summary>
        /// 전환 UI 생성
        /// </summary>
        private void CreateTransitionUI()
        {
            if (debugMode)
                Debug.Log($"[SceneTransitionEffect] 전환 UI 생성 시작 - 스타일: {transitionStyle}");

            // Canvas 생성
            GameObject canvasObject = new GameObject("SceneTransitionCanvas");
            canvasObject.transform.SetParent(transform);

            transitionCanvas = canvasObject.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = canvasSortingOrder;

            // CanvasScaler 추가
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster 추가
            canvasObject.AddComponent<GraphicRaycaster>();

            // 컨테이너 생성
            transitionContainer = new GameObject("TransitionContainer");
            transitionContainer.transform.SetParent(canvasObject.transform, false);

            RectTransform containerRect = transitionContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // 스타일에 따른 UI 생성
            switch (transitionStyle)
            {
                case TransitionStyle.Solid:
                    CreateSolidUI();
                    break;
                case TransitionStyle.Gradient:
                    CreateGradientUI();
                    break;
                case TransitionStyle.Stripes:
                    CreateStripesUI();
                    break;
            }

            if (debugMode)
                Debug.Log("[SceneTransitionEffect] 전환 UI 생성 완료");
        }

        /// <summary>
        /// 단색 UI 생성
        /// </summary>
        private void CreateSolidUI()
        {
            GameObject imageObject = new GameObject("SolidImage");
            imageObject.transform.SetParent(transitionContainer.transform, false);

            solidImage = imageObject.AddComponent<Image>();
            solidImage.color = transitionColor;

            solidRect = imageObject.GetComponent<RectTransform>();
            solidRect.anchorMin = new Vector2(1f, 0f); // 화면 오른쪽 밖
            solidRect.anchorMax = new Vector2(2f, 1f); // 화면 너비만큼 확장
            solidRect.offsetMin = Vector2.zero;
            solidRect.offsetMax = Vector2.zero;
            solidRect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 그라데이션 UI 생성
        /// </summary>
        private void CreateGradientUI()
        {
            gradientImages = new Image[gradientSteps];
            gradientRects = new RectTransform[gradientSteps];

            for (int i = 0; i < gradientSteps; i++)
            {
                GameObject gradientObject = new GameObject($"GradientStep_{i}");
                gradientObject.transform.SetParent(transitionContainer.transform, false);

                gradientImages[i] = gradientObject.AddComponent<Image>();
                gradientRects[i] = gradientObject.GetComponent<RectTransform>();

                // 그라데이션 알파 계산 (오른쪽에서 왼쪽으로 점진적으로 투명해짐)
                float alpha = Mathf.Pow((float)(gradientSteps - i) / gradientSteps, gradientSmoothness);
                Color gradientColor = transitionColor;
                gradientColor.a = alpha;
                gradientImages[i].color = gradientColor;

                // 위치 설정 (각 스텝이 조금씩 오른쪽에 위치)
                float stepWidth = 1f / gradientSteps;
                gradientRects[i].anchorMin = new Vector2(1f + i * stepWidth, 0f);
                gradientRects[i].anchorMax = new Vector2(1f + (i + 2) * stepWidth, 1f); // 약간 겹치게
                gradientRects[i].offsetMin = Vector2.zero;
                gradientRects[i].offsetMax = Vector2.zero;
                gradientRects[i].pivot = new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 스트라이프 UI 생성
        /// </summary>
        private void CreateStripesUI()
        {
            stripeImages = new Image[stripeCount];
            stripeRects = new RectTransform[stripeCount];

            float stripeHeight = 1f / stripeCount;

            for (int i = 0; i < stripeCount; i++)
            {
                GameObject stripeObject = new GameObject($"Stripe_{i}");
                stripeObject.transform.SetParent(transitionContainer.transform, false);

                stripeImages[i] = stripeObject.AddComponent<Image>();
                stripeImages[i].color = transitionColor;

                stripeRects[i] = stripeObject.GetComponent<RectTransform>();

                // 세로로 나누어진 스트라이프 위치 설정
                float yMin = i * stripeHeight;
                float yMax = (i + 1) * stripeHeight;

                stripeRects[i].anchorMin = new Vector2(1f, yMin);
                stripeRects[i].anchorMax = new Vector2(2f, yMax);
                stripeRects[i].offsetMin = Vector2.zero;
                stripeRects[i].offsetMax = Vector2.zero;
                stripeRects[i].pivot = new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 모든 설정을 업데이트하고 UI를 재생성
        /// </summary>
        public void UpdateAllSettings(TransitionStyle style, float duration, Ease ease, Color color,
            int gradSteps, float gradSmoothness, int stripeCnt, float stripeDelay, float maxStagger = 0.3f)
        {
            transitionStyle = style;
            transitionDuration = duration;
            transitionEase = ease;
            transitionColor = color;
            gradientSteps = gradSteps;
            gradientSmoothness = gradSmoothness;
            stripeCount = stripeCnt;
            this.stripeDelay = stripeDelay;
            maxStaggerTime = maxStagger;

            // 기존 UI 제거 후 재생성
            if (transitionContainer != null)
            {
                DestroyImmediate(transitionContainer);
            }

            // 새로운 스타일로 UI 재생성
            CreateTransitionUI();

            if (debugMode)
                Debug.Log($"[SceneTransitionEffect] 설정 업데이트 완료: {style} 스타일");
        }

        /// <summary>
        /// 설정 업데이트 (레거시)
        /// </summary>
        public void UpdateTransitionSettings(float duration, Ease ease, Color color)
        {
            UpdateAllSettings(transitionStyle, duration, ease, color, gradientSteps, gradientSmoothness, stripeCount, stripeDelay);
        }
        #endregion

        #region Transition Animations
        /// <summary>
        /// 단색 전환 In
        /// </summary>
        private async Task StartSolidTransitionIn()
        {
            solidRect.anchorMin = new Vector2(1f, 0f);
            solidRect.anchorMax = new Vector2(2f, 1f);
            solidRect.offsetMin = Vector2.zero;
            solidRect.offsetMax = Vector2.zero;

            await solidRect.DOAnchorMin(new Vector2(0f, 0f), transitionDuration)
                .SetEase(transitionEase)
                .AsyncWaitForCompletion();

            solidRect.anchorMax = new Vector2(1f, 1f);
        }

        /// <summary>
        /// 단색 전환 Out
        /// </summary>
        private async Task StartSolidTransitionOut()
        {
            await solidRect.DOAnchorMax(new Vector2(0f, 1f), transitionDuration)
                .SetEase(transitionEase)
                .AsyncWaitForCompletion();
        }

        /// <summary>
        /// 그라데이션 전환 In
        /// </summary>
        private async Task StartGradientTransitionIn()
        {
            // 모든 그라데이션 스텝을 동시에 애니메이션
            var tasks = new Task[gradientSteps];

            for (int i = 0; i < gradientSteps; i++)
            {
                int index = i; // 클로저 문제 방지
                tasks[i] = gradientRects[index].DOAnchorMin(new Vector2(-index * 0.1f, 0f), transitionDuration)
                    .SetEase(transitionEase)
                    .AsyncWaitForCompletion();
            }

            await Task.WhenAll(tasks);

            // 마지막에 모든 요소가 화면을 덮도록 조정
            for (int i = 0; i < gradientSteps; i++)
            {
                gradientRects[i].anchorMax = new Vector2(1f + i * 0.1f, 1f);
            }
        }

        /// <summary>
        /// 그라데이션 전환 Out
        /// </summary>
        private async Task StartGradientTransitionOut()
        {
            var tasks = new Task[gradientSteps];

            for (int i = 0; i < gradientSteps; i++)
            {
                int index = i;
                tasks[i] = gradientRects[index].DOAnchorMax(new Vector2(-index * 0.1f, 1f), transitionDuration)
                    .SetEase(transitionEase)
                    .AsyncWaitForCompletion();
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 스트라이프 전환 In
        /// </summary>
        private async Task StartStripesTransitionIn()
        {
            if (stripeRects == null || stripeRects.Length == 0)
            {
                Debug.LogError("[SceneTransitionEffect] stripeRects가 초기화되지 않았습니다!");
                return;
            }

            var tasks = new Task[stripeCount];

            // 전체 시차 시간을 스트라이프 개수로 나누어 균등하게 분배
            float actualDelay = Mathf.Min(stripeDelay, maxStaggerTime / stripeCount);
            float individualAnimTime = transitionDuration - (actualDelay * (stripeCount - 1));
            individualAnimTime = Mathf.Max(individualAnimTime, 0.1f); // 최소 애니메이션 시간 보장

            for (int i = 0; i < stripeCount; i++)
            {
                int index = i;
                float delay = i * actualDelay;

                tasks[i] = AnimateStripeIn(index, delay, individualAnimTime);
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 개별 스트라이프 In 애니메이션
        /// </summary>
        private async Task AnimateStripeIn(int index, float delay, float animationTime)
        {
            if (delay > 0)
                await Task.Delay(Mathf.RoundToInt(delay * 1000));

            if (stripeRects[index] != null)
            {
                await stripeRects[index].DOAnchorMin(new Vector2(0f, stripeRects[index].anchorMin.y), animationTime)
                    .SetEase(transitionEase)
                    .AsyncWaitForCompletion();

                stripeRects[index].anchorMax = new Vector2(1f, stripeRects[index].anchorMax.y);
            }
        }

        /// <summary>
        /// 개별 스트라이프 In 애니메이션 (레거시 - 기본 지속시간 사용)
        /// </summary>
        private async Task AnimateStripeIn(int index, float delay)
        {
            await AnimateStripeIn(index, delay, transitionDuration);
        }

        /// <summary>
        /// 스트라이프 전환 Out
        /// </summary>
        private async Task StartStripesTransitionOut()
        {
            if (stripeRects == null || stripeRects.Length == 0)
            {
                Debug.LogError("[SceneTransitionEffect] stripeRects가 초기화되지 않았습니다!");
                return;
            }

            var tasks = new Task[stripeCount];

            // 전체 시차 시간을 스트라이프 개수로 나누어 균등하게 분배
            float actualDelay = Mathf.Min(stripeDelay, maxStaggerTime / stripeCount);
            float individualAnimTime = transitionDuration - (actualDelay * (stripeCount - 1));
            individualAnimTime = Mathf.Max(individualAnimTime, 0.1f); // 최소 애니메이션 시간 보장

            for (int i = 0; i < stripeCount; i++)
            {
                int index = i;
                float delay = i * actualDelay;

                tasks[i] = AnimateStripeOut(index, delay, individualAnimTime);
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 개별 스트라이프 Out 애니메이션
        /// </summary>
        private async Task AnimateStripeOut(int index, float delay, float animationTime)
        {
            if (delay > 0)
                await Task.Delay(Mathf.RoundToInt(delay * 1000));

            if (stripeRects[index] != null)
            {
                await stripeRects[index].DOAnchorMax(new Vector2(0f, stripeRects[index].anchorMax.y), animationTime)
                    .SetEase(transitionEase)
                    .AsyncWaitForCompletion();
            }
        }

        /// <summary>
        /// 개별 스트라이프 Out 애니메이션 (레거시 - 기본 지속시간 사용)
        /// </summary>
        private async Task AnimateStripeOut(int index, float delay)
        {
            await AnimateStripeOut(index, delay, transitionDuration);
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 전환 효과 테스트 (에디터용)
        /// </summary>
        [ContextMenu("Test Transition In")]
        public async void TestTransitionIn()
        {
            await StartTransitionIn();
        }

        /// <summary>
        /// 전환 걷어내기 테스트 (에디터용)
        /// </summary>
        [ContextMenu("Test Transition Out")]
        public async void TestTransitionOut()
        {
            await StartTransitionOut();
        }

        /// <summary>
        /// 전체 전환 테스트 (에디터용)
        /// </summary>
        [ContextMenu("Test Full Transition")]
        public async void TestFullTransition()
        {
            await StartTransitionIn();
            await Task.Delay(1000); // 1초 대기
            await StartTransitionOut();
        }
        #endregion
    }
}
