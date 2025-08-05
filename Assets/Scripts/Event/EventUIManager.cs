using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Player;

namespace Maglin.Event
{
    /// <summary>
    /// 이벤트 UI를 관리하는 매니저 클래스
    /// </summary>
    public class EventUIManager : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private CanvasGroup eventCanvasGroup;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("이벤트 정보 UI")]
        [SerializeField] private Image eventImage;
        [SerializeField] private TextMeshProUGUI eventTitle;
        [SerializeField] private TextMeshProUGUI eventDescription;

        [Header("선택지 UI")]
        [SerializeField] private Transform choicesContent;
        [SerializeField] private GameObject choiceButtonPrefab;

        [Header("결과 UI")]
        [SerializeField] private CanvasGroup resultCanvasGroup;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button continueButton;

        // 싱글톤
        public static EventUIManager Instance { get; private set; }

        // 현재 상태
        private EventSO currentEvent;
        private List<GameObject> choiceButtons = new List<GameObject>();

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeUI();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 초기 상태 업데이트
            StartCoroutine(InitialUpdate());
        }

        /// <summary>
        /// 초기 업데이트 (다른 매니저들이 초기화된 후)
        /// </summary>
        private System.Collections.IEnumerator InitialUpdate()
        {
            yield return new WaitForEndOfFrame();

            // 플레이어 상태 초기 업데이트
            UpdatePlayerStatus();

            // 이벤트 UI 숨김
            HideEventUI();
            HideResultUI();
        }

        private void OnEnable()
        {
            // EventManager 이벤트 구독 (지연 처리)
            StartCoroutine(SubscribeToEventManagerEvents());

            // PlayerManager static 이벤트 구독
            PlayerManager.OnGoldChanged += OnGoldChanged;
            PlayerManager.OnHealthChanged += OnHealthChanged;
        }

        /// <summary>
        /// EventManager 이벤트 구독 (안전하게 처리)
        /// </summary>
        private System.Collections.IEnumerator SubscribeToEventManagerEvents()
        {
            // EventManager가 초기화될 때까지 대기
            while (EventManager.Instance == null)
            {
                yield return null;
            }

            // 이벤트 구독
            EventManager.Instance.OnEventStarted += OnEventStarted;
            EventManager.Instance.OnEventEnded += OnEventEnded;
            EventManager.Instance.OnChoiceSelected += OnChoiceSelected;
            EventManager.Instance.OnRewardsApplied += OnRewardsApplied;

            if (debugMode)
                Debug.Log("[EventUIManager] EventManager 이벤트 구독 완료");
        }

        private void OnDisable()
        {
            // EventManager 이벤트 구독 해제
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEventStarted -= OnEventStarted;
                EventManager.Instance.OnEventEnded -= OnEventEnded;
                EventManager.Instance.OnChoiceSelected -= OnChoiceSelected;
                EventManager.Instance.OnRewardsApplied -= OnRewardsApplied;
            }

            // PlayerManager static 이벤트 구독 해제
            PlayerManager.OnGoldChanged -= OnGoldChanged;
            PlayerManager.OnHealthChanged -= OnHealthChanged;
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            if (debugMode)
                Debug.Log("[EventUIManager] UI 초기화 시작");

            // UI 참조 자동 찾기
            FindUIReferences();

            // 버튼 이벤트 연결
            SetupButtonEvents();

            // 초기 상태 설정
            if (eventCanvasGroup != null)
            {
                eventCanvasGroup.alpha = 0f;
                eventCanvasGroup.interactable = false;
                eventCanvasGroup.blocksRaycasts = false;
            }

            // 결과 UI 숨김
            if (resultCanvasGroup != null)
            {
                resultCanvasGroup.alpha = 0f;
                resultCanvasGroup.interactable = false;
                resultCanvasGroup.blocksRaycasts = false;
            }

            if (debugMode)
                Debug.Log("[EventUIManager] UI 초기화 완료");
        }

        /// <summary>
        /// UI 참조 자동 찾기
        /// </summary>
        private void FindUIReferences()
        {
            // 메인 이벤트 UI 찾기
            if (eventCanvasGroup == null)
            {
                var eventUI = GameObject.Find("EventUI");
                if (eventUI != null)
                {
                    eventCanvasGroup = eventUI.GetComponent<CanvasGroup>();
                }
            }

            // 플레이어 상태 텍스트 찾기
            if (goldText == null && eventCanvasGroup != null)
            {
                // EventUI > PlayerStatusPanel > GoldText
                var goldTextObj = eventCanvasGroup.transform.Find("PlayerStatusPanel/GoldText");
                if (goldTextObj != null)
                {
                    goldText = goldTextObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (healthText == null && eventCanvasGroup != null)
            {
                // EventUI > PlayerStatusPanel > HealthText
                var healthTextObj = eventCanvasGroup.transform.Find("PlayerStatusPanel/HealthText");
                if (healthTextObj != null)
                {
                    healthText = healthTextObj.GetComponent<TextMeshProUGUI>();
                }
            }

            // 이벤트 정보 UI 찾기
            FindEventInfoUI();

            // 선택지 UI 찾기
            FindChoicesUI();

            // 결과 UI 찾기
            FindResultUI();
        }

        /// <summary>
        /// 이벤트 정보 UI 찾기
        /// </summary>
        private void FindEventInfoUI()
        {
            if (eventCanvasGroup == null) return;

            if (eventImage == null)
            {
                // EventUI > EventInfoArea > EventContent > EventImageArea
                var imageObj = eventCanvasGroup.transform.Find("EventInfoArea/EventContent/EventImageArea");
                if (imageObj != null)
                {
                    eventImage = imageObj.GetComponent<Image>();
                }
            }

            if (eventTitle == null)
            {
                // EventUI > EventInfoArea > EventContent > EventTitle
                var titleObj = eventCanvasGroup.transform.Find("EventInfoArea/EventContent/EventTitle");
                if (titleObj != null)
                {
                    eventTitle = titleObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (eventDescription == null)
            {
                // EventUI > EventInfoArea > EventContent > EventDescription
                var descObj = eventCanvasGroup.transform.Find("EventInfoArea/EventContent/EventDescription");
                if (descObj != null)
                {
                    eventDescription = descObj.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// 선택지 UI 찾기
        /// </summary>
        private void FindChoicesUI()
        {
            if (eventCanvasGroup == null) return;

            if (choicesContent == null)
            {
                // EventUI > ChoicesArea > ChoicesScrollView > Viewport > ChoicesContent
                var contentObj = eventCanvasGroup.transform.Find("ChoicesArea/ChoicesScrollView/Viewport/ChoicesContent");
                if (contentObj != null)
                {
                    choicesContent = contentObj.transform;
                }
            }
        }

        /// <summary>
        /// 결과 UI 찾기
        /// </summary>
        private void FindResultUI()
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 결과 UI 찾기 시작 - eventCanvasGroup: {eventCanvasGroup != null}");

            if (resultCanvasGroup == null && eventCanvasGroup != null)
            {
                // EventUI 안에서 EventResultUI를 찾기
                var resultUI = eventCanvasGroup.transform.Find("EventResultUI");
                if (resultUI != null)
                {
                    resultCanvasGroup = resultUI.GetComponent<CanvasGroup>();
                    if (debugMode)
                        Debug.Log($"[EventUIManager] EventResultUI 찾음: {resultCanvasGroup != null}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[EventUIManager] EventResultUI를 찾을 수 없습니다!");
                }
            }

            if (resultText == null && resultCanvasGroup != null)
            {
                // EventResultUI 안에서 ResultText를 찾기
                var textObj = resultCanvasGroup.transform.Find("ResultPanel/ResultText");
                if (textObj != null)
                {
                    resultText = textObj.GetComponent<TextMeshProUGUI>();
                    if (debugMode)
                        Debug.Log($"[EventUIManager] ResultText 찾음: {resultText != null}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[EventUIManager] ResultText를 찾을 수 없습니다!");
                }
            }

            if (continueButton == null && resultCanvasGroup != null)
            {
                // EventResultUI 안에서 ContinueButton을 찾기
                var buttonObj = resultCanvasGroup.transform.Find("ResultPanel/ContinueButton");
                if (buttonObj != null)
                {
                    continueButton = buttonObj.GetComponent<Button>();
                    if (debugMode)
                        Debug.Log($"[EventUIManager] ContinueButton 찾음: {continueButton != null}");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[EventUIManager] ContinueButton을 찾을 수 없습니다!");
                }
            }

            if (debugMode)
                Debug.Log($"[EventUIManager] 결과 UI 찾기 완료 - resultCanvasGroup: {resultCanvasGroup != null}, resultText: {resultText != null}, continueButton: {continueButton != null}");
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            // 계속 버튼
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinueButtonClicked);
            }
        }

        /// <summary>
        /// 이벤트 시작 이벤트 처리
        /// </summary>
        private void OnEventStarted(EventSO eventData)
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 이벤트 시작: {eventData.EventName}");

            currentEvent = eventData;
            UpdateEventInfo();
            UpdateChoices();
            ShowEventUI();
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 이벤트 종료 이벤트 처리
        /// </summary>
        private void OnEventEnded()
        {
            if (debugMode)
                Debug.Log("[EventUIManager] 이벤트 종료");

            currentEvent = null;
            HideEventUI();
        }

        /// <summary>
        /// 선택지 선택 이벤트 처리
        /// </summary>
        private void OnChoiceSelected(EventChoice choice, bool isSuccess)
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 선택지 결과: {choice.choiceText} - {(isSuccess ? "성공" : "실패")}");

            // 선택지 버튼들 비활성화
            DisableChoiceButtons();

            // 결과 텍스트 표시
            string resultMessage = isSuccess ? choice.resultText : choice.failureText;
            if (string.IsNullOrEmpty(resultMessage))
                resultMessage = isSuccess ? "성공했습니다!" : "실패했습니다...";

            ShowResult(resultMessage);
        }

        /// <summary>
        /// 보상 적용 이벤트 처리
        /// </summary>
        private void OnRewardsApplied(EventReward[] rewards, bool isSuccess)
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 보상 적용 완료 - 성공: {isSuccess}");

            // 플레이어 상태 업데이트
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 이벤트 정보 업데이트
        /// </summary>
        private void UpdateEventInfo()
        {
            if (currentEvent == null) return;

            // 이벤트 이미지
            if (eventImage != null)
            {
                if (currentEvent.Image != null)
                {
                    eventImage.sprite = currentEvent.Image;
                    eventImage.color = Color.white;
                }
                else
                {
                    eventImage.sprite = null;
                    eventImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);
                }
            }

            // 이벤트 제목
            if (eventTitle != null)
            {
                eventTitle.text = currentEvent.EventName;
            }

            // 이벤트 설명
            if (eventDescription != null)
            {
                eventDescription.text = currentEvent.Description;
            }

            if (debugMode)
                Debug.Log($"[EventUIManager] 이벤트 정보 업데이트: {currentEvent.EventName}");
        }

        /// <summary>
        /// 선택지 업데이트
        /// </summary>
        private void UpdateChoices()
        {
            if (currentEvent == null || choicesContent == null) return;

            // 기존 선택지 버튼들 제거
            ClearChoiceButtons();

            // 새로운 선택지 버튼들 생성
            for (int i = 0; i < currentEvent.Choices.Length; i++)
            {
                CreateChoiceButton(currentEvent.Choices[i], i);
            }

            if (debugMode)
                Debug.Log($"[EventUIManager] {currentEvent.Choices.Length}개 선택지 버튼 생성");
        }

        /// <summary>
        /// 선택지 버튼 생성
        /// </summary>
        private void CreateChoiceButton(EventChoice choice, int choiceIndex)
        {
            GameObject choiceButton;

            // 프리팹이 있으면 프리팹 사용, 없으면 동적 생성
            if (choiceButtonPrefab != null)
            {
                choiceButton = Instantiate(choiceButtonPrefab, choicesContent);
                choiceButton.name = $"ChoiceButton_{choiceIndex}";

                // 프리팹의 버튼 컴포넌트 찾아서 이벤트 연결
                var button = choiceButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));
                }

                // 프리팹의 텍스트 업데이트
                UpdateChoiceButtonFromPrefab(choiceButton, choice);
            }
            else
            {
                choiceButton = CreateBasicChoiceButton(choice, choiceIndex);
            }

            if (choiceButton != null)
            {
                choiceButtons.Add(choiceButton);
            }
        }

        /// <summary>
        /// 기본 선택지 버튼 생성
        /// </summary>
        private GameObject CreateBasicChoiceButton(EventChoice choice, int choiceIndex)
        {
            GameObject choiceButton = new GameObject($"ChoiceButton_{choiceIndex}");
            choiceButton.transform.SetParent(choicesContent, false);

            // RectTransform
            RectTransform buttonRect = choiceButton.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0, 80);

            // 배경 이미지
            Image buttonImage = choiceButton.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.3f, 0.4f, 1f);

            // 버튼 컴포넌트
            Button button = choiceButton.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            // 버튼 이벤트 연결
            button.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));

            // 레이아웃 요소
            LayoutElement layoutElement = choiceButton.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 80;
            layoutElement.flexibleHeight = 0;

            // 버튼 내용 생성
            CreateChoiceButtonContent(choiceButton, choice);

            return choiceButton;
        }

        /// <summary>
        /// 프리팹 선택지 버튼 내용 업데이트
        /// </summary>
        private void UpdateChoiceButtonFromPrefab(GameObject choiceButton, EventChoice choice)
        {
            // ChoiceText 찾아서 업데이트
            var choiceTextObj = choiceButton.transform.Find("ChoiceContent/ChoiceText");
            if (choiceTextObj != null)
            {
                var choiceText = choiceTextObj.GetComponent<TextMeshProUGUI>();
                if (choiceText != null)
                {
                    choiceText.text = choice.choiceText;
                }
            }

            // ProbabilityText 찾아서 업데이트
            var probabilityObj = choiceButton.transform.Find("ChoiceContent/ProbabilityText");
            if (probabilityObj != null)
            {
                var probabilityText = probabilityObj.GetComponent<TextMeshProUGUI>();
                if (probabilityText != null)
                {
                    probabilityText.text = $"성공 확률: {(choice.successRate * 100):F0}%";
                }
            }

            if (debugMode)
                Debug.Log($"[EventUIManager] 프리팹 선택지 버튼 업데이트: {choice.choiceText}");
        }

        /// <summary>
        /// 선택지 버튼 내용 생성
        /// </summary>
        private void CreateChoiceButtonContent(GameObject parent, EventChoice choice)
        {
            GameObject content = new GameObject("ChoiceContent");
            content.transform.SetParent(parent.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(15, 10);
            contentRect.offsetMax = new Vector2(-15, -10);

            // 레이아웃 그룹
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 5;

            // 선택지 텍스트
            GameObject choiceTextObj = new GameObject("ChoiceText");
            choiceTextObj.transform.SetParent(content.transform, false);

            TextMeshProUGUI choiceText = choiceTextObj.AddComponent<TextMeshProUGUI>();
            choiceText.text = choice.choiceText;
            choiceText.fontSize = 16;
            choiceText.color = Color.white;
            choiceText.alignment = TextAlignmentOptions.TopLeft;
            choiceText.enableWordWrapping = true;

            // 성공 확률 텍스트
            GameObject probabilityObj = new GameObject("ProbabilityText");
            probabilityObj.transform.SetParent(content.transform, false);

            TextMeshProUGUI probabilityText = probabilityObj.AddComponent<TextMeshProUGUI>();
            probabilityText.text = $"성공 확률: {(choice.successRate * 100):F0}%";
            probabilityText.fontSize = 12;
            probabilityText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            probabilityText.alignment = TextAlignmentOptions.TopLeft;

            // 레이아웃 요소들
            LayoutElement choiceLayout = choiceTextObj.AddComponent<LayoutElement>();
            choiceLayout.flexibleHeight = 1;

            LayoutElement probLayout = probabilityObj.AddComponent<LayoutElement>();
            probLayout.preferredHeight = 20;
            probLayout.flexibleHeight = 0;
        }

        /// <summary>
        /// 선택지 버튼들 제거
        /// </summary>
        private void ClearChoiceButtons()
        {
            foreach (var button in choiceButtons)
            {
                if (button != null)
                {
                    Destroy(button);
                }
            }
            choiceButtons.Clear();
        }

        /// <summary>
        /// 선택지 버튼들 비활성화
        /// </summary>
        private void DisableChoiceButtons()
        {
            foreach (var buttonObj in choiceButtons)
            {
                if (buttonObj != null)
                {
                    var button = buttonObj.GetComponent<Button>();
                    if (button != null)
                    {
                        button.interactable = false;
                    }
                }
            }
        }

        /// <summary>
        /// 플레이어 상태 업데이트
        /// </summary>
        private void UpdatePlayerStatus()
        {
            if (PlayerManager.Instance == null)
            {
                if (debugMode)
                    Debug.LogWarning("[EventUIManager] PlayerManager.Instance가 null입니다!");
                return;
            }

            // 골드 업데이트
            if (goldText != null)
            {
                goldText.text = $"골드: {PlayerManager.Instance.CurrentGold}";
            }

            // 체력 업데이트
            if (healthText != null)
            {
                int currentHealth = PlayerManager.Instance.CurrentHealth;
                int maxHealth = PlayerManager.Instance.MaxHealth;
                healthText.text = $"체력: {currentHealth}/{maxHealth}";

                if (debugMode)
                    Debug.Log($"[EventUIManager] 체력 UI 업데이트: {currentHealth}/{maxHealth}");
            }
        }

        /// <summary>
        /// 결과 표시
        /// </summary>
        private void ShowResult(string message)
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 결과 표시 시도: {message}");

            if (resultText != null)
            {
                resultText.text = message;
                if (debugMode)
                    Debug.Log($"[EventUIManager] 결과 텍스트 설정 완료: {message}");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[EventUIManager] resultText가 null입니다!");
            }

            if (resultCanvasGroup != null)
            {
                resultCanvasGroup.alpha = 1f;
                resultCanvasGroup.interactable = true;
                resultCanvasGroup.blocksRaycasts = true;
                if (debugMode)
                    Debug.Log("[EventUIManager] 결과 UI 표시 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogError("[EventUIManager] resultCanvasGroup이 null입니다! 결과 UI를 표시할 수 없습니다.");
            }
        }

        /// <summary>
        /// 결과 UI 숨김
        /// </summary>
        private void HideResultUI()
        {
            if (resultCanvasGroup != null)
            {
                resultCanvasGroup.alpha = 0f;
                resultCanvasGroup.interactable = false;
                resultCanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 골드 변경 이벤트 처리
        /// </summary>
        private void OnGoldChanged(int newGold)
        {
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 체력 변경 이벤트 처리
        /// </summary>
        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 선택지 버튼 클릭 이벤트 처리
        /// </summary>
        private void OnChoiceButtonClicked(int choiceIndex)
        {
            if (debugMode)
                Debug.Log($"[EventUIManager] 선택지 {choiceIndex} 클릭");

            if (EventManager.Instance != null)
            {
                EventManager.Instance.SelectChoice(choiceIndex);
            }
        }

        /// <summary>
        /// 계속 버튼 클릭
        /// </summary>
        private void OnContinueButtonClicked()
        {
            if (debugMode)
                Debug.Log("[EventUIManager] 계속 버튼 클릭");

            HideResultUI();

            if (EventManager.Instance != null)
            {
                EventManager.Instance.EndEvent();
            }
        }

        /// <summary>
        /// 이벤트 UI 표시
        /// </summary>
        public void ShowEventUI()
        {
            if (eventCanvasGroup != null)
            {
                eventCanvasGroup.alpha = 1f;
                eventCanvasGroup.interactable = true;
                eventCanvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 이벤트 UI 숨김
        /// </summary>
        public void HideEventUI()
        {
            if (eventCanvasGroup != null)
            {
                eventCanvasGroup.alpha = 0f;
                eventCanvasGroup.interactable = false;
                eventCanvasGroup.blocksRaycasts = false;
            }

            HideResultUI();
        }
    }
}