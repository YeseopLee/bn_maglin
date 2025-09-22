using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private bool debugMode = false;

        [Header("플레이어 입장 애니메이션")]
        [SerializeField] private float entranceAnimationDuration = 50f; // 입장 애니메이션 시간 (여유롭게)
        [SerializeField] private float entranceStartOffsetX = -50f; // 화면 왼쪽 시작 위치 오프셋 (화면 완전 밖에서부터)

        [Header("사망 카메라 효과")]
        [SerializeField] private float deathCameraZoomScale = 0.5f; // 사망 시 카메라 확대 배율 (작을수록 더 확대)
        [SerializeField] private float deathCameraZoomDuration = 0.8f; // 카메라 확대/축소 애니메이션 시간 (빠르게)

        [Header("사망 시각 효과")]
        [SerializeField] private float deathEffectFadeTime = 1f; // 사망 효과 페이드 인 시간
        [SerializeField] private float whiteOverlayMaxAlpha = 0.7f; // 하얀 오버레이 최대 투명도 (더 강하게)

        [Header("피격 효과")]
        [SerializeField] private float hitShakeDuration = 0.3f; // 화면 떨림 지속 시간
        [SerializeField] private float hitShakeIntensity = 0.15f; // 화면 떨림 강도
        [SerializeField] private float hitVignetteFadeTime = 0.3f; // 빨간 비네팅 페이드 시간
        [SerializeField] private float hitVignetteMaxAlpha = 0.2f; // 빨간 비네팅 최대 투명도

        // 플레이어 위치 설정 (Grid 기반)
        private Vector2Int playerGridPosition = new Vector2Int(0, 0);

        // 플레이어 게임오브젝트
        [Header("플레이어 설정")]
        [SerializeField] private GameObject playerPrefab; // 씬에 배치된 플레이어 오브젝트
        private GameObject playerGameObject = null;
        private bool isInitialized = false;

        // 사망 카메라 효과용
        private float originalCameraSize;
        private Vector3 originalCameraPosition;
        private bool isDeathCameraActive = false;

        // 사망 시각 효과용
        private List<SpriteRenderer> allSpriteRenderers = new List<SpriteRenderer>();
        private List<Color> originalColors = new List<Color>();
        private bool isDeathVisualEffectActive = false;
        private GameObject deathOverlayObject;
        private SpriteRenderer deathOverlayRenderer;

        // UI 숨기기용
        private List<Canvas> allCanvases = new List<Canvas>();
        private List<bool> originalCanvasStates = new List<bool>();

        // 플레이어 렌더러 레이어 관리용
        private SpriteRenderer playerSpriteRenderer;
        private int originalPlayerSortingOrder;

        // 입장 애니메이션 상태 관리
        private bool isPlayingEntranceAnimation = false;
        private bool hasPlayedEntranceAnimation = false; // 입장 애니메이션 완료 여부

        // 피격 효과 관련
        private bool isHitEffectActive = false;
        private GameObject hitVignetteObject;
        private SpriteRenderer hitVignetteRenderer;
        private Coroutine hitEffectCoroutine;

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
                // 이벤트 구독 해제
                PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;
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
                    Debug.Log("[PlayerBattleManager] 이미 초기화됨");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] PlayerBattleManager 초기화 시작");

            // 씬에 배치된 플레이어 찾기
            FindPlayerInScene();

            // 그리드 초기화 대기
            StartCoroutine(InitializeAfterGridReady());
        }

        /// <summary>
        /// 씬에 배치된 플레이어 오브젝트 찾기
        /// </summary>
        private void FindPlayerInScene()
        {
            // 1. Inspector에서 직접 할당된 경우
            if (playerPrefab != null)
            {
                playerGameObject = playerPrefab;
                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] Inspector에서 할당된 플레이어 사용: {playerGameObject.name}");
                return;
            }

            // 2. "Player" 태그로 찾기
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
            {
                playerGameObject = foundPlayer;
                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] Player 태그로 플레이어 발견: {playerGameObject.name}");
                return;
            }

            // 3. 이름으로 찾기
            foundPlayer = GameObject.Find("Player");
            if (foundPlayer != null)
            {
                playerGameObject = foundPlayer;
                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] 이름으로 플레이어 발견: {playerGameObject.name}");
                return;
            }

            Debug.LogError("[PlayerBattleManager] 씬에서 플레이어 오브젝트를 찾을 수 없습니다! 다음 중 하나를 수행하세요:\n" +
                          "1. PlayerBattleManager의 Player Prefab 필드에 플레이어 오브젝트 할당\n" +
                          "2. 플레이어 오브젝트에 'Player' 태그 설정\n" +
                          "3. 플레이어 오브젝트 이름을 'Player'로 설정");
        }

        /// <summary>
        /// 기존 플레이어 위치 업데이트 (이미 존재하는 플레이어)
        /// </summary>
        private IEnumerator UpdateExistingPlayerPosition()
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 기존 플레이어 위치 업데이트 시작");

            // Grid가 초기화될 때까지 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (playerGameObject == null)
            {
                Debug.LogError("[PlayerBattleManager] 플레이어 오브젝트가 없습니다!");
                yield break;
            }

            // 기존 플레이어의 현재 위치를 그리드 위치로 변환
            Vector3 currentWorldPos = playerGameObject.transform.position;
            Vector2Int gridPos = GridFieldManager.Instance.WorldToGridPosition(currentWorldPos);

            // 유효하지 않은 그리드 위치인 경우 기본 위치(0,0)로 설정
            if (!GridFieldManager.Instance.IsValidGridPosition(gridPos))
            {
                gridPos = new Vector2Int(0, 0);
                if (debugMode)
                    Debug.LogWarning($"[PlayerBattleManager] 플레이어가 유효하지 않은 위치에 있어서 기본 위치(0,0)로 이동합니다.");
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 기존 플레이어 위치: 월드={currentWorldPos}, 그리드={gridPos}");

            // 그리드에 등록
            GridFieldManager.Instance.PlaceObjectAtGrid(playerGameObject, gridPos, true);

            // 플레이어 그리드 위치 설정
            playerGridPosition = gridPos;

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 기존 플레이어 그리드 등록 완료: {gridPos}");
        }

        /// <summary>
        /// 그리드 시스템이 준비된 후 초기화
        /// </summary>
        private IEnumerator InitializeAfterGridReady()
        {
            // GridFieldManager가 초기화될 때까지 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] GridFieldManager 초기화 완료, 플레이어 설정 시작");

            // 플레이어가 있는지 확인
            if (playerGameObject != null)
            {
                // 기존 플레이어 위치 업데이트
                yield return StartCoroutine(UpdateExistingPlayerPosition());

                // 플레이어 스프라이트 렌더러 참조 설정
                SetupPlayerSpriteRenderer();

                // PlayerManager에 전투 시작 알림
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.OnBattleStart();

                    // UI 상태 즉시 업데이트 (첫 번째 전투와 동일하게)
                    PlayerManager.Instance.NotifyAllStatsChanged();

                    // 애니메이션 변경 이벤트 구독
                    PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;
                    PlayerManager.OnPlayerAnimationChanged += UpdatePlayerSprite;

                    // 즉시 스프라이트 업데이트 (플레이어 프리팹 스프라이트를 PlayerManager 스프라이트로 덮어씀)
                    UpdatePlayerSprite();
                }

                // 입장 애니메이션은 BattleTestController에서 호출하므로 여기서는 제거
            }
            else
            {
                Debug.LogError("[PlayerBattleManager] 플레이어 오브젝트가 없습니다! 씬에 플레이어를 배치해주세요.");
                yield break;
            }

            isInitialized = true;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 초기화 완료");
        }

        /// <summary>
        /// 플레이어 스프라이트 렌더러 설정
        /// </summary>
        private void SetupPlayerSpriteRenderer()
        {
            if (playerGameObject != null)
            {
                playerSpriteRenderer = playerGameObject.GetComponent<SpriteRenderer>();
                if (playerSpriteRenderer != null)
                {
                    originalPlayerSortingOrder = playerSpriteRenderer.sortingOrder;

                    // 플레이어 프리팹의 스프라이트를 PlayerManager의 현재 스프라이트로 즉시 교체
                    if (PlayerManager.Instance != null)
                    {
                        Sprite currentSprite = PlayerManager.Instance.GetCurrentSprite();
                        if (currentSprite != null)
                        {
                            playerSpriteRenderer.sprite = currentSprite;
                            if (debugMode)
                                Debug.Log($"[PlayerBattleManager] 플레이어 프리팹 스프라이트를 PlayerManager 스프라이트로 교체: {currentSprite.name}");
                        }
                    }

                    if (debugMode)
                        Debug.Log($"[PlayerBattleManager] 플레이어 스프라이트 렌더러 설정 완료. 원래 Sorting Order: {originalPlayerSortingOrder}");
                }
                else
                {
                    Debug.LogWarning("[PlayerBattleManager] 플레이어 오브젝트에 SpriteRenderer가 없습니다!");
                }
            }
        }

        /// <summary>
        /// 전투 종료 시 정리
        /// </summary>
        public void OnBattleEnd()
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 전투 종료 처리");

            // 이벤트 구독 해제
            PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;

            // PlayerManager에 전투 종료 알림
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnBattleEnd();
            }

            // 플레이어 게임오브젝트는 씬 전환 시에만 정리되도록 수정
            // (전투 종료 시에는 플레이어를 유지하여 다음 전투에서 연속성 보장)
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 플레이어 오브젝트 유지 (씬 전환 시까지)");

            // 입장 애니메이션 플래그 리셋 (다음 전투를 위해)
            hasPlayedEntranceAnimation = false;
            isPlayingEntranceAnimation = false;

            // 초기화 상태만 리셋 (재초기화를 위해)
            // isInitialized = false;  // 이것도 주석 처리하여 플레이어 상태 유지
        }

        /// <summary>
        /// 씬 전환 시 플레이어 정리 (명시적 호출)
        /// </summary>
        public void CleanupOnSceneTransition()
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 씬 전환으로 인한 플레이어 정리");

            // GridFieldManager에서 플레이어 제거
            if (playerGameObject != null && GridFieldManager.Instance != null)
            {
                GridFieldManager.Instance.RemoveObjectFromGrid(playerGameObject);
            }

            // 플레이어 게임오브젝트 정리
            if (playerGameObject != null)
            {
                Destroy(playerGameObject);
                playerGameObject = null;
            }

            isInitialized = false;

            // 입장 애니메이션 플래그도 리셋
            hasPlayedEntranceAnimation = false;
            isPlayingEntranceAnimation = false;
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

            // 입장 애니메이션 중에는 GridFieldManager 위치 재설정을 건너뜀 (물리 애니메이션 보호)
            if (isPlayingEntranceAnimation)
            {
                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] 입장 애니메이션 중이므로 GridFieldManager 위치 재설정 건너뜀: {gridPosition}");
                return;
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
        /// 현재 플레이어 월드 위치 반환 (스프라이트 정렬 적용)
        /// </summary>
        public Vector3 GetPlayerWorldPosition()
        {
            if (GridFieldManager.Instance != null)
            {
                // 스프라이트 pivot이 올바르게 설정되었으면 기본 그리드 위치 사용
                return GridFieldManager.Instance.GridToWorldPosition(playerGridPosition);
            }
            return Vector3.zero;
        }
        #endregion

        #region Player GameObject Management
        // 플레이어 자동 생성 로직 제거됨
        // 이제 씬에 미리 배치된 플레이어 오브젝트를 사용함

        /// <summary>
        /// 플레이어 스프라이트 업데이트 (애니메이션 상태 변경 시 호출)
        /// </summary>
        public void UpdatePlayerSprite(PlayerManager.PlayerAnimationState animationState)
        {
            if (playerGameObject == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 플레이어 게임오브젝트가 없어 스프라이트를 업데이트할 수 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 스프라이트 업데이트 요청 - 애니메이션 상태: {animationState}");

            // PlayerManager에서 현재 상태의 스프라이트 가져오기
            Sprite newSprite = null;
            if (PlayerManager.Instance != null)
            {
                newSprite = PlayerManager.Instance.GetPlayerSprite();

                if (debugMode)
                {
                    var currentState = PlayerManager.Instance.CurrentAnimationState;
                    Debug.Log($"[PlayerBattleManager] PlayerManager 현재 상태: {currentState}");
                    Debug.Log($"[PlayerBattleManager] 가져온 스프라이트: {(newSprite != null ? newSprite.name : "null")}");
                }
            }

            // 스프라이트가 없으면 경고 출력
            if (newSprite == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] PlayerManager에서 스프라이트를 가져올 수 없습니다. 플레이어 오브젝트의 SpriteRenderer에 기본 스프라이트를 설정하세요.");
                return; // 스프라이트가 없으면 업데이트하지 않음
            }

            // SpriteRenderer 컴포넌트 찾아서 스프라이트 업데이트
            var spriteRenderer = playerGameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = newSprite;

                // 피격 상태에서만 색상 변경 (원본 스프라이트 색상 유지)
                if (PlayerManager.Instance != null)
                {
                    var currentState = PlayerManager.Instance.CurrentAnimationState;
                    switch (currentState)
                    {
                        case PlayerManager.PlayerAnimationState.Hit:
                            spriteRenderer.color = new Color(1f, 0.5f, 0.5f, 1f); // 약간 빨간색 틴트
                            break;
                        case PlayerManager.PlayerAnimationState.Death:
                            spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 1f); // 약간 어둡게
                            break;
                        case PlayerManager.PlayerAnimationState.Attacking:
                            spriteRenderer.color = Color.white; // Attacking 상태에서는 원본 색상 유지 (스프라이트 자체 색상 표시)
                            break;
                        default:
                            spriteRenderer.color = Color.white; // 원본 색상 유지
                            break;
                    }
                }

                if (debugMode)
                {
                    var currentState = PlayerManager.Instance?.CurrentAnimationState ?? PlayerManager.PlayerAnimationState.Idle;
                    Debug.Log($"[PlayerBattleManager] 플레이어 스프라이트 업데이트 완료: {currentState} -> {newSprite.name}");
                    Debug.Log($"[PlayerBattleManager] SpriteRenderer에 실제 적용된 스프라이트: {spriteRenderer.sprite.name}");
                }
            }
            else
            {
                Debug.LogError("[PlayerBattleManager] 플레이어 게임오브젝트에 SpriteRenderer 컴포넌트가 없습니다!");
            }
        }

        /// <summary>
        /// 플레이어 스프라이트 업데이트 (매개변수 없는 버전 - 호환성 유지)
        /// </summary>
        public void UpdatePlayerSprite()
        {
            if (PlayerManager.Instance != null)
            {
                UpdatePlayerSprite(PlayerManager.Instance.CurrentAnimationState);
            }
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
        /// 플레이어 위치 변경 (즉시 이동)
        /// </summary>
        public void MovePlayerToGrid(Vector2Int newGridPosition)
        {
            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 즉시 이동: {playerGridPosition} -> {newGridPosition}");

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
                else
                {
                    // 성공 시 즉시 월드 위치 동기화
                    Vector3 worldPos = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, newGridPosition);
                    playerGameObject.transform.position = worldPos;
                }
            }
        }

        /// <summary>
        /// 플레이어 부드러운 타일 이동 (애니메이션 포함)
        /// </summary>
        public System.Collections.IEnumerator MovePlayerToGridAnimated(Vector2Int newGridPosition, float moveDuration = 0.5f)
        {
            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 애니메이션 이동: {playerGridPosition} -> {newGridPosition}");

            // 이동 불가능한 경우 체크
            if (GridFieldManager.Instance == null || playerGameObject == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] GridFieldManager 또는 플레이어가 없어서 이동할 수 없습니다.");
                yield break;
            }

            // 유효한 위치인지 확인
            if (!GridFieldManager.Instance.IsValidGridPosition(newGridPosition))
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerBattleManager] 유효하지 않은 그리드 위치: {newGridPosition}");
                yield break;
            }

            // 현재 위치와 목표 위치
            Vector2Int startGrid = playerGridPosition;
            Vector3 startWorldPos = playerGameObject.transform.position;
            Vector3 targetWorldPos = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, newGridPosition);

            // 걷기 애니메이션 시작
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PlayWalkAnimation();
            }

            // 부드러운 이동
            yield return StartCoroutine(SmoothMoveToPosition(targetWorldPos, moveDuration));

            // 이동 완료 후 그리드에 등록
            SetPlayerGridPosition(newGridPosition);
            GridFieldManager.Instance.PlaceObjectAtGrid(playerGameObject, newGridPosition, true);

            // Idle 애니메이션으로 변경
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ReturnToIdle();
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 애니메이션 이동 완료: {newGridPosition}");
        }

        /// <summary>
        /// 레거시 호환성을 위한 메서드 (기존 MovePlayer 대신 사용)
        /// </summary>
        public void MovePlayer(Vector2Int newPosition)
        {
            MovePlayerToGrid(newPosition);
        }

        // DelayedAnimationStart 메서드는 제거됨 - 이제 입장 애니메이션이 직접 호출됨
        /// <summary>
        /// 화면 밖 시작 위치 계산 (카메라 기준)
        /// </summary>
        private Vector3 CalculateOffScreenStartPosition(Vector3 targetPosition)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // 카메라가 없으면 기본 오프셋 사용
                return new Vector3(
                    targetPosition.x + entranceStartOffsetX,
                    targetPosition.y,
                    targetPosition.z
                );
            }

            // 카메라의 화면 경계 계산
            float cameraHeight = 2f * mainCamera.orthographicSize;
            float cameraWidth = cameraHeight * mainCamera.aspect;

            // 카메라 중심점
            Vector3 cameraCenter = mainCamera.transform.position;

            // 화면 왼쪽 경계에서 추가로 여유분만큼 더 왼쪽에 배치
            float leftScreenEdge = cameraCenter.x - (cameraWidth / 2f);
            float offScreenBuffer = 5f; // 화면 경계에서 추가로 5유닛 더 밖에

            Vector3 startPosition = new Vector3(
                leftScreenEdge - offScreenBuffer,
                targetPosition.y,
                targetPosition.z
            );

            if (debugMode)
            {
                Debug.Log($"[PlayerBattleManager] 화면 밖 시작 위치 계산:");
                Debug.Log($"  - 카메라 중심: {cameraCenter}");
                Debug.Log($"  - 카메라 크기: {cameraWidth} x {cameraHeight}");
                Debug.Log($"  - 화면 왼쪽 경계: {leftScreenEdge}");
                Debug.Log($"  - 시작 위치: {startPosition}");
                Debug.Log($"  - 목표 위치: {targetPosition}");
            }

            return startPosition;
        }
        #endregion

        #region Player Entrance Animation
        /// <summary>
        /// 플레이어 입장 애니메이션 실행 (타일맵 기반 - 한 칸씩 이동)
        /// </summary>
        public System.Collections.IEnumerator PlayPlayerEntranceAnimation()
        {
            // 이미 입장 애니메이션을 완료했거나 실행 중이면 건너뜀
            if (hasPlayedEntranceAnimation || isPlayingEntranceAnimation)
            {
                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] 입장 애니메이션 건너뜀 - 완료됨: {hasPlayedEntranceAnimation}, 실행중: {isPlayingEntranceAnimation}");
                yield break;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 플레이어 입장 애니메이션 시작 (타일맵 기반)");

            // 입장 애니메이션 플래그 설정
            isPlayingEntranceAnimation = true;

            // 플레이어가 없으면 애니메이션 실행 불가
            if (playerGameObject == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 플레이어 게임오브젝트가 없어 입장 애니메이션을 실행할 수 없습니다.");

                isPlayingEntranceAnimation = false;
                yield break;
            }

            // GridFieldManager 확인
            if (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] GridFieldManager가 초기화되지 않아 입장 애니메이션을 건너뜁니다.");

                isPlayingEntranceAnimation = false;
                yield break;
            }

            // Rigidbody2D 컴포넌트 확인 (물리 기반 이동을 위해 필수)
            Rigidbody2D playerRb = playerGameObject.GetComponent<Rigidbody2D>();
            if (playerRb == null)
            {
                if (debugMode)
                    Debug.LogError("[PlayerBattleManager] 플레이어에 Rigidbody2D가 없어 물리 기반 입장 애니메이션을 실행할 수 없습니다!");

                isPlayingEntranceAnimation = false;
                yield break;
            }

            // 현재 플레이어의 목표 그리드 위치
            Vector2Int targetGridPosition = playerGridPosition;

            // 화면 밖 시작 위치 계산 (타일맵 위쪽에서 시작하여 떨어지면서 입장)
            Vector2Int startGridPosition = new Vector2Int(
                targetGridPosition.x - 8, // 왼쪽으로 8칸
                targetGridPosition.y + 1   // 타일맵 위 1칸 높이에서 시작 (중력으로 떨어짐)
            );

            // 물리적으로 시작 위치로 텔레포트
            Vector3 startWorldPosition = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, startGridPosition);
            playerGameObject.transform.position = startWorldPosition;

            // 플레이어가 바닥에 떨어질 때까지 대기 (물리 시뮬레이션)
            yield return new WaitForSeconds(0.5f);

            if (debugMode)
            {
                Vector3 targetWorldPosition = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, targetGridPosition);
                Debug.Log($"[PlayerBattleManager] 물리 기반 입장 애니메이션 설정:");
                Debug.Log($"[PlayerBattleManager] 시작 그리드: {startGridPosition}, 목표 그리드: {targetGridPosition}");
                Debug.Log($"[PlayerBattleManager] 시작 월드: {startWorldPosition}, 목표 월드: {targetWorldPosition}");
                Debug.Log($"[PlayerBattleManager] Rigidbody2D 상태: isKinematic={playerRb.isKinematic}, gravityScale={playerRb.gravityScale}");
            }

            // 애니메이션 이벤트 구독이 되어 있는지 확인하고 설정
            if (PlayerManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[PlayerBattleManager] PlayerManager 인스턴스 찾음, 이벤트 구독 시작");

                // 기존 구독 해제 후 재구독 (중복 방지)
                PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;
                PlayerManager.OnPlayerAnimationChanged += UpdatePlayerSprite;

                if (debugMode)
                    Debug.Log("[PlayerBattleManager] Walk 애니메이션 시작 전 이벤트 구독 설정 완료");

                // PlayerManager에 Walk 애니메이션 시작 알림
                PlayerManager.Instance.PlayWalkAnimation();

                // 한 프레임 대기 후 스프라이트 즉시 업데이트
                yield return null;

                if (debugMode)
                    Debug.Log("[PlayerBattleManager] 수동으로 UpdatePlayerSprite 호출");

                UpdatePlayerSprite();
            }
            else
            {
                if (debugMode)
                    Debug.LogError("[PlayerBattleManager] PlayerManager.Instance가 null입니다!");
            }

            // 물리 기반 횡이동 애니메이션 실행
            yield return StartCoroutine(PlayPhysicsBasedEntranceAnimation(targetGridPosition, playerRb));

            // 애니메이션 완료 후 Idle 상태로 변경
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ReturnToIdle();
            }

            // 입장 애니메이션 플래그 해제 및 완료 표시
            isPlayingEntranceAnimation = false;
            hasPlayedEntranceAnimation = true;

            // 애니메이션 완료 후 GridFieldManager에 최종 위치 등록 (위치 재설정 없이)
            if (GridFieldManager.Instance != null && playerGameObject != null)
            {
                // 현재 물리적 위치를 기준으로 그리드 위치 업데이트 (위치 재설정하지 않음)
                Vector3 currentPos = playerGameObject.transform.position;
                Vector2Int actualGridPos = GridFieldManager.Instance.WorldToGridPosition(currentPos);

                // 내부 그리드 상태만 업데이트 (PlaceObjectAtGrid 사용하지 않음)
                var gridObjects = typeof(GridFieldManager).GetField("gridObjects",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var objectPositions = typeof(GridFieldManager).GetField("objectPositions",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (gridObjects != null && objectPositions != null)
                {
                    var gridDict = gridObjects.GetValue(GridFieldManager.Instance) as System.Collections.Generic.Dictionary<Vector2Int, GameObject>;
                    var posDict = objectPositions.GetValue(GridFieldManager.Instance) as System.Collections.Generic.Dictionary<GameObject, Vector2Int>;

                    if (gridDict != null && posDict != null)
                    {
                        // 기존 위치 제거
                        if (posDict.ContainsKey(playerGameObject))
                        {
                            Vector2Int oldPos = posDict[playerGameObject];
                            if (gridDict.ContainsKey(oldPos))
                                gridDict.Remove(oldPos);
                        }

                        // 새 위치 등록 (물리적 위치 변경 없이)
                        gridDict[actualGridPos] = playerGameObject;
                        posDict[playerGameObject] = actualGridPos;
                        playerGridPosition = actualGridPos;

                        if (debugMode)
                            Debug.Log($"[PlayerBattleManager] 입장 애니메이션 완료 후 그리드 상태 업데이트: {actualGridPos}");
                    }
                }
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 플레이어 입장 애니메이션 완료");
        }

        /// <summary>
        /// DOTween을 사용한 입장 애니메이션
        /// </summary>
        private System.Collections.IEnumerator PlayEntranceAnimationWithDOTween(Vector3 startPos, Vector3 targetPos)
        {
            bool doTweenSuccess = false;

            // DOTween 사용 시도
            doTweenSuccess = TryExecuteDOTweenAnimation(startPos, targetPos);

            if (doTweenSuccess)
            {
                // DOTween 애니메이션 실행 성공 - 완료까지 대기
                yield return StartCoroutine(WaitForDOTweenCompletion());

                if (debugMode)
                    Debug.Log("[PlayerBattleManager] DOTween 입장 애니메이션 완료");
            }
            else
            {
                // DOTween 실패 시 기본 애니메이션으로 폴백
                if (debugMode)
                    Debug.Log("[PlayerBattleManager] DOTween 사용 실패, 기본 애니메이션으로 폴백");

                yield return StartCoroutine(PlayEntranceAnimationWithCoroutine(startPos, targetPos));
            }
        }

        /// <summary>
        /// DOTween 애니메이션 실행 시도 (try-catch 포함)
        /// </summary>
        private bool TryExecuteDOTweenAnimation(Vector3 startPos, Vector3 targetPos)
        {
            try
            {
                // 리플렉션을 사용하여 DOTween 호출
                var doTweenType = System.Type.GetType("DG.Tweening.DOTween, DOTween");
                var tweenerType = System.Type.GetType("DG.Tweening.Tweener, DOTween");
                var easeType = System.Type.GetType("DG.Tweening.Ease, DOTween");

                if (doTweenType != null && tweenerType != null && easeType != null)
                {
                    // DOTween.To() 메서드 호출
                    var toMethod = doTweenType.GetMethod("To", new System.Type[] {
                        typeof(System.Func<Vector3>),
                        typeof(System.Action<Vector3>),
                        typeof(Vector3),
                        typeof(float)
                    });

                    if (toMethod != null)
                    {
                        // Tween 생성
                        var tween = toMethod.Invoke(null, new object[] {
                            new System.Func<Vector3>(() => playerGameObject.transform.position),
                            new System.Action<Vector3>(pos => playerGameObject.transform.position = pos),
                            targetPos,
                            entranceAnimationDuration
                        });

                        // Ease 설정 (OutQuad)
                        var setEaseMethod = tweenerType.GetMethod("SetEase", new System.Type[] { easeType });
                        if (setEaseMethod != null)
                        {
                            var outQuadValue = System.Enum.Parse(easeType, "OutQuad");
                            setEaseMethod.Invoke(tween, new object[] { outQuadValue });
                        }

                        // 완료 콜백 설정
                        var onCompleteMethod = tweenerType.GetMethod("OnComplete", new System.Type[] { typeof(System.Action) });
                        if (onCompleteMethod != null)
                        {
                            onCompleteMethod.Invoke(tween, new object[] { new System.Action(() => doTweenAnimationCompleted = true) });
                        }

                        return true; // 성공
                    }
                }
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerBattleManager] DOTween 사용 실패: {e.Message}");
            }

            return false; // 실패
        }

        // DOTween 애니메이션 완료 플래그
        private bool doTweenAnimationCompleted = false;

        /// <summary>
        /// DOTween 애니메이션 완료 대기
        /// </summary>
        private System.Collections.IEnumerator WaitForDOTweenCompletion()
        {
            doTweenAnimationCompleted = false;
            yield return new WaitUntil(() => doTweenAnimationCompleted);
        }

        /// <summary>
        /// 물리 기반 입장 애니메이션 (실제 물리 시뮬레이션 사용)
        /// </summary>
        private System.Collections.IEnumerator PlayPhysicsBasedEntranceAnimation(Vector2Int targetGrid, Rigidbody2D playerRb)
        {
            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 물리 기반 입장 애니메이션 시작 → 목표: {targetGrid}");

            // 목표 위치 계산
            Vector3 targetWorldPosition = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, targetGrid);

            // 현재 위치에서 목표까지의 거리 계산
            Vector3 currentPosition = playerGameObject.transform.position;
            float distanceToTarget = Mathf.Abs(targetWorldPosition.x - currentPosition.x);

            // 이동 속도 계산 (거리를 시간으로 나눔)
            float moveSpeed = distanceToTarget / entranceAnimationDuration;

            if (debugMode)
            {
                Debug.Log($"[PlayerBattleManager] 현재 위치: {currentPosition}");
                Debug.Log($"[PlayerBattleManager] 목표 위치: {targetWorldPosition}");
                Debug.Log($"[PlayerBattleManager] 이동 거리: {distanceToTarget:F2}, 속도: {moveSpeed:F2}");
                Debug.Log($"[PlayerBattleManager] Rigidbody2D 상태: isKinematic={playerRb.isKinematic}, gravityScale={playerRb.gravityScale}");
            }

            // 물리 기반 수평 이동 시작
            float elapsedTime = 0f;
            Vector3 startPosition = currentPosition;

            while (elapsedTime < entranceAnimationDuration)
            {
                elapsedTime += Time.fixedDeltaTime;

                // 목표 위치까지의 진행률 계산
                float progress = elapsedTime / entranceAnimationDuration;
                progress = Mathf.Clamp01(progress);

                // 부드러운 이동을 위한 Ease Out 곡선 적용
                float easedProgress = 1f - (1f - progress) * (1f - progress);

                // 목표 X 위치 계산 (Y는 물리 시뮬레이션에 맡김)
                float targetX = Mathf.Lerp(startPosition.x, targetWorldPosition.x, easedProgress);

                // 현재 위치 가져오기 (Y는 물리 시뮬레이션 결과)
                Vector3 physicsPosition = playerGameObject.transform.position;

                // X 위치만 조정하여 물리적으로 이동 (AddForce 대신 velocity 조정)
                Vector2 currentVelocity = playerRb.velocity;
                float velocityX = (targetX - physicsPosition.x) / Time.fixedDeltaTime;

                // 속도 제한 (너무 빠르지 않도록)
                velocityX = Mathf.Clamp(velocityX, -moveSpeed * 2f, moveSpeed * 2f);

                // X축 속도만 설정 (Y축은 물리 시뮬레이션 유지)
                playerRb.velocity = new Vector2(velocityX, currentVelocity.y);

                if (debugMode && elapsedTime % 0.5f < Time.fixedDeltaTime) // 0.5초마다 로그
                {
                    Debug.Log($"[PlayerBattleManager] 물리 이동 진행률: {progress:F2}, 위치: {physicsPosition}, 속도: {playerRb.velocity}");
                }

                yield return new WaitForFixedUpdate(); // 물리 업데이트와 동기화
            }

            // 목표 위치에 정확히 도달하도록 마지막 조정
            Vector3 finalPosition = playerGameObject.transform.position;
            finalPosition.x = targetWorldPosition.x;
            playerGameObject.transform.position = finalPosition;

            // 이동 완료 후 속도를 0으로 설정
            playerRb.velocity = new Vector2(0f, playerRb.velocity.y);

            // 물리 애니메이션 중에는 GridFieldManager 위치 재설정하지 않음
            // (애니메이션 완료 후 한 번에 처리)

            if (debugMode)
            {
                Debug.Log($"[PlayerBattleManager] 물리 기반 입장 애니메이션 완료");
                Debug.Log($"[PlayerBattleManager] 최종 위치: {playerGameObject.transform.position}");
                Debug.Log($"[PlayerBattleManager] 최종 속도: {playerRb.velocity}");
            }
        }

        /// <summary>
        /// 지정된 위치로 부드럽게 이동
        /// </summary>
        private System.Collections.IEnumerator SmoothMoveToPosition(Vector3 targetPosition, float duration)
        {
            Vector3 startPosition = playerGameObject.transform.position;
            float elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // Ease Out 효과 적용
                t = 1f - Mathf.Pow(1f - t, 2f);

                playerGameObject.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            // 정확한 최종 위치 설정
            playerGameObject.transform.position = targetPosition;
        }

        /// <summary>
        /// 기본 코루틴을 사용한 입장 애니메이션 (폴백용 - 사용하지 않음)
        /// </summary>
        private System.Collections.IEnumerator PlayEntranceAnimationWithCoroutine(Vector3 startPos, Vector3 targetPos)
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 기본 코루틴 입장 애니메이션 시작 (폴백)");

            float elapsedTime = 0f;
            Vector3 currentPos = startPos;

            while (elapsedTime < entranceAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / entranceAnimationDuration;

                // Ease Out Quad 곡선 적용
                float easedProgress = 1f - (1f - progress) * (1f - progress);

                // 현재 위치 계산
                currentPos = Vector3.Lerp(startPos, targetPos, easedProgress);
                playerGameObject.transform.position = currentPos;

                yield return null;
            }

            // 최종 위치로 정확히 설정
            playerGameObject.transform.position = targetPos;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 기본 코루틴 입장 애니메이션 완료");
        }
        #endregion

        #region Death Camera Effects
        /// <summary>
        /// 사망 카메라 효과 시작 (플레이어 확대)
        /// </summary>
        public void StartDeathCameraEffect()
        {
            if (isDeathCameraActive)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 사망 카메라 효과가 이미 활성화되어 있습니다.");
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 메인 카메라를 찾을 수 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 사망 카메라 효과 시작 - 플레이어로 확대");

            isDeathCameraActive = true;

            // 원본 카메라 상태 저장
            originalCameraSize = mainCamera.orthographicSize;
            originalCameraPosition = mainCamera.transform.position;

            // 플레이어 위치로 카메라 이동 및 확대
            Vector3 playerWorldPos = GetPlayerWorldPosition();
            Vector3 targetCameraPos = new Vector3(playerWorldPos.x, playerWorldPos.y, originalCameraPosition.z);

            StartCoroutine(AnimateCameraToPlayer(mainCamera, targetCameraPos, originalCameraSize * deathCameraZoomScale));
        }

        /// <summary>
        /// 사망 카메라 효과 종료 (원래 위치로 복구)
        /// </summary>
        public void EndDeathCameraEffect()
        {
            if (!isDeathCameraActive)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 사망 카메라 효과가 활성화되어 있지 않습니다.");
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 메인 카메라를 찾을 수 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 사망 카메라 효과 종료 - 원래 위치로 복구");

            StartCoroutine(AnimateCameraToOriginal(mainCamera));
        }

        /// <summary>
        /// 카메라를 플레이어 위치로 애니메이션
        /// </summary>
        private System.Collections.IEnumerator AnimateCameraToPlayer(Camera camera, Vector3 targetPosition, float targetSize)
        {
            Vector3 startPosition = camera.transform.position;
            float startSize = camera.orthographicSize;

            float elapsedTime = 0f;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 카메라 확대 애니메이션 시작 (슬로우 모션 영향 받음)");

            while (elapsedTime < deathCameraZoomDuration)
            {
                elapsedTime += Time.deltaTime; // deltaTime 사용 (슬로우 모션 영향 받음)
                float progress = elapsedTime / deathCameraZoomDuration;

                // 극적인 Ease In Cubic 곡선 (빠르게 시작해서 급격히 줌인)
                float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

                // 위치와 크기 동시 애니메이션
                camera.transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
                camera.orthographicSize = Mathf.Lerp(startSize, targetSize, easedProgress);

                yield return null;
            }

            // 최종 값으로 정확히 설정
            camera.transform.position = targetPosition;
            camera.orthographicSize = targetSize;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 플레이어로 카메라 이동 완료");
        }

        /// <summary>
        /// 카메라를 원래 위치로 애니메이션
        /// </summary>
        private System.Collections.IEnumerator AnimateCameraToOriginal(Camera camera)
        {
            Vector3 startPosition = camera.transform.position;
            float startSize = camera.orthographicSize;

            float elapsedTime = 0f;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 카메라 줌 아웃 애니메이션 시작 (슬로우 모션 종료 후)");

            while (elapsedTime < deathCameraZoomDuration)
            {
                elapsedTime += Time.deltaTime; // 일반 deltaTime 사용 (슬로우 모션 이미 해제됨)
                float progress = elapsedTime / deathCameraZoomDuration;

                // Ease In-Out Quad 곡선
                float easedProgress = progress < 0.5f ?
                    2f * progress * progress :
                    1f - 2f * (1f - progress) * (1f - progress);

                // 위치와 크기 동시 애니메이션
                camera.transform.position = Vector3.Lerp(startPosition, originalCameraPosition, easedProgress);
                camera.orthographicSize = Mathf.Lerp(startSize, originalCameraSize, easedProgress);

                yield return null;
            }

            // 최종 값으로 정확히 설정
            camera.transform.position = originalCameraPosition;
            camera.orthographicSize = originalCameraSize;

            isDeathCameraActive = false;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 카메라 원래 위치 복구 완료");
        }
        #endregion

        #region Death Visual Effects
        /// <summary>
        /// 사망 시각 효과 시작 (모든 객체 흑백 변환 + 노이즈 오버레이)
        /// </summary>
        public void StartDeathVisualEffect()
        {
            if (isDeathVisualEffectActive)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 사망 시각 효과가 이미 활성화되어 있습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 사망 시각 효과 시작 - 흑백 변환 및 노이즈 오버레이");

            isDeathVisualEffectActive = true;

            // 모든 스프라이트 렌더러 수집 및 색상 저장
            CollectAllSpriteRenderersAndSaveColors();

            // UI 숨기기
            HideAllUI();

            // 플레이어를 최상위 레이어로 이동
            SetPlayerToTopLayer();

            // 하얀 오버레이 생성
            CreateDeathOverlay();

            // 시각 효과 시작
            StartCoroutine(ApplyDeathVisualEffect());
        }

        /// <summary>
        /// 사망 시각 효과 종료 (원상복구)
        /// </summary>
        public void EndDeathVisualEffect()
        {
            if (!isDeathVisualEffectActive)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerBattleManager] 사망 시각 효과가 활성화되어 있지 않습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 사망 시각 효과 종료 - 원상복구");

            StartCoroutine(RestoreOriginalVisualEffect());
        }

        /// <summary>
        /// 몬스터 스프라이트 렌더러만 수집 및 원본 색상 저장 (플레이어 제외)
        /// </summary>
        private void CollectAllSpriteRenderersAndSaveColors()
        {
            allSpriteRenderers.Clear();
            originalColors.Clear();

            // 씬의 모든 SpriteRenderer 찾기
            SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();

            foreach (var renderer in renderers)
            {
                // UI 요소는 제외 (Canvas 하위에 있는 것들)
                if (renderer.GetComponentInParent<Canvas>() != null)
                    continue;

                // 플레이어 오브젝트는 제외 (PlayerManager나 PlayerBattleManager 컴포넌트가 있는 오브젝트)
                bool isPlayerObject = false;
                GameObject obj = renderer.gameObject;

                // 오브젝트 자체나 부모에서 플레이어 관련 컴포넌트 확인
                if (obj.GetComponent<PlayerManager>() != null ||
                    obj.GetComponentInParent<PlayerManager>() != null ||
                    obj.name.ToLower().Contains("player"))
                {
                    isPlayerObject = true;
                }

                if (!isPlayerObject)
                {
                    allSpriteRenderers.Add(renderer);
                    originalColors.Add(renderer.color);
                }
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 몬스터 스프라이트 렌더러 수집 완료: {allSpriteRenderers.Count}개 (플레이어 제외)");
        }

        /// <summary>
        /// 하얀 오버레이 생성
        /// </summary>
        private void CreateDeathOverlay()
        {
            if (deathOverlayObject != null)
            {
                DestroyImmediate(deathOverlayObject);
            }

            // 오버레이 객체 생성
            deathOverlayObject = new GameObject("DeathOverlay");
            deathOverlayRenderer = deathOverlayObject.AddComponent<SpriteRenderer>();

            // 하얀 텍스처 생성
            Texture2D whiteTexture = CreateSolidColorTexture(512, 512, Color.white);
            Sprite whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 512, 512), new Vector2(0.5f, 0.5f), 100f);

            // 오버레이 설정
            deathOverlayRenderer.sprite = whiteSprite;
            deathOverlayRenderer.color = new Color(1f, 1f, 1f, 0f); // 시작은 투명
            deathOverlayRenderer.sortingOrder = 500; // 플레이어보다는 뒤에 표시

            // 카메라 크기에 맞게 스케일 조정
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float cameraHeight = mainCamera.orthographicSize * 2f;
                float cameraWidth = cameraHeight * mainCamera.aspect;
                deathOverlayObject.transform.localScale = new Vector3(cameraWidth / 5.12f, cameraHeight / 5.12f, 1f);
                deathOverlayObject.transform.position = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, 0f);
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 하얀 오버레이 생성 완료");
        }

        /// <summary>
        /// 단색 텍스처 생성
        /// </summary>
        private Texture2D CreateSolidColorTexture(int width, int height, Color color)
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
        /// 사망 시각 효과 적용 (페이드 인)
        /// </summary>
        private System.Collections.IEnumerator ApplyDeathVisualEffect()
        {
            float elapsedTime = 0f;

            while (elapsedTime < deathEffectFadeTime)
            {
                elapsedTime += Time.deltaTime; // 슬로우 모션 영향을 받음
                float progress = elapsedTime / deathEffectFadeTime;

                // 모든 몬스터 스프라이트를 점진적으로 검은색으로 변환
                for (int i = 0; i < allSpriteRenderers.Count; i++)
                {
                    if (allSpriteRenderers[i] != null)
                    {
                        Color originalColor = originalColors[i];
                        Color blackColor = new Color(0f, 0f, 0f, originalColor.a); // 완전히 검은색, 투명도는 유지
                        allSpriteRenderers[i].color = Color.Lerp(originalColor, blackColor, progress);
                    }
                }

                // 하얀 오버레이 페이드 인
                if (deathOverlayRenderer != null)
                {
                    Color overlayColor = deathOverlayRenderer.color;
                    overlayColor.a = whiteOverlayMaxAlpha * progress;
                    deathOverlayRenderer.color = overlayColor;
                }

                yield return null;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 사망 시각 효과 적용 완료");
        }

        /// <summary>
        /// 원본 시각 효과 복구 (페이드 아웃)
        /// </summary>
        private System.Collections.IEnumerator RestoreOriginalVisualEffect()
        {
            float elapsedTime = 0f;

            while (elapsedTime < deathEffectFadeTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / deathEffectFadeTime;

                // 모든 스프라이트를 점진적으로 원본 색상으로 복구
                for (int i = 0; i < allSpriteRenderers.Count; i++)
                {
                    if (allSpriteRenderers[i] != null && i < originalColors.Count)
                    {
                        Color targetColor = originalColors[i];
                        Color currentColor = allSpriteRenderers[i].color;
                        allSpriteRenderers[i].color = Color.Lerp(currentColor, targetColor, progress);
                    }
                }

                // 하얀 오버레이 페이드 아웃
                if (deathOverlayRenderer != null)
                {
                    Color overlayColor = deathOverlayRenderer.color;
                    overlayColor.a = whiteOverlayMaxAlpha * (1f - progress);
                    deathOverlayRenderer.color = overlayColor;
                }

                yield return null;
            }

            // 최종 정리
            for (int i = 0; i < allSpriteRenderers.Count; i++)
            {
                if (allSpriteRenderers[i] != null && i < originalColors.Count)
                {
                    allSpriteRenderers[i].color = originalColors[i];
                }
            }

            // 오버레이 제거
            if (deathOverlayObject != null)
            {
                DestroyImmediate(deathOverlayObject);
                deathOverlayObject = null;
                deathOverlayRenderer = null;
            }

            // UI 복구
            RestoreAllUI();

            // 플레이어 레이어 복구
            RestorePlayerLayer();

            isDeathVisualEffectActive = false;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 원본 시각 효과 및 UI 복구 완료");
        }

        /// <summary>
        /// 모든 UI 캔버스 숨기기
        /// </summary>
        private void HideAllUI()
        {
            allCanvases.Clear();
            originalCanvasStates.Clear();

            // 씬의 모든 Canvas 찾기
            Canvas[] canvases = FindObjectsOfType<Canvas>();

            foreach (var canvas in canvases)
            {
                allCanvases.Add(canvas);
                originalCanvasStates.Add(canvas.enabled);
                canvas.enabled = false; // Canvas 비활성화
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] UI 캔버스 숨기기 완료: {allCanvases.Count}개");
        }

        /// <summary>
        /// 모든 UI 캔버스 복구
        /// </summary>
        private void RestoreAllUI()
        {
            for (int i = 0; i < allCanvases.Count; i++)
            {
                if (allCanvases[i] != null && i < originalCanvasStates.Count)
                {
                    allCanvases[i].enabled = originalCanvasStates[i];
                }
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] UI 캔버스 복구 완료: {allCanvases.Count}개");

            allCanvases.Clear();
            originalCanvasStates.Clear();
        }

        /// <summary>
        /// 플레이어를 최상위 레이어로 이동
        /// </summary>
        private void SetPlayerToTopLayer()
        {
            // 플레이어 게임오브젝트에서 SpriteRenderer 찾기
            if (playerGameObject != null)
            {
                playerSpriteRenderer = playerGameObject.GetComponent<SpriteRenderer>();
                if (playerSpriteRenderer != null)
                {
                    originalPlayerSortingOrder = playerSpriteRenderer.sortingOrder;
                    playerSpriteRenderer.sortingOrder = 1000; // 오버레이(500)보다 높게 설정

                    if (debugMode)
                        Debug.Log($"[PlayerBattleManager] 플레이어 레이어를 최상위로 이동: {originalPlayerSortingOrder} -> 1000");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[PlayerBattleManager] 플레이어 게임오브젝트에서 SpriteRenderer를 찾을 수 없습니다.");
                }
            }
        }

        /// <summary>
        /// 플레이어 레이어 복구
        /// </summary>
        private void RestorePlayerLayer()
        {
            if (playerSpriteRenderer != null)
            {
                playerSpriteRenderer.sortingOrder = originalPlayerSortingOrder;

                if (debugMode)
                    Debug.Log($"[PlayerBattleManager] 플레이어 레이어 복구: 1000 -> {originalPlayerSortingOrder}");
            }
        }
        #endregion

        #region Hit Effects
        /// <summary>
        /// 피격 효과 시작 (화면 떨림 + 빨간 비네팅)
        /// </summary>
        public void StartHitEffect()
        {
            if (isHitEffectActive)
            {
                // 이미 실행 중인 효과가 있으면 중지하고 새로 시작
                EndHitEffect();
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 피격 효과 시작");

            isHitEffectActive = true;

            // 화면 떨림 효과 시작
            StartScreenShake();

            // 빨간 비네팅 효과 시작
            StartHitVignette();
        }

        /// <summary>
        /// 피격 효과 종료
        /// </summary>
        public void EndHitEffect()
        {
            if (!isHitEffectActive) return;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 피격 효과 종료");

            // 진행 중인 효과 코루틴 중지
            if (hitEffectCoroutine != null)
            {
                StopCoroutine(hitEffectCoroutine);
                hitEffectCoroutine = null;
            }

            // 카메라 원위치 복구
            RestoreCameraPosition();

            // 비네팅 오버레이 제거
            if (hitVignetteObject != null)
            {
                DestroyImmediate(hitVignetteObject);
                hitVignetteObject = null;
                hitVignetteRenderer = null;
            }

            isHitEffectActive = false;
        }

        /// <summary>
        /// 화면 떨림 효과 시작
        /// </summary>
        private void StartScreenShake()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            // 원본 카메라 위치 저장 (아직 저장되지 않은 경우에만)
            if (!isDeathCameraActive)
            {
                originalCameraPosition = mainCamera.transform.position;
            }

            // 화면 떨림 코루틴 시작
            hitEffectCoroutine = StartCoroutine(ExecuteScreenShake(mainCamera));
        }

        /// <summary>
        /// 화면 떨림 실행 코루틴
        /// </summary>
        private System.Collections.IEnumerator ExecuteScreenShake(Camera camera)
        {
            Vector3 originalPos = originalCameraPosition;
            float elapsed = 0f;

            while (elapsed < hitShakeDuration)
            {
                elapsed += Time.deltaTime;

                // 감쇠되는 떨림 강도 계산
                float intensity = hitShakeIntensity * (1f - elapsed / hitShakeDuration);

                // 랜덤한 방향으로 떨림
                float offsetX = UnityEngine.Random.Range(-intensity, intensity);
                float offsetY = UnityEngine.Random.Range(-intensity, intensity);

                Vector3 shakePos = new Vector3(
                    originalPos.x + offsetX,
                    originalPos.y + offsetY,
                    originalPos.z
                );

                camera.transform.position = shakePos;

                yield return null;
            }

            // 원래 위치로 복구 (사망 카메라 효과가 활성화되지 않은 경우에만)
            if (!isDeathCameraActive)
            {
                camera.transform.position = originalPos;
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 화면 떨림 효과 완료");
        }

        /// <summary>
        /// 빨간 비네팅 효과 시작
        /// </summary>
        private void StartHitVignette()
        {
            // 기존 비네팅 오브젝트가 있으면 제거
            if (hitVignetteObject != null)
            {
                DestroyImmediate(hitVignetteObject);
            }

            CreateHitVignette();
            StartCoroutine(ExecuteHitVignette());
        }

        /// <summary>
        /// 빨간 비네팅 오버레이 생성
        /// </summary>
        private void CreateHitVignette()
        {
            hitVignetteObject = new GameObject("HitVignette");
            hitVignetteRenderer = hitVignetteObject.AddComponent<SpriteRenderer>();

            // 빨간색 비네팅 텍스처 생성 (가장자리가 어두운 빨간색, 중앙은 투명)
            Texture2D vignetteTexture = CreateVignetteTexture(512, 512);
            Sprite vignetteSprite = Sprite.Create(vignetteTexture, new Rect(0, 0, 512, 512), new Vector2(0.5f, 0.5f), 100f);

            // 비네팅 설정
            hitVignetteRenderer.sprite = vignetteSprite;
            hitVignetteRenderer.color = new Color(1f, 0f, 0f, 0f); // 시작은 투명
            hitVignetteRenderer.sortingOrder = 999; // 가장 앞에 표시

            // 카메라 크기에 맞게 스케일 조정
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float cameraHeight = mainCamera.orthographicSize * 2f;
                float cameraWidth = cameraHeight * mainCamera.aspect;
                hitVignetteObject.transform.localScale = new Vector3(cameraWidth / 5.12f, cameraHeight / 5.12f, 1f);
                hitVignetteObject.transform.position = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, 0f);
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 빨간 비네팅 오버레이 생성 완료");
        }

        /// <summary>
        /// 비네팅 텍스처 생성 (가장자리가 어두운 빨간색)
        /// </summary>
        private Texture2D CreateVignetteTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float maxDistance = Mathf.Min(width, height) * 0.8f; // 0.5f에서 0.3f로 줄여서 효과 범위 축소

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);

                    // 중앙에서 가장자리로 갈수록 알파값 증가
                    float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

                    // 부드러운 그라데이션을 위한 곡선 적용
                    float alpha = Mathf.Pow(normalizedDistance, 1.5f);

                    pixels[y * width + x] = new Color(1f, 0f, 0f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 빨간 비네팅 효과 실행 (페이드 인/아웃)
        /// </summary>
        private System.Collections.IEnumerator ExecuteHitVignette()
        {
            if (hitVignetteRenderer == null) yield break;

            float halfFadeTime = hitVignetteFadeTime * 0.5f;

            // 페이드 인
            float elapsed = 0f;
            while (elapsed < halfFadeTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / halfFadeTime;

                Color vignetteColor = hitVignetteRenderer.color;
                vignetteColor.a = hitVignetteMaxAlpha * progress;
                hitVignetteRenderer.color = vignetteColor;

                yield return null;
            }

            // 페이드 아웃
            elapsed = 0f;
            while (elapsed < halfFadeTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / halfFadeTime;

                Color vignetteColor = hitVignetteRenderer.color;
                vignetteColor.a = hitVignetteMaxAlpha * (1f - progress);
                hitVignetteRenderer.color = vignetteColor;

                yield return null;
            }

            // 비네팅 오버레이 제거
            if (hitVignetteObject != null)
            {
                DestroyImmediate(hitVignetteObject);
                hitVignetteObject = null;
                hitVignetteRenderer = null;
            }

            isHitEffectActive = false;

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 빨간 비네팅 효과 완료");
        }

        /// <summary>
        /// 카메라 위치 복구 (사망 효과가 활성화되지 않은 경우에만)
        /// </summary>
        private void RestoreCameraPosition()
        {
            if (isDeathCameraActive) return; // 사망 카메라 효과가 활성화된 경우 복구하지 않음

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = originalCameraPosition;

                if (debugMode)
                    Debug.Log("[PlayerBattleManager] 카메라 위치 복구 완료");
            }
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