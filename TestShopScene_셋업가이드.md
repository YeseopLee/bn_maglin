 # TestShopScene 상점 시스템 셋업 가이드

## 1. 기본 매니저 오브젝트 생성

### 필수 매니저들
TestShopScene에서 상점이 제대로 작동하려면 다음 매니저들이 필요합니다:

1. **PlayerManager** - 플레이어 골드, 체력 관리
2. **CardManager** - 플레이어 덱 관리  
3. **ShopManager** - 상점 로직 관리
4. **ShopUIManager** - 상점 UI 관리

### 매니저 오브젝트 생성 방법

```
1. 빈 GameObject 생성 후 이름을 "Managers"로 변경
2. Managers 오브젝트에 다음 컴포넌트들 추가:
   - PlayerManager
   - CardManager  
   - ShopManager
   - ShopUIManager
```

## 2. UI 생성

### 자동 UI 생성 (권장)
```
Unity Editor > Tools > Generate Shop UI 클릭
```

이렇게 하면 다음이 자동 생성됩니다:
- Canvas (없는 경우)
- ShopUI (상점 메인 UI)
- CardRemovalUI (카드 제거 UI)

### 수동 UI 생성 (선택사항)
자동 생성된 UI가 마음에 들지 않으면 수동으로 생성할 수 있습니다:

```
1. Canvas 생성
2. ShopUI 오브젝트 생성 (Canvas 하위)
3. 필요한 UI 요소들 생성:
   - 플레이어 상태 (골드, 체력)
   - 카드 슬롯 5개
   - 유물 슬롯 3개
   - 서비스 슬롯 2개
   - 나가기 버튼
```

## 3. ShopSO 에셋 생성

```
1. Project 창에서 우클릭
2. Create > Maglin > Shop > ShopSO 선택
3. 이름을 "TestShop"로 설정
4. Inspector에서 설정:
   - Shop Name: "테스트 상점"
   - Card Slots Count: 5
   - Relic Slots Count: 3
   - 가격 설정 등
```

## 4. 매니저 설정

### ShopManager 설정
```
1. ShopManager 컴포넌트 선택
2. Default Shop에 생성한 TestShop 에셋 연결
```

### PlayerManager 설정
```
1. PlayerManager 컴포넌트 선택  
2. 초기 골드, 체력 설정
3. Max Health: 100
4. Current Gold: 500 (테스트용)
```

### CardManager 설정
```
1. CardManager 컴포넌트 선택
2. Starter Deck에 테스트용 카드들 연결
   (Resources/Cards 폴더의 카드들)
```

## 5. 테스트용 스크립트 생성

TestShopScene에서 상점을 바로 시작하려면 간단한 테스트 스크립트가 필요합니다:

```csharp
using UnityEngine;
using Maglin.Shop;

public class ShopSceneTest : MonoBehaviour
{
    void Start()
    {
        // 모든 매니저가 초기화될 때까지 잠시 대기
        Invoke(nameof(StartShop), 0.5f);
    }
    
    private void StartShop()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.EnterShop(1); // 1층 상점으로 진입
        }
    }
}
```

## 6. 실행 순서

1. **씬 로드**
2. **매니저들 자동 초기화** (Awake/Start)
3. **ShopSceneTest.StartShop() 호출** (0.5초 후)
4. **ShopManager.EnterShop() 실행**
5. **상점 아이템 생성 및 UI 업데이트**

## 7. 필요한 Resources 폴더 구조

```
Assets/Resources/
├── Cards/
│   ├── Element 타입 카드들
│   ├── Active1 타입 카드들  
│   └── Active2 타입 카드들
└── Relics/
    └── Common 타입 유물들
```

## 8. 문제 해결

### 상점이 열리지 않는 경우
1. Console에서 에러 메시지 확인
2. ShopManager에 ShopSO 에셋이 연결되어 있는지 확인
3. Resources 폴더에 카드/유물이 있는지 확인

### UI가 표시되지 않는 경우  
1. Canvas가 있는지 확인
2. ShopUI 오브젝트가 활성화되어 있는지 확인
3. ShopUIManager가 올바른 UI 참조를 찾았는지 확인

### 구매가 안 되는 경우
1. PlayerManager에 충분한 골드가 있는지 확인
2. CardManager가 초기화되어 있는지 확인

## 9. 빠른 셋업 (최소한의 작업)

가장 빠르게 테스트하려면:

```
1. Managers 오브젝트 생성 + 4개 매니저 컴포넌트 추가
2. Tools > Generate Shop UI 실행
3. TestShop.asset 생성 후 ShopManager에 연결
4. PlayerManager 골드를 500으로 설정
5. ShopSceneTest 스크립트 추가
6. Play!
```
