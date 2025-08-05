using UnityEngine;

namespace Maglin.Battle
{
    /// <summary>
    /// 런타임에서 사용하는 게임 맵 정보
    /// </summary>
    public class RuntimeGameMapInfo : MonoBehaviour
    {
        [Header("맵 설정")]
        public int gridWidth = 10;
        public int gridHeight = 5;
        public float cellSize = 1f;
        public float groundHeight = 1f;

        /// <summary>
        /// 그리드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector2 GetWorldPosition(int gridX, int gridY)
        {
            return new Vector2(gridX * cellSize + cellSize * 0.5f, gridY * cellSize + cellSize * 0.5f);
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환
        /// </summary>
        public Vector2Int GetGridPosition(Vector2 worldPosition)
        {
            int gridX = Mathf.FloorToInt(worldPosition.x / cellSize);
            int gridY = Mathf.FloorToInt(worldPosition.y / cellSize);
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// 그리드 좌표가 유효한지 확인
        /// </summary>
        public bool IsValidGridPosition(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight;
        }

        /// <summary>
        /// 그리드 좌표가 유효한지 확인
        /// </summary>
        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            return IsValidGridPosition(gridPos.x, gridPos.y);
        }

        /// <summary>
        /// 그리드 위치가 바닥 위에 있는지 확인
        /// </summary>
        public bool IsOnGround(int gridX, int gridY)
        {
            return gridY == 0; // 바닥은 y=0
        }

        /// <summary>
        /// 월드 위치가 바닥 위에 있는지 확인
        /// </summary>
        public bool IsOnGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GetGridPosition(worldPosition);
            return IsOnGround(gridPos.x, gridPos.y);
        }
    }
}