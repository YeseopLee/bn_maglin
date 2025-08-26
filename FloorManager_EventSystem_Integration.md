# FloorManager와 EventSystem 통합 작업 보고서

## 개요
FloorManager에서 일반 전투층 대신 이벤트 씬으로 진입할 수 있도록 EventManager와의 통합 작업을 완료했습니다. 이를 통해 층별 진행에서 전투와 이벤트가 자연스럽게 전환될 수 있게 되었습니다.

## 주요 변경 사항

### 1. BattleStageSO 수정
- **제거된 기능**: EventChance 관련 코드 제거
  - `eventChance` 필드 제거
  - `EventChance` 프로퍼티 제거  
  - `ShouldTriggerEvent()` 메서드 제거

**변경 이유**: 이벤트 발생 확률을 EventManager에서만 중앙 관리하기 위함

### 2. FloorManager 수정

#### 2.1 층 시작 로직 개선
**파일**: `Assets/Scripts/Core/FloorManager.cs`
**메서드**: `StartCurrentFloor()`

```csharp
// 기존 코드
case FloorType.Normal:
case FloorType.Elite:
case FloorType.Boss:
    StartBattle();
    break;

// 수정된 코드  
case FloorType.Normal:
    // 일반층에서는 이벤트 발생 확률 체크
    if (ShouldTriggerEvent())
    {
        StartEvent();
    }
    else
    {
        StartBattle();
    }
    break;
```

#### 2.2 이벤트 발생 체크 메서드 추가
**새로운 메서드**: `ShouldTriggerEvent()`

- EventManager와 연동하여 이벤트 발생 여부 판단
- 디버그 로그로 이벤트/전투 진행 상황 추적
- EventManager.Instance null 체크 포함

#### 2.3 이벤트 시작 메서드 개선
**메서드**: `StartEvent()`

- 디버그 로그 개선
- GameState.Event로 상태 전환
- 이벤트 씬 로딩

### 3. EventManager 수정

#### 3.1 이벤트 발생 체크 로직 분리
**메서드**: `TryTriggerEvent(int currentFloor)`

**기존 동작**: 이벤트 발생 확률 체크 → 이벤트 선택 → 즉시 이벤트 시작
**수정된 동작**: 이벤트 발생 확률 체크 → 이벤트 선택 → 이벤트 예정으로 저장 (시작하지 않음)

```csharp
// 이벤트 발생 예정으로 저장 (실제 시작은 StartSelectedEvent에서)
currentEvent = selectedEvent;
return true;
```

#### 3.2 새로운 메서드 추가
**메서드**: `StartSelectedEvent()`

- 이벤트 씬에서 호출될 메서드
- TryTriggerEvent에서 선택된 이벤트를 실제로 시작

#### 3.3 이벤트 선택 로직 개선
**메서드**: `SelectRandomEvent(int currentFloor)`

**개선 사항**:
- 인스펙터에 등록된 availableEvents만 고려
- 상세한 디버그 로그 추가
- 가중치 검증 로직 강화
- 에러 상황에 대한 안전장치 강화

```csharp
// 현재 층에서 등장 가능한 이벤트들 필터링
var validEvents = availableEvents.Where(e =>
    e != null &&
    e.IsValid() &&
    e.CanAppearOnFloor(currentFloor) &&
    (!e.IsOneTimeOnly || !usedOneTimeEvents.Contains(e.EventName))
).ToList();
```

## 시스템 동작 플로우

### 일반층에서 이벤트 발생 시나리오

1. **FloorManager.StartCurrentFloor()** 호출
2. **FloorManager.ShouldTriggerEvent()** 에서 EventManager에게 이벤트 발생 체크 요청
3. **EventManager.TryTriggerEvent()** 에서:
   - 이벤트 발생 확률 체크
   - 현재 층에서 등장 가능한 이벤트 검색
   - 조건을 만족하는 이벤트가 있으면 `currentEvent`에 저장하고 `true` 반환
4. **FloorManager.StartEvent()** 호출하여 이벤트 씬으로 전환
5. 이벤트 씬에서 **EventManager.StartSelectedEvent()** 호출하여 실제 이벤트 시작

### 일반층에서 전투 발생 시나리오

1. **FloorManager.StartCurrentFloor()** 호출
2. **FloorManager.ShouldTriggerEvent()** 에서 `false` 반환
3. **FloorManager.StartBattle()** 호출하여 전투 씬으로 전환

## 설정 방법

### EventManager 설정
1. EventManager 인스펙터에서 `Available Events` 배열에 EventSO 할당
2. `Event Chance` 값 설정 (0.0 ~ 1.0, 기본값: 0.3)

### EventSO 설정  
각 EventSO에서:
- `Min Floor`: 이벤트 등장 최소층
- `Max Floor`: 이벤트 등장 최대층
- `Spawn Weight`: 이벤트 등장 가중치
- `Is One Time Only`: 일회성 이벤트 여부

## 장점

1. **중앙 집중 관리**: 이벤트 발생 확률을 EventManager에서만 관리
2. **층별 조건 관리**: EventSO의 minFloor/maxFloor로 층별 이벤트 제어
3. **인스펙터 기반 관리**: 등록된 이벤트만 등장하는 명확한 시스템
4. **가중치 시스템**: 이벤트별 등장 확률 조절 가능
5. **디버그 친화적**: 상세한 로그로 디버깅 용이

## 호환성

- 기존 FloorManager 시스템과 완전 호환
- 엘리트층, 보스층, 상점층은 기존과 동일하게 동작
- 시작층(Start)은 항상 전투로 진행
- 일반층(Normal)에서만 이벤트 발생 가능

## 테스트 권장 사항

1. EventManager 인스펙터에 다양한 층 범위의 EventSO 등록
2. EventChance 값을 1.0으로 설정하여 이벤트 강제 발생 테스트
3. 각 EventSO의 minFloor/maxFloor 범위 확인
4. 일회성 이벤트 동작 확인
5. 이벤트 완료 후 다음 층 진행 확인
