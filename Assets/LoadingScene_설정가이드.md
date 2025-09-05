# LoadingScene 설정 가이드

## 새로운 로딩 시스템 설정 방법

### 1. LoadingScene 설정

1. **LoadingScene.unity** 열기
2. 빈 GameObject 생성하고 이름을 "LoadingSceneInitializer"로 변경
3. 해당 GameObject에 `LoadingSceneController` 스크립트 추가
4. 설정:
   - Debug Mode: ✓ (개발 중)
   - Enable Simple Animation: 선택사항
   - Rotating Object: 회전할 오브젝트가 있다면 설정 (선택사항)

### 2. 메인 씬에 AdditiveSceneLoader 설정

1. **MainGame.unity** 또는 메인 씬 열기
2. DontDestroyOnLoad 오브젝트에 `AdditiveSceneLoader` 추가:
   - 빈 GameObject 생성
   - 이름: "AdditiveSceneLoader"
   - `AdditiveSceneLoader` 스크립트 추가
   - 설정:
     - Loading Scene Name: "LoadingScene"
     - Pre Loading Delay: 0.1
     - Post Loading Delay: 0.5
     - Debug Mode: ✓

### 3. 전환 효과 설정

`AdditiveSceneLoader`는 자동으로 `SceneTransitionEffect`를 생성하므로 별도 설정 불필요.

### 4. 사용 방법

```csharp
// 기본 씬 전환
await AdditiveSceneLoader.Instance.LoadSceneWithTransition("TargetScene");

// 추가 대기 시간과 함께
await AdditiveSceneLoader.Instance.LoadSceneWithTransition("BattleScene", 0.3f);
```

### 5. Build Settings 확인

다음 씬들이 Build Settings에 포함되어 있는지 확인:
- LoadingScene
- MainGame
- BattleScene
- EventScene
- ShopScene

### 6. 특징

- **자연스러운 전환**: 오른쪽에서 왼쪽으로 덮는 효과
- **빠른 로딩**: 단순한 로딩 씬으로 오버헤드 최소화
- **애니메이션 준비**: 타겟 씬 로딩 후 애니메이션 준비 시간 제공
- **폴백 지원**: 기존 SceneTransitionManager로 폴백 가능

### 7. 디버그

- AdditiveSceneLoader에서 "Debug Scene Info" 컨텍스트 메뉴로 현재 씬 상태 확인
- LoadingSceneController에서 "Debug Loading Scene Info"로 로딩 씬 상태 확인
- SceneTransitionEffect에서 "Test Full Transition"으로 전환 효과 테스트

### 8. 문제 해결

**씬이 로드되지 않는 경우:**
- Build Settings에 씬이 추가되어 있는지 확인
- 씬 이름이 정확한지 확인

**전환 효과가 보이지 않는 경우:**
- Canvas Sorting Order가 충분히 높은지 확인 (기본값: 10000)
- 다른 UI 요소들이 전환 효과를 가리지 않는지 확인

**성능 문제:**
- Post Loading Delay 값을 줄여보기 (기본값: 0.5)
- 복잡한 애니메이션이 있는 씬의 경우 추가 대기 시간 늘리기
