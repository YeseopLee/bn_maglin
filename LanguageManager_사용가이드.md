# LanguageManager 사용 가이드

## 개요
LanguageManager는 언어 설정 및 폰트 자동 변경을 통합 관리하는 매니저입니다.

## 주요 기능
- 다국어 지원 (한국어, 영어, 중국어 간체)
- 언어 변경 시 모든 TMP_Text 컴포넌트의 폰트 자동 변경
- 씬 전환 시 자동으로 새 씬의 TextMeshPro 컴포넌트 등록 및 폰트 적용
- 폰트 캐싱을 통한 성능 최적화
- Unity Localization 시스템과 완전 통합

## 설정 방법

### 1. FontAssetTable에 폰트 추가

Unity Editor에서:
1. `Window` → `Asset Management` → `Localization Tables` 메뉴 열기
2. `Asset Tables` 탭 선택
3. `FontAssetTable` 찾기
4. `New Entry` 항목에 각 언어별 폰트 할당:
   - **Korean (ko-KR)**: 한글 폰트 (예: NotoSansKR)
   - **English (en)**: 영문 폰트 (예: Roboto)
   - **Chinese Simplified (zh-CN)**: 중국어 폰트 (예: NotoSansSC)

### 2. 씬에 LanguageManager 추가

1. Hierarchy에서 빈 GameObject 생성 (이름: `LanguageManager`)
2. `LanguageManager` 컴포넌트 추가
3. Inspector에서 설정:
   - **Debug Mode**: 체크 (디버깅 시)
   - **Font Asset Table Name**: `FontAssetTable` (기본값)
   - **Font Asset Entry Key**: `New Entry` (기본값)
   - **Auto Register Text Components**: 체크 (자동 등록 활성화)

### 3. 테스트

#### Inspector 컨텍스트 메뉴 테스트
LanguageManager 컴포넌트에서 우클릭:

**언어 변경:**
- `Switch to Korean`: 한국어로 변경
- `Switch to English`: 영어로 변경
- `Switch to Chinese`: 중국어로 변경
- `Test Next Language`: 다음 언어로 순환
- `Test Previous Language`: 이전 언어로 순환

**폰트 관리:**
- `Register All Text Components`: 현재 씬의 모든 TextMeshPro 컴포넌트 등록
- `Apply Current Language Font`: 현재 언어의 폰트 적용
- `Print Registered Components Count`: 등록된 컴포넌트 수 출력
- `Clear Font Cache`: 폰트 캐시 클리어
- `Refresh All Fonts`: 모든 컴포넌트 재등록 및 폰트 재적용

**디버그:**
- `Print Current Language`: 현재 언어 정보 출력

## 작동 방식

### 자동 폰트 변경 프로세스

1. **초기화**
   - LanguageManager가 씬에 로드되면 자동으로 초기화
   - PlayerPrefs에서 저장된 언어 설정 로드
   - Unity Localization 시스템 초기화
   - 현재 씬의 모든 TextMeshPro 컴포넌트 자동 등록
   - 현재 언어에 맞는 폰트 적용

2. **언어 변경 시**
   - SetLanguage() 메서드 호출
   - Unity Localization Locale 변경
   - 새 언어에 맞는 폰트 에셋 로드 (캐시되어 있으면 캐시 사용)
   - 등록된 모든 TextMeshPro 컴포넌트에 새 폰트 적용
   - OnLanguageChanged 이벤트 발생

3. **씬 전환 시**
   - AdditiveSceneLoader 또는 SceneTransitionManager가 새 씬 로드
   - 씬 로드 완료 후 LanguageManager.OnSceneLoaded() 호출
   - 새 씬의 모든 TextMeshPro 컴포넌트 자동 등록
   - 현재 언어에 맞는 폰트 즉시 적용

### 컴포넌트 등록 관리

#### 자동 등록 (권장)
- `autoRegisterTextComponents`가 활성화되어 있으면:
  - 초기화 시 자동으로 모든 컴포넌트 등록
  - 씬 전환 시 자동으로 새 씬의 컴포넌트 등록

#### 수동 등록
코드에서 특정 TextMeshPro 컴포넌트만 등록하고 싶은 경우:

```csharp
using Maglin.Core;
using TMPro;

public class MyUIScript : MonoBehaviour
{
    [SerializeField] private TMP_Text myText;
    
    void Start()
    {
        // 특정 컴포넌트 등록
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.RegisterTextComponent(myText);
        }
    }
    
    void OnDestroy()
    {
        // 등록 해제
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.UnregisterTextComponent(myText);
        }
    }
}
```

## 언어 변경 API

### 코드에서 언어 변경

```csharp
using Maglin.Core;

public class SettingsMenu : MonoBehaviour
{
    public void OnKoreanButtonClicked()
    {
        LanguageManager.Instance.SetLanguage(LanguageManager.SupportedLanguage.Korean);
    }
    
    public void OnEnglishButtonClicked()
    {
        LanguageManager.Instance.SetLanguage(LanguageManager.SupportedLanguage.English);
    }
    
    public void OnChineseButtonClicked()
    {
        LanguageManager.Instance.SetLanguage(LanguageManager.SupportedLanguage.ChineseSimplified);
    }
    
    public void OnNextLanguageButtonClicked()
    {
        LanguageManager.Instance.SetNextLanguage();
    }
}
```

### 언어 변경 이벤트 구독

```csharp
using Maglin.Core;
using UnityEngine;

public class LanguageObserver : MonoBehaviour
{
    void OnEnable()
    {
        LanguageManager.OnLanguageChanged += HandleLanguageChanged;
    }
    
    void OnDisable()
    {
        LanguageManager.OnLanguageChanged -= HandleLanguageChanged;
    }
    
    private void HandleLanguageChanged(LanguageManager.SupportedLanguage newLanguage)
    {
        Debug.Log($"언어가 변경되었습니다: {newLanguage}");
        // 언어 변경에 따른 추가 작업 수행
    }
}
```

### 현재 언어 확인

```csharp
// 현재 언어 가져오기
var currentLanguage = LanguageManager.Instance.GetCurrentLanguage();

// 현재 언어의 표시 이름 가져오기
string displayName = LanguageManager.Instance.GetCurrentLanguageDisplayName();
// 예: "한국어", "English", "简体中文"
```

## 폰트 관리 API

### 동적 UI 생성 시

```csharp
using UnityEngine;
using TMPro;
using Maglin.Core;

public class DynamicUIManager : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab;
    
    public void CreateNewText()
    {
        GameObject newTextObj = Instantiate(textPrefab, transform);
        TMP_Text tmpText = newTextObj.GetComponent<TMP_Text>();
        
        if (tmpText != null && LanguageManager.Instance != null)
        {
            // 동적으로 생성된 텍스트 등록
            LanguageManager.Instance.RegisterTextComponent(tmpText);
        }
    }
}
```

### 수동으로 폰트 새로고침

```csharp
using UnityEngine;
using Maglin.Core;

public class RefreshButton : MonoBehaviour
{
    public void OnRefreshButtonClicked()
    {
        if (LanguageManager.Instance != null)
        {
            // 모든 텍스트 컴포넌트 재스캔 및 폰트 재적용
            LanguageManager.Instance.RefreshAllFonts();
        }
    }
}
```

## 디버깅

### 로그 확인
Debug Mode가 활성화되어 있으면 다음 정보가 로그에 출력됩니다:
- 초기화 완료
- 언어 변경
- Locale 설정 변경
- 폰트 로드 성공/실패
- 컴포넌트 등록
- 폰트 적용 완료 (몇 개의 컴포넌트에 적용되었는지)

### 일반적인 문제 해결

#### 1. 폰트가 변경되지 않음
- FontAssetTable에 해당 언어의 폰트가 설정되어 있는지 확인
- Entry Key가 정확히 "New Entry"인지 확인
- LanguageManager의 Debug Mode를 켜고 로그 확인
- Inspector에서 "Apply Current Language Font" 실행

#### 2. 일부 텍스트만 폰트가 변경됨
- 동적으로 생성된 UI는 수동으로 RegisterTextComponent() 호출 필요
- 또는 RefreshAllFonts() 메서드 호출하여 전체 재스캔

#### 3. 씬 전환 후 폰트가 기본 폰트로 돌아감
- SceneTransitionManager 또는 AdditiveSceneLoader에 LanguageManager 통합 확인
- LanguageManager가 DontDestroyOnLoad로 설정되어 있는지 확인 (자동 설정됨)

#### 4. 언어 설정이 저장되지 않음
- PlayerPrefs가 정상 작동하는지 확인
- 빌드 시에는 정상 작동하는지 확인 (에디터와 다를 수 있음)

## 지원 언어

현재 지원하는 언어:
- 🇰🇷 **Korean** (ko-KR) - 한국어
- 🇺🇸 **English** (en) - 영어
- 🇨🇳 **Chinese Simplified** (zh-CN) - 简体中文

### 새 언어 추가 방법

1. **LanguageManager.cs 수정:**
```csharp
public enum SupportedLanguage
{
    Korean = 0,
    English = 1,
    ChineseSimplified = 2,
    Japanese = 3  // 새 언어 추가
}

private readonly string[] languageDisplayNames = new string[]
{
    "한국어",
    "English",
    "简体中文",
    "日本語"  // 표시 이름 추가
};

private readonly string[] localeIdentifiers = new string[]
{
    "ko-KR",
    "en",
    "zh-CN",
    "ja"  // Locale 코드 추가
};
```

2. **Unity Localization 설정:**
   - Localization Settings에 새 Locale 추가
   - FontAssetTable에 해당 언어의 폰트 추가

3. **Context Menu 추가 (선택사항):**
```csharp
[ContextMenu("Switch to Japanese")]
public void SwitchToJapanese()
{
    SetLanguage(SupportedLanguage.Japanese);
}
```

## 주요 메서드 레퍼런스

### 언어 관리

| 메서드 | 설명 |
|--------|------|
| `SetLanguage(SupportedLanguage)` | 언어를 변경합니다 |
| `GetCurrentLanguage()` | 현재 언어를 반환합니다 |
| `GetCurrentLanguageDisplayName()` | 현재 언어의 표시 이름을 반환합니다 |
| `SetNextLanguage()` | 다음 언어로 순환 변경합니다 |
| `SetPreviousLanguage()` | 이전 언어로 순환 변경합니다 |

### 폰트 관리

| 메서드 | 설명 |
|--------|------|
| `RegisterTextComponent(TMP_Text)` | 특정 TextMeshPro 컴포넌트를 등록합니다 |
| `UnregisterTextComponent(TMP_Text)` | 특정 TextMeshPro 컴포넌트의 등록을 해제합니다 |
| `RegisterAllTextComponentsInScene()` | 현재 씬의 모든 TextMeshPro 컴포넌트를 찾아서 등록합니다 |
| `OnSceneLoaded()` | 새 씬이 로드되었을 때 호출합니다 (자동 호출됨) |
| `RefreshAllFonts()` | 모든 TextMeshPro 컴포넌트를 재등록하고 폰트를 다시 적용합니다 |

### 이벤트

| 이벤트 | 설명 |
|--------|------|
| `OnLanguageChanged` | 언어가 변경될 때 발생하는 static 이벤트 |

## 성능 고려사항

- **폰트 캐싱**: 각 언어의 폰트는 한 번 로드되면 캐시되어 재사용됩니다
- **씬 전환**: 새 씬의 모든 TextMeshPro 컴포넌트를 스캔하는 데 약간의 시간이 소요될 수 있습니다
- **대량 텍스트**: 매우 많은 TextMeshPro 컴포넌트가 있는 경우, `autoRegisterTextComponents`를 false로 설정하고 필요한 컴포넌트만 수동 등록하는 것을 고려하세요

## 통합 완료 체크리스트

- [ ] FontAssetTable에 모든 언어의 폰트 설정
- [ ] 씬에 LanguageManager GameObject 추가
- [ ] Inspector에서 설정 확인 (Font Asset Table Name, Entry Key 등)
- [ ] SceneTransitionManager/AdditiveSceneLoader 통합 확인
- [ ] 테스트: Inspector 컨텍스트 메뉴로 언어 변경 시 폰트 자동 변경 확인
- [ ] 테스트: 씬 전환 시 폰트가 유지되는지 확인
- [ ] 필요시 동적 UI에 대한 수동 등록 코드 추가
- [ ] UI에 언어 선택 버튼 추가 및 연결

