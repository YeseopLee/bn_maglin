using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Shop;
using Maglin.Player;

namespace Maglin.UI
{
    /// <summary>
    /// 상점 UI를 관리하는 클래스
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("UI 패널")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Button exitButton;

        [Header("상점 정보")]
        [SerializeField] private TextMeshProUGUI shopNameText;
        [SerializeField] private TextMeshProUGUI playerGoldText;

        [Header("아이템 그리드")]
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private GameObject shopItemPrefab;
        [SerializeField] private int maxItemsPerRow = 3;

        [Header("확인 다이얼로그")]
        [SerializeField] private GameObject confirmDialog;
        [SerializeField] private TextMeshProUGUI confirmText;
        [SerializeField] private Button confirmBuyButton;
        [SerializeField] private Button confirmCancelButton;

        // 현재 상태
        private List<ShopItemUI> shopItemUIs = new List<ShopItemUI>();
        private ShopItem pendingPurchaseItem;
        private int pendingPurchaseIndex;

        private void Awake()
        {
            // 버튼 이벤트 설정
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitButtonClicked);

            if (confirmBuyButton != null)
                confirmBuyButton.onClick.AddListener(OnConfirmPurchase);

            if (confirmCancelButton != null)
                confirmCancelButton.onClick.AddListener(OnCancelPurchase);

            // 초기 상태 설정
            if (shopPanel != null)
                shopPanel.SetActive(false);

            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        private void OnEnable()
        {
            // ShopManager 이벤트 구독
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnShopEntered += OnShopEntered;
                ShopManager.Instance.OnShopExited += OnShopExited;
                ShopManager.Instance.OnShopRefreshed += OnShopRefreshed;
                ShopManager.Instance.OnItemPurchased += OnItemPurchased;
            }

            // PlayerManager 이벤트 구독
            PlayerManager.OnGoldChanged += OnGoldChanged;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnShopEntered -= OnShopEntered;
                ShopManager.Instance.OnShopExited -= OnShopExited;
                ShopManager.Instance.OnShopRefreshed -= OnShopRefreshed;
                ShopManager.Instance.OnItemPurchased -= OnItemPurchased;
            }

            PlayerManager.OnGoldChanged -= OnGoldChanged;
        }

        /// <summary>
        /// 상점 진입 시 호출
        /// </summary>
        private void OnShopEntered()
        {
            ShowShop();
        }

        /// <summary>
        /// 상점 퇴장 시 호출
        /// </summary>
        private void OnShopExited()
        {
            HideShop();
        }

        /// <summary>
        /// 상점 새로고침 시 호출
        /// </summary>
        private void OnShopRefreshed()
        {
            UpdateShopDisplay();
        }

        /// <summary>
        /// 아이템 구매 시 호출
        /// </summary>
        private void OnItemPurchased(ShopItem item, int price)
        {
            UpdateShopDisplay();
            UpdatePlayerGoldDisplay();
        }

        /// <summary>
        /// 골드 변경 시 호출
        /// </summary>
        private void OnGoldChanged(int newGold)
        {
            UpdatePlayerGoldDisplay();
            UpdateItemAffordability();
        }

        /// <summary>
        /// 상점 UI 표시
        /// </summary>
        public void ShowShop()
        {
            if (shopPanel != null)
                shopPanel.SetActive(true);

            UpdateShopDisplay();
            UpdatePlayerGoldDisplay();
        }

        /// <summary>
        /// 상점 UI 숨기기
        /// </summary>
        public void HideShop()
        {
            if (shopPanel != null)
                shopPanel.SetActive(false);

            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        /// <summary>
        /// 상점 표시 내용 업데이트
        /// </summary>
        private void UpdateShopDisplay()
        {
            if (ShopManager.Instance == null) return;

            // 상점 이름 업데이트
            if (shopNameText != null && ShopManager.Instance.CurrentShop != null)
            {
                shopNameText.text = ShopManager.Instance.CurrentShop.ShopName;
            }

            // 기존 아이템 UI 제거
            ClearItemUIs();

            // 새 아이템 UI 생성
            CreateItemUIs();
        }

        /// <summary>
        /// 기존 아이템 UI 제거
        /// </summary>
        private void ClearItemUIs()
        {
            foreach (var itemUI in shopItemUIs)
            {
                if (itemUI != null && itemUI.gameObject != null)
                    Destroy(itemUI.gameObject);
            }
            shopItemUIs.Clear();
        }

        /// <summary>
        /// 아이템 UI 생성
        /// </summary>
        private void CreateItemUIs()
        {
            if (ShopManager.Instance == null || itemsContainer == null || shopItemPrefab == null)
                return;

            var items = ShopManager.Instance.CurrentItems;
            var purchased = ShopManager.Instance.ItemsPurchased;

            for (int i = 0; i < items.Length; i++)
            {
                CreateItemUI(items[i], i, purchased[i]);
            }
        }

        /// <summary>
        /// 개별 아이템 UI 생성
        /// </summary>
        private void CreateItemUI(ShopItem item, int index, bool isPurchased)
        {
            GameObject itemGO = Instantiate(shopItemPrefab, itemsContainer);
            ShopItemUI itemUI = itemGO.GetComponent<ShopItemUI>();

            if (itemUI == null)
            {
                itemUI = itemGO.AddComponent<ShopItemUI>();
            }

            // 아이템 UI 설정
            itemUI.Setup(item, index, isPurchased);
            itemUI.OnItemClicked += OnItemClicked;

            // 구매 가능 여부 설정
            bool canAfford = ShopManager.Instance.CanPurchaseItem(index);
            itemUI.SetAffordable(canAfford);

            shopItemUIs.Add(itemUI);
        }

        /// <summary>
        /// 아이템 클릭 처리
        /// </summary>
        private void OnItemClicked(ShopItemUI itemUI, int index)
        {
            if (ShopManager.Instance == null) return;

            // 구매 가능 여부 확인
            if (!ShopManager.Instance.CanPurchaseItem(index))
                return;

            var items = ShopManager.Instance.CurrentItems;
            if (index < 0 || index >= items.Length)
                return;

            // 구매 확인 다이얼로그 표시
            ShowPurchaseConfirmation(items[index], index);
        }

        /// <summary>
        /// 구매 확인 다이얼로그 표시
        /// </summary>
        private void ShowPurchaseConfirmation(ShopItem item, int index)
        {
            if (confirmDialog == null || confirmText == null) return;

            pendingPurchaseItem = item;
            pendingPurchaseIndex = index;

            int price = ShopManager.Instance.GetItemPrice(item);
            confirmText.text = $"{item.itemName}\n{item.itemDescription}\n\n가격: {price} 골드\n\n구매하시겠습니까?";

            confirmDialog.SetActive(true);
        }

        /// <summary>
        /// 구매 확정
        /// </summary>
        private void OnConfirmPurchase()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.TryPurchaseItem(pendingPurchaseIndex);
            }

            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        /// <summary>
        /// 구매 취소
        /// </summary>
        private void OnCancelPurchase()
        {
            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        /// <summary>
        /// 플레이어 골드 표시 업데이트
        /// </summary>
        private void UpdatePlayerGoldDisplay()
        {
            if (playerGoldText != null && PlayerManager.Instance != null)
            {
                playerGoldText.text = $"골드: {PlayerManager.Instance.CurrentGold}";
            }
        }

        /// <summary>
        /// 아이템 구매 가능 여부 업데이트
        /// </summary>
        private void UpdateItemAffordability()
        {
            if (ShopManager.Instance == null) return;

            for (int i = 0; i < shopItemUIs.Count; i++)
            {
                if (shopItemUIs[i] != null)
                {
                    bool canAfford = ShopManager.Instance.CanPurchaseItem(i);
                    shopItemUIs[i].SetAffordable(canAfford);
                }
            }
        }

        /// <summary>
        /// 나가기 버튼 클릭
        /// </summary>
        private void OnExitButtonClicked()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.ExitShop();
            }
        }
    }

    /// <summary>
    /// 개별 상점 아이템 UI 클래스
    /// </summary>
    public class ShopItemUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI itemPriceText;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private GameObject soldOutOverlay;

        [Header("색상 설정")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = Color.gray;

        // 이벤트
        public System.Action<ShopItemUI, int> OnItemClicked;

        // 현재 상태
        private ShopItem currentItem;
        private int itemIndex;
        private bool isPurchased;

        private void Awake()
        {
            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        }

        /// <summary>
        /// 아이템 UI 설정
        /// </summary>
        public void Setup(ShopItem item, int index, bool purchased)
        {
            currentItem = item;
            itemIndex = index;
            isPurchased = purchased;

            UpdateDisplay();
        }

        /// <summary>
        /// 화면 표시 업데이트
        /// </summary>
        private void UpdateDisplay()
        {
            if (currentItem == null) return;

            // 아이템 정보 표시
            if (itemNameText != null)
                itemNameText.text = currentItem.itemName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = currentItem.itemDescription;

            // 가격 표시
            if (itemPriceText != null && ShopManager.Instance != null)
            {
                int price = ShopManager.Instance.GetItemPrice(currentItem);
                itemPriceText.text = $"{price} 골드";
            }

            // 아이콘 표시
            if (itemIcon != null && currentItem.itemIcon != null)
            {
                itemIcon.sprite = currentItem.itemIcon;
                itemIcon.gameObject.SetActive(true);
            }
            else if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(false);
            }

            // 구매 완료 상태 표시
            if (soldOutOverlay != null)
                soldOutOverlay.SetActive(isPurchased);

            if (purchaseButton != null)
                purchaseButton.interactable = !isPurchased;
        }

        /// <summary>
        /// 구매 가능 여부 설정
        /// </summary>
        public void SetAffordable(bool affordable)
        {
            if (purchaseButton != null && !isPurchased)
            {
                purchaseButton.interactable = affordable;

                // 색상 변경
                var colors = purchaseButton.colors;
                colors.normalColor = affordable ? affordableColor : unaffordableColor;
                purchaseButton.colors = colors;
            }
        }

        /// <summary>
        /// 구매 버튼 클릭 처리
        /// </summary>
        private void OnPurchaseButtonClicked()
        {
            OnItemClicked?.Invoke(this, itemIndex);
        }
    }
}