using UnityEngine;
using System.Threading.Tasks;
using Core;
using Maglin.Player;
using Maglin.Cards;

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
        private BattleTestController battleTestController;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                battleTestController = FindObjectOfType<BattleTestController>();
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

                // 3. 배경 및 필드 초기화
                await InitializeField();

                // 4. 플레이어 시스템 초기화
                // await InitializePlayerSystems();

                // 5. 적 시스템 초기화
                // await InitializeEnemySystems();

                // 6. 카드 시스템 초기화
                // await InitializeCardSystems();

                // 7. 전투 UI 초기화
                // await InitializeBattleUI();

                // 8. 전투 시작 준비 완료
                // await FinalizeBattleSetup();

                // 9. 로딩 화면 숨기기
                if (LoadingManager.Instance != null && LoadingManager.Instance.IsLoading)
                {
                    await LoadingManager.Instance.HideLoading();
                }

                // 10. 몬스터 스폰 애니메이션 시작 (로딩 화면이 숨겨진 후)
                await StartMonsterSpawnAnimations();

                // 11. 몬스터 스폰 애니메이션 완료 후 초기 카드 드로우 실행
                await PerformInitialCardDraw();

                // 12. 씬 페이드 인 (SceneTransitionManager가 처리하지 않은 경우)
                if (SceneTransitionManager.Instance != null && !SceneTransitionManager.Instance.IsTransitioning)
                {
                    await SceneTransitionManager.Instance.FadeOut(0.5f);
                }

                // 12. 플레이어 조작 활성화
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
            if (debugMode)
                Debug.Log($"BattleInitializationSequence: 플레이어 입력 {(block ? "차단" : "허용")} 시작");

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

            if (battleTestController != null)
            {
                // BattleTestController의 입력 차단 메소드 호출
                battleTestController.SetInputBlocked(block);
            }

            if (!skipInitializationDelay)
                await Task.Delay(100);

            // EventSystem 상태 확인
            if (debugMode)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    Debug.Log($"BattleInitializationSequence: EventSystem 상태 - enabled: {UnityEngine.EventSystems.EventSystem.current.enabled}");
                }
                else
                {
                    Debug.LogWarning("BattleInitializationSequence: EventSystem.current가 null입니다!");
                }
            }
        }

        /// <summary>
        /// 배경 및 전투 필드 초기화
        /// </summary>
        private async Task InitializeField()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToFieldSetup();

            // 배경 및 필드 로딩 로직
            if (battleTestController != null)
            {
                // BattleTestController의 필드 초기화 호출
                // battleTestController.InitializeField();
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
        /// 적 시스템 초기화
        /// </summary>
        private async Task InitializeEnemySystems()
        {
            // 적 스폰 단계까지는 로딩 화면 유지
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToEnemySpawn();

            // 적 스폰 및 초기화 (로딩 화면이 있는 상태에서 준비)
            if (battleTestController != null)
            {
                // battleTestController.SpawnEnemies();
                // 몬스터는 준비되지만 애니메이션은 아직 시작하지 않음
            }

            if (!skipInitializationDelay)
                await Task.Delay(300);
        }

        /// <summary>
        /// 카드 시스템 초기화
        /// </summary>
        private async Task InitializeCardSystems()
        {
            // 카드 드로우 애니메이션을 보여주기 위해 로딩 화면 없이 진행
            // if (LoadingManager.Instance != null)
            //     LoadingManager.Instance.UpdateToDeckPrep();

            // 카드 매니저 초기화
            if (CardManager.Instance != null)
            {
                // 덱 초기화 및 초기 드로우 (애니메이션과 함께)
                // CardManager.Instance.InitializeForBattle();
            }

            if (!skipInitializationDelay)
                await Task.Delay(400);
        }

        /// <summary>
        /// 전투 UI 초기화
        /// </summary>
        private async Task InitializeBattleUI()
        {
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.UpdateToUISetup();

            // UI 요소들 초기화
            if (battleTestController != null)
            {
                // battleTestController.InitializeUI();
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
            if (battleTestController != null)
            {
                // battleTestController.StartBattle();
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
        /// 몬스터 스폰 애니메이션 시작
        /// </summary>
        private async Task StartMonsterSpawnAnimations()
        {
            if (debugMode)
                Debug.Log("BattleInitializationSequence: 몬스터 스폰 애니메이션 시작");

            // MonsterSpawnManager에게 애니메이션 시작 신호 전송
            if (MonsterSpawnManager.Instance != null)
            {
                // 대기 중인 몬스터들의 애니메이션 처리 강제 실행
                MonsterSpawnManager.Instance.ProcessPendingAnimations();

                // 모든 애니메이션이 완료될 때까지 대기
                while (MonsterSpawnManager.Instance.IsWaitingForAnimations)
                {
                    await Task.Delay(100);
                }
            }

            if (debugMode)
                Debug.Log("BattleInitializationSequence: 몬스터 스폰 애니메이션 완료");
        }

        /// <summary>
        /// 초기 카드 드로우 실행 (몬스터 스폰 애니메이션 완료 후)
        /// </summary>
        private async Task PerformInitialCardDraw()
        {
            if (debugMode)
                Debug.Log("BattleInitializationSequence: 초기 카드 드로우 시작");

            UpdateProgress(0.92f, "카드 준비 중...");

            // CardManager를 통해 초기 카드 드로우 실행
            if (CardManager.Instance != null)
            {
                CardManager.Instance.PerformInitialCardDraw();

                // 카드 드로우 애니메이션이 완료될 때까지 잠시 대기
                if (CardDrawAnimationManager.Instance != null)
                {
                    // 애니메이션이 진행 중일 때까지 대기
                    int waitCount = 0;
                    while (!CardDrawAnimationManager.Instance.IsAnimating && waitCount < 10)
                    {
                        await Task.Delay(50);
                        waitCount++;
                    }

                    // 애니메이션이 완료될 때까지 대기
                    while (CardDrawAnimationManager.Instance.IsAnimating)
                    {
                        await Task.Delay(50);
                    }
                }
                else
                {
                    // 애니메이션 매니저가 없으면 기본 딜레이
                    await Task.Delay(500);
                }
            }

            if (debugMode)
                Debug.Log("BattleInitializationSequence: 초기 카드 드로우 완료");
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