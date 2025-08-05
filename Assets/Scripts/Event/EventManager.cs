using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Maglin.Core;
using Maglin.Cards;
using Maglin.Relics;
using Maglin.Player;

namespace Maglin.Event
{
    /// <summary>
    /// 이벤트 시스템을 관리하는 매니저 클래스
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        [Header("이벤트 설정")]
        [SerializeField] private EventSO[] availableEvents;
        [SerializeField] private float eventChance = 0.3f;  // 이벤트 발생 확률

        [Header("현재 이벤트 상태")]
        [SerializeField] private EventSO currentEvent;
        [SerializeField] private bool isEventActive = false;

        // 싱글톤
        public static EventManager Instance { get; private set; }

        // 이벤트
        public System.Action<EventSO> OnEventStarted;
        public System.Action OnEventEnded;
        public System.Action<EventChoice, bool> OnChoiceSelected; // 선택지, 성공여부
        public System.Action<EventReward[], bool> OnRewardsApplied; // 보상/페널티, 성공여부

        // 현재 이벤트 정보
        public EventSO CurrentEvent => currentEvent;
        public bool IsEventActive => isEventActive;

        // 한 번만 등장하는 이벤트 추적
        private HashSet<string> usedOneTimeEvents = new HashSet<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 이벤트 발생 체크 (층 이동 시 호출)
        /// </summary>
        public bool TryTriggerEvent(int currentFloor)
        {
            if (isEventActive)
            {
                Debug.LogWarning("Event is already active!");
                return false;
            }

            // 이벤트 발생 확률 체크
            if (Random.Range(0f, 1f) > eventChance)
                return false;

            // 현재 층에서 등장 가능한 이벤트 선택
            EventSO selectedEvent = SelectRandomEvent(currentFloor);
            if (selectedEvent == null)
                return false;

            StartEvent(selectedEvent);
            return true;
        }

        /// <summary>
        /// 강제로 이벤트 시작 (테스트용)
        /// </summary>
        public void ForceStartEvent(EventSO eventData)
        {
            if (eventData == null)
            {
                Debug.LogError("Event data is null!");
                return;
            }

            StartEvent(eventData);
        }

        /// <summary>
        /// 현재 층에서 등장 가능한 이벤트 중 랜덤 선택
        /// </summary>
        private EventSO SelectRandomEvent(int currentFloor)
        {
            if (availableEvents == null || availableEvents.Length == 0)
                return null;

            // 현재 층에서 등장 가능하고 유효한 이벤트들 필터링
            var validEvents = availableEvents.Where(e =>
                e != null &&
                e.IsValid() &&
                e.CanAppearOnFloor(currentFloor) &&
                (!e.IsOneTimeOnly || !usedOneTimeEvents.Contains(e.EventName))
            ).ToList();

            if (validEvents.Count == 0)
                return null;

            // 가중치 기반 선택
            float totalWeight = validEvents.Sum(e => e.SpawnWeight);
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var eventData in validEvents)
            {
                currentWeight += eventData.SpawnWeight;
                if (randomValue <= currentWeight)
                {
                    return eventData;
                }
            }

            return validEvents[validEvents.Count - 1]; // 안전장치
        }

        /// <summary>
        /// 이벤트 시작
        /// </summary>
        private void StartEvent(EventSO eventData)
        {
            currentEvent = eventData;
            isEventActive = true;

            // 한 번만 등장하는 이벤트라면 사용됨으로 표시
            if (eventData.IsOneTimeOnly)
            {
                usedOneTimeEvents.Add(eventData.EventName);
            }

            Debug.Log($"Event started: {eventData.EventName}");
            OnEventStarted?.Invoke(eventData);
        }

        /// <summary>
        /// 선택지 선택
        /// </summary>
        public void SelectChoice(int choiceIndex)
        {
            if (!isEventActive || currentEvent == null)
            {
                Debug.LogError("No active event!");
                return;
            }

            if (choiceIndex < 0 || choiceIndex >= currentEvent.Choices.Length)
            {
                Debug.LogError($"Invalid choice index: {choiceIndex}");
                return;
            }

            EventChoice selectedChoice = currentEvent.Choices[choiceIndex];
            ProcessChoice(selectedChoice);
        }

        /// <summary>
        /// 선택지 처리
        /// </summary>
        private void ProcessChoice(EventChoice choice)
        {
            // 성공/실패 판정
            bool isSuccess = Random.Range(0f, 1f) <= choice.successRate;

            OnChoiceSelected?.Invoke(choice, isSuccess);

            if (isSuccess)
            {
                // 성공 시 보상 적용
                ApplyRewards(choice.rewards, isSuccess);
                ApplyPenalties(choice.penalties, isSuccess);
            }
            else
            {
                // 실패 시 페널티 적용
                ApplyPenalties(choice.failurePenalties, isSuccess);
            }

            // 이벤트 종료는 UI에서 "계속" 버튼을 눌렀을 때 호출되도록 변경
            // EndEvent();
        }

        /// <summary>
        /// 보상 적용
        /// </summary>
        private void ApplyRewards(EventReward[] rewards, bool isSuccess)
        {
            if (rewards == null || rewards.Length == 0)
                return;

            foreach (var reward in rewards)
            {
                ApplyReward(reward, isSuccess);
            }

            OnRewardsApplied?.Invoke(rewards, isSuccess);
        }

        /// <summary>
        /// 페널티 적용
        /// </summary>
        private void ApplyPenalties(EventReward[] penalties, bool isSuccess)
        {
            if (penalties == null || penalties.Length == 0)
                return;

            foreach (var penalty in penalties)
            {
                ApplyReward(penalty, isSuccess, true);
            }

            OnRewardsApplied?.Invoke(penalties, isSuccess);
        }

        /// <summary>
        /// 개별 보상/페널티 적용
        /// </summary>
        private void ApplyReward(EventReward reward, bool isSuccess, bool isPenalty = false)
        {
            if (PlayerManager.Instance == null)
            {
                Debug.LogError("PlayerManager not found!");
                return;
            }

            int actualAmount = currentEvent.GetActualAmount(reward);

            // 페널티인 경우 음수로 변환
            if (isPenalty && actualAmount > 0)
                actualAmount = -actualAmount;

            switch (reward.rewardType)
            {
                case EventRewardType.Gold:
                    if (actualAmount > 0)
                    {
                        PlayerManager.Instance.AddGold(actualAmount);
                        Debug.Log($"Gained {actualAmount} gold");
                    }
                    else
                    {
                        PlayerManager.Instance.SpendGold(Mathf.Abs(actualAmount));
                        Debug.Log($"Lost {Mathf.Abs(actualAmount)} gold");
                    }
                    break;

                case EventRewardType.Health:
                    if (actualAmount > 0)
                    {
                        PlayerManager.Instance.Heal(actualAmount);
                        Debug.Log($"Restored {actualAmount} HP");
                    }
                    else
                    {
                        PlayerManager.Instance.TakeDamage(Mathf.Abs(actualAmount));
                        Debug.Log($"Lost {Mathf.Abs(actualAmount)} HP");
                    }
                    break;

                case EventRewardType.MaxHealth:
                    PlayerManager.Instance.ChangeMaxHealth(actualAmount);
                    Debug.Log($"Max health changed by {actualAmount}");
                    break;

                case EventRewardType.Card:
                    if (reward.cardData != null && CardManager.Instance != null)
                    {
                        if (!isPenalty)
                        {
                            CardManager.Instance.AddCardToDeck(reward.cardData);
                            Debug.Log($"Added card: {reward.cardData.CardName}");
                        }
                    }
                    break;

                case EventRewardType.CardRemoval:
                    if (CardManager.Instance != null)
                    {
                        // 카드 제거 UI 열기 (추후 구현)
                        Debug.Log("Card removal activated");
                    }
                    break;

                case EventRewardType.Relic:
                    if (reward.relicData != null && !isPenalty)
                    {
                        PlayerManager.Instance.AddRelic(reward.relicData);
                        Debug.Log($"Added relic: {reward.relicData.RelicName}");
                    }
                    break;

                case EventRewardType.DeckUpgrade:
                    if (CardManager.Instance != null)
                    {
                        // 덱 업그레이드 UI 열기 (추후 구현)
                        Debug.Log("Deck upgrade activated");
                    }
                    break;
            }
        }

        /// <summary>
        /// 이벤트 종료
        /// </summary>
        public void EndEvent()
        {
            if (!isEventActive)
                return;

            Debug.Log($"Event ended: {currentEvent?.EventName}");

            currentEvent = null;
            isEventActive = false;

            OnEventEnded?.Invoke();

            // FloorManager에게 이벤트 완료 알림
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnContentCompleted();
            }
        }

        /// <summary>
        /// 사용된 일회성 이벤트 초기화 (게임 시작 시)
        /// </summary>
        public void ResetOneTimeEvents()
        {
            usedOneTimeEvents.Clear();
        }

        /// <summary>
        /// 이벤트 발생 확률 설정
        /// </summary>
        public void SetEventChance(float chance)
        {
            eventChance = Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 사용 가능한 이벤트 설정 (테스트용)
        /// </summary>
        public void SetAvailableEvents(EventSO[] events)
        {
            availableEvents = events;
        }

        /// <summary>
        /// 사용 가능한 이벤트 가져오기
        /// </summary>
        public EventSO[] GetAvailableEvents()
        {
            return availableEvents;
        }
    }
}