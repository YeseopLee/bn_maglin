using UnityEngine;

namespace Maglin.Core
{
    /// <summary>
    /// 로딩 씬의 간단한 컨트롤러
    /// UI나 복잡한 기능 없이 최소한의 관리만 수행
    /// </summary>
    public class LoadingSceneController : MonoBehaviour
    {
        #region Fields
        [Header("설정")]
        [SerializeField] private bool debugMode = true;

        [Header("선택적 효과")]
        [SerializeField] private bool enableSimpleAnimation = false;
        [SerializeField] private Transform rotatingObject; // 간단한 회전 오브젝트 (선택사항)
        [SerializeField] private float rotationSpeed = 90f; // 초당 회전 각도
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (debugMode)
                Debug.Log("[LoadingSceneController] 로딩 씬 초기화");

            // 로딩 씬은 아무것도 하지 않음 - 단순히 존재만 함
            Initialize();
        }

        private void Start()
        {
            if (debugMode)
                Debug.Log("[LoadingSceneController] 로딩 씬 시작");

            // AdditiveSceneLoader에게 로딩 씬이 준비되었음을 알림
            NotifyLoadingSceneReady();
        }

        private void Update()
        {
            // 선택적 간단한 애니메이션
            if (enableSimpleAnimation && rotatingObject != null)
            {
                rotatingObject.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[LoadingSceneController] 로딩 씬 종료");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 로딩 씬 초기화
        /// </summary>
        private void Initialize()
        {
            // 로딩 씬에서는 특별한 초기화가 필요 없음
            // 단순히 빠르게 로드되고 언로드되는 것이 목적

            if (debugMode)
                Debug.Log("[LoadingSceneController] 로딩 씬 초기화 완료");
        }

        /// <summary>
        /// AdditiveSceneLoader에게 로딩 씬 준비 완료 알림
        /// </summary>
        private void NotifyLoadingSceneReady()
        {
            // 로딩 씬은 즉시 준비 완료
            // AdditiveSceneLoader가 이벤트로 관리하므로 별도 호출 불필요

            if (debugMode)
                Debug.Log("[LoadingSceneController] 로딩 씬 준비 완료");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 로딩 씬 상태 확인
        /// </summary>
        public bool IsReady()
        {
            return true; // 로딩 씬은 항상 즉시 준비됨
        }

        /// <summary>
        /// 간단한 회전 애니메이션 활성화/비활성화
        /// </summary>
        public void SetAnimationEnabled(bool enabled)
        {
            enableSimpleAnimation = enabled;

            if (debugMode)
                Debug.Log($"[LoadingSceneController] 애니메이션 {(enabled ? "활성화" : "비활성화")}");
        }

        /// <summary>
        /// 회전 오브젝트 설정
        /// </summary>
        public void SetRotatingObject(Transform obj)
        {
            rotatingObject = obj;

            if (debugMode)
                Debug.Log($"[LoadingSceneController] 회전 오브젝트 설정: {obj?.name ?? "null"}");
        }
        #endregion

        #region Debug Methods
        /// <summary>
        /// 로딩 씬 정보 출력
        /// </summary>
        [ContextMenu("Debug Loading Scene Info")]
        public void DebugInfo()
        {
            Debug.Log("=== LoadingSceneController Debug Info ===");
            Debug.Log($"씬 이름: {gameObject.scene.name}");
            Debug.Log($"준비 상태: {IsReady()}");
            Debug.Log($"애니메이션 활성화: {enableSimpleAnimation}");
            Debug.Log($"회전 오브젝트: {rotatingObject?.name ?? "없음"}");
            Debug.Log($"회전 속도: {rotationSpeed}도/초");
        }
        #endregion
    }
}
