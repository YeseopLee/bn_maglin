using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// UI 패널 타입
    /// </summary>
    public enum UIPanel
    {
        None,
        MainMenu,       // 메인 메뉴
        Battle,         // 전투 UI
        Shop,           // 상점 UI
        Event,          // 이벤트 UI
        GameOver,       // 게임 오버 UI
        Victory,        // 승리 UI
        Pause,          // 일시정지 UI
        Settings        // 설정 UI
    }

    /// <summary>
    /// UI 전환 타입
    /// </summary>
    public enum TransitionType
    {
        None,           // 전환 없음
        Fade,           // 페이드 인/아웃
        Slide,          // 슬라이드
        Scale,          // 크기 변화
        Custom          // 커스텀 전환
    }

    /// <summary>
    /// UI 패널 정보
    /// </summary>
    [System.Serializable]
    public class UIPanelInfo
    {
        [Header("패널 정보")]
        public UIPanel panelType;
        public GameObject panelObject;
        public CanvasGroup canvasGroup;

        [Header("전환 설정")]
        public TransitionType transitionType = TransitionType.Fade;
        public float transitionDuration = 0.3f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("설정")]
        public bool blockInput = true;      // 전환 중 입력 차단
        public bool hideOnStart = true;     // 시작 시 숨김
        public int sortingOrder = 0;        // 표시 순서
    }

    /// <summary>
    /// UI 화면 전환과 상태를 관리하는 매니저
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static UIManager _instance;

        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>();
                    // 자동 생성 제거됨 - 수동으로 씬에 배치해야 함
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// UI 패널 변경 이벤트 (이전 패널, 새 패널)
        /// </summary>
        public static event Action<UIPanel, UIPanel> OnPanelChanged;

        /// <summary>
        /// UI 전환 시작 이벤트
        /// </summary>
        public static event Action<UIPanel> OnTransitionStarted;

        /// <summary>
        /// UI 전환 완료 이벤트
        /// </summary>
        public static event Action<UIPanel> OnTransitionCompleted;

        // 로딩 이벤트 제거됨 (AdditiveSceneLoader 사용)
        #endregion

        #region Fields
        [Header("UI 패널들")]
        [SerializeField] private List<UIPanelInfo> panels = new List<UIPanelInfo>();

        // 로딩 관련 필드 제거됨 (새로운 AdditiveSceneLoader 사용)

        [Header("전환 설정")]
        [SerializeField] private float defaultTransitionDuration = 0.3f;
        [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool enableTransitionEffects = true;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 현재 상태
        private UIPanel currentPanel = UIPanel.None;
        private UIPanel previousPanel = UIPanel.None;
        private Stack<UIPanel> panelHistory = new Stack<UIPanel>();

        // 전환 관련
        private bool isTransitioning = false;
        private Coroutine currentTransition;

        // 로딩 관련 변수 제거됨

        // 패널 검색 캐시
        private Dictionary<UIPanel, UIPanelInfo> panelLookup = new Dictionary<UIPanel, UIPanelInfo>();

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 활성 패널
        /// </summary>
        public UIPanel CurrentPanel => currentPanel;

        /// <summary>
        /// 이전 패널
        /// </summary>
        public UIPanel PreviousPanel => previousPanel;

        /// <summary>
        /// UI 전환 중인지 여부
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        // IsLoading 프로퍼티 제거됨

        /// <summary>
        /// 패널 히스토리 깊이
        /// </summary>
        public int HistoryDepth => panelHistory.Count;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUIManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (_instance == this)
            {
                // 게임 시작 시 초기 패널 설정
                SetupInitialUI();
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
        /// UIManager 초기화
        /// </summary>
        private void InitializeUIManager()
        {
            if (debugMode)
                Debug.Log("[UIManager] UI 매니저 초기화 시작");

            // 패널 검색 캐시 구축
            BuildPanelLookup();

            // 이벤트 구독
            if (FloorManager.Instance != null)
            {
                FloorManager.OnGameStateChanged += OnGameStateChanged;
                FloorManager.OnGameInitialized += OnGameInitialized;
            }

            // 로딩 UI 초기화 제거됨

            isInitialized = true;

            if (debugMode)
                Debug.Log("[UIManager] UI 매니저 초기화 완료");
        }

        /// <summary>
        /// 패널 검색 캐시 구축
        /// </summary>
        private void BuildPanelLookup()
        {
            panelLookup.Clear();

            foreach (var panelInfo in panels)
            {
                if (panelInfo.panelObject != null)
                {
                    panelLookup[panelInfo.panelType] = panelInfo;

                    // CanvasGroup이 없으면 자동 추가
                    if (panelInfo.canvasGroup == null)
                    {
                        panelInfo.canvasGroup = panelInfo.panelObject.GetComponent<CanvasGroup>();
                        if (panelInfo.canvasGroup == null)
                        {
                            panelInfo.canvasGroup = panelInfo.panelObject.AddComponent<CanvasGroup>();
                        }
                    }

                    // 시작 시 숨김 처리
                    if (panelInfo.hideOnStart)
                    {
                        panelInfo.panelObject.SetActive(false);
                    }
                }
            }

            if (debugMode)
                Debug.Log($"[UIManager] 패널 캐시 구축 완료: {panelLookup.Count}개");
        }

        // InitializeLoadingUI 메서드 제거됨

        /// <summary>
        /// 게임 초기화 완료 시 호출
        /// </summary>
        private void OnGameInitialized()
        {
            if (debugMode)
                Debug.Log("[UIManager] 게임 초기화 완료 신호 받음");
        }

        /// <summary>
        /// 초기 UI 설정
        /// </summary>
        private void SetupInitialUI()
        {
            if (debugMode)
                Debug.Log("[UIManager] 초기 UI 설정");

            // 모든 패널 숨김
            HideAllPanels();

            // 메인 메뉴 표시
            ShowPanel(UIPanel.MainMenu, false);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 게임 상태 변경 시 호출
        /// </summary>
        private void OnGameStateChanged(GameState previousState, GameState newState)
        {
            if (debugMode)
                Debug.Log($"[UIManager] 게임 상태 변경: {previousState} -> {newState}");

            // 게임 상태에 따른 UI 패널 전환
            UIPanel targetPanel = GetPanelForGameState(newState);

            if (targetPanel != UIPanel.None && targetPanel != currentPanel)
            {
                ShowPanel(targetPanel);
            }
        }

        /// <summary>
        /// 게임 상태에 따른 UI 패널 반환
        /// </summary>
        private UIPanel GetPanelForGameState(GameState gameState)
        {
            switch (gameState)
            {
                case GameState.MainMenu:
                    return UIPanel.MainMenu;
                case GameState.Battle:
                    return UIPanel.Battle;
                case GameState.Shop:
                    return UIPanel.Shop;
                case GameState.Event:
                    return UIPanel.Event;
                case GameState.GameOver:
                    return UIPanel.GameOver;
                case GameState.Victory:
                    return UIPanel.Victory;
                default:
                    return UIPanel.None;
            }
        }
        #endregion

        #region Panel Management
        /// <summary>
        /// 패널 표시
        /// </summary>
        public void ShowPanel(UIPanel panel, bool addToHistory = true)
        {
            if (isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning("[UIManager] 전환 중에는 패널을 변경할 수 없습니다.");
                return;
            }

            if (panel == currentPanel)
            {
                if (debugMode)
                    Debug.LogWarning($"[UIManager] 이미 {panel} 패널이 활성화되어 있습니다.");
                return;
            }

            if (!panelLookup.ContainsKey(panel))
            {
                Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {panel}");
                return;
            }

            if (debugMode)
                Debug.Log($"[UIManager] 패널 전환: {currentPanel} -> {panel}");

            // 히스토리에 추가
            if (addToHistory && currentPanel != UIPanel.None)
            {
                panelHistory.Push(currentPanel);
            }

            // 전환 시작
            StartPanelTransition(panel);
        }

        /// <summary>
        /// 패널 숨김
        /// </summary>
        public void HidePanel(UIPanel panel)
        {
            if (!panelLookup.ContainsKey(panel))
            {
                Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {panel}");
                return;
            }

            var panelInfo = panelLookup[panel];
            if (panelInfo.panelObject != null)
            {
                panelInfo.panelObject.SetActive(false);
            }

            if (debugMode)
                Debug.Log($"[UIManager] 패널 숨김: {panel}");
        }

        /// <summary>
        /// 모든 패널 숨김
        /// </summary>
        public void HideAllPanels()
        {
            foreach (var panelInfo in panels)
            {
                if (panelInfo.panelObject != null)
                {
                    panelInfo.panelObject.SetActive(false);
                }
            }

            currentPanel = UIPanel.None;
            panelHistory.Clear();

            if (debugMode)
                Debug.Log("[UIManager] 모든 패널 숨김");
        }

        /// <summary>
        /// 이전 패널로 돌아가기
        /// </summary>
        public void GoBack()
        {
            if (panelHistory.Count > 0)
            {
                UIPanel previousPanel = panelHistory.Pop();
                ShowPanel(previousPanel, false);
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[UIManager] 돌아갈 패널이 없습니다.");
            }
        }

        /// <summary>
        /// 패널 전환 시작
        /// </summary>
        private void StartPanelTransition(UIPanel targetPanel)
        {
            if (currentTransition != null)
            {
                StopCoroutine(currentTransition);
            }

            currentTransition = StartCoroutine(TransitionToPanel(targetPanel));
        }

        /// <summary>
        /// 패널 전환 코루틴
        /// </summary>
        private IEnumerator TransitionToPanel(UIPanel targetPanel)
        {
            isTransitioning = true;
            OnTransitionStarted?.Invoke(targetPanel);

            var targetPanelInfo = panelLookup[targetPanel];
            UIPanelInfo currentPanelInfo = null;

            if (currentPanel != UIPanel.None && panelLookup.ContainsKey(currentPanel))
            {
                currentPanelInfo = panelLookup[currentPanel];
            }

            // 이전 패널 페이드 아웃
            if (currentPanelInfo != null && enableTransitionEffects)
            {
                yield return StartCoroutine(FadeOutPanel(currentPanelInfo));
            }
            else if (currentPanelInfo != null)
            {
                currentPanelInfo.panelObject.SetActive(false);
            }

            // 패널 상태 업데이트
            previousPanel = currentPanel;
            currentPanel = targetPanel;

            // 새 패널 활성화 및 페이드 인
            targetPanelInfo.panelObject.SetActive(true);

            if (enableTransitionEffects)
            {
                yield return StartCoroutine(FadeInPanel(targetPanelInfo));
            }

            // 전환 완료
            isTransitioning = false;
            OnTransitionCompleted?.Invoke(targetPanel);
            OnPanelChanged?.Invoke(previousPanel, currentPanel);

            if (debugMode)
                Debug.Log($"[UIManager] 패널 전환 완료: {targetPanel}");
        }

        /// <summary>
        /// 패널 페이드 인
        /// </summary>
        private IEnumerator FadeInPanel(UIPanelInfo panelInfo)
        {
            if (panelInfo.canvasGroup == null) yield break;

            float duration = panelInfo.transitionDuration > 0 ? panelInfo.transitionDuration : defaultTransitionDuration;
            float elapsed = 0f;

            panelInfo.canvasGroup.alpha = 0f;
            panelInfo.canvasGroup.interactable = false;
            panelInfo.canvasGroup.blocksRaycasts = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float curveValue = panelInfo.transitionCurve.Evaluate(progress);

                panelInfo.canvasGroup.alpha = curveValue;

                yield return null;
            }

            panelInfo.canvasGroup.alpha = 1f;
            panelInfo.canvasGroup.interactable = true;
            panelInfo.canvasGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// 패널 페이드 아웃
        /// </summary>
        private IEnumerator FadeOutPanel(UIPanelInfo panelInfo)
        {
            if (panelInfo.canvasGroup == null)
            {
                panelInfo.panelObject.SetActive(false);
                yield break;
            }

            float duration = panelInfo.transitionDuration > 0 ? panelInfo.transitionDuration : defaultTransitionDuration;
            float elapsed = 0f;

            panelInfo.canvasGroup.interactable = false;
            panelInfo.canvasGroup.blocksRaycasts = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float curveValue = 1f - panelInfo.transitionCurve.Evaluate(progress);

                panelInfo.canvasGroup.alpha = curveValue;

                yield return null;
            }

            panelInfo.canvasGroup.alpha = 0f;
            panelInfo.panelObject.SetActive(false);
        }
        #endregion

        // Loading System 전체 제거됨 (AdditiveSceneLoader 사용)

        #region Utility
        /// <summary>
        /// 특정 패널이 활성화되어 있는지 확인
        /// </summary>
        public bool IsPanelActive(UIPanel panel)
        {
            return currentPanel == panel;
        }

        /// <summary>
        /// 패널 정보 가져오기
        /// </summary>
        public UIPanelInfo GetPanelInfo(UIPanel panel)
        {
            return panelLookup.ContainsKey(panel) ? panelLookup[panel] : null;
        }

        /// <summary>
        /// 전환 효과 토글
        /// </summary>
        public void SetTransitionEffectsEnabled(bool enabled)
        {
            enableTransitionEffects = enabled;

            if (debugMode)
                Debug.Log($"[UIManager] 전환 효과: {(enabled ? "활성화" : "비활성화")}");
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== UIManager Debug Info ===");
            Debug.Log($"Current Panel: {currentPanel}");
            Debug.Log($"Previous Panel: {previousPanel}");
            Debug.Log($"Is Transitioning: {isTransitioning}");
            Debug.Log($"History Depth: {panelHistory.Count}");
            Debug.Log($"Registered Panels: {panelLookup.Count}");
            Debug.Log($"Transition Effects: {enableTransitionEffects}");
        }

        /// <summary>
        /// 강제 패널 전환 (디버그용)
        /// </summary>
        public void ForceShowPanel(UIPanel panel)
        {
            if (!debugMode)
            {
                Debug.LogWarning("[UIManager] ForceShowPanel은 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            ShowPanel(panel);
        }
        #endregion
    }
}