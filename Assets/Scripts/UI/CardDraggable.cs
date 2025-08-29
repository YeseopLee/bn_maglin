using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace Maglin.UI
{
    /// <summary>
    /// 카드 UI의 드래그 기능을 제공하는 컴포넌트
    /// </summary>
    public class CardDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag Settings")]
        [SerializeField] private float dragAlpha = 0.7f;
        [SerializeField] private bool returnToOriginalParent = true;
        [SerializeField] private float returnSpeed = 5f;

        private Transform originalParent;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private int originalSiblingIndex;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private GraphicRaycaster graphicRaycaster;

        private bool isDragging = false;
        private bool isReturning = false;
        private bool isDragEnabled = true; // 드래그 활성화/비활성화 제어

        private void Awake()
        {
            // CanvasGroup 컴포넌트 확인/추가
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Canvas와 GraphicRaycaster 찾기
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            }
        }

        private void Start()
        {
            // 초기 위치 정보 저장
            SaveOriginalTransform();
        }

        /// <summary>
        /// 원래 Transform 정보 저장
        /// </summary>
        private void SaveOriginalTransform()
        {
            originalParent = transform.parent;
            originalPosition = transform.localPosition;
            originalScale = transform.localScale;
            originalSiblingIndex = transform.GetSiblingIndex();
        }

        /// <summary>
        /// 드래그 시작
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isReturning || !isDragEnabled) return;

            isDragging = true;

            // 현재 상태 저장
            SaveOriginalTransform();

            // 드래그 중 시각적 효과
            canvasGroup.alpha = dragAlpha;
            canvasGroup.blocksRaycasts = false;

            // 드래그 중에는 최상위로 이동
            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();

            Debug.Log($"[CardDraggable] 드래그 시작: {gameObject.name}");
        }

        /// <summary>
        /// 드래그 중
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            // 마우스 위치를 월드 좌표로 변환
            Vector3 mouseWorldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out mouseWorldPos))
            {
                transform.position = mouseWorldPos;
            }
        }

        /// <summary>
        /// 드래그 종료
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;

            // 시각적 효과 복원
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // 드롭된 위치에서 유효한 드롭 존이 있는지 확인
            bool droppedSuccessfully = false;

            if (eventData.pointerEnter != null)
            {
                var dropZone = eventData.pointerEnter.GetComponent<CardDropZone>();
                if (dropZone != null)
                {
                    // 드롭 존이 처리할 예정이므로 여기서는 성공으로 간주
                    droppedSuccessfully = true;
                    Debug.Log($"[CardDraggable] 드롭 존에 드롭: {eventData.pointerEnter.name}");
                }
            }

            // 드롭에 실패한 경우 원래 위치로 복귀
            if (!droppedSuccessfully)
            {
                ReturnToOriginalPosition();
            }

            Debug.Log($"[CardDraggable] 드래그 종료: {gameObject.name}, 성공: {droppedSuccessfully}");
        }

        /// <summary>
        /// 원래 위치로 복귀
        /// </summary>
        public void ReturnToOriginalPosition()
        {
            if (isReturning) return;

            StartCoroutine(ReturnToOriginalPositionCoroutine());
        }

        /// <summary>
        /// 원래 위치로 부드럽게 복귀하는 코루틴
        /// </summary>
        private IEnumerator ReturnToOriginalPositionCoroutine()
        {
            isReturning = true;

            // 드래그 상태 해제
            isDragging = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // 부모 복원
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);

            // 부드러운 이동
            Vector3 startPosition = transform.localPosition;
            Vector3 startScale = transform.localScale;
            float elapsedTime = 0f;
            float duration = 1f / returnSpeed;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // Ease Out 효과
                progress = 1f - (1f - progress) * (1f - progress);

                transform.localPosition = Vector3.Lerp(startPosition, originalPosition, progress);
                transform.localScale = Vector3.Lerp(startScale, originalScale, progress);

                yield return null;
            }

            // 최종 위치 보장
            transform.localPosition = originalPosition;
            transform.localScale = originalScale;

            isReturning = false;

            Debug.Log($"[CardDraggable] 원래 위치 복귀 완료: {gameObject.name}");
        }

        /// <summary>
        /// 새로운 부모로 설정하고 원래 정보 업데이트
        /// </summary>
        public void SetNewParent(Transform newParent, bool updateOriginal = true)
        {
            transform.SetParent(newParent, false);

            if (updateOriginal)
            {
                SaveOriginalTransform();
            }
        }

        /// <summary>
        /// 드래그 가능 여부 설정
        /// </summary>
        public void SetDraggable(bool draggable)
        {
            this.enabled = draggable;
        }

        /// <summary>
        /// 현재 드래그 중인지 확인
        /// </summary>
        public bool IsDragging => isDragging;

        /// <summary>
        /// 현재 복귀 중인지 확인
        /// </summary>
        public bool IsReturning => isReturning;

        /// <summary>
        /// 드래그 기능 활성화/비활성화
        /// </summary>
        public void SetDragEnabled(bool enabled)
        {
            isDragEnabled = enabled;

            // 비활성화될 때 현재 드래그 중이면 강제로 원래 위치로 복귀
            if (!enabled && isDragging)
            {
                ReturnToOriginalPosition();
            }

            // 시각적 피드백 (선택적)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = enabled ? 1f : 0.7f;
            }
        }

        /// <summary>
        /// 현재 드래그 가능 상태 반환
        /// </summary>
        public bool IsDragEnabled()
        {
            return isDragEnabled;
        }
    }
}