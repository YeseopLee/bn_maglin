using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// SettingOverlay Prefab의 컨트롤러
    /// UI 컴포넌트 참조를 관리
    /// </summary>
    public class SettingOverlayController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private TMP_Text languageDisplayText;
        [SerializeField] private Button closeButton;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        private void Start()
        {
            // 자동으로 컴포넌트 찾기 (Inspector에서 설정하지 않은 경우)
            if (leftArrowButton == null || rightArrowButton == null ||
                languageDisplayText == null || closeButton == null)
            {
                FindUIComponents();
            }

            SetupButtons();
        }

        /// <summary>
        /// UI 컴포넌트 자동 찾기
        /// </summary>
        private void FindUIComponents()
        {
            if (leftArrowButton == null)
            {
                var leftButton = transform.Find("ContentPanel/LanguagePanel/Button");
                if (leftButton != null)
                    leftArrowButton = leftButton.GetComponent<Button>();
            }

            if (rightArrowButton == null)
            {
                var rightButton = transform.Find("ContentPanel/LanguagePanel/Button (1)");
                if (rightButton != null)
                    rightArrowButton = rightButton.GetComponent<Button>();
            }

            if (languageDisplayText == null)
            {
                var displayText = transform.Find("ContentPanel/LanguagePanel/CurrentLanguage");
                if (displayText != null)
                    languageDisplayText = displayText.GetComponent<TMP_Text>();
            }

            if (closeButton == null)
            {
                var close = transform.Find("TitlePanel/Button");
                if (close != null)
                    closeButton = close.GetComponent<Button>();
            }

            if (debugMode)
            {
                Debug.Log($"[SettingOverlayController] UI 컴포넌트 찾기 완료: " +
                    $"Left={leftArrowButton != null}, Right={rightArrowButton != null}, " +
                    $"Text={languageDisplayText != null}, Close={closeButton != null}");
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtons()
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
                closeButton.onClick.AddListener(OnClose);
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
        /// 이전 언어 버튼 클릭
        /// </summary>
        private void OnPreviousLanguage()
        {
            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetPreviousLanguage();

                if (debugMode)
                    Debug.Log("[SettingOverlayController] 이전 언어로 변경");
            }
        }

        /// <summary>
        /// 다음 언어 버튼 클릭
        /// </summary>
        private void OnNextLanguage()
        {
            if (LanguageManager.Instance != null)
            {
                LanguageManager.Instance.SetNextLanguage();

                if (debugMode)
                    Debug.Log("[SettingOverlayController] 다음 언어로 변경");
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void OnClose()
        {
            if (SettingOverlayManager.Instance != null)
            {
                SettingOverlayManager.Instance.CloseOverlay();
            }
            else
            {
                // SettingOverlayManager가 없으면 직접 비활성화
                gameObject.SetActive(false);
            }

            if (debugMode)
                Debug.Log("[SettingOverlayController] 닫기 버튼 클릭");
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

        #region Public Getters for SettingOverlayManager
        public Button GetLeftArrowButton() => leftArrowButton;
        public Button GetRightArrowButton() => rightArrowButton;
        public TMP_Text GetLanguageDisplayText() => languageDisplayText;
        public Button GetCloseButton() => closeButton;
        #endregion
    }
}

