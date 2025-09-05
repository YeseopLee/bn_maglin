using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Maglin.Event;
using Maglin.Shop;
using Maglin.Battle;

namespace Maglin.Core
{
    /// <summary>
    /// 게임 상태 열거형
    /// </summary>
    public enum GameState
    {
        MainMenu,       // 메인 메뉴
        Battle,         // 전투
        Shop,           // 상점
        Event,          // 이벤트
        GameOver,       // 게임 오버
        Victory         // 승리
    }

    /// <summary>
    /// 층 타입 enum
    /// </summary>
    public enum FloorType
    {
        Normal,     // 일반 전투층
        Elite,      // 엘리트 전투층 (5층 단위)
        Shop,       // 상점층 (3층 단위)
        Boss,       // 보스층 (10층 단위)
        Event,      // 이벤트층 (랜덤)
        Start       // 시작층 (1층)
    }

    /// <summary>
    /// 층 정보를 담는 구조체
    /// </summary>
    [System.Serializable]
    public class FloorInfo
    {
        public int floorNumber;
        public FloorType floorType;
        public string floorName;
        public bool isCompleted;
        public bool isCurrent;

        public FloorInfo(int number, FloorType type)
        {
            floorNumber = number;
            floorType = type;
            floorName = GetFloorName(number, type);
            isCompleted = false;
            isCurrent = false;
        }

        private string GetFloorName(int number, FloorType type)
        {
            return type switch
            {
                FloorType.Start => $"{number}층 - 시작",
                FloorType.Normal => $"{number}층 - 일반 전투",
                FloorType.Elite => $"{number}층 - 엘리트 전투",
                FloorType.Shop => $"{number}층 - 상점",
                FloorType.Boss => $"{number}층 - 보스 전투",
                FloorType.Event => $"{number}층 - 이벤트",
                _ => $"{number}층"
            };
        }
    }

    /// <summary>
    /// 층 진행 및 게임 플로우를 관리하는 매니저 클래스 (게임 상태 관리 포함)
    /// </summary>
    public class FloorManager : MonoBehaviour
    {
        [Header("게임 상태")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        [SerializeField] private GameState previousState = GameState.MainMenu;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private float transitionDelay = 0.5f;

        [Header("층 설정")]
        [SerializeField] private int currentFloor = 1;
        [SerializeField] private int maxFloor = 100;

        [Header("층별 규칙 설정")]
        [SerializeField] private int eliteFloorInterval = 5;    // 5층마다 엘리트
        [SerializeField] private int shopFloorInterval = 3;     // 3층마다 상점
        [SerializeField] private int bossFloorInterval = 10;    // 10층마다 보스

        [Header("BattleStage 설정")]
        [SerializeField] private BattleStageSO[] normalBattleStages;
        [SerializeField] private BattleStageSO[] eliteBattleStages;
        [SerializeField] private BattleStageSO[] bossBattleStages;

        // Event 설정은 EventManager에서 관리

        [Header("현재 진행중인 SO 정보 (읽기 전용)")]
        [Tooltip("현재 층에서 사용 중인 배틀 스테이지를 표시합니다.")]
        [SerializeField] private BattleStageSO currentBattleStageDisplay;
        [Tooltip("현재 층에서 선택된 실제 전투 데이터를 표시합니다.")]
        [SerializeField] private BattleSO currentBattleDisplay;
        [Tooltip("현재 층에서 진행 중인 이벤트를 표시합니다.")]
        [SerializeField] private EventSO currentEventDisplay;

        [Header("Scene 설정")]
        [SerializeField] private string battleSceneName = "TestBattleScene";
        [SerializeField] private string shopSceneName = "TestShopScene";
        [SerializeField] private string eventSceneName = "TestEventScene";
        [SerializeField] private string mainGameSceneName = "MainGame";

        // Public 프로퍼티들
        public string BattleSceneName => battleSceneName;
        public string ShopSceneName => shopSceneName;
        public string EventSceneName => eventSceneName;
        public string MainGameSceneName => mainGameSceneName;

        // 게임 상태 프로퍼티들
        public GameState CurrentState => currentState;
        public GameState PreviousState => previousState;
        public bool DebugMode => debugMode;

        // 싱글톤
        public static FloorManager Instance { get; private set; }

        // 게임 상태 이벤트
        public static event Action<GameState, GameState> OnGameStateChanged;
        public static event Action OnGameInitialized;

        // 층 이벤트
        public System.Action<int> OnFloorChanged;
        public System.Action<FloorType> OnFloorTypeChanged;
        public System.Action<FloorInfo> OnFloorStarted;
        public System.Action<FloorInfo> OnFloorCompleted;
        public System.Action OnGameReset;

        // 현재 층 정보
        public int CurrentFloor => currentFloor;
        public FloorType CurrentFloorType => GetFloorType(currentFloor);
        public FloorInfo CurrentFloorInfo => new FloorInfo(currentFloor, CurrentFloorType) { isCurrent = true };

        // 층 진행 기록
        private List<FloorInfo> floorHistory = new List<FloorInfo>();
        private bool isProcessingFloor = false;
        private bool isCurrentFloorEvent = false; // 현재 층이 이벤트인지 추적
        private bool isGameInitialized = false;
        private bool isTransitioning = false;

        // 세이브 시스템
        private bool autoSaveEnabled = true;

        // 현재 층에서 사용 중인 SO들
        private BattleStageSO currentBattleStage;
        private BattleSO currentBattle;
        private EventSO currentEvent;

        // 이벤트 전투 모드 관련
        private bool isEventBattleMode = false;
        private EventSO savedEventForBattle = null;

        // 현재 SO 정보 프로퍼티들
        public BattleStageSO CurrentBattleStage => currentBattleStage;
        public BattleSO CurrentBattle => currentBattle;
        public EventSO CurrentEvent => currentEvent;

        // 게임 초기화 완료 여부
        public bool IsGameInitialized => isGameInitialized;
        public bool IsTransitioning => isTransitioning;

        // 이벤트 전투 모드 프로퍼티
        public bool IsEventBattleMode => isEventBattleMode;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 세이브 파일 존재 여부 확인하여 초기화 방식 결정
                bool hasSaveFile = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile;

                if (hasSaveFile)
                {
                    // 세이브 파일이 있으면 기본 초기화만 수행 (층 정보는 나중에 로드)
                    InitializeGameWithoutFloorReset();
                }
                else
                {
                    // 세이브 파일이 없으면 전체 초기화 수행
                    InitializeGame();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Instance == this)
            {
                // 게임 시작 시 초기 상태 설정
                ChangeGameState(GameState.MainMenu);
            }
        }

        /// <summary>
        /// 게임 초기화
        /// </summary>
        private void InitializeGame()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 게임 초기화 시작");

            // 층 시스템 초기화
            InitializeFloorSystem();

            // 게임 상태 초기화
            currentState = GameState.MainMenu;
            previousState = GameState.MainMenu;
            isTransitioning = false;

            // 초기화 완료 표시
            isGameInitialized = true;
            OnGameInitialized?.Invoke();

            if (debugMode)
                Debug.Log("[FloorManager] 게임 초기화 완료");
        }

        /// <summary>
        /// 세이브 파일이 있을 때 사용하는 초기화 (층 정보 리셋 없음)
        /// </summary>
        private void InitializeGameWithoutFloorReset()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 세이브 로드용 게임 초기화 시작");

            // 층 정보는 건드리지 않고 기본 상태만 초기화
            floorHistory.Clear();
            isProcessingFloor = false;

            // 현재 SO들 초기화
            currentBattleStage = null;
            currentBattle = null;
            currentEvent = null;

            // 인스펙터 디스플레이도 초기화
            currentBattleStageDisplay = null;
            currentBattleDisplay = null;
            currentEventDisplay = null;

            // 게임 상태 초기화
            currentState = GameState.MainMenu;
            previousState = GameState.MainMenu;
            isTransitioning = false;

            // 초기화 완료 표시
            isGameInitialized = true;
            OnGameInitialized?.Invoke();

            if (debugMode)
                Debug.Log("[FloorManager] 세이브 로드용 게임 초기화 완료");
        }

        /// <summary>
        /// 층 시스템 초기화
        /// </summary>
        private void InitializeFloorSystem()
        {
            currentFloor = 1;
            floorHistory.Clear();

            // 첫 번째 층 정보 추가
            FloorInfo startFloor = new FloorInfo(1, FloorType.Start) { isCurrent = true };
            floorHistory.Add(startFloor);

            // 현재 SO들 초기화
            currentBattleStage = null;
            currentBattle = null;
            currentEvent = null;

            // 인스펙터 디스플레이도 초기화
            currentBattleStageDisplay = null;
            currentBattleDisplay = null;
            currentEventDisplay = null;

            Debug.Log($"[FloorManager] Floor system initialized. Starting at floor {currentFloor}");
        }

        #region Game State Management
        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void ChangeGameState(GameState newState)
        {
            if (isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning("[FloorManager] 이미 화면 전환 중입니다.");
                return;
            }

            if (currentState == newState)
            {
                if (debugMode)
                    Debug.LogWarning($"[FloorManager] 이미 {newState} 상태입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[FloorManager] 게임 상태 변경: {currentState} -> {newState}");

            // 이전 상태 저장
            previousState = currentState;

            // 화면 전환 시작
            StartCoroutine(TransitionToState(newState));
        }

        /// <summary>
        /// 상태 전환 코루틴
        /// </summary>
        private System.Collections.IEnumerator TransitionToState(GameState newState)
        {
            isTransitioning = true;

            // 이전 상태 종료 처리
            OnExitState(currentState);

            // 전환 딜레이
            if (transitionDelay > 0)
            {
                yield return new WaitForSeconds(transitionDelay);
            }

            // 새 상태로 변경
            currentState = newState;

            // 새 상태 진입 처리
            OnEnterState(newState);

            // 상태 변경 이벤트 발생
            OnGameStateChanged?.Invoke(previousState, currentState);

            isTransitioning = false;

            if (debugMode)
                Debug.Log($"[FloorManager] 상태 전환 완료: {newState}");
        }

        /// <summary>
        /// 상태 진입 시 처리
        /// </summary>
        private void OnEnterState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    // TODO: 메인 메뉴 UI 활성화
                    break;

                case GameState.Battle:
                    // TODO: 전투 시스템 초기화
                    break;

                case GameState.Shop:
                    // TODO: 상점 UI 활성화
                    break;

                case GameState.Event:
                    // TODO: 이벤트 시스템 활성화
                    break;

                case GameState.GameOver:
                    // TODO: 게임 오버 처리
                    break;

                case GameState.Victory:
                    // TODO: 승리 처리
                    break;

            }
        }

        /// <summary>
        /// 상태 종료 시 처리
        /// </summary>
        private void OnExitState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    // TODO: 메인 메뉴 UI 비활성화
                    break;

                case GameState.Battle:
                    // TODO: 전투 시스템 정리
                    break;

                case GameState.Shop:
                    // TODO: 상점 UI 비활성화
                    break;

                case GameState.Event:
                    // TODO: 이벤트 시스템 정리
                    break;

            }
        }

        /// <summary>
        /// 새 게임 시작
        /// </summary>
        public void StartNewGame()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 새 게임 시작");

            // 게임 상태 초기화
            ResetToFloor(1);
            StartCurrentFloor();
        }

        /// <summary>
        /// 게임 재시작 (로그라이크 특성 - 모든 진행상황 초기화)
        /// </summary>
        public void RestartGame()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 게임 재시작 (로그라이크)");

            // 모든 진행상황 초기화
            ResetToFloor(1);

            // 새 게임 시작
            StartNewGame();
        }

        /// <summary>
        /// 게임 오버 처리
        /// </summary>
        public void GameOver()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 게임 오버");

            ChangeGameState(GameState.GameOver);
        }

        /// <summary>
        /// 게임 승리 처리
        /// </summary>
        public void Victory()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 게임 승리");

            ChangeGameState(GameState.Victory);
        }

        /// <summary>
        /// 메인 메뉴로 돌아가기
        /// </summary>
        public void ReturnToMainMenu()
        {
            if (debugMode)
                Debug.Log("[FloorManager] 메인 메뉴로 돌아가기");

            ChangeGameState(GameState.MainMenu);
        }

        /// <summary>
        /// 특정 층으로 이동 (디버그용)
        /// </summary>
        public void SetFloor(int targetFloor)
        {
            if (!debugMode)
            {
                Debug.LogWarning("[FloorManager] SetFloor는 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            if (targetFloor < 1 || targetFloor > maxFloor)
            {
                Debug.LogError($"[FloorManager] 잘못된 층 번호: {targetFloor}");
                return;
            }

            currentFloor = targetFloor;
            OnFloorChanged?.Invoke(currentFloor);

            Debug.Log($"[FloorManager] 디버그: {currentFloor}층으로 이동");
        }

        /// <summary>
        /// 세이브 로드 시 현재 층 설정 (내부 사용)
        /// </summary>
        public void SetCurrentFloorFromSave(int targetFloor)
        {
            if (targetFloor < 1 || targetFloor > maxFloor)
            {
                Debug.LogError($"[FloorManager] 잘못된 층 번호: {targetFloor}, 1층으로 리셋");
                targetFloor = 1;
            }

            currentFloor = targetFloor;

            if (debugMode)
                Debug.Log($"[FloorManager] 세이브에서 {currentFloor}층으로 설정");
        }
        #endregion

        /// <summary>
        /// 게임 시작 (1층부터 시작)
        /// </summary>
        public void StartGame()
        {
            ResetToFloor(1);
            StartCurrentFloor();
        }

        /// <summary>
        /// 현재 층 시작
        /// </summary>
        public void StartCurrentFloor()
        {
            if (isProcessingFloor)
            {
                Debug.LogWarning("Floor is already being processed!");
                return;
            }

            isProcessingFloor = true;
            isCurrentFloorEvent = false; // 플래그 초기화
            FloorInfo currentFloorInfo = CurrentFloorInfo;

            Debug.Log($"Starting floor {currentFloor}: {currentFloorInfo.floorName}");
            Debug.Log($"[FloorManager] OnFloorStarted 이벤트 구독자 수: {OnFloorStarted?.GetInvocationList()?.Length ?? 0}");
            OnFloorStarted?.Invoke(currentFloorInfo);
            Debug.Log($"[FloorManager] OnFloorStarted 이벤트 발생 완료");

            // 층 타입에 따른 처리
            switch (CurrentFloorType)
            {
                case FloorType.Start:
                    StartBattle();
                    break;

                case FloorType.Normal:
                    // 일반층에서는 이벤트 발생 확률 체크
                    if (ShouldTriggerEvent())
                    {
                        isCurrentFloorEvent = true; // 이벤트 플래그 설정
                        StartEvent();
                    }
                    else
                    {
                        StartBattle();
                    }
                    break;

                case FloorType.Elite:
                case FloorType.Boss:
                    StartBattle();
                    break;

                case FloorType.Shop:
                    StartShop();
                    break;

                case FloorType.Event:
                    isCurrentFloorEvent = true; // 이벤트 플래그 설정
                    StartEvent();
                    break;
            }
        }

        /// <summary>
        /// 이벤트가 발생해야 하는지 확인 (EventManager와 연동)
        /// </summary>
        private bool ShouldTriggerEvent()
        {
            if (Event.EventManager.Instance == null)
            {
                Debug.LogWarning("[FloorManager] EventManager.Instance가 null입니다. 이벤트를 건너뜁니다.");
                return false;
            }

            // EventManager에서 이벤트 발생 여부 확인
            bool shouldTrigger = Event.EventManager.Instance.TryTriggerEvent(currentFloor);

            if (shouldTrigger)
            {
                Debug.Log($"[FloorManager] {currentFloor}층에서 이벤트가 발생합니다!");
            }
            else
            {
                Debug.Log($"[FloorManager] {currentFloor}층에서 일반 전투를 진행합니다.");
            }

            return shouldTrigger;
        }

        /// <summary>
        /// 현재 층 완료 처리
        /// </summary>
        public void CompleteCurrentFloor()
        {
            Debug.Log($"[FloorManager] CompleteCurrentFloor 호출됨. isProcessingFloor: {isProcessingFloor}, isEventBattleMode: {isEventBattleMode}");

            // 이벤트 전투 모드인 경우 특별 처리
            if (isEventBattleMode)
            {
                CompleteEventBattle();
                return;
            }

            if (!isProcessingFloor)
            {
                Debug.LogWarning("[FloorManager] No floor is being processed! 강제로 보상 표시 시도");

                // 강제로 보상 표시 (전투 승리 시에만)
                if (ShouldShowRewards(CurrentFloorType))
                {
                    Debug.Log($"[FloorManager] 강제 보상 화면 표시 시작");
                    ShowBattleRewards(currentFloor, CurrentFloorType);
                }
                return;
            }

            FloorInfo completedFloor = CurrentFloorInfo;
            completedFloor.isCompleted = true;
            completedFloor.isCurrent = false;

            // 기록에서 현재 층 업데이트
            for (int i = floorHistory.Count - 1; i >= 0; i--)
            {
                if (floorHistory[i].floorNumber == currentFloor)
                {
                    floorHistory[i] = completedFloor;
                    break;
                }
            }

            Debug.Log($"Completed floor {currentFloor}: {completedFloor.floorName}");
            OnFloorCompleted?.Invoke(completedFloor);

            // 전투 층인 경우에만 보상 표시 (이벤트 층은 제외)
            bool shouldShowRewards = ShouldShowRewards(CurrentFloorType) && !isCurrentFloorEvent;
            Debug.Log($"[FloorManager] 보상 표시 확인 - ShouldShowRewards({CurrentFloorType}): {ShouldShowRewards(CurrentFloorType)}, isCurrentFloorEvent: {isCurrentFloorEvent}, 최종 결과: {shouldShowRewards}");

            if (shouldShowRewards)
            {
                Debug.Log($"[FloorManager] 보상 화면 표시 시작");
                ShowBattleRewards(currentFloor, CurrentFloorType);
            }
            else
            {
                Debug.Log($"[FloorManager] 보상 화면 표시하지 않음. 바로 다음 층 진행");
                FinishFloorCompletion();
            }
        }

        /// <summary>
        /// 이벤트 전투 완료 처리
        /// </summary>
        private void CompleteEventBattle()
        {
            Debug.Log($"[FloorManager] 이벤트 전투 완료 처리 시작");

            // 일반 전투 보상 표시
            ShowBattleRewards(currentFloor, FloorType.Normal);

            // RewardManager 이벤트 구독 (이벤트 복귀용)
            if (Battle.RewardManager.Instance != null)
            {
                Battle.RewardManager.Instance.OnRewardsCompleted -= OnEventBattleRewardsCompleted;
                Battle.RewardManager.Instance.OnRewardsCompleted += OnEventBattleRewardsCompleted;
            }
        }

        /// <summary>
        /// 이벤트 전투 보상 완료 시 호출
        /// </summary>
        private void OnEventBattleRewardsCompleted()
        {
            Debug.Log("[FloorManager] 이벤트 전투 보상 완료 - 원래 이벤트로 복귀");

            // 이벤트 구독 해제
            if (Battle.RewardManager.Instance != null)
            {
                Battle.RewardManager.Instance.OnRewardsCompleted -= OnEventBattleRewardsCompleted;
            }

            // 원래 이벤트 복원
            if (savedEventForBattle != null)
            {
                currentEvent = savedEventForBattle;
                currentEventDisplay = savedEventForBattle;
            }

            // 이벤트 전투 모드 해제
            isEventBattleMode = false;
            savedEventForBattle = null;

            // 이벤트 씬으로 복귀
            ReturnToEventScene();
        }

        /// <summary>
        /// 이벤트 씬으로 복귀
        /// </summary>
        private async void ReturnToEventScene()
        {
            Debug.Log("[FloorManager] 이벤트 씬으로 복귀 시작 (새로운 로딩 시스템)");

            if (AdditiveSceneLoader.Instance != null)
            {
                await AdditiveSceneLoader.Instance.LoadSceneWithTransition(eventSceneName);
            }
            else
            {
                // 폴백: 기존 방식
                var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(eventSceneName);
                while (!asyncLoad.isDone)
                {
                    await Task.Yield();
                }
            }

            Debug.Log("[FloorManager] 이벤트 씬 복귀 완료");
        }

        /// <summary>
        /// 보상을 표시해야 하는 층인지 확인
        /// </summary>
        private bool ShouldShowRewards(FloorType floorType)
        {
            return floorType == FloorType.Normal ||
                   floorType == FloorType.Start ||
                   floorType == FloorType.Elite ||
                   floorType == FloorType.Boss;
        }

        /// <summary>
        /// 전투 보상 표시
        /// </summary>
        private void ShowBattleRewards(int floor, FloorType floorType)
        {
            Debug.Log($"[FloorManager] ShowBattleRewards 호출됨. floor: {floor}, floorType: {floorType}");

            if (Battle.RewardManager.Instance != null)
            {
                Debug.Log($"[FloorManager] RewardManager.Instance 존재, ShowBattleRewards 호출");
            }
            else
            {
                Debug.LogError($"[FloorManager] RewardManager.Instance가 null입니다!");
            }

            if (Battle.RewardManager.Instance != null)
            {
                // RewardManager 이벤트 구독 (일회성)
                Battle.RewardManager.Instance.OnRewardsCompleted -= OnBattleRewardsCompleted;
                Battle.RewardManager.Instance.OnRewardsCompleted += OnBattleRewardsCompleted;

                // 보상 표시
                Battle.RewardManager.Instance.ShowBattleRewards(floor, floorType);
            }
            else
            {
                Debug.LogWarning("RewardManager not found! Proceeding to next floor.");
                FinishFloorCompletion();
            }
        }

        /// <summary>
        /// 보상 선택 완료 시 호출
        /// </summary>
        private void OnBattleRewardsCompleted()
        {
            // 이벤트 구독 해제
            if (Battle.RewardManager.Instance != null)
            {
                Battle.RewardManager.Instance.OnRewardsCompleted -= OnBattleRewardsCompleted;
            }

            // 층 완료 마무리
            FinishFloorCompletion();
        }

        /// <summary>
        /// 층 완료 마무리 처리
        /// </summary>
        private void FinishFloorCompletion()
        {
            isProcessingFloor = false;
            isCurrentFloorEvent = false; // 이벤트 플래그 리셋

            // 현재 층 완료 시 SO들 초기화 (다음 층 준비)
            ClearCurrentSOs();

            // 다음 층으로 진행
            if (currentFloor < maxFloor)
            {
                Invoke(nameof(ProceedToNextFloor), 1f); // 1초 후 다음 층으로
            }
            else
            {
                Debug.Log("[FloorManager] Game completed! Reached max floor.");
            }
        }

        /// <summary>
        /// 다음 층으로 진행
        /// </summary>
        public void ProceedToNextFloor()
        {
            if (currentFloor >= maxFloor)
            {
                Debug.Log("[FloorManager] Maximum floor reached! Game completed.");

                // 게임 승리 처리
                Victory();

                OnGameReset?.Invoke();
                return;
            }

            currentFloor++;

            FloorInfo nextFloor = new FloorInfo(currentFloor, GetFloorType(currentFloor)) { isCurrent = true };
            floorHistory.Add(nextFloor);

            Debug.Log($"Proceeding to floor {currentFloor}: {nextFloor.floorName}");

            OnFloorChanged?.Invoke(currentFloor);
            OnFloorTypeChanged?.Invoke(CurrentFloorType);

            // 메인 게임 씬으로 복귀 후 새로운 층 시작
            ProceedToNextFloorSequence();
        }

        /// <summary>
        /// 다음 층 진행 시퀀스 (새로운 로딩 시스템)
        /// </summary>
        private void ProceedToNextFloorSequence()
        {
            // 메인 게임 씬으로 복귀하지 않고 바로 새로운 층 시작
            // AdditiveSceneLoader가 현재 씬을 자동으로 언로드하고 새로운 씬을 로드할 것임
            StartCurrentFloor();
        }

        /// <summary>
        /// 특정 층으로 리셋 (플레이어 사망 시)
        /// </summary>
        public void ResetToFloor(int targetFloor)
        {
            currentFloor = Mathf.Max(1, targetFloor);
            floorHistory.Clear();
            isProcessingFloor = false;

            // 현재 SO들 초기화 (새로운 층 시작)
            ClearCurrentSOs();

            // 리셋된 층 정보 추가
            FloorInfo resetFloor = new FloorInfo(currentFloor, GetFloorType(currentFloor)) { isCurrent = true };
            floorHistory.Add(resetFloor);

            Debug.Log($"[FloorManager] Game reset to floor {currentFloor}");
            OnGameReset?.Invoke();
            OnFloorChanged?.Invoke(currentFloor);
            OnFloorTypeChanged?.Invoke(CurrentFloorType);
        }

        /// <summary>
        /// 층 번호로 층 타입 결정
        /// </summary>
        private FloorType GetFloorType(int floor)
        {
            if (floor == 1)
                return FloorType.Start;

            // 보스층 우선 확인 (10층마다)
            if (floor % bossFloorInterval == 0)
                return FloorType.Boss;

            // 상점층 확인 (3층 클리어 후: 4층, 7층, 10층 등)
            // 3의 배수 + 1층에서 상점 등장 (단, 보스층이 아닌 경우)
            if ((floor - 1) % shopFloorInterval == 0 && floor > 1 && floor % bossFloorInterval != 0)
                return FloorType.Shop;

            // 엘리트층 확인 (5층마다, 단 보스층이나 상점층이 아닌 경우)
            if (floor % eliteFloorInterval == 0 && floor % bossFloorInterval != 0 &&
                (floor - 1) % shopFloorInterval != 0)
                return FloorType.Elite;

            // 기본은 일반 전투층 (이벤트 확률은 StartCurrentFloor에서 처리)
            return FloorType.Normal;
        }

        /// <summary>
        /// 전투 시작 - 전투 씬으로 전환
        /// </summary>
        private void StartBattle()
        {
            BattleStageSO selectedStage = GetBattleStageForFloor(currentFloor, CurrentFloorType);

            if (selectedStage != null)
            {
                if (debugMode)
                {
                    Debug.Log($"[FloorManager] Starting battle on floor {currentFloor}: {selectedStage.name} ({CurrentFloorType})");
                    Debug.Log($"[FloorManager] BattleTestController will handle BattleSO selection");
                }

                // 전투 상태로 전환
                ChangeGameState(GameState.Battle);

                // 모든 전투 데이터 로드 완료 후 자동 세이브 (BattleTestController에서 SO 설정 후 호출됨)

                // 전투 씬으로 전환
                LoadBattleScene(selectedStage);
            }
            else
            {
                Debug.LogError($"[FloorManager] No battle stage found for floor {currentFloor} type: {CurrentFloorType}");
                CompleteCurrentFloor();
            }
        }

        /// <summary>
        /// 상점 시작 - 상점 씬으로 전환
        /// </summary>
        private void StartShop()
        {
            if (debugMode)
                Debug.Log($"[FloorManager] Starting shop on floor {currentFloor}");

            // 상점 상태로 전환
            ChangeGameState(GameState.Shop);

            // 상점 데이터 로드 완료 후 자동 세이브
            OnFloorDataLoadCompleted();

            // 상점 씬으로 전환
            LoadShopScene();
        }

        /// <summary>
        /// 이벤트 시작 - 이벤트 씬으로 전환
        /// </summary>
        private void StartEvent()
        {
            // EventManager에게 이벤트 선택 위임
            if (Event.EventManager.Instance != null)
            {
                EventSO selectedEvent = Event.EventManager.Instance.SelectEventForFloor(currentFloor);

                if (selectedEvent != null)
                {
                    // 선택된 Event 저장
                    currentEvent = selectedEvent;
                    currentEventDisplay = selectedEvent; // 인스펙터 디스플레이 업데이트

                    if (debugMode)
                    {
                        Debug.Log($"[FloorManager] Starting event on floor {currentFloor}");
                        Debug.Log($"[FloorManager] Selected event: {selectedEvent.EventName}");
                    }

                    // 이벤트 상태로 전환
                    ChangeGameState(GameState.Event);

                    // 이벤트 데이터 로드 완료 후 자동 세이브
                    OnFloorDataLoadCompleted();

                    // 이벤트 씬으로 전환
                    LoadEventScene();
                }
                else
                {
                    Debug.LogWarning($"[FloorManager] No available events for floor {currentFloor}, falling back to battle");
                    StartBattle(); // 이벤트가 없으면 일반 전투로 폴백
                }
            }
            else
            {
                Debug.LogError("[FloorManager] EventManager.Instance가 null입니다. 전투로 폴백합니다.");
                StartBattle();
            }
        }

        // 이벤트 선택 로직은 EventManager로 이동됨

        /// <summary>
        /// 특정 층과 타입에 맞는 배틀 스테이지 가져오기 (BattleTestController에서 호출)
        /// </summary>
        public BattleStageSO GetBattleStageForFloor(int floor, FloorType floorType)
        {
            // 인스펙터에 등록된 스테이지 중에서 현재 층에 등장할 수 있는 스테이지 찾기
            BattleStageSO[] stagePool = GetStagePoolByFloorType(floorType);

            if (stagePool == null || stagePool.Length == 0)
            {
                Debug.LogWarning($"[FloorManager] No battle stages registered for floor type: {floorType}");
                return null;
            }

            // 현재 층에서 등장 가능한 스테이지 필터링
            var validStages = System.Array.FindAll(stagePool, stage =>
                stage != null && CanStageAppearOnFloor(stage, floor));

            if (validStages.Length == 0)
            {
                Debug.LogWarning($"[FloorManager] No valid battle stages found for floor {floor} type: {floorType}");
                return null;
            }

            // 첫 번째로 매칭되는 스테이지 반환 (추후 가중치 로직 추가 가능)
            var selectedStage = validStages[UnityEngine.Random.Range(0, validStages.Length)];
            Debug.Log($"[FloorManager] Selected battle stage for floor {floor}: {selectedStage.name}");
            return selectedStage;
        }

        /// <summary>
        /// 층 타입에 맞는 스테이지 풀 가져오기
        /// </summary>
        private BattleStageSO[] GetStagePoolByFloorType(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Elite => eliteBattleStages,
                FloorType.Boss => bossBattleStages,
                _ => normalBattleStages
            };
        }

        /// <summary>
        /// 특정 스테이지가 현재 층에서 등장할 수 있는지 확인
        /// BattleStageSO 내부의 모든 BattleSO가 현재 층에서 등장 가능한지 확인
        /// </summary>
        private bool CanStageAppearOnFloor(BattleStageSO stage, int floor)
        {
            if (stage == null || stage.AvailableBattles == null)
                return false;

            // BattleStageSO 내부의 BattleSO 중 하나라도 현재 층에서 등장 가능하면 true
            foreach (var battleSpawnData in stage.AvailableBattles)
            {
                if (battleSpawnData != null &&
                    floor >= battleSpawnData.minFloor &&
                    floor <= battleSpawnData.maxFloor)
                {
                    return true;
                }
            }

            return false;
        }

        #region Scene Loading
        /// <summary>
        /// 전투 씬 로드 (새로운 로딩 시스템)
        /// </summary>
        private async void LoadBattleScene(BattleStageSO battleStage)
        {
            Debug.Log($"[FloorManager] Loading battle scene: {battleSceneName} (새로운 로딩 시스템)");

            // 전투 스테이지 정보를 전역으로 저장 (BattleTestController가 접근할 수 있도록)
            SetCurrentBattleStage(battleStage);

            try
            {
                if (AdditiveSceneLoader.Instance != null)
                {
                    Debug.Log($"[FloorManager] AdditiveSceneLoader를 사용해서 {battleSceneName} 로드 중...");
                    // 전투 시작 전 추가 대기 시간 (몬스터 스폰 애니메이션 준비)
                    await AdditiveSceneLoader.Instance.LoadSceneWithTransition(battleSceneName, 0.3f);
                }
                else
                {
                    Debug.LogWarning("[FloorManager] AdditiveSceneLoader가 없어서 기존 방식으로 로드");
                    // 폴백: 기존 방식
                    var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(battleSceneName);

                    if (asyncLoad == null)
                    {
                        Debug.LogError($"[FloorManager] 씬을 로드할 수 없습니다: {battleSceneName}");
                        ReturnToMainMenu();
                        return;
                    }

                    while (!asyncLoad.isDone)
                    {
                        await Task.Yield();
                    }

                    // 씬 로딩 실패 확인
                    if (asyncLoad.isDone && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != battleSceneName)
                    {
                        Debug.LogError($"[FloorManager] 씬 로딩에 실패했습니다: {battleSceneName}");
                        ReturnToMainMenu();
                        return;
                    }
                }

                Debug.Log($"[FloorManager] Battle scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FloorManager] 전투 씬 로드 중 오류 발생: {e.Message}");
                ReturnToMainMenu();
            }
        }

        /// <summary>
        /// 상점 씬 로드 (새로운 로딩 시스템)
        /// </summary>
        private async void LoadShopScene()
        {
            Debug.Log($"[FloorManager] Loading shop scene: {shopSceneName} (새로운 로딩 시스템)");

            try
            {
                if (AdditiveSceneLoader.Instance != null)
                {
                    await AdditiveSceneLoader.Instance.LoadSceneWithTransition(shopSceneName);
                }
                else
                {
                    // 폴백: 기존 방식
                    var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(shopSceneName);
                    while (!asyncLoad.isDone)
                    {
                        await Task.Yield();
                    }
                }

                Debug.Log($"[FloorManager] Shop scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FloorManager] 상점 씬 로드 중 오류 발생: {e.Message}");
            }
        }

        /// <summary>
        /// 이벤트 씬 로드 (새로운 로딩 시스템)
        /// </summary>
        private async void LoadEventScene()
        {
            Debug.Log($"[FloorManager] Loading event scene: {eventSceneName} (새로운 로딩 시스템)");

            try
            {
                if (AdditiveSceneLoader.Instance != null)
                {
                    await AdditiveSceneLoader.Instance.LoadSceneWithTransition(eventSceneName);
                }
                else
                {
                    // 폴백: 기존 방식
                    var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(eventSceneName);
                    while (!asyncLoad.isDone)
                    {
                        await Task.Yield();
                    }
                }

                Debug.Log($"[FloorManager] Event scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FloorManager] 이벤트 씬 로드 중 오류 발생: {e.Message}");
            }
        }

        /// <summary>
        /// 메인 게임 씬으로 복귀 (새로운 로딩 시스템)
        /// </summary>
        public async void ReturnToMainGameScene()
        {
            Debug.Log($"[FloorManager] Returning to main game scene: {mainGameSceneName} (새로운 로딩 시스템)");

            try
            {
                if (AdditiveSceneLoader.Instance != null)
                {
                    await AdditiveSceneLoader.Instance.LoadSceneWithTransition(mainGameSceneName);
                }
                else
                {
                    // 폴백: 기존 방식
                    var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(mainGameSceneName);
                    while (!asyncLoad.isDone)
                    {
                        await Task.Yield();
                    }
                }

                Debug.Log($"[FloorManager] Main game scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FloorManager] 메인 게임 씬 복귀 중 오류 발생: {e.Message}");
            }
        }


        /// <summary>
        /// 현재 배틀 스테이지 가져오기 (BattleTestController에서 사용)
        /// </summary>
        public BattleStageSO GetCurrentBattleStage()
        {
            return currentBattleStage;
        }

        /// <summary>
        /// 현재 배틀 스테이지 설정 (전역 접근용)
        /// </summary>
        public void SetCurrentBattleStage(BattleStageSO battleStage)
        {
            currentBattleStage = battleStage;
            currentBattleStageDisplay = battleStage; // 인스펙터 디스플레이 업데이트
            if (debugMode)
                Debug.Log($"[FloorManager] Current BattleStage set: {battleStage?.name ?? "null"}");
        }

        /// <summary>
        /// 현재 이벤트 가져오기 (이벤트 시스템에서 사용)
        /// </summary>
        public EventSO GetCurrentEvent()
        {
            return currentEvent;
        }

        /// <summary>
        /// 현재 이벤트 설정
        /// </summary>
        public void SetCurrentEvent(EventSO eventSO)
        {
            currentEvent = eventSO;
            currentEventDisplay = eventSO; // 인스펙터 디스플레이 업데이트
            if (debugMode)
                Debug.Log($"[FloorManager] Current Event set: {eventSO?.EventName ?? "null"}");
        }

        /// <summary>
        /// 현재 전투 가져오기
        /// </summary>
        public BattleSO GetCurrentBattle()
        {
            return currentBattle;
        }

        /// <summary>
        /// 현재 전투 설정
        /// </summary>
        public void SetCurrentBattle(BattleSO battleSO)
        {
            currentBattle = battleSO;
            currentBattleDisplay = battleSO; // 인스펙터 디스플레이 업데이트
            if (debugMode)
                Debug.Log($"[FloorManager] Current Battle set: {battleSO?.BattleName ?? "null"}");

            // 전투 데이터 설정 완료 후 자동 세이브 (전투층인 경우에만)
            if (CurrentFloorType == FloorType.Normal || CurrentFloorType == FloorType.Elite ||
                CurrentFloorType == FloorType.Boss || CurrentFloorType == FloorType.Start)
            {
                OnFloorDataLoadCompleted();
            }
        }

        /// <summary>
        /// 현재 층 SO 정보 모두 초기화
        /// </summary>
        public void ClearCurrentSOs()
        {
            currentBattleStage = null;
            currentBattle = null;
            currentEvent = null;

            // 인스펙터 디스플레이도 초기화
            currentBattleStageDisplay = null;
            currentBattleDisplay = null;
            currentEventDisplay = null;

            // 이벤트 전투 모드 초기화
            isEventBattleMode = false;
            savedEventForBattle = null;

            if (debugMode)
                Debug.Log("[FloorManager] All current SOs cleared");
        }

        /// <summary>
        /// 이벤트 전투 모드 설정
        /// </summary>
        public void SetEventBattleMode(bool isEventBattle, EventSO eventToSave = null)
        {
            isEventBattleMode = isEventBattle;
            savedEventForBattle = eventToSave;

            if (debugMode)
                Debug.Log($"[FloorManager] 이벤트 전투 모드: {(isEventBattle ? "활성화" : "비활성화")}");
        }
        #endregion

        #region Save System Integration
        /// <summary>
        /// 자동 세이브 활성화/비활성화
        /// </summary>
        public void SetAutoSaveEnabled(bool enabled)
        {
            autoSaveEnabled = enabled;

            if (debugMode)
                Debug.Log($"[FloorManager] 자동 세이브: {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 현재 층 데이터가 모두 로드된 후 자동 세이브 실행
        /// </summary>
        private void TriggerAutoSave()
        {
            if (!autoSaveEnabled)
            {
                if (debugMode)
                    Debug.Log("[FloorManager] 자동 세이브가 비활성화되어 있습니다.");
                return;
            }

            if (SaveManager.Instance != null)
            {
                bool saveResult = SaveManager.Instance.SaveGame();

                if (debugMode)
                {
                    if (saveResult)
                        Debug.Log($"[FloorManager] 자동 세이브 완료: {currentFloor}층 ({CurrentFloorType})");
                    else
                        Debug.LogWarning("[FloorManager] 자동 세이브 실패");
                }
            }
            else
            {
                Debug.LogWarning("[FloorManager] SaveManager.Instance가 null입니다. 자동 세이브를 건너뜁니다.");
            }
        }

        /// <summary>
        /// 층 시작 시 모든 SO 데이터 로드 완료 후 호출
        /// </summary>
        private void OnFloorDataLoadCompleted()
        {
            if (debugMode)
                Debug.Log($"[FloorManager] {currentFloor}층 데이터 로드 완료 - 자동 세이브 실행");

            // 모든 층 데이터가 로드된 후 자동 세이브
            TriggerAutoSave();
        }
        #endregion

        /// <summary>
        /// 층 정보 리스트 반환
        /// </summary>
        public List<FloorInfo> GetFloorHistory()
        {
            return new List<FloorInfo>(floorHistory);
        }

        /// <summary>
        /// 특정 층까지의 진행률 반환
        /// </summary>
        public float GetProgressPercentage()
        {
            return (float)currentFloor / maxFloor * 100f;
        }



        /// <summary>
        /// 층별 규칙 설정
        /// </summary>
        public void SetFloorRules(int eliteInterval, int shopInterval, int bossInterval)
        {
            eliteFloorInterval = Mathf.Max(1, eliteInterval);
            shopFloorInterval = Mathf.Max(1, shopInterval);
            bossFloorInterval = Mathf.Max(1, bossInterval);
        }

        /// <summary>
        /// 최대 층 설정
        /// </summary>
        public void SetMaxFloor(int max)
        {
            maxFloor = Mathf.Max(1, max);
        }

        /// <summary>
        /// 현재 진행 중인 층이 있는지 확인
        /// </summary>
        public bool IsProcessingFloor()
        {
            return isProcessingFloor;
        }

        /// <summary>
        /// 다음 층 타입 미리보기
        /// </summary>
        public FloorType GetNextFloorType()
        {
            return GetFloorType(currentFloor + 1);
        }

        /// <summary>
        /// 외부에서 층 완료 호출 (전투, 상점, 이벤트 완료 시)
        /// </summary>
        public void OnContentCompleted()
        {
            StartCoroutine(HandleContentCompletion());
        }

        /// <summary>
        /// 컨텐츠 완료 후 다음 층 진행 처리
        /// </summary>
        private System.Collections.IEnumerator HandleContentCompletion()
        {
            Debug.Log($"[FloorManager] {CurrentFloorType} 컨텐츠 완료, 다음 층으로 진행");

            // 현재 층 완료 처리
            CompleteCurrentFloor();

            // 잠시 대기 (FinishFloorCompletion에서 ProceedToNextFloor가 호출됨)
            yield return new WaitForSeconds(0.5f);
        }

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Floor Manager Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== FloorManager Debug Info ===");
            Debug.Log($"Game State: {currentState}");
            Debug.Log($"Previous State: {previousState}");
            Debug.Log($"Current Floor: {currentFloor} ({CurrentFloorType})");
            Debug.Log($"Is Initialized: {isGameInitialized}");
            Debug.Log($"Is Transitioning: {isTransitioning}");
            Debug.Log($"Is Processing Floor: {isProcessingFloor}");
            Debug.Log($"Debug Mode: {debugMode}");
            Debug.Log($"Current BattleStage: {currentBattleStage?.name ?? "null"}");
            Debug.Log($"Current Battle: {currentBattle?.BattleName ?? "null"}");
            Debug.Log($"Current Event: {currentEvent?.EventName ?? "null"}");
        }
        #endregion
    }
}