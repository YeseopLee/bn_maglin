using UnityEngine;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Battle
{
    /// <summary>
    /// 보상 타입 (골드는 RewardConfigSO에서 관리)
    /// </summary>
    public enum RewardType
    {
        Card,       // 카드
        Relic       // 유물
    }

    /// <summary>
    /// 보상 아이템 정보 (카드 또는 유물만)
    /// </summary>
    [System.Serializable]
    public class RewardItem
    {
        [Header("보상 기본 정보")]
        public RewardType rewardType;
        public float spawnWeight = 1.0f; // 등장 가중치

        [Header("카드/유물 데이터")]
        public CardSO cardReward;       // 카드 보상인 경우
        public RelicSO relicReward;     // 유물 보상인 경우

        [Header("등장 조건")]
        public int minFloor = 1;        // 최소 등장 층
        public int maxFloor = 999;      // 최대 등장 층

        /// <summary>
        /// 보상이 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            switch (rewardType)
            {
                case RewardType.Card:
                    return cardReward != null;
                case RewardType.Relic:
                    return relicReward != null;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 보상 이름 반환
        /// </summary>
        public string GetRewardName()
        {
            switch (rewardType)
            {
                case RewardType.Card:
                    return cardReward?.CardName ?? "Unknown Card";
                case RewardType.Relic:
                    return relicReward?.RelicName ?? "Unknown Relic";
                default:
                    return "Unknown Reward";
            }
        }

        /// <summary>
        /// 보상 설명 반환
        /// </summary>
        public string GetRewardDescription()
        {
            switch (rewardType)
            {
                case RewardType.Card:
                    return cardReward?.Description ?? "";
                case RewardType.Relic:
                    return relicReward?.Description ?? "";
                default:
                    return "";
            }
        }
    }

    [CreateAssetMenu(fileName = "New Reward", menuName = "Maglin/Battle/RewardSO")]
    public class RewardSO : ScriptableObject
    {
        [Header("보상 기본 정보")]
        [SerializeField] private string rewardName;
        [SerializeField] private string rewardDescription;

        [Header("보상 아이템 (카드 또는 유물)")]
        [SerializeField] private RewardItem[] rewardItems;

        // Properties
        public string RewardName => rewardName;
        public string Description => rewardDescription;
        public RewardItem[] RewardItems => rewardItems;

        /// <summary>
        /// 현재 층에 맞는 보상 아이템 반환 (카드 또는 유물만)
        /// </summary>
        public RewardItem[] GetValidRewardsForFloor(int currentFloor)
        {
            if (rewardItems == null) return new RewardItem[0];

            return System.Array.FindAll(rewardItems,
                reward => reward.IsValid() &&
                         currentFloor >= reward.minFloor &&
                         currentFloor <= reward.maxFloor);
        }

        /// <summary>
        /// 가중치 기반 보상 선택
        /// </summary>
        public RewardItem SelectWeightedReward(System.Collections.Generic.List<RewardItem> rewards)
        {
            if (rewards.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var reward in rewards)
            {
                totalWeight += reward.spawnWeight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var reward in rewards)
            {
                currentWeight += reward.spawnWeight;
                if (randomValue <= currentWeight)
                {
                    return reward;
                }
            }

            return rewards[0]; // 기본값
        }
    }
}