using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Maglin.Editor
{
    /// <summary>
    /// 전투 보상 UI를 자동 생성하는 에디터 도구
    /// </summary>
    public class RewardUIGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Rewards UI")]
        public static void ShowWindow()
        {
            GetWindow<RewardUIGenerator>("Reward UI Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Reward UI Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("보상 UI 자동 생성 도구입니다.");
            GUILayout.Label("Generate Rewards UI 버튼을 클릭하면 보상 선택 UI가 생성됩니다.");
            GUILayout.Space(10);

            if (GUILayout.Button("Generate Rewards UI", GUILayout.Height(30)))
            {
                GenerateRewardsUI();
            }

            GUILayout.Space(10);
            GUILayout.Label("생성된 UI는 씬에 RewardSelectionUI 오브젝트로 추가됩니다.");
            GUILayout.Label("BattleUIManager에서 자동으로 찾아서 연결됩니다.");
        }

        private void GenerateRewardsUI()
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

                Debug.Log("[RewardUIGenerator] Canvas가 없어서 새로 생성했습니다.");
            }

            // 보상 선택 UI 생성
            GameObject rewardUI = CreateRewardSelectionUI(canvas.gameObject);

            if (rewardUI != null)
            {
                Selection.activeGameObject = rewardUI;
                Debug.Log("[RewardUIGenerator] 보상 UI 생성 완료: " + rewardUI.name);
            }
        }

        /// <summary>
        /// 보상 선택 UI 생성
        /// </summary>
        private GameObject CreateRewardSelectionUI(GameObject parent)
        {
            // 메인 패널 생성
            GameObject rewardUI = new GameObject("RewardSelectionUI");
            rewardUI.transform.SetParent(parent.transform, false);

            // Canvas Group 추가 (전체 opacity 제어용)
            CanvasGroup canvasGroup = rewardUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // 기본적으로 숨김
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 전체 화면 크기로 설정
            RectTransform rewardRect = rewardUI.AddComponent<RectTransform>();
            rewardRect.anchorMin = Vector2.zero;
            rewardRect.anchorMax = Vector2.one;
            rewardRect.offsetMin = Vector2.zero;
            rewardRect.offsetMax = Vector2.zero;

            // 배경 어둡게 만들기 (Semi-transparent overlay)
            CreateOverlay(rewardUI);

            // 메인 패널 생성
            GameObject mainPanel = CreateMainPanel(rewardUI);

            // 제목 생성
            CreateTitle(mainPanel);

            // 보상 슬롯 영역 생성
            CreateRewardSlotsArea(mainPanel);

            // 건너뛰기 버튼 생성
            CreateSkipButton(mainPanel);

            return rewardUI;
        }

        /// <summary>
        /// 반투명 오버레이 생성
        /// </summary>
        private void CreateOverlay(GameObject parent)
        {
            GameObject overlay = new GameObject("Overlay");
            overlay.transform.SetParent(parent.transform, false);

            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.7f); // 70% 검정 투명

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 메인 패널 생성
        /// </summary>
        private GameObject CreateMainPanel(GameObject parent)
        {
            GameObject panel = new GameObject("MainPanel");
            panel.transform.SetParent(parent.transform, false);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f); // 어두운 회색

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.2f);
            panelRect.anchorMax = new Vector2(0.8f, 0.8f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // 패널에 라운드 모서리 효과 (선택사항)
            panel.AddComponent<Outline>();

            return panel;
        }

        /// <summary>
        /// 제목 생성
        /// </summary>
        private void CreateTitle(GameObject parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "전투 보상 선택";
            titleText.fontSize = 32;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.8f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(20f, -10f);
            titleRect.offsetMax = new Vector2(-20f, -10f);
        }

        /// <summary>
        /// 보상 슬롯 영역 생성
        /// </summary>
        private void CreateRewardSlotsArea(GameObject parent)
        {
            GameObject slotsArea = new GameObject("RewardSlotsArea");
            slotsArea.transform.SetParent(parent.transform, false);

            RectTransform slotsRect = slotsArea.AddComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0f, 0.2f);
            slotsRect.anchorMax = new Vector2(1f, 0.8f);
            slotsRect.offsetMin = new Vector2(20f, 20f);
            slotsRect.offsetMax = new Vector2(-20f, -20f);

            // Horizontal Layout Group 추가
            HorizontalLayoutGroup layoutGroup = slotsArea.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 20f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;

            // 보상 슬롯 3개 생성
            for (int i = 0; i < 3; i++)
            {
                CreateRewardSlot(slotsArea, i);
            }
        }

        /// <summary>
        /// 개별 보상 슬롯 생성
        /// </summary>
        private void CreateRewardSlot(GameObject parent, int index)
        {
            GameObject slot = new GameObject($"RewardSlot_{index}");
            slot.transform.SetParent(parent.transform, false);

            // 슬롯 배경
            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // 버튼 기능 추가
            Button slotButton = slot.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;

            // 슬롯 크기 설정 (Layout Group이 제어하지만 최소 크기 설정)
            LayoutElement layoutElement = slot.AddComponent<LayoutElement>();
            layoutElement.minWidth = 200f;
            layoutElement.minHeight = 300f;

            // 슬롯 내부 레이아웃 설정
            VerticalLayoutGroup verticalLayout = slot.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 10f;
            verticalLayout.padding = new RectOffset(10, 10, 10, 10);
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;

            // 보상 아이콘 영역 (카드/유물 이미지가 들어갈 곳)
            CreateRewardIcon(slot);

            // 보상 이름
            CreateRewardName(slot);

            // 보상 설명
            CreateRewardDescription(slot);
        }

        /// <summary>
        /// 보상 아이콘 생성
        /// </summary>
        private void CreateRewardIcon(GameObject parent)
        {
            GameObject iconObj = new GameObject("RewardIcon");
            iconObj.transform.SetParent(parent.transform, false);

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(120f, 160f); // 카드 비율에 맞춤
        }

        /// <summary>
        /// 보상 이름 생성
        /// </summary>
        private void CreateRewardName(GameObject parent)
        {
            GameObject nameObj = new GameObject("RewardName");
            nameObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = "보상 이름";
            nameText.fontSize = 16;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontStyle = FontStyles.Bold;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(0f, 30f);
        }

        /// <summary>
        /// 보상 설명 생성
        /// </summary>
        private void CreateRewardDescription(GameObject parent)
        {
            GameObject descObj = new GameObject("RewardDescription");
            descObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = "보상 설명이 여기에 표시됩니다.";
            descText.fontSize = 12;
            descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            descText.alignment = TextAlignmentOptions.Center;
            descText.enableWordWrapping = true;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.sizeDelta = new Vector2(0f, 60f);
        }

        /// <summary>
        /// 건너뛰기 버튼 생성
        /// </summary>
        private void CreateSkipButton(GameObject parent)
        {
            GameObject skipButtonObj = new GameObject("SkipButton");
            skipButtonObj.transform.SetParent(parent.transform, false);

            Image buttonImage = skipButtonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); // 빨간색

            Button skipButton = skipButtonObj.AddComponent<Button>();
            skipButton.targetGraphic = buttonImage;

            RectTransform buttonRect = skipButtonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.8f, 0.05f);
            buttonRect.anchorMax = new Vector2(0.95f, 0.15f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // 버튼 텍스트
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(skipButtonObj.transform, false);

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "건너뛰기";
            buttonText.fontSize = 14;
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