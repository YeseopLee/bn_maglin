using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Player;
using Maglin.Cards;

namespace Maglin.Shop
{
    /// <summary>
    /// 상점 UI를 관리하는 매니저 클래스
    /// </summary>
    public class ShopUIManager : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private CanvasGroup shopCanvasGroup;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("상품 슬롯")]
        [SerializeField] private Transform[] cardSlots = new Transform[5];
        [SerializeField] private Transform[] relicSlots = new Transform[3];
        [SerializeField] private Transform cardRemovalSlot;
        [SerializeField] private Transform healthRestoreSlot;

        [Header("카드 제거 UI")]
        [SerializeField] private CanvasGroup cardRemovalCanvasGroup;
        [SerializeField] private Transform cardListContent;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmRemovalButton; // 버리기 확인 버튼

        [Header("하단 버튼")]
        [SerializeField] private Button exitButton;

        [Header("프리팹")]
        [SerializeField] private GameObject cardUIPrefab;

        // 싱글톤
        public static ShopUIManager Instance { get; private set; }

        // 현재 상태
        private ShopItem[] currentItems;
        private bool[] itemsPurchased;
        private List<GameObject> cardRemovalUIs = new List<GameObject>();
        private CardSO selectedCardToRemove; // 제거할 선택된 카드
        private int selectedCardIndex = -1; // 선택된 카드의 인덱스

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeUI();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 초기 상태 업데이트 (지연 실행으로 다른 매니저들이 초기화된 후 실행)
            StartCoroutine(InitialUpdate());
        }

        /// <summary>
        /// 초기 업데이트 (다른 매니저들이 초기화된 후)
        /// </summary>
        private System.Collections.IEnumerator InitialUpdate()
        {
            yield return new WaitForEndOfFrame();

            // 플레이어 상태 초기 업데이트
            UpdatePlayerStatus();

            // ShopManager가 있고 상점이 아직 초기화되지 않았다면 자동으로 상점 진입
            if (ShopManager.Instance != null && ShopManager.Instance.CurrentItems == null)
            {
                if (debugMode)
                    Debug.Log("[ShopUIManager] 자동으로 상점 진입 실행");
                ShopManager.Instance.EnterShop(1); // 테스트용으로 1층
            }

            // 상점 아이템이 있다면 즉시 UI 업데이트
            if (ShopManager.Instance != null && ShopManager.Instance.CurrentItems != null)
            {
                if (debugMode)
                    Debug.Log("[ShopUIManager] 기존 상점 아이템으로 UI 업데이트");
                UpdateShopItems();
            }
        }

        private void OnEnable()
        {
            // ShopManager 이벤트 구독 (지연 처리)
            StartCoroutine(SubscribeToShopManagerEvents());

            // PlayerManager static 이벤트 구독
            PlayerManager.OnGoldChanged += OnGoldChanged;
            PlayerManager.OnHealthChanged += OnHealthChanged;
        }

        /// <summary>
        /// ShopManager 이벤트 구독 (안전하게 처리)
        /// </summary>
        private System.Collections.IEnumerator SubscribeToShopManagerEvents()
        {
            // ShopManager가 초기화될 때까지 대기
            while (ShopManager.Instance == null)
            {
                yield return null;
            }

            // 이벤트 구독
            ShopManager.Instance.OnShopEntered += OnShopEntered;
            ShopManager.Instance.OnShopExited += OnShopExited;
            ShopManager.Instance.OnShopRefreshed += OnShopRefreshed;
            ShopManager.Instance.OnItemPurchased += OnItemPurchased;
            ShopManager.Instance.OnCardRemovalRequested += OnCardRemovalRequested;

            if (debugMode)
                Debug.Log("[ShopUIManager] ShopManager 이벤트 구독 완료");
        }

        private void OnDisable()
        {
            // ShopManager 이벤트 구독 해제
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnShopEntered -= OnShopEntered;
                ShopManager.Instance.OnShopExited -= OnShopExited;
                ShopManager.Instance.OnShopRefreshed -= OnShopRefreshed;
                ShopManager.Instance.OnItemPurchased -= OnItemPurchased;
                ShopManager.Instance.OnCardRemovalRequested -= OnCardRemovalRequested;
            }

            // PlayerManager static 이벤트 구독 해제
            PlayerManager.OnGoldChanged -= OnGoldChanged;
            PlayerManager.OnHealthChanged -= OnHealthChanged;
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] UI 초기화 시작");

            // UI 참조 자동 찾기
            FindUIReferences();

            // 버튼 이벤트 연결
            SetupButtonEvents();

            // 초기 상태 설정
            if (shopCanvasGroup != null)
            {
                shopCanvasGroup.alpha = 1f;
                shopCanvasGroup.interactable = true;
                shopCanvasGroup.blocksRaycasts = true;
            }

            // 카드 제거 UI 숨김
            if (cardRemovalCanvasGroup != null)
            {
                cardRemovalCanvasGroup.alpha = 0f;
                cardRemovalCanvasGroup.interactable = false;
                cardRemovalCanvasGroup.blocksRaycasts = false;
            }

            if (debugMode)
                Debug.Log("[ShopUIManager] UI 초기화 완료");
        }

        /// <summary>
        /// UI 참조 자동 찾기
        /// </summary>
        private void FindUIReferences()
        {
            // 메인 상점 UI 찾기
            if (shopCanvasGroup == null)
            {
                var shopUI = GameObject.Find("ShopUI");
                if (shopUI != null)
                {
                    shopCanvasGroup = shopUI.GetComponent<CanvasGroup>();
                }
            }

            // 플레이어 상태 텍스트 찾기
            if (goldText == null)
            {
                var goldTextObj = GameObject.Find("GoldText");
                if (goldTextObj != null)
                {
                    goldText = goldTextObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (healthText == null)
            {
                var healthTextObj = GameObject.Find("HealthText");
                if (healthTextObj != null)
                {
                    healthText = healthTextObj.GetComponent<TextMeshProUGUI>();
                }
            }

            // 상품 슬롯 찾기
            FindItemSlots();

            // 카드 제거 UI 찾기
            FindCardRemovalUI();

            // 하단 버튼 찾기
            if (exitButton == null)
            {
                var exitButtonObj = GameObject.Find("ExitButton");
                if (exitButtonObj != null)
                {
                    exitButton = exitButtonObj.GetComponent<Button>();
                }
            }
        }

        /// <summary>
        /// 상품 슬롯 찾기
        /// </summary>
        private void FindItemSlots()
        {
            // 카드 슬롯
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null)
                {
                    var slotObj = GameObject.Find($"CardSlot_{i}");
                    if (slotObj != null)
                    {
                        cardSlots[i] = slotObj.transform;
                    }
                }
            }

            // 유물 슬롯
            for (int i = 0; i < relicSlots.Length; i++)
            {
                if (relicSlots[i] == null)
                {
                    var slotObj = GameObject.Find($"RelicSlot_{i}");
                    if (slotObj != null)
                    {
                        relicSlots[i] = slotObj.transform;
                    }
                }
            }

            // 서비스 슬롯
            if (cardRemovalSlot == null)
            {
                var slotObj = GameObject.Find("CardRemovalSlot");
                if (slotObj != null)
                {
                    cardRemovalSlot = slotObj.transform;
                }
            }

            if (healthRestoreSlot == null)
            {
                var slotObj = GameObject.Find("HealthRestoreSlot");
                if (slotObj != null)
                {
                    healthRestoreSlot = slotObj.transform;
                }
            }
        }

        /// <summary>
        /// 카드 제거 UI 찾기
        /// </summary>
        private void FindCardRemovalUI()
        {
            if (cardRemovalCanvasGroup == null)
            {
                var cardRemovalUI = GameObject.Find("CardRemovalUI");
                if (cardRemovalUI != null)
                {
                    cardRemovalCanvasGroup = cardRemovalUI.GetComponent<CanvasGroup>();
                }
            }

            if (cardListContent == null)
            {
                var contentObj = GameObject.Find("Content");
                if (contentObj != null && contentObj.transform.parent.name == "Viewport")
                {
                    cardListContent = contentObj.transform;
                }
            }

            if (cancelButton == null)
            {
                var cancelButtonObj = GameObject.Find("CancelButton");
                if (cancelButtonObj != null)
                {
                    cancelButton = cancelButtonObj.GetComponent<Button>();
                }
            }

            if (confirmRemovalButton == null)
            {
                var confirmButtonObj = GameObject.Find("ConfirmRemovalButton");
                if (confirmButtonObj != null)
                {
                    confirmRemovalButton = confirmButtonObj.GetComponent<Button>();
                }
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            // 나가기 버튼
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }

            // 취소 버튼
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelCardRemoval);
            }

            // 확인 버튼 (카드 제거)
            if (confirmRemovalButton != null)
            {
                confirmRemovalButton.onClick.RemoveAllListeners();
                confirmRemovalButton.onClick.AddListener(OnConfirmCardRemoval);
            }

            // 상품 슬롯 버튼들 설정
            SetupItemSlotButtons();
        }

        /// <summary>
        /// 상품 슬롯 버튼 설정
        /// </summary>
        private void SetupItemSlotButtons()
        {
            // 카드 슬롯
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null)
                {
                    var button = cardSlots[i].GetComponent<Button>();
                    if (button != null)
                    {
                        int slotIndex = i; // 클로저용
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));
                    }
                }
            }

            // 유물 슬롯
            for (int i = 0; i < relicSlots.Length; i++)
            {
                if (relicSlots[i] != null)
                {
                    var button = relicSlots[i].GetComponent<Button>();
                    if (button != null)
                    {
                        int slotIndex = i + cardSlots.Length; // 카드 슬롯 다음부터
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));
                    }
                }
            }

            // 서비스 슬롯 (별도 처리)
            if (cardRemovalSlot != null)
            {
                var button = cardRemovalSlot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnCardRemovalSlotClicked());
                }
            }

            if (healthRestoreSlot != null)
            {
                var button = healthRestoreSlot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnHealthRestoreSlotClicked());
                }
            }
        }

        /// <summary>
        /// 상점 진입 이벤트 처리
        /// </summary>
        private void OnShopEntered()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 상점 진입");

            UpdatePlayerStatus();
            UpdateShopItems(); // 상점 진입 시에도 아이템 UI 업데이트
        }

        /// <summary>
        /// 상점 퇴장 이벤트 처리
        /// </summary>
        private void OnShopExited()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 상점 퇴장");
        }

        /// <summary>
        /// 상점 새로고침 이벤트 처리
        /// </summary>
        private void OnShopRefreshed()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 상점 새로고침");

            UpdateShopItems();
        }

        /// <summary>
        /// 아이템 구매 이벤트 처리
        /// </summary>
        private void OnItemPurchased(ShopItem item, int price)
        {
            if (debugMode)
                Debug.Log($"[ShopUIManager] 아이템 구매: {item.itemName} ({price} 골드)");

            // 즉시 UI 업데이트
            UpdatePlayerStatus();
            UpdateShopItems();

            if (debugMode)
                Debug.Log($"[ShopUIManager] UI 업데이트 완료 - 구매 완료된 아이템: {item.itemName}");
        }

        /// <summary>
        /// 플레이어 상태 업데이트
        /// </summary>
        private void UpdatePlayerStatus()
        {
            if (PlayerManager.Instance == null)
            {
                if (debugMode)
                    Debug.LogWarning("[ShopUIManager] PlayerManager.Instance가 null입니다!");
                return;
            }

            // 골드 업데이트
            if (goldText != null)
            {
                goldText.text = $"골드: {PlayerManager.Instance.CurrentGold}";
            }

            // 체력 업데이트
            if (healthText != null)
            {
                int currentHealth = PlayerManager.Instance.CurrentHealth;
                int maxHealth = PlayerManager.Instance.MaxHealth;
                healthText.text = $"체력: {currentHealth}/{maxHealth}";

                if (debugMode)
                    Debug.Log($"[ShopUIManager] 체력 UI 업데이트: {currentHealth}/{maxHealth}");
            }
        }

        /// <summary>
        /// 상점 아이템 업데이트
        /// </summary>
        private void UpdateShopItems()
        {
            if (ShopManager.Instance == null) return;

            currentItems = ShopManager.Instance.CurrentItems;
            itemsPurchased = ShopManager.Instance.ItemsPurchased;

            if (currentItems == null)
            {
                if (debugMode)
                    Debug.LogWarning("[ShopUIManager] currentItems가 null입니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] 상점 아이템 업데이트: {currentItems.Length}개");

            // 타입별로 아이템 분류
            var cardItems = new List<(ShopItem item, int index)>();
            var relicItems = new List<(ShopItem item, int index)>();

            for (int i = 0; i < currentItems.Length; i++)
            {
                switch (currentItems[i].itemType)
                {
                    case ShopItemType.Card:
                        cardItems.Add((currentItems[i], i));
                        break;
                    case ShopItemType.Relic:
                        relicItems.Add((currentItems[i], i));
                        break;
                }
            }

            // 카드 슬롯 업데이트
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (i < cardItems.Count)
                {
                    UpdateItemSlot(cardSlots[i], cardItems[i].item, cardItems[i].index);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 카드 슬롯 {i}: {cardItems[i].item.itemName} (구매완료: {(itemsPurchased != null && cardItems[i].index < itemsPurchased.Length ? itemsPurchased[cardItems[i].index] : false)})");
                }
                else
                {
                    ClearItemSlot(cardSlots[i]);
                }
            }

            // 유물 슬롯 업데이트
            for (int i = 0; i < relicSlots.Length; i++)
            {
                if (i < relicItems.Count)
                {
                    UpdateItemSlot(relicSlots[i], relicItems[i].item, relicItems[i].index);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 유물 슬롯 {i}: {relicItems[i].item.itemName}");
                }
                else
                {
                    ClearItemSlot(relicSlots[i]);
                }
            }

            // 서비스 슬롯 업데이트
            UpdateServiceSlots();

            if (debugMode)
                Debug.Log($"[ShopUIManager] 아이템 UI 업데이트 완료: 카드 {cardItems.Count}개, 유물 {relicItems.Count}개");
        }

        /// <summary>
        /// 서비스 슬롯 업데이트
        /// </summary>
        private void UpdateServiceSlots()
        {
            if (currentItems == null) return;

            // 카드 제거 서비스 찾기
            ShopItem cardRemovalItem = System.Array.Find(currentItems,
                item => item.itemType == ShopItemType.CardRemoval);

            if (cardRemovalItem != null && cardRemovalSlot != null)
            {
                UpdateServiceSlot(cardRemovalSlot, cardRemovalItem);

                // 구매 여부에 따른 버튼 상태 업데이트
                var button = cardRemovalSlot.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !ShopManager.Instance.CardRemovalUsed &&
                                        ShopManager.Instance.CanPurchaseItem(System.Array.IndexOf(currentItems, cardRemovalItem));
                }
            }

            // 체력 회복 서비스 찾기
            ShopItem healthRestoreItem = System.Array.Find(currentItems,
                item => item.itemType == ShopItemType.HealthRestore);

            if (healthRestoreItem != null && healthRestoreSlot != null)
            {
                UpdateServiceSlot(healthRestoreSlot, healthRestoreItem);

                // 구매 여부에 따른 버튼 상태 업데이트
                var button = healthRestoreSlot.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = ShopManager.Instance.CanPurchaseItem(System.Array.IndexOf(currentItems, healthRestoreItem));
                }
            }
        }

        /// <summary>
        /// 개별 아이템 슬롯 업데이트
        /// </summary>
        private void UpdateItemSlot(Transform slot, ShopItem item, int itemIndex)
        {
            if (slot == null || item == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[ShopUIManager] UpdateItemSlot 호출 시 slot이나 item이 null: slot={slot}, item={item}");
                return;
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] UpdateItemSlot: {slot.name} <- {item.itemName}");

            // 아이템 아이콘
            var iconImage = slot.Find("ItemIcon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = item.itemIcon;
                iconImage.color = item.itemIcon != null ? Color.white : GetItemTypeColor(item.itemType);
                if (debugMode)
                    Debug.Log($"[ShopUIManager] 아이콘 업데이트: {item.itemName} (sprite: {item.itemIcon})");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[ShopUIManager] ItemIcon을 찾을 수 없음: {slot.name}");
            }

            // 아이템 이름
            var nameText = slot.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = item.itemName;
                if (debugMode)
                    Debug.Log($"[ShopUIManager] 이름 업데이트: {item.itemName}");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[ShopUIManager] ItemName을 찾을 수 없음: {slot.name}");
            }

            // 아이템 설명
            var descText = slot.Find("ItemDescription")?.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = item.itemDescription;
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[ShopUIManager] ItemDescription을 찾을 수 없음: {slot.name}");
            }

            // 가격
            var priceText = slot.Find("ItemPrice")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                int finalPrice = ShopManager.Instance.GetItemPrice(item);
                priceText.text = $"{finalPrice} 골드";
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[ShopUIManager] ItemPrice를 찾을 수 없음: {slot.name}");
            }

            // 버튼 상태
            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = ShopManager.Instance.CanPurchaseItem(itemIndex);
            }

            // 구매 완료 표시
            if (itemsPurchased != null && itemIndex < itemsPurchased.Length && itemsPurchased[itemIndex])
            {
                var slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 회색으로 표시
                }

                // 가격 텍스트를 "구매 완료"로 변경
                if (priceText != null)
                {
                    priceText.text = "구매 완료";
                    priceText.color = Color.green;
                }

                if (debugMode)
                    Debug.Log($"[ShopUIManager] 구매 완료 표시 적용: {item.itemName}");
            }
            else if (priceText != null)
            {
                // 구매 가능한 상태일 때 가격 색상 복원
                priceText.color = Color.white;
            }
        }

        /// <summary>
        /// 서비스 슬롯 업데이트
        /// </summary>
        private void UpdateServiceSlot(Transform slot, ShopItem item)
        {
            if (slot == null || item == null) return;

            // 서비스들은 UI에 미리 설정된 텍스트 유지 (카드 제거, 체력 회복)
            if (item.itemType != ShopItemType.CardRemoval && item.itemType != ShopItemType.HealthRestore)
            {
                // 서비스 이름
                var nameText = slot.Find("ServiceName")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = item.itemName;
                }

                // 서비스 설명
                var descText = slot.Find("ServiceDescription")?.GetComponent<TextMeshProUGUI>();
                if (descText != null)
                {
                    descText.text = item.itemDescription;
                }
            }

            // 가격만 업데이트 (모든 서비스 공통)
            var priceText = slot.Find("ItemPrice")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                int finalPrice = ShopManager.Instance.GetItemPrice(item);
                priceText.text = $"{finalPrice} 골드";
            }
        }

        /// <summary>
        /// 아이템 슬롯 클리어
        /// </summary>
        private void ClearItemSlot(Transform slot)
        {
            if (slot == null) return;

            // 아이콘 클리어
            var iconImage = slot.Find("ItemIcon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.gray;
            }

            // 텍스트 클리어
            var nameText = slot.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = "품절";
            }

            var descText = slot.Find("ItemDescription")?.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = "";
            }

            var priceText = slot.Find("ItemPrice")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                priceText.text = "";
            }

            // 버튼 비활성화
            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
            }
        }

        /// <summary>
        /// 아이템 타입별 기본 색상 반환
        /// </summary>
        private Color GetItemTypeColor(ShopItemType itemType)
        {
            return itemType switch
            {
                ShopItemType.Card => Color.green,
                ShopItemType.Relic => Color.yellow,
                ShopItemType.CardRemoval => Color.red,
                ShopItemType.HealthRestore => Color.cyan,
                _ => Color.gray
            };
        }

        /// <summary>
        /// 골드 변경 이벤트 처리
        /// </summary>
        private void OnGoldChanged(int newGold)
        {
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 체력 변경 이벤트 처리
        /// </summary>
        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            UpdatePlayerStatus();
        }

        /// <summary>
        /// 아이템 슬롯 클릭 이벤트 처리
        /// </summary>
        private void OnItemSlotClicked(int slotIndex)
        {
            if (debugMode)
                Debug.Log($"[ShopUIManager] 슬롯 {slotIndex} 클릭");

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.TryPurchaseItem(slotIndex);
            }
        }

        /// <summary>
        /// 카드 제거 슬롯 클릭 이벤트 처리
        /// </summary>
        private void OnCardRemovalSlotClicked()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 카드 제거 슬롯 클릭");

            if (ShopManager.Instance != null && currentItems != null)
            {
                // 카드 제거 서비스 아이템 찾기
                for (int i = 0; i < currentItems.Length; i++)
                {
                    if (currentItems[i].itemType == ShopItemType.CardRemoval)
                    {
                        ShopManager.Instance.TryPurchaseItem(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 체력 회복 슬롯 클릭 이벤트 처리
        /// </summary>
        private void OnHealthRestoreSlotClicked()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 체력 회복 슬롯 클릭");

            if (ShopManager.Instance != null && currentItems != null)
            {
                // 체력 회복 서비스 아이템 찾기
                for (int i = 0; i < currentItems.Length; i++)
                {
                    if (currentItems[i].itemType == ShopItemType.HealthRestore)
                    {
                        ShopManager.Instance.TryPurchaseItem(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 나가기 버튼 클릭
        /// </summary>
        private void OnExitButtonClicked()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 나가기 버튼 클릭");

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.ExitShop();
            }
        }

        /// <summary>
        /// 카드 제거 요청 이벤트 처리
        /// </summary>
        private void OnCardRemovalRequested()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 카드 제거 UI 요청");

            ShowCardRemovalUI();
        }

        /// <summary>
        /// 카드 제거 UI 표시
        /// </summary>
        private void ShowCardRemovalUI()
        {
            if (cardRemovalCanvasGroup == null)
            {
                Debug.LogError("[ShopUIManager] 카드 제거 UI를 찾을 수 없습니다!");
                return;
            }

            // UI 표시
            cardRemovalCanvasGroup.alpha = 1f;
            cardRemovalCanvasGroup.interactable = true;
            cardRemovalCanvasGroup.blocksRaycasts = true;

            // 선택된 카드 초기화
            selectedCardToRemove = null;
            selectedCardIndex = -1;

            // 확인 버튼 비활성화
            if (confirmRemovalButton != null)
            {
                confirmRemovalButton.interactable = false;
            }

            // 카드 목록 업데이트
            UpdateCardRemovalList();
        }

        /// <summary>
        /// 카드 제거 UI 숨김
        /// </summary>
        private void HideCardRemovalUI()
        {
            if (cardRemovalCanvasGroup != null)
            {
                cardRemovalCanvasGroup.alpha = 0f;
                cardRemovalCanvasGroup.interactable = false;
                cardRemovalCanvasGroup.blocksRaycasts = false;
            }

            // 카드 UI 정리
            ClearCardRemovalList();
        }

        /// <summary>
        /// 카드 제거 목록 업데이트
        /// </summary>
        private void UpdateCardRemovalList()
        {
            if (cardListContent == null)
            {
                Debug.LogError("[ShopUIManager] 카드 목록 컨테이너를 찾을 수 없습니다!");
                return;
            }

            // 기존 카드 UI 정리
            ClearCardRemovalList();

            // 플레이어 덱의 카드 목록 가져오기
            var deckCards = ShopManager.Instance.GetPlayerDeckCards();

            // 각 카드에 대해 UI 생성 (인덱스와 함께)
            for (int i = 0; i < deckCards.Count; i++)
            {
                GameObject cardUI = CreateCardRemovalUI(deckCards[i], i);
                if (cardUI != null)
                {
                    cardRemovalUIs.Add(cardUI);
                }
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] {deckCards.Count}개 카드 제거 UI 생성");
        }

        /// <summary>
        /// 카드 제거 UI 생성
        /// </summary>
        private GameObject CreateCardRemovalUI(CardSO cardData, int cardIndex)
        {
            if (cardUIPrefab != null)
            {
                GameObject cardUI = Instantiate(cardUIPrefab, cardListContent);

                // 카드 정보 업데이트
                var nameText = cardUI.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = cardData.CardName;
                }

                var iconImage = cardUI.transform.Find("CardIcon")?.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = cardData.Image;
                }

                // 카드 인덱스를 UI에 저장 (고유 식별용)
                var cardIndexComponent = cardUI.AddComponent<CardIndexComponent>();
                cardIndexComponent.cardIndex = cardIndex;
                cardIndexComponent.cardData = cardData;

                // 버튼 이벤트 연결
                var button = cardUI.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnCardSelectedForRemoval(cardData, cardIndex));
                }

                return cardUI;
            }
            else
            {
                // 기본 카드 UI 생성
                return CreateBasicCardRemovalUI(cardData, cardIndex);
            }
        }

        /// <summary>
        /// 기본 카드 제거 UI 생성
        /// </summary>
        private GameObject CreateBasicCardRemovalUI(CardSO cardData, int cardIndex)
        {
            GameObject cardUI = new GameObject($"Card_{cardData.CardName}_{cardIndex}");
            cardUI.transform.SetParent(cardListContent, false);

            // RectTransform 설정
            RectTransform cardRect = cardUI.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(200, 60); // 카드 크기 설정

            // 배경 이미지
            Image bgImage = cardUI.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // 카드 인덱스를 UI에 저장 (고유 식별용)
            var cardIndexComponent = cardUI.AddComponent<CardIndexComponent>();
            cardIndexComponent.cardIndex = cardIndex;
            cardIndexComponent.cardData = cardData;

            // 버튼 컴포넌트
            Button button = cardUI.AddComponent<Button>();
            button.targetGraphic = bgImage;
            button.onClick.AddListener(() => OnCardSelectedForRemoval(cardData, cardIndex));

            // 카드 이름 텍스트
            GameObject nameObj = new GameObject("CardName");
            nameObj.transform.SetParent(cardUI.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = $"{cardData.CardName} ({cardIndex + 1})"; // 인덱스 표시
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Center;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(5, 5);
            nameRect.offsetMax = new Vector2(-5, -5);

            return cardUI;
        }

        /// <summary>
        /// 카드 제거 목록 클리어
        /// </summary>
        private void ClearCardRemovalList()
        {
            foreach (var cardUI in cardRemovalUIs)
            {
                if (cardUI != null)
                {
                    Destroy(cardUI);
                }
            }
            cardRemovalUIs.Clear();
        }

        /// <summary>
        /// 카드 제거 선택 상태 업데이트
        /// </summary>
        private void UpdateCardRemovalSelection()
        {
            for (int i = 0; i < cardRemovalUIs.Count; i++)
            {
                var cardUI = cardRemovalUIs[i];
                if (cardUI == null) continue;

                var bgImage = cardUI.GetComponent<Image>();
                var cardIndexComponent = cardUI.GetComponent<CardIndexComponent>();

                if (bgImage != null && cardIndexComponent != null)
                {
                    // 인덱스 기반으로 정확한 카드만 선택
                    if (cardIndexComponent.cardIndex == selectedCardIndex &&
                        cardIndexComponent.cardData == selectedCardToRemove)
                    {
                        // 선택된 카드는 하이라이트
                        bgImage.color = new Color(1f, 1f, 0f, 0.8f); // 노란색
                    }
                    else
                    {
                        // 선택되지 않은 카드는 기본 색상
                        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 회색
                    }
                }
            }
        }

        /// <summary>
        /// 카드 UI에서 CardSO 데이터 가져오기
        /// </summary>
        private CardSO GetCardDataFromUI(GameObject cardUI)
        {
            // 카드 이름을 기반으로 CardSO 찾기
            var nameText = cardUI.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                var deckCards = ShopManager.Instance.GetPlayerDeckCards();
                return deckCards.Find(card => card.CardName == nameText.text);
            }
            return null;
        }

        /// <summary>
        /// 카드 제거 선택 이벤트 처리
        /// </summary>
        private void OnCardSelectedForRemoval(CardSO cardData, int cardIndex)
        {
            if (debugMode)
                Debug.Log($"[ShopUIManager] 카드 선택: {cardData.CardName} (인덱스: {cardIndex})");

            // 선택된 카드 설정
            selectedCardToRemove = cardData;
            selectedCardIndex = cardIndex;

            // 카드 UI 업데이트 (선택 표시)
            UpdateCardRemovalSelection();

            // 확인 버튼 활성화
            if (confirmRemovalButton != null)
            {
                confirmRemovalButton.interactable = true;
            }
        }

        /// <summary>
        /// 카드 제거 취소
        /// </summary>
        private void OnCancelCardRemoval()
        {
            if (debugMode)
                Debug.Log("[ShopUIManager] 카드 제거 취소");

            // ShopManager에 취소 알림
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.CancelCardRemoval();
            }

            HideCardRemovalUI();
        }

        /// <summary>
        /// 카드 제거 확인
        /// </summary>
        private void OnConfirmCardRemoval()
        {
            if (selectedCardToRemove == null || selectedCardIndex < 0)
            {
                Debug.LogWarning("[ShopUIManager] 선택된 카드가 없습니다!");
                return;
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] 카드 제거 확인: {selectedCardToRemove.CardName} (인덱스: {selectedCardIndex})");

            // 카드 제거 실행
            if (ShopManager.Instance.RemoveCard(selectedCardToRemove, selectedCardIndex))
            {
                // 성공적으로 제거되면 UI 숨김
                HideCardRemovalUI();

                // 상점 UI 업데이트 (골드 변경 반영)
                UpdatePlayerStatus();
                UpdateShopItems();
            }
            else
            {
                Debug.LogWarning("[ShopUIManager] 카드 제거에 실패했습니다!");
            }
        }

        /// <summary>
        /// 상점 UI 표시
        /// </summary>
        public void ShowShopUI()
        {
            if (shopCanvasGroup != null)
            {
                shopCanvasGroup.alpha = 1f;
                shopCanvasGroup.interactable = true;
                shopCanvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 상점 UI 숨김
        /// </summary>
        public void HideShopUI()
        {
            if (shopCanvasGroup != null)
            {
                shopCanvasGroup.alpha = 0f;
                shopCanvasGroup.interactable = false;
                shopCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    /// <summary>
    /// 카드 UI에 카드 인덱스와 데이터를 저장하는 컴포넌트
    /// </summary>
    public class CardIndexComponent : MonoBehaviour
    {
        public int cardIndex;
        public CardSO cardData;
    }
}