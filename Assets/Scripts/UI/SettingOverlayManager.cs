using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// 설정 오버레이를 관리하는 매니저
    /// ESC 키 입력으로 어디서든 설정창을 열 수 있음
    /// </summary>
    public class SettingOverlayManager : MonoBehaviour
    {
        public static SettingOverlayManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject settingOverlayPrefab;
        private GameObject settingOverlayInstance;

        [Header("UI Components")]
        private Button leftArrowButton;
        private Button rightArrowButton;
        private TMP_Text languageDisplayText;
        private Button closeButton;

        [Header("Settings")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        private bool isOverlayActive = false;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (debugMode)
                    Debug.Log("[SettingOverlayManager] 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeOverlay();
        }

        private void Update()
        {
            // ESC 키 입력 감지
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleOverlay();
            }
        }

        /// <summary>
        /// 오버레이 초기화
        /// </summary>
        private void InitializeOverlay()
        {
            // Prefab이 설정되지 않은 경우 Resources에서 로드 시도
            if (settingOverlayPrefab == null)
            {
                settingOverlayPrefab = Resources.Load<GameObject>("Prefabs/SettingOverlay");

                if (settingOverlayPrefab == null)
                {
                    Debug.LogError("[SettingOverlayManager] SettingOverlay Prefab을 찾을 수 없습니다!");
                    return;
                }
            }

            // 인스턴스 생성
            CreateOverlayInstance();
        }

        /// <summary>
        /// 오버레이 인스턴스 생성
        /// </summary>
        private void CreateOverlayInstance()
        {
            if (settingOverlayInstance != null)
                return;

            // Canvas 찾기
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                // Canvas가 없으면 생성
                GameObject canvasObj = new GameObject("SettingOverlayCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                DontDestroyOnLoad(canvasObj);

                if (debugMode)
                    Debug.Log("[SettingOverlayManager] 새 Canvas 생성");
            }

            // Prefab 인스턴스화
            settingOverlayInstance = Instantiate(settingOverlayPrefab, canvas.transform);
            settingOverlayInstance.name = "SettingOverlay";

            // DontDestroyOnLoad 설정 (Canvas가 DontDestroyOnLoad인 경우에만)
            if (canvas.gameObject.scene.name == "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(settingOverlayInstance);
            }

            // UI 컴포넌트 찾기
            FindUIComponents();

            // 버튼 이벤트 연결
            SetupButtonEvents();

            // 초기 상태: 비활성화
            settingOverlayInstance.SetActive(false);
            isOverlayActive = false;

            if (debugMode)
                Debug.Log("[SettingOverlayManager] SettingOverlay 인스턴스 생성 완료");
        }

        /// <summary>
        /// UI 컴포넌트 찾기
        /// </summary>
        private void FindUIComponents()
        {
            if (settingOverlayInstance == null)
                return;

            // SettingOverlayController가 있는지 확인
            var controller = settingOverlayInstance.GetComponent<SettingOverlayController>();
            if (controller != null)
            {
                // Controller에서 직접 참조 가져오기
                leftArrowButton = controller.GetLeftArrowButton();
                rightArrowButton = controller.GetRightArrowButton();
                languageDisplayText = controller.GetLanguageDisplayText();
                closeButton = controller.GetCloseButton();
            }
            else
            {
                // 수동으로 찾기
                leftArrowButton = settingOverlayInstance.transform.Find("ContentPanel/LanguagePanel/Button")?.GetComponent<Button>();
                rightArrowButton = settingOverlayInstance.transform.Find("ContentPanel/LanguagePanel/Button (1)")?.GetComponent<Button>();
                languageDisplayText = settingOverlayInstance.transform.Find("ContentPanel/LanguagePanel/CurrentLanguage")?.GetComponent<TMP_Text>();
                closeButton = settingOverlayInstance.transform.Find("TitlePanel/Button")?.GetComponent<Button>();
            }

            if (debugMode)
            {
                Debug.Log($"[SettingOverlayManager] UI 컴포넌트 찾기: " +
                    $"Left={leftArrowButton != null}, Right={rightArrowButton != null}, " +
                    $"Text={languageDisplayText != null}, Close={closeButton != null}");
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            if (leftArrowButton != null)
            {
                leftArrowButton.onClick.RemoveAllListeners();
                leftArrowButton.onClick.AddListener(OnPreviousLanguage);
            }

            if (rightArrowButton != null)
            {
                rightArrowButton.onClick.RemoveAllListeners();
                rightArrowButton.onClick.AddListener(OnNextLanguage);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseOverlay);
            }

            // 언어 변경 이벤트 구독
            LanguageManager.OnLanguageChanged += OnLanguageChanged;

            // 초기 언어 표시
            UpdateLanguageDisplay();
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            LanguageManager.OnLanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// 오버레이 토글
        /// </summary>
        public void ToggleOverlay()
        {
            if (isOverlayActive)
            {
                CloseOverlay();
            }
            else
            {
                OpenOverlay();
            }
        }

        /// <summary>
        /// 오버레이 열기
        /// </summary>
        public void OpenOverlay()
        {
            if (settingOverlayInstance == null)
            {
                CreateOverlayInstance();
            }

            if (settingOverlayInstance != null)
            {
                settingOverlayInstance.SetActive(true);
                isOverlayActive = true;

                // 언어 표시 업데이트
                UpdateLanguageDisplay();

                if (debugMode)
                    Debug.Log("[SettingOverlayManager] 설정 오버레이 열림");
            }
        }

        /// <summary>
        /// 오버레이 닫기
        /// </summary>
        public void CloseOverlay()
        {
            if (settingOverlayInstance != null)
            {
                settingOverlayInstance.SetActive(false);
                isOverlayActive = false;

                if (debugMode)
                    Debug.Log("[SettingOverlayManager] 설정 오버레이 닫힘");
            }
        }

        /// <summary>
        /// 이전 언어로 변경
        /// </summary>
        private void OnPreviousLanguage()
        {
            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetPreviousLanguage();
            }
        }

        /// <summary>
        /// 다음 언어로 변경
        /// </summary>
        private void OnNextLanguage()
        {
            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetNextLanguage();
            }
        }

        /// <summary>
        /// 언어 변경 이벤트 핸들러
        /// </summary>
        private void OnLanguageChanged(LanguageManager.SupportedLanguage newLanguage)
        {
            UpdateLanguageDisplay();
        }

        /// <summary>
        /// 언어 표시 업데이트
        /// </summary>
        private void UpdateLanguageDisplay()
        {
            if (languageDisplayText != null && LanguageManager.Instance != null)
            {
                languageDisplayText.text = LanguageManager.Instance.GetCurrentLanguageDisplayName();
            }
        }

        /// <summary>
        /// 현재 오버레이가 활성화 상태인지 확인
        /// </summary>
        public bool IsActive => isOverlayActive;

        #region Debug
        [ContextMenu("Open Overlay")]
        public void DebugOpenOverlay()
        {
            OpenOverlay();
        }

        [ContextMenu("Close Overlay")]
        public void DebugCloseOverlay()
        {
            CloseOverlay();
        }

        [ContextMenu("Toggle Overlay")]
        public void DebugToggleOverlay()
        {
            ToggleOverlay();
        }
        #endregion
    }
}

