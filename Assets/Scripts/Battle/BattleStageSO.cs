using UnityEngine;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Battle
{
    /// <summary>
    /// 층 타입
    /// </summary>
    public enum StageType
    {
        Normal,     // 일반층
        Elite,      // 엘리트층 (5층 단위)
        Shop,       // 상점층 (3층 단위)
        Boss,       // 보스층 (10층 단위)
        Event       // 이벤트층 (랜덤)
    }

    /// <summary>
    /// 전투 등장 확률 정보
    /// </summary>
    [System.Serializable]
    public class BattleSpawnData
    {
        [Header("전투 정보")]
        public BattleSO battleData;         // 등장할 전투
        public float spawnWeight = 1.0f;    // 등장 가중치 (높을수록 자주 등장)

        [Header("등장 조건")]
        public int minFloor = 1;            // 최소 등장 층
        public int maxFloor = 999;          // 최대 등장 층
        public bool isUnique = false;       // 고유 전투 (한 번만 등장)
    }

    /// <summary>
    /// 보상 풀 정보
    /// </summary>
    [System.Serializable]
    public class RewardPoolData
    {
        [Header("카드 보상")]
        public CardSO[] availableCards;     // 등장 가능한 카드 목록
        public float cardSpawnWeight = 1.0f; // 카드 등장 가중치

        [Header("유물 보상")]
        public RelicSO[] availableRelics;   // 등장 가능한 유물 목록
        public float relicSpawnWeight = 1.0f; // 유물 등장 가중치
    }

    [CreateAssetMenu(fileName = "New Battle Stage", menuName = "Maglin/Battle/BattleStageSO")]
    public class BattleStageSO : ScriptableObject
    {
        [Header("층 기본 정보")]
        [SerializeField] private int floorNumber;
        [SerializeField] private StageType stageType;
        [SerializeField] private string stageDescription;

        [Header("전투 풀")]
        [SerializeField] private BattleSpawnData[] availableBattles;

        [Header("보상 풀")]
        [SerializeField] private RewardPoolData rewardPool;



        [Header("난이도 스케일링")]
        [SerializeField] private float difficultyMultiplier = 1.0f; // 난이도 배수

        // Properties
        public int FloorNumber => floorNumber;
        public StageType Type => stageType;
        public string Description => stageDescription;
        public BattleSpawnData[] AvailableBattles => availableBattles;
        public RewardPoolData RewardPool => rewardPool;

        public float DifficultyMultiplier => difficultyMultiplier;

        /// <summary>
        /// 가중치를 고려하여 랜덤 전투 선택
        /// 현재 층에서 등장 가능한 BattleSO만 필터링하여 선택
        /// </summary>
        public BattleSO GetRandomBattle(int currentFloor)
        {
            if (availableBattles == null || availableBattles.Length == 0)
            {
                Debug.LogWarning($"[BattleStageSO] {name}에 사용 가능한 전투가 없습니다.");
                return null;
            }

            // 현재 층에서 등장 가능한 전투만 필터링
            var validBattles = System.Array.FindAll(availableBattles,
                battle => battle != null &&
                         battle.battleData != null &&
                         currentFloor >= battle.minFloor &&
                         currentFloor <= battle.maxFloor);

            if (validBattles.Length == 0)
            {
                Debug.LogWarning($"[BattleStageSO] {name}에서 {currentFloor}층에 등장 가능한 전투가 없습니다.");
                return null;
            }

            Debug.Log($"[BattleStageSO] {name}에서 {currentFloor}층에 등장 가능한 전투 수: {validBattles.Length}");

            // 가중치 기반 랜덤 선택
            float totalWeight = 0f;
            foreach (var battle in validBattles)
            {
                totalWeight += battle.spawnWeight;
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning($"[BattleStageSO] {name}의 전투 가중치 합이 0 이하입니다. 첫 번째 전투를 선택합니다.");
                return validBattles[0].battleData;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var battle in validBattles)
            {
                currentWeight += battle.spawnWeight;
                if (randomValue <= currentWeight)
                {
                    Debug.Log($"[BattleStageSO] 선택된 전투: {battle.battleData.name} (가중치: {battle.spawnWeight})");
                    return battle.battleData;
                }
            }

            // 기본값으로 첫 번째 전투 반환 (이론적으로는 여기까지 오면 안됨)
            Debug.LogWarning($"[BattleStageSO] 가중치 계산 오류로 첫 번째 전투를 선택합니다: {validBattles[0].battleData.name}");
            return validBattles[0].battleData;
        }



        /// <summary>
        /// 이 BattleStage가 해당 층에서 등장 가능한지 확인
        /// BattleStageSO 내부의 BattleSO 중 하나라도 해당 층에서 등장 가능하면 true
        /// </summary>
        public bool CanAppearOnFloor(int floor)
        {
            if (availableBattles == null || availableBattles.Length == 0)
                return false;

            // 내부 BattleSO 중 하나라도 현재 층에서 등장 가능하면 true
            foreach (var battleSpawnData in availableBattles)
            {
                if (battleSpawnData != null &&
                    battleSpawnData.battleData != null &&
                    floor >= battleSpawnData.minFloor &&
                    floor <= battleSpawnData.maxFloor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}