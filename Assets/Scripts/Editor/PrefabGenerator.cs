using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Maglin.Editor
{
    /// <summary>
    /// 몬스터 및 카드 프리팹을 자동 생성하는 에디터 도구
    /// </summary>
    public class PrefabGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Prefabs")]
        public static void ShowWindow()
        {
            GetWindow<PrefabGenerator>("Prefab Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("프리팹 생성 도구", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // 현재 상태 정보 표시
            EditorGUILayout.LabelField("현재 씬:", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            EditorGUILayout.LabelField("Prefabs 폴더 존재:", System.IO.Directory.Exists("Assets/Prefabs").ToString());

            EditorGUILayout.Space();

            if (GUILayout.Button("몬스터 프리팹 생성", GUILayout.Height(30)))
            {
                Debug.Log("[PrefabGenerator] 몬스터 프리팹 생성 버튼 클릭됨");
                GenerateMonsterPrefab();
            }

            if (GUILayout.Button("카드 UI 프리팹 생성", GUILayout.Height(30)))
            {
                Debug.Log("[PrefabGenerator] 카드 UI 프리팹 생성 버튼 클릭됨");
                GenerateCardUIPrefab();
            }

            if (GUILayout.Button("유물 프리팹 생성", GUILayout.Height(30)))
            {
                Debug.Log("[PrefabGenerator] 유물 프리팹 생성 버튼 클릭됨");
                GenerateRelicPrefab();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("모든 프리팹 생성", GUILayout.Height(30)))
            {
                Debug.Log("[PrefabGenerator] 모든 프리팹 생성 버튼 클릭됨");
                GenerateAllPrefabs();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("동적으로 생성될 몬스터, 카드 UI, 유물 프리팹을 생성합니다.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("콘솔창을 확인하여 생성 과정을 모니터링하세요.", MessageType.Info);
        }

        /// <summary>
        /// 모든 프리팹 생성
        /// </summary>
        private void GenerateAllPrefabs()
        {
            GenerateMonsterPrefab();
            GenerateCardUIPrefab();
            GenerateRelicPrefab();
            Debug.Log("[PrefabGenerator] 모든 프리팹 생성 완료!");
        }

        /// <summary>
        /// 몬스터 프리팹 생성
        /// </summary>
        private void GenerateMonsterPrefab()
        {
            Debug.Log("[PrefabGenerator] 몬스터 프리팹 생성 시작...");

            GameObject monster = new GameObject("MonsterPrefab");

            // 몬스터 기본 컴포넌트들
            SpriteRenderer spriteRenderer = monster.AddComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white; // 기본값은 흰색 (원본 스프라이트 색상 유지)
            Debug.Log("[PrefabGenerator] SpriteRenderer 추가 완료");

            // 몬스터 스크립트 컴포넌트 추가
            monster.AddComponent<Maglin.Enemy.Enemy>();
            monster.AddComponent<Maglin.Enemy.EnemyAI>();
            Debug.Log("[PrefabGenerator] Enemy 컴포넌트 추가 완료");

            // 체력바 UI 생성
            CreateMonsterHealthBar(monster);
            Debug.Log("[PrefabGenerator] 체력바 생성 완료");

            // 타겟 마커 생성
            CreateTargetMarker(monster);
            Debug.Log("[PrefabGenerator] 타겟 마커 생성 완료");

            SaveAsPrefab(monster, "MonsterPrefab");
        }

        /// <summary>
        /// 몬스터 체력바 생성
        /// </summary>
        private void CreateMonsterHealthBar(GameObject parent)
        {
            GameObject healthBarCanvas = new GameObject("HealthBarCanvas");
            healthBarCanvas.transform.SetParent(parent.transform, false);

            Canvas canvas = healthBarCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            // CanvasScaler 설정 개선
            CanvasScaler scaler = healthBarCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // GraphicRaycaster 추가 (UI 상호작용을 위해)
            healthBarCanvas.AddComponent<GraphicRaycaster>();

            // Canvas 크기와 위치 설정 (WorldSpace에서 적절한 크기)
            RectTransform canvasRect = healthBarCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2f, 0.3f); // 월드 단위로 크기 설정
            canvasRect.localPosition = new Vector3(0, -0.6f, 0); // 몬스터 아래쪽에 위치

            // 체력바 배경
            GameObject healthBarBG = new GameObject("HealthBarBackground");
            healthBarBG.transform.SetParent(healthBarCanvas.transform, false);

            Image bgImage = healthBarBG.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // 반투명 검은색

            RectTransform bgRect = healthBarBG.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 체력바 
            GameObject healthBar = new GameObject("HealthBar");
            healthBar.transform.SetParent(healthBarBG.transform, false);

            Image healthImage = healthBar.AddComponent<Image>();
            healthImage.color = Color.green; // 기본은 초록색
            healthImage.type = Image.Type.Filled;
            healthImage.fillMethod = Image.FillMethod.Horizontal;

            RectTransform healthRect = healthBar.GetComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero;
            healthRect.anchorMax = Vector2.one;
            healthRect.offsetMin = new Vector2(2, 2); // 약간의 패딩
            healthRect.offsetMax = new Vector2(-2, -8); // 텍스트 공간 확보

            // 체력 텍스트
            GameObject healthText = new GameObject("HealthText");
            healthText.transform.SetParent(healthBarBG.transform, false);

            TextMeshProUGUI healthTMP = healthText.AddComponent<TextMeshProUGUI>();
            healthTMP.text = "100";
            healthTMP.fontSize = 24; // WorldSpace에서는 더 큰 폰트 크기 필요
            healthTMP.color = Color.white;
            healthTMP.alignment = TextAlignmentOptions.Center;
            healthTMP.fontStyle = FontStyles.Bold;

            RectTransform textRect = healthText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.3f); // 하단 30% 영역
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 타겟 마커 생성 (애니메이션 지원)
        /// </summary>
        private void CreateTargetMarker(GameObject parent)
        {
            GameObject targetMarker = new GameObject("TargetMarker");
            targetMarker.transform.SetParent(parent.transform, false);

            // SpriteRenderer 컴포넌트 추가
            SpriteRenderer markerSprite = targetMarker.AddComponent<SpriteRenderer>();
            markerSprite.color = Color.white; // 원본 스프라이트 색상 유지
            markerSprite.sortingOrder = 10; // 가장 위에 그려지도록

            // Animator 컴포넌트 추가 (애니메이션용)
            Animator animator = targetMarker.AddComponent<Animator>();
            // 런타임에 애니메이션 컨트롤러를 설정할 예정

            // 타겟 마커 애니메이션 스크립트 추가
            targetMarker.AddComponent<Maglin.Battle.TargetMarkerAnimator>();

            // 기본적으로 비활성화
            targetMarker.SetActive(false);

            // 마커 위치 조정
            targetMarker.transform.localPosition = new Vector3(0, 0.7f, 0);
            targetMarker.transform.localScale = new Vector3(1f, 1f, 1f); // 스프라이트 원본 크기로 시작
        }

        /// <summary>
        /// 카드 UI 프리팹 생성
        /// </summary>
        private void GenerateCardUIPrefab()
        {
            Debug.Log("[PrefabGenerator] 카드 UI 프리팹 생성 시작...");

            GameObject cardUI = new GameObject("CardUIPrefab");

            // 카드 배경
            Image cardBG = cardUI.AddComponent<Image>();
            cardBG.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            RectTransform cardRect = cardUI.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(80, 120);
            Debug.Log("[PrefabGenerator] 카드 기본 설정 완료");

            // 카드 레이아웃 그룹
            VerticalLayoutGroup layout = cardUI.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 2;
            Debug.Log("[PrefabGenerator] 레이아웃 그룹 설정 완료");

            // 카드 이름
            CreateCardText(cardUI, "CardName", "카드 이름", 10, Color.black, 20);

            // 카드 일러스트 영역
            CreateCardIllustration(cardUI);

            // 카드 비용
            CreateCardText(cardUI, "CardCost", "비용: 2", 8, Color.blue, 15);

            // 카드 설명
            CreateCardText(cardUI, "CardDescription", "카드 설명이 여기에 표시됩니다.", 7, Color.black, 40);

            // 카드 타입/속성 정보
            CreateCardText(cardUI, "CardInfo", "화염 | 공격", 6, Color.gray, 15);
            Debug.Log("[PrefabGenerator] 카드 텍스트 요소들 생성 완료");

            // 드래그 앤 드롭 컴포넌트 추가
            cardUI.AddComponent<Button>();
            Debug.Log("[PrefabGenerator] 버튼 컴포넌트 추가 완료");

            SaveAsPrefab(cardUI, "CardUIPrefab");
        }

        /// <summary>
        /// 카드 텍스트 생성
        /// </summary>
        private void CreateCardText(GameObject parent, string name, string text, int fontSize, Color color, float height)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = fontSize;
            textTMP.color = color;
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.enableWordWrapping = true;

            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
        }

        /// <summary>
        /// 카드 일러스트 영역 생성
        /// </summary>
        private void CreateCardIllustration(GameObject parent)
        {
            GameObject illustration = new GameObject("CardIllustration");
            illustration.transform.SetParent(parent.transform, false);

            Image illustImage = illustration.AddComponent<Image>();
            illustImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            LayoutElement layoutElement = illustration.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleHeight = 1;
        }

        /// <summary>
        /// 유물 프리팹 생성
        /// </summary>
        private void GenerateRelicPrefab()
        {
            Debug.Log("[PrefabGenerator] 유물 프리팹 생성 시작...");

            GameObject relic = new GameObject("RelicPrefab");

            // 유물 이미지
            Image relicImage = relic.AddComponent<Image>();
            relicImage.color = Color.white;
            relicImage.preserveAspect = true; // 종횡비 유지
            relicImage.raycastTarget = true; // 마우스 이벤트 수신

            RectTransform relicRect = relic.GetComponent<RectTransform>();
            relicRect.sizeDelta = new Vector2(64, 64); // 기본 크기 64x64
            Debug.Log("[PrefabGenerator] 유물 이미지 설정 완료");

            // 유물 툴팁 캔버스 생성 (호버 시 표시될 정보)
            CreateRelicTooltip(relic);
            Debug.Log("[PrefabGenerator] 유물 툴팁 생성 완료");

            // Button 컴포넌트 추가 (클릭 이벤트용)
            Button relicButton = relic.AddComponent<Button>();
            relicButton.transition = Selectable.Transition.None; // 버튼 시각 효과 없음
            Debug.Log("[PrefabGenerator] Button 컴포넌트 추가 완료");

            // 유물 컨트롤러 스크립트 추가 (Button 컴포넌트 후에 추가)
            Maglin.Relics.RelicUI relicUI = relic.AddComponent<Maglin.Relics.RelicUI>();

            // RelicUI 컴포넌트의 참조 설정
            SetupRelicUIReferences(relicUI, relic, relicImage);
            Debug.Log("[PrefabGenerator] RelicUI 컴포넌트 추가 및 참조 설정 완료");

            SaveAsPrefab(relic, "RelicPrefab");
        }

        /// <summary>
        /// 유물 툴팁 생성 (호버 시 표시될 정보창)
        /// </summary>
        private void CreateRelicTooltip(GameObject parent)
        {
            GameObject tooltip = new GameObject("RelicTooltip");
            tooltip.transform.SetParent(parent.transform, false);

            // Canvas 컴포넌트 추가 (Screen Space Overlay)
            Canvas tooltipCanvas = tooltip.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.sortingOrder = 1000; // 가장 위에 표시
            tooltipCanvas.enabled = false; // 기본적으로 비활성화

            // CanvasScaler 추가
            CanvasScaler scaler = tooltip.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster 추가 (UI 이벤트 처리용)
            tooltip.AddComponent<GraphicRaycaster>();

            // 툴팁 배경 패널
            GameObject tooltipPanel = new GameObject("TooltipPanel");
            tooltipPanel.transform.SetParent(tooltip.transform, false);

            Image panelBG = tooltipPanel.AddComponent<Image>();
            panelBG.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 반투명 검은 배경

            RectTransform panelRect = tooltipPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(280, 120);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            // 레이아웃 그룹 추가
            VerticalLayoutGroup layout = tooltipPanel.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;

            // 유물 이름 텍스트
            CreateTooltipText(tooltipPanel, "RelicNameText", "유물 이름", 16, Color.yellow, FontStyles.Bold, 25);

            // 유물 설명 텍스트
            CreateTooltipText(tooltipPanel, "RelicDescriptionText", "유물 설명이 여기에 표시됩니다.\n효과와 사용법을 설명합니다.", 12, Color.white, FontStyles.Normal, 70);

            // 기본적으로 툴팁은 비활성화
            tooltip.SetActive(false);
        }

        /// <summary>
        /// RelicUI 컴포넌트의 참조 설정
        /// </summary>
        private void SetupRelicUIReferences(Maglin.Relics.RelicUI relicUI, GameObject relic, Image relicImage)
        {
            // 리플렉션을 사용하여 private 필드에 접근
            var relicUIType = typeof(Maglin.Relics.RelicUI);

            // relicImage 필드 설정
            var relicImageField = relicUIType.GetField("relicImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (relicImageField != null)
            {
                relicImageField.SetValue(relicUI, relicImage);
                Debug.Log("[PrefabGenerator] RelicImage 참조 설정 완료");
            }

            // tooltipCanvas 찾기 및 설정
            Canvas tooltipCanvas = relic.GetComponentInChildren<Canvas>();
            var tooltipCanvasField = relicUIType.GetField("tooltipCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tooltipCanvasField != null && tooltipCanvas != null)
            {
                tooltipCanvasField.SetValue(relicUI, tooltipCanvas);
                Debug.Log("[PrefabGenerator] TooltipCanvas 참조 설정 완료");
            }

            // 텍스트 컴포넌트들 찾기 및 설정
            TextMeshProUGUI[] texts = relic.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.gameObject.name.Contains("Name"))
                {
                    var nameField = relicUIType.GetField("relicNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (nameField != null)
                    {
                        nameField.SetValue(relicUI, text);
                        Debug.Log("[PrefabGenerator] RelicNameText 참조 설정 완료");
                    }
                }
                else if (text.gameObject.name.Contains("Description"))
                {
                    var descField = relicUIType.GetField("relicDescriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (descField != null)
                    {
                        descField.SetValue(relicUI, text);
                        Debug.Log("[PrefabGenerator] RelicDescriptionText 참조 설정 완료");
                    }
                }
            }
        }

        /// <summary>
        /// 툴팁 텍스트 생성
        /// </summary>
        private void CreateTooltipText(GameObject parent, string name, string text, int fontSize, Color color, FontStyles fontStyle, float height)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = fontSize;
            textTMP.color = color;
            textTMP.fontStyle = fontStyle;
            textTMP.alignment = TextAlignmentOptions.TopLeft;
            textTMP.enableWordWrapping = true;
            textTMP.overflowMode = TextOverflowModes.Overflow;

            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
        }

        /// <summary>
        /// 프리팹으로 저장
        /// </summary>
        private void SaveAsPrefab(GameObject obj, string name)
        {
            string path = $"Assets/Prefabs/{name}.prefab";

            // 디렉토리 생성
            if (!System.IO.Directory.Exists("Assets/Prefabs"))
            {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
                AssetDatabase.Refresh();
            }

            try
            {
                // 기존 프리팹이 있으면 삭제
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                // 프리팹 저장
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);

                if (prefab != null)
                {
                    Debug.Log($"[PrefabGenerator] ✅ {name} 프리팹 저장 완료: {path}");
                }
                else
                {
                    Debug.LogError($"[PrefabGenerator] ❌ {name} 프리팹 저장 실패!");
                }

                // 씬에서 제거
                DestroyImmediate(obj);

                // AssetDatabase 갱신
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PrefabGenerator] {name} 프리팹 저장 중 오류: {e.Message}");
                DestroyImmediate(obj);
            }
        }
    }
}