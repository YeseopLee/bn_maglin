using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Maglin.Relics
{
    /// <summary>
    /// 유물 UI 컨트롤러 - 마우스 호버 시 툴팁 표시 기능
    /// </summary>
    public class RelicUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("유물 정보")]
        [SerializeField] private RelicSO relicData;

        [Header("UI 컴포넌트들")]
        [SerializeField] private Image relicImage;
        [SerializeField] private Canvas tooltipCanvas;
        [SerializeField] private TextMeshProUGUI relicNameText;
        [SerializeField] private TextMeshProUGUI relicDescriptionText;

        [Header("툴팁 설정")]
        [SerializeField] private float tooltipDelay = 0.5f;
        [SerializeField] private Vector2 tooltipOffset = new Vector2(10, -10);

        private bool isHovering = false;
        private float hoverTimer = 0f;
        private RectTransform tooltipRect;
        private Camera uiCamera;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupRelic();
            uiCamera = Camera.main; // UI 카메라 참조
        }

        private void Update()
        {
            HandleTooltipDelay();
            // UpdateTooltipPosition() 제거 - 이제 고정 위치 사용
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            if (relicImage == null)
                relicImage = GetComponent<Image>();

            if (tooltipCanvas == null)
            {
                // 먼저 직접 이름으로 찾기
                Transform tooltipTransform = transform.Find("RelicTooltip");
                if (tooltipTransform != null)
                {
                    tooltipCanvas = tooltipTransform.GetComponent<Canvas>();
                    Debug.Log($"[RelicUI] RelicTooltip 직접 찾기 성공: {tooltipCanvas != null}");
                }

                // 그래도 없으면 GetComponentInChildren 사용 (비활성화된 오브젝트도 포함)
                if (tooltipCanvas == null)
                {
                    tooltipCanvas = GetComponentInChildren<Canvas>(true); // includeInactive = true
                    Debug.Log($"[RelicUI] GetComponentInChildren으로 찾기: {tooltipCanvas != null}");
                }

                // 여전히 없으면 하위 오브젝트 디버그
                if (tooltipCanvas == null)
                {
                    Debug.LogWarning($"[RelicUI] 현재 오브젝트: {gameObject.name}, 자식 수: {transform.childCount}");
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        Debug.LogWarning($"[RelicUI] 자식 {i}: {child.name} (Canvas: {child.GetComponent<Canvas>() != null})");
                    }
                }
            }

            if (tooltipCanvas != null)
            {
                tooltipRect = tooltipCanvas.GetComponent<RectTransform>();

                // 툴팁 초기 상태 설정
                tooltipCanvas.enabled = false;
                tooltipCanvas.gameObject.SetActive(true); // GameObject는 활성화, Canvas 컴포넌트만 비활성화

                // 툴팁 내부의 텍스트 컴포넌트들 찾기 (비활성화된 오브젝트도 포함)
                TextMeshProUGUI[] texts = tooltipCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                Debug.Log($"[RelicUI] 발견된 TextMeshProUGUI 컴포넌트 수: {texts.Length}");

                foreach (var text in texts)
                {
                    Debug.Log($"[RelicUI] 텍스트 컴포넌트: {text.gameObject.name} - '{text.text}'");
                    if (text.gameObject.name.Contains("Name"))
                    {
                        relicNameText = text;
                        Debug.Log($"[RelicUI] RelicNameText 설정: {text.gameObject.name}");
                    }
                    else if (text.gameObject.name.Contains("Description"))
                    {
                        relicDescriptionText = text;
                        Debug.Log($"[RelicUI] RelicDescriptionText 설정: {text.gameObject.name}");
                    }
                }

                Debug.Log($"[RelicUI] 툴팁 Canvas 설정 완료 - GameObject: {tooltipCanvas.gameObject.name}, Active: {tooltipCanvas.gameObject.activeSelf}, Enabled: {tooltipCanvas.enabled}");
            }
            else
            {
                Debug.LogWarning($"[RelicUI] 툴팁 Canvas를 찾을 수 없습니다! GameObject: {gameObject.name}");
            }

            Debug.Log($"[RelicUI] 컴포넌트 초기화 완료 - Image: {relicImage != null}, Tooltip: {tooltipCanvas != null}, NameText: {relicNameText != null}, DescText: {relicDescriptionText != null}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 유물 데이터 설정
        /// </summary>
        public void SetRelicData(RelicSO newRelicData)
        {
            relicData = newRelicData;
            SetupRelic();
            Debug.Log($"[RelicUI] 유물 데이터 설정됨: {relicData?.RelicName}");
        }

        /// <summary>
        /// 유물 정보를 즉시 표시/숨김
        /// </summary>
        public void ShowTooltip(bool show)
        {
            if (tooltipCanvas != null)
            {
                if (show)
                {
                    // 툴팁을 최상위 렌더링 순서로 설정
                    SetTooltipRenderingOrder();

                    // 툴팁 위치를 유물 중앙 기준으로 고정 설정
                    PositionTooltipFixed();

                    // 툴팁 배경이 마우스 이벤트를 받지 않도록 설정
                    SetTooltipRaycastTarget(false);
                }

                tooltipCanvas.enabled = show;
                tooltipCanvas.gameObject.SetActive(show);
                Debug.Log($"[RelicUI] 툴팁 {(show ? "표시" : "숨김")} - Canvas enabled: {tooltipCanvas.enabled}, GameObject active: {tooltipCanvas.gameObject.activeSelf}");
            }
            else
            {
                Debug.LogWarning($"[RelicUI] 툴팁 Canvas가 null입니다! {(show ? "표시" : "숨김")} 요청이 무시됩니다.");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 유물 설정 적용
        /// </summary>
        private void SetupRelic()
        {
            if (relicData == null) return;

            // 유물 이미지 설정
            if (relicImage != null && relicData.Image != null)
            {
                relicImage.sprite = relicData.Image;
                Debug.Log($"[RelicUI] 유물 이미지 설정: {relicData.Image.name}");
            }

            // 툴팁 텍스트 설정
            UpdateTooltipText();
        }

        /// <summary>
        /// 툴팁 텍스트 업데이트
        /// </summary>
        private void UpdateTooltipText()
        {
            if (relicData == null) return;

            if (relicNameText != null)
            {
                relicNameText.text = relicData.RelicName;
            }

            if (relicDescriptionText != null)
            {
                string description = relicData.Description;

                // 효과값 정보 추가
                if (relicData.EffectType != RelicEffectType.CustomEffect)
                {
                    string effectInfo = relicData.GetEffectValueString();
                    description += $"\n\n<color=yellow>효과: {effectInfo}</color>";
                }

                relicDescriptionText.text = description;
            }

            Debug.Log($"[RelicUI] 툴팁 텍스트 업데이트 완료");
        }

        /// <summary>
        /// 툴팁 딜레이 처리
        /// </summary>
        private void HandleTooltipDelay()
        {
            if (isHovering)
            {
                hoverTimer += Time.deltaTime;

                if (hoverTimer >= tooltipDelay && tooltipCanvas != null && !tooltipCanvas.enabled)
                {
                    Debug.Log($"[RelicUI] 툴팁 딜레이 완료, 표시 시도: {relicData?.RelicName}, 딜레이: {hoverTimer:F2}s");
                    ShowTooltip(true);
                }
            }
            else
            {
                if (hoverTimer > 0)
                {
                    hoverTimer = 0f;
                    ShowTooltip(false);
                }
            }
        }

        /// <summary>
        /// 툴팁 위치 업데이트 (마우스 위치에 따라)
        /// </summary>
        private void UpdateTooltipPosition()
        {
            if (tooltipCanvas == null || !tooltipCanvas.enabled || tooltipRect == null) return;

            Vector2 mousePosition = Input.mousePosition;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            // 툴팁이 화면을 벗어나지 않도록 위치 조정
            Vector2 tooltipSize = tooltipRect.sizeDelta;
            Vector2 adjustedPosition = mousePosition + tooltipOffset;

            // 오른쪽 경계 체크
            if (adjustedPosition.x + tooltipSize.x > screenSize.x)
            {
                adjustedPosition.x = mousePosition.x - tooltipSize.x - tooltipOffset.x;
            }

            // 위쪽 경계 체크
            if (adjustedPosition.y + tooltipSize.y > screenSize.y)
            {
                adjustedPosition.y = mousePosition.y - tooltipSize.y + tooltipOffset.y;
            }

            // 왼쪽과 아래쪽 경계 체크
            adjustedPosition.x = Mathf.Max(0, adjustedPosition.x);
            adjustedPosition.y = Mathf.Max(0, adjustedPosition.y);

            tooltipRect.position = adjustedPosition;
        }

        /// <summary>
        /// 툴팁 위치를 유물 중앙 기준으로 고정 설정 (오른쪽 아래)
        /// </summary>
        private void PositionTooltipFixed()
        {
            if (tooltipCanvas == null || tooltipRect == null) return;

            // 유물(RelicPrefab)의 월드 위치를 스크린 위치로 변환
            Vector3 relicWorldPosition = transform.position;
            Vector2 relicScreenPosition = RectTransformUtility.WorldToScreenPoint(null, relicWorldPosition);

            // 툴팁을 유물의 오른쪽 아래에 위치시킴
            Vector2 tooltipPosition = relicScreenPosition + new Vector2(70, -50); // 유물 중앙에서 우측하단으로 오프셋

            // 화면 경계 체크
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 tooltipSize = tooltipRect.sizeDelta;

            // 오른쪽 경계 체크
            if (tooltipPosition.x + tooltipSize.x > screenSize.x)
            {
                tooltipPosition.x = relicScreenPosition.x - tooltipSize.x - 10; // 유물 왼쪽에 표시
            }

            // 아래쪽 경계 체크 (툴팁이 화면 아래로 벗어나지 않도록)
            if (tooltipPosition.y - tooltipSize.y < 0)
            {
                tooltipPosition.y = relicScreenPosition.y + tooltipSize.y + 50; // 유물 위쪽에 표시
            }

            // 왼쪽과 위쪽 경계 체크
            tooltipPosition.x = Mathf.Max(10, tooltipPosition.x);
            tooltipPosition.y = Mathf.Min(screenSize.y - 10, tooltipPosition.y);

            tooltipRect.position = tooltipPosition;

            Debug.Log($"[RelicUI] 툴팁 고정 위치 설정 (우측하단): {tooltipPosition}, 유물 위치: {relicScreenPosition}");
        }

        /// <summary>
        /// 툴팁 배경의 Raycast Target 설정
        /// </summary>
        private void SetTooltipRaycastTarget(bool enableRaycast)
        {
            if (tooltipCanvas == null) return;

            // 툴팁 패널과 그 하위 UI 요소들의 raycast target을 비활성화
            var tooltipPanel = tooltipCanvas.transform.Find("TooltipPanel");
            if (tooltipPanel != null)
            {
                var panelGraphic = tooltipPanel.GetComponent<UnityEngine.UI.Graphic>();
                if (panelGraphic != null)
                {
                    panelGraphic.raycastTarget = enableRaycast;
                    Debug.Log($"[RelicUI] 툴팁 패널 RaycastTarget 설정: {enableRaycast}");
                }

                // 텍스트 컴포넌트들도 raycast target 비활성화
                var textComponents = tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>();
                foreach (var graphic in textComponents)
                {
                    graphic.raycastTarget = enableRaycast;
                }

                Debug.Log($"[RelicUI] 툴팁 하위 컴포넌트 {textComponents.Length}개의 RaycastTarget 설정: {enableRaycast}");
            }
        }

        /// <summary>
        /// 툴팁을 최상위 렌더링 순서로 설정
        /// </summary>
        private void SetTooltipRenderingOrder()
        {
            if (tooltipCanvas == null) return;

            // Canvas의 sortingOrder를 매우 높은 값으로 설정하여 다른 모든 UI 위에 표시
            tooltipCanvas.sortingOrder = 9999;

            // OverrideSorting을 true로 설정하여 상위 Canvas의 sorting을 무시
            tooltipCanvas.overrideSorting = true;

            // PlaneDistance를 낮게 설정하여 더 앞쪽에 렌더링
            if (tooltipCanvas.renderMode == RenderMode.ScreenSpaceCamera || tooltipCanvas.renderMode == RenderMode.WorldSpace)
            {
                tooltipCanvas.planeDistance = 0.1f;
            }

            Debug.Log($"[RelicUI] 툴팁 렌더링 순서 설정: sortingOrder={tooltipCanvas.sortingOrder}, overrideSorting={tooltipCanvas.overrideSorting}");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 마우스 포인터가 유물 위에 올라왔을 때
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            hoverTimer = 0f;
            Debug.Log($"[RelicUI] 마우스 호버 시작: {relicData?.RelicName}");
        }

        /// <summary>
        /// 마우스 포인터가 유물에서 벗어났을 때
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            hoverTimer = 0f;
            ShowTooltip(false);
            Debug.Log($"[RelicUI] 마우스 호버 종료: {relicData?.RelicName}");
        }

        #endregion

        #region Editor Methods

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 컴포넌트가 추가되었을 때 자동으로 참조 설정
        /// </summary>
        private void Reset()
        {
            InitializeComponents();
        }
        
        /// <summary>
        /// 에디터에서 값이 변경되었을 때
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying && relicData != null)
            {
                SetupRelic();
            }
        }
#endif

        #endregion
    }
}
