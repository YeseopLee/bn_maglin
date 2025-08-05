using UnityEngine;
using UnityEngine.EventSystems;
using System;
using Maglin.Cards;
using Maglin.Battle;

namespace Maglin.UI
{
    /// <summary>
    /// 카드를 드롭할 수 있는 영역을 정의하는 컴포넌트
    /// </summary>
    public class CardDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Drop Zone Settings")]
        [SerializeField] private CardType acceptedCardType = CardType.Element;
        [SerializeField] private Color highlightColor = Color.green;
        [SerializeField] private Color normalColor = Color.gray;

        private UnityEngine.UI.Image backgroundImage;
        private Action<GameObject, CardType> onCardDropped;

        private void Awake()
        {
            // 배경 이미지 컴포넌트 확인/추가
            backgroundImage = GetComponent<UnityEngine.UI.Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<UnityEngine.UI.Image>();
            }

            // 기본 색상 설정
            backgroundImage.color = normalColor;
        }

        /// <summary>
        /// 드롭 존 초기화
        /// </summary>
        /// <param name="cardType">허용되는 카드 타입</param>
        /// <param name="dropCallback">드롭 콜백</param>
        public void Initialize(CardType cardType, Action<GameObject, CardType> dropCallback)
        {
            acceptedCardType = cardType;
            onCardDropped = dropCallback;
        }

        /// <summary>
        /// 드롭 이벤트 처리
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            var draggedObject = eventData.pointerDrag;
            if (draggedObject == null) return;

            var cardDraggable = draggedObject.GetComponent<CardDraggable>();
            if (cardDraggable == null) return;

            var cardData = draggedObject.GetComponent<CardUIData>();
            if (cardData == null || cardData.CardInstance == null) return;

            // 카드 타입 확인
            if (cardData.CardInstance.Type == acceptedCardType)
            {
                // 드롭 성공
                onCardDropped?.Invoke(draggedObject, acceptedCardType);

                // 시각적 피드백 제거
                backgroundImage.color = normalColor;

                Debug.Log($"[CardDropZone] 카드 드롭 성공: {cardData.CardInstance.CardName} -> {acceptedCardType} 슬롯");
            }
            else
            {
                // 타입 불일치로 드롭 실패
                Debug.Log($"[CardDropZone] 카드 타입 불일치: {cardData.CardInstance.Type} != {acceptedCardType}");

                // 카드를 원래 위치로 되돌리기
                cardDraggable.ReturnToOriginalPosition();
            }
        }

        /// <summary>
        /// 포인터가 드롭 존에 들어올 때
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            var draggedObject = eventData.pointerDrag;
            if (draggedObject == null) return;

            var cardData = draggedObject.GetComponent<CardUIData>();
            if (cardData == null || cardData.CardInstance == null) return;

            // 올바른 카드 타입인 경우에만 하이라이트
            if (cardData.CardInstance.Type == acceptedCardType)
            {
                backgroundImage.color = highlightColor;
            }
        }

        /// <summary>
        /// 포인터가 드롭 존에서 나갈 때
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            backgroundImage.color = normalColor;
        }

        /// <summary>
        /// 허용되는 카드 타입 설정
        /// </summary>
        public void SetAcceptedCardType(CardType cardType)
        {
            acceptedCardType = cardType;
        }

        /// <summary>
        /// 드롭 존 색상 설정
        /// </summary>
        public void SetColors(Color normal, Color highlight)
        {
            normalColor = normal;
            highlightColor = highlight;
            if (backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }
        }
    }
}