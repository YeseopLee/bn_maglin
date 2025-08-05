using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections;

namespace Core
{
    /// <summary>
    /// 로딩 화면과 초기화 중 UI 블록 관리
    /// </summary>
    public class LoadingManager : MonoBehaviour
    {
        [Header("UI 프리팹")]
        [SerializeField] private GameObject loadingUIPrefab; // 로딩 UI 프리팹 (null이면 씬에서 자동으로 찾음)
        
        [Header("설정")]
        [SerializeField] private float fadeSpeed = 2f;
        [SerializeField] private bool enableProgressBar = true;
        [SerializeField] private bool enableSpinner = true;
        [SerializeField] private bool debugMode = false;
        
        [Header("로딩 메시지 설정")]
        [SerializeField] private string defaultLoadingMessage = "로딩 중...";
        [SerializeField] private string gamePreparationMessage = "게임 준비 중...";
        [SerializeField] private string gameManagerCheckMessage = "게임 매니저 확인 중...";
        [SerializeField] private string gameDataInitMessage = "게임 데이터 초기화 중...";
        [SerializeField] private string battlePreparationMessage = "전투 준비 중...";
        [SerializeField] private string gameStartMessage = "게임 시작!";
        [SerializeField] private string battleInitMessage = "전투 준비 중...";
        [SerializeField] private string inputBlockMessage = "입력 차단 중...";
        [SerializeField] private string fieldSetupMessage = "전투 필드 설정 중...";
        [SerializeField] private string playerInitMessage = "플레이어 상태 초기화 중...";
        [SerializeField] private string enemySpawnMessage = "적 등장 중...";
        [SerializeField] private string deckPrepMessage = "덱 준비 중...";
        [SerializeField] private string uiSetupMessage = "UI 설정 중...";
        [SerializeField] private string battleReadyMessage = "전투 시작 준비 완료!";
        [SerializeField] private string inputEnableMessage = "입력 활성화";
        
        public static LoadingManager Instance { get; private set; }
        
        // 이벤트
        public System.Action OnLoadingStarted;
        public System.Action OnLoadingCompleted;
        
        private bool isLoading = false;
        private Coroutine spinnerCoroutine;
        private GameObject currentLoadingUI; // 현재 씬의 로딩 UI 인스턴스
        
        // 런타임에 찾은 UI 컴포넌트들
        private CanvasGroup loadingPanel;
        private TextMeshProUGUI loadingText;
        private Slider progressBar;
        private GameObject loadingSpinner;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 씬 변경 이벤트 구독
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                
                InitializeLoadingUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void OnDestroy()
        {
            // 씬 변경 이벤트 구독 해제
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        /// <summary>
        /// 새 씬이 로드될 때 호출되는 이벤트 핸들러
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // 기존 로딩 UI 정리
            ClearCurrentLoadingUI();
            
            // 새 씬에서 로딩 UI 찾기 또는 생성
            SetupLoadingUIForCurrentScene();
        }
        
        private void InitializeLoadingUI()
        {
            SetupLoadingUIForCurrentScene();
        }
        
        /// <summary>
        /// 현재 씬에서 로딩 UI를 찾거나 생성합니다
        /// </summary>
        private void SetupLoadingUIForCurrentScene()
        {
            // 1. 먼저 씬에서 기존 로딩 UI를 찾아봅니다
            FindExistingLoadingUI();
            
            // 2. 찾지 못했고 프리팹이 있다면 생성합니다
            if (loadingPanel == null && loadingUIPrefab != null)
            {
                CreateLoadingUIFromPrefab();
            }
            
            // 3. 로딩 UI 초기화
            ConfigureLoadingUI();
        }
        
        /// <summary>
        /// 씬에서 기존 로딩 UI를 찾습니다
        /// </summary>
        private void FindExistingLoadingUI()
        {
            // LoadingCanvas 이름으로 찾기
            GameObject loadingCanvas = GameObject.Find("LoadingCanvas");
            
            if (loadingCanvas != null)
            {
                currentLoadingUI = loadingCanvas;
                ExtractUIComponents(loadingCanvas);
                
                if (debugMode)
                    Debug.Log("LoadingManager: 씬에서 기존 LoadingCanvas를 찾았습니다.");
            }
        }
        
        /// <summary>
        /// 프리팹에서 로딩 UI를 생성합니다
        /// </summary>
        private void CreateLoadingUIFromPrefab()
        {
            currentLoadingUI = Instantiate(loadingUIPrefab);
            currentLoadingUI.name = "LoadingCanvas (Generated)";
            
            ExtractUIComponents(currentLoadingUI);
            
            if (debugMode)
                Debug.Log("LoadingManager: 프리팹에서 로딩 UI를 생성했습니다.");
        }
        
        /// <summary>
        /// UI 오브젝트에서 필요한 컴포넌트들을 추출합니다
        /// </summary>
        private void ExtractUIComponents(GameObject uiRoot)
        {
            // CanvasGroup 찾기 (페이드 효과용)
            loadingPanel = uiRoot.GetComponent<CanvasGroup>();
            if (loadingPanel == null)
            {
                loadingPanel = uiRoot.GetComponentInChildren<CanvasGroup>();
            }
            
            // 텍스트 컴포넌트 찾기
            loadingText = uiRoot.GetComponentInChildren<TextMeshProUGUI>();
            
            // 진행률 바 찾기
            progressBar = uiRoot.GetComponentInChildren<Slider>();
            
            // 스피너 찾기 (특정 이름으로)
            Transform spinnerTransform = uiRoot.transform.Find("LoadingContainer/LoadingSpinner");
            if (spinnerTransform == null)
            {
                // 이름이 다를 수 있으니 하위에서 "Spinner" 포함하는 오브젝트 찾기
                Transform[] children = uiRoot.GetComponentsInChildren<Transform>();
                foreach (Transform child in children)
                {
                    if (child.name.ToLower().Contains("spinner"))
                    {
                        spinnerTransform = child;
                        break;
                    }
                }
            }
            
            if (spinnerTransform != null)
            {
                loadingSpinner = spinnerTransform.gameObject;
            }
            
            // 컴포넌트 찾기 결과 로그
            if (debugMode)
            {
                Debug.Log($"LoadingManager UI 컴포넌트 추출 결과:");
                Debug.Log($"- LoadingPanel: {(loadingPanel != null ? "✓" : "✗")}");
                Debug.Log($"- LoadingText: {(loadingText != null ? "✓" : "✗")}");
                Debug.Log($"- ProgressBar: {(progressBar != null ? "✓" : "✗")}");
                Debug.Log($"- LoadingSpinner: {(loadingSpinner != null ? "✓" : "✗")}");
            }
        }
        
        /// <summary>
        /// 로딩 UI를 구성합니다
        /// </summary>
        private void ConfigureLoadingUI()
        {
            if (loadingPanel != null)
            {
                loadingPanel.alpha = 0f;
                loadingPanel.blocksRaycasts = false;
                loadingPanel.interactable = false;
            }
            
            if (progressBar != null && !enableProgressBar)
            {
                progressBar.gameObject.SetActive(false);
            }
            
            if (loadingSpinner != null && !enableSpinner)
            {
                loadingSpinner.SetActive(false);
            }
        }
        
        /// <summary>
        /// 현재 로딩 UI를 정리합니다
        /// </summary>
        private void ClearCurrentLoadingUI()
        {
            // 스피너 정지
            StopSpinner();
            
            // 생성된 UI라면 제거 (씬의 원래 UI는 유지)
            if (currentLoadingUI != null && currentLoadingUI.name.Contains("(Generated)"))
            {
                DestroyImmediate(currentLoadingUI);
            }
            
            // 참조 초기화
            loadingPanel = null;
            loadingText = null;
            progressBar = null;
            loadingSpinner = null;
            currentLoadingUI = null;
        }
        
        /// <summary>
        /// 로딩 화면을 표시합니다
        /// </summary>
        /// <param name="message">로딩 메시지 (null이면 기본 메시지 사용)</param>
        /// <param name="showProgress">진행률 바 표시 여부</param>
        public async Task ShowLoading(string message = null, bool showProgress = false)
        {
            if (isLoading) return;
            
            isLoading = true;
            OnLoadingStarted?.Invoke();
            
            // 로딩 UI가 없다면 다시 설정 시도
            if (loadingPanel == null)
            {
                SetupLoadingUIForCurrentScene();
            }
            
            // 여전히 없다면 경고 출력 후 리턴
            if (loadingPanel == null)
            {
                Debug.LogWarning("LoadingManager: 로딩 UI를 찾을 수 없습니다. 프리팹을 설정하거나 씬에 LoadingCanvas를 추가해주세요.");
                isLoading = false;
                return;
            }
            
            // UI 설정
            loadingPanel.blocksRaycasts = true;
            loadingPanel.interactable = false;
            
            if (loadingText != null)
            {
                loadingText.text = string.IsNullOrEmpty(message) ? defaultLoadingMessage : message;
            }
            
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(showProgress && enableProgressBar);
                progressBar.value = 0f;
            }
            
            if (loadingSpinner != null && enableSpinner)
            {
                loadingSpinner.SetActive(true);
                StartSpinner();
            }
            
            // 페이드 인
            float timer = 0f;
            while (timer < 1f)
            {
                timer += Time.unscaledDeltaTime * fadeSpeed;
                loadingPanel.alpha = Mathf.Lerp(0f, 1f, timer);
                await Task.Yield();
            }
            
            loadingPanel.alpha = 1f;
        }
        
        /// <summary>
        /// 로딩 화면을 숨깁니다
        /// </summary>
        public async Task HideLoading()
        {
            if (!isLoading) return;
            
            if (loadingPanel == null) return;
            
            // 스피너 정지
            StopSpinner();
            
            // 페이드 아웃
            float timer = 0f;
            while (timer < 1f)
            {
                timer += Time.unscaledDeltaTime * fadeSpeed;
                loadingPanel.alpha = Mathf.Lerp(1f, 0f, timer);
                await Task.Yield();
            }
            
            loadingPanel.alpha = 0f;
            loadingPanel.blocksRaycasts = false;
            loadingPanel.interactable = false;
            
            if (loadingSpinner != null)
            {
                loadingSpinner.SetActive(false);
            }
            
            isLoading = false;
            OnLoadingCompleted?.Invoke();
        }
        
        /// <summary>
        /// 로딩 진행률과 메시지를 업데이트합니다
        /// </summary>
        /// <param name="progress">진행률 (0-1)</param>
        /// <param name="message">로딩 메시지</param>
        public void UpdateProgress(float progress, string message = null)
        {
            if (!isLoading) return;
            
            if (progressBar != null && progressBar.gameObject.activeInHierarchy)
            {
                progressBar.value = Mathf.Clamp01(progress);
            }
            
            if (loadingText != null && !string.IsNullOrEmpty(message))
            {
                loadingText.text = message;
            }
        }
        
        /// <summary>
        /// 로딩 메시지만 업데이트합니다
        /// </summary>
        /// <param name="message">새 메시지</param>
        public void UpdateMessage(string message)
        {
            if (loadingText != null)
            {
                loadingText.text = message;
            }
        }
        
        /// <summary>
        /// 현재 로딩 중인지 확인합니다
        /// </summary>
        public bool IsLoading => isLoading;
        
        #region 미리 정의된 메시지 메소드들
        /// <summary>
        /// 게임 준비 로딩 표시
        /// </summary>
        public async Task ShowGamePreparationLoading(bool showProgress = true)
        {
            await ShowLoading(gamePreparationMessage, showProgress);
        }
        
        /// <summary>
        /// 전투 초기화 로딩 표시
        /// </summary>
        public async Task ShowBattleInitLoading(bool showProgress = true)
        {
            await ShowLoading(battleInitMessage, showProgress);
        }
        
        /// <summary>
        /// 게임 매니저 확인 중 메시지 업데이트
        /// </summary>
        public void UpdateToGameManagerCheck(float progress = 0.2f)
        {
            UpdateProgress(progress, gameManagerCheckMessage);
        }
        
        /// <summary>
        /// 게임 데이터 초기화 중 메시지 업데이트
        /// </summary>
        public void UpdateToGameDataInit(float progress = 0.5f)
        {
            UpdateProgress(progress, gameDataInitMessage);
        }
        
        /// <summary>
        /// 전투 준비 중 메시지 업데이트
        /// </summary>
        public void UpdateToBattlePreparation(float progress = 0.8f)
        {
            UpdateProgress(progress, battlePreparationMessage);
        }
        
        /// <summary>
        /// 게임 시작 메시지 업데이트
        /// </summary>
        public void UpdateToGameStart(float progress = 1.0f)
        {
            UpdateProgress(progress, gameStartMessage);
        }
        
        /// <summary>
        /// 입력 차단 중 메시지 업데이트
        /// </summary>
        public void UpdateToInputBlock(float progress = 0.1f)
        {
            UpdateProgress(progress, inputBlockMessage);
        }
        
        /// <summary>
        /// 전투 필드 설정 중 메시지 업데이트
        /// </summary>
        public void UpdateToFieldSetup(float progress = 0.15f)
        {
            UpdateProgress(progress, fieldSetupMessage);
        }
        
        /// <summary>
        /// 플레이어 상태 초기화 중 메시지 업데이트
        /// </summary>
        public void UpdateToPlayerInit(float progress = 0.3f)
        {
            UpdateProgress(progress, playerInitMessage);
        }
        
        /// <summary>
        /// 적 등장 중 메시지 업데이트
        /// </summary>
        public void UpdateToEnemySpawn(float progress = 0.5f)
        {
            UpdateProgress(progress, enemySpawnMessage);
        }
        
        /// <summary>
        /// 덱 준비 중 메시지 업데이트
        /// </summary>
        public void UpdateToDeckPrep(float progress = 0.7f)
        {
            UpdateProgress(progress, deckPrepMessage);
        }
        
        /// <summary>
        /// UI 설정 중 메시지 업데이트
        /// </summary>
        public void UpdateToUISetup(float progress = 0.85f)
        {
            UpdateProgress(progress, uiSetupMessage);
        }
        
        /// <summary>
        /// 전투 시작 준비 완료 메시지 업데이트
        /// </summary>
        public void UpdateToBattleReady(float progress = 0.95f)
        {
            UpdateProgress(progress, battleReadyMessage);
        }
        
        /// <summary>
        /// 입력 활성화 메시지 업데이트
        /// </summary>
        public void UpdateToInputEnable(float progress = 1.0f)
        {
            UpdateProgress(progress, inputEnableMessage);
        }
        #endregion
        
        /// <summary>
        /// 로딩 스피너 시작
        /// </summary>
        private void StartSpinner()
        {
            if (loadingSpinner != null && spinnerCoroutine == null)
            {
                spinnerCoroutine = StartCoroutine(SpinLoadingIcon());
            }
        }
        
        /// <summary>
        /// 로딩 스피너 정지
        /// </summary>
        private void StopSpinner()
        {
            if (spinnerCoroutine != null)
            {
                StopCoroutine(spinnerCoroutine);
                spinnerCoroutine = null;
            }
        }
        
        /// <summary>
        /// 스피너 회전 코루틴
        /// </summary>
        private IEnumerator SpinLoadingIcon()
        {
            while (true)
            {
                loadingSpinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
                yield return null;
            }
        }
        
        /// <summary>
        /// 로딩 시스템을 강제로 리셋합니다 (디버그용)
        /// </summary>
        [ContextMenu("Force Reset Loading")]
        public void ForceResetLoading()
        {
            isLoading = false;
            StopSpinner();
            
            if (loadingPanel != null)
            {
                loadingPanel.alpha = 0f;
                loadingPanel.blocksRaycasts = false;
                loadingPanel.interactable = false;
            }
            
            if (loadingSpinner != null)
            {
                loadingSpinner.SetActive(false);
            }
        }
    }
} 