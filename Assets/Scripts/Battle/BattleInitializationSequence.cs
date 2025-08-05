using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using Core;
using Maglin.Player;
using Maglin.Cards;
using Maglin.UI;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투 초기화 시퀀스 관리
    /// 전투 시작 시 모든 초기화 과정을 순차적으로 처리하고 시각적 피드백 제공
    /// </summary>
    public class BattleInitializationSequence : MonoBehaviour
    {
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool skipInitializationDelay = false;
        
        public static BattleInitializationSequence Instance { get; private set; }
        
        // 이벤트
        public System.Action OnInitializationStarted;
        public System.Action OnInitializationCompleted;
        public System.Action<string> OnInitializationStepChanged;
        public System.Action<float> OnInitializationProgressChanged;
        
        private bool isInitializing = false;
        private BattleController battleController;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                battleController = FindObjectOfType<BattleController>();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // 씬 로드 완료 후 초기화 시작
            _ = InitializeBattleSequence();
        }
        
        /// <summary>
        /// 전투 초기화 시퀀스 실행
        /// </summary>
        public async Task InitializeBattleSequence()
        {
            if (isInitializing)
            {
                Debug.LogWarning("BattleInitializationSequence: 이미 초기화 중입니다.");
                return;
            }
            
            isInitializing = true;
            OnInitializationStarted?.Invoke();
            
            try
            {
                if (debugMode)
                    Debug.Log("BattleInitializationSequence: 전투 초기화 시작");
                
                // 1. 로딩 화면 표시
                if (LoadingManager.Instance != null)
                {
                    await LoadingManager.Instance.ShowBattleInitLoading();
                }
                
                // 2. UI 블록 (플레이어 조작 방지)
                await BlockPlayerInput(true);
                
                // 3. TargetManager 초기화 시작 (타겟 마커 비활성화)
                if (TargetManager.Instance != null)
                {
                    TargetManager.Instance.StartBattleInitialization();
                }
                
                // 4. 배경 및 필드 초기화
                await InitializeField();
                
                // 5. 플레이어 시스템 초기화
                await InitializePlayerSystems();
                
                // 6. **애니메이션 전 로딩 화면 숨김**
                if (LoadingManager.Instance != null)
                {
                    if (debugMode)
                        Debug.Log("BattleInitializationSequence: 애니메이션 전 로딩 화면 숨김");
                    await LoadingManager.Instance.HideLoading();
                }
                
                // 7. 적 시스템 초기화 (애니메이션 포함)
                await InitializeEnemySystems();
                
                // 8. 카드 시스템 초기화 (애니메이션 포함)
                await InitializeCardSystems();
                
                // 9. 전투 UI 초기화
                await InitializeBattleUI();
                
                // 10. 전투 시작 준비 완료
                await FinalizeBattleSetup();
                
                // 11. TargetManager 초기화 완료 (타겟 시스템 활성화)
                if (TargetManager.Instance != null)
                {
                    TargetManager.Instance.CompleteBattleInitialization();
                }
                
                // 12. 씬 페이드 인 (SceneTransitionManager가 처리하지 않은 경우)
                if (SceneTransitionManager.Instance != null && !SceneTransitionManager.Instance.IsTransitioning)
                {
                    await SceneTransitionManager.Instance.FadeOut(0.5f);
                }
                
                // 13. 플레이어 조작 활성화
                await BlockPlayerInput(false);
                
                if (debugMode)
                    Debug.Log("BattleInitializationSequence: 전투 초기화 완료");
                
                OnInitializationCompleted?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BattleInitializationSequence: 초기화 중 오류 발생 - {e.Message}");
                
                // 오류 발생 시 정리
                if (LoadingManager.Instance != null)
                {
                    await LoadingManager.Instance.HideLoading();
                }
                
                await BlockPlayerInput(false);
            }
            finally
            {
                isInitializing = false;
            }
        }
        
        /// <summary>
        /// 플레이어 입력 차단/허용
        /// </summary>
        private async Task BlockPlayerInput(bool block)
        {
            if (block)
            {
                if (LoadingManager.Instance != null)
                    LoadingManager.Instance.UpdateToInputBlock();
            }
            else
            {
                if (LoadingManager.Instance != null)
                    LoadingManager.Instance.UpdateToInputEnable();
            }
            
            if (battleController != null)
            {
                // BattleController의 입력 차단 메소드 호출
                battleController.SetInputBlocked(block);
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(100);
        }
        
        /// <summary>
        /// 배경 및 전투 필드 초기화
        /// </summary>
        private async Task InitializeField()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToFieldSetup();
            
            // 배경 및 필드 로딩 로직
            if (battleController != null)
            {
                // BattleController의 필드 초기화 호출
                // battleController.InitializeField();
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(300);
        }
        
        /// <summary>
        /// 플레이어 시스템 초기화
        /// </summary>
        private async Task InitializePlayerSystems()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToPlayerInit();
            
            // 플레이어 매니저 초기화
            if (PlayerManager.Instance != null)
            {
                // 플레이어 체력, 마나 등 초기화
                // PlayerManager.Instance.InitializeForBattle();
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(400);
        }
        
        /// <summary>
        /// 적 시스템 초기화 (애니메이션 포함)
        /// </summary>
        private async Task InitializeEnemySystems()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToEnemySpawn();
            
            // 몬스터 스폰 매니저를 통한 애니메이션 스폰
            if (MonsterSpawnManager.Instance != null)
            {
                // 스폰된 몬스터들을 애니메이션과 함께 등장시키기
                var spawnedMonsters = new List<GameObject>(MonsterSpawnManager.Instance.SpawnedMonsters);
                
                if (spawnedMonsters.Count > 0)
                {
                    if (debugMode)
                        Debug.Log($"BattleInitializationSequence: {spawnedMonsters.Count}마리 몬스터 스폰 시작");
                    
                    if (MonsterSpawnAnimationManager.Instance != null)
                    {
                        if (debugMode)
                            Debug.Log($"BattleInitializationSequence: 몬스터 애니메이션 시작");
                        
                        // 애니메이션과 함께 등장시키기
                        await MonsterSpawnAnimationManager.Instance.SpawnMonstersSequentially(spawnedMonsters);
                        
                        if (debugMode)
                            Debug.Log($"BattleInitializationSequence: 몬스터 애니메이션 완료");
                    }
                    else
                    {
                        // 애니메이션 매니저가 없으면 즉시 활성화
                        if (debugMode)
                            Debug.Log("BattleInitializationSequence: MonsterSpawnAnimationManager 없음, 몬스터 즉시 활성화");
                        
                        foreach (var monster in spawnedMonsters)
                        {
                            if (monster != null && !monster.activeInHierarchy)
                            {
                                monster.SetActive(true);
                                if (debugMode)
                                    Debug.Log($"BattleInitializationSequence: {monster.name} 활성화");
                            }
                        }
                        
                        // 약간의 대기 시간
                        await Task.Delay(500);
                    }
                }
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(200); // 애니메이션 이후 추가 대기 시간 단축
        }
        
        /// <summary>
        /// 카드 시스템 초기화 (애니메이션 포함)
        /// </summary>
        private async Task InitializeCardSystems()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToDeckPrep();
            
            // 카드 매니저 초기화 및 초기 드로우 애니메이션
            if (CardManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("BattleInitializationSequence: 초기 카드 드로우 애니메이션 시작");
                
                try
                {
                    // CardManager.OnBattleStarted에서 이미 손패가 비워졌으므로 바로 드로우
                    if (debugMode)
                        Debug.Log($"BattleInitializationSequence: 현재 손패 {CardManager.Instance.HandCardCount}장, 드로우 가능한 덱 {CardManager.Instance.MainDeckCount}장");
                    
                    // 애니메이션과 함께 카드 드로우 (5장)
                    var drawnCards = await CardManager.Instance.DrawCardsWithAnimation(5);
                    
                    if (debugMode)
                        Debug.Log($"BattleInitializationSequence: {drawnCards.Count}장 카드 드로우 애니메이션 완료");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"BattleInitializationSequence: 카드 드로우 애니메이션 중 오류 - {e.Message}");
                    
                    // 오류 발생 시 기본 드로우로 대체
                    CardManager.Instance.DrawCardsToMax();
                }
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(200); // 애니메이션 이후 추가 대기 시간 단축
        }
        

        
        /// <summary>
        /// 전투 UI 초기화
        /// </summary>
        private async Task InitializeBattleUI()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToUISetup();
            
            // UI 요소들 초기화
            if (battleController != null)
            {
                // battleController.InitializeUI();
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(300);
        }
        
        /// <summary>
        /// 전투 시작 준비 완료
        /// </summary>
        private async Task FinalizeBattleSetup()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToBattleReady();
            
            // 최종 설정 및 전투 시작 신호
            if (battleController != null)
            {
                // battleController.StartBattle();
            }
            
            if (!skipInitializationDelay)
                await Task.Delay(200);
        }
        
        /// <summary>
        /// 진행률 및 메시지 업데이트
        /// </summary>
        private void UpdateProgress(float progress, string message)
        {
            OnInitializationProgressChanged?.Invoke(progress);
            OnInitializationStepChanged?.Invoke(message);
            
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.UpdateProgress(progress, message);
            }
            
            if (debugMode)
                Debug.Log($"BattleInitializationSequence: {message} ({progress:P0})");
        }
        
        /// <summary>
        /// 현재 초기화 중인지 확인
        /// </summary>
        public bool IsInitializing => isInitializing;
        
        /// <summary>
        /// 초기화 강제 중단 (디버그용)
        /// </summary>
        [ContextMenu("Force Stop Initialization")]
        public async void ForceStopInitialization()
        {
            if (!isInitializing) return;
            
            Debug.LogWarning("BattleInitializationSequence: 초기화 강제 중단");
            
            if (LoadingManager.Instance != null)
            {
                await LoadingManager.Instance.HideLoading();
            }
            
            await BlockPlayerInput(false);
            isInitializing = false;
        }
        
        /// <summary>
        /// 초기화 다시 시작 (디버그용)
        /// </summary>
        [ContextMenu("Restart Initialization")]
        public void RestartInitialization()
        {
            if (isInitializing)
            {
                Debug.LogWarning("BattleInitializationSequence: 이미 초기화 중입니다.");
                return;
            }
            
            _ = InitializeBattleSequence();
        }
    }
} 