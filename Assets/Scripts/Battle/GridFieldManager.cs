using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Maglin.Battle
{
    /// <summary>
    /// Tilemap 기반 그리드 필드 시스템을 관리하는 매니저
    /// Canvas UI 기반 FieldSlot을 대체합니다.
    /// </summary>
    public class GridFieldManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static GridFieldManager _instance;

        public static GridFieldManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GridFieldManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GridFieldManager");
                        _instance = go.AddComponent<GridFieldManager>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 그리드 필드가 초기화되었을 때 발생하는 이벤트
        /// </summary>
        public static event System.Action OnGridFieldInitialized;

        /// <summary>
        /// 오브젝트가 그리드 위치로 이동했을 때 발생하는 이벤트
        /// </summary>
        public static event System.Action<GameObject, Vector2Int> OnObjectMovedToGrid;
        #endregion

        #region Fields
        [Header("그리드 설정")]
        [SerializeField] private int gridWidth = 10;
        [SerializeField] private int gridHeight = 1; // 전투는 1차원 라인
        [SerializeField] private float cellSize = 1f;

        [Header("컴포넌트 참조")]
        [SerializeField] private Grid gridComponent;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private RuntimeGameMapInfo runtimeGameMapInfo;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool drawGizmos = true;

        // 그리드 위 오브젝트 관리
        private Dictionary<Vector2Int, GameObject> gridObjects = new Dictionary<Vector2Int, GameObject>();
        private Dictionary<GameObject, Vector2Int> objectPositions = new Dictionary<GameObject, Vector2Int>();

        // 초기화 상태
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 그리드 너비
        /// </summary>
        public int GridWidth => gridWidth;

        /// <summary>
        /// 그리드 높이 (전투에서는 1)
        /// </summary>
        public int GridHeight => gridHeight;

        /// <summary>
        /// 셀 크기
        /// </summary>
        public float CellSize => cellSize;

        /// <summary>
        /// 초기화 완료 여부
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Grid 컴포넌트
        /// </summary>
        public Grid GridComponent => gridComponent;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !isInitialized) return;

            // 그리드 라인 그리기
            Gizmos.color = Color.white;
            Vector3 startPos = transform.position;

            // 세로 라인들
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = startPos + new Vector3(x * cellSize, 0, 0);
                Vector3 end = start + new Vector3(0, gridHeight * cellSize, 0);
                Gizmos.DrawLine(start, end);
            }

            // 가로 라인들
            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = startPos + new Vector3(0, y * cellSize, 0);
                Vector3 end = start + new Vector3(gridWidth * cellSize, 0, 0);
                Gizmos.DrawLine(start, end);
            }

            // 오브젝트 위치 표시
            Gizmos.color = Color.red;
            foreach (var kvp in gridObjects)
            {
                Vector3 worldPos = GridToWorldPosition(kvp.Key);
                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// GridFieldManager 초기화
        /// </summary>
        public void InitializeGridField()
        {
            if (debugMode)
                Debug.Log("[GridFieldManager] 그리드 필드 초기화 시작");

            // GameMap 찾기 또는 생성
            FindOrCreateGameMap();

            // 그리드 설정 (기존 참조가 있으면 유지, 없으면 새로 찾기)
            SetupGrid();

            // 컴포넌트 참조 설정 (기존 참조가 있으면 유지, 없으면 새로 찾기)
            SetupComponentReferences();

            // 오브젝트 딕셔너리 정리 (씬 전환으로 인해 무효해진 오브젝트들 제거)
            CleanupInvalidObjects();

            isInitialized = true;

            // 이벤트 발생
            OnGridFieldInitialized?.Invoke();

            if (debugMode)
                Debug.Log("[GridFieldManager] 그리드 필드 초기화 완료");
        }

        /// <summary>
        /// GameMap 찾기 또는 생성
        /// </summary>
        private void FindOrCreateGameMap()
        {
            GameObject gameMapObj = GameObject.Find("GameMap");

            if (gameMapObj == null)
            {
                if (debugMode)
                    Debug.LogWarning("[GridFieldManager] GameMap을 찾을 수 없습니다. GameMapGenerator를 사용하여 맵을 생성해주세요.");

                // 기본값으로 계속 진행
                gridWidth = 10;
                gridHeight = 1;
                cellSize = 1f;
                return;
            }

            // RuntimeGameMapInfo 가져오기
            runtimeGameMapInfo = gameMapObj.GetComponent<RuntimeGameMapInfo>();
            if (runtimeGameMapInfo != null)
            {
                gridWidth = runtimeGameMapInfo.gridWidth;
                gridHeight = 1; // 전투는 1차원이므로 1로 고정
                cellSize = runtimeGameMapInfo.cellSize;

                if (debugMode)
                    Debug.Log($"[GridFieldManager] RuntimeGameMapInfo에서 설정 로드: {gridWidth}x{gridHeight}, 셀크기: {cellSize}");
            }
            else
            {
                // RuntimeGameMapInfo가 없으면 기본값 사용하고 컴포넌트 추가
                gridWidth = 10;
                gridHeight = 1;
                cellSize = 1f;

                // RuntimeGameMapInfo 컴포넌트 추가
                runtimeGameMapInfo = gameMapObj.AddComponent<RuntimeGameMapInfo>();
                runtimeGameMapInfo.gridWidth = gridWidth;
                runtimeGameMapInfo.gridHeight = gridHeight;
                runtimeGameMapInfo.cellSize = cellSize;

                if (debugMode)
                    Debug.Log("[GridFieldManager] RuntimeGameMapInfo를 새로 생성하고 기본값을 설정했습니다.");
            }
        }

        /// <summary>
        /// 그리드 설정
        /// </summary>
        private void SetupGrid()
        {
            // 기존 참조가 유효하지 않으면 새로 찾기
            if (gridComponent == null)
            {
                gridComponent = FindObjectOfType<Grid>();
            }

            if (gridComponent == null)
            {
                if (debugMode)
                    Debug.LogWarning("[GridFieldManager] Grid 컴포넌트를 찾을 수 없습니다.");
                return;
            }

            // 인스펙터에서 설정한 값이 있으면 그대로 사용, 없으면 Grid 컴포넌트의 현재 값 사용
            if (cellSize <= 0)
            {
                cellSize = gridComponent.cellSize.x;
                if (debugMode)
                    Debug.Log($"[GridFieldManager] Grid에서 cellSize 가져옴: {cellSize}");
            }
            else
            {
                // 인스펙터에서 설정한 값이 있으면 Grid 컴포넌트에 적용하지 않고 그대로 사용
                if (debugMode)
                    Debug.Log($"[GridFieldManager] 인스펙터 설정값 사용: cellSize = {cellSize}");
            }
        }

        /// <summary>
        /// 컴포넌트 참조 설정
        /// </summary>
        private void SetupComponentReferences()
        {
            // 기존 참조가 유효하지 않으면 새로 찾기
            if (groundTilemap == null)
            {
                GameObject groundObj = GameObject.Find("Ground");
                if (groundObj != null)
                {
                    groundTilemap = groundObj.GetComponent<Tilemap>();
                    if (debugMode)
                        Debug.Log("[GridFieldManager] Ground Tilemap 참조를 새로 찾았습니다.");
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log("[GridFieldManager] 기존 Ground Tilemap 참조를 유지합니다.");
            }
        }

        /// <summary>
        /// 씬 전환으로 무효해진 오브젝트들 정리
        /// </summary>
        private void CleanupInvalidObjects()
        {
            List<Vector2Int> invalidPositions = new List<Vector2Int>();
            List<GameObject> invalidObjects = new List<GameObject>();

            // 무효한 오브젝트들 찾기
            foreach (var kvp in gridObjects)
            {
                if (kvp.Value == null)
                {
                    invalidPositions.Add(kvp.Key);
                }
            }

            foreach (var kvp in objectPositions)
            {
                if (kvp.Key == null)
                {
                    invalidObjects.Add(kvp.Key);
                }
            }

            // 무효한 항목들 제거
            foreach (var pos in invalidPositions)
            {
                gridObjects.Remove(pos);
            }

            foreach (var obj in invalidObjects)
            {
                objectPositions.Remove(obj);
            }

            if (debugMode && (invalidPositions.Count > 0 || invalidObjects.Count > 0))
            {
                Debug.Log($"[GridFieldManager] 씬 전환으로 무효해진 오브젝트 {invalidPositions.Count + invalidObjects.Count}개를 정리했습니다.");
            }
        }
        #endregion

        #region Grid Position Management
        /// <summary>
        /// 그리드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            if (gridComponent != null)
            {
                Vector3Int cellPosition = new Vector3Int(gridPosition.x, gridPosition.y, 0);
                // Grid의 실제 transform 위치를 고려하여 월드 좌표 계산
                Vector3 cellWorldPos = gridComponent.CellToWorld(cellPosition);
                // 셀의 중앙에 배치 (기존 그리드 위치 그대로 사용)
                Vector3 cellCenter = cellWorldPos + gridComponent.cellSize * 0.5f;
                return cellCenter;
            }

            // Fallback: 수동 계산 (Grid 컴포넌트가 없을 때)
            // 이 경우에는 이 GridFieldManager의 transform 위치를 기준으로 계산
            Vector3 basePosition = transform.position;
            return basePosition + new Vector3(
                gridPosition.x * cellSize + cellSize * 0.5f,
                gridPosition.y * cellSize + cellSize * 0.5f, // 기존 중앙 정렬로 복구
                0f
            );
        }

        /// <summary>
        /// 스프라이트 정렬을 고려한 그리드 월드 위치 계산
        /// 스프라이트의 바닥이 그리드 셀의 바닥에 맞도록 조정
        /// </summary>
        public Vector3 GridToWorldPositionWithSpriteAlignment(GameObject obj, Vector2Int gridPosition)
        {
            Vector3 baseCenterPosition = GridToWorldPosition(gridPosition);
            
            // 그리드 셀의 바닥 위치 계산
            Vector3 cellBottomPosition = baseCenterPosition;
            if (gridComponent != null)
            {
                // Grid 컴포넌트가 있는 경우: 셀 크기의 절반만큼 아래로
                cellBottomPosition.y -= gridComponent.cellSize.y * 0.5f;
            }
            else
            {
                // Fallback: 수동 계산
                cellBottomPosition.y -= cellSize * 0.5f;
            }
            
            // SpriteRenderer가 있는지 확인
            SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Sprite sprite = spriteRenderer.sprite;
                
                // 스프라이트의 pivot과 크기 정보
                Vector2 pivot = sprite.pivot;
                Vector2 spriteSize = sprite.rect.size;
                float pixelsPerUnit = sprite.pixelsPerUnit;
                
                // 스프라이트의 실제 크기 (월드 단위)
                Vector2 worldSpriteSize = spriteSize / pixelsPerUnit;
                
                // pivot이 스프라이트 내에서 차지하는 비율 계산
                Vector2 pivotRatio = new Vector2(pivot.x / spriteSize.x, pivot.y / spriteSize.y);
                
                // 스프라이트의 바닥이 셀 바닥에 맞도록 Y 오프셋 계산
                // pivot에서 스프라이트 바닥까지의 거리
                float bottomOffset = pivotRatio.y * worldSpriteSize.y;
                
                // 최종 위치: 셀 바닥 + 스프라이트 바닥에서 pivot까지의 오프셋
                Vector3 finalPosition = cellBottomPosition;
                finalPosition.y += bottomOffset;
                
                // X 위치는 셀 중앙 유지
                finalPosition.x = baseCenterPosition.x;
                
                if (debugMode)
                    Debug.Log($"[GridFieldManager] 스프라이트 정렬 적용: {obj.name} - 셀바닥: {cellBottomPosition.y}, 오프셋: {bottomOffset}, 최종: {finalPosition}");
                
                return finalPosition;
            }
            
            // 스프라이트가 없으면 셀 바닥에 중앙 정렬
            cellBottomPosition.x = baseCenterPosition.x;
            return cellBottomPosition;
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            if (gridComponent != null)
            {
                Vector3Int cellPosition = gridComponent.WorldToCell(worldPosition);
                return new Vector2Int(cellPosition.x, cellPosition.y);
            }

            // Fallback: 수동 계산
            int gridX = Mathf.FloorToInt(worldPosition.x / cellSize);
            int gridY = Mathf.FloorToInt(worldPosition.y / cellSize);
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// 그리드 좌표가 유효한지 확인
        /// </summary>
        public bool IsValidGridPosition(Vector2Int gridPosition)
        {
            return gridPosition.x >= 0 && gridPosition.x < gridWidth &&
                   gridPosition.y >= 0 && gridPosition.y < gridHeight;
        }

        /// <summary>
        /// 그리드 위치가 비어있는지 확인
        /// </summary>
        public bool IsGridPositionEmpty(Vector2Int gridPosition)
        {
            return !gridObjects.ContainsKey(gridPosition) || gridObjects[gridPosition] == null;
        }

        /// <summary>
        /// 특정 그리드 위치에 있는 오브젝트 가져오기
        /// </summary>
        public GameObject GetObjectAtGridPosition(Vector2Int gridPosition)
        {
            return gridObjects.TryGetValue(gridPosition, out GameObject obj) ? obj : null;
        }

        /// <summary>
        /// 오브젝트의 현재 그리드 위치 가져오기
        /// </summary>
        public Vector2Int GetObjectGridPosition(GameObject obj)
        {
            return objectPositions.TryGetValue(obj, out Vector2Int pos) ? pos : new Vector2Int(-1, -1);
        }
        #endregion

        #region Object Placement
        /// <summary>
        /// 오브젝트를 그리드 위치에 배치
        /// </summary>
        public bool PlaceObjectAtGrid(GameObject obj, Vector2Int gridPosition, bool forceMove = false)
        {
            if (obj == null) return false;

            if (!IsValidGridPosition(gridPosition))
            {
                if (debugMode)
                    Debug.LogWarning($"[GridFieldManager] 유효하지 않은 그리드 위치: {gridPosition}");
                return false;
            }

            if (!forceMove && !IsGridPositionEmpty(gridPosition))
            {
                if (debugMode)
                    Debug.LogWarning($"[GridFieldManager] 그리드 위치가 이미 점유됨: {gridPosition}");
                return false;
            }

            // 기존 위치에서 제거
            RemoveObjectFromGrid(obj);

            // 새 위치에 배치
            gridObjects[gridPosition] = obj;
            objectPositions[obj] = gridPosition;

            // 월드 위치 설정 (스프라이트 pivot이 올바르게 설정되었으면 기본 그리드 위치 사용)
            Vector3 worldPosition = GridToWorldPosition(gridPosition);
            obj.transform.position = worldPosition;

            // 이벤트 발생
            OnObjectMovedToGrid?.Invoke(obj, gridPosition);

            if (debugMode)
                Debug.Log($"[GridFieldManager] {obj.name}을(를) 그리드 위치 {gridPosition}에 배치했습니다.");

            return true;
        }

        /// <summary>
        /// 오브젝트를 그리드에서 제거
        /// </summary>
        public void RemoveObjectFromGrid(GameObject obj)
        {
            if (obj == null) return;

            if (objectPositions.TryGetValue(obj, out Vector2Int currentPos))
            {
                gridObjects.Remove(currentPos);
                objectPositions.Remove(obj);

                if (debugMode)
                    Debug.Log($"[GridFieldManager] {obj.name}을(를) 그리드 위치 {currentPos}에서 제거했습니다.");
            }
        }

        /// <summary>
        /// 오브젝트를 새로운 그리드 위치로 이동
        /// </summary>
        public bool MoveObjectToGrid(GameObject obj, Vector2Int newGridPosition)
        {
            return PlaceObjectAtGrid(obj, newGridPosition, false);
        }

        /// <summary>
        /// 가장 가까운 빈 그리드 위치 찾기
        /// </summary>
        public Vector2Int FindNearestEmptyGridPosition(Vector2Int preferredPosition)
        {
            // 우선 희망 위치가 비어있는지 확인
            if (IsValidGridPosition(preferredPosition) && IsGridPositionEmpty(preferredPosition))
            {
                return preferredPosition;
            }

            // 주변 위치 검색 (거리순)
            for (int distance = 1; distance < gridWidth; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    for (int dy = -distance; dy <= distance; dy++)
                    {
                        if (Mathf.Abs(dx) != distance && Mathf.Abs(dy) != distance)
                            continue; // 가장자리만 검사

                        Vector2Int testPos = preferredPosition + new Vector2Int(dx, dy);
                        if (IsValidGridPosition(testPos) && IsGridPositionEmpty(testPos))
                        {
                            return testPos;
                        }
                    }
                }
            }

            return new Vector2Int(-1, -1); // 찾을 수 없음
        }
        #endregion

        #region Utility
        /// <summary>
        /// 모든 그리드 오브젝트 제거
        /// </summary>
        public void ClearAllGridObjects()
        {
            gridObjects.Clear();
            objectPositions.Clear();

            if (debugMode)
                Debug.Log("[GridFieldManager] 모든 그리드 오브젝트를 제거했습니다.");
        }

        /// <summary>
        /// 그리드에 배치된 모든 오브젝트 목록 반환
        /// </summary>
        public List<GameObject> GetAllGridObjects()
        {
            List<GameObject> objects = new List<GameObject>();
            foreach (var obj in gridObjects.Values)
            {
                if (obj != null)
                    objects.Add(obj);
            }
            return objects;
        }

        /// <summary>
        /// 특정 범위 내의 그리드 위치들 반환
        /// </summary>
        public List<Vector2Int> GetGridPositionsInRange(Vector2Int center, int range)
        {
            List<Vector2Int> positions = new List<Vector2Int>();

            for (int x = center.x - range; x <= center.x + range; x++)
            {
                for (int y = center.y - range; y <= center.y + range; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (IsValidGridPosition(pos))
                    {
                        positions.Add(pos);
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Grid Info")]
        public void DebugGridInfo()
        {
            Debug.Log($"=== GridFieldManager Debug Info ===");
            Debug.Log($"초기화 상태: {isInitialized}");
            Debug.Log($"그리드 크기: {gridWidth} x {gridHeight}");
            Debug.Log($"셀 크기: {cellSize}");
            Debug.Log($"배치된 오브젝트 수: {gridObjects.Count}");

            foreach (var kvp in gridObjects)
            {
                if (kvp.Value != null)
                {
                    Debug.Log($"  - {kvp.Value.name}: {kvp.Key}");
                }
            }
        }
        #endregion
    }
}