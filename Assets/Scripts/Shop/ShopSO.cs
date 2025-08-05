using UnityEngine;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Shop
{
    /// <summary>
    /// 상점 아이템 타입
    /// </summary>
    public enum ShopItemType
    {
        Card,           // 카드
        Relic,          // 유물
        CardRemoval,    // 카드 제거
        HealthRestore   // 체력 회복
    }

    /// <summary>
    /// 카드 타입별 가중치 설정
    /// </summary>
    [System.Serializable]
    public class CardTypeWeight
    {
        public CardType cardType;
        public float weight = 1.0f;
    }

    /// <summary>
    /// 상점 아이템 정보
    /// </summary>
    [System.Serializable]
    public class ShopItem
    {
        [Header("아이템 기본 정보")]
        public ShopItemType itemType;
        public string itemName;
        public string itemDescription;
        public Sprite itemIcon;

        [Header("아이템 데이터")]
        public CardSO cardData;         // 카드인 경우
        public RelicSO relicData;       // 유물인 경우
        public int healthRestoreAmount; // 체력 회복인 경우

        [Header("가격 정보")]
        public int basePrice;
        public bool isPriceFixed = false;   // 고정 가격인지 (유물 효과 무시)

        [Header("등장 조건")]
        public float spawnWeight = 1.0f;    // 등장 가중치
        public int minFloor = 1;            // 최소 등장 층
        public int maxFloor = 999;          // 최대 등장 층

        /// <summary>
        /// 아이템이 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            switch (itemType)
            {
                case ShopItemType.Card:
                    return cardData != null;
                case ShopItemType.Relic:
                    return relicData != null;
                case ShopItemType.CardRemoval:
                case ShopItemType.HealthRestore:
                    return true;
                default:
                    return false;
            }
        }
    }

    [CreateAssetMenu(fileName = "New Shop", menuName = "Maglin/Shop/ShopSO")]
    public class ShopSO : ScriptableObject
    {
        [Header("상점 기본 정보")]
        [SerializeField] private string shopName;
        [SerializeField] private string shopDescription;
        [SerializeField] private Sprite shopIcon;

        [Header("상점 설정")]
        [SerializeField] private int cardSlotsCount = 5;        // 카드 판매 슬롯 수
        [SerializeField] private int relicSlotsCount = 3;       // 유물 판매 슬롯 수
        [SerializeField] private float priceMultiplier = 1.0f;  // 가격 배수
        [SerializeField] private int cardRemovalPrice = 50;     // 카드 제거 가격
        [SerializeField] private int healthRestorePrice = 30;   // 체력 회복 가격 (체력당)

        [Header("카드 타입별 가중치")]
        [SerializeField]
        private CardTypeWeight[] cardTypeWeights = new CardTypeWeight[]
        {
            new CardTypeWeight { cardType = CardType.Element, weight = 0.8f },
            new CardTypeWeight { cardType = CardType.Active1, weight = 1.0f },
            new CardTypeWeight { cardType = CardType.Active2, weight = 1.0f }
        };

        [Header("카드 가격 설정")]
        [SerializeField] private int elementCardPrice = 40;
        [SerializeField] private int active1CardPrice = 50;
        [SerializeField] private int active2CardPrice = 50;

        [Header("유물 가격 설정")]
        [SerializeField] private int commonRelicPrice = 100;

        // Properties
        public string ShopName => shopName;
        public string Description => shopDescription;
        public Sprite Icon => shopIcon;
        public int CardSlotsCount => cardSlotsCount;
        public int RelicSlotsCount => relicSlotsCount;
        public float PriceMultiplier => priceMultiplier;
        public int CardRemovalPrice => cardRemovalPrice;
        public int HealthRestorePrice => healthRestorePrice;

        // 캐시된 카드 및 유물 데이터
        private CardSO[] allCards;
        private RelicSO[] allRelics;
        private bool isDataLoaded = false;

        /// <summary>
        /// 상점 아이템 생성 (카드 + 유물 + 서비스)
        /// </summary>
        public ShopItem[] GenerateShopItems(int currentFloor)
        {
            LoadGameData();

            var shopItems = new System.Collections.Generic.List<ShopItem>();

            // 카드 슬롯 생성
            var cardItems = GenerateCardItems(currentFloor);
            shopItems.AddRange(cardItems);

            // 유물 슬롯 생성
            var relicItems = GenerateRelicItems(currentFloor);
            shopItems.AddRange(relicItems);

            // 카드 제거 서비스 추가
            shopItems.Add(CreateCardRemovalService());

            // 체력 회복 서비스 추가
            var healthService = CreateHealthRestoreService();
            if (healthService != null)
            {
                shopItems.Add(healthService);
            }

            return shopItems.ToArray();
        }

        /// <summary>
        /// 게임 데이터 로드 (카드, 유물)
        /// </summary>
        private void LoadGameData()
        {
            if (isDataLoaded) return;

            // Resources 폴더에서 모든 카드 로드
            allCards = Resources.LoadAll<CardSO>("Cards");

            // Resources 폴더에서 모든 유물 로드
            allRelics = Resources.LoadAll<RelicSO>("Relics");

            isDataLoaded = true;

            Debug.Log($"[ShopSO] 카드 {allCards.Length}개, 유물 {allRelics.Length}개 로드됨");
        }

        /// <summary>
        /// 카드 아이템 생성
        /// </summary>
        private ShopItem[] GenerateCardItems(int currentFloor)
        {
            var cardItems = new System.Collections.Generic.List<ShopItem>();

            // 판매 가능한 카드 타입
            var sellableCardTypes = new CardType[] { CardType.Element, CardType.Active1, CardType.Active2 };

            for (int i = 0; i < cardSlotsCount; i++)
            {
                // 가중치 기반으로 카드 타입 선택
                CardType selectedType = SelectCardTypeByWeight();

                // 해당 타입의 카드 중 랜덤 선택
                var cardsOfType = System.Array.FindAll(allCards,
                    card => card != null && card.Type == selectedType);

                if (cardsOfType.Length > 0)
                {
                    CardSO selectedCard = cardsOfType[Random.Range(0, cardsOfType.Length)];
                    ShopItem cardItem = CreateCardItem(selectedCard);
                    cardItems.Add(cardItem);
                }
            }

            return cardItems.ToArray();
        }

        /// <summary>
        /// 유물 아이템 생성
        /// </summary>
        private ShopItem[] GenerateRelicItems(int currentFloor)
        {
            var relicItems = new System.Collections.Generic.List<ShopItem>();

            // Common 타입 유물만 판매
            var commonRelics = System.Array.FindAll(allRelics,
                relic => relic != null && relic.Type == RelicType.Common);

            for (int i = 0; i < relicSlotsCount; i++)
            {
                if (commonRelics.Length > 0)
                {
                    RelicSO selectedRelic = commonRelics[Random.Range(0, commonRelics.Length)];
                    ShopItem relicItem = CreateRelicItem(selectedRelic);
                    relicItems.Add(relicItem);

                    // 중복 방지를 위해 선택된 유물 제거
                    var tempList = new System.Collections.Generic.List<RelicSO>(commonRelics);
                    tempList.Remove(selectedRelic);
                    commonRelics = tempList.ToArray();
                }
            }

            return relicItems.ToArray();
        }

        /// <summary>
        /// 가중치 기반 카드 타입 선택
        /// </summary>
        private CardType SelectCardTypeByWeight()
        {
            float totalWeight = 0f;
            foreach (var weightData in cardTypeWeights)
            {
                totalWeight += weightData.weight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var weightData in cardTypeWeights)
            {
                currentWeight += weightData.weight;
                if (randomValue <= currentWeight)
                {
                    return weightData.cardType;
                }
            }

            // 기본값
            return CardType.Active1;
        }

        /// <summary>
        /// 카드 아이템 생성
        /// </summary>
        private ShopItem CreateCardItem(CardSO cardData)
        {
            int price = GetCardPrice(cardData.Type);

            return new ShopItem
            {
                itemType = ShopItemType.Card,
                itemName = cardData.CardName,
                itemDescription = cardData.Description,
                itemIcon = cardData.Image,
                cardData = cardData,
                basePrice = price,
                isPriceFixed = false
            };
        }

        /// <summary>
        /// 유물 아이템 생성
        /// </summary>
        private ShopItem CreateRelicItem(RelicSO relicData)
        {
            return new ShopItem
            {
                itemType = ShopItemType.Relic,
                itemName = relicData.RelicName,
                itemDescription = relicData.Description,
                itemIcon = relicData.Image,
                relicData = relicData,
                basePrice = commonRelicPrice,
                isPriceFixed = false
            };
        }

        /// <summary>
        /// 카드 제거 서비스 생성
        /// </summary>
        private ShopItem CreateCardRemovalService()
        {
            return new ShopItem
            {
                itemType = ShopItemType.CardRemoval,
                itemName = "카드 제거",
                itemDescription = "원하지 않는 카드를 덱에서 제거합니다.",
                basePrice = cardRemovalPrice,
                isPriceFixed = true
            };
        }

        /// <summary>
        /// 체력 회복 서비스 생성
        /// </summary>
        private ShopItem CreateHealthRestoreService()
        {
            // PlayerManager를 통해 현재 체력 확인
            var playerManager = FindObjectOfType<Maglin.Player.PlayerManager>();
            if (playerManager == null) return null;

            int missingHP = playerManager.MaxHealth - playerManager.CurrentHealth;
            if (missingHP <= 0) return null; // 체력이 가득참

            // 고정 가격 사용 (잃은 체력과 상관없이)
            int totalPrice = healthRestorePrice;

            return new ShopItem
            {
                itemType = ShopItemType.HealthRestore,
                itemName = "체력 회복",
                itemDescription = $"체력을 {missingHP}만큼 회복합니다",
                healthRestoreAmount = missingHP,
                basePrice = totalPrice,
                isPriceFixed = true
            };
        }

        /// <summary>
        /// 카드 타입별 가격 반환
        /// </summary>
        private int GetCardPrice(CardType cardType)
        {
            return cardType switch
            {
                CardType.Element => elementCardPrice,
                CardType.Active1 => active1CardPrice,
                CardType.Active2 => active2CardPrice,
                _ => active1CardPrice
            };
        }

        /// <summary>
        /// 아이템의 최종 가격 계산
        /// </summary>
        public int GetFinalPrice(ShopItem item)
        {
            if (item.isPriceFixed)
                return item.basePrice;

            return Mathf.RoundToInt(item.basePrice * priceMultiplier);
        }

        /// <summary>
        /// 데이터 다시 로드 (에디터에서 테스트용)
        /// </summary>
        public void ReloadData()
        {
            isDataLoaded = false;
            LoadGameData();
        }
    }
}