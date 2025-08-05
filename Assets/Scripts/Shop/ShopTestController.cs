using UnityEngine;
using Maglin.Player;
using Maglin.Cards;

namespace Maglin.Shop
{
    /// <summary>
    /// 상점 테스트를 위한 컨트롤러
    /// </summary>
    public class ShopTestController : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private int testFloor = 1;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private void Start()
        {
            // 약간의 지연 후 상점 진입 (다른 매니저들이 초기화될 시간을 줌)
            Invoke(nameof(EnterShopForTest), 0.5f);
        }

        /// <summary>
        /// 테스트용 상점 진입
        /// </summary>
        private void EnterShopForTest()
        {
            if (ShopManager.Instance == null)
            {
                Debug.LogError("[ShopTestController] ShopManager.Instance가 null입니다!");
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[ShopTestController] {testFloor}층 상점 진입");
                LogCurrentStatus(); // 시작 시 현재 상태 출력
            }

            ShopManager.Instance.EnterShop(testFloor);
        }

        /// <summary>
        /// 테스트 골드 추가 (UI 버튼용)
        /// </summary>
        public void AddTestGold()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.AddGold(100);
                if (debugMode)
                    Debug.Log("[ShopTestController] 테스트 골드 100 추가");
            }
        }

        /// <summary>
        /// 상점 새로고침 (UI 버튼용)
        /// </summary>
        public void RefreshShop()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.RefreshShop();
                if (debugMode)
                    Debug.Log("[ShopTestController] 상점 새로고침");
            }
        }

        /// <summary>
        /// 현재 상태 로그 출력 (UI 버튼용)
        /// </summary>
        public void LogCurrentStatus()
        {
            Debug.Log($"[ShopTestController] 현재 상태:");

            if (PlayerManager.Instance != null)
            {
                Debug.Log($"  - 골드: {PlayerManager.Instance.CurrentGold}");
                Debug.Log($"  - 체력: {PlayerManager.Instance.CurrentHealth}/{PlayerManager.Instance.MaxHealth}");
            }
            else
            {
                Debug.LogWarning("  - PlayerManager.Instance가 null입니다!");
            }

            if (CardManager.Instance != null)
            {
                Debug.Log($"  - 덱 카드 수: {CardManager.Instance.DeckCount}");
                var deckCards = CardManager.Instance.GetDeckCards();
                if (deckCards.Count > 0)
                {
                    foreach (var card in deckCards)
                    {
                        Debug.Log($"    * {card.CardName}");
                    }
                }
                else
                {
                    Debug.Log("    * 덱에 카드가 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning("  - CardManager.Instance가 null입니다!");
            }

            if (ShopManager.Instance != null && ShopManager.Instance.CurrentItems != null)
            {
                Debug.Log($"  - 상점 아이템 수: {ShopManager.Instance.CurrentItems.Length}");
                for (int i = 0; i < ShopManager.Instance.CurrentItems.Length; i++)
                {
                    var item = ShopManager.Instance.CurrentItems[i];
                    Debug.Log($"    {i}: {item.itemName} ({item.itemType}) - {ShopManager.Instance.GetItemPrice(item)} 골드");
                }
            }
            else
            {
                Debug.LogWarning("  - ShopManager.Instance 또는 CurrentItems가 null입니다!");
            }
        }
    }
}