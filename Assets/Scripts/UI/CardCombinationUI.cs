using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Maglin.Cards;
using Maglin.Core;
using Maglin.Enemy;
using Maglin.Battle;
using Maglin.Player;

namespace Maglin.UI
{
    /// <summary>
    /// 카드 조합 UI - 3개 슬롯과 사용 버튼을 관리
    /// </summary>
    public class CardCombinationUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private CardSlotUI[] cardSlots = new CardSlotUI[3];
        [SerializeField] private Button useButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Text useButtonText;
        [SerializeField] private Image useButtonImage;

        [Header("버튼 상태 색상")]
        [SerializeField] private Color enabledButtonColor = Color.green;
        [SerializeField] private Color disabledButtonColor = Color.gray;
        [SerializeField] private Color invalidCombinationColor = Color.red;

        [Header("조합 결과 미리보기")]
        [SerializeField] private GameObject previewPanel;
        [SerializeField] private Image previewCardImage;
        [SerializeField] private Text previewCardName;
        [SerializeField] private Text previewDescription;

        [Header("대상 선택 UI")]
        [SerializeField] private GameObject targetSelectionPanel;
        [SerializeField] private Text targetInstructionText;

        // 슬롯 관리
        private List<Card> slottedCards = new List<Card>();
        private bool isReadyToUse = false;
        private bool isTargetSelectionMode = false;

        // 조합 결과
        private Card previewedComboResult;
        private bool hasValidCombination = false;

        // 참조
        private ComboManager comboManager;
        private BattleManager battleManager;
        private PlayerManager playerManager;

        // 이벤트
        public System.Action<List<Card>> OnCombinationChanged;
        public System.Action<Card> OnCombinationUsed;
        public System.Action OnCombinationCleared;
        public System.Action OnTargetSelectionStarted;
        public System.Action OnTargetSelectionCancelled;

        #region Unity Events

        private void Awake()
        {
            InitializeSlots();
            InitializeButtons();
        }

        private void Start()
        {
            InitializeReferences();
            UpdateUI();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 슬롯에 카드 추가 시도
        /// </summary>
        public bool TryAddCard(Card card, int slotIndex = -1)
        {
            if (card == null) return false;

            // 특정 슬롯 지정
            if (slotIndex >= 0 && slotIndex < cardSlots.Length)
            {
                if (cardSlots[slotIndex].PlaceCard(card))
                {
                    UpdateCombination();
                    return true;
                }
                return false;
            }

            // 빈 슬롯 자동 찾기
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (!cardSlots[i].HasCard && cardSlots[i].PlaceCard(card))
                {
                    UpdateCombination();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 특정 슬롯에서 카드 제거
        /// </summary>
        public Card RemoveCard(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= cardSlots.Length)
                return null;

            Card removedCard = cardSlots[slotIndex].RemoveCard();
            if (removedCard != null)
            {
                UpdateCombination();
            }

            return removedCard;
        }

        /// <summary>
        /// 모든 슬롯 클리어
        /// </summary>
        public void ClearAllSlots()
        {
            foreach (var slot in cardSlots)
            {
                slot.RemoveCard();
            }

            UpdateCombination();
            OnCombinationCleared?.Invoke();
        }

        /// <summary>
        /// 조합 사용
        /// </summary>
        public void UseCombination()
        {
            if (!CanUseCombination()) return;

            var combination = GetCurrentCombination();
            if (combination.Count == 0) return;

            // 대상 지정이 필요한 카드인지 확인
            if (RequiresTarget(combination))
            {
                StartTargetSelection();
            }
            else
            {
                ExecuteCombination(null);
            }
        }

        /// <summary>
        /// 대상 선택 모드 시작
        /// </summary>
        public void StartTargetSelection()
        {
            isTargetSelectionMode = true;

            if (targetSelectionPanel != null)
                targetSelectionPanel.SetActive(true);

            if (targetInstructionText != null)
                targetInstructionText.text = "공격할 대상을 선택하세요 (Tab키로 변경, ESC로 취소)";

            OnTargetSelectionStarted?.Invoke();
        }

        /// <summary>
        /// 대상 선택 모드 취소
        /// </summary>
        public void CancelTargetSelection()
        {
            isTargetSelectionMode = false;

            if (targetSelectionPanel != null)
                targetSelectionPanel.SetActive(false);

            OnTargetSelectionCancelled?.Invoke();
        }

        /// <summary>
        /// 대상과 함께 조합 실행
        /// </summary>
        public void ExecuteCombination(GameObject target)
        {
            if (!CanUseCombination()) return;

            var combination = GetCurrentCombination();
            if (combination.Count == 0) return;

            // TurnManager를 통한 카드 사용
            var turnManager = TurnManager.Instance;
            if (turnManager != null && turnManager.IsPlayerTurnActive)
            {
                // 대상 처리
                List<Maglin.Enemy.Enemy> targets = null;
                if (target != null)
                {
                    var enemy = target.GetComponent<Maglin.Enemy.Enemy>();
                    if (enemy != null)
                    {
                        targets = new List<Maglin.Enemy.Enemy> { enemy };
                    }
                }

                // 조합된 카드 중 첫 번째 카드로 대표 사용 (실제로는 조합 결과 카드를 사용해야 함)
                if (combination.Count > 0)
                {
                    var representativeCard = combination[0];
                    if (turnManager.UseCard(representativeCard, targets))
                    {
                        OnCombinationUsed?.Invoke(representativeCard);
                        ClearAllSlots();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[CardCombinationUI] 플레이어 턴이 아니거나 TurnManager가 없습니다.");
            }

            // 대상 선택 모드 종료
            if (isTargetSelectionMode)
                CancelTargetSelection();
        }

        /// <summary>
        /// 조합 가능 여부 확인
        /// </summary>
        public bool CanUseCombination()
        {
            return isReadyToUse && hasValidCombination && !isTargetSelectionMode;
        }

        /// <summary>
        /// 슬롯 활성화/비활성화
        /// </summary>
        public void SetSlotsEnabled(bool enabled)
        {
            foreach (var slot in cardSlots)
            {
                slot.SetAcceptCard(enabled);
            }
        }

        #endregion

        #region Properties

        public List<Card> SlottedCards => new List<Card>(slottedCards);
        public bool HasCards => slottedCards.Count > 0;
        public bool IsTargetSelectionMode => isTargetSelectionMode;
        public Card PreviewedResult => previewedComboResult;

        #endregion

        #region Private Methods

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        private void InitializeSlots()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null)
                {
                    int index = i; // 클로저를 위한 로컬 변수
                    cardSlots[i].OnCardPlaced += (slot, card) => OnSlotCardPlaced(index, card);
                    cardSlots[i].OnCardRemoved += (slot) => OnSlotCardRemoved(index);
                }
            }
        }

        /// <summary>
        /// 버튼 초기화
        /// </summary>
        private void InitializeButtons()
        {
            if (useButton != null)
                useButton.onClick.AddListener(UseCombination);

            if (clearButton != null)
                clearButton.onClick.AddListener(ClearAllSlots);
        }

        /// <summary>
        /// 참조 초기화
        /// </summary>
        private void InitializeReferences()
        {
            comboManager = ComboManager.Instance;
            battleManager = BattleManager.Instance;
            playerManager = PlayerManager.Instance;
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // 필요시 추가
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // 필요시 추가
        }

        /// <summary>
        /// 조합 업데이트
        /// </summary>
        private void UpdateCombination()
        {
            // 현재 슬롯된 카드들 수집
            slottedCards.Clear();
            foreach (var slot in cardSlots)
            {
                if (slot.HasCard)
                    slottedCards.Add(slot.PlacedCard);
            }

            // 조합 유효성 검사 및 미리보기 업데이트
            CheckCombinationValidity();
            UpdatePreview();
            UpdateUI();

            OnCombinationChanged?.Invoke(slottedCards);
        }

        /// <summary>
        /// 조합 유효성 검사
        /// </summary>
        private void CheckCombinationValidity()
        {
            hasValidCombination = false;
            previewedComboResult = null;
            isReadyToUse = false;

            if (slottedCards.Count == 0)
                return;

            // 조합 가능성 확인
            if (comboManager != null)
            {
                if (slottedCards.Count == 1)
                {
                    // 단일 카드 사용
                    hasValidCombination = true;
                    previewedComboResult = slottedCards[0];
                }
                else if (slottedCards.Count >= 2)
                {
                    // 조합 확인
                    var combination = CardCombination.CreateFromCards(slottedCards.ToArray());
                    var result = comboManager.AttemptCombination(combination);

                    if (result != null)
                    {
                        hasValidCombination = true;
                        previewedComboResult = new Card(result);
                    }
                }

                // 마나 체크
                if (hasValidCombination && previewedComboResult != null && playerManager != null)
                {
                    int manaCost = previewedComboResult.CurrentManaCost;
                    isReadyToUse = playerManager.CurrentMana >= manaCost;
                }
            }
        }

        /// <summary>
        /// 미리보기 업데이트
        /// </summary>
        private void UpdatePreview()
        {
            if (previewPanel == null) return;

            bool showPreview = hasValidCombination && previewedComboResult != null;
            previewPanel.SetActive(showPreview);

            if (showPreview)
            {
                var cardData = previewedComboResult.CardData;

                if (previewCardImage != null && cardData.Image != null)
                    previewCardImage.sprite = cardData.Image;

                if (previewCardName != null)
                    previewCardName.text = cardData.CardName;

                if (previewDescription != null)
                    previewDescription.text = cardData.Description;
            }
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            // 사용 버튼 상태 업데이트
            if (useButton != null)
            {
                useButton.interactable = CanUseCombination();

                if (useButtonImage != null)
                {
                    Color targetColor = disabledButtonColor;
                    if (isReadyToUse)
                        targetColor = enabledButtonColor;
                    else if (hasValidCombination)
                        targetColor = invalidCombinationColor;

                    useButtonImage.color = targetColor;
                }

                if (useButtonText != null)
                {
                    if (slottedCards.Count == 0)
                        useButtonText.text = "카드 선택";
                    else if (!hasValidCombination)
                        useButtonText.text = "잘못된 조합";
                    else if (!isReadyToUse)
                        useButtonText.text = "마나 부족";
                    else
                        useButtonText.text = "사용";
                }
            }

            // 클리어 버튼 상태 업데이트
            if (clearButton != null)
            {
                clearButton.interactable = slottedCards.Count > 0;
            }
        }

        /// <summary>
        /// 현재 조합 가져오기
        /// </summary>
        private List<Card> GetCurrentCombination()
        {
            return new List<Card>(slottedCards);
        }

        /// <summary>
        /// 대상 지정이 필요한지 확인
        /// </summary>
        private bool RequiresTarget(List<Card> cards)
        {
            if (cards.Count == 0) return false;

            // 조합의 결과 카드 확인
            if (previewedComboResult != null)
            {
                return previewedComboResult.CardData.Target == TargetType.SingleEnemy ||
                       previewedComboResult.CardData.Target == TargetType.AllEnemies ||
                       previewedComboResult.CardData.Target == TargetType.TargetFrontStrip ||
                       previewedComboResult.CardData.Target == TargetType.TargetBackStrip ||
                       previewedComboResult.CardData.Target == TargetType.TargetCenteredRange;
            }

            return false;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 슬롯에 카드 배치 이벤트
        /// </summary>
        private void OnSlotCardPlaced(int slotIndex, Card card)
        {
            UpdateCombination();
        }

        /// <summary>
        /// 슬롯에서 카드 제거 이벤트
        /// </summary>
        private void OnSlotCardRemoved(int slotIndex)
        {
            UpdateCombination();
        }

        #endregion

        #region Input Handling

        private void Update()
        {
            if (isTargetSelectionMode)
            {
                HandleTargetSelectionInput();
            }
        }

        /// <summary>
        /// 대상 선택 모드 입력 처리
        /// </summary>
        private void HandleTargetSelectionInput()
        {
            // ESC키로 대상 선택 취소
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelTargetSelection();
            }

            // Tab키로 대상 변경 (추후 구현)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // 대상 순환 로직 구현
                CycleTargets();
            }
        }

        /// <summary>
        /// 대상 순환 (추후 구현)
        /// </summary>
        private void CycleTargets()
        {
            // 전투 시스템과 연동하여 대상 순환 구현
            // battleManager?.CycleTargets();
        }

        #endregion
    }
}