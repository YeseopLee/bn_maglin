using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Maglin.Core;
using Maglin.Cards;
using Maglin.Relics;
using Maglin.Player;

namespace Maglin.Shop
{
    /// <summary>
    /// 상점 시스템을 관리하는 매니저 클래스
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        [Header("상점 설정")]
        [SerializeField] private ShopSO defaultShop;
        [SerializeField] private ShopSO[] availableShops;

        [Header("현재 상점 상태")]
        [SerializeField] private ShopItem[] currentShopItems;
        [SerializeField] private bool[] itemsPurchased;
        [SerializeField] private bool cardRemovalUsed = false; // 카드 제거 서비스 사용 여부
        [SerializeField] private int cardRemovalPrice = 0; // 카드 제거 가격 (결제 대기 중)

        // 싱글톤
        public static ShopManager Instance { get; private set; }

        // 이벤트
        public System.Action<ShopItem, int> OnItemPurchased;
        public System.Action OnShopRefreshed;
        public System.Action OnShopEntered;
        public System.Action OnShopExited;
        public System.Action OnCardRemovalRequested; // 카드 제거 UI 요청 이벤트

        // 현재 상점 정보
        public ShopSO CurrentShop { get; private set; }
        public ShopItem[] CurrentItems => currentShopItems;
        public bool[] ItemsPurchased => itemsPurchased;
        public bool CardRemovalUsed => cardRemovalUsed;

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

        private void Start()
        {
            // 기본 상점 설정
            if (defaultShop != null)
            {
                CurrentShop = defaultShop;
            }
        }

        /// <summary>
        /// 상점 진입 시 호출
        /// </summary>
        public void EnterShop(int currentFloor = -1)
        {
            if (currentFloor == -1)
            {
                var floorManager = FloorManager.Instance;
                currentFloor = floorManager != null ? floorManager.CurrentFloor : 1;
            }

            // 현재 층에 맞는 상점 선택
            SelectShopForFloor(currentFloor);

            // 상점 아이템 생성
            GenerateShopItems(currentFloor);

            // 카드 제거 서비스 상태 초기화
            cardRemovalUsed = false;

            OnShopEntered?.Invoke();
        }

        /// <summary>
        /// 상점 퇴장 시 호출
        /// </summary>
        public void ExitShop()
        {
            OnShopExited?.Invoke();

            // FloorManager에게 상점 완료 알림
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnContentCompleted();
            }
        }

        /// <summary>
        /// 현재 층에 맞는 상점 선택
        /// </summary>
        private void SelectShopForFloor(int floor)
        {
            if (availableShops != null && availableShops.Length > 0)
            {
                // 추후 층별 다른 상점을 구현할 경우 여기서 선택 로직 추가
                CurrentShop = availableShops[0];
            }
            else if (defaultShop != null)
            {
                CurrentShop = defaultShop;
            }
        }

        /// <summary>
        /// 상점 아이템 생성
        /// </summary>
        private void GenerateShopItems(int currentFloor)
        {
            if (CurrentShop == null)
            {
                Debug.LogError("[ShopManager] CurrentShop이 null입니다!");
                return;
            }

            // ShopSO에서 동적으로 아이템 생성
            currentShopItems = CurrentShop.GenerateShopItems(currentFloor);
            itemsPurchased = new bool[currentShopItems.Length];

            OnShopRefreshed?.Invoke();

            Debug.Log($"[ShopManager] {currentShopItems.Length}개의 상점 아이템 생성됨");
        }

        /// <summary>
        /// 아이템 구매 시도
        /// </summary>
        public bool TryPurchaseItem(int itemIndex)
        {
            // 유효성 검사
            if (itemIndex < 0 || itemIndex >= currentShopItems.Length)
            {
                Debug.LogError($"Invalid item index: {itemIndex}");
                return false;
            }

            if (itemsPurchased[itemIndex])
            {
                Debug.LogWarning("Item already purchased!");
                return false;
            }

            ShopItem item = currentShopItems[itemIndex];

            // 카드 제거 서비스 중복 확인
            if (item.itemType == ShopItemType.CardRemoval && cardRemovalUsed)
            {
                Debug.LogWarning("Card removal service already used!");
                return false;
            }

            int finalPrice = GetItemPrice(item);

            // 골드 확인
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentGold < finalPrice)
            {
                Debug.LogWarning("Not enough gold!");
                return false;
            }

            // 구매 처리
            if (ProcessPurchase(item, finalPrice))
            {
                // 카드 제거 서비스는 실제 카드 제거 시에만 구매 완료 처리
                if (item.itemType != ShopItemType.CardRemoval)
                {
                    itemsPurchased[itemIndex] = true;
                }

                Debug.Log($"[ShopManager] 구매 완료 이벤트 발생: {item.itemName}, 가격: {finalPrice}");
                OnItemPurchased?.Invoke(item, finalPrice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 실제 구매 처리
        /// </summary>
        private bool ProcessPurchase(ShopItem item, int price)
        {
            if (PlayerManager.Instance == null) return false;

            switch (item.itemType)
            {
                case ShopItemType.Card:
                    return PurchaseCard(item.cardData, price);

                case ShopItemType.Relic:
                    return PurchaseRelic(item.relicData, price);

                case ShopItemType.CardRemoval:
                    return PurchaseCardRemoval(price);

                case ShopItemType.HealthRestore:
                    return PurchaseHealthRestore(item.healthRestoreAmount, price);

                default:
                    Debug.LogError($"Unknown shop item type: {item.itemType}");
                    return false;
            }
        }

        /// <summary>
        /// 카드 구매 처리
        /// 스타터 덱에 카드 추가
        /// </summary>
        private bool PurchaseCard(CardSO cardData, int price)
        {
            if (cardData == null) return false;

            // 골드 차감
            PlayerManager.Instance.SpendGold(price);

            // 카드를 플레이어 스타터 덱에 추가
            if (CardManager.Instance != null)
            {
                CardManager.Instance.AddCardToStarterDeck(cardData);
                Debug.Log($"Purchased card added to starter deck: {cardData.CardName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 유물 구매 처리
        /// </summary>
        private bool PurchaseRelic(RelicSO relicData, int price)
        {
            if (relicData == null) return false;

            // 골드 차감
            PlayerManager.Instance.SpendGold(price);

            // 유물을 플레이어에게 추가
            PlayerManager.Instance.AddRelic(relicData);
            Debug.Log($"Purchased relic: {relicData.RelicName}");
            return true;
        }

        /// <summary>
        /// 카드 제거 서비스 구매 처리
        /// </summary>
        private bool PurchaseCardRemoval(int price)
        {
            // 골드 차감은 실제 카드 제거 시에 수행
            // 일단 카드 제거 가격을 저장
            cardRemovalPrice = price;

            // 카드 제거 UI 요청 이벤트 발생
            OnCardRemovalRequested?.Invoke();
            Debug.Log("Card removal service purchased - Requesting card selection UI");

            return true;
        }

        /// <summary>
        /// 체력 회복 서비스 구매 처리
        /// </summary>
        private bool PurchaseHealthRestore(int amount, int price)
        {
            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[ShopManager] PlayerManager.Instance가 null입니다!");
                return false;
            }

            int oldHealth = PlayerManager.Instance.CurrentHealth;
            int maxHealth = PlayerManager.Instance.MaxHealth;

            // 골드 차감
            PlayerManager.Instance.SpendGold(price);

            // 체력 회복
            PlayerManager.Instance.Heal(amount);

            int newHealth = PlayerManager.Instance.CurrentHealth;
            Debug.Log($"[ShopManager] 체력 회복: {oldHealth}/{maxHealth} -> {newHealth}/{maxHealth} (+{newHealth - oldHealth})");

            return true;
        }

        /// <summary>
        /// 아이템의 최종 가격 계산
        /// </summary>
        public int GetItemPrice(ShopItem item)
        {
            if (CurrentShop == null) return item.basePrice;
            return CurrentShop.GetFinalPrice(item);
        }

        /// <summary>
        /// 플레이어가 아이템을 구매할 수 있는지 확인
        /// </summary>
        public bool CanPurchaseItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= currentShopItems.Length)
                return false;

            if (itemsPurchased[itemIndex])
                return false;

            ShopItem item = currentShopItems[itemIndex];

            // 카드 제거 서비스 중복 체크
            if (item.itemType == ShopItemType.CardRemoval && cardRemovalUsed)
                return false;

            // 체력 회복 서비스 특별 처리
            if (item.itemType == ShopItemType.HealthRestore)
            {
                if (PlayerManager.Instance == null)
                {
                    Debug.Log("[ShopManager] CanPurchaseItem: PlayerManager가 null");
                    return false;
                }

                // 현재 체력이 최대 체력과 같으면 구매 불가
                bool isHealthFull = PlayerManager.Instance.CurrentHealth >= PlayerManager.Instance.MaxHealth;
                if (isHealthFull)
                {
                    Debug.Log($"[ShopManager] CanPurchaseItem: 체력이 가득참 ({PlayerManager.Instance.CurrentHealth}/{PlayerManager.Instance.MaxHealth})");
                    return false;
                }
            }

            int price = GetItemPrice(item);
            bool hasEnoughGold = PlayerManager.Instance != null && PlayerManager.Instance.CurrentGold >= price;

            if (!hasEnoughGold && item.itemType == ShopItemType.HealthRestore)
            {
                Debug.Log($"[ShopManager] CanPurchaseItem: 골드 부족 (현재:{PlayerManager.Instance?.CurrentGold}, 필요:{price})");
            }

            return hasEnoughGold;
        }

        /// <summary>
        /// 상점 새로고침 (테스트용)
        /// </summary>
        public void RefreshShop()
        {
            var floorManager = FloorManager.Instance;
            int currentFloor = floorManager != null ? floorManager.CurrentFloor : 1;
            GenerateShopItems(currentFloor);
        }

        /// <summary>
        /// 카드 제거 실행 (카드 선택 UI에서 호출)
        /// 스타터 덱에서 카드 제거
        /// </summary>
        public bool RemoveCard(CardSO cardToRemove, int cardIndex)
        {
            if (CardManager.Instance == null || cardToRemove == null)
                return false;

            // 골드가 충분한지 확인
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentGold < cardRemovalPrice)
            {
                Debug.LogWarning("카드 제거를 위한 골드가 부족합니다!");
                return false;
            }

            // 스타터 덱에서 특정 인덱스의 카드 제거
            bool removed = CardManager.Instance.RemoveCardFromStarterDeckByIndex(cardToRemove, cardIndex);
            if (removed)
            {
                // 실제 카드 제거 시에 골드 차감
                PlayerManager.Instance.SpendGold(cardRemovalPrice);
                cardRemovalUsed = true;
                cardRemovalPrice = 0; // 가격 초기화

                // 카드 제거 서비스 아이템을 구매 완료로 표시
                for (int i = 0; i < currentShopItems.Length; i++)
                {
                    if (currentShopItems[i].itemType == ShopItemType.CardRemoval)
                    {
                        itemsPurchased[i] = true;
                        break;
                    }
                }

                Debug.Log($"Removed card from starter deck: {cardToRemove.CardName} (인덱스: {cardIndex})");
            }

            return removed;
        }

        /// <summary>
        /// 카드 제거 취소 (UI에서 취소 버튼 클릭 시 호출)
        /// </summary>
        public void CancelCardRemoval()
        {
            cardRemovalPrice = 0; // 가격 초기화
            Debug.Log("[ShopManager] 카드 제거 서비스 취소됨");
        }

        /// <summary>
        /// 플레이어 덱의 카드 목록 반환 (카드 제거 UI용)
        /// 스타터 덱 기준으로 반환
        /// </summary>
        public List<CardSO> GetPlayerDeckCards()
        {
            if (CardManager.Instance == null)
            {
                Debug.LogWarning("[ShopManager] CardManager.Instance가 null입니다!");
                return new List<CardSO>();
            }

            var starterDeckCards = CardManager.Instance.GetStarterDeckCards();
            Debug.Log($"[ShopManager] 스타터 덱 카드 수: {starterDeckCards.Count}");

            if (starterDeckCards.Count > 0)
            {
                Debug.Log($"[ShopManager] 스타터 덱 카드 목록: {string.Join(", ", starterDeckCards.Select(c => c.CardName))}");
            }

            return starterDeckCards;
        }
    }
}