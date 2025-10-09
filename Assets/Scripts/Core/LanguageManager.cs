using UnityEngine;
using UnityEngine.Localization.Settings;
using System;
using System.Collections;

namespace Maglin.Core
{
    /// <summary>
    /// 언어 설정을 관리하는 매니저
    /// </summary>
    public class LanguageManager : MonoBehaviour
    {
        public static LanguageManager Instance { get; private set; }

        [Header("Language Settings")]
        [SerializeField] private bool debugMode = true;

        // 지원하는 언어 목록
        public enum SupportedLanguage
        {
            Korean = 0,
            English = 1
        }

        // 현재 선택된 언어
        private SupportedLanguage currentLanguage = SupportedLanguage.Korean;

        // 언어 변경 이벤트
        public static event Action<SupportedLanguage> OnLanguageChanged;

        // 언어 표시 이름 배열
        private readonly string[] languageDisplayNames = new string[]
        {
            "한국어",
            "English"
        };

        // Unity Localization과의 매핑을 위한 Locale 코드
        private readonly string[] localeIdentifiers = new string[]
        {
            "ko-KR", // Korean
            "en"  // English
        };

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeLanguageManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 언어 매니저 초기화
        /// </summary>
        private void InitializeLanguageManager()
        {
            // PlayerPrefs에서 저장된 언어 설정 로드
            LoadLanguageSettings();

            // Unity Localization 초기 설정
            StartCoroutine(InitializeLocalization());

            if (debugMode)
                Debug.Log($"[LanguageManager] 초기화 완료 - 현재 언어: {GetCurrentLanguageDisplayName()}");
        }

        /// <summary>
        /// Unity Localization 초기 설정
        /// </summary>
        private IEnumerator InitializeLocalization()
        {
            // Localization Settings가 초기화될 때까지 대기
            yield return LocalizationSettings.InitializationOperation;

            // 현재 설정된 언어로 Locale 설정
            string localeCode = localeIdentifiers[(int)currentLanguage];
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);

            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;

                if (debugMode)
                    Debug.Log($"[LanguageManager] 초기 Locale 설정: {locale.Identifier}");
            }
        }

        /// <summary>
        /// 저장된 언어 설정 로드
        /// </summary>
        private void LoadLanguageSettings()
        {
            int savedLanguage = PlayerPrefs.GetInt("GameLanguage", (int)SupportedLanguage.Korean);
            currentLanguage = (SupportedLanguage)savedLanguage;

            if (debugMode)
                Debug.Log($"[LanguageManager] 저장된 언어 설정 로드: {GetCurrentLanguageDisplayName()}");
        }

        /// <summary>
        /// 언어 설정 저장
        /// </summary>
        private void SaveLanguageSettings()
        {
            PlayerPrefs.SetInt("GameLanguage", (int)currentLanguage);
            PlayerPrefs.Save();

            if (debugMode)
                Debug.Log($"[LanguageManager] 언어 설정 저장: {GetCurrentLanguageDisplayName()}");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 현재 언어 가져오기
        /// </summary>
        public SupportedLanguage GetCurrentLanguage()
        {
            return currentLanguage;
        }

        /// <summary>
        /// 현재 언어의 표시 이름 가져오기
        /// </summary>
        public string GetCurrentLanguageDisplayName()
        {
            return languageDisplayNames[(int)currentLanguage];
        }

        /// <summary>
        /// 언어 설정
        /// </summary>
        public void SetLanguage(SupportedLanguage language)
        {
            if (currentLanguage != language)
            {
                currentLanguage = language;
                SaveLanguageSettings();

                if (debugMode)
                    Debug.Log($"[LanguageManager] 언어 변경: {GetCurrentLanguageDisplayName()}");

                // Unity Localization 시스템에 언어 변경 적용
                StartCoroutine(ApplyLocalizationChange());

                // 언어 변경 이벤트 발생
                OnLanguageChanged?.Invoke(currentLanguage);
            }
        }

        /// <summary>
        /// Unity Localization 시스템에 언어 변경 적용
        /// </summary>
        private IEnumerator ApplyLocalizationChange()
        {
            // Localization Settings가 초기화될 때까지 대기
            yield return LocalizationSettings.InitializationOperation;

            string localeCode = localeIdentifiers[(int)currentLanguage];

            // 해당 Locale 찾기
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);

            if (locale != null)
            {
                // Locale 변경
                LocalizationSettings.SelectedLocale = locale;

                if (debugMode)
                    Debug.Log($"[LanguageManager] Unity Localization Locale 변경: {locale.Identifier}");
            }
            else
            {
                Debug.LogError($"[LanguageManager] Locale '{localeCode}'를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// 다음 언어로 변경
        /// </summary>
        public void SetNextLanguage()
        {
            int nextLanguageIndex = ((int)currentLanguage + 1) % languageDisplayNames.Length;
            SetLanguage((SupportedLanguage)nextLanguageIndex);
        }

        /// <summary>
        /// 이전 언어로 변경
        /// </summary>
        public void SetPreviousLanguage()
        {
            int prevLanguageIndex = ((int)currentLanguage - 1 + languageDisplayNames.Length) % languageDisplayNames.Length;
            SetLanguage((SupportedLanguage)prevLanguageIndex);
        }

        /// <summary>
        /// 지원하는 언어 개수 가져오기
        /// </summary>
        public int GetSupportedLanguageCount()
        {
            return languageDisplayNames.Length;
        }

        /// <summary>
        /// 특정 인덱스의 언어 표시 이름 가져오기
        /// </summary>
        public string GetLanguageDisplayName(int index)
        {
            if (index >= 0 && index < languageDisplayNames.Length)
                return languageDisplayNames[index];
            return "Unknown";
        }

        /// <summary>
        /// 특정 언어의 표시 이름 가져오기
        /// </summary>
        public string GetLanguageDisplayName(SupportedLanguage language)
        {
            return GetLanguageDisplayName((int)language);
        }
        #endregion

        #region Debug
        [ContextMenu("Switch to Korean")]
        public void SwitchToKorean()
        {
            SetLanguage(SupportedLanguage.Korean);
        }

        [ContextMenu("Switch to English")]
        public void SwitchToEnglish()
        {
            SetLanguage(SupportedLanguage.English);
        }

        [ContextMenu("Test Next Language")]
        public void TestNextLanguage()
        {
            SetNextLanguage();
        }

        [ContextMenu("Test Previous Language")]
        public void TestPreviousLanguage()
        {
            SetPreviousLanguage();
        }

        [ContextMenu("Print Current Language")]
        public void PrintCurrentLanguage()
        {
            Debug.Log($"현재 언어: {GetCurrentLanguageDisplayName()} ({currentLanguage})");
        }
        #endregion
    }
}
