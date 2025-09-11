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

        [Header("플레이어 입장 애니메이션")]
        [SerializeField] private float entranceAnimationDuration = 1.2f; // 입장 애니메이션 시간 (더 빠르게)
        [SerializeField] private float entranceStartOffsetX = -50f; // 화면 왼쪽 시작 위치 오프셋 (화면 완전 밖에서부터)

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
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 초기화 시작");

            // 이미 초기화되어 있고 플레이어 오브젝트가 존재하면 위치만 업데이트
            if (isInitialized && playerGameObject != null)
            {
                if (debugMode)
                    Debug.Log("[PlayerBattleManager] 기존 플레이어 오브젝트 유지, 위치만 업데이트");

                // 기존 플레이어의 위치만 업데이트
                StartCoroutine(UpdateExistingPlayerPosition());
                return;
            }

            // 완전히 새로운 초기화가 필요한 경우
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 새로운 플레이어 초기화");

            // 기존 플레이어 게임오브젝트가 있으면 정리 (안전 장치)
            if (playerGameObject != null)
            {
                if (GridFieldManager.Instance != null)
                {
                    GridFieldManager.Instance.RemoveObjectFromGrid(playerGameObject);
                }
                DestroyImmediate(playerGameObject);
                playerGameObject = null;
            }

            isInitialized = false;

            // GridFieldManager 초기화 대기
            StartCoroutine(InitializeAfterGridReady());
        }

        /// <summary>
        /// 기존 플레이어 위치 업데이트
        /// </summary>
        private IEnumerator UpdateExistingPlayerPosition()
        {
            // GridFieldManager가 초기화될 때까지 대기
            while (GridFieldManager.Instance == null || !GridFieldManager.Instance.IsInitialized)
            {
                yield return null;
            }

            // 플레이어 위치 재설정
            SetPlayerGridPosition(playerGridPosition);

            // 애니메이션 이벤트 재구독
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;
                PlayerManager.OnPlayerAnimationChanged += UpdatePlayerSprite;

                // 스프라이트 업데이트
                UpdatePlayerSprite();
            }

            if (debugMode)
                Debug.Log("[PlayerBattleManager] 기존 플레이어 위치 업데이트 완료");
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

                // UI 상태 즉시 업데이트 (첫 번째 전투와 동일하게)
                PlayerManager.Instance.NotifyAllStatsChanged();

                // 애니메이션 변경 이벤트 구독
                PlayerManager.OnPlayerAnimationChanged -= UpdatePlayerSprite;
                PlayerManager.OnPlayerAnimationChanged += UpdatePlayerSprite;

                // 애니메이션이 시작되도록 강제로 업데이트
                StartCoroutine(DelayedAnimationStart());
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
        /// 플레이어 게임오브젝트 생성 (기본 위치)
        /// </summary>
        private void CreatePlayerGameObject()
        {
            CreatePlayerGameObjectAtPosition(GetPlayerWorldPosition());
        }

        /// <summary>
        /// 플레이어 게임오브젝트를 특정 위치에 생성
        /// </summary>
        private void CreatePlayerGameObjectAtPosition(Vector3 worldPosition)
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
            spriteRenderer.color = Color.white; // 원본 스프라이트 색상 사용
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 10;

            // 지정된 월드 위치에 배치
            playerGameObject.transform.position = worldPosition;

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어를 위치 {worldPosition}에 생성했습니다.");
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

            // PlayerManager에서 현재 상태의 스프라이트 가져오기
            Sprite newSprite = null;
            if (PlayerManager.Instance != null)
            {
                newSprite = PlayerManager.Instance.GetPlayerSprite();
            }

            // 스프라이트가 없으면 기본 스프라이트 사용
            if (newSprite == null)
            {
                newSprite = CreateDefaultPlayerSprite();
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
                        default:
                            spriteRenderer.color = Color.white; // 원본 색상 유지
                            break;
                    }
                }

                if (debugMode)
                {
                    var currentState = PlayerManager.Instance?.CurrentAnimationState ?? PlayerManager.PlayerAnimationState.Idle;
                    Debug.Log($"[PlayerBattleManager] 플레이어 스프라이트 업데이트: {currentState}");
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

        /// <summary>
        /// 지연된 애니메이션 시작 (플레이어 게임오브젝트가 완전히 생성된 후)
        /// </summary>
        private System.Collections.IEnumerator DelayedAnimationStart()
        {
            // 한 프레임 대기 (플레이어 게임오브젝트 완전 생성 대기)
            yield return null;

            if (PlayerManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[PlayerBattleManager] 지연된 애니메이션 시작 트리거");

                // 강제로 Idle 애니메이션 재시작
                PlayerManager.Instance.ForceSetAnimationState(PlayerManager.PlayerAnimationState.Idle);

                // 즉시 스프라이트 업데이트
                UpdatePlayerSprite();
            }
        }
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
        /// 플레이어 입장 애니메이션 실행 (화면 왼쪽에서 걸어와서 그리드 위치에 도착)
        /// </summary>
        public System.Collections.IEnumerator PlayPlayerEntranceAnimation()
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 플레이어 입장 애니메이션 시작");

            // 최종 목표 위치 (그리드 위치)
            Vector3 targetWorldPosition = GetPlayerWorldPosition();

            // 카메라를 기준으로 화면 완전 밖에서 시작하도록 계산
            Vector3 startPosition = CalculateOffScreenStartPosition(targetWorldPosition);

            // 플레이어 게임오브젝트가 없으면 시작 위치에서 생성
            if (playerGameObject == null)
            {
                CreatePlayerGameObjectAtPosition(startPosition);
            }
            else
            {
                // 이미 존재하면 시작 위치로 즉시 이동
                playerGameObject.transform.position = startPosition;
            }

            if (playerGameObject == null)
            {
                Debug.LogError("[PlayerBattleManager] 플레이어 게임오브젝트 생성 실패!");
                yield break;
            }

            // PlayerManager에 Walk 애니메이션 시작 알림
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PlayWalkAnimation();
            }

            if (debugMode)
                Debug.Log($"[PlayerBattleManager] 플레이어 이동 시작: {startPosition} -> {targetWorldPosition}");

            // DOTween을 사용한 부드러운 이동 (DOTween이 없으면 기본 코루틴 사용)
            bool useDOTween = false;

            // DOTween 사용 가능 여부 확인
            try
            {
                // DOTween 클래스가 있는지 확인
                var doTweenType = System.Type.GetType("DG.Tweening.DOTween, DOTween");
                useDOTween = doTweenType != null;
            }
            catch
            {
                useDOTween = false;
            }

            if (useDOTween)
            {
                // DOTween 사용
                yield return StartCoroutine(PlayEntranceAnimationWithDOTween(startPosition, targetWorldPosition));
            }
            else
            {
                // 기본 코루틴 사용
                yield return StartCoroutine(PlayEntranceAnimationWithCoroutine(startPosition, targetWorldPosition));
            }

            // 애니메이션 완료 후 Idle 상태로 변경
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ReturnToIdle();
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
        /// 기본 코루틴을 사용한 입장 애니메이션
        /// </summary>
        private System.Collections.IEnumerator PlayEntranceAnimationWithCoroutine(Vector3 startPos, Vector3 targetPos)
        {
            if (debugMode)
                Debug.Log("[PlayerBattleManager] 기본 코루틴 입장 애니메이션 시작");

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