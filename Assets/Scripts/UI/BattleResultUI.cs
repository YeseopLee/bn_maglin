using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Maglin.Battle;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// 전투 결과 UI를 관리하는 클래스
    /// </summary>
    public class BattleResultUI : MonoBehaviour
    {
        [Header("메인 UI")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject resultBackground;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("결과 표시")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Image resultIcon;

        [Header("통계 표시")]
        [SerializeField] private GameObject statisticsPanel;
        [SerializeField] private TextMeshProUGUI floorInfoText;
        [SerializeField] private TextMeshProUGUI battleDurationText;
        [SerializeField] private TextMeshProUGUI cardsUsedText;
        [SerializeField] private TextMeshProUGUI damageDealtText;
        [SerializeField] private TextMeshProUGUI damageTakenText;
        [SerializeField] private TextMeshProUGUI combosSuccessText;
        [SerializeField] private TextMeshProUGUI efficiencyText;

        [Header("버튼")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("애니메이션 설정")]
        [SerializeField] private float showDuration = 0.8f;
        [SerializeField] private float hideDuration = 0.5f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve hideCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("색상 설정")]
        [SerializeField] private Color victoryColor = Color.green;
        [SerializeField] private Color defeatColor = Color.red;
        [SerializeField] private Color drawColor = Color.yellow;
        [SerializeField] private Color timeoutColor = new Color(1f, 0.65f, 0f); // Orange color

        [Header("아이콘")]
        [SerializeField] private Sprite victoryIcon;
        [SerializeField] private Sprite defeatIcon;
        [SerializeField] private Sprite drawIcon;
        [SerializeField] private Sprite timeoutIcon;

        [Header("사운드")]
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip defeatSound;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 현재 상태
        private BattleResult currentResult;
        private bool isShowing = false;

        private void Awake()
        {
            InitializeUI();
        }

        private void OnEnable()
        {
            // BattleResultManager 이벤트 구독
            if (BattleResultManager.Instance != null)
            {
                BattleResultManager.Instance.OnBattleResultDetermined += OnBattleResultDetermined;
            }
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (BattleResultManager.Instance != null)
            {
                BattleResultManager.Instance.OnBattleResultDetermined -= OnBattleResultDetermined;
            }
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 처음에는 숨김
            if (resultPanel != null)
                resultPanel.SetActive(false);

            // 버튼 이벤트 연결
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueButtonClicked);

            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryButtonClicked);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);

            // CanvasGroup 설정
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 전투 결과 표시
        /// </summary>
        public void ShowBattleResult(BattleResult result)
        {
            if (isShowing) return;

            currentResult = result;
            isShowing = true;

            if (debugMode)
                Debug.Log($"[BattleResultUI] 전투 결과 표시: {result.resultType}");

            // 패널 활성화
            if (resultPanel != null)
                resultPanel.SetActive(true);

            // 결과 정보 설정
            SetupResultDisplay(result);

            // 통계 정보 설정
            if (statisticsPanel != null && result.statistics != null)
            {
                SetupStatisticsDisplay(result.statistics);
                statisticsPanel.SetActive(true);
            }

            // 버튼 설정
            SetupButtons(result);

            // 사운드 재생
            PlayResultSound(result.resultType);

            // 애니메이션 시작
            StartCoroutine(ShowAnimation());
        }

        /// <summary>
        /// 결과 표시 설정
        /// </summary>
        private void SetupResultDisplay(BattleResult result)
        {
            // 제목 설정
            if (resultTitleText != null)
            {
                switch (result.resultType)
                {
                    case BattleResultType.Victory:
                        resultTitleText.text = "승리!";
                        resultTitleText.color = victoryColor;
                        break;
                    case BattleResultType.Defeat:
                        resultTitleText.text = "패배...";
                        resultTitleText.color = defeatColor;
                        break;
                    case BattleResultType.Draw:
                        resultTitleText.text = "무승부";
                        resultTitleText.color = drawColor;
                        break;
                    case BattleResultType.Timeout:
                        resultTitleText.text = "시간 초과";
                        resultTitleText.color = timeoutColor;
                        break;
                }
            }

            // 메시지 설정
            if (resultMessageText != null)
            {
                resultMessageText.text = result.resultMessage;
            }

            // 아이콘 설정
            if (resultIcon != null)
            {
                Sprite iconToUse = null;
                Color iconColor = Color.white;

                switch (result.resultType)
                {
                    case BattleResultType.Victory:
                        iconToUse = victoryIcon;
                        iconColor = victoryColor;
                        break;
                    case BattleResultType.Defeat:
                        iconToUse = defeatIcon;
                        iconColor = defeatColor;
                        break;
                    case BattleResultType.Draw:
                        iconToUse = drawIcon;
                        iconColor = drawColor;
                        break;
                    case BattleResultType.Timeout:
                        iconToUse = timeoutIcon;
                        iconColor = timeoutColor;
                        break;
                }

                if (iconToUse != null)
                {
                    resultIcon.sprite = iconToUse;
                    resultIcon.color = iconColor;
                    resultIcon.gameObject.SetActive(true);
                }
                else
                {
                    resultIcon.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 통계 표시 설정
        /// </summary>
        private void SetupStatisticsDisplay(BattleStatistics stats)
        {
            // 층 정보
            if (floorInfoText != null)
            {
                floorInfoText.text = $"{stats.floorNumber}층 ({GetFloorTypeKorean(stats.floorType)})";
            }

            // 전투 시간
            if (battleDurationText != null)
            {
                battleDurationText.text = $"전투 시간: {FormatTime(stats.battleDuration)}";
            }

            // 사용한 카드
            if (cardsUsedText != null)
            {
                cardsUsedText.text = $"사용한 카드: {stats.cardsUsed}장";
            }

            // 피해량
            if (damageDealtText != null)
            {
                damageDealtText.text = $"가한 피해: {stats.damageDealt}";
            }

            if (damageTakenText != null)
            {
                damageTakenText.text = $"받은 피해: {stats.damageTaken}";
            }

            // 조합 성공률
            if (combosSuccessText != null)
            {
                combosSuccessText.text = $"조합 성공률: {stats.combinationSuccessRate:F1}%";
            }

            // 효율성 지표
            if (efficiencyText != null)
            {
                efficiencyText.text = $"카드당 피해: {stats.damagePerCard:F1} | 턴당 피해: {stats.damagePerTurn:F1}";
            }
        }

        /// <summary>
        /// 버튼 설정
        /// </summary>
        private void SetupButtons(BattleResult result)
        {
            // 계속하기 버튼 (승리 시)
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(result.isVictory && result.shouldProceedToNext);
            }

            // 재시도 버튼 (패배 시)
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(!result.isVictory);
            }

            // 메인 메뉴 버튼 (항상 표시)
            if (mainMenuButton != null)
            {
                mainMenuButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 결과 사운드 재생
        /// </summary>
        private void PlayResultSound(BattleResultType resultType)
        {
            AudioClip soundToPlay = null;

            switch (resultType)
            {
                case BattleResultType.Victory:
                    soundToPlay = victorySound;
                    break;
                case BattleResultType.Defeat:
                case BattleResultType.Timeout:
                    soundToPlay = defeatSound;
                    break;
            }

            if (soundToPlay != null)
            {
                // AudioManager가 있으면 사용, 없으면 AudioSource로 직접 재생
                var audioManagerObj = GameObject.Find("AudioManager");
                if (audioManagerObj != null)
                {
                    var audioManager = audioManagerObj.GetComponent<MonoBehaviour>();
                    if (audioManager != null)
                    {
                        // AudioManager의 PlaySFX 메서드 사용 (있다면)
                        var playSFXMethod = audioManager.GetType().GetMethod("PlaySFX");
                        if (playSFXMethod != null)
                        {
                            playSFXMethod.Invoke(audioManager, new object[] { soundToPlay });
                        }
                    }
                }
                else
                {
                    // 직접 재생
                    var audioSource = Camera.main?.GetComponent<AudioSource>();
                    if (audioSource != null)
                    {
                        audioSource.PlayOneShot(soundToPlay);
                    }
                }
            }
        }

        /// <summary>
        /// 결과 UI 숨기기
        /// </summary>
        public void HideBattleResult()
        {
            if (!isShowing) return;

            StartCoroutine(HideAnimation());
        }

        /// <summary>
        /// 표시 애니메이션
        /// </summary>
        private IEnumerator ShowAnimation()
        {
            if (canvasGroup == null) yield break;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;

            // 페이드 인 애니메이션
            float elapsed = 0f;
            while (elapsed < showDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / showDuration;
                float curveValue = showCurve.Evaluate(t);

                canvasGroup.alpha = curveValue;

                // 스케일 애니메이션 (옵션)
                if (resultBackground != null)
                {
                    float scale = Mathf.Lerp(0.8f, 1f, curveValue);
                    resultBackground.transform.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            canvasGroup.alpha = 1f;
            if (resultBackground != null)
                resultBackground.transform.localScale = Vector3.one;

            canvasGroup.interactable = true;
        }

        /// <summary>
        /// 숨김 애니메이션
        /// </summary>
        private IEnumerator HideAnimation()
        {
            if (canvasGroup == null) yield break;

            canvasGroup.interactable = false;

            // 페이드 아웃 애니메이션
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < hideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hideDuration;
                float curveValue = hideCurve.Evaluate(t);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, curveValue);

                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // 패널 비활성화
            if (resultPanel != null)
                resultPanel.SetActive(false);

            isShowing = false;
            currentResult = null;
        }

        /// <summary>
        /// 층 타입 한국어 변환
        /// </summary>
        private string GetFloorTypeKorean(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Normal => "일반층",
                FloorType.Elite => "엘리트층",
                FloorType.Boss => "보스층",
                FloorType.Shop => "상점층",
                FloorType.Event => "이벤트층",
                FloorType.Start => "시작층",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// 시간 포맷팅
        /// </summary>
        private string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        #region Event Handlers
        /// <summary>
        /// 전투 결과 결정 이벤트 처리
        /// </summary>
        private void OnBattleResultDetermined(BattleResult result)
        {
            ShowBattleResult(result);
        }

        /// <summary>
        /// 계속하기 버튼 클릭
        /// </summary>
        private void OnContinueButtonClicked()
        {
            if (debugMode)
                Debug.Log("[BattleResultUI] 계속하기 버튼 클릭");

            HideBattleResult();

            // 다음 층 진행은 이미 BattleResultManager에서 처리됨
        }

        /// <summary>
        /// 재시도 버튼 클릭
        /// </summary>
        private void OnRetryButtonClicked()
        {
            if (debugMode)
                Debug.Log("[BattleResultUI] 재시도 버튼 클릭");

            HideBattleResult();

            // 1층으로 리셋은 이미 BattleResultManager에서 처리됨
            // 필요시 추가 처리 (플레이어 상태 초기화 등)
        }

        /// <summary>
        /// 메인 메뉴 버튼 클릭
        /// </summary>
        private void OnMainMenuButtonClicked()
        {
            if (debugMode)
                Debug.Log("[BattleResultUI] 메인 메뉴 버튼 클릭");

            HideBattleResult();

            // 메인 메뉴로 이동
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.ReturnToMainMenu();
            }
        }
        #endregion

        /// <summary>
        /// 강제로 결과 UI 새로고침 (테스트용)
        /// </summary>
        [ContextMenu("Test Victory")]
        public void TestVictory()
        {
            var testStats = new BattleStatistics();
            testStats.Initialize(5, FloorType.Elite);
            testStats.cardsUsed = 12;
            testStats.damageDealt = 150;
            testStats.damageTaken = 45;
            testStats.combinationsAttempted = 8;
            testStats.successfulCombinations = 6;
            testStats.Finalize();

            var testResult = new BattleResult(BattleResultType.Victory, testStats);
            ShowBattleResult(testResult);
        }

        /// <summary>
        /// 패배 테스트
        /// </summary>
        [ContextMenu("Test Defeat")]
        public void TestDefeat()
        {
            var testStats = new BattleStatistics();
            testStats.Initialize(3, FloorType.Normal);
            testStats.cardsUsed = 8;
            testStats.damageDealt = 80;
            testStats.damageTaken = 100;
            testStats.combinationsAttempted = 5;
            testStats.successfulCombinations = 2;
            testStats.Finalize();

            var testResult = new BattleResult(BattleResultType.Defeat, testStats);
            ShowBattleResult(testResult);
        }
    }
}