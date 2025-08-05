using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Maglin.Editor
{
    /// <summary>
    /// 전투 UI를 자동 생성하는 에디터 도구
    /// </summary>
    public class BattleUIGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Battle UI")]
        public static void ShowWindow()
        {
            GetWindow<BattleUIGenerator>("Battle UI Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("전투 UI 생성 도구", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            if (GUILayout.Button("전투 화면 UI 생성", GUILayout.Height(30)))
            {
                GenerateBattleUI();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("플레이어 상태 패널 생성", GUILayout.Height(30)))
            {
                GeneratePlayerStatusPanel();
            }

            if (GUILayout.Button("카드 UI 시스템 생성", GUILayout.Height(30)))
            {
                GenerateCardUISystem();
            }

            if (GUILayout.Button("필드 슬롯 생성", GUILayout.Height(30)))
            {
                GenerateFieldSlots();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("전투 씬에서 실행해주세요. 생성된 UI는 프리팹으로 저장됩니다.", MessageType.Info);
        }

        /// <summary>
        /// 전체 전투 UI 생성
        /// </summary>
        private void GenerateBattleUI()
        {
            // Canvas 찾기 또는 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("BattleCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 메인 UI 루트 생성
            GameObject battleUIRoot = new GameObject("BattleUI");
            battleUIRoot.transform.SetParent(canvas.transform, false);
            RectTransform battleUIRect = battleUIRoot.AddComponent<RectTransform>();
            battleUIRect.anchorMin = Vector2.zero;
            battleUIRect.anchorMax = Vector2.one;
            battleUIRect.offsetMin = Vector2.zero;
            battleUIRect.offsetMax = Vector2.zero;

            // 각 영역 생성
            CreatePlayerStatusPanel(battleUIRoot);
            CreateFieldArea(battleUIRoot);
            CreateCardArea(battleUIRoot);

            // 프리팹으로 저장
            SaveAsPrefab(battleUIRoot, "BattleUI");

            Debug.Log("[BattleUIGenerator] 전투 UI 생성 완료!");
        }

        /// <summary>
        /// 플레이어 상태 패널 생성
        /// </summary>
        private void GeneratePlayerStatusPanel()
        {
            GameObject statusPanel = CreatePlayerStatusPanel(null);
            SaveAsPrefab(statusPanel, "PlayerStatusPanel");
        }

        /// <summary>
        /// 플레이어 상태 패널 생성 (내부)
        /// </summary>
        private GameObject CreatePlayerStatusPanel(GameObject parent)
        {
            GameObject statusPanel = new GameObject("PlayerStatusPanel");
            if (parent != null)
                statusPanel.transform.SetParent(parent.transform, false);

            // 배경 이미지 추가
            Image bg = statusPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // RectTransform 설정 (화면 왼쪽 상단)
            RectTransform rect = statusPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(200, 100);

            // 레이아웃 그룹 추가
            VerticalLayoutGroup layout = statusPanel.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;

            // 체력 UI
            CreateStatusText(statusPanel, "HealthText", "체력: 100/100", Color.red);

            // 마나 UI
            CreateStatusText(statusPanel, "ManaText", "마나: 50/50", Color.blue);

            // 골드 UI  
            CreateStatusText(statusPanel, "GoldText", "골드: 125", Color.yellow);

            return statusPanel;
        }

        /// <summary>
        /// 필드 영역 생성
        /// </summary>
        private void CreateFieldArea(GameObject parent)
        {
            GameObject fieldArea = new GameObject("FieldArea");
            fieldArea.transform.SetParent(parent.transform, false);

            // RectTransform 설정 (화면 중앙)
            RectTransform rect = fieldArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.3f);
            rect.anchorMax = new Vector2(0.9f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경
            Image bg = fieldArea.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.3f, 0.2f, 0.8f);

            // 10개의 슬롯 생성
            CreateFieldSlots(fieldArea);

            // 플레이어 표시 (슬롯 0에 배치)
            CreatePlayerIndicator(fieldArea);
        }

        /// <summary>
        /// 필드 슬롯들 생성
        /// </summary>
        private void GenerateFieldSlots()
        {
            GameObject fieldSlots = CreateFieldSlots(null);
            SaveAsPrefab(fieldSlots, "FieldSlots");
        }

        /// <summary>
        /// 필드 슬롯들 생성 (내부)
        /// </summary>
        private GameObject CreateFieldSlots(GameObject parent)
        {
            GameObject slotsContainer = new GameObject("FieldSlots");
            if (parent != null)
                slotsContainer.transform.SetParent(parent.transform, false);

            // 레이아웃 설정
            RectTransform containerRect = slotsContainer.AddComponent<RectTransform>();
            if (parent == null)
            {
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.offsetMin = Vector2.zero;
                containerRect.offsetMax = Vector2.zero;
            }

            HorizontalLayoutGroup layout = slotsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 5;
            layout.padding = new RectOffset(10, 10, 10, 10);

            // 10개 슬롯 생성
            for (int i = 0; i < 10; i++)
            {
                CreateFieldSlot(slotsContainer, i);
            }

            return slotsContainer;
        }

        /// <summary>
        /// 개별 필드 슬롯 생성
        /// </summary>
        private void CreateFieldSlot(GameObject parent, int index)
        {
            GameObject slot = new GameObject($"FieldSlot_{index}");
            slot.transform.SetParent(parent.transform, false);

            // 슬롯 배경
            Image slotBg = slot.AddComponent<Image>();
            slotBg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);

            // 슬롯 인덱스 표시
            GameObject indexText = new GameObject("IndexText");
            indexText.transform.SetParent(slot.transform, false);

            TextMeshProUGUI indexTMP = indexText.AddComponent<TextMeshProUGUI>();
            indexTMP.text = index.ToString();
            indexTMP.fontSize = 12;
            indexTMP.color = Color.white;
            indexTMP.alignment = TextAlignmentOptions.Center;

            RectTransform indexRect = indexText.GetComponent<RectTransform>();
            indexRect.anchorMin = new Vector2(0, 0);
            indexRect.anchorMax = new Vector2(1, 0.2f);
            indexRect.offsetMin = Vector2.zero;
            indexRect.offsetMax = Vector2.zero;

            // 캐릭터/몬스터 표시 영역
            GameObject characterArea = new GameObject("CharacterArea");
            characterArea.transform.SetParent(slot.transform, false);

            RectTransform charRect = characterArea.AddComponent<RectTransform>();
            charRect.anchorMin = new Vector2(0, 0.2f);
            charRect.anchorMax = new Vector2(1, 0.8f);
            charRect.offsetMin = Vector2.zero;
            charRect.offsetMax = Vector2.zero;

            // 체력 표시 영역
            GameObject healthArea = new GameObject("HealthArea");
            healthArea.transform.SetParent(slot.transform, false);

            RectTransform healthRect = healthArea.AddComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0, 0.8f);
            healthRect.anchorMax = new Vector2(1, 1);
            healthRect.offsetMin = Vector2.zero;
            healthRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 플레이어 표시 생성
        /// </summary>
        private void CreatePlayerIndicator(GameObject parent)
        {
            GameObject playerIndicator = new GameObject("PlayerIndicator");
            playerIndicator.transform.SetParent(parent.transform, false);

            // 플레이어 스프라이트
            Image playerImage = playerIndicator.AddComponent<Image>();
            playerImage.color = Color.green;

            RectTransform rect = playerIndicator.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.01f, 0.1f);
            rect.anchorMax = new Vector2(0.09f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 카드 영역 생성
        /// </summary>
        private GameObject CreateCardArea(GameObject parent)
        {
            GameObject cardArea = new GameObject("CardArea");
            cardArea.transform.SetParent(parent.transform, false);

            // RectTransform 설정 (화면 하단)
            RectTransform rect = cardArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.3f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경
            Image bg = cardArea.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // 카드 덱 영역
            CreateCardDeckArea(cardArea);

            // 손패 영역
            CreateHandArea(cardArea);

            // 조합 슬롯 영역
            CreateComboSlotArea(cardArea);

            // 버튼 영역
            CreateButtonArea(cardArea);

            return cardArea;
        }

        /// <summary>
        /// 카드 UI 시스템 생성
        /// </summary>
        private void GenerateCardUISystem()
        {
            // 임시 부모 GameObject 생성
            GameObject tempParent = new GameObject("TempParent");
            GameObject cardUI = CreateCardArea(tempParent);

            // 부모 관계 해제하여 독립적인 프리팹으로 만들기
            cardUI.transform.SetParent(null);

            SaveAsPrefab(cardUI, "CardUISystem");

            // 임시 부모 삭제
            DestroyImmediate(tempParent);
        }

        /// <summary>
        /// 카드 덱 영역 생성
        /// </summary>
        private void CreateCardDeckArea(GameObject parent)
        {
            GameObject deckArea = new GameObject("DeckArea");
            deckArea.transform.SetParent(parent.transform, false);

            RectTransform rect = deckArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0.2f, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 덱 이미지
            Image deckImage = deckArea.AddComponent<Image>();
            deckImage.color = new Color(0.3f, 0.3f, 0.5f, 1f);

            // 덱 카운트 텍스트
            GameObject deckCountText = new GameObject("DeckCountText");
            deckCountText.transform.SetParent(deckArea.transform, false);

            TextMeshProUGUI deckTMP = deckCountText.AddComponent<TextMeshProUGUI>();
            deckTMP.text = "덱: 30";
            deckTMP.fontSize = 14;
            deckTMP.color = Color.white;
            deckTMP.alignment = TextAlignmentOptions.Center;

            RectTransform deckTextRect = deckCountText.GetComponent<RectTransform>();
            deckTextRect.anchorMin = Vector2.zero;
            deckTextRect.anchorMax = Vector2.one;
            deckTextRect.offsetMin = Vector2.zero;
            deckTextRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 손패 영역 생성
        /// </summary>
        private void CreateHandArea(GameObject parent)
        {
            GameObject handArea = new GameObject("HandArea");
            handArea.transform.SetParent(parent.transform, false);

            RectTransform rect = handArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.5f);
            rect.anchorMax = new Vector2(0.8f, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 손패 스크롤 영역
            ScrollRect scrollRect = handArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;

            // 손패 컨텐츠 영역
            GameObject content = new GameObject("HandContent");
            content.transform.SetParent(handArea.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // 레이아웃 그룹
            HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.spacing = 5;
            layout.padding = new RectOffset(5, 5, 5, 5);

            // Content Size Fitter
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;

            // 손패 라벨
            CreateLabel(handArea, "손패", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), new Vector2(0, 0));
        }

        /// <summary>
        /// 조합 슬롯 영역 생성
        /// </summary>
        private void CreateComboSlotArea(GameObject parent)
        {
            GameObject comboArea = new GameObject("ComboSlotArea");
            comboArea.transform.SetParent(parent.transform, false);

            RectTransform rect = comboArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.8f, 0.5f);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 조합 슬롯 3개 생성
            VerticalLayoutGroup layout = comboArea.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 2;
            layout.padding = new RectOffset(2, 2, 2, 2);

            for (int i = 0; i < 3; i++)
            {
                CreateComboSlot(comboArea, i);
            }

            // 조합 라벨
            CreateLabel(comboArea, "조합", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -15), new Vector2(0, 0));
        }

        /// <summary>
        /// 조합 슬롯 생성
        /// </summary>
        private void CreateComboSlot(GameObject parent, int index)
        {
            GameObject slot = new GameObject($"ComboSlot_{index}");
            slot.transform.SetParent(parent.transform, false);

            Image slotBg = slot.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // 드롭 핸들러 추가 (나중에 구현)
            // slot.AddComponent<ComboSlotDropHandler>();
        }

        /// <summary>
        /// 버튼 영역 생성
        /// </summary>
        private void CreateButtonArea(GameObject parent)
        {
            GameObject buttonArea = new GameObject("ButtonArea");
            buttonArea.transform.SetParent(parent.transform, false);

            RectTransform rect = buttonArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 레이아웃 그룹
            HorizontalLayoutGroup layout = buttonArea.AddComponent<HorizontalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);

            // 사용 버튼
            CreateButton(buttonArea, "UseButton", "사용", Color.green);

            // 턴 종료 버튼
            CreateButton(buttonArea, "EndTurnButton", "턴 종료", Color.red);

            // 추가 드로우 버튼
            CreateButton(buttonArea, "DrawButton", "드로우 (10)", Color.blue);
        }

        /// <summary>
        /// 버튼 생성
        /// </summary>
        private void CreateButton(GameObject parent, string name, string text, Color color)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent.transform, false);

            Image buttonImage = button.AddComponent<Image>();
            buttonImage.color = color;

            Button buttonComp = button.AddComponent<Button>();

            // 버튼 텍스트
            GameObject buttonText = new GameObject("Text");
            buttonText.transform.SetParent(button.transform, false);

            TextMeshProUGUI textTMP = buttonText.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = 14;
            textTMP.color = Color.white;
            textTMP.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = buttonText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 상태 텍스트 생성
        /// </summary>
        private void CreateStatusText(GameObject parent, string name, string text, Color color)
        {
            GameObject statusText = new GameObject(name);
            statusText.transform.SetParent(parent.transform, false);

            TextMeshProUGUI textTMP = statusText.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = 14;
            textTMP.color = color;
            textTMP.alignment = TextAlignmentOptions.Left;
        }

        /// <summary>
        /// 라벨 생성
        /// </summary>
        private void CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject label = new GameObject($"Label_{text}");
            label.transform.SetParent(parent.transform, false);

            TextMeshProUGUI labelTMP = label.AddComponent<TextMeshProUGUI>();
            labelTMP.text = text;
            labelTMP.fontSize = 12;
            labelTMP.color = Color.white;
            labelTMP.alignment = TextAlignmentOptions.Center;

            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = anchorMin;
            labelRect.anchorMax = anchorMax;
            labelRect.offsetMin = offsetMin;
            labelRect.offsetMax = offsetMax;
        }

        /// <summary>
        /// 프리팹으로 저장
        /// </summary>
        private void SaveAsPrefab(GameObject obj, string name)
        {
            string path = $"Assets/Prefabs/UI/{name}.prefab";

            // 디렉토리 생성
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");

            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(obj, path);

            Debug.Log($"[BattleUIGenerator] {name} 프리팹 저장 완료: {path}");
        }
    }
}