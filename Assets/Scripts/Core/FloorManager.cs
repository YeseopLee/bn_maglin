using UnityEngine;
using System.Collections.Generic;
using Maglin.Event;
using Maglin.Shop;
using Maglin.Battle;

namespace Maglin.Core
{
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
    /// 층 진행 및 게임 플로우를 관리하는 매니저 클래스
    /// </summary>
    public class FloorManager : MonoBehaviour
    {
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

        // 싱글톤
        public static FloorManager Instance { get; private set; }

        // 이벤트
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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFloorSystem();
            }
            else
            {
                Destroy(gameObject);
            }
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

            Debug.Log($"Floor system initialized. Starting at floor {currentFloor}");
        }

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
            Debug.Log($"[FloorManager] CompleteCurrentFloor 호출됨. isProcessingFloor: {isProcessingFloor}");

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

            // 다음 층으로 진행
            if (currentFloor < maxFloor)
            {
                Invoke(nameof(ProceedToNextFloor), 1f); // 1초 후 다음 층으로
            }
            else
            {
                Debug.Log("Game completed! Reached max floor.");
            }
        }

        /// <summary>
        /// 다음 층으로 진행
        /// </summary>
        public void ProceedToNextFloor()
        {
            if (currentFloor >= maxFloor)
            {
                Debug.Log("Maximum floor reached! Game completed.");

                // 게임 승리 처리
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Victory();
                }

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
            StartCoroutine(ProceedToNextFloorSequence());
        }

        /// <summary>
        /// 다음 층 진행 시퀀스
        /// </summary>
        private System.Collections.IEnumerator ProceedToNextFloorSequence()
        {
            // 메인 게임 씬으로 복귀
            yield return StartCoroutine(ReturnToMainGameScene());

            // 잠시 대기 후 새로운 층 시작
            yield return new WaitForSeconds(0.5f);

            // 새로운 층 시작
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

            // 리셋된 층 정보 추가
            FloorInfo resetFloor = new FloorInfo(currentFloor, GetFloorType(currentFloor)) { isCurrent = true };
            floorHistory.Add(resetFloor);

            Debug.Log($"Game reset to floor {currentFloor}");
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
                Debug.Log($"Starting battle on floor {currentFloor}: {selectedStage.name} ({CurrentFloorType})");

                // GameManager에게 전투 상태로 전환 요청
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeGameState(GameState.Battle);
                }

                // 전투 씬으로 전환
                StartCoroutine(LoadBattleScene(selectedStage));
            }
            else
            {
                Debug.LogError($"No battle stage found for floor {currentFloor} type: {CurrentFloorType}");
                CompleteCurrentFloor();
            }
        }

        /// <summary>
        /// 상점 시작 - 상점 씬으로 전환
        /// </summary>
        private void StartShop()
        {
            Debug.Log($"Starting shop on floor {currentFloor}");

            // GameManager에게 상점 상태로 전환 요청
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeGameState(GameState.Shop);
            }

            // 상점 씬으로 전환
            StartCoroutine(LoadShopScene());
        }

        /// <summary>
        /// 이벤트 시작 - 이벤트 씬으로 전환
        /// </summary>
        private void StartEvent()
        {
            Debug.Log($"[FloorManager] Starting event on floor {currentFloor}");

            // GameManager에게 이벤트 상태로 전환 요청
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeGameState(GameState.Event);
            }

            // 이벤트 씬으로 전환
            StartCoroutine(LoadEventScene());
        }

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
            var selectedStage = validStages[Random.Range(0, validStages.Length)];
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
        /// 전투 씬 로드
        /// </summary>
        private System.Collections.IEnumerator LoadBattleScene(BattleStageSO battleStage)
        {
            Debug.Log($"[FloorManager] Loading battle scene: {battleSceneName}");

            // 전투 스테이지 정보를 전역으로 저장 (BattleTestController가 접근할 수 있도록)
            SetCurrentBattleStage(battleStage);

            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(battleSceneName);

            if (asyncLoad == null)
            {
                Debug.LogError($"[FloorManager] 씬을 로드할 수 없습니다: {battleSceneName}. Build Settings에 씬이 추가되었는지 확인하세요.");

                // 오류 발생 시 메인 메뉴로 복귀
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ReturnToMainMenu();
                }
                yield break;
            }

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // 씬 로딩이 실패했는지 확인
            if (asyncLoad.isDone && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != battleSceneName)
            {
                Debug.LogError($"[FloorManager] 씬 로딩에 실패했습니다: {battleSceneName}");

                // 오류 발생 시 메인 메뉴로 복귀
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ReturnToMainMenu();
                }
                yield break;
            }

            Debug.Log($"[FloorManager] Battle scene loaded successfully");
        }

        /// <summary>
        /// 상점 씬 로드
        /// </summary>
        private System.Collections.IEnumerator LoadShopScene()
        {
            Debug.Log($"[FloorManager] Loading shop scene: {shopSceneName}");

            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(shopSceneName);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Debug.Log($"[FloorManager] Shop scene loaded successfully");
        }

        /// <summary>
        /// 이벤트 씬 로드
        /// </summary>
        private System.Collections.IEnumerator LoadEventScene()
        {
            Debug.Log($"[FloorManager] Loading event scene: {eventSceneName}");

            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(eventSceneName);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Debug.Log($"[FloorManager] Event scene loaded successfully");
        }

        /// <summary>
        /// 메인 게임 씬으로 복귀
        /// </summary>
        public System.Collections.IEnumerator ReturnToMainGameScene()
        {
            Debug.Log($"[FloorManager] Returning to main game scene: {mainGameSceneName}");

            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(mainGameSceneName);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Debug.Log($"[FloorManager] Main game scene loaded successfully");
        }

        /// <summary>
        /// 현재 배틀 스테이지 설정 (전역 접근용)
        /// </summary>
        private BattleStageSO currentBattleStage;

        private void SetCurrentBattleStage(BattleStageSO battleStage)
        {
            currentBattleStage = battleStage;
        }

        /// <summary>
        /// 현재 배틀 스테이지 가져오기 (BattleTestController에서 사용)
        /// </summary>
        public BattleStageSO GetCurrentBattleStage()
        {
            return currentBattleStage;
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
    }
}