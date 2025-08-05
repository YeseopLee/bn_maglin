using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Maglin.Core;
using Maglin.Player;
using Maglin.Cards;
using Maglin.Battle;
using Maglin.Shop;
using Maglin.Event;
using Maglin.Relics;
using Maglin.Audio;
using Core;

namespace Maglin.Core
{
    /// <summary>
    /// 메인 게임의 중앙 컨트롤러
    /// 게임 시작, 매니저 초기화, 전체 게임 플로우 관리
    /// </summary>
    public class MainGameController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Button exitButton;

        [Header("Manager Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject floorManagerPrefab;
        [SerializeField] private GameObject playerManagerPrefab;
        [SerializeField] private GameObject cardManagerPrefab;
        [SerializeField] private GameObject audioManagerPrefab;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool skipToFloor = false;
        [SerializeField] private int skipFloorNumber = 1;

        private bool isInitialized = false;

        #region Unity Events
        private void Start()
        {
            InitializeMainGame();
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 정리
            if (startButton != null)
                startButton.onClick.RemoveAllListeners();
            if (exitButton != null)
                exitButton.onClick.RemoveAllListeners();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 메인 게임 초기화
        /// </summary>
        private void InitializeMainGame()
        {
            if (debugMode)
                Debug.Log("[MainGameController] 메인 게임 초기화 시작");

            // UI 설정
            SetupUI();

            // 핵심 매니저들 초기화
            InitializeCoreManagers();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[MainGameController] 메인 게임 초기화 완료");
        }

        /// <summary>
        /// UI 설정
        /// </summary>
        private void SetupUI()
        {
            // 시작 버튼 자동 찾기
            if (startButton == null)
            {
                var startButtonObj = GameObject.Find("StartButton");
                if (startButtonObj != null)
                    startButton = startButtonObj.GetComponent<Button>();
            }

            // 로딩 패널 자동 찾기
            if (loadingPanel == null)
            {
                var loadingPanelObj = GameObject.Find("LoadingPanel");
                if (loadingPanelObj != null)
                    loadingPanel = loadingPanelObj;
            }

            // 버튼 이벤트 연결
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartGameClicked);
                if (debugMode)
                    Debug.Log("[MainGameController] 시작 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogError("[MainGameController] 시작 버튼을 찾을 수 없습니다!");
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitGameClicked);
            }

            // 로딩 패널 숨김
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// 핵심 매니저들 초기화 (DontDestroyOnLoad)
        /// </summary>
        private void InitializeCoreManagers()
        {
            // GameManager 초기화 (가장 먼저)
            if (GameManager.Instance == null)
            {
                if (gameManagerPrefab != null)
                {
                    var gameManagerObj = Instantiate(gameManagerPrefab);
                    gameManagerObj.name = "GameManager";
                    DontDestroyOnLoad(gameManagerObj);
                }
                else
                {
                    var gameManagerObj = new GameObject("GameManager");
                    gameManagerObj.AddComponent<GameManager>();
                    DontDestroyOnLoad(gameManagerObj);
                }

                if (debugMode)
                    Debug.Log("[MainGameController] GameManager 생성 완료");
            }

            // FloorManager 초기화
            if (FloorManager.Instance == null)
            {
                if (floorManagerPrefab != null)
                {
                    var floorManagerObj = Instantiate(floorManagerPrefab);
                    floorManagerObj.name = "FloorManager";
                    DontDestroyOnLoad(floorManagerObj);
                }
                else
                {
                    var floorManagerObj = new GameObject("FloorManager");
                    floorManagerObj.AddComponent<FloorManager>();
                    DontDestroyOnLoad(floorManagerObj);
                }

                if (debugMode)
                    Debug.Log("[MainGameController] FloorManager 생성 완료");
            }

            // PlayerManager 초기화
            if (PlayerManager.Instance == null)
            {
                if (playerManagerPrefab != null)
                {
                    var playerManagerObj = Instantiate(playerManagerPrefab);
                    playerManagerObj.name = "PlayerManager";
                    DontDestroyOnLoad(playerManagerObj);
                }
                else
                {
                    var playerManagerObj = new GameObject("PlayerManager");
                    playerManagerObj.AddComponent<PlayerManager>();
                    DontDestroyOnLoad(playerManagerObj);
                }

                if (debugMode)
                    Debug.Log("[MainGameController] PlayerManager 생성 완료");
            }

            // CardManager 초기화
            if (CardManager.Instance == null)
            {
                if (cardManagerPrefab != null)
                {
                    var cardManagerObj = Instantiate(cardManagerPrefab);
                    cardManagerObj.name = "CardManager";
                    DontDestroyOnLoad(cardManagerObj);
                }
                else
                {
                    var cardManagerObj = new GameObject("CardManager");
                    cardManagerObj.AddComponent<CardManager>();
                    DontDestroyOnLoad(cardManagerObj);
                }

                if (debugMode)
                    Debug.Log("[MainGameController] CardManager 생성 완료");
            }

            // AudioManager 초기화
            if (AudioManager.Instance == null)
            {
                if (audioManagerPrefab != null)
                {
                    var audioManagerObj = Instantiate(audioManagerPrefab);
                    audioManagerObj.name = "AudioManager";
                    DontDestroyOnLoad(audioManagerObj);
                }
                else
                {
                    var audioManagerObj = new GameObject("AudioManager");
                    audioManagerObj.AddComponent<AudioManager>();
                    DontDestroyOnLoad(audioManagerObj);
                }

                if (debugMode)
                    Debug.Log("[MainGameController] AudioManager 생성 완료");
            }

            // RelicManager 초기화
            if (RelicManager.Instance == null)
            {
                var relicManagerObj = new GameObject("RelicManager");
                relicManagerObj.AddComponent<RelicManager>();
                DontDestroyOnLoad(relicManagerObj);

                if (debugMode)
                    Debug.Log("[MainGameController] RelicManager 생성 완료");
            }

            if (debugMode)
                Debug.Log("[MainGameController] 모든 핵심 매니저 초기화 완료");
        }
        #endregion

        #region Button Events
        /// <summary>
        /// 게임 시작 버튼 클릭 이벤트
        /// </summary>
        public async void OnStartGameClicked()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[MainGameController] 아직 초기화가 완료되지 않았습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[MainGameController] 게임 시작 버튼 클릭됨");

            startButton.interactable = false;
            await StartGameSequence();
        }

        /// <summary>
        /// 게임 종료 버튼 클릭 이벤트
        /// </summary>
        public void OnExitGameClicked()
        {
            if (debugMode)
                Debug.Log("[MainGameController] 게임 종료");

            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        #endregion

        #region Game Flow
        /// <summary>
        /// 게임 시작 시퀀스 (개선된 로딩 및 전환 효과 포함)
        /// </summary>
        private async Task StartGameSequence()
        {
            try
            {
                // 1. 로딩 화면 표시
                if (LoadingManager.Instance != null)
                {
                    await LoadingManager.Instance.ShowGamePreparationLoading();
                    LoadingManager.Instance.UpdateToGameManagerCheck();
                }

                await Task.Delay(300); // 짧은 로딩 시간

                // 2. GameManager 확인 및 게임 시작 준비
                if (GameManager.Instance == null)
                {
                    Debug.LogError("[MainGameController] GameManager가 없어서 게임을 시작할 수 없습니다!");
                    
                    if (LoadingManager.Instance != null)
                    {
                        await LoadingManager.Instance.HideLoading();
                    }
                    
                    startButton.interactable = true;
                    return;
                }

                if (LoadingManager.Instance != null)
                {
                    LoadingManager.Instance.UpdateToGameDataInit();
                }

                // 3. 디버그 설정 적용
                if (skipToFloor && debugMode)
                {
                    GameManager.Instance.SetFloor(skipFloorNumber);
                }

                await Task.Delay(400);

                if (LoadingManager.Instance != null)
                {
                    LoadingManager.Instance.UpdateToBattlePreparation();
                }

                await Task.Delay(300);

                if (LoadingManager.Instance != null)
                {
                    LoadingManager.Instance.UpdateToGameStart();
                }

                await Task.Delay(200);

                // 4. 로딩 화면 숨기기
                if (LoadingManager.Instance != null)
                {
                    await LoadingManager.Instance.HideLoading();
                }

                // 5. 씬 전환과 함께 게임 시작
                if (SceneTransitionManager.Instance != null)
                {
                    // 페이드 효과와 함께 전투 씬으로 전환
                    string battleSceneName = FloorManager.Instance?.BattleSceneName ?? "TestBattleScene";
                    await SceneTransitionManager.Instance.TransitionToScene(battleSceneName, 0.8f, 1.0f);
                }
                else
                {
                    // 기존 방식으로 게임 시작 (백업)
                    GameManager.Instance.StartNewGame();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainGameController] 게임 시작 중 오류 발생: {e.Message}");
                
                // 로딩 화면 정리
                if (LoadingManager.Instance != null)
                {
                    await LoadingManager.Instance.HideLoading();
                }
                
                // 버튼 다시 활성화
                startButton.interactable = true;
            }
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그용 메뉴들
        /// </summary>
        [ContextMenu("Force Start Game")]
        public void ForceStartGame()
        {
            OnStartGameClicked();
        }

        [ContextMenu("Test Floor 3")]
        public void TestFloor3()
        {
            skipToFloor = true;
            skipFloorNumber = 3;
            OnStartGameClicked();
        }

        [ContextMenu("Test Floor 5")]
        public void TestFloor5()
        {
            skipToFloor = true;
            skipFloorNumber = 5;
            OnStartGameClicked();
        }

        [ContextMenu("Print Manager Status")]
        public void PrintManagerStatus()
        {
            Debug.Log("=== Manager Status ===");
            Debug.Log($"GameManager: {(GameManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"FloorManager: {(FloorManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"PlayerManager: {(PlayerManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"CardManager: {(CardManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"AudioManager: {(AudioManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"RelicManager: {(RelicManager.Instance != null ? "✓" : "✗")}");
        }
        #endregion
    }
}