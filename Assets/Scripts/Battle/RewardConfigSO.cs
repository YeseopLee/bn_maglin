using UnityEngine;
using Maglin.Core;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Battle
{
    /// <summary>
    /// 보상 시스템의 기본 설정을 관리하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "New Reward Config", menuName = "Maglin/Battle/RewardConfigSO")]
    public class RewardConfigSO : ScriptableObject
    {
        [Header("일반층 보상 설정")]
        [SerializeField]
        private FloorTypeReward normalFloorReward = new FloorTypeReward
        {
            floorType = FloorType.Normal,
            maxSelectableRewards = 3,
            alwaysShowGold = true,
            baseGoldAmount = 15,
            goldMultiplier = 1.0f
        };
        [SerializeField] private RewardSO normalCardPool;     // 일반층 카드 풀
        [SerializeField] private RewardSO normalRelicPool;    // 일반층 유물 풀

        [Header("엘리트층 보상 설정")]
        [SerializeField]
        private FloorTypeReward eliteFloorReward = new FloorTypeReward
        {
            floorType = FloorType.Elite,
            maxSelectableRewards = 3,
            alwaysShowGold = true,
            baseGoldAmount = 25,
            goldMultiplier = 1.5f
        };
        [SerializeField] private RewardSO eliteCardPool;      // 엘리트층 카드 풀
        [SerializeField] private RewardSO eliteRelicPool;     // 엘리트층 유물 풀

        [Header("보스층 보상 설정")]
        [SerializeField]
        private FloorTypeReward bossFloorReward = new FloorTypeReward
        {
            floorType = FloorType.Boss,
            maxSelectableRewards = 3,
            alwaysShowGold = true,
            baseGoldAmount = 50,
            goldMultiplier = 2.0f
        };
        [SerializeField] private RewardSO bossCardPool;       // 보스층 카드 풀
        [SerializeField] private RewardSO bossRelicPool;      // 보스층 유물 풀

        [Header("시작층 보상 설정")]
        [SerializeField]
        private FloorTypeReward startFloorReward = new FloorTypeReward
        {
            floorType = FloorType.Start,
            maxSelectableRewards = 3,
            alwaysShowGold = true,
            baseGoldAmount = 10,
            goldMultiplier = 0.5f
        };
        [SerializeField] private RewardSO startCardPool;      // 시작층 카드 풀
        [SerializeField] private RewardSO startRelicPool;     // 시작층 유물 풀

        // Properties
        public FloorTypeReward NormalFloorReward => normalFloorReward;
        public FloorTypeReward EliteFloorReward => eliteFloorReward;
        public FloorTypeReward BossFloorReward => bossFloorReward;
        public FloorTypeReward StartFloorReward => startFloorReward;

        // 층별 카드 풀
        public RewardSO NormalCardPool => normalCardPool;
        public RewardSO EliteCardPool => eliteCardPool;
        public RewardSO BossCardPool => bossCardPool;
        public RewardSO StartCardPool => startCardPool;

        // 층별 유물 풀
        public RewardSO NormalRelicPool => normalRelicPool;
        public RewardSO EliteRelicPool => eliteRelicPool;
        public RewardSO BossRelicPool => bossRelicPool;
        public RewardSO StartRelicPool => startRelicPool;

        /// <summary>
        /// 층 타입에 따른 보상 설정 반환
        /// </summary>
        public FloorTypeReward GetFloorRewardConfig(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Normal => normalFloorReward,
                FloorType.Elite => eliteFloorReward,
                FloorType.Boss => bossFloorReward,
                FloorType.Start => startFloorReward,
                _ => normalFloorReward
            };
        }

        /// <summary>
        /// 층 타입에 따른 카드 풀 반환
        /// </summary>
        public RewardSO GetCardPoolForFloor(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Normal => normalCardPool,
                FloorType.Elite => eliteCardPool,
                FloorType.Boss => bossCardPool,
                FloorType.Start => startCardPool,
                _ => normalCardPool
            };
        }

        /// <summary>
        /// 층 타입에 따른 유물 풀 반환
        /// </summary>
        public RewardSO GetRelicPoolForFloor(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Normal => normalRelicPool,
                FloorType.Elite => eliteRelicPool,
                FloorType.Boss => bossRelicPool,
                FloorType.Start => startRelicPool,
                _ => normalRelicPool
            };
        }

        /// <summary>
        /// 모든 층 타입 설정 배열 반환
        /// </summary>
        public FloorTypeReward[] GetAllFloorRewards()
        {
            return new FloorTypeReward[]
            {
                normalFloorReward,
                eliteFloorReward,
                bossFloorReward,
                startFloorReward
            };
        }

        /// <summary>
        /// RewardManager에 설정 적용 (더 이상 사용하지 않음 - RewardManager.SetRewardConfig 사용)
        /// </summary>
        [System.Obsolete("Use RewardManager.SetRewardConfig() instead")]
        public void ApplyToRewardManager(RewardManager rewardManager)
        {
            Debug.LogWarning("[RewardConfigSO] ApplyToRewardManager는 더 이상 사용되지 않습니다. RewardManager.SetRewardConfig()를 사용하세요.");
        }

        /// <summary>
        /// 설정 유효성 검증
        /// </summary>
        [ContextMenu("Validate Configuration")]
        public void ValidateConfiguration()
        {
            bool isValid = true;

            // 각 층별 카드 풀 검증
            var cardPools = new[] { normalCardPool, eliteCardPool, bossCardPool, startCardPool };
            var cardPoolNames = new[] { "일반층", "엘리트층", "보스층", "시작층" };

            for (int i = 0; i < cardPools.Length; i++)
            {
                if (cardPools[i] == null)
                {
                    Debug.LogWarning($"[RewardConfigSO] {cardPoolNames[i]} 카드 풀이 설정되지 않았습니다.");
                    isValid = false;
                }
                else if (cardPools[i].RewardItems == null || cardPools[i].RewardItems.Length == 0)
                {
                    Debug.LogWarning($"[RewardConfigSO] {cardPoolNames[i]} 카드 풀이 비어있습니다.");
                    isValid = false;
                }
            }

            // 각 층별 유물 풀 검증
            var relicPools = new[] { normalRelicPool, eliteRelicPool, bossRelicPool, startRelicPool };
            var relicPoolNames = new[] { "일반층", "엘리트층", "보스층", "시작층" };

            for (int i = 0; i < relicPools.Length; i++)
            {
                if (relicPools[i] == null)
                {
                    Debug.LogWarning($"[RewardConfigSO] {relicPoolNames[i]} 유물 풀이 설정되지 않았습니다.");
                    isValid = false;
                }
                else if (relicPools[i].RewardItems == null || relicPools[i].RewardItems.Length == 0)
                {
                    Debug.LogWarning($"[RewardConfigSO] {relicPoolNames[i]} 유물 풀이 비어있습니다.");
                    isValid = false;
                }
            }

            // 골드 설정 검증
            if (normalFloorReward.baseGoldAmount <= 0 ||
                eliteFloorReward.baseGoldAmount <= 0 ||
                bossFloorReward.baseGoldAmount <= 0 ||
                startFloorReward.baseGoldAmount <= 0)
            {
                Debug.LogWarning("[RewardConfigSO] 일부 층의 기본 골드 설정이 잘못되었습니다.");
                isValid = false;
            }

            if (isValid)
            {
                Debug.Log("[RewardConfigSO] 설정이 유효합니다.");
            }
            else
            {
                Debug.LogError("[RewardConfigSO] 설정에 문제가 있습니다. 수정이 필요합니다.");
            }
        }

        /// <summary>
        /// 기본 설정으로 리셋
        /// </summary>
        [ContextMenu("Reset to Default")]
        public void ResetToDefault()
        {
            normalFloorReward = new FloorTypeReward
            {
                floorType = FloorType.Normal,
                maxSelectableRewards = 3,
                alwaysShowGold = true,
                baseGoldAmount = 15,
                goldMultiplier = 1.0f
            };

            eliteFloorReward = new FloorTypeReward
            {
                floorType = FloorType.Elite,
                maxSelectableRewards = 3,
                alwaysShowGold = true,
                baseGoldAmount = 25,
                goldMultiplier = 1.5f
            };

            bossFloorReward = new FloorTypeReward
            {
                floorType = FloorType.Boss,
                maxSelectableRewards = 3,
                alwaysShowGold = true,
                baseGoldAmount = 50,
                goldMultiplier = 2.0f
            };

            startFloorReward = new FloorTypeReward
            {
                floorType = FloorType.Start,
                maxSelectableRewards = 3,
                alwaysShowGold = true,
                baseGoldAmount = 10,
                goldMultiplier = 0.5f
            };

            Debug.Log("[RewardConfigSO] 기본 설정으로 리셋되었습니다.");
        }

        /// <summary>
        /// 전투 승리 보상 생성 (골드 + 카드/유물 선택)
        /// </summary>
        public BattleRewardData GenerateBattleRewards(int currentFloor, FloorType floorType)
        {
            var floorConfig = GetFloorRewardConfig(floorType);

            // 골드 보상 계산
            int goldAmount = CalculateGoldReward(currentFloor, floorConfig);

            // 해당 층에 맞는 카드 보상 3개 생성
            var cardRewards = GenerateCardRewards(currentFloor, floorType, 3);

            // 해당 층에 맞는 유물 보상 3개 생성
            var relicRewards = GenerateRelicRewards(currentFloor, floorType, 3);

            return new BattleRewardData
            {
                goldAmount = goldAmount,
                cardChoices = cardRewards,
                relicChoices = relicRewards
            };
        }

        /// <summary>
        /// 골드 보상 계산
        /// </summary>
        private int CalculateGoldReward(int currentFloor, FloorTypeReward floorConfig)
        {
            // 기본 골드에 층별 스케일링과 층 타입 배수 적용
            float goldAmount = floorConfig.baseGoldAmount * floorConfig.goldMultiplier;

            // 층별 스케일링 (매 층마다 약간씩 증가)
            goldAmount *= Mathf.Pow(1.05f, currentFloor - 1);

            return Mathf.RoundToInt(goldAmount);
        }

        /// <summary>
        /// 층별 카드 보상 생성
        /// </summary>
        private RewardItem[] GenerateCardRewards(int currentFloor, FloorType floorType, int count)
        {
            var cardPool = GetCardPoolForFloor(floorType);
            if (cardPool == null)
            {
                Debug.LogWarning($"[RewardConfigSO] {floorType}층의 카드 풀이 설정되지 않았습니다.");
                return new RewardItem[0];
            }

            Debug.Log($"[RewardConfigSO] {floorType}층 카드 풀 '{cardPool.name}' 확인됨");

            var availableCards = cardPool.GetValidRewardsForFloor(currentFloor);
            if (availableCards.Length == 0)
            {
                Debug.LogWarning($"[RewardConfigSO] {floorType}층({currentFloor}층)에 사용 가능한 카드가 없습니다. " +
                               $"카드 풀 '{cardPool.name}'의 아이템 수: {cardPool.RewardItems?.Length ?? 0}");

                // 카드 풀의 각 아이템 상태 확인
                if (cardPool.RewardItems != null)
                {
                    for (int i = 0; i < cardPool.RewardItems.Length; i++)
                    {
                        var item = cardPool.RewardItems[i];
                        Debug.Log($"  카드 {i}: {item?.GetRewardName() ?? "null"}, " +
                                $"Valid: {item?.IsValid() ?? false}, " +
                                $"Floor: {item?.minFloor ?? 0}-{item?.maxFloor ?? 0}");
                    }
                }

                return new RewardItem[0];
            }

            Debug.Log($"[RewardConfigSO] {currentFloor}층에 사용 가능한 카드 {availableCards.Length}개 발견");

            var selectedCards = new System.Collections.Generic.List<RewardItem>();
            var remainingCards = new System.Collections.Generic.List<RewardItem>(availableCards);

            for (int i = 0; i < count && remainingCards.Count > 0; i++)
            {
                var selected = cardPool.SelectWeightedReward(remainingCards);
                if (selected != null)
                {
                    selectedCards.Add(selected);
                    remainingCards.Remove(selected);
                    Debug.Log($"[RewardConfigSO] 카드 선택됨: {selected.GetRewardName()}");
                }
            }

            Debug.Log($"[RewardConfigSO] 최종 선택된 카드 수: {selectedCards.Count}");
            return selectedCards.ToArray();
        }

        /// <summary>
        /// 층별 유물 보상 생성
        /// </summary>
        private RewardItem[] GenerateRelicRewards(int currentFloor, FloorType floorType, int count)
        {
            var relicPool = GetRelicPoolForFloor(floorType);
            if (relicPool == null)
            {
                Debug.LogWarning($"[RewardConfigSO] {floorType}층의 유물 풀이 설정되지 않았습니다.");
                return new RewardItem[0];
            }

            Debug.Log($"[RewardConfigSO] {floorType}층 유물 풀 '{relicPool.name}' 확인됨");

            var availableRelics = relicPool.GetValidRewardsForFloor(currentFloor);
            if (availableRelics.Length == 0)
            {
                Debug.LogWarning($"[RewardConfigSO] {floorType}층({currentFloor}층)에 사용 가능한 유물이 없습니다. " +
                               $"유물 풀 '{relicPool.name}'의 아이템 수: {relicPool.RewardItems?.Length ?? 0}");

                // 유물 풀의 각 아이템 상태 확인
                if (relicPool.RewardItems != null)
                {
                    for (int i = 0; i < relicPool.RewardItems.Length; i++)
                    {
                        var item = relicPool.RewardItems[i];
                        Debug.Log($"  유물 {i}: {item?.GetRewardName() ?? "null"}, " +
                                $"Valid: {item?.IsValid() ?? false}, " +
                                $"Floor: {item?.minFloor ?? 0}-{item?.maxFloor ?? 0}");
                    }
                }

                return new RewardItem[0];
            }

            Debug.Log($"[RewardConfigSO] {currentFloor}층에 사용 가능한 유물 {availableRelics.Length}개 발견");

            var selectedRelics = new System.Collections.Generic.List<RewardItem>();
            var remainingRelics = new System.Collections.Generic.List<RewardItem>(availableRelics);

            for (int i = 0; i < count && remainingRelics.Count > 0; i++)
            {
                var selected = relicPool.SelectWeightedReward(remainingRelics);
                if (selected != null)
                {
                    selectedRelics.Add(selected);
                    remainingRelics.Remove(selected);
                    Debug.Log($"[RewardConfigSO] 유물 선택됨: {selected.GetRewardName()}");
                }
            }

            Debug.Log($"[RewardConfigSO] 최종 선택된 유물 수: {selectedRelics.Count}");
            return selectedRelics.ToArray();
        }
    }

    /// <summary>
    /// 전투 보상 데이터
    /// </summary>
    [System.Serializable]
    public class BattleRewardData
    {
        public int goldAmount;                  // 골드 보상 (자동 획득)
        public RewardItem[] cardChoices;        // 카드 선택지 (3개 중 1개 선택)
        public RewardItem[] relicChoices;       // 유물 선택지 (3개 중 1개 선택)
    }
}