using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Threading.Tasks;
using DG.Tweening;

namespace Maglin.Core
{
    /// <summary>
    /// Additive 방식을 이용한 자연스러운 씬 전환 매니저
    /// 1번씬 -> 로딩씬 -> 2번씬 순으로 진행하며 부드러운 전환 효과 제공
    /// </summary>
    public class AdditiveSceneLoader : MonoBehaviour
    {
        #region Singleton Implementation
        private static AdditiveSceneLoader _instance;

        public static AdditiveSceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AdditiveSceneLoader>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AdditiveSceneLoader");
                        _instance = go.AddComponent<AdditiveSceneLoader>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

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
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 씬 전환 시작 이벤트
        /// </summary>
        public static event System.Action<string> OnSceneTransitionStarted;

        /// <summary>
        /// 로딩 씬 표시 완료 이벤트
        /// </summary>
        public static event System.Action OnLoadingSceneShown;

        /// <summary>
        /// 타겟 씬 로딩 완료 이벤트
        /// </summary>
        public static event System.Action<string> OnTargetSceneLoaded;

        /// <summary>
        /// 씬 전환 완료 이벤트
        /// </summary>
        public static event System.Action<string> OnSceneTransitionCompleted;
        #endregion

        #region Fields
        [Header("로딩 설정")]
        [SerializeField] private string loadingSceneName = "LoadingScene";
        [SerializeField] private float preLoadingDelay = 0.1f; // 전환 시작 전 딜레이
        [SerializeField] private float postLoadingDelay = 0.5f; // 타겟 씬 로딩 후 딜레이 (애니메이션 준비 시간)

        [Header("전환 효과 설정")]
        [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.Gradient;
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private Ease transitionEase = Ease.OutExpo;
        [SerializeField] private Color transitionColor = Color.black;

        [Header("그라데이션 설정")]
        [SerializeField] private int gradientSteps = 20;
        [SerializeField] private float gradientSmoothness = 2f;

        [Header("스트라이프 설정")]
        [SerializeField] private int stripeCount = 8;
        [SerializeField] private float stripeDelay = 0.05f;
        [SerializeField] private float maxStaggerTime = 0.3f;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private bool isTransitioning = false;
        private string currentTargetScene = "";
        private SceneTransitionEffect transitionEffect;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (debugMode)
                Debug.Log("[AdditiveSceneLoader] AdditiveSceneLoader 초기화 시작");

            // SceneTransitionEffect 컴포넌트 찾기 또는 생성
            transitionEffect = GetComponent<SceneTransitionEffect>();
            if (transitionEffect == null)
            {
                transitionEffect = gameObject.AddComponent<SceneTransitionEffect>();
            }

            // 전환 효과 설정 적용
            ApplyTransitionSettings();

            if (debugMode)
                Debug.Log("[AdditiveSceneLoader] AdditiveSceneLoader 초기화 완료");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 씬 전환 실행 (메인 메서드)
        /// </summary>
        /// <param name="targetSceneName">이동할 씬 이름</param>
        /// <param name="additionalDelay">추가 대기 시간 (선택사항)</param>
        public async Task LoadSceneWithTransition(string targetSceneName, float additionalDelay = 0f)
        {
            if (isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning($"[AdditiveSceneLoader] 이미 씬 전환 중입니다. 요청 무시: {targetSceneName}");
                return;
            }

            string currentSceneName = SceneManager.GetActiveScene().name;
            bool isSameScene = currentSceneName == targetSceneName;

            if (debugMode)
            {
                if (isSameScene)
                    Debug.Log($"[AdditiveSceneLoader] 같은 씬으로 전환: {currentSceneName} → {targetSceneName} (새로고침)");
                else
                    Debug.Log($"[AdditiveSceneLoader] 씬 전환 시작: {currentSceneName} → {targetSceneName}");
            }

            isTransitioning = true;
            currentTargetScene = targetSceneName;

            OnSceneTransitionStarted?.Invoke(targetSceneName);

            try
            {
                // 같은 씬으로의 전환인 경우 전환 효과 리셋
                if (isSameScene)
                {
                    transitionEffect.ForceReset();
                    await Task.Delay(50); // 리셋 후 짧은 딜레이
                }

                // 1. 전환 효과 시작 (오른쪽에서 검은색으로 덮기)
                await transitionEffect.StartTransitionIn();

                // 2. 로딩 씬 로드 (Additive)
                await LoadLoadingScene();

                // 3. 타겟 씬 로드 (Additive)
                await LoadTargetScene(targetSceneName);

                // 4. 추가 대기 시간 (애니메이션 준비)
                if (additionalDelay > 0f)
                {
                    await Task.Delay((int)(additionalDelay * 1000));
                }

                // 5. 기존 씬 언로드
                await UnloadPreviousScene();

                // 6. 전환 효과 완료 (왼쪽부터 검은색 걷어내기)
                await transitionEffect.StartTransitionOut();

                // 7. 로딩 씬 언로드
                await UnloadLoadingScene();

                OnSceneTransitionCompleted?.Invoke(targetSceneName);

                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] 씬 전환 완료: {targetSceneName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AdditiveSceneLoader] 씬 전환 중 오류 발생: {e.Message}");
            }
            finally
            {
                isTransitioning = false;
                currentTargetScene = "";
            }
        }

        /// <summary>
        /// 현재 전환 중인지 확인
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        /// <summary>
        /// 현재 타겟 씬 이름
        /// </summary>
        public string CurrentTargetScene => currentTargetScene;

        /// <summary>
        /// 전환 스타일 변경
        /// </summary>
        public void SetTransitionStyle(TransitionStyle style)
        {
            transitionStyle = style;
            ApplyTransitionSettings();
        }

        /// <summary>
        /// 전환 설정 변경
        /// </summary>
        public void SetTransitionSettings(TransitionStyle style, float duration, Ease ease, Color color)
        {
            transitionStyle = style;
            transitionDuration = duration;
            transitionEase = ease;
            transitionColor = color;
            ApplyTransitionSettings();
        }

        /// <summary>
        /// 그라데이션 설정 변경
        /// </summary>
        public void SetGradientSettings(int steps, float smoothness)
        {
            gradientSteps = steps;
            gradientSmoothness = smoothness;
            if (transitionStyle == TransitionStyle.Gradient)
                ApplyTransitionSettings();
        }

        /// <summary>
        /// 스트라이프 설정 변경
        /// </summary>
        public void SetStripeSettings(int count, float delay)
        {
            stripeCount = count;
            stripeDelay = delay;
            if (transitionStyle == TransitionStyle.Stripes)
                ApplyTransitionSettings();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 로딩 씬 로드
        /// </summary>
        private async Task LoadLoadingScene()
        {
            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 로딩 씬 로드 시작: {loadingSceneName}");

            // 로딩 씬이 이미 로드되어 있는지 확인
            Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);
            if (loadingScene.isLoaded)
            {
                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] 로딩 씬이 이미 로드되어 있음: {loadingSceneName}");
                return;
            }

            var loadOperation = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);

            while (!loadOperation.isDone)
            {
                await Task.Yield();
            }

            // 로딩 씬 로드 후 AudioListener 중복 확인
            Scene currentLoadingScene = SceneManager.GetSceneByName(loadingSceneName);
            if (currentLoadingScene.isLoaded)
            {
                CheckAndDisableLoadingSceneAudioListener(currentLoadingScene);
            }

            OnLoadingSceneShown?.Invoke();

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 로딩 씬 로드 완료: {loadingSceneName}");
        }

        /// <summary>
        /// 타겟 씬 로드
        /// </summary>
        private async Task LoadTargetScene(string sceneName)
        {
            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 타겟 씬 로드 시작: {sceneName}");

            // 짧은 딜레이 (로딩 씬이 완전히 표시되도록)
            await Task.Delay((int)(preLoadingDelay * 1000));

            // 같은 이름의 씬이 이미 존재하는지 확인
            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            bool isSameSceneReload = existingScene.isLoaded;

            if (isSameSceneReload)
            {
                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] 같은 씬 새로고침 모드: {sceneName}");

                // 같은 씬을 다시 로드하는 경우 특별 처리
                // 기존 씬을 먼저 언로드한 후 새로 로드
                var unloadOperation = SceneManager.UnloadSceneAsync(existingScene);
                while (!unloadOperation.isDone)
                {
                    await Task.Yield();
                }

                // 언로드 완료까지 약간 대기
                await Task.Delay(100);
            }

            var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            while (!loadOperation.isDone)
            {
                await Task.Yield();
            }

            // 타겟 씬을 활성 씬으로 설정
            Scene targetScene = SceneManager.GetSceneByName(sceneName);
            if (targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);

                // AudioListener 중복 문제 해결
                CleanupDuplicateAudioListeners(targetScene);
            }

            // 씬 로딩 후 추가 대기 (게임 오브젝트들이 초기화될 시간)
            await Task.Delay((int)(postLoadingDelay * 1000));

            OnTargetSceneLoaded?.Invoke(sceneName);

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 타겟 씬 로드 및 초기화 완료: {sceneName}");
        }

        /// <summary>
        /// 이전 씬 언로드 (로딩 씬과 타겟 씬 제외)
        /// </summary>
        private async Task UnloadPreviousScene()
        {
            int sceneCount = SceneManager.sceneCount;
            Scene targetScene = SceneManager.GetSceneByName(currentTargetScene);
            Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);

            bool foundSceneToUnload = false;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                // 타겟 씬과 로딩 씬은 제외
                if (scene == targetScene || scene == loadingScene) continue;

                // DontDestroyOnLoad 씬 제외
                if (scene.name == "DontDestroyOnLoad") continue;

                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] 이전 씬 언로드: {scene.name}");

                var unloadOperation = SceneManager.UnloadSceneAsync(scene);
                while (!unloadOperation.isDone)
                {
                    await Task.Yield();
                }
                foundSceneToUnload = true;
                break; // 한 번에 하나씩 언로드
            }

            if (!foundSceneToUnload && debugMode)
            {
                Debug.Log("[AdditiveSceneLoader] 언로드할 이전 씬이 없습니다 (같은 씬 새로고침일 가능성)");
            }
        }

        /// <summary>
        /// 로딩 씬 언로드
        /// </summary>
        private async Task UnloadLoadingScene()
        {
            Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);
            if (!loadingScene.isLoaded)
            {
                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] 로딩 씬이 이미 언로드됨: {loadingSceneName}");
                return;
            }

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 로딩 씬 언로드 시작: {loadingSceneName}");

            var unloadOperation = SceneManager.UnloadSceneAsync(loadingScene);
            while (!unloadOperation.isDone)
            {
                await Task.Yield();
            }

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 로딩 씬 언로드 완료: {loadingSceneName}");
        }
        #endregion

        /// <summary>
        /// 로딩 씬의 AudioListener 비활성화 (있다면)
        /// </summary>
        private void CheckAndDisableLoadingSceneAudioListener(Scene loadingScene)
        {
            GameObject[] rootObjects = loadingScene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                AudioListener listener = rootObj.GetComponentInChildren<AudioListener>();
                if (listener != null)
                {
                    if (debugMode)
                        Debug.Log($"[AdditiveSceneLoader] 로딩 씬의 AudioListener 비활성화: {listener.name}");

                    listener.enabled = false;
                }
            }
        }

        /// <summary>
        /// AudioListener 중복 문제 해결
        /// </summary>
        private void CleanupDuplicateAudioListeners(Scene targetScene)
        {
            // 모든 씬에서 AudioListener 찾기
            AudioListener[] allListeners = FindObjectsOfType<AudioListener>();

            if (allListeners.Length <= 1)
            {
                if (debugMode)
                    Debug.Log($"[AdditiveSceneLoader] AudioListener 수: {allListeners.Length} (정상)");
                return;
            }

            if (debugMode)
                Debug.LogWarning($"[AdditiveSceneLoader] AudioListener 중복 발견: {allListeners.Length}개");

            // 타겟 씬의 AudioListener 찾기
            AudioListener targetSceneListener = null;
            GameObject[] rootObjects = targetScene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                AudioListener listener = rootObj.GetComponentInChildren<AudioListener>();
                if (listener != null)
                {
                    targetSceneListener = listener;
                    break;
                }
            }

            // 타겟 씬의 AudioListener만 유지하고 나머지는 비활성화
            foreach (AudioListener listener in allListeners)
            {
                if (listener != targetSceneListener)
                {
                    if (debugMode)
                        Debug.Log($"[AdditiveSceneLoader] AudioListener 비활성화: {listener.name} (씬: {listener.gameObject.scene.name})");

                    listener.enabled = false;
                }
            }

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] AudioListener 정리 완료. 활성: {targetSceneListener?.name ?? "없음"}");
        }

        /// <summary>
        /// SceneTransitionEffect에 현재 설정 적용
        /// </summary>
        private void ApplyTransitionSettings()
        {
            if (transitionEffect == null) return;

            // 직접 메서드 호출로 설정 적용
            transitionEffect.UpdateAllSettings(
                transitionStyle,
                transitionDuration,
                transitionEase,
                transitionColor,
                gradientSteps,
                gradientSmoothness,
                stripeCount,
                stripeDelay,
                maxStaggerTime
            );

            if (debugMode)
                Debug.Log($"[AdditiveSceneLoader] 전환 효과 설정 적용: {transitionStyle} 스타일");
        }

        #region Utility Methods
        /// <summary>
        /// 특정 씬이 로드되어 있는지 확인
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.isLoaded;
        }

        /// <summary>
        /// 현재 로드된 모든 씬 정보 출력 (디버그용)
        /// </summary>
        [ContextMenu("Debug Scene Info")]
        public void DebugSceneInfo()
        {
            Debug.Log("=== 현재 로드된 씬 정보 ===");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                Debug.Log($"씬 {i}: {scene.name} (활성: {scene == SceneManager.GetActiveScene()})");
            }
            Debug.Log($"활성 씬: {SceneManager.GetActiveScene().name}");
            Debug.Log($"전환 중: {isTransitioning}");
        }
        #endregion
    }
}
