# 메글린(Maglin) - 2D 덱빌딩 카드 배틀 게임

Unity 2022 기반의 로그라이크 카드 배틀 게임입니다.

## 📂 프로젝트 구조

```
Assets/
├─ Scripts/
│   ├─ Core/          # 기본 시스템 로직, 게임 관리자
│   ├─ UI/            # UI 관련 로직 및 매니저
│   ├─ Cards/         # 카드 시스템 관리
│   ├─ Battle/        # 전투 관련 로직 및 시스템
│   ├─ Event/         # 이벤트화면 관련 로직 및 시스템
│   ├─ Player/        # 플레이어 상태 관리
│   ├─ Enemy/         # 몬스터 관련 스크립트
│   ├─ Relics/        # 유물 시스템 관리
│   ├─ Shop/          # 상점 관련 로직 관리
│   └─ Utilities/     # 보조 함수 및 클래스
│
├─ ScriptableObjects/
│   ├─ Cards/         # 카드 데이터
│   ├─ Battles/       # 전투 구성 데이터
│   ├─ BattleStage/   # 층별 전투 풀 데이터
│   ├─ Enemies/       # 몬스터 데이터
│   ├─ Objects/       # 중립 오브젝트 데이터
│   ├─ Relics/        # 유물 데이터
│   ├─ Shops/         # 상점 데이터
│   └─ Rewards/       # 보상 데이터
│
├─ Scenes/
│   ├─ MainMenu/      # 메인메뉴 씬
│   ├─ BattleScene/   # 전투 씬
│   ├─ EventScene/    # 이벤트 씬
│   └─ ShopScene/     # 상점 씬
│
├─ Prefabs/
│   ├─ UI/            # UI 프리팹
│   ├─ Player/        # 플레이어 프리팹
│   ├─ Cards/         # 카드 프리팹
│   ├─ Enemies/       # 몬스터 프리팹
│   └─ Effects/       # 이펙트 프리팹
│
├─ ArtAssets/
│   ├─ Sprites/       # 캐릭터, 몬스터 등 2D 이미지
│   └─ UI/            # 아이콘, 버튼 등 UI 이미지
│
├─ Animations/        # 애니메이션 파일
├─ Sounds/
│   ├─ Effects/       # 효과음
│   └─ Music/         # 배경음악
└─ Fonts/             # 폰트 파일
```

## 🎯 개발 순서

1. **Unity 프로젝트 초기 설정 및 폴더 구조 생성** ✅
2. **기본 ScriptableObject 클래스 구조 정의**
3. **게임 매니저 및 핵심 시스템 매니저 구현**
4. **카드 데이터 구조 및 조합 시스템 구현**
5. **전투 시스템 구현**
6. **UI 구현**
7. **통합 테스트**

## 🎮 게임 특징

- **로그라이크 시스템**: 매 회차마다 다른 경험
- **독특한 카드 조합 메커니즘**: 최대 3장 카드 조합
- **속성과 필드 시스템**: 5가지 속성 상성 시스템
- **10칸 횡스크롤 전투**: 위치 기반 전략 전투
- **유물 시스템**: 영구적인 효과와 전략적 선택

## 🛠️ 기술 스택

- **Engine**: Unity 2022
- **Language**: C#
- **Platform**: PC (Windows/Mac)
- **Resolution**: FHD (1920x1080) 기본 지원

## 📋 현재 상태

프로젝트 초기 설정 완료. 다음 단계: ScriptableObject 클래스 구조 정의 