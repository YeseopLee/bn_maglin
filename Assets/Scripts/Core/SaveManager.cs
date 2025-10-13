using UnityEngine;
using System.IO;
using System;
using Maglin.Player;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Core
{
    /// <summary>
    /// 게임 세이브/로드를 관리하는 매니저
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        #region Singleton
        private static SaveManager _instance;
        public static SaveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SaveManager>();
                    if (_instance == null)
                    {
                        GameObject saveManagerObj = new GameObject("SaveManager");
                        _instance = saveManagerObj.AddComponent<SaveManager>();
                        DontDestroyOnLoad(saveManagerObj);
                    }
                }
                return _instance;
            }
        }
        #endregion

        [Header("세이브 설정")]
        [SerializeField] private string saveFileName = "GameSave.json";
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool autoSaveEnabled = true;
        [SerializeField] private bool dontUseSaveFile = false;

        [Header("세이브 상태")]
        [SerializeField] private bool hasSaveFile = false;
        [SerializeField] private string lastSaveTime = "";

        // 이벤트
        public static event Action<GameSaveData> OnGameSaved;
        public static event Action<GameSaveData> OnGameLoaded;
        public static event Action OnSaveFileCreated;

        // 세이브 파일 경로
        private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

        // 현재 세이브 데이터
        private GameSaveData currentSaveData;

        // 프로퍼티
        public bool HasSaveFile => hasSaveFile && !dontUseSaveFile;
        public bool AutoSaveEnabled => autoSaveEnabled;
        public bool DontUseSaveFile => dontUseSaveFile;
        public GameSaveData CurrentSaveData => currentSaveData;

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSaveManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 게임 시작 시 세이브 파일 존재 여부 확인
            CheckSaveFileExists();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// SaveManager 초기화
        /// </summary>
        private void InitializeSaveManager()
        {
            if (debugMode)
                Debug.Log("[SaveManager] SaveManager 초기화");

            // 세이브 디렉토리 확인 및 생성
            string saveDirectory = Path.GetDirectoryName(SaveFilePath);
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
                if (debugMode)
                    Debug.Log($"[SaveManager] 세이브 디렉토리 생성: {saveDirectory}");
            }

            CheckSaveFileExists();
        }

        /// <summary>
        /// 세이브 파일 존재 여부 확인
        /// </summary>
        private void CheckSaveFileExists()
        {
            hasSaveFile = File.Exists(SaveFilePath);

            if (hasSaveFile)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(SaveFilePath);
                    lastSaveTime = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (debugMode)
                    {
                        if (dontUseSaveFile)
                            Debug.Log($"[SaveManager] 세이브 파일 발견하였으나 사용 안함 설정으로 무시: {lastSaveTime}");
                        else
                            Debug.Log($"[SaveManager] 기존 세이브 파일 발견: {lastSaveTime}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] 세이브 파일 정보 읽기 실패: {e.Message}");
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[SaveManager] 세이브 파일이 없습니다.");
            }
        }
        #endregion

        #region Save Operations
        /// <summary>
        /// 현재 게임 상태를 저장
        /// </summary>
        public bool SaveGame()
        {
            try
            {
                // 현재 게임 상태에서 세이브 데이터 생성
                GameSaveData saveData = CreateSaveDataFromCurrentState();

                if (saveData == null || !saveData.IsValid())
                {
                    Debug.LogError("[SaveManager] 유효하지 않은 세이브 데이터");
                    return false;
                }

                return SaveGameData(saveData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 게임 저장 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 특정 세이브 데이터를 파일에 저장
        /// </summary>
        public bool SaveGameData(GameSaveData saveData)
        {
            try
            {
                // JSON으로 직렬화
                string jsonData = JsonUtility.ToJson(saveData, true);

                // 파일에 저장
                File.WriteAllText(SaveFilePath, jsonData);

                // 상태 업데이트
                currentSaveData = saveData;
                hasSaveFile = true;
                lastSaveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] 게임 저장 완료: {SaveFilePath}");
                    Debug.Log($"[SaveManager] 저장된 층: {saveData.currentFloor} ({saveData.currentFloorType})");
                    Debug.Log($"[SaveManager] 플레이어 체력: {saveData.playerData.currentHealth}/{saveData.playerData.maxHealth}");
                    Debug.Log($"[SaveManager] 플레이어 골드: {saveData.playerData.currentGold}");
                }

                // 이벤트 발생
                OnGameSaved?.Invoke(saveData);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 세이브 데이터 저장 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 게임 상태에서 세이브 데이터 생성
        /// </summary>
        private GameSaveData CreateSaveDataFromCurrentState()
        {
            GameSaveData saveData = new GameSaveData();

            // FloorManager에서 층 정보 수집
            if (FloorManager.Instance != null)
            {
                saveData.currentFloor = FloorManager.Instance.CurrentFloor;
                saveData.currentFloorType = SaveDataHelper.FloorTypeToString(FloorManager.Instance.CurrentFloorType);
                saveData.gameState = SaveDataHelper.GameStateToString(FloorManager.Instance.CurrentState);
                saveData.debugMode = FloorManager.Instance.DebugMode;

                // 현재 층 SO 정보
                saveData.currentBattleStageId = SaveDataHelper.GetSOId(FloorManager.Instance.CurrentBattleStage);
                saveData.currentBattleId = SaveDataHelper.GetSOId(FloorManager.Instance.CurrentBattle);
                saveData.currentEventId = SaveDataHelper.GetSOId(FloorManager.Instance.CurrentEvent);

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] FloorManager 데이터 수집:");
                    Debug.Log($"  - 현재 층: {saveData.currentFloor} ({saveData.currentFloorType})");
                    Debug.Log($"  - 게임 상태: {saveData.gameState}");
                    Debug.Log($"  - BattleStage: {saveData.currentBattleStageId}");
                    Debug.Log($"  - Battle: {saveData.currentBattleId}");
                    Debug.Log($"  - Event: {saveData.currentEventId}");
                }
            }
            else
            {
                Debug.LogWarning("[SaveManager] FloorManager.Instance가 null입니다.");
            }

            // PlayerManager에서 플레이어 정보 수집
            if (PlayerManager.Instance != null)
            {
                saveData.playerData = CreatePlayerSaveData();
            }
            else
            {
                Debug.LogWarning("[SaveManager] PlayerManager.Instance가 null입니다.");
            }

            return saveData;
        }

        /// <summary>
        /// PlayerManager에서 플레이어 세이브 데이터 생성
        /// </summary>
        private PlayerSaveData CreatePlayerSaveData()
        {
            PlayerSaveData playerData = new PlayerSaveData();

            if (PlayerManager.Instance != null)
            {
                // 기본 스탯 (유물 효과 제외)
                playerData.currentHealth = PlayerManager.Instance.CurrentHealth;
                playerData.maxHealth = PlayerManager.Instance.BaseMaxHealth; // 기본값만 저장
                playerData.currentMana = PlayerManager.Instance.CurrentMana;
                playerData.maxMana = PlayerManager.Instance.BaseMaxMana; // 기본값만 저장
                playerData.currentGold = PlayerManager.Instance.CurrentGold;
                playerData.manaRecoveryPerTurn = PlayerManager.Instance.BaseManaRecoveryPerTurn; // 기본값만 저장
                playerData.maxHandSize = PlayerManager.Instance.BaseMaxHandSize; // 기본값만 저장

                // 애니메이션 상태
                playerData.currentAnimationState = SaveDataHelper.AnimationStateToString(PlayerManager.Instance.CurrentAnimationState);

                // 덱 정보 (CardManager에서 수집)
                CollectDeckData(playerData);

                // 유물 정보
                CollectRelicData(playerData);

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] PlayerManager 데이터 수집:");
                    Debug.Log($"  - 현재 체력: {playerData.currentHealth}");
                    Debug.Log($"  - 기본 최대 체력: {playerData.maxHealth} (실제 최대: {PlayerManager.Instance.MaxHealth})");
                    Debug.Log($"  - 현재 마나: {playerData.currentMana}");
                    Debug.Log($"  - 기본 최대 마나: {playerData.maxMana} (실제 최대: {PlayerManager.Instance.MaxMana})");
                    Debug.Log($"  - 골드: {playerData.currentGold}");
                    Debug.Log($"  - 기본 손패 크기: {playerData.maxHandSize} (실제: {PlayerManager.Instance.MaxHandSize})");
                    Debug.Log($"  - 덱 카드 수: {playerData.deckCardIds.Count}");
                    Debug.Log($"  - 유물 수: {playerData.relicIds.Count}");
                }
            }

            return playerData;
        }

        /// <summary>
        /// 덱 데이터 수집 (스타터 덱 기준)
        /// </summary>
        private void CollectDeckData(PlayerSaveData playerData)
        {
            if (CardManager.Instance != null)
            {
                // CardManager에서 스타터 덱 정보 가져오기 (플레이어 보유 카드)
                var deckCards = CardManager.Instance.GetStarterDeckCards();

                if (deckCards != null)
                {
                    playerData.deckCardIds.Clear();
                    playerData.deckCardCounts.Clear();

                    foreach (var card in deckCards)
                    {
                        if (card != null)
                        {
                            string cardId = card.name; // 또는 card.CardID가 있다면 사용

                            // 이미 존재하는 카드인지 확인
                            int existingIndex = playerData.deckCardIds.IndexOf(cardId);
                            if (existingIndex >= 0)
                            {
                                playerData.deckCardCounts[existingIndex]++;
                            }
                            else
                            {
                                playerData.deckCardIds.Add(cardId);
                                playerData.deckCardCounts.Add(1);
                            }
                        }
                    }
                }

                if (debugMode && playerData.deckCardIds.Count > 0)
                {
                    Debug.Log($"[SaveManager] 스타터 덱 카드 저장:");
                    for (int i = 0; i < playerData.deckCardIds.Count; i++)
                    {
                        Debug.Log($"  - {playerData.deckCardIds[i]} x{playerData.deckCardCounts[i]}");
                    }
                }
            }
        }

        /// <summary>
        /// 유물 데이터 수집
        /// </summary>
        private void CollectRelicData(PlayerSaveData playerData)
        {
            if (PlayerManager.Instance != null)
            {
                var relics = PlayerManager.Instance.CurrentRelics;

                playerData.relicIds.Clear();

                foreach (var relic in relics)
                {
                    if (relic != null)
                    {
                        playerData.relicIds.Add(SaveDataHelper.GetSOId(relic));
                    }
                }

                if (debugMode && playerData.relicIds.Count > 0)
                {
                    Debug.Log($"[SaveManager] 유물 저장:");
                    foreach (var relicId in playerData.relicIds)
                    {
                        Debug.Log($"  - {relicId}");
                    }
                }
            }
        }
        #endregion

        #region Load Operations
        /// <summary>
        /// 세이브 파일 로드
        /// </summary>
        public GameSaveData LoadGame()
        {
            try
            {
                if (!HasSaveFile)
                {
                    if (debugMode)
                    {
                        if (dontUseSaveFile && hasSaveFile)
                            Debug.Log("[SaveManager] 세이브 파일 사용 안함 설정으로 로드하지 않습니다.");
                        else
                            Debug.Log("[SaveManager] 로드할 세이브 파일이 없습니다.");
                    }
                    return null;
                }

                string jsonData = File.ReadAllText(SaveFilePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(jsonData);

                if (saveData == null || !saveData.IsValid())
                {
                    Debug.LogError("[SaveManager] 유효하지 않은 세이브 데이터");
                    return null;
                }

                currentSaveData = saveData;

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] 게임 로드 완료:");
                    Debug.Log($"  - 층: {saveData.currentFloor} ({saveData.currentFloorType})");
                    Debug.Log($"  - 게임 상태: {saveData.gameState}");
                    Debug.Log($"  - 플레이어 체력: {saveData.playerData.currentHealth}/{saveData.playerData.maxHealth}");
                    Debug.Log($"  - 플레이어 골드: {saveData.playerData.currentGold}");
                }

                OnGameLoaded?.Invoke(saveData);
                return saveData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 게임 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 세이브 데이터를 게임에 적용
        /// </summary>
        public bool ApplySaveDataToGame(GameSaveData saveData)
        {
            if (saveData == null || !saveData.IsValid())
            {
                Debug.LogError("[SaveManager] 유효하지 않은 세이브 데이터");
                return false;
            }

            try
            {
                if (debugMode)
                    Debug.Log("[SaveManager] 세이브 데이터를 게임에 적용 시작");

                // FloorManager에 층 정보 적용
                ApplyFloorDataToGame(saveData);

                // PlayerManager에 플레이어 정보 적용
                ApplyPlayerDataToGame(saveData.playerData);

                if (debugMode)
                    Debug.Log("[SaveManager] 세이브 데이터 적용 완료");

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 세이브 데이터 적용 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 층 데이터를 FloorManager에 적용
        /// </summary>
        private void ApplyFloorDataToGame(GameSaveData saveData)
        {
            if (FloorManager.Instance != null)
            {
                // 세이브 로드용 층 정보 설정 (디버그 모드 제한 없음)
                FloorManager.Instance.SetCurrentFloorFromSave(saveData.currentFloor);

                // 게임 상태 설정
                GameState gameState = SaveDataHelper.StringToGameState(saveData.gameState);
                FloorManager.Instance.ChangeGameState(gameState);

                // 현재 층 SO 정보는 층 시작 시 자동으로 로드됨

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] FloorManager 데이터 적용:");
                    Debug.Log($"  - 층: {saveData.currentFloor}");
                    Debug.Log($"  - 게임 상태: {gameState}");
                }
            }
        }

        /// <summary>
        /// 플레이어 데이터를 PlayerManager에 적용
        /// </summary>
        private void ApplyPlayerDataToGame(PlayerSaveData playerData)
        {
            if (PlayerManager.Instance != null && playerData != null)
            {
                // 먼저 유물 정보 적용 (기본 스탯 설정 전에)
                ApplyRelicDataToGame(playerData);

                // 기본 스탯 설정 (유물 효과 자동 계산됨)
                PlayerManager.Instance.SetBaseStats(
                    playerData.maxHealth,
                    playerData.maxMana,
                    playerData.manaRecoveryPerTurn,
                    playerData.maxHandSize
                );

                // 현재 값 직접 설정 (기본 스탯 설정 후에 해야 함)
                PlayerManager.Instance.SetCurrentHealth(playerData.currentHealth);
                PlayerManager.Instance.SetCurrentMana(playerData.currentMana);
                PlayerManager.Instance.SetGold(playerData.currentGold);

                // 애니메이션 상태 설정
                var animState = SaveDataHelper.StringToAnimationState(playerData.currentAnimationState);
                PlayerManager.Instance.SetAnimationState(animState);

                // 덱 정보 적용
                ApplyDeckDataToGame(playerData);

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] PlayerManager 데이터 적용:");
                    Debug.Log($"  - 현재 체력: {PlayerManager.Instance.CurrentHealth}");
                    Debug.Log($"  - 기본 최대 체력: {PlayerManager.Instance.BaseMaxHealth} -> 계산된 최대 체력: {PlayerManager.Instance.MaxHealth}");
                    Debug.Log($"  - 현재 마나: {PlayerManager.Instance.CurrentMana}");
                    Debug.Log($"  - 기본 최대 마나: {PlayerManager.Instance.BaseMaxMana} -> 계산된 최대 마나: {PlayerManager.Instance.MaxMana}");
                    Debug.Log($"  - 골드: {PlayerManager.Instance.CurrentGold}");
                    Debug.Log($"  - 기본 손패 크기: {PlayerManager.Instance.BaseMaxHandSize} -> 계산된 손패 크기: {PlayerManager.Instance.MaxHandSize}");
                }
            }
        }

        /// <summary>
        /// 덱 데이터를 CardManager에 적용 (스타터 덱으로)
        /// </summary>
        private void ApplyDeckDataToGame(PlayerSaveData playerData)
        {
            if (CardManager.Instance != null && playerData.deckCardIds.Count > 0)
            {
                // 스타터 덱 설정
                CardManager.Instance.SetDeckFromSaveData(playerData.deckCardIds, playerData.deckCardCounts);

                // 스타터 덱에서 currentDeck으로 복사 (즉시 사용 가능하도록)
                CardManager.Instance.InitializeDeckForNewGame();

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] 스타터 덱 데이터 적용 및 currentDeck 초기화: {playerData.deckCardIds.Count}개 카드 타입");
                }
            }
        }

        /// <summary>
        /// 유물 데이터를 PlayerManager에 적용
        /// </summary>
        private void ApplyRelicDataToGame(PlayerSaveData playerData)
        {
            if (PlayerManager.Instance != null && playerData.relicIds.Count > 0)
            {
                // 기존 유물 제거 (세이브 로드 시)
                var currentRelics = PlayerManager.Instance.CurrentRelics;
                foreach (var relic in currentRelics)
                {
                    PlayerManager.Instance.RemoveRelic(relic);
                }

                // 세이브된 유물 추가
                foreach (var relicId in playerData.relicIds)
                {
                    RelicSO relic = Resources.Load<RelicSO>($"Relics/{relicId}");
                    if (relic == null)
                        relic = Resources.Load<RelicSO>(relicId); // 폴백

                    if (relic != null)
                    {
                        PlayerManager.Instance.AddRelic(relic);
                    }
                    else
                    {
                        Debug.LogWarning($"[SaveManager] 유물을 찾을 수 없습니다: {relicId}");
                    }
                }

                if (debugMode)
                {
                    Debug.Log($"[SaveManager] 유물 데이터 적용: {playerData.relicIds.Count}개 유물");
                }
            }
        }
        #endregion

        #region File Operations
        /// <summary>
        /// 새로운 세이브 파일 생성
        /// </summary>
        public bool CreateNewSaveFile()
        {
            try
            {
                GameSaveData newSave = new GameSaveData();
                // 기본값은 이미 생성자에서 설정됨

                if (SaveGameData(newSave))
                {
                    OnSaveFileCreated?.Invoke();

                    if (debugMode)
                        Debug.Log("[SaveManager] 새로운 세이브 파일 생성 완료");

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 새 세이브 파일 생성 실패: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// 세이브 파일 삭제
        /// </summary>
        public bool DeleteSaveFile()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                    hasSaveFile = false;
                    lastSaveTime = "";
                    currentSaveData = null;

                    if (debugMode)
                        Debug.Log("[SaveManager] 세이브 파일 삭제 완료");

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 세이브 파일 삭제 실패: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// 자동 저장 설정
        /// </summary>
        public void SetAutoSave(bool enabled)
        {
            autoSaveEnabled = enabled;

            if (debugMode)
                Debug.Log($"[SaveManager] 자동 저장: {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 세이브 파일 사용 안함 설정
        /// </summary>
        public void SetDontUseSaveFile(bool dontUse)
        {
            dontUseSaveFile = dontUse;

            if (debugMode)
                Debug.Log($"[SaveManager] 세이브 파일 사용 안함: {(dontUse ? "활성화" : "비활성화")}");
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Save Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== SaveManager Debug Info ===");
            Debug.Log($"Save File Path: {SaveFilePath}");
            Debug.Log($"Has Save File (Physical): {hasSaveFile}");
            Debug.Log($"Has Save File (Effective): {HasSaveFile}");
            Debug.Log($"Don't Use Save File: {dontUseSaveFile}");
            Debug.Log($"Last Save Time: {lastSaveTime}");
            Debug.Log($"Auto Save Enabled: {autoSaveEnabled}");
            Debug.Log($"Current Save Data: {(currentSaveData != null ? "Loaded" : "None")}");

            if (currentSaveData != null)
            {
                Debug.Log($"Current Floor: {currentSaveData.currentFloor}");
                Debug.Log($"Floor Type: {currentSaveData.currentFloorType}");
                Debug.Log($"Game State: {currentSaveData.gameState}");
            }
        }
        #endregion
    }
}
