using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
namespace Core.Editor
{
    /// <summary>
    /// 로딩 UI 프리팹을 자동으로 생성하는 에디터 스크립트
    /// </summary>
    public class LoadingUIPrefabGenerator
    {
        [MenuItem("Tools/Create Loading UI Prefab")]
        public static void CreateLoadingUIPrefab()
        {
            // 1. 메인 캔버스 생성
            GameObject loadingCanvas = new GameObject("LoadingCanvas");
            Canvas canvas = loadingCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = loadingCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            loadingCanvas.AddComponent<GraphicRaycaster>();
            
            // CanvasGroup 추가 (페이드 효과용)
            CanvasGroup canvasGroup = loadingCanvas.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            
            // 2. 배경 패널 생성
            GameObject backgroundPanel = new GameObject("BackgroundPanel");
            backgroundPanel.transform.SetParent(loadingCanvas.transform, false);
            
            RectTransform bgRect = backgroundPanel.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            Image bgImage = backgroundPanel.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.8f); // 반투명 검정
            
            // 3. 로딩 컨테이너 생성
            GameObject loadingContainer = new GameObject("LoadingContainer");
            loadingContainer.transform.SetParent(loadingCanvas.transform, false);
            
            RectTransform containerRect = loadingContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(400, 200);
            
            // 4. 로딩 텍스트 생성
            GameObject loadingText = new GameObject("LoadingText");
            loadingText.transform.SetParent(loadingContainer.transform, false);
            
            RectTransform textRect = loadingText.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.7f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI textComponent = loadingText.AddComponent<TextMeshProUGUI>();
            textComponent.text = "로딩 중...";
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.fontStyle = FontStyles.Bold;
            
            // 5. 진행률 바 생성
            GameObject progressBar = new GameObject("ProgressBar");
            progressBar.transform.SetParent(loadingContainer.transform, false);
            
            RectTransform progressRect = progressBar.AddComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.1f, 0.4f);
            progressRect.anchorMax = new Vector2(0.9f, 0.6f);
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = Vector2.zero;
            
            Slider slider = progressBar.AddComponent<Slider>();
            
            // 진행률 바 배경
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(progressBar.transform, false);
            
            RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.offsetMin = Vector2.zero;
            sliderBgRect.offsetMax = Vector2.zero;
            
            Image sliderBgImage = sliderBg.AddComponent<Image>();
            sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // 진행률 바 Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(progressBar.transform, false);
            
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;
            
            // 진행률 바 Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // 초록색
            
            // 슬라이더 설정
            slider.targetGraphic = fillImage;
            slider.fillRect = fillRect;
            slider.value = 0f;
            
            // 6. 로딩 스피너 생성
            GameObject loadingSpinner = new GameObject("LoadingSpinner");
            loadingSpinner.transform.SetParent(loadingContainer.transform, false);
            
            RectTransform spinnerRect = loadingSpinner.AddComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.45f, 0.1f);
            spinnerRect.anchorMax = new Vector2(0.55f, 0.3f);
            spinnerRect.offsetMin = Vector2.zero;
            spinnerRect.offsetMax = Vector2.zero;
            
            Image spinnerImage = loadingSpinner.AddComponent<Image>();
            
            // 기본 스피너 이미지 생성 (간단한 원형)
            Texture2D spinnerTexture = CreateSpinnerTexture();
            Sprite spinnerSprite = Sprite.Create(spinnerTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            spinnerImage.sprite = spinnerSprite;
            spinnerImage.color = Color.white;
            
            // 7. 프리팹으로 저장
            string prefabPath = "Assets/Prefabs/UI/LoadingUIPrefab.prefab";
            
            // 폴더가 없으면 생성
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));
            
            // 프리팹 생성
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(loadingCanvas, prefabPath);
            
            // 생성된 오브젝트 삭제
            Object.DestroyImmediate(loadingCanvas);
            
            // 생성된 프리팹 선택
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            Debug.Log($"로딩 UI 프리팹이 생성되었습니다: {prefabPath}");
        }
        
        /// <summary>
        /// 간단한 스피너 텍스처 생성
        /// </summary>
        private static Texture2D CreateSpinnerTexture()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.4f;
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    
                    if (distance <= radius && distance >= radius * 0.7f)
                    {
                        // 그라데이션 효과
                        float angle = Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg;
                        float alpha = Mathf.Clamp01((angle + 180f) / 360f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            
            texture.Apply();
            return texture;
        }
    }
}
#endif 