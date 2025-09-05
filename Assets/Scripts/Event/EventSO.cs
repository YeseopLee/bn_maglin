using UnityEngine;
using Maglin.Cards;
using Maglin.Relics;
using Maglin.Battle;

namespace Maglin.Event
{
    /// <summary>
    /// 이벤트 보상/페널티 타입
    /// </summary>
    public enum EventRewardType
    {
        Gold,           // 골드 획득/손실
        Health,         // 체력 증감
        Card,           // 카드 획득
        CardRemoval,    // 카드 제거
        Relic,          // 유물 획득
        MaxHealth,      // 최대 체력 증감
        DeckUpgrade,    // 덱 강화
        Battle          // 전투 발생
    }

    /// <summary>
    /// 이벤트 선택지 정보
    /// </summary>
    [System.Serializable]
    public class EventChoice
    {
        [Header("선택지 기본 정보")]
        public string choiceText;           // 선택지 텍스트
        public string resultText;           // 결과 텍스트

        [Header("보상/페널티")]
        public EventReward[] rewards;       // 보상 배열
        public EventReward[] penalties;     // 페널티 배열

        [Header("확률")]
        [Range(0f, 1f)]
        public float successRate = 1f;      // 성공 확률 (1 = 100% 성공)
        public string failureText;          // 실패 시 텍스트
        public EventReward[] failurePenalties; // 실패 시 페널티
    }

    /// <summary>
    /// 이벤트 보상/페널티 데이터
    /// </summary>
    [System.Serializable]
    public class EventReward
    {
        public EventRewardType rewardType;
        public int amount;                  // 수량 (골드, 체력 등)
        public CardSO cardData;             // 카드 타입인 경우
        public RelicSO relicData;           // 유물 타입인 경우
        public BattleSO battleData;         // 전투 타입인 경우

        [Header("추가 설정")]
        public bool isRandom = false;       // 랜덤 요소가 있는지
        public int minAmount;               // 최소값 (랜덤인 경우)
        public int maxAmount;               // 최대값 (랜덤인 경우)
    }

    [CreateAssetMenu(fileName = "New Event", menuName = "Maglin/Event/EventSO")]
    public class EventSO : ScriptableObject
    {
        [Header("이벤트 기본 정보")]
        [SerializeField] private string eventName;
        [SerializeField][TextArea(3, 6)] private string eventDescription;
        [SerializeField] private Sprite eventImage;

        [Header("등장 조건")]
        [SerializeField] private int minFloor = 1;
        [SerializeField] private int maxFloor = 999;
        [SerializeField] private float spawnWeight = 1.0f;
        [SerializeField] private bool isOneTimeOnly = false;    // 한 번만 등장하는 이벤트인지

        [Header("선택지")]
        [SerializeField] private EventChoice[] choices;

        // Properties
        public string EventName => eventName;
        public string Description => eventDescription;
        public Sprite Image => eventImage;
        public int MinFloor => minFloor;
        public int MaxFloor => maxFloor;
        public float SpawnWeight => spawnWeight;
        public bool IsOneTimeOnly => isOneTimeOnly;
        public EventChoice[] Choices => choices;

        /// <summary>
        /// 현재 층에서 이 이벤트가 등장 가능한지 확인
        /// </summary>
        public bool CanAppearOnFloor(int currentFloor)
        {
            return currentFloor >= minFloor && currentFloor <= maxFloor;
        }

        /// <summary>
        /// 이벤트가 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(eventName) &&
                   !string.IsNullOrEmpty(eventDescription) &&
                   choices != null &&
                   choices.Length > 0;
        }

        /// <summary>
        /// 보상의 실제 수량 계산 (랜덤 요소 고려)
        /// </summary>
        public int GetActualAmount(EventReward reward)
        {
            if (reward.isRandom)
            {
                return Random.Range(reward.minAmount, reward.maxAmount + 1);
            }
            return reward.amount;
        }
    }
}