using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Player;
using Maglin.Cards;
using Maglin.Relics; // RelicSO를 위해 추가
using Maglin.Battle; // CardUIData를 위해 추가

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
        [SerializeField] private Transform[] itemSlots = new Transform[8]; // 통합된 아이템 슬롯 (카드5개 + 유물3개)
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
        [SerializeField] private GameObject relicPrefab; // 유물 프리팹

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
            if (debugMode)
                Debug.Log("[ShopUIManager] FindItemSlots 시작");

            // 통합된 아이템 슬롯 찾기 (CardSlot_0~4, RelicSlot_0~2 순서로)
            for (int i = 0; i < itemSlots.Length; i++)
            {
                GameObject slotObj = null;
                string slotName = "";

                // 처음 5개는 카드 슬롯으로 찾기
                if (i < 5)
                {
                    slotName = $"CardSlot_{i}";
                    slotObj = GameObject.Find(slotName);
                }
                // 나머지 3개는 유물 슬롯으로 찾기
                else
                {
                    slotName = $"RelicSlot_{i - 5}";
                    slotObj = GameObject.Find(slotName);
                }

                if (slotObj != null)
                {
                    itemSlots[i] = slotObj.transform;
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 슬롯 {i} 찾음: {slotName} -> {slotObj.name}");
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"[ShopUIManager] 슬롯 {i} 찾을 수 없음: {slotName}");
            }
            }

            // 찾은 슬롯 수 확인
            int foundSlots = 0;
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (itemSlots[i] != null) foundSlots++;
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] 총 {foundSlots}개 슬롯 찾음 (카드 5개 + 유물 3개 예상)");

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
            // 통합된 아이템 슬롯
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (itemSlots[i] != null)
                {
                    var button = itemSlots[i].GetComponent<Button>();
                    if (button != null)
                    {
                        int slotIndex = i; // 클로저용
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

            // 일반 아이템 (카드, 유물)만 필터링 (서비스 제외)
            var normalItems = new List<(ShopItem item, int index)>();

            for (int i = 0; i < currentItems.Length; i++)
            {
                switch (currentItems[i].itemType)
                {
                    case ShopItemType.Card:
                    case ShopItemType.Relic:
                        normalItems.Add((currentItems[i], i));
                        break;
                    case ShopItemType.CardRemoval:
                    case ShopItemType.HealthRestore:
                        // 서비스 아이템들은 UpdateServiceSlots에서 처리
                        break;
                }
            }

            // 통합된 아이템 슬롯 업데이트
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (i < normalItems.Count)
                {
                    UpdateItemSlot(itemSlots[i], normalItems[i].item, normalItems[i].index);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 아이템 슬롯 {i}: {normalItems[i].item.itemName} ({normalItems[i].item.itemType}) (구매완료: {(itemsPurchased != null && normalItems[i].index < itemsPurchased.Length ? itemsPurchased[normalItems[i].index] : false)})");
                }
                else
                {
                    ClearItemSlot(itemSlots[i]);
                }
            }

            // 서비스 슬롯 업데이트
            if (debugMode)
                Debug.Log("[ShopUIManager] UpdateServiceSlots 호출 시작");
            UpdateServiceSlots();
            if (debugMode)
                Debug.Log("[ShopUIManager] UpdateServiceSlots 호출 완료");

            if (debugMode)
                Debug.Log($"[ShopUIManager] 아이템 UI 업데이트 완료: 총 {normalItems.Count}개 아이템");
        }

        /// <summary>
        /// 서비스 슬롯 업데이트
        /// </summary>
        private void UpdateServiceSlots()
        {
            if (currentItems == null)
            {
                if (debugMode)
                    Debug.Log("[ShopUIManager] UpdateServiceSlots: currentItems가 null");
                return;
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] UpdateServiceSlots: 총 {currentItems.Length}개 아이템 확인");

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
            ShopItem healthRestoreItem = null;
            int healthRestoreIndex = -1;

            if (debugMode)
                Debug.Log("[ShopUIManager] 체력 회복 아이템 검색 시작");

            for (int i = 0; i < currentItems.Length; i++)
            {
                if (debugMode)
                    Debug.Log($"[ShopUIManager] 아이템 {i}: {currentItems[i].itemName} (타입: {currentItems[i].itemType})");

                if (currentItems[i].itemType == ShopItemType.HealthRestore)
                {
                    healthRestoreItem = currentItems[i];
                    healthRestoreIndex = i;
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 체력 회복 아이템 발견: {healthRestoreItem.itemName} (인덱스: {healthRestoreIndex})");
                    break;
                }
            }

            if (healthRestoreItem != null && healthRestoreSlot != null)
            {
                UpdateServiceSlot(healthRestoreSlot, healthRestoreItem);

                // 구매 여부에 따른 버튼 상태 업데이트
                var button = healthRestoreSlot.GetComponent<Button>();
                if (button != null)
                {
                    bool canPurchase = ShopManager.Instance.CanPurchaseItem(healthRestoreIndex);
                    button.interactable = canPurchase;

                    if (debugMode)
                    {
                        Debug.Log($"[ShopUIManager] 체력 회복 버튼 상태 업데이트: 활성화={canPurchase}, " +
                                 $"현재체력={PlayerManager.Instance?.CurrentHealth}, 최대체력={PlayerManager.Instance?.MaxHealth}, " +
                                 $"골드={PlayerManager.Instance?.CurrentGold}, 가격={ShopManager.Instance.GetItemPrice(healthRestoreItem)}");
                    }
                }
            }
            else if (debugMode)
            {
                Debug.Log("[ShopUIManager] 체력 회복 아이템이 없음 - 이제 항상 생성되어야 함");
            }
        }

        /// <summary>
        /// 개별 아이템 슬롯 업데이트 (프리팹 사용)
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

            // 슬롯 활성화
            slot.gameObject.SetActive(true);

            // 기존 프리팹 인스턴스 정리
            ClearItemSlotContent(slot);

            GameObject itemUI = null;

            switch (item.itemType)
            {
                case ShopItemType.Card:
                    itemUI = CreateCardShopUI(slot, item, itemIndex);
                    break;

                case ShopItemType.Relic:
                    itemUI = CreateRelicShopUI(slot, item, itemIndex);
                    break;

                case ShopItemType.CardRemoval:
                case ShopItemType.HealthRestore:
                    // 서비스 아이템은 기존 방식 사용 (슬롯 UI 유지)
                    UpdateServiceItemSlot(slot, item, itemIndex);
                    break;
            }

            if (itemUI != null && debugMode)
            {
                Debug.Log($"[ShopUIManager] 상점 아이템 UI 생성 완료: {item.itemType} - {item.itemName}");
            }
        }

        /// <summary>
        /// 아이템 슬롯 내용 정리 (프리팹 인스턴스만)
        /// </summary>
        private void ClearItemSlotContent(Transform slot)
        {
            // CardUIPrefab(Clone)이나 RelicPrefab(Clone) 같은 프리팹 인스턴스만 제거
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var child = slot.GetChild(i);
                if (child.name.Contains("Prefab") || child.name.Contains("Clone"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 카드 상점 UI 생성 (CardUIPrefab 사용)
        /// </summary>
        private GameObject CreateCardShopUI(Transform slot, ShopItem item, int itemIndex)
        {
            if (cardUIPrefab == null || item.cardData == null)
            {
                if (debugMode)
                    Debug.LogWarning("[ShopUIManager] CardUIPrefab이 없거나 카드 데이터가 null입니다.");
                return null;
            }

            // CardUIPrefab 인스턴스화
            GameObject cardUI = Instantiate(cardUIPrefab, slot);

            // 카드 데이터 설정
            var cardUIData = cardUI.GetComponent<CardUIData>();
            if (cardUIData == null)
            {
                cardUIData = cardUI.AddComponent<CardUIData>();
            }

            // Card 인스턴스 생성 (상점용)
            var cardInstance = new Card(item.cardData);
            cardUIData.CardInstance = cardInstance;

            // 카드 정보 업데이트
            UpdateCardUIInfo(cardUI, cardInstance);

            // 상점 구매 버튼 이벤트 설정
            var button = cardUI.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnItemSlotClicked(itemIndex));
                button.interactable = ShopManager.Instance.CanPurchaseItem(itemIndex);
            }

            // 가격 및 구매 완료 상태 오버레이 추가
            CreateShopItemOverlay(cardUI, item, itemIndex);

            // 크기 조정 (상점 슬롯에 맞게)
            var rectTransform = cardUI.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            return cardUI;
        }

        /// <summary>
        /// 유물 상점 UI 생성 (RelicPrefab 사용)
        /// </summary>
        private GameObject CreateRelicShopUI(Transform slot, ShopItem item, int itemIndex)
        {
            if (relicPrefab == null || item.relicData == null)
            {
                if (debugMode)
                    Debug.LogWarning("[ShopUIManager] RelicPrefab이 없거나 유물 데이터가 null입니다.");
                return null;
            }

            // RelicPrefab 인스턴스화
            GameObject relicUI = Instantiate(relicPrefab, slot);

            // 유물 정보 업데이트
            UpdateRelicUIInfo(relicUI, item.relicData);

            // 상점 구매 버튼 이벤트 설정 (RelicPanel에 Button 컴포넌트 추가)
            var relicPanel = relicUI.transform.Find("RelicPanel");
            if (relicPanel != null)
            {
                var button = relicPanel.GetComponent<Button>();
                if (button == null)
                {
                    button = relicPanel.gameObject.AddComponent<Button>();
                    button.targetGraphic = relicPanel.GetComponent<Image>();
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnItemSlotClicked(itemIndex));
                button.interactable = ShopManager.Instance.CanPurchaseItem(itemIndex);
            }

            // 가격 및 구매 완료 상태 오버레이 추가
            CreateShopItemOverlay(relicUI, item, itemIndex);

            // 크기 조정 (상점 슬롯에 맞게)
            var rectTransform = relicUI.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one * 0.8f; // 유물은 약간 작게
            }

            return relicUI;
        }

        /// <summary>
        /// 상점 아이템 오버레이 생성 (가격, 구매 완료 표시)
        /// </summary>
        private void CreateShopItemOverlay(GameObject itemUI, ShopItem item, int itemIndex)
        {
            // 오버레이 패널 생성
            GameObject overlay = new GameObject("ShopOverlay");
            overlay.transform.SetParent(itemUI.transform, false);

            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // 가격 텍스트 추가
            GameObject priceTextObj = new GameObject("PriceText");
            priceTextObj.transform.SetParent(overlay.transform, false);

            var priceText = priceTextObj.AddComponent<TextMeshProUGUI>();
            var priceRect = priceTextObj.GetComponent<RectTransform>();

            // 가격 텍스트 위치 설정 (하단)
            priceRect.anchorMin = new Vector2(0, 0);
            priceRect.anchorMax = new Vector2(1, 0.2f);
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = Vector2.zero;

            // 가격 텍스트 스타일
            priceText.text = $"{ShopManager.Instance.GetItemPrice(item)} 골드";
            priceText.fontSize = 16;
            priceText.color = Color.white;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontStyle = TMPro.FontStyles.Bold;

            // 배경 추가 (가독성을 위해)
            var bgObj = new GameObject("PriceBackground");
            bgObj.transform.SetParent(overlay.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchoredPosition = priceRect.anchoredPosition;
            bgRect.sizeDelta = new Vector2(priceRect.sizeDelta.x + 10, priceRect.sizeDelta.y + 5);

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f); // 반투명 검은색 배경

            // 가격 텍스트를 배경 앞으로 이동
            priceTextObj.transform.SetAsLastSibling();

            // 구매 완료 상태 처리
            if (itemsPurchased != null && itemIndex < itemsPurchased.Length && itemsPurchased[itemIndex])
            {
                priceText.text = "구매 완료";
                priceText.color = Color.green;
                bgImage.color = new Color(0, 0.5f, 0, 0.8f); // 초록색 배경

                // 전체 아이템에 회색 오버레이 추가
                var soldOutOverlay = new GameObject("SoldOutOverlay");
                soldOutOverlay.transform.SetParent(overlay.transform, false);

                var soldOutRect = soldOutOverlay.AddComponent<RectTransform>();
                soldOutRect.anchorMin = Vector2.zero;
                soldOutRect.anchorMax = Vector2.one;
                soldOutRect.offsetMin = Vector2.zero;
                soldOutRect.offsetMax = Vector2.zero;

                var soldOutImage = soldOutOverlay.AddComponent<Image>();
                soldOutImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 반투명 회색
            }
        }

        /// <summary>
        /// 서비스 아이템 슬롯 업데이트 (기존 방식)
        /// </summary>
        private void UpdateServiceItemSlot(Transform slot, ShopItem item, int itemIndex)
        {
            // 서비스들은 기존 슬롯 UI 사용
            var nameText = slot.Find("ServiceName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = item.itemName;
            }

            var descText = slot.Find("ServiceDescription")?.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = item.itemDescription;
            }

            // 가격 업데이트
            var priceText = slot.Find("ItemPrice")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                int finalPrice = ShopManager.Instance.GetItemPrice(item);
                priceText.text = $"{finalPrice} 골드";
            }

            // 버튼 상태 업데이트
            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = ShopManager.Instance.CanPurchaseItem(itemIndex);
            }
        }

        /// <summary>
        /// 카드 UI 정보 업데이트 (BattleUIManager와 동일)
        /// </summary>
        private void UpdateCardUIInfo(GameObject cardUI, Card card)
        {
            var nameText = cardUI.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = card.CardName;
            }

            var costText = cardUI.transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                costText.text = $"{card.CurrentManaCost}";
            }

            var descText = cardUI.transform.Find("CardDescription")?.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                descText.text = card.Description;
            }

            var infoText = cardUI.transform.Find("CardInfo")?.GetComponent<TextMeshProUGUI>();
            if (infoText != null)
            {
                infoText.text = $"{card.Element} | {card.Type}";
            }
        }

        /// <summary>
        /// 유물 UI 정보 업데이트 (BattleUIManager와 동일)
        /// </summary>
        private void UpdateRelicUIInfo(GameObject relicUI, RelicSO relicData)
        {
            if (relicUI == null || relicData == null) return;

            // RelicImage 업데이트
            var relicImage = relicUI.transform.Find("RelicPanel/RelicImage")?.GetComponent<Image>();
            if (relicImage != null && relicData.Image != null)
            {
                relicImage.sprite = relicData.Image;
                relicImage.color = Color.white;
            }

            // RelicDescription 업데이트
            var relicDesc = relicUI.transform.Find("RelicPanel/RelicDescription")?.GetComponent<TextMeshProUGUI>();
            if (relicDesc != null)
            {
                relicDesc.text = relicData.Description;
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
        /// 아이템 슬롯 클리어 (프리팹 인스턴스 제거)
        /// </summary>
        private void ClearItemSlot(Transform slot)
        {
            if (slot == null) return;

            // 프리팹 인스턴스들만 제거
            ClearItemSlotContent(slot);

            // 슬롯은 활성화 상태로 유지 (빈 슬롯으로 표시)
            slot.gameObject.SetActive(true);

            if (debugMode)
                Debug.Log($"[ShopUIManager] 슬롯 클리어: {slot.name} (활성화 상태 유지)");
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
            UpdateShopItems(); // 골드 변경 시 모든 상품 버튼 상태 업데이트
        }

        /// <summary>
        /// 체력 변경 이벤트 처리
        /// </summary>
        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            UpdatePlayerStatus();
            UpdateShopItems(); // 체력 회복 버튼 상태 업데이트
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