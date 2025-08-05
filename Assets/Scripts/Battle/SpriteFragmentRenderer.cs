using UnityEngine;
using System.Collections.Generic; // Added for List

namespace Maglin.Battle
{
    /// <summary>
    /// 스프라이트 조각을 더 정확하게 렌더링하기 위한 컴포넌트
    /// </summary>
    public class SpriteFragmentRenderer : MonoBehaviour
    {
        [Header("조각 설정")]
        [SerializeField] private Sprite originalSprite;
        [SerializeField] private Vector2 uvOffset;
        [SerializeField] private Vector2 uvScale;
        [SerializeField] private int fragmentIndex;
        [SerializeField] private Vector2Int gridPosition; // 조각의 그리드 위치 (col, row)
        [SerializeField] private Vector2Int totalGrid;    // 전체 그리드 크기 (cols, rows)

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;

        #region Unity Lifecycle
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            ApplyFragmentProperties();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 조각 설정
        /// </summary>
        public void SetupFragment(Sprite sprite, Color color, int col, int row, int totalCols, int totalRows, int index)
        {
            originalSprite = sprite;
            fragmentIndex = index;
            gridPosition = new Vector2Int(col, row);
            totalGrid = new Vector2Int(totalCols, totalRows);

            // UV 좌표 계산
            uvOffset = new Vector2((float)col / totalCols, (float)row / totalRows);
            uvScale = new Vector2(1.0f / totalCols, 1.0f / totalRows);

            // SpriteRenderer 설정
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 100;

            ApplyFragmentProperties();
        }

        /// <summary>
        /// 조각별 색상 설정
        /// </summary>
        public void SetFragmentColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// 조각별 머티리얼 속성 적용
        /// </summary>
        public void ApplyFragmentProperties()
        {
            if (spriteRenderer == null || propertyBlock == null) return;

            // UV 오프셋과 스케일을 Material Property Block에 설정
            propertyBlock.SetVector("_UVOffset", uvOffset);
            propertyBlock.SetVector("_UVScale", uvScale);
            propertyBlock.SetInt("_FragmentIndex", fragmentIndex);

            // SpriteRenderer에 적용
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// 조각 정보 디버그 표시
        /// </summary>
        public void ShowDebugInfo()
        {
            Debug.Log($"[SpriteFragmentRenderer] Fragment {fragmentIndex}: Grid({gridPosition.x},{gridPosition.y}) of ({totalGrid.x},{totalGrid.y}), UV Offset: {uvOffset}, UV Scale: {uvScale}");
        }
        #endregion

        #region Static Helper Methods
        /// <summary>
        /// 조각 생성 헬퍼 메서드
        /// </summary>
        public static GameObject CreateFragment(Sprite originalSprite, Color originalColor, Vector3 position, Vector3 scale, int col, int row, int totalCols, int totalRows, int index)
        {
            GameObject fragment = new GameObject($"Fragment_{col}_{row}");
            fragment.transform.position = position;
            fragment.transform.localScale = scale;

            // SpriteFragmentRenderer 컴포넌트 추가
            var fragmentRenderer = fragment.AddComponent<SpriteFragmentRenderer>();
            fragmentRenderer.SetupFragment(originalSprite, originalColor, col, row, totalCols, totalRows, index);

            return fragment;
        }

        /// <summary>
        /// 조각들을 생성하여 리스트로 반환
        /// </summary>
        public static List<GameObject> CreateFragments(Sprite originalSprite, Color originalColor, Vector3 centerPosition, Vector3 scale, int fragmentCount)
        {
            List<GameObject> fragments = new List<GameObject>();

            if (originalSprite == null) return fragments;

            // 조각 그리드 계산
            int cols = Mathf.CeilToInt(Mathf.Sqrt(fragmentCount));
            int rows = Mathf.CeilToInt((float)fragmentCount / cols);

            // 스프라이트 크기 계산
            Bounds spriteBounds = originalSprite.bounds;
            float spriteWidth = spriteBounds.size.x * scale.x;
            float spriteHeight = spriteBounds.size.y * scale.y;

            float fragmentWidth = spriteWidth / cols;
            float fragmentHeight = spriteHeight / rows;

            for (int i = 0; i < fragmentCount; i++)
            {
                int col = i % cols;
                int row = i / cols;

                if (row >= rows) break;

                // 조각 위치 계산
                float x = centerPosition.x - spriteWidth * 0.5f + fragmentWidth * (col + 0.5f);
                float y = centerPosition.y - spriteHeight * 0.5f + fragmentHeight * (row + 0.5f);
                Vector3 fragmentPosition = new Vector3(x, y, centerPosition.z);

                // 조각 크기 계산
                Vector3 fragmentScale = new Vector3(scale.x / cols, scale.y / rows, scale.z);

                GameObject fragment = CreateFragment(originalSprite, originalColor, fragmentPosition, fragmentScale, col, row, cols, rows, i);
                fragments.Add(fragment);
            }

            return fragments;
        }
        #endregion

        #region Properties
        /// <summary>
        /// 조각 인덱스
        /// </summary>
        public int FragmentIndex => fragmentIndex;

        /// <summary>
        /// 그리드 위치
        /// </summary>
        public Vector2Int GridPosition => gridPosition;

        /// <summary>
        /// UV 오프셋
        /// </summary>
        public Vector2 UVOffset => uvOffset;

        /// <summary>
        /// UV 스케일
        /// </summary>
        public Vector2 UVScale => uvScale;
        #endregion
    }
} 