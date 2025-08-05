using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Maglin.Battle;

namespace Maglin.UI
{
    /// <summary>
    /// 턴 정보와 상태를 시각적으로 표시하는 UI 클래스
    /// </summary>
    public class TurnUI : MonoBehaviour
    {
        #region Fields
        [Header("턴 정보 UI")]
        [SerializeField] private Text currentTurnText;          // 현재 턴 표시
        [SerializeField] private Text turnNumberText;           // 턴 번호 표시
        [SerializeField] private Text phaseText;                // 현재 페이즈 표시

        [Header("플레이어 턴 UI")]
        [SerializeField] private GameObject playerTurnPanel;    // 플레이어 턴 패널
        [SerializeField] private Button endTurnButton;          // 턴 종료 버튼
        [SerializeField] private Text playerActionText;         // 플레이어 행동 안내 텍스트
        [SerializeField] private Slider turnTimerSlider;        // 턴 제한 시간 슬라이더

        [Header("몬스터 턴 UI")]
        [SerializeField] private GameObject enemyTurnPanel;     // 몬스터 턴 패널
        [SerializeField] private Text enemyActionText;          // 몬스터 행동 표시 텍스트

        [Header("전투 결과 UI")]
        [SerializeField] private GameObject battleResultPanel; // 전투 결과 패널
        [SerializeField] private Text battleResultText;        // 전투 결과 텍스트
        [SerializeField] private Button continueButton;        // 계속하기 버튼

        [Header("애니메이션 설정")]
        [SerializeField] private float panelFadeTime = 0.5f;    // 패널 페이드 시간
        [SerializeField] private float turnTransitionTime = 1f; // 턴 전환 애니메이션 시간

        [Header("색상 설정")]
        [SerializeField] private Color playerTurnColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField] private Color enemyTurnColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color victoryColor = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private Color defeatColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 컴포넌트 참조
        private TurnManager turnManager;
        private CanvasGroup playerTurnCanvasGroup;
        private CanvasGroup enemyTurnCanvasGroup;
        private CanvasGroup battleResultCanvasGroup;

        // 타이머 관련
        private Coroutine turnTimerCoroutine;
        private float currentTurnTimeLimit;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Unity Events
        private void Awake()
        {
            turnManager = TurnManager.Instance;
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            SetupUIComponents();
            SetupButtons();
            ResetUI();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[TurnUI] 초기화 완료");
        }

        /// <summary>
        /// UI 새로고침
        /// </summary>
        public void RefreshUI()
        {
            if (turnManager == null) return;

            UpdateTurnDisplay();
            UpdatePhaseDisplay();
        }

        /// <summary>
        /// 턴 타이머 업데이트
        /// </summary>
        public void UpdateTurnTimer(float remainingTime, float totalTime)
        {
            if (turnTimerSlider != null)
            {
                turnTimerSlider.value = remainingTime / totalTime;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// UI 컴포넌트 설정
        /// </summary>
        private void SetupUIComponents()
        {
            // CanvasGroup 컴포넌트 확인/추가
            if (playerTurnPanel != null)
            {
                playerTurnCanvasGroup = playerTurnPanel.GetComponent<CanvasGroup>();
                if (playerTurnCanvasGroup == null)
                    playerTurnCanvasGroup = playerTurnPanel.AddComponent<CanvasGroup>();
            }

            if (enemyTurnPanel != null)
            {
                enemyTurnCanvasGroup = enemyTurnPanel.GetComponent<CanvasGroup>();
                if (enemyTurnCanvasGroup == null)
                    enemyTurnCanvasGroup = enemyTurnPanel.AddComponent<CanvasGroup>();
            }

            if (battleResultPanel != null)
            {
                battleResultCanvasGroup = battleResultPanel.GetComponent<CanvasGroup>();
                if (battleResultCanvasGroup == null)
                    battleResultCanvasGroup = battleResultPanel.AddComponent<CanvasGroup>();
            }

            // 턴 타이머 슬라이더 초기 설정
            if (turnTimerSlider != null)
            {
                turnTimerSlider.minValue = 0f;
                turnTimerSlider.maxValue = 1f;
                turnTimerSlider.value = 1f;
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtons()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinueButtonClicked);
            }
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void ResetUI()
        {
            // 모든 패널 숨기기
            SetPanelVisibility(playerTurnPanel, false);
            SetPanelVisibility(enemyTurnPanel, false);
            SetPanelVisibility(battleResultPanel, false);

            // 텍스트 초기화
            if (currentTurnText != null)
                currentTurnText.text = "대기 중...";

            if (turnNumberText != null)
                turnNumberText.text = "턴: 0";

            if (phaseText != null)
                phaseText.text = "None";

            if (playerActionText != null)
                playerActionText.text = "카드를 사용하거나 턴을 종료하세요.";

            if (enemyActionText != null)
                enemyActionText.text = "몬스터들이 행동하고 있습니다...";
        }

        /// <summary>
        /// 턴 표시 업데이트
        /// </summary>
        private void UpdateTurnDisplay()
        {
            if (turnManager == null) return;

            string turnTypeText = turnManager.CurrentTurnType == TurnType.Player ? "플레이어 턴" : "몬스터 턴";
            Color turnColor = turnManager.CurrentTurnType == TurnType.Player ? playerTurnColor : enemyTurnColor;

            if (currentTurnText != null)
            {
                currentTurnText.text = turnTypeText;
                currentTurnText.color = turnColor;
            }

            if (turnNumberText != null)
            {
                turnNumberText.text = $"턴: {turnManager.CurrentTurnNumber}";
            }
        }

        /// <summary>
        /// 페이즈 표시 업데이트
        /// </summary>
        private void UpdatePhaseDisplay()
        {
            if (phaseText == null || turnManager == null) return;

            string phaseDisplayText = turnManager.CurrentTurnType switch
            {
                TurnType.Player => "플레이어 턴",
                TurnType.Enemy => "몬스터 턴",
                _ => "대기 중"
            };

            this.phaseText.text = phaseDisplayText;
        }

        /// <summary>
        /// 패널 가시성 설정
        /// </summary>
        private void SetPanelVisibility(GameObject panel, bool visible, bool animated = true)
        {
            if (panel == null) return;

            if (animated)
            {
                StartCoroutine(AnimatePanelVisibility(panel, visible));
            }
            else
            {
                panel.SetActive(visible);
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                    canvasGroup.interactable = visible;
                    canvasGroup.blocksRaycasts = visible;
                }
            }
        }

        /// <summary>
        /// 패널 애니메이션
        /// </summary>
        private IEnumerator AnimatePanelVisibility(GameObject panel, bool visible)
        {
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) yield break;

            if (visible)
            {
                panel.SetActive(true);
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            float startAlpha = canvasGroup.alpha;
            float targetAlpha = visible ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < panelFadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / panelFadeTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;

            if (visible)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// 턴 전환 애니메이션
        /// </summary>
        private IEnumerator PlayTurnTransitionAnimation(TurnType newTurnType)
        {
            // 현재 UI 페이드 아웃
            yield return StartCoroutine(FadeOutCurrentUI());

            // 턴 정보 업데이트
            UpdateTurnDisplay();
            UpdatePhaseDisplay();

            // 새 턴 UI 페이드 인
            yield return StartCoroutine(FadeInNewTurnUI(newTurnType));
        }

        /// <summary>
        /// 현재 UI 페이드 아웃
        /// </summary>
        private IEnumerator FadeOutCurrentUI()
        {
            SetPanelVisibility(playerTurnPanel, false, true);
            SetPanelVisibility(enemyTurnPanel, false, true);
            yield return new WaitForSeconds(panelFadeTime);
        }

        /// <summary>
        /// 새 턴 UI 페이드 인
        /// </summary>
        private IEnumerator FadeInNewTurnUI(TurnType turnType)
        {
            if (turnType == TurnType.Player)
            {
                SetPanelVisibility(playerTurnPanel, true, true);
            }
            else
            {
                SetPanelVisibility(enemyTurnPanel, true, true);
            }

            yield return new WaitForSeconds(panelFadeTime);
        }

        /// <summary>
        /// 플레이어 턴 타이머 시작
        /// </summary>
        private void StartPlayerTurnTimer(float timeLimit)
        {
            if (turnTimerCoroutine != null)
            {
                StopCoroutine(turnTimerCoroutine);
            }

            currentTurnTimeLimit = timeLimit;
            turnTimerCoroutine = StartCoroutine(PlayerTurnTimerCoroutine());
        }

        /// <summary>
        /// 플레이어 턴 타이머 코루틴
        /// </summary>
        private IEnumerator PlayerTurnTimerCoroutine()
        {
            float remainingTime = currentTurnTimeLimit;

            while (remainingTime > 0 && turnManager.IsPlayerTurnActive)
            {
                UpdateTurnTimer(remainingTime, currentTurnTimeLimit);
                remainingTime -= Time.deltaTime;
                yield return null;
            }

            UpdateTurnTimer(0f, currentTurnTimeLimit);
        }

        /// <summary>
        /// 플레이어 턴 타이머 중지
        /// </summary>
        private void StopPlayerTurnTimer()
        {
            if (turnTimerCoroutine != null)
            {
                StopCoroutine(turnTimerCoroutine);
                turnTimerCoroutine = null;
            }

            if (turnTimerSlider != null)
            {
                turnTimerSlider.value = 1f;
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            TurnManager.OnTurnStarted += OnTurnStarted;
            TurnManager.OnTurnEnded += OnTurnEnded;
            TurnManager.OnPlayerTurnStarted += OnPlayerTurnStarted;
            TurnManager.OnPlayerTurnEnded += OnPlayerTurnEnded;
            TurnManager.OnEnemyTurnStarted += OnEnemyTurnStarted;
            TurnManager.OnEnemyTurnEnded += OnEnemyTurnEnded;
            TurnManager.OnBattleStarted += OnBattleStarted;
            TurnManager.OnBattleEnded += OnBattleEnded;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            TurnManager.OnTurnStarted -= OnTurnStarted;
            TurnManager.OnTurnEnded -= OnTurnEnded;
            TurnManager.OnPlayerTurnStarted -= OnPlayerTurnStarted;
            TurnManager.OnPlayerTurnEnded -= OnPlayerTurnEnded;
            TurnManager.OnEnemyTurnStarted -= OnEnemyTurnStarted;
            TurnManager.OnEnemyTurnEnded -= OnEnemyTurnEnded;
            TurnManager.OnBattleStarted -= OnBattleStarted;
            TurnManager.OnBattleEnded -= OnBattleEnded;
        }

        /// <summary>
        /// 턴 시작 이벤트 처리
        /// </summary>
        private void OnTurnStarted(TurnType turnType, int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 턴 시작: {turnType} 턴 {turnNumber}");

            StartCoroutine(PlayTurnTransitionAnimation(turnType));
        }

        /// <summary>
        /// 턴 종료 이벤트 처리
        /// </summary>
        private void OnTurnEnded(TurnType turnType, int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 턴 종료: {turnType} 턴 {turnNumber}");

            if (turnType == TurnType.Player)
            {
                StopPlayerTurnTimer();
            }
        }

        /// <summary>
        /// 플레이어 턴 시작 이벤트 처리
        /// </summary>
        private void OnPlayerTurnStarted(int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 플레이어 턴 {turnNumber} 시작");

            // 플레이어 턴 타이머 시작 (제한 시간이 있는 경우)
            if (currentTurnTimeLimit > 0)
            {
                StartPlayerTurnTimer(currentTurnTimeLimit);
            }

            // 버튼 활성화
            if (endTurnButton != null)
            {
                endTurnButton.interactable = true;
            }
        }

        /// <summary>
        /// 플레이어 턴 종료 이벤트 처리
        /// </summary>
        private void OnPlayerTurnEnded(int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 플레이어 턴 {turnNumber} 종료");

            // 버튼 비활성화
            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
            }
        }

        /// <summary>
        /// 몬스터 턴 시작 이벤트 처리
        /// </summary>
        private void OnEnemyTurnStarted(int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 몬스터 턴 {turnNumber} 시작");
        }

        /// <summary>
        /// 몬스터 턴 종료 이벤트 처리
        /// </summary>
        private void OnEnemyTurnEnded(int turnNumber)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 몬스터 턴 {turnNumber} 종료");
        }

        /// <summary>
        /// 전투 시작 이벤트 처리
        /// </summary>
        private void OnBattleStarted()
        {
            if (debugMode)
                Debug.Log("[TurnUI] 전투 시작");

            RefreshUI();
        }

        /// <summary>
        /// 전투 종료 이벤트 처리
        /// </summary>
        private void OnBattleEnded(bool playerWon)
        {
            if (debugMode)
                Debug.Log($"[TurnUI] 전투 종료 - {(playerWon ? "승리" : "패배")}");

            // 모든 턴 UI 숨기기
            SetPanelVisibility(playerTurnPanel, false);
            SetPanelVisibility(enemyTurnPanel, false);

            // 전투 결과 표시
            ShowBattleResult(playerWon);
        }

        /// <summary>
        /// 턴 종료 버튼 클릭 처리
        /// </summary>
        private void OnEndTurnButtonClicked()
        {
            if (turnManager != null && turnManager.IsPlayerTurnActive)
            {
                if (debugMode)
                    Debug.Log("[TurnUI] 플레이어가 턴 종료 버튼을 클릭했습니다.");

                turnManager.EndPlayerTurn();
            }
        }

        /// <summary>
        /// 계속하기 버튼 클릭 처리
        /// </summary>
        private void OnContinueButtonClicked()
        {
            if (debugMode)
                Debug.Log("[TurnUI] 계속하기 버튼 클릭");

            SetPanelVisibility(battleResultPanel, false);
            // 여기에 다음 스테이지나 메인 메뉴로 이동하는 로직 추가
        }
        #endregion

        #region Battle Result
        /// <summary>
        /// 전투 결과 표시
        /// </summary>
        private void ShowBattleResult(bool playerWon)
        {
            if (battleResultPanel == null) return;

            // 결과 텍스트 설정
            if (battleResultText != null)
            {
                battleResultText.text = playerWon ? "승리!" : "패배...";
                battleResultText.color = playerWon ? victoryColor : defeatColor;
            }

            // 결과 패널 표시
            SetPanelVisibility(battleResultPanel, true);
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// UI 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug UI Status")]
        public void DebugUIStatus()
        {
            Debug.Log("=== TurnUI 상태 ===");
            Debug.Log($"초기화 여부: {isInitialized}");

            if (turnManager != null)
            {
                Debug.Log($"현재 턴: {turnManager.CurrentTurnType} 턴 {turnManager.CurrentTurnNumber}");
                Debug.Log($"전투 활성화: {turnManager.IsBattleActive}");
                Debug.Log($"플레이어 턴 활성화: {turnManager.IsPlayerTurnActive}");
            }

            Debug.Log($"플레이어 턴 패널 활성화: {playerTurnPanel?.activeInHierarchy ?? false}");
            Debug.Log($"몬스터 턴 패널 활성화: {enemyTurnPanel?.activeInHierarchy ?? false}");
            Debug.Log($"전투 결과 패널 활성화: {battleResultPanel?.activeInHierarchy ?? false}");
        }
        #endregion
    }
}