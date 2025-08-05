# 턴제 전투 테스트 가이드

이 가이드는 완료된 태스크들을 바탕으로 턴제 전투 시스템을 테스트하는 방법을 설명합니다.

## 🎯 테스트 목표

1. **플레이어 턴**: 카드 드로우 → 카드 선택 → 턴 종료
2. **몬스터 턴**: 몬스터 이동 → 몬스터 공격 → 다음 턴으로
3. **전투 흐름**: 턴제 기반으로 플레이어와 몬스터가 번갈아 행동

## 🛠️ 설정 단계

### 1단계: 에디터 도구로 데이터 생성

Unity 에디터에서 다음 순서로 실행:

1. **Tools → Generate Test Data** 창 열기
2. **"모든 테스트 데이터 생성"** 버튼 클릭
   - 5장의 기본 카드 생성 (파이어볼, 아이스 샤드, 힐링 포션, 바람 칼날, 전기 충격)
   - 1마리의 고블린 몬스터 생성
   - 1층 전투 스테이지 생성
   - 고블린과의 전투 SO 생성

### 2단계: UI 프리팹 생성

1. **Tools → Generate Battle UI** 창 열기
2. **"전투 화면 UI 생성"** 버튼 클릭
3. **Tools → Generate Prefabs** 창 열기
4. **"모든 프리팹 생성"** 버튼 클릭

### 3단계: 전투 씬 설정

1. 새로운 씬을 생성하거나 BattleScene을 열기
2. 생성된 **BattleUI** 프리팹을 씬에 배치
3. 빈 GameObject를 생성하고 **BattleTestController** 스크립트 추가

### 4단계: BattleTestController 설정

Inspector에서 다음 항목들을 연결:

#### UI References:
- **Health Text**: `BattleUI/PlayerStatusPanel/HealthText`
- **Mana Text**: `BattleUI/PlayerStatusPanel/ManaText` 
- **Gold Text**: `BattleUI/PlayerStatusPanel/GoldText`
- **Deck Count Text**: `BattleUI/CardArea/DeckArea/DeckCountText`
- **Hand Content**: `BattleUI/CardArea/HandArea/HandContent`
- **End Turn Button**: `BattleUI/CardArea/ButtonArea/EndTurnButton`
- **Use Cards Button**: `BattleUI/CardArea/ButtonArea/UseButton`
- **Draw Button**: `BattleUI/CardArea/ButtonArea/DrawButton`
- **Turn Indicator**: 별도 텍스트 추가 (화면 상단 중앙에 배치)

#### Field References:
- **Player Indicator**: `BattleUI/FieldArea/PlayerIndicator`

#### Prefabs:
- **Card UI Prefab**: `Assets/Prefabs/CardUIPrefab.prefab`
- **Monster Prefab**: `Assets/Prefabs/MonsterPrefab.prefab`

#### Test Data:
- **Test Cards**: `Assets/ScriptableObjects/Cards/` 폴더의 5장 카드 모두 드래그
- **Test Monsters**: `Assets/ScriptableObjects/Enemies/Goblin.asset` 드래그
- **Test Battle Stage**: `Assets/ScriptableObjects/BattleStage/Floor1_Stage.asset` 드래그

## 🎮 테스트 방법

### 기본 전투 흐름

1. **Play 버튼** 클릭하여 게임 실행
2. 자동으로 전투가 시작되고 다음이 확인됩니다:
   - 고블린이 필드 오른쪽에 스폰됨
   - 플레이어가 필드 왼쪽에 위치함
   - 플레이어 턴으로 시작하며 5장의 카드가 드로우됨

### 플레이어 턴 테스트

1. **카드 선택**: 손패의 카드를 클릭하여 선택 (최대 3장)
   - 선택된 카드는 노란색으로 변함
2. **카드 사용**: "사용" 버튼을 클릭하여 선택된 카드들 사용
3. **추가 드로우**: "드로우 (10)" 버튼을 클릭하여 마나를 소모하고 추가 카드 드로우
4. **턴 종료**: "턴 종료" 버튼을 클릭하여 몬스터 턴으로 넘어감

### 몬스터 턴 테스트

1. 턴 종료 후 자동으로 몬스터 턴 시작
2. 고블린이 플레이어 쪽으로 이동
3. 공격 범위에 들어오면 플레이어를 공격
4. 몬스터 행동 완료 후 다시 플레이어 턴으로 돌아감

### 승부 판정

- **승리 조건**: 고블린 체력이 0이 되면 전투 승리
- **패배 조건**: 플레이어 체력이 0이 되면 전투 패배

## 🔍 테스트 포인트

### 확인할 기능들

1. **카드 시스템**:
   - ✅ 턴 시작 시 5장 드로우
   - ✅ 카드 선택 UI 동작
   - ✅ 턴 종료 시 모든 카드가 덱으로 복귀하고 셔플
   - ✅ 추가 드로우 시 마나 소모 (10→20→30)

2. **몬스터 시스템**:
   - ✅ 몬스터 스폰 및 위치 표시
   - ✅ 왼쪽부터 순서대로 행동
   - ✅ 이동 → 공격 순서로 행동
   - ✅ 체력 표시 및 데미지 적용

3. **턴 시스템**:
   - ✅ 플레이어 턴 ↔ 몬스터 턴 교대
   - ✅ 턴 인디케이터 표시
   - ✅ 턴에 따른 버튼 활성화/비활성화

4. **UI 시스템**:
   - ✅ 플레이어 상태 (체력, 마나, 골드) 표시
   - ✅ 덱 카운트 표시
   - ✅ 필드 슬롯과 캐릭터 위치 표시

## 🐛 디버그 정보

**BattleTestController**에서 `Debug Mode`가 활성화되어 있으면 콘솔에서 다음 정보 확인 가능:

- 전투 시작/종료 로그
- 카드 드로우/사용 로그  
- 몬스터 스폰/행동 로그
- 턴 변경 로그

**디버그 메뉴**: 
- 우클릭 → `Debug Battle Info`로 현재 전투 상태 확인

## 🎯 기대 결과

이 테스트를 통해 다음을 확인할 수 있습니다:

1. **완료된 Task 66-76의 시스템들이 올바르게 통합되어 작동**
2. **턴제 전투의 기본 흐름이 정상 동작**
3. **각 Manager들 간의 이벤트 통신이 원활히 작동**
4. **UI가 실제 데이터와 동기화되어 업데이트**

## 📝 다음 단계

현재 구현은 기본적인 턴제 전투 흐름입니다. 추후 다음 기능들을 추가할 수 있습니다:

- 카드 조합 시스템 (Task 70 기반)
- 속성 상성 시스템 (Task 72 기반)  
- 필드 효과 시스템 (Task 72 기반)
- 실제 카드 효과 적용
- 몬스터 AI 개선
- 애니메이션 및 이펙트

## ⚠️ 주의사항

1. **Manager들의 싱글톤 초기화**: 게임 시작 시 모든 Manager가 올바르게 초기화되는지 확인
2. **이벤트 구독**: BattleTestController가 올바르게 이벤트를 구독하는지 확인
3. **null 참조**: UI 레퍼런스가 모두 올바르게 연결되었는지 확인
4. **데이터 연결**: Test Data 필드에 생성된 ScriptableObject들이 올바르게 연결되었는지 확인

---

**성공적인 테스트를 위해 위 단계를 순서대로 따라하시기 바랍니다!** 🎮 