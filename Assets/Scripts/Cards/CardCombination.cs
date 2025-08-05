using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maglin.Cards
{
    /// <summary>
    /// 카드 조합 키 구조체 - Dictionary 키로 사용하여 O(1) 조합 검색 지원
    /// </summary>
    [Serializable]
    public struct CardCombination : IEquatable<CardCombination>
    {
        #region Fields
        [SerializeField] private ElementType element;
        [SerializeField] private CardSO active1;
        [SerializeField] private CardSO active2;
        [SerializeField] private ElementType field;
        #endregion

        #region Properties
        /// <summary>
        /// 조합에 사용되는 Element 카드의 속성
        /// </summary>
        public ElementType Element => element;

        /// <summary>
        /// 조합에 사용되는 첫 번째 Active 카드
        /// </summary>
        public CardSO Active1 => active1;

        /// <summary>
        /// 조합에 사용되는 두 번째 Active 카드
        /// </summary>
        public CardSO Active2 => active2;

        /// <summary>
        /// 현재 필드 효과
        /// </summary>
        public ElementType Field => field;

        /// <summary>
        /// 유효한 조합인지 확인
        /// </summary>
        public bool IsValid =>
            (element != ElementType.None && active1 != null && active2 == null) ||  // 속성 + 액티브1
            (element != ElementType.None && active1 == null && active2 != null) ||  // 속성 + 액티브2
            (element == ElementType.None && active1 != null && active2 != null) ||  // 액티브1 + 액티브2
            (element != ElementType.None && active1 != null && active2 != null);    // 속성 + 액티브1 + 액티브2
        #endregion

        #region Constructors
        /// <summary>
        /// 모든 요소를 지정하는 생성자
        /// </summary>
        public CardCombination(ElementType element, CardSO active1, CardSO active2, ElementType field = ElementType.None)
        {
            this.element = element;
            this.active1 = active1;
            this.active2 = active2;
            this.field = field;
        }

        /// <summary>
        /// 카드 인스턴스들로부터 조합 키 생성
        /// </summary>
        public CardCombination(Card elementCard, Card active1Card, Card active2Card, ElementType currentField = ElementType.None)
        {
            // 입력 검증
            if (elementCard == null || !elementCard.IsValid() || elementCard.Type != CardType.Element)
            {
                throw new ArgumentException("Element 카드가 유효하지 않습니다.");
            }

            if (active1Card == null || !active1Card.IsValid() ||
                (active1Card.Type != CardType.Active1 && active1Card.Type != CardType.Active2))
            {
                throw new ArgumentException("첫 번째 Active 카드가 유효하지 않습니다.");
            }

            if (active2Card == null || !active2Card.IsValid() ||
                (active2Card.Type != CardType.Active1 && active2Card.Type != CardType.Active2))
            {
                throw new ArgumentException("두 번째 Active 카드가 유효하지 않습니다.");
            }

            this.element = elementCard.Element;
            this.active1 = active1Card.CardData;
            this.active2 = active2Card.CardData;
            this.field = currentField;
        }

        /// <summary>
        /// 정규화된 조합 키 생성 (카드 순서를 표준화)
        /// </summary>
        public static CardCombination CreateNormalized(ElementType element, CardSO active1, CardSO active2, ElementType field = ElementType.None)
        {
            // Active 카드들을 이름 순으로 정렬하여 순서 무관하게 동일한 키 생성
            // null인 카드는 뒤로 보내기
            if (active1 != null && active2 != null)
            {
                // 두 카드 모두 있는 경우: 이름으로 정렬
                int comparison = string.Compare(active1.CardName, active2.CardName, System.StringComparison.Ordinal);
                if (comparison > 0)
                {
                    // active1이 사전순으로 뒤에 있으면 순서 바꾸기
                    var temp = active1;
                    active1 = active2;
                    active2 = temp;
                }
            }
            else if (active1 == null && active2 != null)
            {
                // active1이 null이고 active2가 있으면 순서 바꾸기
                active1 = active2;
                active2 = null;
            }
            // active2가 null이고 active1이 있으면 그대로 유지

            return new CardCombination(element, active1, active2, field);
        }

        /// <summary>
        /// 카드 배열로부터 정규화된 조합 키 생성
        /// </summary>
        public static CardCombination CreateFromCards(Card[] cards, ElementType currentField = ElementType.None)
        {
            if (cards == null || cards.Length == 0)
            {
                throw new ArgumentException("최소 1장의 카드가 필요합니다.");
            }

            Card elementCard = null;
            Card active1Card = null;
            Card active2Card = null;

            // 카드 타입별로 분류
            foreach (var card in cards)
            {
                if (card == null || !card.IsValid()) continue;

                switch (card.Type)
                {
                    case CardType.Element:
                        if (elementCard == null)
                            elementCard = card;
                        else
                            throw new ArgumentException("Element 카드는 하나만 있어야 합니다.");
                        break;

                    case CardType.Active1:
                        if (active1Card == null)
                            active1Card = card;
                        else
                            throw new ArgumentException("Active1 카드는 하나만 있어야 합니다.");
                        break;

                    case CardType.Active2:
                        if (active2Card == null)
                            active2Card = card;
                        else
                            throw new ArgumentException("Active2 카드는 하나만 있어야 합니다.");
                        break;

                    default:
                        throw new ArgumentException($"조합에 사용할 수 없는 카드 타입입니다: {card.Type}");
                }
            }

            // 허용되는 조합 패턴 검증
            bool isValidCombination = false;

            // ① 속성 + 액티브1
            if (elementCard != null && active1Card != null && active2Card == null)
                isValidCombination = true;

            // ② 속성 + 액티브2  
            else if (elementCard != null && active1Card == null && active2Card != null)
                isValidCombination = true;

            // ③ 액티브1 + 액티브2
            else if (elementCard == null && active1Card != null && active2Card != null)
                isValidCombination = true;

            // ④ 속성 + 액티브1 + 액티브2
            else if (elementCard != null && active1Card != null && active2Card != null)
                isValidCombination = true;

            if (!isValidCombination)
            {
                throw new ArgumentException("허용되지 않는 조합입니다. 가능한 조합: 속성+액티브1, 속성+액티브2, 액티브1+액티브2, 속성+액티브1+액티브2");
            }

            // 조합 키 생성
            ElementType elementType = elementCard?.Element ?? ElementType.None;
            CardSO active1Data = active1Card?.CardData;
            CardSO active2Data = active2Card?.CardData;

            return CreateNormalized(elementType, active1Data, active2Data, currentField);
        }
        #endregion

        #region Validation
        /// <summary>
        /// 조합 키 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";

            // 허용되는 조합 패턴 검증
            bool isValidPattern = false;

            // ① 속성 + 액티브1
            if (element != ElementType.None && active1 != null && active2 == null)
                isValidPattern = true;

            // ② 속성 + 액티브2
            else if (element != ElementType.None && active1 == null && active2 != null)
                isValidPattern = true;

            // ③ 액티브1 + 액티브2
            else if (element == ElementType.None && active1 != null && active2 != null)
                isValidPattern = true;

            // ④ 속성 + 액티브1 + 액티브2
            else if (element != ElementType.None && active1 != null && active2 != null)
                isValidPattern = true;

            if (!isValidPattern)
            {
                errorMessage = "허용되지 않는 조합입니다. 가능한 조합: 속성+액티브1, 속성+액티브2, 액티브1+액티브2, 속성+액티브1+액티브2";
                return false;
            }

            // 동일한 Active 카드 사용 금지 (둘 다 null이 아닌 경우만)
            if (active1 != null && active2 != null && active1 == active2)
            {
                errorMessage = "동일한 Active 카드는 조합할 수 없습니다.";
                return false;
            }

            // Active 카드 타입 검증 (null이 아닌 경우만)
            if (active1 != null && active1.Type != CardType.Active1 && active1.Type != CardType.Active2)
            {
                errorMessage = $"첫 번째 카드가 Active 타입이 아닙니다: {active1.Type}";
                return false;
            }

            if (active2 != null && active2.Type != CardType.Active1 && active2.Type != CardType.Active2)
            {
                errorMessage = $"두 번째 카드가 Active 타입이 아닙니다: {active2.Type}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 특정 조합 요구사항과 일치하는지 확인
        /// </summary>
        public bool MatchesRequirement(CardCombinationData requirement)
        {
            if (requirement == null) return false;

            // Element 카드 확인
            if (requirement.elementCard != null &&
                requirement.elementCard.Element != element)
            {
                return false;
            }

            // Active1 카드 확인
            if (requirement.active1Card != null &&
                requirement.active1Card != active1 &&
                requirement.active1Card != active2)
            {
                return false;
            }

            // Active2 카드 확인
            if (requirement.active2Card != null &&
                requirement.active2Card != active1 &&
                requirement.active2Card != active2)
            {
                return false;
            }

            // Active1과 Active2가 모두 지정된 경우, 둘 다 포함되어야 함
            if (requirement.active1Card != null && requirement.active2Card != null)
            {
                bool hasActive1 = (active1 == requirement.active1Card || active2 == requirement.active1Card);
                bool hasActive2 = (active1 == requirement.active2Card || active2 == requirement.active2Card);

                if (!hasActive1 || !hasActive2)
                {
                    return false;
                }
            }

            // 필드 효과 확인 (선택사항)
            if (requirement.requiredField != null && field != ElementType.None)
            {
                // requiredField가 지정된 경우, 현재 필드와 일치해야 함
                // 실제 구현에서는 FieldEffectSO의 속성을 확인해야 할 수 있음
                // 임시로 기본 로직만 구현
            }

            return true;
        }
        #endregion

        #region IEquatable Implementation
        /// <summary>
        /// 다른 CardCombination과 같은지 비교
        /// </summary>
        public bool Equals(CardCombination other)
        {
            return element == other.element &&
                   CardEquals(active1, other.active1) &&
                   CardEquals(active2, other.active2) &&
                   field == other.field;
        }

        /// <summary>
        /// 두 CardSO가 같은 카드인지 비교 (null 안전)
        /// </summary>
        private bool CardEquals(CardSO card1, CardSO card2)
        {
            if (card1 == null && card2 == null) return true;
            if (card1 == null || card2 == null) return false;

            // 카드 이름으로 비교 (CardSO는 이름이 유니크해야 함)
            return card1.CardName == card2.CardName;
        }

        /// <summary>
        /// Object와 같은지 비교
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is CardCombination other && Equals(other);
        }

        /// <summary>
        /// 해시코드 생성 - Dictionary 키로 사용하기 위해 중요
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)element;
                hash = hash * 23 + (active1 != null ? active1.CardName.GetHashCode() : 0);
                hash = hash * 23 + (active2 != null ? active2.CardName.GetHashCode() : 0);
                hash = hash * 23 + (int)field;
                return hash;
            }
        }

        /// <summary>
        /// == 연산자 오버로드
        /// </summary>
        public static bool operator ==(CardCombination left, CardCombination right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// != 연산자 오버로드
        /// </summary>
        public static bool operator !=(CardCombination left, CardCombination right)
        {
            return !left.Equals(right);
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 조합에 특정 카드가 포함되어 있는지 확인
        /// </summary>
        public bool ContainsCard(CardSO card)
        {
            return card != null && (ReferenceEquals(active1, card) || ReferenceEquals(active2, card));
        }

        /// <summary>
        /// 조합에 특정 속성의 Element가 포함되어 있는지 확인
        /// </summary>
        public bool ContainsElement(ElementType elementType)
        {
            return element == elementType;
        }

        /// <summary>
        /// 필드 효과를 고려한 조합인지 확인
        /// </summary>
        public bool UsesField()
        {
            return field != ElementType.None;
        }

        /// <summary>
        /// 조합 정보를 문자열로 변환
        /// </summary>
        public override string ToString()
        {
            string active1Name = active1 != null ? active1.CardName : "null";
            string active2Name = active2 != null ? active2.CardName : "null";
            string fieldStr = field != ElementType.None ? $" + Field({field})" : "";

            return $"Combination({element} + {active1Name} + {active2Name}{fieldStr})";
        }

        /// <summary>
        /// 디버그용 상세 정보 반환
        /// </summary>
        public string GetDebugInfo()
        {
            return $"CardCombination[" +
                   $"Element: {element}, " +
                   $"Active1: {(active1 != null ? active1.CardName : "null")}, " +
                   $"Active2: {(active2 != null ? active2.CardName : "null")}, " +
                   $"Field: {field}, " +
                   $"Valid: {IsValid}, " +
                   $"Hash: {GetHashCode():X8}" +
                   $"]";
        }
        #endregion

        #region Static Utilities
        /// <summary>
        /// 빈 조합 키 (무효한 조합)
        /// </summary>
        public static readonly CardCombination Empty = new CardCombination(ElementType.None, null, null, ElementType.None);

        /// <summary>
        /// 두 조합 키가 충돌하는지 확인 (디버깅용)
        /// </summary>
        public static bool HasHashCollision(CardCombination combo1, CardCombination combo2)
        {
            return combo1.GetHashCode() == combo2.GetHashCode() && !combo1.Equals(combo2);
        }
        #endregion
    }
}