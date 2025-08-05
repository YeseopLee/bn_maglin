using UnityEngine;
using System.Collections;
using Maglin.Player;
using Maglin.Core;

namespace Maglin.Battle
{
    /// <summary>
    /// 전투 화면에서의 플레이어 관련 로직을 관리하는 매니저
    /// </summary>
    public class PlayerBattleManager : MonoBehaviour
    {
        private static PlayerBattleManager _instance;
        public static PlayerBattleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerBattleManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("PlayerBattleManager");
                        _instance = go.AddComponent<PlayerBattleManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 플레이어 위치 설정 (Grid 기반)
        private Vector2Int playerGridPosition = new Vector2Int(0, 0);

        // 플레이어 게임오브젝트
        private GameObject playerGameObject = null;
        private bool isInitialized = false;

        #region Unity Events
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
        #endregion

        #region Initialization
        /// <summary>
        /// PlayerBattleManager 초기화
        /// </summary>
        public void InitializePlayerBattleManager()
        {
            if (isInitialized)
            {
                if (debugMode)
                    Debug.Log("[PlayerBattleManager] 이미 초기화되어 있습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 초기화 시작");

            // GridFieldManager 초기화 대기
            StartCoroutine(InitializeAfterGridReady());
        }

        /// <summary>
        /// GridFieldManager 초기화 후 플레이어 초기화
        /// </summary>
        private IEnumerator InitializeAfterGridReady()
        {
            // GridFieldManager가 초기화될 때까지 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return null;
            }

            // 플레이어 위치 설정
            SetPlayerGridPosition(playerGridPosition);

            // 플레이어 게임오브젝트 생성
            CreatePlayerGameObject();

            // PlayerManager에 전투 시작 알림
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnBattleStart();
            }

            isInitialized = true;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 초기화 완료");
        }

        /// <summary>
        /// 전투 종료 시 정리
        /// </summary>
        public void OnBattleEnd()
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 전투 종료 처리");

            // PlayerManager에 전투 종료 알림
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnBattleEnd();
            }

            // 플레이어 게임오브젝트 정리
            if (playerGameObject != null)
            {
                Destroy(playerGameObject);
                playerGameObject = null;
            }

            isInitialized = false;
        }
        #endregion

        #region Player Position Management
        /// <summary>
        /// 플레이어 그리드 위치 설정
        /// </summary>
        public void SetPlayerGridPosition(Vector2Int gridPosition)
        {
            playerGridPosition = gridPosition;

            // PlayerManager에 위치 업데이트
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.SetPosition(gridPosition);
            }

            // GridFieldManager에 플레이어 배치
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                if (playerGameObject != null)
                {
                    GridFieldManager.Instance.PlaceObjectAtGrid(playerGameObject, gridPosition, true);
                }
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 그리드 위치 설정: {gridPosition}");
        }

        /// <summary>
        /// 현재 플레이어 그리드 위치 반환
        /// </summary>
        public Vector2Int GetPlayerGridPosition()
        {
            return playerGridPosition;
        }

        /// <summary>
        /// 현재 플레이어 월드 위치 반환
        /// </summary>
        public Vector3 GetPlayerWorldPosition()
        {
            if (GridFieldManager.Instance != null)
            {
                return GridFieldManager.Instance.GridToWorldPosition(playerGridPosition);
            }
            return Vector3.zero;
        }
        #endregion

        #region Player GameObject Management
        /// <summary>
        /// 플레이어 게임오브젝트 생성
        /// </summary>
        private void CreatePlayerGameObject()
        {
            // 기존 플레이어 오브젝트 제거
            if (playerGameObject != null)
            {
                if (GridFieldManager.Instance != null)
                {
                    GridFieldManager.Instance.RemoveObjectFromGrid(playerGameObject);
                }
                DestroyImmediate(playerGameObject);
            }

            // PlayerManager에서 플레이어 스프라이트 가져오기
            Sprite playerSprite = null;
            if (PlayerManager.Instance != null)
            {
                playerSprite = PlayerManager.Instance.GetPlayerSprite();
            }

            // 스프라이트가 없으면 기본 스프라이트 생성
            if (playerSprite == null)
            {
                playerSprite = CreateDefaultPlayerSprite();
            }

            // 플레이어 게임오브젝트 생성
            playerGameObject = new GameObject("Player");

            // SpriteRenderer 컴포넌트 추가
            var spriteRenderer = playerGameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.color = Color.blue;
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 10;

            // GridFieldManager를 통해 플레이어를 그리드에 배치
            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                bool placed = GridFieldManager.Instance.PlaceObjectAtGrid(playerGameObject, playerGridPosition, true);

                if (debugMode)
                {
                    if (placed)
                        Debug.Log($"[PlayerBattleManager] 플레이어를 그리드 위치 {playerGridPosition}에 배치했습니다.");
                    else
                        Debug.LogWarning($"[PlayerBattleManager] 플레이어를 그리드 위치 {playerGridPosition}에 배치할 수 없습니다.");
                }
            }
            else
            {
                // 그리드가 준비되지 않은 경우 기본 위치 설정
                playerGameObject.transform.position = new Vector3(0f, 0.5f, 0f);
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] GridFieldManager가 준비되지 않아 기본 위치에 플레이어 배치");
            }
        }

        /// <summary>
        /// 기본 플레이어 스프라이트 생성 (PlayerManager에서 스프라이트를 가져올 수 없을 때)
        /// </summary>
        private Sprite CreateDefaultPlayerSprite()
        {
            // 플레이어용 사각형 스프라이트 생성 (파란색)
            Texture2D texture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.blue;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }


        #endregion

        #region Public API
        /// <summary>
        /// 플레이어 게임오브젝트 반환
        /// </summary>
        public GameObject GetPlayerGameObject()
        {
            return playerGameObject;
        }



        /// <summary>
        /// 플레이어 위치 변경 (이동)
        /// </summary>
        public void MovePlayerToGrid(Vector2Int newGridPosition)
        {
            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 이동: {playerGridPosition} -> {newGridPosition}");

            // 이전 위치 저장
            Vector2Int oldPosition = playerGridPosition;

            // 새 위치 설정
            SetPlayerGridPosition(newGridPosition);

            // GridFieldManager를 통해 실제 이동 처리
            if (GridFieldManager.Instance != null && playerGameObject != null)
            {
                bool moved = GridFieldManager.Instance.MoveObjectToGrid(playerGameObject, newGridPosition);

                if (!moved && debugMode)
                {
                    Debug.LogWarning($"[PlayerBattleManager] 플레이어를 {newGridPosition}로 이동할 수 없습니다.");
                    // 실패 시 이전 위치로 복구
                    playerGridPosition = oldPosition;
                }
            }
        }

        /// <summary>
        /// 레거시 호환성을 위한 메서드 (기존 MovePlayer 대신 사용)
        /// </summary>
        public void MovePlayer(Vector2Int newPosition)
        {
            MovePlayerToGrid(newPosition);
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Player Battle Info")]
        public void DebugPlayerBattleInfo()
        {
            Debug.Log($"=== PlayerBattleManager Debug Info ===");
            Debug.Log($"초기화 상태: {isInitialized}");
            Debug.Log($"플레이어 그리드 위치: {playerGridPosition}");
            Debug.Log($"플레이어 게임오브젝트: {(playerGameObject != null ? "존재" : "없음")}");
            if (playerGameObject != null)
            {
                Debug.Log($"플레이어 월드 위치: {playerGameObject.transform.position}");
            }
            Debug.Log($"GridFieldManager 초기화 상태: {(GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized ? "완료" : "미완료")}");
        }
        #endregion
    }
}