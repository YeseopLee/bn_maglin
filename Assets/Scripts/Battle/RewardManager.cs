using UnityEngine;
using System.Collections.Generic;
using Maglin.Core;
using Maglin.Cards;
using Maglin.Relics;
using Maglin.Player;

namespace Maglin.Battle
{
    /// <summary>
    /// 층 타입별 보상 정의
    /// </summary>
    [System.Serializable]
    public class FloorTypeReward
    {
        [Header("층 타입")]
        public FloorType floorType;

        [Header("선택 가능한 보상 수")]
        public int maxSelectableRewards = 3;

        [Header("골드 설정")]
        public bool alwaysShowGold = true;
        public int baseGoldAmount = 20;
        public float goldMultiplier = 1.0f;
    }

    /// <summary>
    /// 전투 보상 시스템을 관리하는 매니저 클래스
    /// </summary>
    public class RewardManager : MonoBehaviour
    {
        [Header("층 타입별 보상 설정")]
        [SerializeField] private FloorTypeReward[] floorTypeRewards;

        [Header("보상 풀")]
        [SerializeField] private RewardSO cardRewardPool;      // 카드 보상 풀
        [SerializeField] private RewardSO relicRewardPool;     // 유물 보상 풀

        [Header("보상 설정")]
        [SerializeField] private RewardConfigSO rewardConfig;  // 보상 설정

        [Header("디버그 설정")]
        [SerializeField] private bool debugMode = false;

        // 싱글톤
        public static RewardManager Instance { get; private set; }

        // 이벤트
        public System.Action<BattleRewardData> OnBattleRewardsGenerated;  // 전투 보상 생성됨
        public System.Action<RewardItem> OnRewardSelected;                // 보상 선택됨 (카드 또는 유물)
        public System.Action<int> OnGoldReceived;                         // 골드 획득
        public System.Action OnRewardsCompleted;                          // 보상 선택 완료

        // 현재 보상 상태
        private BattleRewardData currentBattleRewards;
        private bool isShowingRewards = false;
        private bool hasSelectedCard = false;
        private bool hasSelectedRelic = false;

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
        /// 전투 완료 시 보상 생성 및 표시
        /// </summary>
        public void ShowBattleRewards(int currentFloor, FloorType floorType)
        {
            if (isShowingRewards)
            {
                Debug.LogWarning($"[RewardManager] 이미 보상을 표시 중입니다. 중복 호출 무시됨: {currentFloor}층 {floorType}");
                return;
            }

            if (debugMode)
                Debug.Log($"[RewardManager] {currentFloor}층 {floorType} 보상 생성 시작");

            // RewardConfig를 통해 보상 생성
            if (rewardConfig == null)
            {
                Debug.LogError("[RewardManager] RewardConfig가 설정되지 않았습니다!");
                return;
            }

            // 전투 보상 데이터 생성
            currentBattleRewards = rewardConfig.GenerateBattleRewards(currentFloor, floorType);

            if (currentBattleRewards == null)
            {
                Debug.LogWarning("[RewardManager] 생성된 보상이 없습니다.");
                CompleteRewards();
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[RewardManager] 보상 생성 완료:");
                Debug.Log($"  - 골드: {currentBattleRewards.goldAmount}");
                Debug.Log($"  - 카드 선택지: {currentBattleRewards.cardChoices?.Length ?? 0}개");
                Debug.Log($"  - 유물 선택지: {currentBattleRewards.relicChoices?.Length ?? 0}개");
            }

            // 골드는 즉시 지급
            if (currentBattleRewards.goldAmount > 0)
            {
                // PlayerBattleManager를 통해 골드 지급 (현재는 로그만 출력)
                OnGoldReceived?.Invoke(currentBattleRewards.goldAmount);

                if (debugMode)
                    Debug.Log($"[RewardManager] 골드 {currentBattleRewards.goldAmount} 자동 지급");
            }

            isShowingRewards = true;
            hasSelectedCard = false;
            hasSelectedRelic = false;

            OnBattleRewardsGenerated?.Invoke(currentBattleRewards);

            if (debugMode)
                Debug.Log("[RewardManager] OnBattleRewardsGenerated 이벤트 발생");
        }

        /// <summary>
        /// 보상 선택 처리 (카드 또는 유물)
        /// </summary>
        public void SelectReward(RewardItem selectedReward)
        {
            if (!isShowingRewards)
            {
                Debug.LogWarning("[RewardManager] 현재 보상 선택 중이 아닙니다.");
                return;
            }

            if (selectedReward == null)
            {
                Debug.LogWarning("[RewardManager] 선택된 보상이 null입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[RewardManager] 보상 선택됨: {selectedReward.rewardType} - {selectedReward.GetRewardName()}");

            // 보상 적용
            ApplyReward(selectedReward);

            // 이벤트 발생
            OnRewardSelected?.Invoke(selectedReward);

            // 선택 상태 업데이트
            if (selectedReward.rewardType == RewardType.Card)
            {
                hasSelectedCard = true;
            }
            else if (selectedReward.rewardType == RewardType.Relic)
            {
                hasSelectedRelic = true;
            }

            // 보상을 하나 선택하면 바로 완료 처리 (스킵과 동일한 동작)
            if (debugMode)
                Debug.Log("[RewardManager] 보상 선택 완료, 다음 층으로 진행");

            CompleteRewards();
        }

        /// <summary>
        /// 보상 적용 (카드 덱에 추가 또는 유물 적용)
        /// </summary>
        private void ApplyReward(RewardItem reward)
        {
            switch (reward.rewardType)
            {
                case RewardType.Card:
                    if (reward.cardReward != null)
                    {
                        // 카드를 덱에 추가
                        CardManager.Instance?.AddCardToDeck(reward.cardReward);
                        if (debugMode)
                            Debug.Log($"[RewardManager] 카드 '{reward.cardReward.CardName}' 덱에 추가");
                    }
                    break;

                case RewardType.Relic:
                    if (reward.relicReward != null)
                    {
                        // 유물 적용 (현재는 로그만 출력)
                        // TODO: PlayerBattleManager나 PlayerManager에 AddRelic 메서드 구현 후 연결
                        if (debugMode)
                            Debug.Log($"[RewardManager] 유물 '{reward.relicReward.RelicName}' 적용");
                    }
                    break;
            }
        }

        /// <summary>
        /// 보상 완료 처리
        /// </summary>
        private void CompleteRewards()
        {
            isShowingRewards = false;
            currentBattleRewards = null;
            hasSelectedCard = false;
            hasSelectedRelic = false;

            OnRewardsCompleted?.Invoke();

            if (debugMode)
                Debug.Log("[RewardManager] 보상 선택 완료");
        }

        /// <summary>
        /// RewardConfig 설정
        /// </summary>
        public void SetRewardConfig(RewardConfigSO config)
        {
            rewardConfig = config;

            if (debugMode)
                Debug.Log("[RewardManager] RewardConfig 설정 완료");
        }

        /// <summary>
        /// 보상 건너뛰기
        /// </summary>
        public void SkipRewards()
        {
            if (!isShowingRewards) return;

            if (debugMode)
                Debug.Log("[RewardManager] 보상 건너뛰기");

            CompleteRewards();
        }

        /// <summary>
        /// 현재 보상 데이터 반환
        /// </summary>
        public BattleRewardData GetCurrentBattleRewards()
        {
            return currentBattleRewards;
        }

        /// <summary>
        /// 보상 표시 중인지 확인
        /// </summary>
        public bool IsShowingRewards()
        {
            return isShowingRewards;
        }
    }
}