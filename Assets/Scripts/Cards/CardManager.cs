using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Maglin.Core;
using Maglin.Player;
using Maglin.Battle;

namespace Maglin.Cards
{
    /// <summary>
    /// 카드 덱과 손패를 관리하는 매니저
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static CardManager _instance;

        public static CardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CardManager>();

                    if (_instance == null)
                    {
                        GameObject cardManagerObject = new GameObject("CardManager");
                        _instance = cardManagerObject.AddComponent<CardManager>();
                        DontDestroyOnLoad(cardManagerObject);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events
        /// <summary>
        /// 카드 드로우 이벤트 (드로우한 카드)
        /// </summary>
        public static event Action<CardSO> OnCardDrawn;

        /// <summary>
        /// 카드 사용 이벤트 (사용한 카드들)
        /// </summary>
        public static event Action<CardSO[]> OnCardsUsed;

        /// <summary>
        /// 손패 변경 이벤트 (현재 손패)
        /// </summary>
        public static event Action<List<CardSO>> OnHandChanged;

        /// <summary>
        /// 덱 셔플 이벤트
        /// </summary>
        public static event Action OnDeckShuffled;

        /// <summary>
        /// 추가 드로우 비용 변경 이벤트 (현재 비용)
        /// </summary>
        public static event Action<int> OnDrawCostChanged;

        /// <summary>
        /// Card 인스턴스 손패 변경 이벤트 (태스크 71번)
        /// </summary>
        public static event Action<List<Card>> OnHandCardsChanged;

        /// <summary>
        /// Card 인스턴스 드로우 이벤트 (태스크 71번)
        /// </summary>
        public static event Action<Card> OnCardInstanceDrawn;

        /// <summary>
        /// Card 인스턴스 사용 이벤트 (태스크 71번)
        /// </summary>
        public static event Action<Card[]> OnCardInstancesUsed;
        #endregion

        #region Fields
        [Header("덱 구성")]
        [SerializeField] private List<CardSO> starterDeck = new List<CardSO>(); // 시작 덱
        [SerializeField] private List<CardSO> currentDeck = new List<CardSO>(); // 현재 덱

        [Header("게임 중 상태")]
        [SerializeField] private List<CardSO> hand = new List<CardSO>(); // 손패 (CardSO)
        [SerializeField] private List<CardSO> discardPile = new List<CardSO>(); // 임시무덤 (CardSO)
        [SerializeField] private List<CardSO> removedCards = new List<CardSO>(); // 제거된 카드 (CardSO)

        [Header("Card 인스턴스 관리 (태스크 71번)")]
        [SerializeField] private List<Card> handCards = new List<Card>(); // 손패 Card 인스턴스
        [SerializeField] private List<Card> mainDeck = new List<Card>(); // 메인 덱 Card 인스턴스  
        [SerializeField] private List<Card> tempGraveyard = new List<Card>(); // 임시무덤 Card 인스턴스

        [Header("드로우 설정")]
        [SerializeField] private int baseDrawCost = 10; // 기본 추가 드로우 비용
        [SerializeField] private int drawCostIncrease = 10; // 드로우마다 증가하는 비용
        [SerializeField] private int currentDrawCost = 10; // 현재 드로우 비용
        [SerializeField] private int additionalDrawsThisTurn = 0; // 이번 턴 추가 드로우 횟수

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 초기화 관련
        private bool isInitialized = false;
        private bool hasPerformedInitialDraw = false; // 초기 카드 드로우 완료 여부
        #endregion

        #region Properties
        /// <summary>
        /// 현재 손패
        /// </summary>
        public IReadOnlyList<CardSO> Hand => hand.AsReadOnly();

        /// <summary>
        /// 현재 덱 카드 수
        /// </summary>
        public int DeckCount => currentDeck.Count;

        /// <summary>
        /// 현재 덱 카드 목록 (읽기 전용)
        /// </summary>
        public IReadOnlyList<CardSO> CurrentDeck => currentDeck;

        /// <summary>
        /// 현재 덱 카드 목록 반환 (상점 등에서 사용)
        /// </summary>
        public List<CardSO> GetDeckCards()
        {
            return new List<CardSO>(currentDeck);
        }

        /// <summary>
        /// 임시무덤 카드 수
        /// </summary>
        public int DiscardPileCount => discardPile.Count;

        /// <summary>
        /// 손패 카드 수
        /// </summary>
        public int HandCount => hand.Count;

        /// <summary>
        /// 최대 손패 크기 (PlayerManager에서 가져옴)
        /// </summary>
        public int MaxHandSize => PlayerManager.Instance?.MaxHandSize ?? 5;

        /// <summary>
        /// 현재 추가 드로우 비용
        /// </summary>
        public int CurrentDrawCost => currentDrawCost;

        /// <summary>
        /// 이번 턴 추가 드로우 횟수
        /// </summary>
        public int AdditionalDrawsThisTurn => additionalDrawsThisTurn;

        /// <summary>
        /// 덱이 비어있는지 여부
        /// </summary>
        public bool IsDeckEmpty => currentDeck.Count == 0;

        /// <summary>
        /// 손패가 가득 찼는지 여부
        /// </summary>
        public bool IsHandFull => hand.Count >= MaxHandSize;

        #region Card Instance Properties (태스크 71번)
        /// <summary>
        /// 현재 손패 Card 인스턴스 (읽기 전용)
        /// </summary>
        public IReadOnlyList<Card> HandCards => handCards.AsReadOnly();

        /// <summary>
        /// 메인 덱 Card 인스턴스 (읽기 전용)
        /// </summary>
        public IReadOnlyList<Card> MainDeck => mainDeck.AsReadOnly();

        /// <summary>
        /// 임시무덤 Card 인스턴스 (읽기 전용)
        /// </summary>
        public IReadOnlyList<Card> TempGraveyard => tempGraveyard.AsReadOnly();

        /// <summary>
        /// 손패 Card 인스턴스 수
        /// </summary>
        public int HandCardCount => handCards.Count;

        /// <summary>
        /// 메인 덱 Card 인스턴스 수
        /// </summary>
        public int MainDeckCount => mainDeck.Count;

        /// <summary>
        /// 임시무덤 Card 인스턴스 수
        /// </summary>
        public int TempGraveyardCount => tempGraveyard.Count;

        /// <summary>
        /// 메인 덱이 비어있는지 여부
        /// </summary>
        public bool IsMainDeckEmpty => mainDeck.Count == 0;

        /// <summary>
        /// Card 인스턴스 손패가 가득 찼는지 여부
        /// </summary>
        public bool IsHandCardsFull => handCards.Count >= MaxHandSize;
        #endregion
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeCardManager();
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[CardManager] 중복된 CardManager 감지됨. 삭제합니다.");
                Destroy(gameObject);
                return;
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
        /// CardManager 초기화
        /// </summary>
        private void InitializeCardManager()
        {
            if (debugMode)
                Debug.Log("[CardManager] 카드 매니저 초기화 시작");

            // 이벤트 구독
            if (BattleManager.Instance != null)
            {
                BattleManager.OnTurnChanged += OnTurnChanged;
                BattleManager.OnBattleStarted += OnBattleStarted;
                BattleManager.OnBattleEnded += OnBattleEnded;
            }

            if (GameManager.Instance != null)
            {
                GameManager.OnGameInitialized += OnGameInitialized;
            }

            // 기본 상태 초기화
            ResetDrawCosts();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[CardManager] 카드 매니저 초기화 완료");
        }

        /// <summary>
        /// 게임 초기화 완료 시 호출
        /// </summary>
        private void OnGameInitialized()
        {
            // 시작 덱 설정 (기본 카드들로 구성)
            SetupStarterDeck();
        }

        /// <summary>
        /// 시작 덱 설정
        /// </summary>
        private void SetupStarterDeck()
        {
            if (debugMode)
                Debug.Log($"[CardManager] 시작 덱 확인: 인스펙터에서 설정된 카드 {starterDeck.Count}장");

            // 인스펙터에서 설정된 스타터 덱이 있으면 그대로 사용
            if (starterDeck.Count > 0)
            {
                if (debugMode)
                {
                    Debug.Log("[CardManager] 인스펙터에서 설정된 스타터 덱 사용:");
                    for (int i = 0; i < starterDeck.Count; i++)
                    {
                        Debug.Log($"  - {i}: {starterDeck[i]?.CardName ?? "null"}");
                    }
                }
            }
            else
            {
                // 인스펙터에 설정된 카드가 없으면 Resources에서 기본 카드 로드
                LoadDefaultStarterCards();
            }

            if (debugMode)
                Debug.Log($"[CardManager] 시작 덱 설정 완료: {starterDeck.Count}장");
        }

        /// <summary>
        /// 기본 스타터 카드 로드 (인스펙터에 설정된 카드가 없을 때)
        /// </summary>
        private void LoadDefaultStarterCards()
        {
            if (debugMode)
                Debug.Log("[CardManager] 기본 스타터 카드 로드 시작");

            starterDeck.Clear();

            // Resources에서 기본 카드들을 찾아 추가
            var defaultCards = Resources.LoadAll<CardSO>("Cards");

            if (defaultCards.Length > 0)
            {
                // 기본적으로 각 카드를 몇 장씩 추가
                foreach (var card in defaultCards.Take(3)) // 처음 3종류만
                {
                    for (int i = 0; i < 2; i++) // 각각 2장씩
                    {
                        starterDeck.Add(card);
                    }
                }

                if (debugMode)
                    Debug.Log($"[CardManager] Resources에서 기본 카드 로드: {starterDeck.Count}장");
            }
            else
            {
                Debug.LogWarning("[CardManager] Resources/Cards에 카드가 없습니다!");
            }
        }

        /// <summary>
        /// 새 게임 시작 시 덱 초기화
        /// </summary>
        public void InitializeDeckForNewGame()
        {
            if (debugMode)
            {
                Debug.Log("[CardManager] 새 게임용 덱 초기화");
                Debug.Log($"[CardManager] 시작 덱 카드 수: {starterDeck.Count}");
            }

            // 시작 덱을 현재 덱으로 복사
            currentDeck.Clear();
            currentDeck.AddRange(starterDeck);

            if (debugMode)
                Debug.Log($"[CardManager] 현재 덱으로 복사 완료: {currentDeck.Count}장");

            // 다른 영역 초기화
            hand.Clear();
            discardPile.Clear();
            removedCards.Clear();

            // 덱 셔플
            ShuffleDeck();

            // 드로우 비용 초기화
            ResetDrawCosts();

            // 초기 드로우 플래그 초기화
            hasPerformedInitialDraw = false;

            // Card 인스턴스 시스템 초기화
            InitializeCardInstanceSystem();

            // 이벤트 발생
            OnHandChanged?.Invoke(hand);
            OnDrawCostChanged?.Invoke(currentDrawCost);

            if (debugMode)
                Debug.Log("[CardManager] 새 게임용 덱 초기화 완료");
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 턴 변경 시 호출
        /// </summary>
        private void OnTurnChanged(TurnType turnType, int turnNumber)
        {
            if (turnType == TurnType.Player)
            {
                // 플레이어 턴 시작
                StartPlayerTurn();
            }
            else
            {
                // 적 턴 시작 - 특별한 처리 없음
            }
        }

        /// <summary>
        /// 전투 시작 시 호출
        /// </summary>
        private void OnBattleStarted(BattleSO battleData)
        {
            if (debugMode)
                Debug.Log("[CardManager] 전투 시작 - 덱 준비 (드로우는 몬스터 스폰 완료 후 진행)");

            // 전투 시작 시 손패와 임시무덤 모두 덱으로 되돌리고 셔플 (Card 인스턴스)
            ReturnAllCardInstancesToDeck();

            // 기존 CardSO 시스템도 동기화
            ReturnAllCardsToDeck();

            // 초기 드로우는 몬스터 스폰 애니메이션 완료 후에 진행하도록 변경
            // DrawCards(MaxHandSize, true); // 제거됨
        }

        /// <summary>
        /// 전투 종료 시 호출
        /// </summary>
        private void OnBattleEnded(bool victory)
        {
            if (debugMode)
                Debug.Log("[CardManager] 전투 종료 - 카드 정리");

            // 모든 카드를 덱으로 되돌림
            ReturnAllCardsToDeck();
        }

        /// <summary>
        /// 초기 카드 드로우 실행 (몬스터 스폰 애니메이션 완료 후 호출)
        /// </summary>
        public void PerformInitialCardDraw()
        {
            if (debugMode)
                Debug.Log("[CardManager] 초기 카드 드로우 시작");

            // 초기 드로우 (Card 인스턴스 기반)
            DrawCards(MaxHandSize, true);

            // 초기 드로우 완료 플래그 설정
            hasPerformedInitialDraw = true;
        }
        #endregion

        #region Draw System
        /// <summary>
        /// 플레이어 턴 시작 시 처리
        /// </summary>
        private void StartPlayerTurn()
        {
            if (debugMode)
                Debug.Log("[CardManager] 플레이어 턴 시작 처리");

            // 드로우 비용 초기화
            ResetDrawCosts();

            // 초기 카드 드로우가 완료된 후에만 카드 회수 및 드로우 진행
            if (hasPerformedInitialDraw)
            {
                // 조합창에 있는 카드들을 먼저 임시무덤으로 이동
                ProcessComboSlotCardsOnTurnStart();

                // 턴 종료 시 버린 카드들과 손에 있던 카드들을 덱으로 되돌리고 셔플 (Card 인스턴스)
                ReturnAllCardInstancesToDeck();

                // CardSO 시스템 동기화
                SyncCardSOSystemFromCardInstances();

                // 새 손패 드로우 (Card 인스턴스 기반)
                DrawCards(MaxHandSize, true);
            }
            else if (debugMode)
            {
                Debug.Log("[CardManager] 초기 드로우가 아직 완료되지 않아 카드 회수 및 드로우 생략");
            }
        }

        /// <summary>
        /// 최대 손패까지 드로우
        /// </summary>
        private void DrawToMaxHand()
        {
            int targetHandSize = MaxHandSize;
            int cardsToDraw = targetHandSize - hand.Count;

            if (cardsToDraw > 0)
            {
                DrawCards(cardsToDraw);
            }
        }

        /// <summary>
        /// 초기 손패 드로우 (전투 시작 시)
        /// </summary>
        private void DrawInitialHand()
        {
            if (debugMode)
                Debug.Log("[CardManager] 초기 손패 드로우");

            DrawToMaxHand();
        }

        /// <summary>
        /// 지정한 수만큼 카드 드로우
        /// </summary>
        public void DrawCards(int count)
        {
            if (count <= 0) return;

            int actualDraws = 0;

            for (int i = 0; i < count; i++)
            {
                if (IsHandFull)
                {
                    if (debugMode)
                        Debug.Log($"[CardManager] 손패가 가득 참 - {actualDraws}장만 드로우");
                    break;
                }

                if (DrawSingleCard())
                {
                    actualDraws++;
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[CardManager] 더 이상 드로우할 카드가 없음 - {actualDraws}장 드로우");
                    break;
                }
            }

            if (actualDraws > 0)
            {
                OnHandChanged?.Invoke(hand);

                if (debugMode)
                    Debug.Log($"[CardManager] {actualDraws}장 드로우 완료");
            }
        }

        /// <summary>
        /// 단일 카드 드로우
        /// </summary>
        private bool DrawSingleCard()
        {
            // 덱이 비어있으면 임시무덤에서 셔플
            if (IsDeckEmpty && discardPile.Count > 0)
            {
                ReshuffleDiscardPile();
            }

            // 여전히 덱이 비어있으면 드로우 불가
            if (IsDeckEmpty)
            {
                return false;
            }

            // 덱에서 카드 뽑기
            var drawnCard = currentDeck[0];
            currentDeck.RemoveAt(0);

            // 손패에 추가
            hand.Add(drawnCard);

            // 이벤트 발생
            OnCardDrawn?.Invoke(drawnCard);

            if (debugMode)
                Debug.Log($"[CardManager] 카드 드로우: {drawnCard.CardName}");

            return true;
        }

        /// <summary>
        /// 마나를 사용하여 추가 드로우
        /// </summary>
        public bool DrawAdditionalCard()
        {
            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[CardManager] PlayerManager를 찾을 수 없습니다.");
                return false;
            }

            // 마나 확인
            if (!PlayerManager.Instance.SpendMana(currentDrawCost))
            {
                if (debugMode)
                    Debug.Log($"[CardManager] 추가 드로우 실패 - 마나 부족 (필요: {currentDrawCost})");
                return false;
            }

            // 추가 드로우 실행
            if (DrawSingleCard())
            {
                // 드로우 성공 시 비용 증가
                additionalDrawsThisTurn++;
                currentDrawCost += drawCostIncrease;

                OnHandChanged?.Invoke(hand);
                OnDrawCostChanged?.Invoke(currentDrawCost);

                if (debugMode)
                    Debug.Log($"[CardManager] 추가 드로우 성공 - 다음 비용: {currentDrawCost}");

                return true;
            }
            else
            {
                // 드로우 실패 시 마나 환불
                PlayerManager.Instance.RestoreMana(currentDrawCost);

                if (debugMode)
                    Debug.Log("[CardManager] 추가 드로우 실패 - 드로우할 카드 없음");

                return false;
            }
        }

        /// <summary>
        /// 드로우 비용 초기화
        /// </summary>
        private void ResetDrawCosts()
        {
            currentDrawCost = baseDrawCost;
            additionalDrawsThisTurn = 0;
            OnDrawCostChanged?.Invoke(currentDrawCost);

            if (debugMode)
                Debug.Log($"[CardManager] 드로우 비용 초기화: {currentDrawCost}");
        }
        #endregion

        #region Card Usage
        /// <summary>
        /// 카드 사용
        /// </summary>
        public void UseCards(CardSO[] cards)
        {
            if (cards == null || cards.Length == 0)
            {
                Debug.LogWarning("[CardManager] 사용할 카드가 없습니다.");
                return;
            }

            // 카드 유효성 검증
            foreach (var card in cards)
            {
                if (!hand.Contains(card))
                {
                    Debug.LogError($"[CardManager] 손패에 없는 카드를 사용하려고 시도: {card.CardName}");
                    return;
                }
            }

            if (debugMode)
                Debug.Log($"[CardManager] 카드 사용: {string.Join(", ", cards.Select(c => c.CardName))}");

            // 손패에서 제거하고 임시무덤으로 이동
            foreach (var card in cards)
            {
                hand.Remove(card);
                discardPile.Add(card);
            }

            // 이벤트 발생
            OnCardsUsed?.Invoke(cards);
            OnHandChanged?.Invoke(hand);

            // BattleManager에게 카드 사용 알림
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.UseCards(cards);
            }
        }

        /// <summary>
        /// 단일 카드 사용
        /// </summary>
        public void UseCard(CardSO card)
        {
            UseCards(new CardSO[] { card });
        }
        #endregion

        #region Deck Management
        /// <summary>
        /// 덱에 카드 추가 (전투 보상, 상점 구매 등)
        /// </summary>
        public void AddCardToDeck(CardSO card)
        {
            if (card == null)
            {
                Debug.LogError("[CardManager] null 카드를 덱에 추가하려고 시도했습니다.");
                return;
            }

            currentDeck.Add(card);

            if (debugMode)
                Debug.Log($"[CardManager] 덱에 카드 추가: {card.CardName}");
        }

        /// <summary>
        /// 덱에서 카드 제거 (상점에서 카드 제거 등)
        /// </summary>
        public bool RemoveCardFromDeck(CardSO card)
        {
            if (card == null) return false;

            // 현재 덱에서 제거 시도
            if (currentDeck.Remove(card))
            {
                removedCards.Add(card);

                if (debugMode)
                    Debug.Log($"[CardManager] 덱에서 카드 제거: {card.CardName}");

                return true;
            }

            // 손패에서 제거 시도
            if (hand.Remove(card))
            {
                removedCards.Add(card);
                OnHandChanged?.Invoke(hand);

                if (debugMode)
                    Debug.Log($"[CardManager] 손패에서 카드 제거: {card.CardName}");

                return true;
            }

            // 임시무덤에서 제거 시도
            if (discardPile.Remove(card))
            {
                removedCards.Add(card);

                if (debugMode)
                    Debug.Log($"[CardManager] 임시무덤에서 카드 제거: {card.CardName}");

                return true;
            }

            if (debugMode)
                Debug.LogWarning($"[CardManager] 제거할 카드를 찾을 수 없음: {card.CardName}");

            return false;
        }

        /// <summary>
        /// 덱에서 특정 인덱스의 카드 제거 (상점 카드 제거용)
        /// </summary>
        public bool RemoveCardFromDeckByIndex(CardSO card, int cardIndex)
        {
            if (card == null || cardIndex < 0 || cardIndex >= currentDeck.Count) return false;

            // 현재 덱에서 지정된 인덱스의 카드가 맞는지 확인
            if (currentDeck[cardIndex] == card)
            {
                currentDeck.RemoveAt(cardIndex);
                removedCards.Add(card);

                if (debugMode)
                    Debug.Log($"[CardManager] 덱에서 카드 제거 (인덱스 {cardIndex}): {card.CardName}");

                return true;
            }

            if (debugMode)
                Debug.LogWarning($"[CardManager] 인덱스 {cardIndex}의 카드가 일치하지 않음. 예상: {card.CardName}, 실제: {(cardIndex < currentDeck.Count ? currentDeck[cardIndex].CardName : "없음")}");

            return false;
        }

        /// <summary>
        /// 덱 셔플
        /// </summary>
        public void ShuffleDeck()
        {
            if (currentDeck.Count <= 1) return;

            // Fisher-Yates 셔플 알고리즘
            for (int i = currentDeck.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                var temp = currentDeck[i];
                currentDeck[i] = currentDeck[randomIndex];
                currentDeck[randomIndex] = temp;
            }

            OnDeckShuffled?.Invoke();

            if (debugMode)
                Debug.Log($"[CardManager] 덱 셔플 완료: {currentDeck.Count}장");
        }

        /// <summary>
        /// 임시무덤을 덱으로 셔플하여 되돌리기
        /// </summary>
        private void ReshuffleDiscardPile()
        {
            if (discardPile.Count == 0) return;

            if (debugMode)
                Debug.Log($"[CardManager] 임시무덤 재셔플: {discardPile.Count}장");

            // 임시무덤의 모든 카드를 덱으로 이동
            currentDeck.AddRange(discardPile);
            discardPile.Clear();

            // 덱 셔플
            ShuffleDeck();
        }

        /// <summary>
        /// 모든 카드를 덱으로 되돌리고 셔플
        /// </summary>
        private void ReturnAllCardsToDeck()
        {
            if (debugMode)
                Debug.Log($"[CardManager] 모든 카드 덱으로 회수 - 손패: {hand.Count}, 임시무덤: {discardPile.Count}");

            // 손패의 모든 카드를 덱으로 이동
            currentDeck.AddRange(hand);
            hand.Clear();

            // 임시무덤의 모든 카드를 덱으로 이동
            currentDeck.AddRange(discardPile);
            discardPile.Clear();

            // 덱 셔플
            ShuffleDeck();

            // 이벤트 발생
            OnHandChanged?.Invoke(hand);
        }
        #endregion

        #region Card Query
        /// <summary>
        /// 전체 보유 카드 수 (덱 + 손패 + 임시무덤)
        /// </summary>
        public int GetTotalCardCount()
        {
            return currentDeck.Count + hand.Count + discardPile.Count;
        }

        /// <summary>
        /// 특정 카드를 보유하고 있는지 확인
        /// </summary>
        public bool HasCard(CardSO card)
        {
            return currentDeck.Contains(card) || hand.Contains(card) || discardPile.Contains(card);
        }

        /// <summary>
        /// 특정 이름의 카드 개수 반환
        /// </summary>
        public int GetCardCount(string cardName)
        {
            int count = 0;

            count += currentDeck.Count(c => c.CardName == cardName);
            count += hand.Count(c => c.CardName == cardName);
            count += discardPile.Count(c => c.CardName == cardName);

            return count;
        }

        /// <summary>
        /// 현재 손패에서 특정 타입의 카드들 반환
        /// </summary>
        public List<CardSO> GetCardsInHandByType(CardType cardType)
        {
            return hand.Where(c => c.Type == cardType).ToList();
        }

        /// <summary>
        /// 현재 손패에서 특정 속성의 카드들 반환
        /// </summary>
        public List<CardSO> GetCardsInHandByElement(ElementType elementType)
        {
            return hand.Where(c => c.Element == elementType).ToList();
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== CardManager Debug Info ===");
            Debug.Log($"== CardSO System ==");
            Debug.Log($"Deck: {currentDeck.Count}장");
            Debug.Log($"Hand: {hand.Count}/{MaxHandSize}장");
            Debug.Log($"Discard Pile: {discardPile.Count}장");
            Debug.Log($"Removed Cards: {removedCards.Count}장");
            Debug.Log($"Total Cards: {GetTotalCardCount()}장");

            Debug.Log($"== Card Instance System (태스크 71번) ==");
            Debug.Log($"Main Deck: {mainDeck.Count}장");
            Debug.Log($"Hand Cards: {handCards.Count}/{MaxHandSize}장");
            Debug.Log($"Temp Graveyard: {tempGraveyard.Count}장");

            Debug.Log($"== Draw System ==");
            Debug.Log($"Draw Cost: {currentDrawCost} (draws this turn: {additionalDrawsThisTurn})");

            if (handCards.Count > 0)
            {
                Debug.Log("Current Hand Cards (Instances):");
                foreach (var card in handCards)
                {
                    Debug.Log($"  - {card.CardName} ({card.Type}) [ID: {card.InstanceId}]");
                }
            }
        }

        /// <summary>
        /// 강제 드로우 (디버그용)
        /// </summary>
        [ContextMenu("Force Draw Card")]
        public void ForceDrawCard()
        {
            if (!debugMode)
            {
                Debug.LogWarning("[CardManager] ForceDrawCard는 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            DrawCards(1);
        }

        /// <summary>
        /// 강제 덱 셔플 (디버그용)
        /// </summary>
        [ContextMenu("Force Shuffle Deck")]
        public void ForceShuffleDeck()
        {
            if (!debugMode)
            {
                Debug.LogWarning("[CardManager] ForceShuffleDeck는 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            ShuffleDeck();
        }
        #endregion

        #region Card Instance Management (태스크 71번)
        /// <summary>
        /// CardSO에서 Card 인스턴스로 변환
        /// </summary>
        private Card CreateCardInstance(CardSO cardSO)
        {
            if (cardSO == null) return null;
            return new Card(cardSO);
        }

        /// <summary>
        /// CardSO 리스트를 Card 인스턴스 리스트로 변환
        /// </summary>
        private List<Card> ConvertToCardInstances(List<CardSO> cardSOs)
        {
            var cardInstances = new List<Card>();
            foreach (var cardSO in cardSOs)
            {
                if (cardSO != null)
                {
                    cardInstances.Add(CreateCardInstance(cardSO));
                }
            }
            return cardInstances;
        }

        /// <summary>
        /// Card 인스턴스에서 CardSO 추출
        /// </summary>
        private CardSO ExtractCardSO(Card card)
        {
            return card?.CardData;
        }

        /// <summary>
        /// Card 인스턴스 리스트에서 CardSO 리스트로 변환
        /// </summary>
        private List<CardSO> ConvertToCardSOs(List<Card> cards)
        {
            var cardSOs = new List<CardSO>();
            foreach (var card in cards)
            {
                var cardSO = ExtractCardSO(card);
                if (cardSO != null)
                {
                    cardSOs.Add(cardSO);
                }
            }
            return cardSOs;
        }

        /// <summary>
        /// 덱 동기화 - CardSO 덱에서 Card 인스턴스 덱으로
        /// </summary>
        private void SyncMainDeckFromCurrentDeck()
        {
            mainDeck.Clear();
            mainDeck.AddRange(ConvertToCardInstances(currentDeck));

            if (debugMode)
                Debug.Log($"[CardManager] 메인덱 동기화: {mainDeck.Count}장");
        }

        /// <summary>
        /// 손패 동기화 - CardSO 손패에서 Card 인스턴스 손패로
        /// </summary>
        private void SyncHandCardsFromHand()
        {
            handCards.Clear();
            handCards.AddRange(ConvertToCardInstances(hand));

            OnHandCardsChanged?.Invoke(handCards);

            if (debugMode)
                Debug.Log($"[CardManager] 손패 동기화: {handCards.Count}장");
        }

        /// <summary>
        /// 임시무덤 동기화 - CardSO 임시무덤에서 Card 인스턴스 임시무덤으로
        /// </summary>
        private void SyncTempGraveyardFromDiscardPile()
        {
            tempGraveyard.Clear();
            tempGraveyard.AddRange(ConvertToCardInstances(discardPile));

            if (debugMode)
                Debug.Log($"[CardManager] 임시무덤 동기화: {tempGraveyard.Count}장");
        }

        /// <summary>
        /// 모든 Card 인스턴스 컬렉션 동기화
        /// </summary>
        private void SyncAllCardInstances()
        {
            SyncMainDeckFromCurrentDeck();
            SyncHandCardsFromHand();
            SyncTempGraveyardFromDiscardPile();
        }

        /// <summary>
        /// Card 인스턴스 기반 카드 드로우 (태스크 71번 API)
        /// </summary>
        public List<Card> DrawCards(int count, bool useCardInstances = true)
        {
            if (!useCardInstances)
            {
                // 기존 방식 사용
                DrawCards(count);
                SyncHandCardsFromHand();
                return handCards.ToList();
            }

            var drawnCards = new List<Card>();

            if (count <= 0) return drawnCards;

            for (int i = 0; i < count; i++)
            {
                if (IsHandCardsFull)
                {
                    if (debugMode)
                        Debug.Log($"[CardManager] Card 인스턴스 손패가 가득 찬 상태 - {drawnCards.Count}장만 드로우");
                    break;
                }

                var drawnCard = DrawSingleCardInstance();
                if (drawnCard != null)
                {
                    drawnCards.Add(drawnCard);
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"[CardManager] 더 이상 드로우할 카드가 없음 - {drawnCards.Count}장 드로우");
                    break;
                }
            }

            if (drawnCards.Count > 0)
            {
                // CardSO 시스템 동기화
                SyncCardSOSystemFromCardInstances();

                OnHandCardsChanged?.Invoke(handCards);
                OnHandChanged?.Invoke(hand);

                if (debugMode)
                    Debug.Log($"[CardManager] Card 인스턴스 {drawnCards.Count}장 드로우 완료");
            }

            return drawnCards;
        }

        /// <summary>
        /// 단일 Card 인스턴스 드로우
        /// </summary>
        private Card DrawSingleCardInstance()
        {
            // 메인 덱이 비어있으면 임시무덤에서 셔플
            if (IsMainDeckEmpty && tempGraveyard.Count > 0)
            {
                ReshuffleCardInstancesFromTempGraveyard();
            }

            // 여전히 덱이 비어있으면 드로우 불가
            if (IsMainDeckEmpty)
            {
                return null;
            }

            // 메인 덱에서 카드 뽑기
            var drawnCard = mainDeck[0];
            mainDeck.RemoveAt(0);

            // 손패에 추가
            handCards.Add(drawnCard);

            // CardSO 시스템 동기화는 나중에 한번에 처리

            // 이벤트 발생
            OnCardInstanceDrawn?.Invoke(drawnCard);

            if (debugMode)
                Debug.Log($"[CardManager] Card 인스턴스 드로우: {drawnCard.CardName}");

            return drawnCard;
        }

        /// <summary>
        /// Card 인스턴스 사용 (태스크 71번 API)
        /// </summary>
        public void UseCard(Card card)
        {
            if (card == null)
            {
                Debug.LogWarning("[CardManager] 사용할 Card 인스턴스가 null입니다.");
                return;
            }

            UseCards(new Card[] { card });
        }

        /// <summary>
        /// Card 인스턴스들 사용 (태스크 71번 API)  
        /// </summary>
        public void UseCards(Card[] cards)
        {
            if (cards == null || cards.Length == 0)
            {
                Debug.LogWarning("[CardManager] 사용할 Card 인스턴스가 없습니다.");
                return;
            }

            // 카드 유효성 검증
            foreach (var card in cards)
            {
                if (card == null || !handCards.Contains(card))
                {
                    Debug.LogError($"[CardManager] 손패에 없는 Card 인스턴스를 사용하려고 시도: {card?.CardName ?? "null"}");
                    return;
                }
            }

            if (debugMode)
                Debug.Log($"[CardManager] Card 인스턴스 사용: {string.Join(", ", cards.Select(c => c.CardName))}");

            // 손패에서 제거하고 임시무덤으로 이동 (Card 인스턴스만 처리)
            foreach (var card in cards)
            {
                handCards.Remove(card);
                tempGraveyard.Add(card);
            }

            // CardSO 시스템 동기화
            SyncCardSOSystemFromCardInstances();

            // 이벤트 발생
            OnCardInstancesUsed?.Invoke(cards);
            OnHandCardsChanged?.Invoke(handCards);

            var cardSOs = ConvertToCardSOs(cards.ToList()).ToArray();
            if (cardSOs.Length > 0)
            {
                OnCardsUsed?.Invoke(cardSOs);
                OnHandChanged?.Invoke(hand);

                // BattleManager에게 카드 사용 알림
                if (BattleManager.Instance != null)
                {
                    BattleManager.Instance.UseCards(cardSOs);
                }
            }
        }

        /// <summary>
        /// 턴 종료 처리 (태스크 71번 API)
        /// </summary>
        public void EndTurn()
        {
            if (debugMode)
                Debug.Log($"[CardManager] 턴 종료 - 모든 Card 인스턴스 메인덱으로 회수");

            // 조합창에 있는 카드들을 먼저 임시무덤으로 이동
            ProcessComboSlotCardsOnTurnEnd();

            // 모든 Card 인스턴스를 메인덱으로 되돌리고 셔플
            ReturnAllCardInstancesToDeck();

            // CardSO 시스템은 Card 인스턴스 시스템에서 역동기화하여 처리
            SyncCardSOSystemFromCardInstances();

            // 드로우 비용 초기화
            ResetDrawCosts();
        }

        /// <summary>
        /// 턴 시작 시 조합창에 있는 카드들을 임시무덤으로 이동
        /// </summary>
        private void ProcessComboSlotCardsOnTurnStart()
        {
            if (BattleUIManager.Instance == null) return;

            var comboSlotCards = BattleUIManager.Instance.GetComboSlotCards();
            if (comboSlotCards.Count == 0) return;

            if (debugMode)
                Debug.Log($"[CardManager] 턴 시작 - 조합창 카드 {comboSlotCards.Count}장을 임시무덤으로 이동");

            // 조합창 카드들을 손패에서 제거하고 임시무덤으로 이동 (Card 인스턴스만 처리)
            foreach (var card in comboSlotCards)
            {
                if (card != null)
                {
                    // 손패에서 해당 카드 제거 (중복 방지)
                    if (handCards.Contains(card))
                    {
                        handCards.Remove(card);

                        if (debugMode)
                            Debug.Log($"[CardManager] 조합창 카드 손패에서 제거: {card.CardName}");
                    }

                    // 임시무덤에 추가
                    tempGraveyard.Add(card);

                    if (debugMode)
                        Debug.Log($"[CardManager] 조합창 카드 임시무덤 이동: {card.CardName}");
                }
            }

            // 조합창 UI 정리
            BattleUIManager.Instance.ClearComboSlotsUIOnly();

            // 손패 변경 이벤트 발생
            OnHandCardsChanged?.Invoke(handCards);
        }

        /// <summary>
        /// 턴 종료 시 조합창에 있는 카드들을 임시무덤으로 이동
        /// </summary>
        private void ProcessComboSlotCardsOnTurnEnd()
        {
            if (BattleUIManager.Instance == null) return;

            var comboSlotCards = BattleUIManager.Instance.GetComboSlotCards();
            if (comboSlotCards.Count == 0) return;

            if (debugMode)
                Debug.Log($"[CardManager] 턴 종료 - 조합창 카드 {comboSlotCards.Count}장을 임시무덤으로 이동");

            // 조합창 카드들을 손패에서 제거하고 임시무덤으로 이동 (Card 인스턴스만 처리)
            foreach (var card in comboSlotCards)
            {
                if (card != null)
                {
                    // 손패에서 해당 카드 제거 (중복 방지)
                    if (handCards.Contains(card))
                    {
                        handCards.Remove(card);

                        if (debugMode)
                            Debug.Log($"[CardManager] 조합창 카드 손패에서 제거: {card.CardName}");
                    }

                    // 임시무덤에 추가
                    tempGraveyard.Add(card);

                    if (debugMode)
                        Debug.Log($"[CardManager] 조합창 카드 임시무덤 이동: {card.CardName}");
                }
            }

            // 조합창 UI 정리
            BattleUIManager.Instance.ClearComboSlotsUIOnly();

            // 손패 변경 이벤트 발생
            OnHandCardsChanged?.Invoke(handCards);
        }

        /// <summary>
        /// 모든 Card 인스턴스를 메인덱으로 되돌리고 셔플
        /// </summary>
        private void ReturnAllCardInstancesToDeck()
        {
            if (debugMode)
                Debug.Log($"[CardManager] 모든 Card 인스턴스 메인덱으로 회수 - 손패: {handCards.Count}, 임시무덤: {tempGraveyard.Count}");

            // 손패의 모든 카드를 메인덱으로 이동
            mainDeck.AddRange(handCards);
            handCards.Clear();

            // 임시무덤의 모든 카드를 메인덱으로 이동
            mainDeck.AddRange(tempGraveyard);
            tempGraveyard.Clear();

            // 메인덱 셔플
            ShuffleMainDeck();

            // 이벤트 발생
            OnHandCardsChanged?.Invoke(handCards);
        }

        /// <summary>
        /// 메인덱 셔플 (Card 인스턴스)
        /// </summary>
        private void ShuffleMainDeck()
        {
            if (mainDeck.Count <= 1) return;

            // Fisher-Yates 셔플 알고리즘
            for (int i = mainDeck.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                var temp = mainDeck[i];
                mainDeck[i] = mainDeck[randomIndex];
                mainDeck[randomIndex] = temp;
            }

            if (debugMode)
                Debug.Log($"[CardManager] 메인덱 셔플 완료: {mainDeck.Count}장");
        }

        /// <summary>
        /// 임시무덤을 메인덱으로 셔플하여 되돌리기 (Card 인스턴스)
        /// </summary>
        private void ReshuffleCardInstancesFromTempGraveyard()
        {
            if (tempGraveyard.Count == 0) return;

            if (debugMode)
                Debug.Log($"[CardManager] Card 인스턴스 임시무덤 재셔플: {tempGraveyard.Count}장");

            // 임시무덤의 모든 카드를 메인덱으로 이동
            mainDeck.AddRange(tempGraveyard);
            tempGraveyard.Clear();

            // 메인덱 셔플
            ShuffleMainDeck();
        }

        /// <summary>
        /// Card 인스턴스 시스템에서 CardSO 시스템으로 역동기화
        /// </summary>
        private void SyncCardSOSystemFromCardInstances()
        {
            if (debugMode)
            {
                int beforeTotal = currentDeck.Count + hand.Count + discardPile.Count;
                int afterTotal = mainDeck.Count + handCards.Count + tempGraveyard.Count;

                Debug.Log($"[CardManager] 역동기화 전 CardSO 총 카드 수: {beforeTotal} (덱:{currentDeck.Count}, 손패:{hand.Count}, 임시무덤:{discardPile.Count})");
                Debug.Log($"[CardManager] 역동기화 후 예상 총 카드 수: {afterTotal} (메인덱:{mainDeck.Count}, 손패:{handCards.Count}, 임시무덤:{tempGraveyard.Count})");
            }

            // 현재 덱 동기화
            currentDeck.Clear();
            currentDeck.AddRange(ConvertToCardSOs(mainDeck));

            // 손패 동기화
            hand.Clear();
            hand.AddRange(ConvertToCardSOs(handCards));

            // 임시무덤 동기화
            discardPile.Clear();
            discardPile.AddRange(ConvertToCardSOs(tempGraveyard));

            if (debugMode)
            {
                int finalTotal = currentDeck.Count + hand.Count + discardPile.Count;
                Debug.Log($"[CardManager] CardSO 시스템 역동기화 완료 - 최종 총 카드 수: {finalTotal} (덱:{currentDeck.Count}, 손패:{hand.Count}, 임시무덤:{discardPile.Count})");

                if (finalTotal != mainDeck.Count + handCards.Count + tempGraveyard.Count)
                {
                    Debug.LogWarning($"[CardManager] 카드 수 불일치! Card인스턴스: {mainDeck.Count + handCards.Count + tempGraveyard.Count}, CardSO: {finalTotal}");
                }
            }
        }

        /// <summary>
        /// 게임 시작 시 Card 인스턴스 시스템 초기화
        /// </summary>
        public void InitializeCardInstanceSystem()
        {
            if (debugMode)
                Debug.Log("[CardManager] Card 인스턴스 시스템 초기화");

            // 기존 CardSO 시스템에서 Card 인스턴스 시스템으로 동기화
            SyncAllCardInstances();

            if (debugMode)
                Debug.Log($"[CardManager] Card 인스턴스 시스템 초기화 완료 - 메인덱: {mainDeck.Count}, 손패: {handCards.Count}, 임시무덤: {tempGraveyard.Count}");
        }

        /// <summary>
        /// 마나를 사용하여 추가 Card 인스턴스 드로우 (태스크 71번 API)
        /// </summary>
        public Card DrawAdditionalCardInstance()
        {
            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[CardManager] PlayerManager를 찾을 수 없습니다.");
                return null;
            }

            // 마나 확인
            if (!PlayerManager.Instance.SpendMana(currentDrawCost))
            {
                if (debugMode)
                    Debug.Log($"[CardManager] 추가 Card 인스턴스 드로우 실패 - 마나 부족 (필요: {currentDrawCost})");
                return null;
            }

            // 추가 드로우 실행
            var drawnCard = DrawSingleCardInstance();
            if (drawnCard != null)
            {
                // 드로우 성공 시 비용 증가
                additionalDrawsThisTurn++;
                currentDrawCost += drawCostIncrease;

                OnHandCardsChanged?.Invoke(handCards);
                OnDrawCostChanged?.Invoke(currentDrawCost);

                if (debugMode)
                    Debug.Log($"[CardManager] 추가 Card 인스턴스 드로우 성공 - 다음 비용: {currentDrawCost}");

                return drawnCard;
            }
            else
            {
                // 드로우 실패 시 마나 환불
                PlayerManager.Instance.RestoreMana(currentDrawCost);

                if (debugMode)
                    Debug.Log("[CardManager] 추가 Card 인스턴스 드로우 실패 - 드로우할 카드 없음");

                return null;
            }
        }

        #region 태스크 71번 공식 API
        /// <summary>
        /// 메인덱 참조 (태스크 71번 요구사항)
        /// DeckManager의 mainDeck 역할
        /// </summary>
        public IReadOnlyList<Card> MainDeckCards => MainDeck;

        /// <summary>
        /// 손패 참조 (태스크 71번 요구사항)  
        /// DeckManager의 handCards 역할
        /// </summary>
        public IReadOnlyList<Card> HandCardsCollection => HandCards;

        /// <summary>
        /// 임시무덤 참조 (태스크 71번 요구사항)
        /// DeckManager의 tempGraveyard 역할
        /// </summary>
        public IReadOnlyList<Card> TempGraveyardCards => TempGraveyard;

        /// <summary>
        /// 카드 드로우 메서드 (태스크 71번 요구사항)
        /// 최대 5장까지 드로우, 유물 효과로 최대 수량 증가 가능
        /// </summary>
        public List<Card> DrawCardsToMax()
        {
            return DrawCards(MaxHandSize - HandCardCount, true);
        }

        /// <summary>
        /// 지정된 수만큼 카드 드로우 (태스크 71번 요구사항)
        /// </summary>
        public List<Card> DrawCardInstances(int count)
        {
            return DrawCards(count, true);
        }

        /// <summary>
        /// Card 인스턴스 사용 메서드 (태스크 71번 요구사항)
        /// 카드 사용 후 tempGraveyard로 이동
        /// </summary>
        public void UseCardInstance(Card card)
        {
            UseCard(card);
        }

        /// <summary>
        /// 여러 Card 인스턴스 사용 메서드 (태스크 71번 요구사항)
        /// </summary>
        public void UseCardInstances(params Card[] cards)
        {
            UseCards(cards);
        }

        /// <summary>
        /// 턴 종료 메서드 (태스크 71번 요구사항)
        /// 모든 카드를 mainDeck으로 복귀 후 셔플
        /// </summary>
        public void EndTurnProcessing()
        {
            EndTurn();
        }

        /// <summary>
        /// 추가 Card 인스턴스 드로우 메서드 (태스크 71번 요구사항)
        /// 마나 소모 (10→20→30) 처리
        /// </summary>
        public Card DrawAdditionalCardWithMana()
        {
            return DrawAdditionalCardInstance();
        }

        /// <summary>
        /// DeckManager 스타일 정보 프로퍼티 (태스크 71번 호환)
        /// </summary>
        public class DeckManagerInfo
        {
            public IReadOnlyList<Card> MainDeck { get; internal set; }
            public IReadOnlyList<Card> HandCards { get; internal set; }
            public IReadOnlyList<Card> TempGraveyard { get; internal set; }
            public int MainDeckCount { get; internal set; }
            public int HandCardCount { get; internal set; }
            public int TempGraveyardCount { get; internal set; }
            public int MaxHandSize { get; internal set; }
            public int CurrentDrawCost { get; internal set; }
            public int AdditionalDrawsThisTurn { get; internal set; }
        }

        /// <summary>
        /// DeckManager 정보 반환 (태스크 71번 호환)
        /// </summary>
        public DeckManagerInfo GetDeckManagerInfo()
        {
            return new DeckManagerInfo
            {
                MainDeck = MainDeck,
                HandCards = HandCards,
                TempGraveyard = TempGraveyard,
                MainDeckCount = MainDeckCount,
                HandCardCount = HandCardCount,
                TempGraveyardCount = TempGraveyardCount,
                MaxHandSize = MaxHandSize,
                CurrentDrawCost = CurrentDrawCost,
                AdditionalDrawsThisTurn = AdditionalDrawsThisTurn
            };
        }
        #endregion
        #endregion
    }
}