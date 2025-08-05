using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Maglin.Enemy;
using Maglin.Player;

namespace Maglin.Battle
{
    /// <summary>
    /// 위치에 배치할 수 있는 객체 타입
    /// </summary>
    public enum PositionObjectType
    {
        None,       // 빈 위치
        Player,     // 플레이어
        Enemy,      // 몬스터
        Object      // 기타 오브젝트
    }

    /// <summary>
    /// 전투 필드의 개별 위치를 나타내는 클래스
    /// </summary>
    [System.Serializable]
    public class FieldPosition
    {
        [Header("위치 정보")]
        [SerializeField] private int positionIndex;      // 위치 번호 (0-9)
        [SerializeField] private Vector2Int gridCoord;   // 그리드 좌표
        [SerializeField] private Vector3 worldPosition;  // 월드 좌표

        [Header("점유 상태")]
        [SerializeField] private bool isOccupied;        // 점유 여부
        [SerializeField] private PositionObjectType objectType; // 점유 객체 타입
        [SerializeField] private GameObject occupyingObject;    // 점유 중인 객체

        [Header("위치 설정")]
        [SerializeField] private bool isTraversable = true;     // 이동 가능 여부
        [SerializeField] private bool isPlayerStartPosition;    // 플레이어 시작 위치
        [SerializeField] private bool isEnemySpawnPosition;     // 몬스터 스폰 위치

        #region Properties
        /// <summary>위치 번호</summary>
        public int PositionIndex => positionIndex;

        /// <summary>그리드 좌표</summary>
        public Vector2Int GridCoord => gridCoord;

        /// <summary>월드 좌표</summary>
        public Vector3 WorldPosition => worldPosition;

        /// <summary>점유 여부</summary>
        public bool IsOccupied => isOccupied;

        /// <summary>점유 객체 타입</summary>
        public PositionObjectType ObjectType => objectType;

        /// <summary>점유 중인 객체</summary>
        public GameObject OccupyingObject => occupyingObject;

        /// <summary>이동 가능 여부</summary>
        public bool IsTraversable => isTraversable && !isOccupied;

        /// <summary>플레이어 시작 위치 여부</summary>
        public bool IsPlayerStartPosition => isPlayerStartPosition;

        /// <summary>몬스터 스폰 위치 여부</summary>
        public bool IsEnemySpawnPosition => isEnemySpawnPosition;
        #endregion

        #region Constructor
        public FieldPosition(int index, Vector2Int coord, Vector3 worldPos)
        {
            positionIndex = index;
            gridCoord = coord;
            worldPosition = worldPos;
            isOccupied = false;
            objectType = PositionObjectType.None;
            occupyingObject = null;
            isTraversable = true;
            isPlayerStartPosition = index == 0;  // 0번이 플레이어 시작 위치
            isEnemySpawnPosition = index >= 5;    // 5번 이후가 몬스터 스폰 위치
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 객체 배치
        /// </summary>
        public bool PlaceObject(GameObject obj, PositionObjectType type)
        {
            if (isOccupied || obj == null || !isTraversable) return false;

            occupyingObject = obj;
            objectType = type;
            isOccupied = true;

            // 객체의 위치를 필드 위치로 설정
            obj.transform.position = worldPosition;

            return true;
        }

        /// <summary>
        /// 객체 제거
        /// </summary>
        public GameObject RemoveObject()
        {
            GameObject removedObject = occupyingObject;

            occupyingObject = null;
            objectType = PositionObjectType.None;
            isOccupied = false;

            return removedObject;
        }

        /// <summary>
        /// 위치 설정 변경
        /// </summary>
        public void SetTraversable(bool traversable)
        {
            isTraversable = traversable;
        }

        /// <summary>
        /// 월드 좌표 업데이트
        /// </summary>
        public void UpdateWorldPosition(Vector3 newWorldPos)
        {
            worldPosition = newWorldPos;

            // 점유 중인 객체가 있다면 위치 업데이트
            if (occupyingObject != null)
            {
                occupyingObject.transform.position = worldPosition;
            }
        }
        #endregion

        #region Debug
        public override string ToString()
        {
            return $"Position {positionIndex}: {(isOccupied ? $"[{objectType}]" : "[Empty]")} at {gridCoord}";
        }
        #endregion
    }

    /// <summary>
    /// 10칸 횡스크롤 전투 필드 관리 클래스
    /// </summary>
    public class BattleField : MonoBehaviour
    {
        #region Singleton Implementation
        private static BattleField _instance;

        public static BattleField Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BattleField>();

                    if (_instance == null)
                    {
                        GameObject battleFieldObject = new GameObject("BattleField");
                        _instance = battleFieldObject.AddComponent<BattleField>();
                        DontDestroyOnLoad(battleFieldObject);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 객체 배치 이벤트 (위치 인덱스, 객체, 타입)
        /// </summary>
        public static event Action<int, GameObject, PositionObjectType> OnObjectPlaced;

        /// <summary>
        /// 객체 제거 이벤트 (위치 인덱스, 객체, 타입)
        /// </summary>
        public static event Action<int, GameObject, PositionObjectType> OnObjectRemoved;

        /// <summary>
        /// 객체 이동 이벤트 (객체, 이전 위치, 새 위치)
        /// </summary>
        public static event Action<GameObject, int, int> OnObjectMoved;
        #endregion

        #region Fields
        [Header("필드 설정")]
        [SerializeField] private int fieldLength = 10;        // 필드 길이 (10칸)
        [SerializeField] private float positionSpacing = 2f;   // 위치 간 간격
        [SerializeField] private Vector3 fieldStartPosition = Vector3.zero; // 필드 시작 위치

        [Header("시각적 표현")]
        [SerializeField] private GameObject positionMarkerPrefab;   // 위치 마커 프리팹
        [SerializeField] private Transform fieldContainer;         // 필드 컨테이너
        [SerializeField] private bool showPositionMarkers = true;  // 위치 마커 표시

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 필드 위치들
        private FieldPosition[] fieldPositions;
        private Dictionary<GameObject, int> objectToPositionMap;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 필드 길이
        /// </summary>
        public int FieldLength => fieldLength;

        /// <summary>
        /// 모든 필드 위치 (읽기 전용)
        /// </summary>
        public IReadOnlyList<FieldPosition> FieldPositions => System.Array.AsReadOnly(fieldPositions);

        /// <summary>
        /// 플레이어 위치 (0번 위치)
        /// </summary>
        public FieldPosition PlayerPosition => fieldPositions?[0];

        /// <summary>
        /// 빈 위치들
        /// </summary>
        public List<FieldPosition> EmptyPositions =>
            fieldPositions?.Where(p => !p.IsOccupied && p.IsTraversable).ToList() ?? new List<FieldPosition>();

        /// <summary>
        /// 몬스터 스폰 가능한 빈 위치들
        /// </summary>
        public List<FieldPosition> AvailableEnemySpawnPositions =>
            fieldPositions?.Where(p => p.IsEnemySpawnPosition && !p.IsOccupied && p.IsTraversable).ToList() ?? new List<FieldPosition>();
        #endregion

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Initialize();
        }

        private void Start()
        {
            if (fieldContainer == null)
            {
                GameObject container = new GameObject("FieldContainer");
                container.transform.SetParent(transform);
                fieldContainer = container.transform;
            }

            CreatePositionMarkers();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 객체를 특정 위치에 배치
        /// </summary>
        public bool PlaceObjectAtPosition(GameObject obj, int positionIndex, PositionObjectType objectType)
        {
            if (!IsValidPositionIndex(positionIndex) || obj == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[BattleField] 잘못된 배치 시도: 위치 {positionIndex}, 객체 {obj?.name}");
                return false;
            }

            FieldPosition position = fieldPositions[positionIndex];
            if (!position.PlaceObject(obj, objectType))
            {
                if (debugMode)
                    Debug.LogWarning($"[BattleField] 위치 {positionIndex}에 객체 배치 실패");
                return false;
            }

            // 매핑 업데이트
            if (objectToPositionMap.ContainsKey(obj))
                objectToPositionMap[obj] = positionIndex;
            else
                objectToPositionMap.Add(obj, positionIndex);

            if (debugMode)
                Debug.Log($"[BattleField] {obj.name}을 위치 {positionIndex}에 배치");

            OnObjectPlaced?.Invoke(positionIndex, obj, objectType);
            return true;
        }

        /// <summary>
        /// 특정 위치에서 객체 제거
        /// </summary>
        public GameObject RemoveObjectFromPosition(int positionIndex)
        {
            if (!IsValidPositionIndex(positionIndex))
                return null;

            FieldPosition position = fieldPositions[positionIndex];
            GameObject removedObject = position.RemoveObject();

            if (removedObject != null)
            {
                PositionObjectType objectType = position.ObjectType;
                objectToPositionMap.Remove(removedObject);

                if (debugMode)
                    Debug.Log($"[BattleField] {removedObject.name}을 위치 {positionIndex}에서 제거");

                OnObjectRemoved?.Invoke(positionIndex, removedObject, objectType);
            }

            return removedObject;
        }

        /// <summary>
        /// 객체를 다른 위치로 이동
        /// </summary>
        public bool MoveObject(GameObject obj, int newPositionIndex)
        {
            if (obj == null || !IsValidPositionIndex(newPositionIndex))
                return false;

            // 현재 위치 찾기
            if (!objectToPositionMap.TryGetValue(obj, out int currentPosition))
            {
                if (debugMode)
                    Debug.LogWarning($"[BattleField] {obj.name}의 현재 위치를 찾을 수 없음");
                return false;
            }

            // 새 위치가 같은 위치라면 무시
            if (currentPosition == newPositionIndex)
                return true;

            // 새 위치가 점유되어 있는지 확인
            if (fieldPositions[newPositionIndex].IsOccupied)
            {
                if (debugMode)
                    Debug.LogWarning($"[BattleField] 위치 {newPositionIndex}가 이미 점유됨");
                return false;
            }

            // 기존 위치에서 제거
            FieldPosition oldPosition = fieldPositions[currentPosition];
            PositionObjectType objectType = oldPosition.ObjectType;
            oldPosition.RemoveObject();

            // 새 위치에 배치
            FieldPosition newPosition = fieldPositions[newPositionIndex];
            if (!newPosition.PlaceObject(obj, objectType))
            {
                // 배치 실패 시 원래 위치로 복구
                oldPosition.PlaceObject(obj, objectType);
                return false;
            }

            // 매핑 업데이트
            objectToPositionMap[obj] = newPositionIndex;

            if (debugMode)
                Debug.Log($"[BattleField] {obj.name}을 위치 {currentPosition}에서 {newPositionIndex}로 이동");

            OnObjectMoved?.Invoke(obj, currentPosition, newPositionIndex);
            return true;
        }

        /// <summary>
        /// 객체의 현재 위치 반환
        /// </summary>
        public int GetObjectPosition(GameObject obj)
        {
            return objectToPositionMap.TryGetValue(obj, out int position) ? position : -1;
        }

        /// <summary>
        /// 특정 위치의 객체 반환
        /// </summary>
        public GameObject GetObjectAtPosition(int positionIndex)
        {
            return IsValidPositionIndex(positionIndex) ? fieldPositions[positionIndex].OccupyingObject : null;
        }

        /// <summary>
        /// 두 위치 간 거리 계산
        /// </summary>
        public int GetDistanceBetweenPositions(int pos1, int pos2)
        {
            return Mathf.Abs(pos1 - pos2);
        }

        /// <summary>
        /// 공격 범위 내 위치들 반환
        /// </summary>
        public List<int> GetPositionsInRange(int centerPosition, int range)
        {
            List<int> positions = new List<int>();

            for (int i = 0; i < fieldLength; i++)
            {
                if (GetDistanceBetweenPositions(centerPosition, i) <= range)
                {
                    positions.Add(i);
                }
            }

            return positions;
        }

        /// <summary>
        /// 다중 칸 차지 객체 배치 (연속된 위치)
        /// </summary>
        public bool PlaceMultiPositionObject(GameObject obj, int startPosition, int size, PositionObjectType objectType)
        {
            if (size <= 0 || !IsValidPositionIndex(startPosition) ||
                !IsValidPositionIndex(startPosition + size - 1))
                return false;

            // 모든 위치가 사용 가능한지 확인
            for (int i = 0; i < size; i++)
            {
                if (fieldPositions[startPosition + i].IsOccupied)
                    return false;
            }

            // 모든 위치에 배치 (첫 번째 위치에만 실제 객체, 나머지는 점유 표시)
            for (int i = 0; i < size; i++)
            {
                int posIndex = startPosition + i;
                fieldPositions[posIndex].PlaceObject(i == 0 ? obj : null, objectType);
            }

            objectToPositionMap[obj] = startPosition;

            if (debugMode)
                Debug.Log($"[BattleField] {obj.name}을 위치 {startPosition}부터 {size}칸 차지로 배치");

            OnObjectPlaced?.Invoke(startPosition, obj, objectType);
            return true;
        }

        /// <summary>
        /// 필드 초기화 (전투 시작 시)
        /// </summary>
        public void ResetField()
        {
            if (fieldPositions == null) return;

            // 모든 위치에서 객체 제거
            foreach (var position in fieldPositions)
            {
                position.RemoveObject();
            }

            objectToPositionMap.Clear();

            if (debugMode)
                Debug.Log("[BattleField] 필드 초기화 완료");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            if (isInitialized) return;

            CreateFieldPositions();
            objectToPositionMap = new Dictionary<GameObject, int>();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[BattleField] 초기화 완료");
        }

        /// <summary>
        /// 필드 위치들 생성
        /// </summary>
        private void CreateFieldPositions()
        {
            fieldPositions = new FieldPosition[fieldLength];

            for (int i = 0; i < fieldLength; i++)
            {
                Vector2Int gridCoord = new Vector2Int(i, 0);
                Vector3 worldPos = fieldStartPosition + new Vector3(i * positionSpacing, 0, 0);

                fieldPositions[i] = new FieldPosition(i, gridCoord, worldPos);
            }

            if (debugMode)
                Debug.Log($"[BattleField] {fieldLength}개의 위치 생성 완료");
        }

        /// <summary>
        /// 위치 마커 생성
        /// </summary>
        private void CreatePositionMarkers()
        {
            if (!showPositionMarkers || fieldPositions == null) return;

            for (int i = 0; i < fieldPositions.Length; i++)
            {
                GameObject marker;
                if (positionMarkerPrefab != null)
                {
                    marker = Instantiate(positionMarkerPrefab, fieldContainer);
                }
                else
                {
                    // 기본 마커 생성
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.transform.SetParent(fieldContainer);
                    marker.transform.localScale = Vector3.one * 0.5f;

                    // 색상 설정
                    Renderer renderer = marker.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        if (fieldPositions[i].IsPlayerStartPosition)
                            renderer.material.color = Color.blue;
                        else if (fieldPositions[i].IsEnemySpawnPosition)
                            renderer.material.color = Color.red;
                        else
                            renderer.material.color = Color.gray;
                    }
                }

                marker.name = $"PositionMarker_{i}";
                marker.transform.position = fieldPositions[i].WorldPosition;
            }
        }

        /// <summary>
        /// 유효한 위치 인덱스인지 확인
        /// </summary>
        private bool IsValidPositionIndex(int index)
        {
            return index >= 0 && index < fieldLength;
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 필드 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug Field Status")]
        public void DebugFieldStatus()
        {
            Debug.Log("=== BattleField 상태 ===");
            Debug.Log($"필드 길이: {fieldLength}");
            Debug.Log($"점유된 위치 수: {fieldPositions?.Count(p => p.IsOccupied) ?? 0}");

            if (fieldPositions != null)
            {
                foreach (var position in fieldPositions)
                {
                    if (position.IsOccupied)
                    {
                        Debug.Log($"- {position}");
                    }
                }
            }
        }
        #endregion
    }
}