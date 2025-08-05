using UnityEngine;
using UnityEngine.SceneManagement;
using System;

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
        Victory,        // 승리
        Loading         // 로딩
    }

    /// <summary>
    /// 게임 전체 상태와 흐름을 관리하는 중앙 매니저
    /// MonoBehaviour 기반 싱글톤으로 구현
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();

                    if (_instance == null)
                    {
                        GameObject gameManagerObject = new GameObject("GameManager");
                        _instance = gameManagerObject.AddComponent<GameManager>();
                        DontDestroyOnLoad(gameManagerObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 게임 상태 변경 이벤트
        /// </summary>
        public static event Action<GameState, GameState> OnGameStateChanged;

        /// <summary>
        /// 층 변경 이벤트
        /// </summary>
        public static event Action<int> OnFloorChanged;

        /// <summary>
        /// 게임 초기화 완료 이벤트
        /// </summary>
        public static event Action OnGameInitialized;
        #endregion

        #region Fields
        [Header("게임 상태")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        [SerializeField] private GameState previousState = GameState.MainMenu;

        [Header("층 정보")]
        [SerializeField] private int currentFloor = 1;
        [SerializeField] private int maxFloor = 100;

        [Header("게임 설정")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private float transitionDelay = 0.5f;

        // 게임 진행 관련
        private bool isGameInitialized = false;
        private bool isTransitioning = false;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 게임 상태
        /// </summary>
        public GameState CurrentState => currentState;

        /// <summary>
        /// 이전 게임 상태
        /// </summary>
        public GameState PreviousState => previousState;

        /// <summary>
        /// 현재 층
        /// </summary>
        public int CurrentFloor => currentFloor;

        /// <summary>
        /// 최대 층
        /// </summary>
        public int MaxFloor => maxFloor;

        /// <summary>
        /// 디버그 모드 여부
        /// </summary>
        public bool DebugMode => debugMode;

        /// <summary>
        /// 게임이 초기화되었는지 여부
        /// </summary>
        public bool IsGameInitialized => isGameInitialized;

        /// <summary>
        /// 현재 화면 전환 중인지 여부
        /// </summary>
        public bool IsTransitioning => isTransitioning;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGame();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (_instance == this)
            {
                // 게임 시작 시 초기 상태 설정
                ChangeGameState(GameState.MainMenu);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Game Initialization
        /// <summary>
        /// 게임 초기화
        /// </summary>
        private void InitializeGame()
        {
            if (debugMode)
                Debug.Log("[GameManager] 게임 초기화 시작");

            // 기본 설정 초기화
            currentFloor = 1;
            currentState = GameState.Loading;
            previousState = GameState.Loading;
            isTransitioning = false;

            // 초기화 완료 표시
            isGameInitialized = true;
            OnGameInitialized?.Invoke();

            if (debugMode)
                Debug.Log("[GameManager] 게임 초기화 완료");
        }

        /// <summary>
        /// 새 게임 시작
        /// </summary>
        public void StartNewGame()
        {
            if (debugMode)
                Debug.Log("[GameManager] 새 게임 시작");

            // 게임 상태 초기화
            currentFloor = 1;

            // FloorManager를 통한 게임 시작
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.StartGame();
            }
            else
            {
                // 기존 방식으로 폴백
                AdvanceToNextFloor();
            }
        }

        /// <summary>
        /// 게임 재시작 (로그라이크 특성 - 모든 진행상황 초기화)
        /// </summary>
        public void RestartGame()
        {
            if (debugMode)
                Debug.Log("[GameManager] 게임 재시작 (로그라이크)");

            // 모든 진행상황 초기화
            currentFloor = 1;

            // FloorManager 리셋
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.ResetToFloor(1);
            }

            // 플레이어 상태 초기화 (다른 매니저들에게 알림)
            // TODO: PlayerManager 구현 후 연결

            // 새 게임 시작
            StartNewGame();
        }
        #endregion

        #region State Management
        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        /// <param name="newState">새로운 게임 상태</param>
        public void ChangeGameState(GameState newState)
        {
            if (isTransitioning)
            {
                if (debugMode)
                    Debug.LogWarning("[GameManager] 이미 화면 전환 중입니다.");
                return;
            }

            if (currentState == newState)
            {
                if (debugMode)
                    Debug.LogWarning($"[GameManager] 이미 {newState} 상태입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[GameManager] 게임 상태 변경: {currentState} -> {newState}");

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
                Debug.Log($"[GameManager] 상태 전환 완료: {newState}");
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

                case GameState.Loading:
                    // TODO: 로딩 화면 표시
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

                case GameState.Loading:
                    // TODO: 로딩 화면 숨김
                    break;
            }
        }
        #endregion

        #region Floor Management
        /// <summary>
        /// 다음 층으로 진행
        /// </summary>
        public void AdvanceToNextFloor()
        {
            if (currentFloor >= maxFloor)
            {
                // 최대 층 도달 시 승리 처리
                ChangeGameState(GameState.Victory);
                return;
            }

            currentFloor++;
            OnFloorChanged?.Invoke(currentFloor);

            if (debugMode)
                Debug.Log($"[GameManager] {currentFloor}층으로 진행");

            // 층 타입에 따라 적절한 상태로 전환
            DetermineFloorState(currentFloor);
        }

        /// <summary>
        /// 층 타입에 따른 게임 상태 결정
        /// </summary>
        private void DetermineFloorState(int floor)
        {
            // PRD에 따른 층 구성 규칙
            if (floor % 10 == 0)
            {
                // 10층 단위: 보스층
                ChangeGameState(GameState.Battle);
            }
            else if (floor % 5 == 0)
            {
                // 5층 단위: 엘리트층
                ChangeGameState(GameState.Battle);
            }
            else if ((floor - 1) % 3 == 2) // 3.5, 6.5, 9.5층 등
            {
                // 3층 단위: 상점층
                ChangeGameState(GameState.Shop);
            }
            else
            {
                // 일반층 또는 이벤트층 (랜덤)
                // TODO: 이벤트 발생 확률 계산 후 결정
                float eventChance = 0.1f; // 10% 확률
                if (UnityEngine.Random.Range(0f, 1f) <= eventChance)
                {
                    ChangeGameState(GameState.Event);
                }
                else
                {
                    ChangeGameState(GameState.Battle);
                }
            }
        }

        /// <summary>
        /// 특정 층으로 이동 (디버그용)
        /// </summary>
        public void SetFloor(int targetFloor)
        {
            if (!debugMode)
            {
                Debug.LogWarning("[GameManager] SetFloor는 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            if (targetFloor < 1 || targetFloor > maxFloor)
            {
                Debug.LogError($"[GameManager] 잘못된 층 번호: {targetFloor}");
                return;
            }

            currentFloor = targetFloor;
            OnFloorChanged?.Invoke(currentFloor);

            Debug.Log($"[GameManager] 디버그: {currentFloor}층으로 이동");
        }
        #endregion

        #region Game End
        /// <summary>
        /// 게임 오버 처리
        /// </summary>
        public void GameOver()
        {
            if (debugMode)
                Debug.Log("[GameManager] 게임 오버");

            ChangeGameState(GameState.GameOver);
        }

        /// <summary>
        /// 게임 승리 처리
        /// </summary>
        public void Victory()
        {
            if (debugMode)
                Debug.Log("[GameManager] 게임 승리");

            ChangeGameState(GameState.Victory);
        }

        /// <summary>
        /// 메인 메뉴로 돌아가기
        /// </summary>
        public void ReturnToMainMenu()
        {
            if (debugMode)
                Debug.Log("[GameManager] 메인 메뉴로 돌아가기");

            ChangeGameState(GameState.MainMenu);
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== GameManager Debug Info ===");
            Debug.Log($"Current State: {currentState}");
            Debug.Log($"Previous State: {previousState}");
            Debug.Log($"Current Floor: {currentFloor}");
            Debug.Log($"Is Initialized: {isGameInitialized}");
            Debug.Log($"Is Transitioning: {isTransitioning}");
            Debug.Log($"Debug Mode: {debugMode}");
        }
        #endregion
    }
}