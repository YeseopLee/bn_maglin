using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Maglin.Core
{
    /// <summary>
    /// 언어 설정 및 폰트 자동 변경을 관리하는 매니저
    /// </summary>
    public class LanguageManager : MonoBehaviour
    {
        public static LanguageManager Instance { get; private set; }

        [Header("Language Settings")]
        [SerializeField] private bool debugMode = true;

        [Header("Font Asset Settings")]
        [SerializeField] private string fontAssetTableName = "FontAssetTable";
        [SerializeField] private string fontAssetEntryKey = "New Entry";
        [SerializeField] private bool autoRegisterTextComponents = true;

        // 캐시된 폰트 에셋
        private Dictionary<string, TMP_FontAsset> cachedFontAssets = new Dictionary<string, TMP_FontAsset>();

        // 씬에 있는 모든 TextMeshPro 컴포넌트 추적
        private HashSet<TMP_Text> registeredTextComponents = new HashSet<TMP_Text>();

        // 지원하는 언어 목록
        public enum SupportedLanguage
        {
            Korean = 0,
            English = 1,
            ChineseSimplified = 2
        }

        // 현재 선택된 언어
        private SupportedLanguage currentLanguage = SupportedLanguage.Korean;

        // 언어 변경 이벤트
        public static event Action<SupportedLanguage> OnLanguageChanged;

        // 언어 표시 이름 배열
        private readonly string[] languageDisplayNames = new string[]
        {
            "한국어",
            "English",
            "简体中文"
        };

        // Unity Localization과의 매핑을 위한 Locale 코드
        private readonly string[] localeIdentifiers = new string[]
        {
            "ko-KR", // Korean
            "en",    // English
            "zh-CN"  // Chinese Simplified
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

        private void OnDestroy()
        {
            // 필요시 정리 작업
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

            // 초기 폰트 설정
            if (autoRegisterTextComponents)
            {
                yield return new WaitForSeconds(0.5f);
                RegisterAllTextComponentsInScene();
                yield return ApplyCurrentLanguageFont();
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

                // 폰트 변경 적용
                StartCoroutine(ApplyCurrentLanguageFont());

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

        [ContextMenu("Switch to Chinese")]
        public void SwitchToChinese()
        {
            SetLanguage(SupportedLanguage.ChineseSimplified);
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

        [ContextMenu("Register All Text Components")]
        public void DebugRegisterAll()
        {
            RegisterAllTextComponentsInScene();
        }

        [ContextMenu("Apply Current Language Font")]
        public void DebugApplyFont()
        {
            StartCoroutine(ApplyCurrentLanguageFont());
        }

        [ContextMenu("Print Registered Components Count")]
        public void DebugPrintCount()
        {
            registeredTextComponents.RemoveWhere(text => text == null);
            Debug.Log($"[LanguageManager] 등록된 TextMeshPro 컴포넌트 수: {registeredTextComponents.Count}");
        }

        [ContextMenu("Clear Font Cache")]
        public void DebugClearCache()
        {
            cachedFontAssets.Clear();
            Debug.Log("[LanguageManager] 폰트 캐시 클리어");
        }

        [ContextMenu("Refresh All Fonts")]
        public void DebugRefreshFonts()
        {
            RefreshAllFonts();
        }
        #endregion

        #region Font Management
        /// <summary>
        /// 현재 선택된 언어에 맞는 폰트를 모든 TextMeshPro 컴포넌트에 적용
        /// </summary>
        private IEnumerator ApplyCurrentLanguageFont()
        {
            yield return LocalizationSettings.InitializationOperation;

            TMP_FontAsset fontAsset = null;
            yield return LoadFontAssetForCurrentLocale((asset) => fontAsset = asset);

            if (fontAsset != null)
            {
                ApplyFontToAllRegisteredText(fontAsset);
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[LanguageManager] 현재 언어에 맞는 폰트를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 현재 Locale에 맞는 폰트 에셋 로드
        /// </summary>
        private IEnumerator LoadFontAssetForCurrentLocale(System.Action<TMP_FontAsset> onComplete)
        {
            var currentLocale = LocalizationSettings.SelectedLocale;
            if (currentLocale == null)
            {
                Debug.LogError("[LanguageManager] 선택된 Locale이 없습니다!");
                onComplete?.Invoke(null);
                yield break;
            }

            string localeCode = currentLocale.Identifier.Code;

            if (cachedFontAssets.TryGetValue(localeCode, out TMP_FontAsset cachedFont))
            {
                if (debugMode)
                    Debug.Log($"[LanguageManager] 캐시된 폰트 사용: {localeCode}");
                onComplete?.Invoke(cachedFont);
                yield break;
            }

            var loadOperation = LocalizationSettings.AssetDatabase.GetTableAsync(fontAssetTableName);
            yield return loadOperation;

            if (loadOperation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                var assetTable = loadOperation.Result as AssetTable;

                if (assetTable != null)
                {
                    var entry = assetTable.GetEntry(fontAssetEntryKey);

                    if (entry != null)
                    {
                        var fontLoadOperation = assetTable.GetAssetAsync<TMP_FontAsset>(entry.KeyId);
                        yield return fontLoadOperation;

                        if (fontLoadOperation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        {
                            TMP_FontAsset fontAsset = fontLoadOperation.Result;
                            cachedFontAssets[localeCode] = fontAsset;

                            if (debugMode)
                                Debug.Log($"[LanguageManager] 폰트 로드 성공: {localeCode} - {fontAsset.name}");

                            onComplete?.Invoke(fontAsset);
                        }
                        else
                        {
                            Debug.LogError($"[LanguageManager] 폰트 에셋 로드 실패: {fontAssetEntryKey}");
                            onComplete?.Invoke(null);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[LanguageManager] 폰트 테이블에서 '{fontAssetEntryKey}' 엔트리를 찾을 수 없습니다!");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[LanguageManager] Asset Table '{fontAssetTableName}'을 AssetTable로 캐스팅할 수 없습니다!");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[LanguageManager] Asset Table '{fontAssetTableName}' 로드 실패!");
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// 모든 등록된 TextMeshPro 컴포넌트에 폰트 적용
        /// </summary>
        private void ApplyFontToAllRegisteredText(TMP_FontAsset fontAsset)
        {
            registeredTextComponents.RemoveWhere(text => text == null);

            int appliedCount = 0;
            foreach (var textComponent in registeredTextComponents)
            {
                if (textComponent != null)
                {
                    textComponent.font = fontAsset;
                    appliedCount++;
                }
            }

            if (debugMode)
                Debug.Log($"[LanguageManager] {appliedCount}개의 TextMeshPro 컴포넌트에 폰트 적용: {fontAsset.name}");

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 현재 씬의 모든 TextMeshPro 컴포넌트를 찾아서 등록
        /// </summary>
        public void RegisterAllTextComponentsInScene()
        {
            TMP_Text[] allTextComponents = FindObjectsOfType<TMP_Text>(true);

            int newRegistrations = 0;
            foreach (var textComponent in allTextComponents)
            {
                if (RegisterTextComponent(textComponent))
                {
                    newRegistrations++;
                }
            }

            if (debugMode)
                Debug.Log($"[LanguageManager] {newRegistrations}개의 새로운 TextMeshPro 컴포넌트 등록 (총 {registeredTextComponents.Count}개)");
        }

        /// <summary>
        /// 특정 TextMeshPro 컴포넌트를 등록
        /// </summary>
        public bool RegisterTextComponent(TMP_Text textComponent)
        {
            if (textComponent == null)
                return false;

            return registeredTextComponents.Add(textComponent);
        }

        /// <summary>
        /// 특정 TextMeshPro 컴포넌트의 등록을 해제
        /// </summary>
        public void UnregisterTextComponent(TMP_Text textComponent)
        {
            if (textComponent == null)
                return;

            registeredTextComponents.Remove(textComponent);
        }

        /// <summary>
        /// 새로운 씬이 로드될 때 호출 (씬 전환 시 사용)
        /// </summary>
        public void OnSceneLoaded()
        {
            if (autoRegisterTextComponents)
            {
                StartCoroutine(OnSceneLoadedCoroutine());
            }
        }

        private IEnumerator OnSceneLoadedCoroutine()
        {
            yield return new WaitForSeconds(0.5f);
            RegisterAllTextComponentsInScene();
            yield return ApplyCurrentLanguageFont();
        }

        /// <summary>
        /// 수동으로 현재 언어의 폰트를 다시 적용
        /// </summary>
        public void RefreshAllFonts()
        {
            if (autoRegisterTextComponents)
            {
                RegisterAllTextComponentsInScene();
            }
            StartCoroutine(ApplyCurrentLanguageFont());
        }
        #endregion
    }
}
