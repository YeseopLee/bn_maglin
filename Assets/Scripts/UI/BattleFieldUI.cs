using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using Maglin.Battle;

namespace Maglin.UI
{
    /// <summary>
    /// 개별 필드 위치의 UI 표현
    /// </summary>
    [System.Serializable]
    public class FieldPositionUI
    {
        [Header("UI 컴포넌트")]
        public GameObject positionObject;      // 위치 표시 오브젝트
        public Image backgroundImage;          // 배경 이미지
        public Image highlightImage;           // 하이라이트 이미지
        public Text positionIndexText;         // 위치 번호 텍스트
        public Transform objectContainer;      // 배치된 객체의 컨테이너

        [Header("상태")]
        public int positionIndex;              // 위치 번호
        public bool isOccupied;                // 점유 상태
        public PositionObjectType objectType;  // 점유 객체 타입

        [Header("애니메이션")]
        public CanvasGroup canvasGroup;        // 페이드 애니메이션용
        public RectTransform rectTransform;    // 위치 애니메이션용

        /// <summary>
        /// FieldPositionUI 초기화
        /// </summary>
        public void Initialize(int index)
        {
            positionIndex = index;
            isOccupied = false;
            objectType = PositionObjectType.None;

            if (positionIndexText != null)
                positionIndexText.text = index.ToString();

            if (canvasGroup == null && positionObject != null)
                canvasGroup = positionObject.GetComponent<CanvasGroup>();

            if (rectTransform == null && positionObject != null)
                rectTransform = positionObject.GetComponent<RectTransform>();

            SetHighlight(false);
        }

        /// <summary>
        /// 하이라이트 설정
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            if (highlightImage != null)
                highlightImage.gameObject.SetActive(highlight);
        }

        /// <summary>
        /// 배경색 설정
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            if (backgroundImage != null)
                backgroundImage.color = color;
        }

        /// <summary>
        /// 점유 상태 업데이트
        /// </summary>
        public void UpdateOccupationStatus(bool occupied, PositionObjectType type)
        {
            isOccupied = occupied;
            objectType = type;

            // 점유 상태에 따른 시각적 변화
            Color backgroundColor = GetColorForObjectType(type);
            SetBackgroundColor(backgroundColor);
        }

        /// <summary>
        /// 객체 타입에 따른 색상 반환
        /// </summary>
        private Color GetColorForObjectType(PositionObjectType type)
        {
            return type switch
            {
                PositionObjectType.Player => new Color(0.3f, 0.6f, 1f, 0.8f),      // 파란색
                PositionObjectType.Enemy => new Color(1f, 0.3f, 0.3f, 0.8f),       // 빨간색
                PositionObjectType.Object => new Color(0.8f, 0.8f, 0.3f, 0.8f),    // 노란색
                _ => new Color(0.5f, 0.5f, 0.5f, 0.3f)                             // 회색
            };
        }
    }

    /// <summary>
    /// 10칸 전투 필드의 시각적 표현을 담당하는 UI 클래스
    /// </summary>
    public class BattleFieldUI : MonoBehaviour
    {
        #region Fields
        [Header("UI 설정")]
        [SerializeField] private GameObject fieldPositionPrefab;  // 필드 위치 프리팹
        [SerializeField] private Transform fieldContainer;        // 필드 컨테이너
        [SerializeField] private GridLayoutGroup gridLayout;     // 그리드 레이아웃

        [Header("시각적 설정")]
        [SerializeField] private float positionSpacing = 100f;   // 위치 간 간격
        [SerializeField] private Vector2 positionSize = new Vector2(80f, 80f); // 위치 크기
        [SerializeField] private bool showPositionNumbers = true; // 위치 번호 표시

        [Header("애니메이션 설정")]
        [SerializeField] private float highlightDuration = 0.3f;  // 하이라이트 애니메이션 시간
        [SerializeField] private float objectPlaceDuration = 0.5f; // 객체 배치 애니메이션 시간

        [Header("색상 설정")]
        [SerializeField] private Color playerStartColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color enemySpawnColor = new Color(1f, 0.3f, 0.3f, 0.3f);
        [SerializeField] private Color neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0f, 0.8f);

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 필드 위치 UI들
        private FieldPositionUI[] fieldPositionUIs;
        private BattleField battleField;

        // 애니메이션 관련
        private Dictionary<int, Coroutine> activeAnimations = new Dictionary<int, Coroutine>();

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 필드 위치 UI 배열 (읽기 전용)
        /// </summary>
        public IReadOnlyList<FieldPositionUI> FieldPositionUIs => System.Array.AsReadOnly(fieldPositionUIs);
        #endregion

        #region Unity Events
        private void Awake()
        {
            battleField = BattleField.Instance;
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            SetupFieldContainer();
            CreateFieldPositionUIs();
            UpdateFieldDisplay();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[BattleFieldUI] 초기화 완료");
        }

        /// <summary>
        /// 특정 위치 하이라이트
        /// </summary>
        public void HighlightPosition(int positionIndex, bool highlight)
        {
            if (!IsValidPositionIndex(positionIndex)) return;

            fieldPositionUIs[positionIndex].SetHighlight(highlight);

            if (highlight)
            {
                StartHighlightAnimation(positionIndex);
            }
            else
            {
                StopHighlightAnimation(positionIndex);
            }
        }

        /// <summary>
        /// 다중 위치 하이라이트
        /// </summary>
        public void HighlightPositions(List<int> positionIndices, bool highlight)
        {
            foreach (int index in positionIndices)
            {
                HighlightPosition(index, highlight);
            }
        }

        /// <summary>
        /// 공격 범위 하이라이트
        /// </summary>
        public void HighlightAttackRange(int centerPosition, int range)
        {
            if (battleField == null) return;

            // 모든 하이라이트 제거
            ClearAllHighlights();

            // 범위 내 위치들 하이라이트
            var positionsInRange = battleField.GetPositionsInRange(centerPosition, range);
            HighlightPositions(positionsInRange, true);
        }

        /// <summary>
        /// 모든 하이라이트 제거
        /// </summary>
        public void ClearAllHighlights()
        {
            if (fieldPositionUIs == null) return;

            for (int i = 0; i < fieldPositionUIs.Length; i++)
            {
                HighlightPosition(i, false);
            }
        }

        /// <summary>
        /// 필드 표시 업데이트
        /// </summary>
        public void UpdateFieldDisplay()
        {
            if (battleField == null || fieldPositionUIs == null) return;

            var fieldPositions = battleField.FieldPositions;

            for (int i = 0; i < fieldPositions.Count && i < fieldPositionUIs.Length; i++)
            {
                var fieldPosition = fieldPositions[i];
                var positionUI = fieldPositionUIs[i];

                // 점유 상태 업데이트
                positionUI.UpdateOccupationStatus(fieldPosition.IsOccupied, fieldPosition.ObjectType);

                // 기본 배경색 설정 (점유되지 않은 경우)
                if (!fieldPosition.IsOccupied)
                {
                    if (fieldPosition.IsPlayerStartPosition)
                        positionUI.SetBackgroundColor(playerStartColor);
                    else if (fieldPosition.IsEnemySpawnPosition)
                        positionUI.SetBackgroundColor(enemySpawnColor);
                    else
                        positionUI.SetBackgroundColor(neutralColor);
                }
            }
        }

        /// <summary>
        /// 특정 위치에 객체 배치 애니메이션
        /// </summary>
        public void AnimateObjectPlacement(int positionIndex, GameObject obj)
        {
            if (!IsValidPositionIndex(positionIndex) || obj == null) return;

            StartCoroutine(ObjectPlacementAnimation(positionIndex, obj));
        }

        /// <summary>
        /// 객체 이동 애니메이션
        /// </summary>
        public void AnimateObjectMovement(GameObject obj, int fromPosition, int toPosition)
        {
            if (obj == null || !IsValidPositionIndex(fromPosition) || !IsValidPositionIndex(toPosition))
                return;

            StartCoroutine(ObjectMovementAnimation(obj, fromPosition, toPosition));
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 필드 컨테이너 설정
        /// </summary>
        private void SetupFieldContainer()
        {
            if (fieldContainer == null)
            {
                GameObject container = new GameObject("FieldContainer");
                container.transform.SetParent(transform);
                fieldContainer = container.transform;

                // RectTransform 설정
                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.sizeDelta = Vector2.zero;
                containerRect.anchoredPosition = Vector2.zero;
            }

            // GridLayoutGroup 설정
            if (gridLayout == null)
            {
                gridLayout = fieldContainer.GetComponent<GridLayoutGroup>();
                if (gridLayout == null)
                {
                    gridLayout = fieldContainer.gameObject.AddComponent<GridLayoutGroup>();
                }
            }

            gridLayout.cellSize = positionSize;
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayout.constraintCount = 1; // 한 줄로 배치
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
        }

        /// <summary>
        /// 필드 위치 UI들 생성
        /// </summary>
        private void CreateFieldPositionUIs()
        {
            if (battleField == null) return;

            int fieldLength = battleField.FieldLength;
            fieldPositionUIs = new FieldPositionUI[fieldLength];

            for (int i = 0; i < fieldLength; i++)
            {
                GameObject positionObj = CreateFieldPositionObject(i);
                FieldPositionUI positionUI = SetupFieldPositionUI(positionObj, i);
                fieldPositionUIs[i] = positionUI;
            }

            if (debugMode)
                Debug.Log($"[BattleFieldUI] {fieldLength}개의 위치 UI 생성 완료");
        }

        /// <summary>
        /// 필드 위치 오브젝트 생성
        /// </summary>
        private GameObject CreateFieldPositionObject(int index)
        {
            GameObject positionObj;

            if (fieldPositionPrefab != null)
            {
                positionObj = Instantiate(fieldPositionPrefab, fieldContainer);
            }
            else
            {
                // 기본 위치 오브젝트 생성
                positionObj = new GameObject($"FieldPosition_{index}");
                positionObj.transform.SetParent(fieldContainer);

                // UI 컴포넌트 추가
                var rectTransform = positionObj.AddComponent<RectTransform>();
                rectTransform.sizeDelta = positionSize;

                var backgroundImage = positionObj.AddComponent<Image>();
                backgroundImage.color = neutralColor;

                // 위치 번호 텍스트 추가
                if (showPositionNumbers)
                {
                    GameObject textObj = new GameObject("PositionText");
                    textObj.transform.SetParent(positionObj.transform);

                    var textRect = textObj.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;
                    textRect.anchoredPosition = Vector2.zero;

                    var text = textObj.AddComponent<Text>();
                    text.text = index.ToString();
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 16;
                    text.color = Color.white;
                    text.alignment = TextAnchor.MiddleCenter;
                }

                // 하이라이트 이미지 추가
                GameObject highlightObj = new GameObject("Highlight");
                highlightObj.transform.SetParent(positionObj.transform);

                var highlightRect = highlightObj.AddComponent<RectTransform>();
                highlightRect.anchorMin = Vector2.zero;
                highlightRect.anchorMax = Vector2.one;
                highlightRect.sizeDelta = Vector2.zero;
                highlightRect.anchoredPosition = Vector2.zero;

                var highlightImage = highlightObj.AddComponent<Image>();
                highlightImage.color = highlightColor;
                highlightObj.SetActive(false);

                // 객체 컨테이너 추가
                GameObject containerObj = new GameObject("ObjectContainer");
                containerObj.transform.SetParent(positionObj.transform);

                var containerRect = containerObj.AddComponent<RectTransform>();
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.sizeDelta = Vector2.zero;
                containerRect.anchoredPosition = Vector2.zero;
            }

            return positionObj;
        }

        /// <summary>
        /// 필드 위치 UI 설정
        /// </summary>
        private FieldPositionUI SetupFieldPositionUI(GameObject positionObj, int index)
        {
            FieldPositionUI positionUI = new FieldPositionUI();
            positionUI.positionObject = positionObj;
            positionUI.backgroundImage = positionObj.GetComponent<Image>();
            positionUI.rectTransform = positionObj.GetComponent<RectTransform>();

            // 하이라이트 이미지 찾기
            Transform highlightTransform = positionObj.transform.Find("Highlight");
            if (highlightTransform != null)
                positionUI.highlightImage = highlightTransform.GetComponent<Image>();

            // 위치 번호 텍스트 찾기
            Transform textTransform = positionObj.transform.Find("PositionText");
            if (textTransform != null)
                positionUI.positionIndexText = textTransform.GetComponent<Text>();

            // 객체 컨테이너 찾기
            Transform containerTransform = positionObj.transform.Find("ObjectContainer");
            if (containerTransform != null)
                positionUI.objectContainer = containerTransform;

            // CanvasGroup 추가
            positionUI.canvasGroup = positionObj.GetComponent<CanvasGroup>();
            if (positionUI.canvasGroup == null)
                positionUI.canvasGroup = positionObj.AddComponent<CanvasGroup>();

            positionUI.Initialize(index);
            return positionUI;
        }

        /// <summary>
        /// 하이라이트 애니메이션 시작
        /// </summary>
        private void StartHighlightAnimation(int positionIndex)
        {
            StopHighlightAnimation(positionIndex);

            Coroutine animation = StartCoroutine(HighlightPulseAnimation(positionIndex));
            activeAnimations[positionIndex] = animation;
        }

        /// <summary>
        /// 하이라이트 애니메이션 중지
        /// </summary>
        private void StopHighlightAnimation(int positionIndex)
        {
            if (activeAnimations.TryGetValue(positionIndex, out Coroutine animation))
            {
                if (animation != null)
                    StopCoroutine(animation);
                activeAnimations.Remove(positionIndex);
            }
        }

        /// <summary>
        /// 하이라이트 펄스 애니메이션
        /// </summary>
        private IEnumerator HighlightPulseAnimation(int positionIndex)
        {
            FieldPositionUI positionUI = fieldPositionUIs[positionIndex];
            Image highlightImage = positionUI.highlightImage;

            if (highlightImage == null) yield break;

            while (true)
            {
                // 페이드 인
                float elapsed = 0f;
                Color startColor = highlightImage.color;
                startColor.a = 0.3f;
                Color endColor = highlightImage.color;
                endColor.a = 0.8f;

                while (elapsed < highlightDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / highlightDuration;
                    highlightImage.color = Color.Lerp(startColor, endColor, t);
                    yield return null;
                }

                // 페이드 아웃
                elapsed = 0f;
                startColor = highlightImage.color;
                endColor.a = 0.3f;

                while (elapsed < highlightDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / highlightDuration;
                    highlightImage.color = Color.Lerp(startColor, endColor, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 객체 배치 애니메이션
        /// </summary>
        private IEnumerator ObjectPlacementAnimation(int positionIndex, GameObject obj)
        {
            FieldPositionUI positionUI = fieldPositionUIs[positionIndex];

            // 스케일 애니메이션
            Vector3 originalScale = obj.transform.localScale;
            obj.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < objectPlaceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / objectPlaceDuration;
                float scaleFactor = Mathf.Lerp(0f, 1f, Mathf.Sqrt(t)); // 이징 효과
                obj.transform.localScale = originalScale * scaleFactor;
                yield return null;
            }

            obj.transform.localScale = originalScale;
        }

        /// <summary>
        /// 객체 이동 애니메이션
        /// </summary>
        private IEnumerator ObjectMovementAnimation(GameObject obj, int fromPosition, int toPosition)
        {
            FieldPositionUI fromUI = fieldPositionUIs[fromPosition];
            FieldPositionUI toUI = fieldPositionUIs[toPosition];

            Vector3 startPos = fromUI.positionObject.transform.position;
            Vector3 endPos = toUI.positionObject.transform.position;

            float elapsed = 0f;
            while (elapsed < objectPlaceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / objectPlaceDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                obj.transform.position = Vector3.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            obj.transform.position = endPos;
        }

        /// <summary>
        /// 유효한 위치 인덱스인지 확인
        /// </summary>
        private bool IsValidPositionIndex(int index)
        {
            return index >= 0 && index < (fieldPositionUIs?.Length ?? 0);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            BattleField.OnObjectPlaced += OnObjectPlaced;
            BattleField.OnObjectRemoved += OnObjectRemoved;
            BattleField.OnObjectMoved += OnObjectMoved;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            BattleField.OnObjectPlaced -= OnObjectPlaced;
            BattleField.OnObjectRemoved -= OnObjectRemoved;
            BattleField.OnObjectMoved -= OnObjectMoved;
        }

        /// <summary>
        /// 객체 배치 이벤트 처리
        /// </summary>
        private void OnObjectPlaced(int positionIndex, GameObject obj, PositionObjectType objectType)
        {
            if (!IsValidPositionIndex(positionIndex)) return;

            // UI 업데이트
            fieldPositionUIs[positionIndex].UpdateOccupationStatus(true, objectType);

            // 배치 애니메이션
            AnimateObjectPlacement(positionIndex, obj);

            if (debugMode)
                Debug.Log($"[BattleFieldUI] 위치 {positionIndex}에 {objectType} 배치 UI 업데이트");
        }

        /// <summary>
        /// 객체 제거 이벤트 처리
        /// </summary>
        private void OnObjectRemoved(int positionIndex, GameObject obj, PositionObjectType objectType)
        {
            if (!IsValidPositionIndex(positionIndex)) return;

            // UI 업데이트
            fieldPositionUIs[positionIndex].UpdateOccupationStatus(false, PositionObjectType.None);

            // 기본 배경색으로 복구
            var fieldPosition = battleField.FieldPositions[positionIndex];
            if (fieldPosition.IsPlayerStartPosition)
                fieldPositionUIs[positionIndex].SetBackgroundColor(playerStartColor);
            else if (fieldPosition.IsEnemySpawnPosition)
                fieldPositionUIs[positionIndex].SetBackgroundColor(enemySpawnColor);
            else
                fieldPositionUIs[positionIndex].SetBackgroundColor(neutralColor);

            if (debugMode)
                Debug.Log($"[BattleFieldUI] 위치 {positionIndex}에서 {objectType} 제거 UI 업데이트");
        }

        /// <summary>
        /// 객체 이동 이벤트 처리
        /// </summary>
        private void OnObjectMoved(GameObject obj, int fromPosition, int toPosition)
        {
            // 이동 애니메이션
            AnimateObjectMovement(obj, fromPosition, toPosition);

            // UI 업데이트는 OnObjectRemoved와 OnObjectPlaced에서 처리됨

            if (debugMode)
                Debug.Log($"[BattleFieldUI] {obj.name} 이동: {fromPosition} -> {toPosition}");
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// UI 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug UI Status")]
        public void DebugUIStatus()
        {
            Debug.Log("=== BattleFieldUI 상태 ===");
            Debug.Log($"필드 위치 UI 수: {fieldPositionUIs?.Length ?? 0}");
            Debug.Log($"활성 애니메이션 수: {activeAnimations.Count}");

            if (fieldPositionUIs != null)
            {
                for (int i = 0; i < fieldPositionUIs.Length; i++)
                {
                    var ui = fieldPositionUIs[i];
                    Debug.Log($"- 위치 {i}: {(ui.isOccupied ? $"점유됨({ui.objectType})" : "비어있음")}");
                }
            }
        }
        #endregion
    }
}