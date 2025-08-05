using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Maglin.Editor
{
    /// <summary>
    /// 이벤트 UI를 자동 생성하는 에디터 도구
    /// </summary>
    public class EventUIGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Event UI")]
        public static void ShowWindow()
        {
            GetWindow<EventUIGenerator>("Event UI Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("이벤트 UI 생성 도구", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            if (GUILayout.Button("이벤트 화면 UI 생성", GUILayout.Height(30)))
            {
                GenerateEventUI();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("이벤트 정보 패널 생성", GUILayout.Height(30)))
            {
                GenerateEventInfoPanel();
            }

            if (GUILayout.Button("선택지 영역 생성", GUILayout.Height(30)))
            {
                GenerateChoicesArea();
            }

            if (GUILayout.Button("결과 UI 생성", GUILayout.Height(30)))
            {
                GenerateResultUI();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("이벤트 씬에서 실행해주세요. 생성된 UI는 프리팹으로 저장됩니다.", MessageType.Info);
        }

        /// <summary>
        /// 전체 이벤트 UI 생성
        /// </summary>
        private void GenerateEventUI()
        {
            // Canvas 찾기 또는 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("EventCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 메인 UI 루트 생성
            GameObject eventUIRoot = new GameObject("EventUI");
            eventUIRoot.transform.SetParent(canvas.transform, false);
            RectTransform eventUIRect = eventUIRoot.AddComponent<RectTransform>();
            eventUIRect.anchorMin = Vector2.zero;
            eventUIRect.anchorMax = Vector2.one;
            eventUIRect.offsetMin = Vector2.zero;
            eventUIRect.offsetMax = Vector2.zero;

            // CanvasGroup 추가 (EventUIManager에서 사용)
            CanvasGroup canvasGroup = eventUIRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 각 영역 생성
            CreatePlayerStatusPanel(eventUIRoot);
            CreateEventInfoArea(eventUIRoot);
            CreateChoicesArea(eventUIRoot);
            CreateResultUI(eventUIRoot);

            // 프리팹으로 저장
            SaveAsPrefab(eventUIRoot, "EventUI");

            Debug.Log("[EventUIGenerator] 이벤트 UI 생성 완료!");
        }

        /// <summary>
        /// 플레이어 상태 패널 생성
        /// </summary>
        private void CreatePlayerStatusPanel(GameObject parent)
        {
            GameObject statusPanel = new GameObject("PlayerStatusPanel");
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

            // 골드 UI  
            CreateStatusText(statusPanel, "GoldText", "골드: 125", Color.yellow);
        }

        /// <summary>
        /// 이벤트 정보 영역 생성
        /// </summary>
        private void CreateEventInfoArea(GameObject parent)
        {
            GameObject eventInfoArea = new GameObject("EventInfoArea");
            eventInfoArea.transform.SetParent(parent.transform, false);

            // RectTransform 설정 (화면 상단 중앙)
            RectTransform rect = eventInfoArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.4f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경
            Image bg = eventInfoArea.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);

            // 내용 영역
            CreateEventContent(eventInfoArea);
        }

        /// <summary>
        /// 이벤트 내용 생성
        /// </summary>
        private void CreateEventContent(GameObject parent)
        {
            GameObject contentArea = new GameObject("EventContent");
            contentArea.transform.SetParent(parent.transform, false);

            RectTransform contentRect = contentArea.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(20, 20);
            contentRect.offsetMax = new Vector2(-20, -20);

            // 레이아웃 그룹
            VerticalLayoutGroup layout = contentArea.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 15;
            layout.padding = new RectOffset(0, 0, 0, 0);

            // 이벤트 이미지 영역
            CreateEventImageArea(contentArea);

            // 이벤트 제목
            CreateEventTitle(contentArea);

            // 이벤트 설명
            CreateEventDescription(contentArea);
        }

        /// <summary>
        /// 이벤트 이미지 영역 생성
        /// </summary>
        private void CreateEventImageArea(GameObject parent)
        {
            GameObject imageArea = new GameObject("EventImageArea");
            imageArea.transform.SetParent(parent.transform, false);

            RectTransform imageRect = imageArea.AddComponent<RectTransform>();
            imageRect.sizeDelta = new Vector2(0, 150);

            // 이미지
            Image eventImage = imageArea.AddComponent<Image>();
            eventImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);
            eventImage.preserveAspect = true;

            // 레이아웃 요소
            LayoutElement layoutElement = imageArea.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 150;
            layoutElement.flexibleHeight = 0;
        }

        /// <summary>
        /// 이벤트 제목 생성
        /// </summary>
        private void CreateEventTitle(GameObject parent)
        {
            GameObject titleObj = new GameObject("EventTitle");
            titleObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "이벤트 제목";
            titleText.fontSize = 24;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            // 레이아웃 요소
            LayoutElement layoutElement = titleObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 40;
            layoutElement.flexibleHeight = 0;
        }

        /// <summary>
        /// 이벤트 설명 생성
        /// </summary>
        private void CreateEventDescription(GameObject parent)
        {
            GameObject descObj = new GameObject("EventDescription");
            descObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = "이벤트 설명이 여기에 표시됩니다.";
            descText.fontSize = 16;
            descText.color = Color.white;
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.enableWordWrapping = true;

            // 레이아웃 요소
            LayoutElement layoutElement = descObj.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1;
        }

        /// <summary>
        /// 선택지 영역 생성
        /// </summary>
        private void CreateChoicesArea(GameObject parent)
        {
            GameObject choicesArea = new GameObject("ChoicesArea");
            choicesArea.transform.SetParent(parent.transform, false);

            // RectTransform 설정 (화면 하단)
            RectTransform rect = choicesArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.05f);
            rect.anchorMax = new Vector2(0.9f, 0.35f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경
            Image bg = choicesArea.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // 스크롤 영역 생성
            CreateChoicesScrollArea(choicesArea);
        }

        /// <summary>
        /// 선택지 스크롤 영역 생성
        /// </summary>
        private void CreateChoicesScrollArea(GameObject parent)
        {
            // 스크롤 뷰
            GameObject scrollView = new GameObject("ChoicesScrollView");
            scrollView.transform.SetParent(parent.transform, false);

            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -10);

            ScrollRect scrollComponent = scrollView.AddComponent<ScrollRect>();
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;

            // 뷰포트
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;

            // 컨텐츠
            GameObject content = new GameObject("ChoicesContent");
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // 레이아웃 그룹
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);

            // Content Size Fitter
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 스크롤 설정
            scrollComponent.content = contentRect;
            scrollComponent.viewport = viewportRect;

            // 예시 선택지들 생성
            for (int i = 0; i < 3; i++)
            {
                CreateChoiceButton(content, i);
            }
        }

        /// <summary>
        /// 선택지 버튼 생성
        /// </summary>
        private void CreateChoiceButton(GameObject parent, int index)
        {
            GameObject choiceButton = new GameObject($"ChoiceButton_{index}");
            choiceButton.transform.SetParent(parent.transform, false);

            // RectTransform
            RectTransform buttonRect = choiceButton.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0, 80);

            // 배경 이미지
            Image buttonImage = choiceButton.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.3f, 0.4f, 1f);

            // 버튼 컴포넌트
            Button button = choiceButton.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            // 레이아웃 요소
            LayoutElement layoutElement = choiceButton.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 80;
            layoutElement.flexibleHeight = 0;

            // 버튼 내용
            CreateChoiceButtonContent(choiceButton, index);
        }

        /// <summary>
        /// 선택지 버튼 내용 생성
        /// </summary>
        private void CreateChoiceButtonContent(GameObject parent, int index)
        {
            GameObject content = new GameObject("ChoiceContent");
            content.transform.SetParent(parent.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(15, 10);
            contentRect.offsetMax = new Vector2(-15, -10);

            // 레이아웃 그룹
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 5;

            // 선택지 텍스트
            GameObject choiceTextObj = new GameObject("ChoiceText");
            choiceTextObj.transform.SetParent(content.transform, false);

            TextMeshProUGUI choiceText = choiceTextObj.AddComponent<TextMeshProUGUI>();
            choiceText.text = $"선택지 {index + 1}";
            choiceText.fontSize = 16;
            choiceText.color = Color.white;
            choiceText.alignment = TextAlignmentOptions.TopLeft;
            choiceText.enableWordWrapping = true;

            // 성공 확률 텍스트
            GameObject probabilityObj = new GameObject("ProbabilityText");
            probabilityObj.transform.SetParent(content.transform, false);

            TextMeshProUGUI probabilityText = probabilityObj.AddComponent<TextMeshProUGUI>();
            probabilityText.text = "성공 확률: 80%";
            probabilityText.fontSize = 12;
            probabilityText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            probabilityText.alignment = TextAlignmentOptions.TopLeft;

            // 레이아웃 요소들
            LayoutElement choiceLayout = choiceTextObj.AddComponent<LayoutElement>();
            choiceLayout.flexibleHeight = 1;

            LayoutElement probLayout = probabilityObj.AddComponent<LayoutElement>();
            probLayout.preferredHeight = 20;
            probLayout.flexibleHeight = 0;
        }

        /// <summary>
        /// 이벤트 정보 패널 생성 (단독)
        /// </summary>
        private void GenerateEventInfoPanel()
        {
            // 임시 부모 GameObject 생성
            GameObject tempParent = new GameObject("TempParent");
            CreateEventInfoArea(tempParent);

            // 생성된 EventInfoArea를 독립적인 프리팹으로 만들기
            GameObject eventInfoArea = tempParent.transform.GetChild(0).gameObject;
            eventInfoArea.transform.SetParent(null);

            SaveAsPrefab(eventInfoArea, "EventInfoPanel");

            // 임시 부모 삭제
            DestroyImmediate(tempParent);
        }

        /// <summary>
        /// 선택지 영역 생성 (단독)
        /// </summary>
        private void GenerateChoicesArea()
        {
            // 임시 부모 GameObject 생성
            GameObject tempParent = new GameObject("TempParent");
            CreateChoicesArea(tempParent);

            // 생성된 ChoicesArea를 독립적인 프리팹으로 만들기
            GameObject choicesArea = tempParent.transform.GetChild(0).gameObject;
            choicesArea.transform.SetParent(null);

            SaveAsPrefab(choicesArea, "EventChoicesArea");

            // 임시 부모 삭제
            DestroyImmediate(tempParent);
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
        /// 결과 UI 생성
        /// </summary>
        private void CreateResultUI(GameObject parent)
        {
            GameObject resultUI = new GameObject("EventResultUI");
            resultUI.transform.SetParent(parent.transform, false);

            // CanvasGroup 추가
            CanvasGroup canvasGroup = resultUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // RectTransform 설정 (전체 화면)
            RectTransform rect = resultUI.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경 (반투명)
            Image bg = resultUI.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            // 결과 패널
            CreateResultPanel(resultUI);
        }

        /// <summary>
        /// 결과 패널 생성
        /// </summary>
        private void CreateResultPanel(GameObject parent)
        {
            GameObject resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(parent.transform, false);

            // RectTransform 설정 (중앙)
            RectTransform rect = resultPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, 0.3f);
            rect.anchorMax = new Vector2(0.7f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 배경
            Image bg = resultPanel.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // 레이아웃 그룹
            VerticalLayoutGroup layout = resultPanel.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 20;
            layout.padding = new RectOffset(30, 30, 30, 30);

            // 결과 텍스트
            CreateResultText(resultPanel);

            // 계속 버튼
            CreateContinueButton(resultPanel);
        }

        /// <summary>
        /// 결과 텍스트 생성
        /// </summary>
        private void CreateResultText(GameObject parent)
        {
            GameObject textObj = new GameObject("ResultText");
            textObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI resultText = textObj.AddComponent<TextMeshProUGUI>();
            resultText.text = "결과 텍스트가 여기에 표시됩니다.";
            resultText.fontSize = 18;
            resultText.color = Color.white;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.enableWordWrapping = true;

            // 레이아웃 요소
            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1;
        }

        /// <summary>
        /// 계속 버튼 생성
        /// </summary>
        private void CreateContinueButton(GameObject parent)
        {
            GameObject buttonObj = new GameObject("ContinueButton");
            buttonObj.transform.SetParent(parent.transform, false);

            // RectTransform
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(200, 50);

            // 배경 이미지
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.6f, 0.3f, 1f);

            // 버튼 컴포넌트
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            // 레이아웃 요소
            LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleHeight = 0;

            // 버튼 텍스트
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "계속";
            buttonText.fontSize = 16;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = buttonTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 결과 UI 생성 (단독)
        /// </summary>
        private void GenerateResultUI()
        {
            // 임시 부모 GameObject 생성
            GameObject tempParent = new GameObject("TempParent");
            CreateResultUI(tempParent);

            // 생성된 ResultUI를 독립적인 프리팹으로 만들기
            GameObject resultUI = tempParent.transform.GetChild(0).gameObject;
            resultUI.transform.SetParent(null);

            SaveAsPrefab(resultUI, "EventResultUI");

            // 임시 부모 삭제
            DestroyImmediate(tempParent);
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

            Debug.Log($"[EventUIGenerator] {name} 프리팹 저장 완료: {path}");
        }
    }
}