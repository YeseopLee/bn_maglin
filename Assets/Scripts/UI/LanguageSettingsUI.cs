using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// 언어 설정 UI를 관리하는 컨트롤러
    /// </summary>
    public class LanguageSettingsUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private TextMeshProUGUI languageDisplayText;
        [SerializeField] private Button closeButton;

        [Header("Settings")]
        [SerializeField] private bool debugMode = true;

        #region Unity Events
        private void Start()
        {
            InitializeUI();
        }

        private void OnEnable()
        {
            // 언어 변경 이벤트 구독
            LanguageManager.OnLanguageChanged += OnLanguageChanged;

            // UI 업데이트
            UpdateLanguageDisplay();
        }

        private void OnDisable()
        {
            // 언어 변경 이벤트 구독 해제
            LanguageManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 정리
            if (leftArrowButton != null)
                leftArrowButton.onClick.RemoveAllListeners();
            if (rightArrowButton != null)
                rightArrowButton.onClick.RemoveAllListeners();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 버튼 자동 찾기 (필요한 경우)
            if (leftArrowButton == null)
                leftArrowButton = transform.Find("LanguageSection/LeftArrow")?.GetComponent<Button>();
            if (rightArrowButton == null)
                rightArrowButton = transform.Find("LanguageSection/RightArrow")?.GetComponent<Button>();
            if (languageDisplayText == null)
                languageDisplayText = transform.Find("LanguageSection/LanguageText")?.GetComponent<TextMeshProUGUI>();
            if (closeButton == null)
                closeButton = transform.Find("CloseButton")?.GetComponent<Button>();

            // 버튼 이벤트 연결
            if (leftArrowButton != null)
            {
                leftArrowButton.onClick.AddListener(OnLeftArrowClicked);
                if (debugMode)
                    Debug.Log("[LanguageSettingsUI] 왼쪽 화살표 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogError("[LanguageSettingsUI] 왼쪽 화살표 버튼을 찾을 수 없습니다!");
            }

            if (rightArrowButton != null)
            {
                rightArrowButton.onClick.AddListener(OnRightArrowClicked);
                if (debugMode)
                    Debug.Log("[LanguageSettingsUI] 오른쪽 화살표 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogError("[LanguageSettingsUI] 오른쪽 화살표 버튼을 찾을 수 없습니다!");
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
                if (debugMode)
                    Debug.Log("[LanguageSettingsUI] 닫기 버튼 이벤트 연결 완료");
            }

            // 초기 언어 표시 업데이트
            UpdateLanguageDisplay();

            if (debugMode)
                Debug.Log("[LanguageSettingsUI] UI 초기화 완료");
        }
        #endregion

        #region UI Updates
        /// <summary>
        /// 언어 표시 업데이트
        /// </summary>
        private void UpdateLanguageDisplay()
        {
            if (LanguageManager.Instance != null && languageDisplayText != null)
            {
                string currentLanguageName = LanguageManager.Instance.GetCurrentLanguageDisplayName();
                languageDisplayText.text = currentLanguageName;

                if (debugMode)
                    Debug.Log($"[LanguageSettingsUI] 언어 표시 업데이트: {currentLanguageName}");
            }
        }
        #endregion

        #region Button Events
        /// <summary>
        /// 왼쪽 화살표 버튼 클릭 (이전 언어)
        /// </summary>
        private void OnLeftArrowClicked()
        {
            if (debugMode)
                Debug.Log("[LanguageSettingsUI] 왼쪽 화살표 클릭 - 이전 언어로 변경");

            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetPreviousLanguage();
            }
            else
            {
                Debug.LogError("[LanguageSettingsUI] LanguageManager를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// 오른쪽 화살표 버튼 클릭 (다음 언어)
        /// </summary>
        private void OnRightArrowClicked()
        {
            if (debugMode)
                Debug.Log("[LanguageSettingsUI] 오른쪽 화살표 클릭 - 다음 언어로 변경");

            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetNextLanguage();
            }
            else
            {
                Debug.LogError("[LanguageSettingsUI] LanguageManager를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void OnCloseButtonClicked()
        {
            if (debugMode)
                Debug.Log("[LanguageSettingsUI] 설정창 닫기");

            // 설정 오버레이 비활성화
            gameObject.SetActive(false);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 언어 변경 이벤트 핸들러
        /// </summary>
        private void OnLanguageChanged(LanguageManager.SupportedLanguage newLanguage)
        {
            if (debugMode)
                Debug.Log($"[LanguageSettingsUI] 언어 변경 이벤트 수신: {newLanguage}");

            UpdateLanguageDisplay();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 설정창 열기
        /// </summary>
        public void OpenSettings()
        {
            gameObject.SetActive(true);
            UpdateLanguageDisplay();

            if (debugMode)
                Debug.Log("[LanguageSettingsUI] 설정창 열림");
        }

        /// <summary>
        /// 설정창 닫기
        /// </summary>
        public void CloseSettings()
        {
            gameObject.SetActive(false);

            if (debugMode)
                Debug.Log("[LanguageSettingsUI] 설정창 닫힘");
        }
        #endregion

        #region Debug
        [ContextMenu("Test Left Arrow")]
        public void TestLeftArrow()
        {
            OnLeftArrowClicked();
        }

        [ContextMenu("Test Right Arrow")]
        public void TestRightArrow()
        {
            OnRightArrowClicked();
        }

        [ContextMenu("Update Display")]
        public void TestUpdateDisplay()
        {
            UpdateLanguageDisplay();
        }
        #endregion
    }
}
