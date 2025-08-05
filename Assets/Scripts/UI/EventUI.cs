using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Event;

namespace Maglin.UI
{
    /// <summary>
    /// 이벤트 UI를 관리하는 클래스
    /// </summary>
    public class EventUI : MonoBehaviour
    {
        [Header("UI 패널")]
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private GameObject resultPanel;

        [Header("이벤트 표시")]
        [SerializeField] private TextMeshProUGUI eventNameText;
        [SerializeField] private TextMeshProUGUI eventDescriptionText;
        [SerializeField] private Image eventImage;

        [Header("선택지")]
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private GameObject choiceButtonPrefab;

        [Header("결과 표시")]
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button continueButton;

        [Header("설정")]
        [SerializeField] private float resultDisplayDuration = 3f;

        // 현재 상태
        private List<Button> choiceButtons = new List<Button>();
        private EventSO currentEvent;
        private bool isShowingResult = false;

        private void Awake()
        {
            // 버튼 이벤트 설정
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueButtonClicked);

            // 초기 상태 설정
            if (eventPanel != null)
                eventPanel.SetActive(false);

            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // EventManager 이벤트 구독
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEventStarted += OnEventStarted;
                EventManager.Instance.OnEventEnded += OnEventEnded;
                EventManager.Instance.OnChoiceSelected += OnChoiceSelected;
                EventManager.Instance.OnRewardsApplied += OnRewardsApplied;
            }
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEventStarted -= OnEventStarted;
                EventManager.Instance.OnEventEnded -= OnEventEnded;
                EventManager.Instance.OnChoiceSelected -= OnChoiceSelected;
                EventManager.Instance.OnRewardsApplied -= OnRewardsApplied;
            }
        }

        /// <summary>
        /// 이벤트 시작 시 호출
        /// </summary>
        private void OnEventStarted(EventSO eventData)
        {
            currentEvent = eventData;
            ShowEvent();
        }

        /// <summary>
        /// 이벤트 종료 시 호출
        /// </summary>
        private void OnEventEnded()
        {
            if (!isShowingResult)
            {
                HideEvent();
            }
        }

        /// <summary>
        /// 선택지 선택 시 호출
        /// </summary>
        private void OnChoiceSelected(EventChoice choice, bool isSuccess)
        {
            string resultMessage = isSuccess ? choice.resultText : choice.failureText;

            if (string.IsNullOrEmpty(resultMessage))
            {
                resultMessage = isSuccess ? "선택이 성공했습니다!" : "선택이 실패했습니다...";
            }

            ShowResult(resultMessage);
        }

        /// <summary>
        /// 보상 적용 시 호출
        /// </summary>
        private void OnRewardsApplied(EventReward[] rewards, bool isSuccess)
        {
            // 보상/페널티 정보를 결과 텍스트에 추가할 수 있음
            // 현재는 기본 결과 메시지만 표시
        }

        /// <summary>
        /// 이벤트 UI 표시
        /// </summary>
        public void ShowEvent()
        {
            if (currentEvent == null)
            {
                Debug.LogError("No current event to display!");
                return;
            }

            // 패널 활성화
            if (eventPanel != null)
                eventPanel.SetActive(true);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            // 이벤트 정보 표시
            UpdateEventDisplay();

            // 선택지 생성
            CreateChoiceButtons();

            isShowingResult = false;
        }

        /// <summary>
        /// 이벤트 UI 숨기기
        /// </summary>
        public void HideEvent()
        {
            if (eventPanel != null)
                eventPanel.SetActive(false);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            // 선택지 버튼 정리
            ClearChoiceButtons();

            currentEvent = null;
            isShowingResult = false;
        }

        /// <summary>
        /// 이벤트 표시 내용 업데이트
        /// </summary>
        private void UpdateEventDisplay()
        {
            if (currentEvent == null) return;

            // 이벤트 이름
            if (eventNameText != null)
                eventNameText.text = currentEvent.EventName;

            // 이벤트 설명
            if (eventDescriptionText != null)
                eventDescriptionText.text = currentEvent.Description;

            // 이벤트 이미지
            if (eventImage != null && currentEvent.Image != null)
            {
                eventImage.sprite = currentEvent.Image;
                eventImage.gameObject.SetActive(true);
            }
            else if (eventImage != null)
            {
                eventImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 선택지 버튼 생성
        /// </summary>
        private void CreateChoiceButtons()
        {
            ClearChoiceButtons();

            if (currentEvent == null || choicesContainer == null || choiceButtonPrefab == null)
                return;

            for (int i = 0; i < currentEvent.Choices.Length; i++)
            {
                CreateChoiceButton(currentEvent.Choices[i], i);
            }
        }

        /// <summary>
        /// 개별 선택지 버튼 생성
        /// </summary>
        private void CreateChoiceButton(EventChoice choice, int index)
        {
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choicesContainer);
            Button button = buttonObj.GetComponent<Button>();

            if (button == null)
            {
                button = buttonObj.AddComponent<Button>();
            }

            // 텍스트 설정
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = choice.choiceText;
            }

            // 클릭 이벤트 설정
            int choiceIndex = index; // 클로저 캡처를 위한 로컬 복사
            button.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));

            choiceButtons.Add(button);
        }

        /// <summary>
        /// 선택지 버튼 정리
        /// </summary>
        private void ClearChoiceButtons()
        {
            foreach (var button in choiceButtons)
            {
                if (button != null && button.gameObject != null)
                    Destroy(button.gameObject);
            }
            choiceButtons.Clear();
        }

        /// <summary>
        /// 선택지 버튼 클릭 처리
        /// </summary>
        private void OnChoiceButtonClicked(int choiceIndex)
        {
            if (EventManager.Instance != null)
            {
                // 선택지 버튼 비활성화 (중복 클릭 방지)
                foreach (var button in choiceButtons)
                {
                    if (button != null)
                        button.interactable = false;
                }

                EventManager.Instance.SelectChoice(choiceIndex);
            }
        }

        /// <summary>
        /// 결과 표시
        /// </summary>
        private void ShowResult(string resultMessage)
        {
            isShowingResult = true;

            // 이벤트 패널 숨기고 결과 패널 표시
            if (eventPanel != null)
                eventPanel.SetActive(false);

            if (resultPanel != null)
                resultPanel.SetActive(true);

            // 결과 텍스트 설정
            if (resultText != null)
                resultText.text = resultMessage;

            // 자동으로 결과 화면 닫기
            if (resultDisplayDuration > 0)
            {
                Invoke(nameof(HideResult), resultDisplayDuration);
            }
        }

        /// <summary>
        /// 결과 화면 숨기기
        /// </summary>
        private void HideResult()
        {
            HideEvent();
        }

        /// <summary>
        /// 계속하기 버튼 클릭
        /// </summary>
        private void OnContinueButtonClicked()
        {
            CancelInvoke(nameof(HideResult)); // 자동 닫기 취소
            HideResult();
        }

        /// <summary>
        /// 강제로 이벤트 표시 (테스트용)
        /// </summary>
        public void ShowTestEvent(EventSO eventData)
        {
            if (eventData == null)
            {
                Debug.LogError("Event data is null!");
                return;
            }

            currentEvent = eventData;
            ShowEvent();
        }
    }
}