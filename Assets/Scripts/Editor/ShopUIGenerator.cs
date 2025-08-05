using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Maglin.Editor
{
    /// <summary>
    /// 상점 UI를 자동 생성하는 에디터 도구
    /// </summary>
    public class ShopUIGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Shop UI")]
        public static void ShowWindow()
        {
            GetWindow<ShopUIGenerator>("Shop UI Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Shop UI Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("상점 UI 자동 생성 도구입니다.");
            GUILayout.Label("Generate Shop UI 버튼을 클릭하면 상점 UI가 생성됩니다.");
            GUILayout.Space(10);

            GUILayout.Label("생성될 UI 구성:");
            GUILayout.Label("• 카드 판매 슬롯 5개");
            GUILayout.Label("• 유물 판매 슬롯 3개");
            GUILayout.Label("• 카드 제거 서비스");
            GUILayout.Label("• 체력 회복 서비스");
            GUILayout.Label("• 플레이어 상태 (골드, 체력)");
            GUILayout.Space(10);

            if (GUILayout.Button("Generate Shop UI", GUILayout.Height(30)))
            {
                GenerateShopUI();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Generate Card Removal UI", GUILayout.Height(30)))
            {
                GenerateCardRemovalUI();
            }

            GUILayout.Space(10);
            GUILayout.Label("생성된 UI는 씬에 ShopUI 오브젝트로 추가됩니다.");
            GUILayout.Label("ShopUIManager에서 자동으로 찾아서 연결됩니다.");
        }

        private void GenerateShopUI()
        {
            // Canvas 찾기 또는 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                Debug.Log("[ShopUIGenerator] Canvas가 없어서 새로 생성했습니다.");
            }

            // 상점 UI 생성
            GameObject shopUI = CreateShopUI(canvas.gameObject);

            if (shopUI != null)
            {
                Selection.activeGameObject = shopUI;
                Debug.Log("[ShopUIGenerator] 상점 UI 생성 완료: " + shopUI.name);
            }
        }

        private void GenerateCardRemovalUI()
        {
            // Canvas 찾기 또는 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                Debug.Log("[ShopUIGenerator] Canvas가 없어서 새로 생성했습니다.");
            }

            // 카드 제거 UI 생성
            GameObject cardRemovalUI = CreateCardRemovalUI(canvas.gameObject);

            if (cardRemovalUI != null)
            {
                Selection.activeGameObject = cardRemovalUI;
                Debug.Log("[ShopUIGenerator] 카드 제거 UI 생성 완료: " + cardRemovalUI.name);
            }
        }

        /// <summary>
        /// 상점 UI 생성
        /// </summary>
        private GameObject CreateShopUI(GameObject parent)
        {
            // 메인 패널 생성
            GameObject shopUI = new GameObject("ShopUI");
            shopUI.transform.SetParent(parent.transform, false);

            // Canvas Group 추가
            CanvasGroup canvasGroup = shopUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 전체 화면 크기로 설정
            RectTransform shopRect = shopUI.AddComponent<RectTransform>();
            shopRect.anchorMin = Vector2.zero;
            shopRect.anchorMax = Vector2.one;
            shopRect.offsetMin = Vector2.zero;
            shopRect.offsetMax = Vector2.zero;

            // 배경 생성
            CreateShopBackground(shopUI);

            // 메인 컨테이너 생성
            GameObject mainContainer = CreateMainContainer(shopUI);

            // 제목 생성
            CreateShopTitle(mainContainer);

            // 플레이어 상태 패널 생성
            CreatePlayerStatusPanel(mainContainer);

            // 상품 영역 생성
            CreateItemsArea(mainContainer);

            // 하단 버튼 영역 생성
            CreateBottomButtonsArea(mainContainer);

            return shopUI;
        }

        /// <summary>
        /// 배경 생성
        /// </summary>
        private void CreateShopBackground(GameObject parent)
        {
            GameObject background = new GameObject("Background");
            background.transform.SetParent(parent.transform, false);

            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f); // 어두운 파란색

            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 메인 컨테이너 생성
        /// </summary>
        private GameObject CreateMainContainer(GameObject parent)
        {
            GameObject container = new GameObject("MainContainer");
            container.transform.SetParent(parent.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.05f, 0.05f);
            containerRect.anchorMax = new Vector2(0.95f, 0.95f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            return container;
        }

        /// <summary>
        /// 상점 제목 생성
        /// </summary>
        private void CreateShopTitle(GameObject parent)
        {
            GameObject titleObj = new GameObject("ShopTitle");
            titleObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "상점";
            titleText.fontSize = 48;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.9f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 플레이어 상태 패널 생성
        /// </summary>
        private void CreatePlayerStatusPanel(GameObject parent)
        {
            GameObject statusPanel = new GameObject("PlayerStatusPanel");
            statusPanel.transform.SetParent(parent.transform, false);

            Image panelImage = statusPanel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);

            RectTransform panelRect = statusPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.8f);
            panelRect.anchorMax = new Vector2(1f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Horizontal Layout Group 추가
            HorizontalLayoutGroup layoutGroup = statusPanel.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 20f;
            layoutGroup.padding = new RectOffset(20, 20, 10, 10);
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            // 골드 표시
            CreateStatusText(statusPanel, "GoldText", "골드: 0");

            // 체력 표시
            CreateStatusText(statusPanel, "HealthText", "체력: 0/0");
        }

        /// <summary>
        /// 상태 텍스트 생성
        /// </summary>
        private void CreateStatusText(GameObject parent, string name, string text)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200f, 40f);
        }

        /// <summary>
        /// 상품 영역 생성
        /// </summary>
        private void CreateItemsArea(GameObject parent)
        {
            GameObject itemsArea = new GameObject("ItemsArea");
            itemsArea.transform.SetParent(parent.transform, false);

            RectTransform itemsRect = itemsArea.AddComponent<RectTransform>();
            itemsRect.anchorMin = new Vector2(0f, 0.15f);
            itemsRect.anchorMax = new Vector2(1f, 0.8f);
            itemsRect.offsetMin = Vector2.zero;
            itemsRect.offsetMax = Vector2.zero;

            // Vertical Layout Group 추가
            VerticalLayoutGroup verticalLayout = itemsArea.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 20f;
            verticalLayout.padding = new RectOffset(10, 10, 10, 10);
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;

            // 카드 영역 생성
            CreateCardArea(itemsArea);

            // 유물 영역 생성
            CreateRelicArea(itemsArea);

            // 서비스 영역 생성
            CreateServiceArea(itemsArea);
        }

        /// <summary>
        /// 카드 영역 생성
        /// </summary>
        private void CreateCardArea(GameObject parent)
        {
            GameObject cardArea = new GameObject("CardArea");
            cardArea.transform.SetParent(parent.transform, false);

            RectTransform cardRect = cardArea.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(0f, 180f);

            // 라벨 추가
            CreateSectionLabel(cardArea, "카드");

            // 카드 그리드 생성
            GameObject cardGrid = new GameObject("CardGrid");
            cardGrid.transform.SetParent(cardArea.transform, false);

            RectTransform gridRect = cardGrid.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0f);
            gridRect.anchorMax = new Vector2(1f, 0.8f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            // Grid Layout Group 추가
            GridLayoutGroup gridLayout = cardGrid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(140f, 200f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            // 카드 슬롯 5개 생성
            for (int i = 0; i < 5; i++)
            {
                CreateItemSlot(cardGrid, $"CardSlot_{i}", "카드", 0);
            }
        }

        /// <summary>
        /// 유물 영역 생성
        /// </summary>
        private void CreateRelicArea(GameObject parent)
        {
            GameObject relicArea = new GameObject("RelicArea");
            relicArea.transform.SetParent(parent.transform, false);

            RectTransform relicRect = relicArea.AddComponent<RectTransform>();
            relicRect.sizeDelta = new Vector2(0f, 180f);

            // 라벨 추가
            CreateSectionLabel(relicArea, "유물");

            // 유물 그리드 생성
            GameObject relicGrid = new GameObject("RelicGrid");
            relicGrid.transform.SetParent(relicArea.transform, false);

            RectTransform gridRect = relicGrid.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0f);
            gridRect.anchorMax = new Vector2(1f, 0.8f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            // Grid Layout Group 추가
            GridLayoutGroup gridLayout = relicGrid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(140f, 200f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            // 유물 슬롯 3개 생성
            for (int i = 0; i < 3; i++)
            {
                CreateItemSlot(relicGrid, $"RelicSlot_{i}", "유물", 0);
            }
        }

        /// <summary>
        /// 서비스 영역 생성
        /// </summary>
        private void CreateServiceArea(GameObject parent)
        {
            GameObject serviceArea = new GameObject("ServiceArea");
            serviceArea.transform.SetParent(parent.transform, false);

            RectTransform serviceRect = serviceArea.AddComponent<RectTransform>();
            serviceRect.sizeDelta = new Vector2(0f, 120f);

            // 라벨 추가
            CreateSectionLabel(serviceArea, "서비스");

            // 서비스 그리드 생성
            GameObject serviceGrid = new GameObject("ServiceGrid");
            serviceGrid.transform.SetParent(serviceArea.transform, false);

            RectTransform gridRect = serviceGrid.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0f);
            gridRect.anchorMax = new Vector2(1f, 0.8f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            // Horizontal Layout Group 추가
            HorizontalLayoutGroup layoutGroup = serviceGrid.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 20f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;

            // 카드 제거 서비스
            CreateServiceSlot(serviceGrid, "CardRemovalSlot", "카드 제거", 50);

            // 체력 회복 서비스
            CreateServiceSlot(serviceGrid, "HealthRestoreSlot", "체력 회복", 30);
        }

        /// <summary>
        /// 섹션 라벨 생성
        /// </summary>
        private void CreateSectionLabel(GameObject parent, string labelText)
        {
            GameObject labelObj = new GameObject($"{labelText}Label");
            labelObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 24;
            label.color = Color.yellow;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.8f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 아이템 슬롯 생성
        /// </summary>
        private void CreateItemSlot(GameObject parent, string slotName, string itemName, int price)
        {
            GameObject slot = new GameObject(slotName);
            slot.transform.SetParent(parent.transform, false);

            // 슬롯 배경
            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // 버튼 기능 추가
            Button slotButton = slot.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;

            // 슬롯 내부 레이아웃 설정
            VerticalLayoutGroup verticalLayout = slot.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 5f;
            verticalLayout.padding = new RectOffset(5, 5, 5, 5);
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;

            // 아이템 아이콘
            CreateItemIcon(slot);

            // 아이템 이름
            CreateItemName(slot, itemName);

            // 아이템 설명
            CreateItemDescription(slot, "아이템 설명");

            // 가격
            CreateItemPrice(slot, price);
        }

        /// <summary>
        /// 서비스 슬롯 생성
        /// </summary>
        private void CreateServiceSlot(GameObject parent, string slotName, string serviceName, int price)
        {
            GameObject slot = new GameObject(slotName);
            slot.transform.SetParent(parent.transform, false);

            // 슬롯 배경
            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(0.2f, 0.4f, 0.6f, 1f);

            // 버튼 기능 추가
            Button slotButton = slot.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;

            // 슬롯 내부 레이아웃 설정
            VerticalLayoutGroup verticalLayout = slot.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 5f;
            verticalLayout.padding = new RectOffset(10, 10, 10, 10);
            verticalLayout.childAlignment = TextAnchor.MiddleCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;

            // 서비스 이름
            CreateServiceName(slot, serviceName);

            // 서비스 설명
            CreateServiceDescription(slot, "서비스 설명");

            // 가격
            CreateItemPrice(slot, price);
        }

        /// <summary>
        /// 아이템 아이콘 생성
        /// </summary>
        private void CreateItemIcon(GameObject parent)
        {
            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(parent.transform, false);

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(80f, 100f);
        }

        /// <summary>
        /// 아이템 이름 생성
        /// </summary>
        private void CreateItemName(GameObject parent, string name)
        {
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = name;
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontStyle = FontStyles.Bold;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(0f, 20f);
        }

        /// <summary>
        /// 아이템 설명 생성
        /// </summary>
        private void CreateItemDescription(GameObject parent, string description)
        {
            GameObject descObj = new GameObject("ItemDescription");
            descObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = description;
            descText.fontSize = 10;
            descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            descText.alignment = TextAlignmentOptions.Center;
            descText.enableWordWrapping = true;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.sizeDelta = new Vector2(0f, 40f);
        }

        /// <summary>
        /// 서비스 이름 생성
        /// </summary>
        private void CreateServiceName(GameObject parent, string name)
        {
            GameObject nameObj = new GameObject("ServiceName");
            nameObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = name;
            nameText.fontSize = 18;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontStyle = FontStyles.Bold;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(0f, 30f);
        }

        /// <summary>
        /// 서비스 설명 생성
        /// </summary>
        private void CreateServiceDescription(GameObject parent, string description)
        {
            GameObject descObj = new GameObject("ServiceDescription");
            descObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = description;
            descText.fontSize = 12;
            descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            descText.alignment = TextAlignmentOptions.Center;
            descText.enableWordWrapping = true;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.sizeDelta = new Vector2(0f, 30f);
        }

        /// <summary>
        /// 가격 생성
        /// </summary>
        private void CreateItemPrice(GameObject parent, int price)
        {
            GameObject priceObj = new GameObject("ItemPrice");
            priceObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI priceText = priceObj.AddComponent<TextMeshProUGUI>();
            priceText.text = $"{price} 골드";
            priceText.fontSize = 12;
            priceText.color = Color.yellow;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontStyle = FontStyles.Bold;

            RectTransform priceRect = priceObj.GetComponent<RectTransform>();
            priceRect.sizeDelta = new Vector2(0f, 20f);
        }

        /// <summary>
        /// 하단 버튼 영역 생성
        /// </summary>
        private void CreateBottomButtonsArea(GameObject parent)
        {
            GameObject buttonArea = new GameObject("BottomButtonsArea");
            buttonArea.transform.SetParent(parent.transform, false);

            RectTransform buttonRect = buttonArea.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0.15f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // Horizontal Layout Group 추가
            HorizontalLayoutGroup layoutGroup = buttonArea.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 20f;
            layoutGroup.padding = new RectOffset(20, 20, 20, 20);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;

            // 나가기 버튼
            CreateExitButton(buttonArea);
        }

        /// <summary>
        /// 나가기 버튼 생성
        /// </summary>
        private void CreateExitButton(GameObject parent)
        {
            GameObject exitButtonObj = new GameObject("ExitButton");
            exitButtonObj.transform.SetParent(parent.transform, false);

            Image buttonImage = exitButtonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); // 빨간색

            Button exitButton = exitButtonObj.AddComponent<Button>();
            exitButton.targetGraphic = buttonImage;

            // 버튼 텍스트
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(exitButtonObj.transform, false);

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "나가기";
            buttonText.fontSize = 24;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.fontStyle = FontStyles.Bold;

            RectTransform textRect = buttonTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 카드 제거 UI 생성
        /// </summary>
        private GameObject CreateCardRemovalUI(GameObject parent)
        {
            // 메인 패널 생성
            GameObject cardRemovalUI = new GameObject("CardRemovalUI");
            cardRemovalUI.transform.SetParent(parent.transform, false);

            // Canvas Group 추가
            CanvasGroup canvasGroup = cardRemovalUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // 기본적으로 숨김
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 전체 화면 크기로 설정
            RectTransform uiRect = cardRemovalUI.AddComponent<RectTransform>();
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.offsetMin = Vector2.zero;
            uiRect.offsetMax = Vector2.zero;

            // 배경 오버레이 생성
            CreateRemovalOverlay(cardRemovalUI);

            // 메인 패널 생성
            GameObject mainPanel = CreateRemovalMainPanel(cardRemovalUI);

            // 제목 생성
            CreateRemovalTitle(mainPanel);

            // 카드 목록 영역 생성
            CreateCardListArea(mainPanel);

            // 취소 버튼 생성
            CreateCancelButton(mainPanel);

            return cardRemovalUI;
        }

        /// <summary>
        /// 카드 제거 오버레이 생성
        /// </summary>
        private void CreateRemovalOverlay(GameObject parent)
        {
            GameObject overlay = new GameObject("Overlay");
            overlay.transform.SetParent(parent.transform, false);

            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.8f); // 80% 검정 투명

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 카드 제거 메인 패널 생성
        /// </summary>
        private GameObject CreateRemovalMainPanel(GameObject parent)
        {
            GameObject panel = new GameObject("MainPanel");
            panel.transform.SetParent(parent.transform, false);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.9f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            return panel;
        }

        /// <summary>
        /// 카드 제거 제목 생성
        /// </summary>
        private void CreateRemovalTitle(GameObject parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "제거할 카드를 선택하세요";
            titleText.fontSize = 32;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.85f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(20f, -10f);
            titleRect.offsetMax = new Vector2(-20f, -10f);
        }

        /// <summary>
        /// 카드 목록 영역 생성
        /// </summary>
        private void CreateCardListArea(GameObject parent)
        {
            GameObject cardListArea = new GameObject("CardListArea");
            cardListArea.transform.SetParent(parent.transform, false);

            RectTransform listRect = cardListArea.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0.15f);
            listRect.anchorMax = new Vector2(1f, 0.85f);
            listRect.offsetMin = new Vector2(20f, 20f);
            listRect.offsetMax = new Vector2(-20f, -20f);

            // Scroll Rect 추가
            ScrollRect scrollRect = cardListArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // Viewport 생성
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(cardListArea.transform, false);

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            // Content 생성
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);

            // Grid Layout Group 추가
            GridLayoutGroup gridLayout = content.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(120f, 160f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            // Content Size Fitter 추가
            ContentSizeFitter sizeFitter = content.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Scroll Rect 연결
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
        }

        /// <summary>
        /// 취소 버튼 생성
        /// </summary>
        private void CreateCancelButton(GameObject parent)
        {
            GameObject cancelButtonObj = new GameObject("CancelButton");
            cancelButtonObj.transform.SetParent(parent.transform, false);

            Image buttonImage = cancelButtonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            Button cancelButton = cancelButtonObj.AddComponent<Button>();
            cancelButton.targetGraphic = buttonImage;

            RectTransform buttonRect = cancelButtonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.4f, 0.05f);
            buttonRect.anchorMax = new Vector2(0.6f, 0.15f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // 버튼 텍스트
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(cancelButtonObj.transform, false);

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "취소";
            buttonText.fontSize = 18;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = buttonTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
    }
}