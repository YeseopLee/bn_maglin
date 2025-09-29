using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // FirstOrDefault를 위해 추가
using System.Reflection; // Reflection을 위해 추가
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
        [SerializeField] private GameObject cardHoverUIPrefab; // 카드 hover용 프리팹
        [SerializeField] private GameObject relicPrefab; // RelicPrefab (Tools > Generate Prefabs로 생성)

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
            // 프리팹 참조 검증
            ValidatePrefabReferences();

            // 초기 상태 업데이트 (지연 실행으로 다른 매니저들이 초기화된 후 실행)
            StartCoroutine(InitialUpdate());
        }

        /// <summary>
        /// 프리팹 참조 검증
        /// </summary>
        private void ValidatePrefabReferences()
        {
            if (cardUIPrefab == null)
            {
                Debug.LogWarning("[ShopUIManager] CardUIPrefab이 설정되지 않았습니다! Inspector에서 설정해주세요.");
            }

            if (cardHoverUIPrefab == null)
            {
                Debug.LogWarning("[ShopUIManager] CardHoverUIPrefab이 설정되지 않았습니다! Inspector에서 설정해주세요.");
            }

            if (relicPrefab == null)
            {
                Debug.LogWarning("[ShopUIManager] RelicPrefab이 설정되지 않았습니다! Tools > Generate Prefabs로 생성 후 Inspector에서 설정해주세요.");
            }
            else
            {
                // RelicPrefab에 필요한 컴포넌트들이 있는지 확인
                var relicUIComponent = relicPrefab.GetComponent<Maglin.Relics.RelicUI>();
                var imageComponent = relicPrefab.GetComponent<Image>();
                var buttonComponent = relicPrefab.GetComponent<Button>();

                if (relicUIComponent == null)
                {
                    Debug.LogWarning("[ShopUIManager] RelicPrefab에 RelicUI 컴포넌트가 없습니다!");
                }

                if (imageComponent == null)
                {
                    Debug.LogWarning("[ShopUIManager] RelicPrefab에 Image 컴포넌트가 없습니다!");
                }

                if (buttonComponent == null)
                {
                    Debug.LogWarning("[ShopUIManager] RelicPrefab에 Button 컴포넌트가 없습니다!");
                }

                if (relicUIComponent != null && imageComponent != null && buttonComponent != null)
                {
                    if (debugMode)
                        Debug.Log("[ShopUIManager] RelicPrefab 검증 완료 - 모든 필수 컴포넌트가 존재합니다.");
                }
            }
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

            // 서비스 슬롯들도 오버레이 버튼을 사용하므로 별도 버튼 설정 불필요
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
                goldText.text = $"{PlayerManager.Instance.CurrentGold}";
            }

            // 체력 업데이트
            if (healthText != null)
            {
                int currentHealth = PlayerManager.Instance.CurrentHealth;
                int maxHealth = PlayerManager.Instance.MaxHealth;
                healthText.text = $"{currentHealth}/{maxHealth}";

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
        /// 서비스 슬롯 업데이트 (카드 제거, 체력 회복)
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
            ShopItem cardRemovalItem = null;
            int cardRemovalIndex = -1;

            // 체력 회복 서비스 찾기
            ShopItem healthRestoreItem = null;
            int healthRestoreIndex = -1;

            for (int i = 0; i < currentItems.Length; i++)
            {
                if (currentItems[i].itemType == ShopItemType.CardRemoval)
                {
                    cardRemovalItem = currentItems[i];
                    cardRemovalIndex = i;
                }
                else if (currentItems[i].itemType == ShopItemType.HealthRestore)
                {
                    healthRestoreItem = currentItems[i];
                    healthRestoreIndex = i;
                }
            }

            // 카드 제거 슬롯 업데이트
            if (cardRemovalItem != null && cardRemovalSlot != null)
            {
                UpdateServiceSlotWithOverlay(cardRemovalSlot, cardRemovalItem, cardRemovalIndex);
            }

            // 체력 회복 슬롯 업데이트
            if (healthRestoreItem != null && healthRestoreSlot != null)
            {
                UpdateServiceSlotWithOverlay(healthRestoreSlot, healthRestoreItem, healthRestoreIndex);
            }
        }

        /// <summary>
        /// 서비스 슬롯을 오버레이 방식으로 업데이트
        /// </summary>
        private void UpdateServiceSlotWithOverlay(Transform slot, ShopItem item, int itemIndex)
        {
            if (slot == null || item == null) return;

            // 슬롯 활성화
            slot.gameObject.SetActive(true);

            // 기존 오버레이 정리
            ClearServiceSlotOverlays(slot);

            // 슬롯에 오버레이 생성 (다른 아이템들과 동일한 방식)
            CreateServiceSlotOverlay(slot, item, itemIndex);

            if (debugMode)
                Debug.Log($"[ShopUIManager] 서비스 슬롯 업데이트 완료: {item.itemName}");
        }

        /// <summary>
        /// 서비스 슬롯 오버레이 정리
        /// </summary>
        private void ClearServiceSlotOverlays(Transform slot)
        {
            // ShopOverlay 제거
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var child = slot.GetChild(i);
                if (child.name.Contains("ShopOverlay"))
                {
                    Destroy(child.gameObject);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 서비스 슬롯 오버레이 정리: {child.name}");
                }
            }
        }

        /// <summary>
        /// 서비스 슬롯 오버레이 생성 (가격 표시)
        /// </summary>
        private void CreateServiceSlotOverlay(Transform slot, ShopItem item, int itemIndex)
        {
            // 오버레이 패널 생성
            GameObject overlay = new GameObject("ShopOverlay");
            overlay.transform.SetParent(slot, false);

            var overlayRect = overlay.AddComponent<RectTransform>();

            // 슬롯 아래쪽에 위치시킴
            overlayRect.anchorMin = new Vector2(0, 0);
            overlayRect.anchorMax = new Vector2(1, 0);
            overlayRect.anchoredPosition = new Vector2(0, -25); // 슬롯 아래 25픽셀
            overlayRect.sizeDelta = new Vector2(0, 40); // 높이 40픽셀

            // 가격 배경 추가
            var bgImage = overlay.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 진한 반투명 배경

            // 가격 텍스트 추가
            GameObject priceTextObj = new GameObject("PriceText");
            priceTextObj.transform.SetParent(overlay.transform, false);

            var priceText = priceTextObj.AddComponent<TextMeshProUGUI>();
            var priceRect = priceTextObj.GetComponent<RectTransform>();

            // 가격 텍스트를 오버레이 전체에 맞춤
            priceRect.anchorMin = Vector2.zero;
            priceRect.anchorMax = Vector2.one;
            priceRect.offsetMin = new Vector2(5, 2);
            priceRect.offsetMax = new Vector2(-5, -2);

            // 가격 텍스트 스타일
            priceText.text = $"{ShopManager.Instance.GetItemPrice(item)} GOLD";
            priceText.fontSize = 14;
            priceText.color = Color.white;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontStyle = TMPro.FontStyles.Bold;

            // 구매 완료 상태 처리
            if (itemsPurchased != null && itemIndex < itemsPurchased.Length && itemsPurchased[itemIndex])
            {
                priceText.text = "PURCHASED";
                priceText.color = Color.white;
                bgImage.color = new Color(0, 0.6f, 0, 0.9f); // 초록색 배경
            }
            else
            {
                // 구매 가능한 경우에만 버튼 추가 (hover 효과와 클릭 기능)
                bool canPurchase = ShopManager.Instance.CanPurchaseItem(itemIndex);
                if (canPurchase)
                {
                    // 오버레이에 버튼 컴포넌트 추가 (클릭 및 hover 효과)
                    var overlayButton = overlay.AddComponent<Button>();
                    overlayButton.targetGraphic = bgImage;
                    overlayButton.onClick.AddListener(() => OnServiceSlotClicked(itemIndex));

                    // Hover 효과 설정
                    var colorBlock = overlayButton.colors;
                    colorBlock.normalColor = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 기본 색상
                    colorBlock.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 밝은 회색 (hover)
                    colorBlock.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f); // 어두운 색상 (클릭)
                    colorBlock.colorMultiplier = 1f;
                    overlayButton.colors = colorBlock;

                    overlayButton.transition = Selectable.Transition.ColorTint;
                }
                else
                {
                    // 구매 불가능한 경우 텍스트 색상만 변경
                    priceText.color = Color.red;
                    bgImage.color = new Color(0.4f, 0.1f, 0.1f, 0.9f); // 붉은 배경
                }
            }

            // 오버레이를 맨 위로
            overlay.transform.SetAsLastSibling();

            if (debugMode)
                Debug.Log($"[ShopUIManager] 서비스 슬롯 오버레이 생성 완료: {item.itemName}");
        }

        /// <summary>
        /// 서비스 슬롯 클릭 이벤트 처리 (오버레이 버튼용)
        /// </summary>
        private void OnServiceSlotClicked(int itemIndex)
        {
            if (debugMode)
                Debug.Log($"[ShopUIManager] 서비스 슬롯 클릭: 아이템 인덱스 {itemIndex}");

            if (ShopManager.Instance != null)
            {
                // 구매 가능 여부 확인 후 구매 시도
                bool canPurchase = ShopManager.Instance.CanPurchaseItem(itemIndex);
                if (canPurchase)
                {
                    ShopManager.Instance.TryPurchaseItem(itemIndex);
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 서비스 아이템 {itemIndex} 구매 불가");
                }
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
                    // 서비스 아이템은 별도 슬롯에서 처리
                    break;
            }

            if (itemUI != null && debugMode)
            {
                Debug.Log($"[ShopUIManager] 상점 아이템 UI 생성 완료: {item.itemType} - {item.itemName}");
            }
        }

        /// <summary>
        /// 아이템 슬롯 내용 정리 (프리팹 인스턴스와 오버레이 모두)
        /// </summary>
        private void ClearItemSlotContent(Transform slot)
        {
            // CardUIPrefab(Clone), RelicPrefab(Clone), ShopOverlay 등 모든 동적 요소 제거
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var child = slot.GetChild(i);
                if (child.name.Contains("Prefab") ||
                    child.name.Contains("Clone") ||
                    child.name.Contains("ShopOverlay"))
                {
                    Destroy(child.gameObject);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 슬롯 내용 정리: {child.name}");
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

            // CardUI 컴포넌트 추가 (hover 기능을 위해)
            var cardUIComponent = cardUI.GetComponent<Maglin.UI.CardUI>();
            if (cardUIComponent == null)
            {
                cardUIComponent = cardUI.AddComponent<Maglin.UI.CardUI>();
            }

            // CardUI 컴포넌트에 카드 데이터 설정 (hover 시 참조할 수 있도록)
            // Reflection을 사용하여 private 필드에 직접 접근
            var associatedCardField = typeof(Maglin.UI.CardUI).GetField("associatedCard",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            associatedCardField?.SetValue(cardUIComponent, cardInstance);

            // CanvasGroup 컴포넌트 추가 (CardUI에서 필요)
            var canvasGroup = cardUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = cardUI.AddComponent<CanvasGroup>();
            }

            // 카드 정보 업데이트
            UpdateCardUIInfo(cardUI, cardInstance);

            // 카드 버튼은 툴팁 전용으로 설정 (구매는 overlay에서)
            var button = cardUI.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                // 구매 기능 제거, 툴팁만 작동하도록 설정
                button.interactable = true;
                button.transition = Selectable.Transition.None;
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

            if (debugMode)
                Debug.Log($"[ShopUIManager] RelicPrefab 인스턴스 생성: {relicUI.name}");

            // RelicUI 컴포넌트를 통해 유물 데이터 설정
            var relicUIComponent = relicUI.GetComponent<Maglin.Relics.RelicUI>();
            if (relicUIComponent != null)
            {
                // 먼저 컴포넌트 참조들을 수동으로 설정
                SetupRelicUIReferences(relicUIComponent, relicUI);

                // 그 다음 데이터 설정
                relicUIComponent.SetRelicData(item.relicData);

                if (debugMode)
                    Debug.Log($"[ShopUIManager] RelicUI 컴포넌트를 통해 데이터 설정: {item.relicData.RelicName}");
            }
            else
            {
                // RelicUI 컴포넌트가 없으면 기존 방식으로 업데이트
                if (debugMode)
                    Debug.LogWarning("[ShopUIManager] RelicUI 컴포넌트를 찾을 수 없습니다. 기존 방식으로 업데이트합니다.");
                UpdateRelicUIInfo(relicUI, item.relicData);
            }

            // 상점 구매 버튼 이벤트 설정
            SetupRelicShopButton(relicUI, itemIndex);

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
                rectTransform.localScale = Vector3.one; // 원본 크기 유지
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] 유물 상점 UI 생성 완료: {item.relicData.RelicName}");

            return relicUI;
        }

        /// <summary>
        /// 유물 상점 버튼 설정 (툴팁 전용, 구매는 overlay에서)
        /// </summary>
        private void SetupRelicShopButton(GameObject relicUI, int itemIndex)
        {
            // RelicUI의 기본 Button 컴포넌트 사용 (툴팁 전용)
            var mainButton = relicUI.GetComponent<Button>();
            if (mainButton != null)
            {
                mainButton.onClick.RemoveAllListeners();
                // 구매 기능 제거, 툴팁만 작동하도록 설정
                mainButton.interactable = true;
                mainButton.transition = Selectable.Transition.None;

                if (debugMode)
                    Debug.Log($"[ShopUIManager] 메인 버튼 설정 완료 (툴팁 전용, 인덱스: {itemIndex})");
            }
            else
            {
                // 메인 Button이 없으면 새로 추가 (툴팁 전용)
                mainButton = relicUI.AddComponent<Button>();
                var image = relicUI.GetComponent<Image>();
                if (image != null)
                {
                    mainButton.targetGraphic = image;
                }

                mainButton.interactable = true; // 툴팁을 위해 항상 활성화
                mainButton.transition = Selectable.Transition.None;

                if (debugMode)
                    Debug.Log($"[ShopUIManager] 새 메인 버튼 추가 완료 (툴팁 전용, 인덱스: {itemIndex})");
            }

            // RelicUI 컴포넌트의 툴팁 활성화 확인
            var relicUIComponent = relicUI.GetComponent<Maglin.Relics.RelicUI>();
            if (relicUIComponent != null)
            {
                // 툴팁 Canvas 참조 설정 확인
                EnsureTooltipCanvasReference(relicUIComponent, relicUI);

                if (debugMode)
                    Debug.Log("[ShopUIManager] RelicUI 컴포넌트 툴팁 설정 확인 완료");
            }
        }

        /// <summary>
        /// RelicUI 컴포넌트의 모든 참조 설정
        /// </summary>
        private void SetupRelicUIReferences(Maglin.Relics.RelicUI relicUIComponent, GameObject relicUI)
        {
            if (debugMode)
                Debug.Log($"[ShopUIManager] RelicUI 참조 설정 시작: {relicUI.name}");

            var relicUIType = typeof(Maglin.Relics.RelicUI);

            // relicImage 필드 설정
            var relicImageField = relicUIType.GetField("relicImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (relicImageField != null)
            {
                var mainImage = relicUI.GetComponent<Image>();
                if (mainImage != null)
                {
                    relicImageField.SetValue(relicUIComponent, mainImage);
                    if (debugMode)
                        Debug.Log("[ShopUIManager] RelicImage 참조 설정 완료");
                }
                else if (debugMode)
                {
                    Debug.LogWarning("[ShopUIManager] RelicUI에서 Image 컴포넌트를 찾을 수 없습니다");
                }
            }

            // tooltipCanvas 찾기 및 설정
            var tooltipCanvasField = relicUIType.GetField("tooltipCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tooltipCanvasField != null)
            {
                var tooltipCanvas = relicUI.GetComponentInChildren<Canvas>();
                if (tooltipCanvas != null)
                {
                    tooltipCanvasField.SetValue(relicUIComponent, tooltipCanvas);

                    // 툴팁이 제대로 작동하도록 Canvas 활성화
                    tooltipCanvas.gameObject.SetActive(true);
                    tooltipCanvas.enabled = false; // 기본적으로는 숨겨진 상태

                    if (debugMode)
                        Debug.Log($"[ShopUIManager] TooltipCanvas 참조 설정 및 활성화 완료: {tooltipCanvas.gameObject.name}");
                }
                else if (debugMode)
                {
                    Debug.LogWarning("[ShopUIManager] RelicUI에서 Canvas 컴포넌트를 찾을 수 없습니다");
                }
            }

            // 텍스트 컴포넌트들 찾기 및 설정
            var allTexts = relicUI.GetComponentsInChildren<TextMeshProUGUI>(true); // includeInactive = true

            if (debugMode)
                Debug.Log($"[ShopUIManager] 발견된 TextMeshProUGUI 컴포넌트 수: {allTexts.Length}");

            var nameField = relicUIType.GetField("relicNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = relicUIType.GetField("relicDescriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var text in allTexts)
            {
                if (debugMode)
                    Debug.Log($"[ShopUIManager] 텍스트 컴포넌트 발견: {text.gameObject.name} - {text.text}");

                if (text.gameObject.name.Contains("Name") && nameField != null)
                {
                    nameField.SetValue(relicUIComponent, text);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] RelicNameText 참조 설정 완료: {text.gameObject.name}");
                }
                else if (text.gameObject.name.Contains("Description") && descField != null)
                {
                    descField.SetValue(relicUIComponent, text);
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] RelicDescriptionText 참조 설정 완료: {text.gameObject.name}");
                }
            }

            // RelicUI 컴포넌트가 IPointerEnterHandler, IPointerExitHandler를 구현하는지 확인
            if (relicUIComponent is UnityEngine.EventSystems.IPointerEnterHandler &&
                relicUIComponent is UnityEngine.EventSystems.IPointerExitHandler)
            {
                if (debugMode)
                    Debug.Log("[ShopUIManager] RelicUI가 마우스 이벤트 인터페이스를 구현하고 있습니다");
            }
            else if (debugMode)
            {
                Debug.LogWarning("[ShopUIManager] RelicUI가 마우스 이벤트 인터페이스를 구현하지 않습니다");
            }
        }

        /// <summary>
        /// 툴팁 Canvas 참조가 올바르게 설정되어 있는지 확인
        /// </summary>
        private void EnsureTooltipCanvasReference(Maglin.Relics.RelicUI relicUIComponent, GameObject relicUI)
        {
            // 리플렉션을 사용하여 private 필드에 접근
            var relicUIType = typeof(Maglin.Relics.RelicUI);

            // tooltipCanvas 필드 확인
            var tooltipCanvasField = relicUIType.GetField("tooltipCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tooltipCanvasField != null)
            {
                var currentCanvas = tooltipCanvasField.GetValue(relicUIComponent) as Canvas;
                if (currentCanvas == null)
                {
                    // Canvas 참조가 없으면 찾아서 설정
                    var foundCanvas = relicUI.GetComponentInChildren<Canvas>();
                    if (foundCanvas != null)
                    {
                        tooltipCanvasField.SetValue(relicUIComponent, foundCanvas);
                        if (debugMode)
                            Debug.Log("[ShopUIManager] 툴팁 Canvas 참조 자동 설정 완료");
                    }
                }
            }

            // 텍스트 컴포넌트들 참조 확인
            var nameField = relicUIType.GetField("relicNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = relicUIType.GetField("relicDescriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (nameField != null && nameField.GetValue(relicUIComponent) == null)
            {
                var nameText = relicUI.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.gameObject.name.Contains("Name"));
                if (nameText != null)
                {
                    nameField.SetValue(relicUIComponent, nameText);
                    if (debugMode)
                        Debug.Log("[ShopUIManager] 유물 이름 텍스트 참조 자동 설정 완료");
                }
            }

            if (descField != null && descField.GetValue(relicUIComponent) == null)
            {
                var descText = relicUI.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.gameObject.name.Contains("Description"));
                if (descText != null)
                {
                    descField.SetValue(relicUIComponent, descText);
                    if (debugMode)
                        Debug.Log("[ShopUIManager] 유물 설명 텍스트 참조 자동 설정 완료");
                }
            }
        }

        /// <summary>
        /// 상점 아이템 오버레이 생성 (가격, 구매 완료 표시) - 프리팹 아래쪽에 표시
        /// </summary>
        private void CreateShopItemOverlay(GameObject itemUI, ShopItem item, int itemIndex)
        {
            // 아이템UI의 부모 슬롯을 가져옴
            var parentSlot = itemUI.transform.parent;
            if (parentSlot == null) return;

            // 오버레이 패널을 슬롯의 자식으로 생성 (아이템UI와 같은 레벨)
            GameObject overlay = new GameObject("ShopOverlay");
            overlay.transform.SetParent(parentSlot, false);

            var overlayRect = overlay.AddComponent<RectTransform>();

            // 슬롯 크기를 기준으로 아래쪽에 위치시킴
            overlayRect.anchorMin = new Vector2(0, 0);
            overlayRect.anchorMax = new Vector2(1, 0);
            overlayRect.anchoredPosition = new Vector2(0, -25); // 슬롯 아래 25픽셀
            overlayRect.sizeDelta = new Vector2(0, 40); // 높이 40픽셀

            // 가격 배경 추가
            var bgImage = overlay.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 진한 반투명 배경

            // 가격 텍스트 추가
            GameObject priceTextObj = new GameObject("PriceText");
            priceTextObj.transform.SetParent(overlay.transform, false);

            var priceText = priceTextObj.AddComponent<TextMeshProUGUI>();
            var priceRect = priceTextObj.GetComponent<RectTransform>();

            // 가격 텍스트를 오버레이 전체에 맞춤
            priceRect.anchorMin = Vector2.zero;
            priceRect.anchorMax = Vector2.one;
            priceRect.offsetMin = new Vector2(5, 2);
            priceRect.offsetMax = new Vector2(-5, -2);

            // 가격 텍스트 스타일
            priceText.text = $"{ShopManager.Instance.GetItemPrice(item)} GOLD";
            priceText.fontSize = 14;
            priceText.color = Color.white;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontStyle = TMPro.FontStyles.Bold;

            // 구매 완료 상태 처리
            if (itemsPurchased != null && itemIndex < itemsPurchased.Length && itemsPurchased[itemIndex])
            {
                priceText.text = "PURCHASED";
                priceText.color = Color.white;
                bgImage.color = new Color(0, 0.6f, 0, 0.9f); // 초록색 배경

                // 아이템UI에 회색 오버레이 추가 (가독성 저하 방지를 위해)
                var soldOutOverlay = new GameObject("SoldOutOverlay");
                soldOutOverlay.transform.SetParent(itemUI.transform, false);

                var soldOutRect = soldOutOverlay.AddComponent<RectTransform>();
                soldOutRect.anchorMin = Vector2.zero;
                soldOutRect.anchorMax = Vector2.one;
                soldOutRect.offsetMin = Vector2.zero;
                soldOutRect.offsetMax = Vector2.zero;

                var soldOutImage = soldOutOverlay.AddComponent<Image>();
                soldOutImage.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // 약한 반투명 회색 (가독성 유지)
            }
            else
            {
                // 구매 가능한 경우에만 버튼 추가 (hover 효과와 클릭 기능)
                bool canPurchase = ShopManager.Instance.CanPurchaseItem(itemIndex);
                if (canPurchase)
                {
                    // 오버레이에 버튼 컴포넌트 추가 (클릭 및 hover 효과)
                    var overlayButton = overlay.AddComponent<Button>();
                    overlayButton.targetGraphic = bgImage;
                    overlayButton.onClick.AddListener(() => OnItemSlotClicked(itemIndex));

                    // Hover 효과 설정
                    var colorBlock = overlayButton.colors;
                    colorBlock.normalColor = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 기본 색상
                    colorBlock.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 밝은 회색 (hover)
                    colorBlock.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f); // 어두운 색상 (클릭)
                    colorBlock.colorMultiplier = 1f;
                    overlayButton.colors = colorBlock;

                    // 커서 모양 변경을 위한 설정
                    overlayButton.transition = Selectable.Transition.ColorTint;
                }
                else
                {
                    // 구매 불가능한 경우 텍스트 색상만 변경 (투명도 조절 제거)
                    priceText.color = Color.red;
                    bgImage.color = new Color(0.4f, 0.1f, 0.1f, 0.9f); // 붉은 배경
                }
            }

            // 오버레이를 맨 위로 (다른 UI 요소들보다 위에 표시)
            overlay.transform.SetAsLastSibling();

            if (debugMode)
                Debug.Log($"[ShopUIManager] 상점 오버레이 생성 완료: {item.itemName} (아래쪽 위치)");
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
                priceText.text = $"{finalPrice} GOLD";
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
            // 카드 일러스트 이미지 설정
            var cardImage = cardUI.transform.Find("CardIllustration")?.GetComponent<Image>();
            if (cardImage != null && card.CardData.Image != null)
            {
                cardImage.sprite = card.CardData.Image;
            }

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

            var damageText = cardUI.transform.Find("CardDamage")?.GetComponent<TextMeshProUGUI>();
            if (damageText != null)
            {
                damageText.text = card.CurrentDamage.ToString();
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
        /// 유물 UI 정보 업데이트 (RelicPrefab 구조에 맞게)
        /// </summary>
        private void UpdateRelicUIInfo(GameObject relicUI, RelicSO relicData)
        {
            if (relicUI == null || relicData == null) return;

            if (debugMode)
                Debug.Log($"[ShopUIManager] UpdateRelicUIInfo 호출: {relicData.RelicName}");

            // 메인 Image 컴포넌트 업데이트 (RelicPrefab의 루트에 있는 Image)
            var relicImage = relicUI.GetComponent<Image>();
            if (relicImage != null && relicData.Image != null)
            {
                relicImage.sprite = relicData.Image;
                relicImage.color = Color.white;
                relicImage.preserveAspect = true;

                if (debugMode)
                    Debug.Log($"[ShopUIManager] 유물 이미지 설정 완료: {relicData.Image.name}");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[ShopUIManager] 유물 이미지 설정 실패 - Image컴포넌트: {relicImage != null}, 데이터이미지: {relicData.Image != null}");
            }

            // 툴팁 텍스트들 직접 찾아서 업데이트 (RelicPrefab 구조에 맞게)
            var tooltipTexts = relicUI.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in tooltipTexts)
            {
                if (text.gameObject.name.Contains("Name"))
                {
                    text.text = relicData.RelicName;
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 유물 이름 설정: {relicData.RelicName}");
                }
                else if (text.gameObject.name.Contains("Description"))
                {
                    string description = relicData.Description;

                    // 효과값 정보 추가
                    if (relicData.EffectType != RelicEffectType.CustomEffect)
                    {
                        string effectInfo = relicData.GetEffectValueString();
                        description += $"\n\n<color=yellow>효과: {effectInfo}</color>";
                    }

                    text.text = description;
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 유물 설명 설정 완료");
                }
            }

            if (debugMode)
                Debug.Log($"[ShopUIManager] UpdateRelicUIInfo 완료: {relicData.RelicName}");
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
                priceText.text = $"{finalPrice} GOLD";
            }
        }

        /// <summary>
        /// 아이템 슬롯 클리어 (프리팹 인스턴스와 오버레이 제거)
        /// </summary>
        private void ClearItemSlot(Transform slot)
        {
            if (slot == null) return;

            // 프리팹 인스턴스들과 오버레이 모두 제거
            ClearItemSlotContent(slot);

            // 슬롯은 활성화 상태로 유지 (빈 슬롯으로 표시)
            slot.gameObject.SetActive(true);

            if (debugMode)
                Debug.Log($"[ShopUIManager] 슬롯 클리어 완료: {slot.name} (활성화 상태 유지)");
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
                // 구매 가능 여부 확인 후 구매 시도
                bool canPurchase = ShopManager.Instance.CanPurchaseItem(slotIndex);
                if (canPurchase)
                {
                    ShopManager.Instance.TryPurchaseItem(slotIndex);
                }
                else
                {
                    // 구매 불가능한 경우 사용자에게 피드백 제공
                    if (debugMode)
                        Debug.Log($"[ShopUIManager] 아이템 {slotIndex} 구매 불가 - 골드 부족 또는 기타 조건 미충족");

                    // 여기에 UI 피드백 추가 가능 (예: 소리, 텍스트 메시지 등)
                }
            }
        }

        // 카드 제거 및 체력 회복 슬롯 클릭 이벤트는 이제 OnServiceSlotClicked로 처리됨

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

                // 카드 데이터 설정 (CardUIData)
                var cardUIData = cardUI.GetComponent<CardUIData>();
                if (cardUIData == null)
                {
                    cardUIData = cardUI.AddComponent<CardUIData>();
                }

                // Card 인스턴스 생성 (카드 제거용)
                var cardInstance = new Card(cardData);
                cardUIData.CardInstance = cardInstance;

                // CardUI 컴포넌트 추가 (hover 기능을 위해)
                var cardUIComponent = cardUI.GetComponent<Maglin.UI.CardUI>();
                if (cardUIComponent == null)
                {
                    cardUIComponent = cardUI.AddComponent<Maglin.UI.CardUI>();
                }

                // CardUI 컴포넌트에 카드 데이터 설정 (hover 시 참조할 수 있도록)
                var associatedCardField = typeof(Maglin.UI.CardUI).GetField("associatedCard",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                associatedCardField?.SetValue(cardUIComponent, cardInstance);

                // CanvasGroup 컴포넌트 추가 (CardUI에서 필요)
                var canvasGroup = cardUI.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = cardUI.AddComponent<CanvasGroup>();
                }

                // 카드 정보 업데이트
                UpdateCardUIInfo(cardUI, cardInstance);

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