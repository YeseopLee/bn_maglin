using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Collections;

namespace Core
{
    /// <summary>
    /// 씬 전환과 페이드 효과 관리
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("페이드 UI")]
        [SerializeField] private CanvasGroup fadePanel;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("설정")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private Color fadeColor = Color.black;
        
        public static SceneTransitionManager Instance { get; private set; }
        
        // 이벤트
        public System.Action<string> OnSceneTransitionStarted;
        public System.Action<string> OnSceneTransitionCompleted;
        public System.Action OnFadeInStarted;
        public System.Action OnFadeInCompleted;
        public System.Action OnFadeOutStarted;
        public System.Action OnFadeOutCompleted;
        
        private bool isTransitioning = false;
        private string currentSceneName;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFadePanel();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            currentSceneName = SceneManager.GetActiveScene().name;
        }
        
        private void InitializeFadePanel()
        {
            if (fadePanel != null)
            {
                fadePanel.alpha = 0f;
                fadePanel.blocksRaycasts = false;
                fadePanel.interactable = false;
                
                // 페이드 패널 색상 설정
                var image = fadePanel.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    image.color = fadeColor;
                }
            }
        }
        
        /// <summary>
        /// 페이드 인 효과 (화면이 어두워짐)
        /// </summary>
        /// <param name="duration">페이드 지속 시간</param>
        public async Task FadeIn(float duration = -1f)
        {
            if (fadePanel == null)
            {
                Debug.LogWarning("SceneTransitionManager: fadePanel이 설정되지 않았습니다.");
                return;
            }
            
            if (duration < 0f) duration = fadeDuration;
            
            OnFadeInStarted?.Invoke();
            
            fadePanel.blocksRaycasts = true;
            fadePanel.interactable = false;
            
            float timer = 0f;
            float startAlpha = fadePanel.alpha;
            
            while (timer < duration)
            {
                timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = timer / duration;
                float curveValue = fadeCurve.Evaluate(normalizedTime);
                fadePanel.alpha = Mathf.Lerp(startAlpha, 1f, curveValue);
                await Task.Yield();
            }
            
            fadePanel.alpha = 1f;
            OnFadeInCompleted?.Invoke();
        }
        
        /// <summary>
        /// 페이드 아웃 효과 (화면이 밝아짐)
        /// </summary>
        /// <param name="duration">페이드 지속 시간</param>
        public async Task FadeOut(float duration = -1f)
        {
            if (fadePanel == null)
            {
                Debug.LogWarning("SceneTransitionManager: fadePanel이 설정되지 않았습니다.");
                return;
            }
            
            if (duration < 0f) duration = fadeDuration;
            
            OnFadeOutStarted?.Invoke();
            
            float timer = 0f;
            float startAlpha = fadePanel.alpha;
            
            while (timer < duration)
            {
                timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = timer / duration;
                float curveValue = fadeCurve.Evaluate(normalizedTime);
                fadePanel.alpha = Mathf.Lerp(startAlpha, 0f, curveValue);
                await Task.Yield();
            }
            
            fadePanel.alpha = 0f;
            fadePanel.blocksRaycasts = false;
            fadePanel.interactable = false;
            
            OnFadeOutCompleted?.Invoke();
        }
        
        /// <summary>
        /// 씬 전환 (페이드 효과 포함)
        /// </summary>
        /// <param name="sceneName">전환할 씬 이름</param>
        /// <param name="fadeInDuration">페이드 인 시간</param>
        /// <param name="fadeOutDuration">페이드 아웃 시간</param>
        public async Task TransitionToScene(string sceneName, float fadeInDuration = -1f, float fadeOutDuration = -1f)
        {
            if (isTransitioning)
            {
                Debug.LogWarning($"SceneTransitionManager: 이미 씬 전환 중입니다. ({sceneName})");
                return;
            }
            
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("SceneTransitionManager: 씬 이름이 비어있습니다.");
                return;
            }
            
            isTransitioning = true;
            OnSceneTransitionStarted?.Invoke(sceneName);
            
            try
            {
                // 1. 페이드 인
                await FadeIn(fadeInDuration);
                
                // 2. 씬 로드
                var loadOperation = SceneManager.LoadSceneAsync(sceneName);
                
                while (!loadOperation.isDone)
                {
                    await Task.Yield();
                }
                
                // 잠깐 대기 (씬 초기화 시간)
                await Task.Delay(100);
                
                // 3. 페이드 아웃
                await FadeOut(fadeOutDuration);
                
                currentSceneName = sceneName;
                OnSceneTransitionCompleted?.Invoke(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SceneTransitionManager: 씬 전환 중 오류 발생 - {e.Message}");
            }
            finally
            {
                isTransitioning = false;
            }
        }
        
        /// <summary>
        /// 즉시 페이드 인 (애니메이션 없음)
        /// </summary>
        public void InstantFadeIn()
        {
            if (fadePanel != null)
            {
                fadePanel.alpha = 1f;
                fadePanel.blocksRaycasts = true;
                fadePanel.interactable = false;
            }
        }
        
        /// <summary>
        /// 즉시 페이드 아웃 (애니메이션 없음)
        /// </summary>
        public void InstantFadeOut()
        {
            if (fadePanel != null)
            {
                fadePanel.alpha = 0f;
                fadePanel.blocksRaycasts = false;
                fadePanel.interactable = false;
            }
        }
        
        /// <summary>
        /// 현재 전환 중인지 확인
        /// </summary>
        public bool IsTransitioning => isTransitioning;
        
        /// <summary>
        /// 현재 씬 이름
        /// </summary>
        public string CurrentSceneName => currentSceneName;
        
        /// <summary>
        /// 페이드 지속 시간 설정
        /// </summary>
        /// <param name="duration">새 지속 시간</param>
        public void SetFadeDuration(float duration)
        {
            fadeDuration = Mathf.Max(0.1f, duration);
        }
        
        /// <summary>
        /// 페이드 색상 설정
        /// </summary>
        /// <param name="color">새 색상</param>
        public void SetFadeColor(Color color)
        {
            fadeColor = color;
            if (fadePanel != null)
            {
                var image = fadePanel.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    image.color = color;
                }
            }
        }
        
        /// <summary>
        /// 전환 시스템을 강제로 리셋합니다 (디버그용)
        /// </summary>
        [ContextMenu("Force Reset Transition")]
        public void ForceResetTransition()
        {
            isTransitioning = false;
            InstantFadeOut();
            currentSceneName = SceneManager.GetActiveScene().name;
        }
    }
} 