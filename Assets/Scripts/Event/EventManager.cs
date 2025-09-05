using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maglin.Core;
using Maglin.Cards;
using Maglin.Relics;
using Maglin.Player;
using Maglin.Battle;

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
        public bool HasPendingBattle => pendingBattleData != null;

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
        /// 이벤트 발생 체크 (FloorManager에서 호출)
        /// 실제 이벤트를 시작하지 않고 발생 여부만 확인
        /// </summary>
        public bool TryTriggerEvent(int currentFloor)
        {
            if (isEventActive)
            {
                Debug.LogWarning("[EventManager] Event is already active!");
                return false;
            }

            // 이벤트 발생 확률 체크
            if (Random.Range(0f, 1f) > eventChance)
            {
                Debug.Log($"[EventManager] 이벤트 발생 확률 체크 실패. 확률: {eventChance:P}");
                return false;
            }

            // FloorManager에서 이벤트 선택이 이미 처리되므로 단순히 확률만 체크
            Debug.Log($"[EventManager] {currentFloor}층에서 이벤트 발생 결정 (확률: {eventChance:P})");
            return true;
        }

        /// <summary>
        /// 선택된 이벤트 시작 (이벤트 씬에서 호출)
        /// </summary>
        public void StartSelectedEvent()
        {
            // FloorManager에서 선택된 이벤트 가져오기
            if (FloorManager.Instance != null && FloorManager.Instance.CurrentEvent != null)
            {
                currentEvent = FloorManager.Instance.CurrentEvent;
                StartEvent(currentEvent);
            }
            else
            {
                Debug.LogError("[EventManager] FloorManager에서 선택된 이벤트가 없습니다!");
            }
        }

        /// <summary>
        /// 강제로 이벤트 시작 (테스트용)
        /// </summary>
        public void ForceStartEvent(EventSO eventData)
        {
            if (eventData == null)
            {
                Debug.LogError("[EventManager] Event data is null!");
                return;
            }

            StartEvent(eventData);
        }

        /// <summary>
        /// 외부에서 호출 가능한 이벤트 선택 메서드 (FloorManager에서 사용)
        /// </summary>
        public EventSO SelectEventForFloor(int currentFloor)
        {
            return SelectRandomEvent(currentFloor);
        }

        /// <summary>
        /// 현재 층에서 등장 가능한 이벤트 중 랜덤 선택
        /// 인스펙터에 등록된 availableEvents에서만 선택
        /// </summary>
        private EventSO SelectRandomEvent(int currentFloor)
        {
            if (availableEvents == null || availableEvents.Length == 0)
            {
                Debug.LogWarning("[EventManager] 인스펙터에 등록된 이벤트가 없습니다!");
                return null;
            }

            // 현재 층에서 등장 가능하고 유효한 이벤트들 필터링
            var validEvents = availableEvents.Where(e =>
                e != null &&
                e.IsValid() &&
                e.CanAppearOnFloor(currentFloor) &&
                (!e.IsOneTimeOnly || !usedOneTimeEvents.Contains(e.EventName))
            ).ToList();

            if (validEvents.Count == 0)
            {
                Debug.LogWarning($"[EventManager] {currentFloor}층에서 등장 가능한 이벤트가 없습니다. " +
                    $"전체 이벤트 수: {availableEvents.Length}");
                return null;
            }

            Debug.Log($"[EventManager] {currentFloor}층에서 등장 가능한 이벤트 수: {validEvents.Count}");

            // 가중치 기반 선택
            float totalWeight = validEvents.Sum(e => e.SpawnWeight);
            if (totalWeight <= 0f)
            {
                Debug.LogWarning("[EventManager] 이벤트 가중치 합이 0 이하입니다. 첫 번째 이벤트를 선택합니다.");
                return validEvents[0];
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var eventData in validEvents)
            {
                currentWeight += eventData.SpawnWeight;
                if (randomValue <= currentWeight)
                {
                    Debug.Log($"[EventManager] 선택된 이벤트: {eventData.EventName} (가중치: {eventData.SpawnWeight})");
                    return eventData;
                }
            }

            // 안전장치 - 이론적으로는 여기까지 오면 안됨
            Debug.LogWarning("[EventManager] 가중치 계산 오류로 마지막 이벤트를 선택합니다.");
            return validEvents[validEvents.Count - 1];
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

        // 대기 중인 전투 데이터
        private BattleSO pendingBattleData = null;

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

                case EventRewardType.Battle:
                    if (reward.battleData != null && !isPenalty)
                    {
                        // 전투를 즉시 시작하지 않고 대기 상태로 설정
                        pendingBattleData = reward.battleData;
                        Debug.Log($"Event battle queued: {reward.battleData.BattleName}");
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

            // 대기 중인 전투가 있는지 확인
            if (pendingBattleData != null)
            {
                Debug.Log($"Starting pending battle: {pendingBattleData.BattleName}");
                var battleToStart = pendingBattleData;
                pendingBattleData = null; // 대기 상태 해제

                // 이벤트를 먼저 종료하고 전투 시작
                currentEvent = null;
                isEventActive = false;
                OnEventEnded?.Invoke();

                // 전투 시작
                StartEventBattle(battleToStart);
                return;
            }

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

        /// <summary>
        /// 이벤트에서 발생하는 전투 시작
        /// </summary>
        private void StartEventBattle(BattleSO battleData)
        {
            if (battleData == null)
            {
                Debug.LogError("[EventManager] BattleSO가 null입니다!");
                return;
            }

            Debug.Log($"[EventManager] 이벤트 전투 시작: {battleData.BattleName}");

            // FloorManager에 이벤트 전투 정보 설정
            if (FloorManager.Instance != null)
            {
                // 현재 이벤트를 일시적으로 저장
                var savedEvent = currentEvent;

                // 게임 상태를 전투로 변경 (중요!)
                FloorManager.Instance.ChangeGameState(GameState.Battle);

                // FloorManager에 이벤트 전투 설정
                FloorManager.Instance.SetCurrentBattle(battleData);
                FloorManager.Instance.SetEventBattleMode(true, savedEvent);

                // 전투 씬으로 전환
                _ = LoadEventBattleSceneAsync();
            }
            else
            {
                Debug.LogError("[EventManager] FloorManager가 없습니다!");
            }
        }

        /// <summary>
        /// 이벤트 전투 씬 로드 (비동기)
        /// </summary>
        private async Task LoadEventBattleSceneAsync()
        {
            Debug.Log("[EventManager] 이벤트 전투 씬 로딩 시작");

            try
            {
                // AdditiveSceneLoader를 사용한 전투 씬 전환
                if (AdditiveSceneLoader.Instance != null)
                {
                    await AdditiveSceneLoader.Instance.LoadSceneWithTransition("TestBattleScene");
                    Debug.Log("[EventManager] AdditiveSceneLoader를 통한 이벤트 전투 씬 로딩 완료");
                }
                else
                {
                    Debug.LogWarning("[EventManager] AdditiveSceneLoader가 없어서 기존 방식으로 로딩합니다.");
                    await LoadEventBattleSceneFallbackAsync();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EventManager] 이벤트 전투 씬 로딩 중 오류 발생: {ex.Message}");
                // 폴백으로 기존 방식 시도
                await LoadEventBattleSceneFallbackAsync();
            }
        }

        /// <summary>
        /// 폴백: 기존 방식으로 이벤트 전투 씬 로드 (비동기)
        /// </summary>
        private async Task LoadEventBattleSceneFallbackAsync()
        {
            Debug.Log("[EventManager] 기존 방식으로 이벤트 전투 씬 로딩");

            // 전투 씬으로 전환
            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("TestBattleScene");

            while (!asyncLoad.isDone)
            {
                await Task.Yield();
            }

            Debug.Log("[EventManager] 기존 방식 이벤트 전투 씬 로딩 완료");
        }
    }
}