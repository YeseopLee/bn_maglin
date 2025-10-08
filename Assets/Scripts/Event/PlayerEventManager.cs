using System.Collections;
using UnityEngine;
using Maglin.Player;
using Maglin.Battle;

namespace Maglin.Event
{
    /// <summary>
    /// 이벤트 씬에서 플레이어 관리 및 입장 애니메이션을 담당하는 클래스
    /// </summary>
    public class PlayerEventManager : MonoBehaviour
    {
        private static PlayerEventManager _instance;
        public static PlayerEventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerEventManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("[PlayerEventManager] 씬에 PlayerEventManager가 없습니다!");
                    }
                }
                return _instance;
            }
        }

        [Header("플레이어 설정")]
        [SerializeField] private GameObject playerPrefab; // 플레이어 프리팹 (씬에 배치된 오브젝트)
        
        [Header("입장 애니메이션 설정")]
        [SerializeField] private float entranceAnimationDuration = 2.5f; // 입장 애니메이션 시간
        [SerializeField] private float entranceStartOffsetX = -10f; // 화면 왼쪽 시작 위치 오프셋
        [SerializeField] private bool usePhysicsBasedMovement = true; // 물리 기반 이동 사용 여부

        [Header("플레이어 그리드 위치")]
        [SerializeField] private Vector2Int playerGridPosition = new Vector2Int(0, 0); // 플레이어의 목표 그리드 위치

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        // 플레이어 게임오브젝트
        private GameObject playerGameObject = null;
        private bool isInitialized = false;

        // 입장 애니메이션 상태 관리
        private bool isPlayingEntranceAnimation = false;
        private bool hasPlayedEntranceAnimation = false; // 입장 애니메이션 완료 여부

        #region Unity Events
        private void Awake()
        {
            // 싱글톤 패턴 적용
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[PlayerEventManager] 중복된 PlayerEventManager 인스턴스가 제거됩니다.");
                Destroy(gameObject);
                return;
            }
        }

        #endregion

        #region 초기화
        /// <summary>
        /// PlayerEventManager 초기화
        /// </summary>
        public void InitializePlayerEventManager()
        {
            if (isInitialized)
            {
                if (debugMode)
                    Debug.Log("[PlayerEventManager] 이미 초기화되었습니다.");
                return;
            }

            if (debugMode)
                Debug.Log("[PlayerEventManager] 초기화 시작");

            // 씬에서 플레이어 찾기
            FindPlayerInScene();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[PlayerEventManager] 초기화 완료");
        }

        /// <summary>
        /// 씬에서 플레이어 게임오브젝트 찾기
        /// </summary>
        private void FindPlayerInScene()
        {
            // 1. playerPrefab이 설정되어 있으면 해당 오브젝트 사용
            if (playerPrefab != null)
            {
                playerGameObject = playerPrefab;
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] 설정된 플레이어 프리팹 사용: {playerGameObject.name}");
                return;
            }

            // 2. "Player" 태그로 찾기
            GameObject playerByTag = GameObject.FindWithTag("Player");
            if (playerByTag != null)
            {
                playerGameObject = playerByTag;
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] Player 태그로 플레이어 발견: {playerGameObject.name}");
                return;
            }

            // 3. "Player"라는 이름으로 찾기
            GameObject playerByName = GameObject.Find("Player");
            if (playerByName != null)
            {
                playerGameObject = playerByName;
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] Player 이름으로 플레이어 발견: {playerGameObject.name}");
                return;
            }

            // 4. PlayerManager 컴포넌트가 있는 오브젝트 찾기
            PlayerManager playerManager = FindObjectOfType<PlayerManager>();
            if (playerManager != null)
            {
                playerGameObject = playerManager.gameObject;
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] PlayerManager 컴포넌트로 플레이어 발견: {playerGameObject.name}");
                return;
            }

            if (debugMode)
                Debug.LogWarning("[PlayerEventManager] 씬에서 플레이어를 찾을 수 없습니다.");
        }
        #endregion

        #region 입장 애니메이션
        /// <summary>
        /// 플레이어 입장 애니메이션 실행 (물리 기반)
        /// </summary>
        public System.Collections.IEnumerator PlayPlayerEntranceAnimation()
        {
            // 이미 입장 애니메이션을 완료했거나 실행 중이면 건너뜀
            if (hasPlayedEntranceAnimation || isPlayingEntranceAnimation)
            {
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] 입장 애니메이션 건너뜀 - 완료됨: {hasPlayedEntranceAnimation}, 실행중: {isPlayingEntranceAnimation}");
                yield break;
            }

            if (debugMode)
                Debug.Log("[PlayerEventManager] 플레이어 입장 애니메이션 시작 (물리 기반)");

            // 입장 애니메이션 플래그 설정
            isPlayingEntranceAnimation = true;

            // 플레이어가 없으면 애니메이션 실행 불가
            if (playerGameObject == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerEventManager] 플레이어 게임오브젝트가 없어 입장 애니메이션을 실행할 수 없습니다.");

                isPlayingEntranceAnimation = false;
                yield break;
            }

            // 물리 기반 이동을 사용하는 경우 Rigidbody2D 확인
            Rigidbody2D playerRb = null;
            if (usePhysicsBasedMovement)
            {
                playerRb = playerGameObject.GetComponent<Rigidbody2D>();
                if (playerRb == null)
                {
                    if (debugMode)
                        Debug.LogWarning("[PlayerEventManager] 플레이어에 Rigidbody2D가 없어 일반 이동으로 변경합니다.");
                    usePhysicsBasedMovement = false;
                }
            }

            // 현재 플레이어 위치를 시작 위치로 사용 (씬에 배치된 위치 유지)
            Vector3 startPosition = playerGameObject.transform.position;
            Vector3 targetWorldPosition;

            if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized)
            {
                // 그리드 기반 목표 위치 계산 (playerGridPosition으로 설정된 목표 그리드 위치)
                targetWorldPosition = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, playerGridPosition);
                
                if (debugMode)
                {
                    Debug.Log($"[PlayerEventManager] 그리드 기반 위치 계산:");
                    Debug.Log($"[PlayerEventManager] 목표 그리드: {playerGridPosition}");
                    Debug.Log($"[PlayerEventManager] 목표 월드 위치: {targetWorldPosition}");
                    Debug.Log($"[PlayerEventManager] 시작 위치 (씬 배치): {startPosition}");
                    Debug.Log($"[PlayerEventManager] 이동 거리: {Vector3.Distance(startPosition, targetWorldPosition):F2}");
                    
                    // 이동이 필요한지 확인
                    float distanceToTarget = Vector3.Distance(startPosition, targetWorldPosition);
                    if (distanceToTarget < 0.1f)
                    {
                        Debug.LogWarning($"[PlayerEventManager] 시작 위치와 목표 위치가 거의 같습니다! (거리: {distanceToTarget:F3}) 이동 애니메이션이 보이지 않을 수 있습니다.");
                    }
                }
            }
            else
            {
                // GridFieldManager가 없으면 현재 위치에서 오른쪽으로 이동
                targetWorldPosition = new Vector3(
                    startPosition.x + Mathf.Abs(entranceStartOffsetX), // 현재 위치에서 오른쪽으로
                    startPosition.y, 
                    startPosition.z
                );
                
                if (debugMode)
                {
                    Debug.LogWarning("[PlayerEventManager] GridFieldManager가 없어 현재 위치 기준으로 목표 위치를 계산합니다.");
                    Debug.Log($"[PlayerEventManager] 시작 위치 (씬 배치): {startPosition}");
                    Debug.Log($"[PlayerEventManager] 목표 위치 (계산됨): {targetWorldPosition}");
                }
            }

            // 플레이어 위치는 변경하지 않음 (씬에 배치된 위치 유지)

            if (debugMode)
            {
                Debug.Log($"[PlayerEventManager] 입장 애니메이션 설정:");
                Debug.Log($"[PlayerEventManager] 시작 위치: {startPosition}");
                Debug.Log($"[PlayerEventManager] 목표 위치: {targetWorldPosition}");
                Debug.Log($"[PlayerEventManager] 애니메이션 시간: {entranceAnimationDuration}초");
                Debug.Log($"[PlayerEventManager] 물리 기반 이동: {usePhysicsBasedMovement}");
                if (playerRb != null)
                {
                    Debug.Log($"[PlayerEventManager] Rigidbody2D 상태: isKinematic={playerRb.isKinematic}, gravityScale={playerRb.gravityScale}");
                }
            }

            // PlayerManager와 연동하여 걷기 애니메이션 시작
            if (PlayerManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[PlayerEventManager] PlayerManager 찾음, 걷기 애니메이션 시작");

                // 기존 구독 해제 후 재구독 (중복 방지)
                PlayerManager.OnPlayerAnimationChanged -= OnPlayerAnimationChanged;
                PlayerManager.OnPlayerAnimationChanged += OnPlayerAnimationChanged;

                // 걷기 애니메이션 시작
                PlayerManager.Instance.PlayWalkAnimation();

                // 한 프레임 대기 후 스프라이트 즉시 업데이트
                yield return null;

                if (debugMode)
                    Debug.Log("[PlayerEventManager] 수동으로 UpdatePlayerSprite 호출");

                UpdatePlayerSprite();

                if (debugMode)
                    Debug.Log("[PlayerEventManager] Walk 애니메이션 시작 완료");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerEventManager] PlayerManager.Instance가 null입니다!");
            }

            // 이동 애니메이션 실행 (물리 기반 또는 일반)
            if (usePhysicsBasedMovement && playerRb != null)
            {
                yield return StartCoroutine(PlayPhysicsBasedEntranceAnimation(targetWorldPosition, playerRb));
            }
            else
            {
                yield return StartCoroutine(SmoothMoveToPosition(startPosition, targetWorldPosition, entranceAnimationDuration));
            }

            // 애니메이션 완료 후 Idle 상태로 변경
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ReturnToIdle();
            }

            // 입장 애니메이션 플래그 해제 및 완료 표시
            isPlayingEntranceAnimation = false;
            hasPlayedEntranceAnimation = true;

            if (debugMode)
                Debug.Log("[PlayerEventManager] 플레이어 입장 애니메이션 완료");
        }

        /// <summary>
        /// 물리 기반 입장 애니메이션 (PlayerBattleManager와 동일한 로직)
        /// </summary>
        private System.Collections.IEnumerator PlayPhysicsBasedEntranceAnimation(Vector3 targetWorldPosition, Rigidbody2D playerRb)
        {
            if (debugMode)
                Debug.Log($"[PlayerEventManager] 물리 기반 입장 애니메이션 시작 → 목표: {targetWorldPosition}");

            // 현재 위치에서 목표까지의 거리 계산
            Vector3 currentPosition = playerGameObject.transform.position;
            float distanceToTarget = Mathf.Abs(targetWorldPosition.x - currentPosition.x);

            // 이동 속도 계산 (거리를 시간으로 나눔)
            float moveSpeed = distanceToTarget / entranceAnimationDuration;

            if (debugMode)
            {
                Debug.Log($"[PlayerEventManager] 현재 위치: {currentPosition}");
                Debug.Log($"[PlayerEventManager] 목표 위치: {targetWorldPosition}");
                Debug.Log($"[PlayerEventManager] 이동 거리: {distanceToTarget:F2}, 속도: {moveSpeed:F2}");
                Debug.Log($"[PlayerEventManager] Rigidbody2D 상태: isKinematic={playerRb.isKinematic}, gravityScale={playerRb.gravityScale}");
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
                    Debug.Log($"[PlayerEventManager] 물리 이동 진행률: {progress:F2}, 위치: {physicsPosition}, 속도: {playerRb.velocity}");
                }

                yield return new WaitForFixedUpdate(); // 물리 업데이트와 동기화
            }

            // 목표 위치에 정확히 도달하도록 마지막 조정
            Vector3 finalPosition = playerGameObject.transform.position;
            finalPosition.x = targetWorldPosition.x;
            playerGameObject.transform.position = finalPosition;

            // 이동 완료 후 속도를 0으로 설정
            playerRb.velocity = new Vector2(0f, playerRb.velocity.y);

            if (debugMode)
            {
                Debug.Log($"[PlayerEventManager] 물리 기반 입장 애니메이션 완료");
                Debug.Log($"[PlayerEventManager] 최종 위치: {playerGameObject.transform.position}");
                Debug.Log($"[PlayerEventManager] 최종 속도: {playerRb.velocity}");
            }
        }

        /// <summary>
        /// 지정된 위치로 부드럽게 이동하는 애니메이션 (일반 모드)
        /// </summary>
        private System.Collections.IEnumerator SmoothMoveToPosition(Vector3 startPosition, Vector3 targetPosition, float duration)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // Ease Out 효과 적용 (부드럽게 감속)
                float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);

                // 현재 위치 계산
                Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, easedProgress);
                playerGameObject.transform.position = currentPosition;

                if (debugMode && elapsedTime % 0.5f < Time.deltaTime) // 0.5초마다 로그
                {
                    Debug.Log($"[PlayerEventManager] 이동 진행률: {progress:F2}, 위치: {currentPosition}");
                }

                yield return null;
            }

            // 목표 위치에 정확히 도달하도록 마지막 조정
            playerGameObject.transform.position = targetPosition;

            if (debugMode)
            {
                Debug.Log($"[PlayerEventManager] 이동 애니메이션 완료");
                Debug.Log($"[PlayerEventManager] 최종 위치: {playerGameObject.transform.position}");
            }
        }

        /// <summary>
        /// PlayerManager 애니메이션 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnPlayerAnimationChanged(PlayerManager.PlayerAnimationState newState)
        {
            // if (debugMode)
            //     Debug.Log($"[PlayerEventManager] 플레이어 애니메이션 상태 변경: {newState}");

            // 스프라이트 업데이트
            UpdatePlayerSprite(newState);
        }

        /// <summary>
        /// 플레이어 스프라이트 업데이트 (PlayerBattleManager와 동일한 로직)
        /// </summary>
        public void UpdatePlayerSprite(PlayerManager.PlayerAnimationState animationState)
        {
            if (playerGameObject == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerEventManager] 플레이어 게임오브젝트가 없어 스프라이트를 업데이트할 수 없습니다.");
                return;
            }

            // if (debugMode)
            //     Debug.Log($"[PlayerEventManager] 스프라이트 업데이트 요청 - 애니메이션 상태: {animationState}");

            // PlayerManager에서 현재 상태의 스프라이트 가져오기
            Sprite newSprite = null;
            if (PlayerManager.Instance != null)
            {
                newSprite = PlayerManager.Instance.GetPlayerSprite();

                // if (debugMode)
                // {
                //     var currentState = PlayerManager.Instance.CurrentAnimationState;
                //     Debug.Log($"[PlayerEventManager] PlayerManager 현재 상태: {currentState}");
                //     Debug.Log($"[PlayerEventManager] 가져온 스프라이트: {(newSprite != null ? newSprite.name : "null")}");
                // }
            }

            // 스프라이트가 없으면 경고 출력
            if (newSprite == null)
            {
                if (debugMode)
                    Debug.LogWarning("[PlayerEventManager] PlayerManager에서 스프라이트를 가져올 수 없습니다. 플레이어 오브젝트의 SpriteRenderer에 기본 스프라이트를 설정하세요.");
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

                // if (debugMode)
                // {
                //     var currentState = PlayerManager.Instance?.CurrentAnimationState ?? PlayerManager.PlayerAnimationState.Idle;
                //     Debug.Log($"[PlayerEventManager] 플레이어 스프라이트 업데이트 완료: {currentState} -> {newSprite.name}");
                //     Debug.Log($"[PlayerEventManager] SpriteRenderer에 실제 적용된 스프라이트: {spriteRenderer.sprite.name}");
                // }
            }
            else
            {
                Debug.LogError("[PlayerEventManager] 플레이어 게임오브젝트에 SpriteRenderer 컴포넌트가 없습니다!");
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

        #region 플레이어 제어
        /// <summary>
        /// 플레이어 게임오브젝트 반환
        /// </summary>
        public GameObject GetPlayerGameObject()
        {
            return playerGameObject;
        }

        /// <summary>
        /// 플레이어 그리드 위치 설정 (목표 위치만 설정, 실제 위치는 변경하지 않음)
        /// </summary>
        public void SetPlayerGridPosition(Vector2Int gridPosition)
        {
            playerGridPosition = gridPosition;
            
            if (debugMode)
            {
                if (GridFieldManager.Instance != null && GridFieldManager.Instance.IsInitialized && playerGameObject != null)
                {
                    Vector3 calculatedWorldPosition = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, gridPosition);
                    Vector3 currentPosition = playerGameObject.transform.position;
                    Debug.Log($"[PlayerEventManager] 목표 그리드 위치 설정: {gridPosition}");
                    Debug.Log($"[PlayerEventManager] 계산된 목표 월드 위치: {calculatedWorldPosition}");
                    Debug.Log($"[PlayerEventManager] 현재 플레이어 위치: {currentPosition} (변경하지 않음)");
                }
                else
                {
                    Debug.Log($"[PlayerEventManager] 목표 그리드 위치 설정: {gridPosition} (GridFieldManager 없음)");
                }
            }
        }

        /// <summary>
        /// 플레이어 위치 설정 (월드 좌표)
        /// </summary>
        public void SetPlayerPosition(Vector3 position)
        {
            if (playerGameObject != null)
            {
                playerGameObject.transform.position = position;
                
                if (debugMode)
                    Debug.Log($"[PlayerEventManager] 플레이어 월드 위치 설정: {position}");
            }
        }

        /// <summary>
        /// 플레이어 그리드 위치 반환
        /// </summary>
        public Vector2Int GetPlayerGridPosition()
        {
            return playerGridPosition;
        }

        /// <summary>
        /// 플레이어 월드 위치 반환
        /// </summary>
        public Vector3 GetPlayerWorldPosition()
        {
            if (playerGameObject != null)
            {
                return playerGameObject.transform.position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 입장 애니메이션 완료 여부 확인
        /// </summary>
        public bool HasPlayedEntranceAnimation()
        {
            return hasPlayedEntranceAnimation;
        }

        /// <summary>
        /// 입장 애니메이션 상태 리셋 (다음 씬 전환용)
        /// </summary>
        public void ResetEntranceAnimationState()
        {
            hasPlayedEntranceAnimation = false;
            isPlayingEntranceAnimation = false;
            
            if (debugMode)
                Debug.Log("[PlayerEventManager] 입장 애니메이션 상태 리셋");
        }
        #endregion

        #region 정리
        /// <summary>
        /// 씬 전환 시 정리
        /// </summary>
        public void CleanupOnSceneTransition()
        {
            if (debugMode)
                Debug.Log("[PlayerEventManager] 씬 전환 시 정리 수행");

            // 이벤트 구독 해제
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerAnimationChanged -= OnPlayerAnimationChanged;
            }

            // 상태 리셋
            ResetEntranceAnimationState();
            isInitialized = false;
            playerGameObject = null;
        }

        private void OnDestroy()
        {
            // PlayerManager 이벤트 구독 해제
            if (PlayerManager.Instance != null)
            {
                PlayerManager.OnPlayerAnimationChanged -= OnPlayerAnimationChanged;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region 디버그
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Player Event Info")]
        public void DebugPlayerEventInfo()
        {
            Debug.Log("=== PlayerEventManager Debug Info ===");
            Debug.Log($"초기화됨: {isInitialized}");
            Debug.Log($"플레이어 오브젝트: {(playerGameObject != null ? playerGameObject.name : "null")}");
            Debug.Log($"입장 애니메이션 완료: {hasPlayedEntranceAnimation}");
            Debug.Log($"입장 애니메이션 실행 중: {isPlayingEntranceAnimation}");
            Debug.Log($"플레이어 그리드 위치: {playerGridPosition}");
            Debug.Log($"시작 오프셋: {entranceStartOffsetX}");
            Debug.Log($"애니메이션 시간: {entranceAnimationDuration}초");
            Debug.Log($"물리 기반 이동: {usePhysicsBasedMovement}");
            
            if (playerGameObject != null)
            {
                Debug.Log($"현재 월드 위치: {playerGameObject.transform.position}");
            }
            
            if (GridFieldManager.Instance != null)
            {
                Debug.Log($"GridFieldManager 상태: 초기화됨={GridFieldManager.Instance.IsInitialized}");
                if (GridFieldManager.Instance.IsInitialized && playerGameObject != null)
                {
                    Vector3 calculatedWorldPos = GridFieldManager.Instance.GridToWorldPositionWithSpriteAlignment(playerGameObject, playerGridPosition);
                    Debug.Log($"계산된 목표 월드 위치: {calculatedWorldPos}");
                }
            }
            else
            {
                Debug.Log("GridFieldManager: null");
            }
            
            Debug.Log("=====================================");
        }
        #endregion
    }
}
