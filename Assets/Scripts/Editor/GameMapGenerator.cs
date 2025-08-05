using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Maglin.Editor
{
    /// <summary>
    /// 게임 맵을 자동 생성하는 에디터 도구
    /// 횡스크롤 타입의 10칸 그리드 기반 맵 생성
    /// </summary>
    public class GameMapGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Game Map")]
        public static void ShowWindow()
        {
            GetWindow<GameMapGenerator>("Game Map Generator");
        }

        // 맵 설정
        private int gridWidth = 10; // 가로 10칸
        private int gridHeight = 5; // 세로 5칸 (플랫폼 높이)
        private float cellSize = 1f; // 셀 크기
        private float groundHeight = 1f; // 바닥 높이

        // 배경 설정
        private Color backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        private Color groundColor = new Color(0.6f, 0.4f, 0.2f, 1f);
        private Color gridColor = new Color(1f, 1f, 1f, 0.3f);

        // 카메라 설정
        private bool autoSetupCamera = true;
        private float cameraSize = 5f;

        // 타일 에셋 생성 설정
        private bool createTileAssets = true;
        private string tileAssetPath = "Assets/Resources/Tiles";

        private void OnGUI()
        {
            GUILayout.Label("게임 맵 생성 도구", EditorStyles.boldLabel);
            GUILayout.Label("횡스크롤 타입 10칸 그리드 기반 맵", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            // 맵 설정
            EditorGUILayout.LabelField("맵 설정", EditorStyles.boldLabel);
            gridWidth = EditorGUILayout.IntField("가로 칸수", gridWidth);
            gridHeight = EditorGUILayout.IntField("세로 칸수", gridHeight);
            cellSize = EditorGUILayout.FloatField("셀 크기", cellSize);
            groundHeight = EditorGUILayout.FloatField("바닥 높이", groundHeight);

            EditorGUILayout.Space();

            // 색상 설정
            EditorGUILayout.LabelField("색상 설정", EditorStyles.boldLabel);
            backgroundColor = EditorGUILayout.ColorField("배경색", backgroundColor);
            groundColor = EditorGUILayout.ColorField("바닥색", groundColor);
            gridColor = EditorGUILayout.ColorField("그리드색", gridColor);

            EditorGUILayout.Space();

            // 카메라 설정
            EditorGUILayout.LabelField("카메라 설정", EditorStyles.boldLabel);
            autoSetupCamera = EditorGUILayout.Toggle("카메라 자동 설정", autoSetupCamera);
            if (autoSetupCamera)
            {
                cameraSize = EditorGUILayout.FloatField("카메라 크기", cameraSize);
            }

            EditorGUILayout.Space();

            // 타일 에셋 설정
            EditorGUILayout.LabelField("타일 에셋 설정", EditorStyles.boldLabel);
            createTileAssets = EditorGUILayout.Toggle("타일 에셋 생성", createTileAssets);
            if (createTileAssets)
            {
                tileAssetPath = EditorGUILayout.TextField("타일 에셋 경로", tileAssetPath);
            }

            EditorGUILayout.Space();

            // 생성 버튼들
            if (GUILayout.Button("전체 게임 맵 생성", GUILayout.Height(30)))
            {
                GenerateCompleteGameMap();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("타일 에셋만 생성", GUILayout.Height(25)))
            {
                CreateTileAssets();
            }

            if (GUILayout.Button("배경만 생성", GUILayout.Height(25)))
            {
                GenerateBackground();
            }

            if (GUILayout.Button("바닥만 생성", GUILayout.Height(25)))
            {
                GenerateGround();
            }

            if (GUILayout.Button("그리드만 생성", GUILayout.Height(25)))
            {
                GenerateGrid();
            }

            if (GUILayout.Button("카메라 설정", GUILayout.Height(25)))
            {
                SetupCamera();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("생성된 맵은 'GameMap' GameObject로 저장됩니다.\n각 Tilemap은 별도 레이어로 구성되어 있습니다.", MessageType.Info);
        }

        /// <summary>
        /// 전체 게임 맵 생성
        /// </summary>
        private void GenerateCompleteGameMap()
        {
            // 타일 에셋 생성
            if (createTileAssets)
            {
                CreateTileAssets();
            }

            // 기존 GameMap이 있다면 삭제
            GameObject existingMap = GameObject.Find("GameMap");
            if (existingMap != null)
            {
                DestroyImmediate(existingMap);
            }

            // 메인 맵 컨테이너 생성
            GameObject gameMap = new GameObject("GameMap");

            // 배경 생성
            GameObject background = CreateBackground(gameMap);

            // 바닥 생성
            GameObject ground = CreateGround(gameMap);

            // 그리드 생성
            GameObject grid = CreateGrid(gameMap);

            // 카메라 설정
            if (autoSetupCamera)
            {
                SetupCamera();
            }

            // 맵 정보 컴포넌트 추가
            GameMapInfo mapInfo = gameMap.AddComponent<GameMapInfo>();
            mapInfo.gridWidth = gridWidth;
            mapInfo.gridHeight = gridHeight;
            mapInfo.cellSize = cellSize;
            mapInfo.groundHeight = groundHeight;

            Debug.Log("[GameMapGenerator] 전체 게임 맵 생성 완료!");
            Debug.Log($"[GameMapGenerator] 맵 크기: {gridWidth} x {gridHeight}, 셀 크기: {cellSize}");
        }

        /// <summary>
        /// 타일 에셋 생성
        /// </summary>
        private void CreateTileAssets()
        {
            // 디렉토리 생성
            if (!System.IO.Directory.Exists(tileAssetPath))
            {
                System.IO.Directory.CreateDirectory(tileAssetPath);
            }

            // 바닥 타일 스프라이트 생성
            Texture2D groundTexture = CreateColorTexture(groundColor, 64, 64);
            string groundSpritePath = $"{tileAssetPath}/GroundSprite.png";
            System.IO.File.WriteAllBytes(groundSpritePath, groundTexture.EncodeToPNG());
            AssetDatabase.Refresh();

            // 스프라이트 임포트 설정
            TextureImporter importer = AssetImporter.GetAtPath(groundSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            // 바닥 타일 생성
            Sprite groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(groundSpritePath);
            if (groundSprite != null)
            {
                Tile groundTile = ScriptableObject.CreateInstance<Tile>();
                groundTile.sprite = groundSprite;
                groundTile.name = "GroundTile";

                string groundTilePath = $"{tileAssetPath}/GroundTile.asset";
                AssetDatabase.CreateAsset(groundTile, groundTilePath);
            }

            // 그리드 라인 타일 생성
            Texture2D gridTexture = CreateGridTexture(gridColor, 64, 64);
            string gridSpritePath = $"{tileAssetPath}/GridSprite.png";
            System.IO.File.WriteAllBytes(gridSpritePath, gridTexture.EncodeToPNG());
            AssetDatabase.Refresh();

            // 그리드 스프라이트 임포트 설정
            TextureImporter gridImporter = AssetImporter.GetAtPath(gridSpritePath) as TextureImporter;
            if (gridImporter != null)
            {
                gridImporter.textureType = TextureImporterType.Sprite;
                gridImporter.spritePixelsPerUnit = 64f;
                gridImporter.filterMode = FilterMode.Point;
                gridImporter.SaveAndReimport();
            }

            // 그리드 타일 생성
            Sprite gridSprite = AssetDatabase.LoadAssetAtPath<Sprite>(gridSpritePath);
            if (gridSprite != null)
            {
                Tile gridTile = ScriptableObject.CreateInstance<Tile>();
                gridTile.sprite = gridSprite;
                gridTile.name = "GridTile";

                string gridTilePath = $"{tileAssetPath}/GridTile.asset";
                AssetDatabase.CreateAsset(gridTile, gridTilePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GameMapGenerator] 타일 에셋 생성 완료!");
        }

        /// <summary>
        /// 배경 생성
        /// </summary>
        private GameObject CreateBackground(GameObject parent)
        {
            GameObject background = new GameObject("Background");
            background.transform.SetParent(parent.transform);

            // 배경 스프라이트 렌더러
            SpriteRenderer bgRenderer = background.AddComponent<SpriteRenderer>();

            // 단색 스프라이트 생성
            Texture2D bgTexture = CreateColorTexture(backgroundColor, 256, 256);
            Sprite bgSprite = Sprite.Create(bgTexture, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
            bgRenderer.sprite = bgSprite;

            // 배경 크기 설정 (맵 전체를 덮도록)
            float bgWidth = gridWidth * cellSize;
            float bgHeight = gridHeight * cellSize + groundHeight;
            background.transform.localScale = new Vector3(bgWidth, bgHeight, 1f);

            // 배경 위치 설정 (중앙)
            background.transform.position = new Vector3(bgWidth * 0.5f, bgHeight * 0.5f, 10f);

            // 정렬 설정
            bgRenderer.sortingOrder = -100;

            return background;
        }

        /// <summary>
        /// 바닥 생성 (Tilemap 사용)
        /// </summary>
        private GameObject CreateGround(GameObject parent)
        {
            GameObject ground = new GameObject("Ground");
            ground.transform.SetParent(parent.transform);

            // Grid 컴포넌트 추가
            Grid grid = ground.AddComponent<Grid>();
            grid.cellSize = new Vector3(cellSize, cellSize, 1f);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;

            // Tilemap 컴포넌트들 추가
            Tilemap tilemap = ground.AddComponent<Tilemap>();
            TilemapRenderer renderer = ground.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -50;

            // 바닥 타일 로드
            Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tileAssetPath}/GroundTile.asset");
            if (groundTile == null)
            {
                // 타일이 없으면 기본 타일 생성
                groundTile = CreateDefaultGroundTile();
            }

            // 바닥을 10칸 가로로 배치
            for (int x = 0; x < gridWidth; x++)
            {
                Vector3Int tilePosition = new Vector3Int(x, 0, 0);
                tilemap.SetTile(tilePosition, groundTile);
            }

            // 바닥 콜라이더 추가
            TilemapCollider2D groundCollider = ground.AddComponent<TilemapCollider2D>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;

            return ground;
        }

        /// <summary>
        /// 기본 바닥 타일 생성
        /// </summary>
        private Tile CreateDefaultGroundTile()
        {
            // 임시 스프라이트 생성
            Texture2D groundTexture = CreateColorTexture(groundColor, 64, 64);
            Sprite groundSprite = Sprite.Create(groundTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

            Tile groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.sprite = groundSprite;
            groundTile.name = "DefaultGroundTile";

            return groundTile;
        }

        /// <summary>
        /// 그리드 생성 (Tilemap 사용)
        /// </summary>
        private GameObject CreateGrid(GameObject parent)
        {
            GameObject grid = new GameObject("Grid");
            grid.transform.SetParent(parent.transform);

            // Grid 컴포넌트 추가 (바닥과 같은 설정)
            Grid gridComponent = grid.AddComponent<Grid>();
            gridComponent.cellSize = new Vector3(cellSize, cellSize, 1f);
            gridComponent.cellLayout = GridLayout.CellLayout.Rectangle;

            // Tilemap 컴포넌트들 추가
            Tilemap tilemap = grid.AddComponent<Tilemap>();
            TilemapRenderer renderer = grid.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -25;

            // 그리드 타일 로드
            Tile gridTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tileAssetPath}/GridTile.asset");
            if (gridTile == null)
            {
                // 타일이 없으면 기본 그리드 타일 생성
                gridTile = CreateDefaultGridTile();
            }

            // 그리드 라인 배치
            // 세로선들
            for (int x = 0; x <= gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    tilemap.SetTile(tilePosition, gridTile);
                }
            }

            // 가로선들
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y <= gridHeight; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    tilemap.SetTile(tilePosition, gridTile);
                }
            }

            // 그리드 인덱스 표시
            CreateGridIndices(grid);

            return grid;
        }

        /// <summary>
        /// 기본 그리드 타일 생성
        /// </summary>
        private Tile CreateDefaultGridTile()
        {
            // 그리드 라인 텍스처 생성
            Texture2D gridTexture = CreateGridTexture(gridColor, 64, 64);
            Sprite gridSprite = Sprite.Create(gridTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

            Tile gridTile = ScriptableObject.CreateInstance<Tile>();
            gridTile.sprite = gridSprite;
            gridTile.name = "DefaultGridTile";

            return gridTile;
        }

        /// <summary>
        /// 그리드 인덱스 표시 생성
        /// </summary>
        private void CreateGridIndices(GameObject parent)
        {
            GameObject indices = new GameObject("GridIndices");
            indices.transform.SetParent(parent.transform);

            // 각 그리드 셀에 인덱스 표시
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    GameObject indexText = new GameObject($"Index_{x}_{y}");
                    indexText.transform.SetParent(indices.transform);

                    TextMesh textMesh = indexText.AddComponent<TextMesh>();
                    textMesh.text = $"{x}";
                    textMesh.fontSize = 20;
                    textMesh.color = Color.white;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.anchor = TextAnchor.MiddleCenter;

                    // 위치 설정 (셀 중앙)
                    indexText.transform.position = new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        y * cellSize + cellSize * 0.5f,
                        0f
                    );

                    // 작은 텍스트는 보이지 않게 설정
                    textMesh.characterSize = 0.3f;
                }
            }
        }

        /// <summary>
        /// 카메라 설정
        /// </summary>
        private void SetupCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // 메인 카메라가 없으면 새로 생성
                GameObject cameraObj = new GameObject("Main Camera");
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }

            // 2D 카메라 설정
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = cameraSize;
            mainCamera.backgroundColor = backgroundColor;

            // 카메라 위치 설정 (맵 중앙)
            float mapWidth = gridWidth * cellSize;
            float mapHeight = gridHeight * cellSize + groundHeight;
            mainCamera.transform.position = new Vector3(mapWidth * 0.5f, mapHeight * 0.5f, -10f);

            Debug.Log("[GameMapGenerator] 카메라 설정 완료!");
        }

        /// <summary>
        /// 배경만 생성
        /// </summary>
        private void GenerateBackground()
        {
            GameObject gameMap = GameObject.Find("GameMap");
            if (gameMap == null)
            {
                gameMap = new GameObject("GameMap");
            }

            GameObject existingBg = GameObject.Find("Background");
            if (existingBg != null)
            {
                DestroyImmediate(existingBg);
            }

            CreateBackground(gameMap);
            Debug.Log("[GameMapGenerator] 배경 생성 완료!");
        }

        /// <summary>
        /// 바닥만 생성
        /// </summary>
        private void GenerateGround()
        {
            GameObject gameMap = GameObject.Find("GameMap");
            if (gameMap == null)
            {
                gameMap = new GameObject("GameMap");
            }

            GameObject existingGround = GameObject.Find("Ground");
            if (existingGround != null)
            {
                DestroyImmediate(existingGround);
            }

            CreateGround(gameMap);
            Debug.Log("[GameMapGenerator] 바닥 생성 완료!");
        }

        /// <summary>
        /// 그리드만 생성
        /// </summary>
        private void GenerateGrid()
        {
            GameObject gameMap = GameObject.Find("GameMap");
            if (gameMap == null)
            {
                gameMap = new GameObject("GameMap");
            }

            GameObject existingGrid = GameObject.Find("Grid");
            if (existingGrid != null)
            {
                DestroyImmediate(existingGrid);
            }

            CreateGrid(gameMap);
            Debug.Log("[GameMapGenerator] 그리드 생성 완료!");
        }

        /// <summary>
        /// 단색 텍스처 생성
        /// </summary>
        private Texture2D CreateColorTexture(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 그리드 라인 텍스처 생성
        /// </summary>
        private Texture2D CreateGridTexture(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            // 투명한 배경
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            // 그리드 라인 그리기 (테두리만)
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 테두리 부분만 색상 적용
                    if (x < 2 || x >= width - 2 || y < 2 || y >= height - 2)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }

    /// <summary>
    /// 게임 맵 정보를 저장하는 컴포넌트
    /// </summary>
    public class GameMapInfo : MonoBehaviour
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