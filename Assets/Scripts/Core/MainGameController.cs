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
        [SerializeField] private Button optionButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject settingsOverlay;

        [Header("Manager Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject floorManagerPrefab;
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
            if (optionButton != null)
                optionButton.onClick.RemoveAllListeners();
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
            // 버튼 자동 찾기
            if (startButton == null)
            {
                var startButtonObj = GameObject.Find("StartButton");
                if (startButtonObj != null)
                    startButton = startButtonObj.GetComponent<Button>();
            }

            if (optionButton == null)
            {
                var optionButtonObj = GameObject.Find("OptionButton");
                if (optionButtonObj != null)
                    optionButton = optionButtonObj.GetComponent<Button>();
            }

            // 설정 오버레이 자동 찾기
            if (settingsOverlay == null)
            {
                settingsOverlay = GameObject.Find("SettingsOverlay");
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

            if (optionButton != null)
            {
                optionButton.onClick.AddListener(OnOptionButtonClicked);
                if (debugMode)
                    Debug.Log("[MainGameController] 옵션 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogError("[MainGameController] 옵션 버튼을 찾을 수 없습니다!");
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitGameClicked);
            }

            // 설정 오버레이 초기 상태 설정
            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// 핵심 매니저들 초기화 (DontDestroyOnLoad)
        /// </summary>
        private void InitializeCoreManagers()
        {
            // SaveManager 초기화 (가장 먼저)
            if (SaveManager.Instance == null)
            {
                var saveManagerObj = new GameObject("SaveManager");
                saveManagerObj.AddComponent<SaveManager>();
                DontDestroyOnLoad(saveManagerObj);

                if (debugMode)
                    Debug.Log("[MainGameController] SaveManager 생성 완료");
            }

            // FloorManager 초기화
            if (FloorManager.Instance == null)
            {
                // FloorManager는 씬에 이미 있어야 함
                var floorManager = FindObjectOfType<FloorManager>();
                if (floorManager == null)
                {
                    Debug.LogError("[MainGameController] FloorManager를 찾을 수 없습니다!");
                }
                else
                {
                    if (debugMode)
                        Debug.Log("[MainGameController] FloorManager 확인됨");
                }
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

            // PlayerManager는 씬에 직접 배치되어 있으므로 자동 생성하지 않음
            if (debugMode && PlayerManager.Instance != null)
                Debug.Log("[MainGameController] PlayerManager 확인됨 (씬에서 로드)");

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

            // LanguageManager 초기화
            if (LanguageManager.Instance == null)
            {
                var languageManagerObj = new GameObject("LanguageManager");
                languageManagerObj.AddComponent<LanguageManager>();
                DontDestroyOnLoad(languageManagerObj);

                if (debugMode)
                    Debug.Log("[MainGameController] LanguageManager 생성 완료");
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
        /// 옵션 버튼 클릭 이벤트
        /// </summary>
        public void OnOptionButtonClicked()
        {
            if (debugMode)
                Debug.Log("[MainGameController] 옵션 버튼 클릭됨");

            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(true);
            }
            else
            {
                Debug.LogError("[MainGameController] 설정 오버레이를 찾을 수 없습니다!");
            }
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
        /// 게임 시작 시퀀스 (단순 씬 전환으로 변경)
        /// </summary>
        private async Task StartGameSequence()
        {
            try
            {
                // 1. FloorManager와 SaveManager 확인
                if (FloorManager.Instance == null)
                {
                    Debug.LogError("[MainGameController] FloorManager가 없어서 게임을 시작할 수 없습니다!");
                    startButton.interactable = true;
                    return;
                }

                if (SaveManager.Instance == null)
                {
                    Debug.LogError("[MainGameController] SaveManager가 없어서 게임을 시작할 수 없습니다!");
                    startButton.interactable = true;
                    return;
                }

                // 2. 세이브 데이터 처리
                bool hasExistingSave = SaveManager.Instance.HasSaveFile;
                GameSaveData saveData = null;

                if (hasExistingSave && !skipToFloor)
                {
                    // 기존 세이브 파일 로드
                    saveData = SaveManager.Instance.LoadGame();

                    if (saveData != null)
                    {
                        if (debugMode)
                            Debug.Log($"[MainGameController] 기존 세이브 로드: {saveData.currentFloor}층");

                        // 세이브 데이터를 게임에 적용
                        SaveManager.Instance.ApplySaveDataToGame(saveData);
                    }
                    else
                    {
                        Debug.LogWarning("[MainGameController] 세이브 로드 실패, 새 게임 시작");
                        hasExistingSave = false;
                    }
                }
                else if (!hasExistingSave)
                {
                    // 새 세이브 파일 생성
                    SaveManager.Instance.CreateNewSaveFile();
                    if (debugMode)
                        Debug.Log("[MainGameController] 새 세이브 파일 생성");
                }

                // 3. 디버그 설정 적용 (세이브보다 우선)
                if (skipToFloor && debugMode)
                {
                    FloorManager.Instance.SetFloor(skipFloorNumber);
                    if (debugMode)
                        Debug.Log($"[MainGameController] 디버그 모드: {skipFloorNumber}층으로 건너뜀");
                }

                // 4. 게임 시작 방식 결정
                if (hasExistingSave && saveData != null && !skipToFloor)
                {
                    // 세이브된 층에서 이어서 시작
                    if (debugMode)
                        Debug.Log($"[MainGameController] 저장된 게임 이어하기: {saveData.currentFloor}층");

                    FloorManager.Instance.StartCurrentFloor();
                }
                else
                {
                    // 새 게임 시작
                    if (debugMode)
                        Debug.Log("[MainGameController] 새 게임 시작");

                    FloorManager.Instance.StartNewGame();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainGameController] 게임 시작 중 오류 발생: {e.Message}");
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
            Debug.Log($"SaveManager: {(SaveManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"FloorManager: {(FloorManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"PlayerManager: {(PlayerManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"CardManager: {(CardManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"AudioManager: {(AudioManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"RelicManager: {(RelicManager.Instance != null ? "✓" : "✗")}");
            Debug.Log($"LanguageManager: {(LanguageManager.Instance != null ? "✓" : "✗")}");

            if (SaveManager.Instance != null)
            {
                Debug.Log($"Save File Exists: {SaveManager.Instance.HasSaveFile}");
            }
        }
        #endregion
    }
}