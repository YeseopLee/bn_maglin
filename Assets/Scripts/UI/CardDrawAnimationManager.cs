using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Maglin.Cards;
using Maglin.Battle;
using TMPro;

namespace Maglin.UI
{
    /// <summary>
    /// 카드 드로우 애니메이션 관리
    /// DOTween을 사용하여 카드가 덱에서 손으로 드로우되는 애니메이션 처리
    /// </summary>
    public class CardDrawAnimationManager : MonoBehaviour
    {
        [Header("애니메이션 설정")]
        [SerializeField] private float cardDrawDuration = 0.5f;
        [SerializeField] private float cardDrawInterval = 0.2f;
        [SerializeField] private Ease cardDrawEase = Ease.OutBack;
        [SerializeField] private float cardFlipDuration = 0.3f;
        [SerializeField] private Ease cardFlipEase = Ease.InOutQuad;
        
        [Header("위치 설정")]
        [SerializeField] private Transform deckPosition;
        [SerializeField] private Transform handArea;
        [SerializeField] private Vector3 deckStartOffset = new Vector3(0, 200f, 0);
        [SerializeField] private float handCardSpacing = 120f;
        
        [Header("효과 설정")]
        [SerializeField] private bool enableCardGlow = true;
        [SerializeField] private bool enableDrawSound = true;
        [SerializeField] private bool enableParticleEffect = false;
        [SerializeField] private GameObject cardDrawParticlePrefab;
        
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        
        public static CardDrawAnimationManager Instance { get; private set; }
        
        // 이벤트
        public System.Action<GameObject> OnCardDrawStarted;
        public System.Action<GameObject> OnCardDrawCompleted;
        public System.Action OnAllCardsDrawn;
        
        private bool isDrawing = false;
        private List<GameObject> activeDrawTweens = new List<GameObject>();
        private Canvas parentCanvas;
        private HorizontalLayoutGroup handLayoutGroup;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("CardDrawAnimationManager: 싱글톤 인스턴스 설정됨");
                InitializeAnimationManager();
            }
            else
            {
                Debug.Log("CardDrawAnimationManager: 중복 인스턴스 감지, 제거됨");
                Destroy(gameObject);
            }
        }
        
        private void InitializeAnimationManager()
        {
            Debug.Log("CardDrawAnimationManager: 초기화 시작");
            
            parentCanvas = GetComponentInParent<Canvas>();
            Debug.Log($"CardDrawAnimationManager: Canvas 발견 - {(parentCanvas != null ? parentCanvas.name : "없음")}");
            
            // 덱 위치가 설정되지 않았다면 자동으로 찾기
            if (deckPosition == null)
            {
                deckPosition = GameObject.Find("DeckArea")?.transform;
                Debug.Log($"CardDrawAnimationManager: DeckArea 자동 탐지 - {(deckPosition != null ? deckPosition.name : "없음")}");
            }
            
            // 손 영역이 설정되지 않았다면 자동으로 찾기
            if (handArea == null)
            {
                handArea = GameObject.Find("HandArea")?.transform;
                if (handArea == null)
                {
                    handArea = GameObject.Find("Hand")?.transform;
                    if (handArea == null)
                    {
                        handArea = GameObject.Find("PlayerHand")?.transform;
                    }
                }
                Debug.Log($"CardDrawAnimationManager: HandArea 자동 탐지 - {(handArea != null ? handArea.name : "없음")}");
            }
            
            // HorizontalLayoutGroup 참조 찾기
            if (handArea != null)
            {
                handLayoutGroup = handArea.GetComponent<HorizontalLayoutGroup>();
                Debug.Log($"CardDrawAnimationManager: HorizontalLayoutGroup 발견 - {(handLayoutGroup != null ? "예" : "없음")}");
            }
            
            Debug.Log($"CardDrawAnimationManager 초기화 완료");
            Debug.Log($"- DeckPosition: {(deckPosition != null ? deckPosition.name : "없음")}");
            Debug.Log($"- HandArea: {(handArea != null ? handArea.name : "없음")}");
            Debug.Log($"- Instance 설정됨: {(Instance != null ? "예" : "아니오")}");
        }
        
        /// <summary>
        /// 여러 카드를 순차적으로 드로우하는 애니메이션
        /// </summary>
        /// <param name="cardObjects">드로우할 카드 오브젝트들</param>
        public async Task DrawCardsSequentially(List<GameObject> cardObjects)
        {
            if (isDrawing)
            {
                Debug.LogWarning("CardDrawAnimationManager: 이미 카드 드로우 중입니다.");
                return;
            }
            
            if (cardObjects == null || cardObjects.Count == 0)
            {
                Debug.LogWarning("CardDrawAnimationManager: 드로우할 카드가 없습니다.");
                return;
            }
            
            isDrawing = true;
            activeDrawTweens.Clear();
            
            if (debugMode)
                Debug.Log($"CardDrawAnimationManager: {cardObjects.Count}장의 카드 드로우 시작");
            
            // HorizontalLayoutGroup 비활성화 (애니메이션 중 자동 레이아웃 방지)
            bool layoutWasEnabled = false;
            if (handLayoutGroup != null)
            {
                layoutWasEnabled = handLayoutGroup.enabled;
                handLayoutGroup.enabled = false;
                if (debugMode)
                    Debug.Log("CardDrawAnimationManager: HorizontalLayoutGroup 비활성화 (애니메이션 시작)");
            }
            
            // 현재 손패의 총 카드 수를 고려하여 최종 위치 미리 계산
            int currentHandCount = GetCurrentHandCount();
            int totalFinalCards = currentHandCount + cardObjects.Count;
            
            if (debugMode)
                Debug.Log($"CardDrawAnimationManager: 현재 손패 {currentHandCount}장 + 새로 드로우 {cardObjects.Count}장 = 최종 {totalFinalCards}장");
            
            try
            {
                for (int i = 0; i < cardObjects.Count; i++)
                {
                    if (cardObjects[i] != null)
                    {
                        // 각 카드를 순차적으로 드로우 (최종 위치 기준으로)
                        int finalHandIndex = currentHandCount + i;
                        await DrawSingleCard(cardObjects[i], finalHandIndex, totalFinalCards);
                        
                        // 다음 카드까지 대기
                        if (i < cardObjects.Count - 1)
                        {
                            await Task.Delay((int)(cardDrawInterval * 1000));
                        }
                    }
                }
                
                // 모든 카드가 도착한 후 카드 플립 애니메이션 (뒷면 → 앞면)
                if (debugMode)
                    Debug.Log("CardDrawAnimationManager: 모든 카드 도착 완료, 플립 애니메이션 시작");
                
                await FlipAllCardsToFront(cardObjects);
                
                // 애니메이션 완료 후 BattleUIManager에 카드 UI들 등록
                RegisterCardsWithBattleUIManager(cardObjects);
                
                OnAllCardsDrawn?.Invoke();
                
                if (debugMode)
                    Debug.Log("CardDrawAnimationManager: 모든 카드 드로우 및 플립 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CardDrawAnimationManager: 카드 드로우 중 오류 발생 - {e.Message}");
            }
            finally
            {
                // HorizontalLayoutGroup 다시 활성화 (애니메이션 완료 후)
                if (handLayoutGroup != null && layoutWasEnabled)
                {
                    handLayoutGroup.enabled = true;
                    if (debugMode)
                        Debug.Log("CardDrawAnimationManager: HorizontalLayoutGroup 재활성화 (애니메이션 완료)");
                }
                
                isDrawing = false;
                activeDrawTweens.Clear();
            }
        }
        
        /// <summary>
        /// 현재 손패의 카드 수를 가져옴 (애니메이션 중인 카드 제외)
        /// </summary>
        private int GetCurrentHandCount()
        {
            if (handArea == null) return 0;
            
            // HandArea의 자식 중 활성화된 카드 UI 개수를 셈
            int count = 0;
            for (int i = 0; i < handArea.childCount; i++)
            {
                var child = handArea.GetChild(i);
                if (child.gameObject.activeInHierarchy && child.GetComponent<CardUI>() != null)
                {
                    count++;
                }
            }
            
            if (debugMode)
                Debug.Log($"CardDrawAnimationManager: 현재 활성화된 손패 카드 수 = {count}");
                
            return count;
        }
        
        /// <summary>
        /// 모든 카드를 앞면으로 뒤집는 애니메이션
        /// </summary>
        private async Task FlipAllCardsToFront(List<GameObject> cardObjects)
        {
            var flipTasks = new List<Task>();
            
            for (int i = 0; i < cardObjects.Count; i++)
            {
                if (cardObjects[i] != null)
                {
                    var cardRect = cardObjects[i].GetComponent<RectTransform>();
                    if (cardRect != null)
                    {
                        // 각 카드마다 약간의 지연을 주어 순차적으로 플립
                        int delayMs = i * 100; // 100ms씩 지연
                        flipTasks.Add(FlipSingleCardToFront(cardRect, delayMs));
                    }
                }
            }
            
            // 모든 플립 애니메이션이 완료될 때까지 대기
            await Task.WhenAll(flipTasks);
        }
        
        /// <summary>
        /// 단일 카드를 앞면으로 뒤집는 애니메이션
        /// </summary>
        private async Task FlipSingleCardToFront(RectTransform cardRect, int delayMs)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            
            // Y축 180도에서 0도로 회전 (뒷면 → 앞면)
            var flipTween = cardRect.DORotate(Vector3.zero, cardFlipDuration)
                .SetEase(cardFlipEase);
                
            // 플립 중간에 카드 이미지 업데이트
            bool imageFlipped = false;
            flipTween.OnUpdate(() =>
            {
                if (!imageFlipped && cardRect.rotation.eulerAngles.y > 90f && cardRect.rotation.eulerAngles.y < 270f)
                {
                    // 90도를 넘어갔을 때 카드 이미지 업데이트 (한 번만)
                    FlipCardImage(cardRect.gameObject);
                    imageFlipped = true;
                }
            });
            
            // 플립 완료 후 상호작용 활성화
            flipTween.OnComplete(() =>
            {
                EnableCardInteraction(cardRect.gameObject);
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: 카드 플립 완료 및 상호작용 활성화 - {cardRect.gameObject.name}");
            });
            
            await flipTween.AsyncWaitForCompletion();
        }
        
        /// <summary>
        /// 단일 카드 드로우 애니메이션
        /// </summary>
        /// <param name="cardObject">드로우할 카드 오브젝트</param>
        /// <param name="handIndex">손에서의 인덱스</param>
        /// <param name="totalCardCount">최종 총 카드 개수</param>
        private async Task DrawSingleCard(GameObject cardObject, int handIndex, int totalCardCount = -1)
        {
            if (cardObject == null) return;
            
            OnCardDrawStarted?.Invoke(cardObject);
            
            if (debugMode)
                Debug.Log($"CardDrawAnimationManager: 카드 드로우 시작 - {cardObject.name} (인덱스: {handIndex})");
            
            // 카드 활성화 (애니메이션 시작 전)
            cardObject.SetActive(true);
            
            // 카드 활성화 후 카드 데이터 확인 및 설정
            var cardUI = cardObject.GetComponent<CardUI>();
            var cardUIData = cardObject.GetComponent<CardUIData>();
            
            if (debugMode)
            {
                string cardDataInfo = "카드 데이터 없음";
                if (cardUI?.AssociatedCard != null)
                    cardDataInfo = $"CardUI: {cardUI.AssociatedCard.CardName}";
                else if (cardUIData?.CardInstance != null)
                    cardDataInfo = $"CardUIData: {cardUIData.CardInstance.CardName}";
                
                Debug.Log($"CardDrawAnimationManager: 카드 활성화 후 데이터 확인 - {cardDataInfo}");
            }
            
            // 카드 UI 즉시 업데이트 (활성화 직후)
            if (cardUI != null && cardUI.AssociatedCard != null)
            {
                cardUI.UpdateCardDisplay();
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: CardUI 즉시 업데이트 - {cardUI.AssociatedCard.CardName}");
            }
            else if (cardUIData?.CardInstance != null)
            {
                UpdateCardUIDirectly(cardObject, cardUIData.CardInstance);
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: CardUIData 직접 업데이트 - {cardUIData.CardInstance.CardName}");
            }
            
            // 상호작용 기능 활성화 확인
            EnableCardInteraction(cardObject);
            
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect == null)
            {
                Debug.LogWarning($"CardDrawAnimationManager: {cardObject.name}에 RectTransform이 없습니다.");
                return;
            }
            
            // 1. 시작 위치 설정 (덱 위치)
            Vector3 startPosition = GetDeckWorldPosition();
            cardRect.position = startPosition;
            cardRect.localScale = Vector3.one * 0.8f; // 덱에서 약간 작게 시작
            cardRect.rotation = Quaternion.Euler(0, 180, Random.Range(-5f, 5f)); // 뒤집힌 상태로 시작 (Y축 180도 회전)
            
            // 2. 목표 위치 계산 (손 영역)
            int finalCardCount = totalCardCount > 0 ? totalCardCount : 5; // 기본값 5장
            Vector3 targetPosition = GetHandPosition(handIndex, finalCardCount);
            
            activeDrawTweens.Add(cardObject);
            
            // 3. 카드 등장 애니메이션 시퀀스 생성
            Sequence drawSequence = DOTween.Sequence();
            
            // 3-1. 위치 이동 (덱에서 손패로)
            drawSequence.Append(cardRect.DOMove(targetPosition, cardDrawDuration)
                .SetEase(cardDrawEase));
            
            // 3-2. 크기 조정과 회전 정리 (이동과 동시 실행)
            drawSequence.Join(cardRect.DOScale(Vector3.one, cardDrawDuration * 0.8f)
                .SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DORotate(new Vector3(0, 180, 0), cardDrawDuration * 0.6f)
                .SetEase(Ease.OutQuad)); // 뒤집힌 상태 유지
            
            // 4. 사운드 효과 (선택적)
            if (enableDrawSound)
            {
                drawSequence.AppendCallback(() => PlayCardDrawSound());
            }
            
            // 5. 완료 콜백
            drawSequence.OnComplete(() => {
                OnCardDrawCompleted?.Invoke(cardObject);
                activeDrawTweens.Remove(cardObject);
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: 카드 이동 완료 - {cardObject.name} (뒷면 상태)");
            });
            
            // 6. 애니메이션 재생
            drawSequence.Play();
            
            // 7. 애니메이션 완료까지 대기
            await drawSequence.AsyncWaitForCompletion();
        }
        
        /// <summary>
        /// 카드 플립 애니메이션 생성
        /// </summary>
        private Tween CreateCardFlipAnimation(RectTransform cardRect)
        {
            Sequence flipSequence = DOTween.Sequence();
            
            // 1. Y축으로 90도 회전 (뒷면이 보이지 않게)
            flipSequence.Append(cardRect.DORotate(new Vector3(0, 90, 0), cardFlipDuration * 0.5f)
                .SetEase(cardFlipEase));
            
            // 2. 카드 이미지 교체 시점 (콜백)
            flipSequence.AppendCallback(() => {
                // 여기서 카드 이미지를 뒷면에서 앞면으로 교체
                FlipCardImage(cardRect.gameObject);
            });
            
            // 3. Y축으로 0도 회전 (앞면이 보이게)
            flipSequence.Append(cardRect.DORotate(Vector3.zero, cardFlipDuration * 0.5f)
                .SetEase(cardFlipEase));
            
            return flipSequence;
        }
        
                 /// <summary>
         /// 카드 이미지 플립 (뒷면 → 앞면)
         /// </summary>
         private void FlipCardImage(GameObject cardObject)
         {
             if (cardObject == null) return;
             
             if (debugMode)
                 Debug.Log($"CardDrawAnimationManager: 카드 이미지 플립 시작 - {cardObject.name}");
             
             // 카드 활성화 (혹시 비활성화 상태라면)
             if (!cardObject.activeInHierarchy)
             {
                 cardObject.SetActive(true);
                 if (debugMode)
                     Debug.Log($"CardDrawAnimationManager: 카드 활성화됨 - {cardObject.name}");
             }
             
             // CardUI 컴포넌트에서 카드 데이터 가져오기
             var cardUI = cardObject.GetComponent<CardUI>();
             if (cardUI != null)
             {
                 if (debugMode)
                     Debug.Log($"CardDrawAnimationManager: CardUI 발견 - {cardUI.AssociatedCard?.CardName ?? "카드 데이터 없음"}");
                 
                 // CardUI의 UpdateCardDisplay 메서드 호출하여 카드 표시 업데이트
                 cardUI.UpdateCardDisplay();
                 
                 if (debugMode)
                     Debug.Log($"CardDrawAnimationManager: 카드 이미지 플립 완료 - {cardObject.name}");
             }
             else
             {
                 // CardUI가 없는 경우 CardUIData에서 데이터 가져와서 직접 업데이트
                 var cardUIData = cardObject.GetComponent<CardUIData>();
                 if (cardUIData != null && cardUIData.CardInstance != null)
                 {
                     if (debugMode)
                         Debug.Log($"CardDrawAnimationManager: CardUIData에서 카드 데이터 발견 - {cardUIData.CardInstance.CardName}");
                     
                     UpdateCardUIDirectly(cardObject, cardUIData.CardInstance);
                 }
                 else
                 {
                     Debug.LogWarning($"CardDrawAnimationManager: {cardObject.name}에서 카드 데이터를 찾을 수 없음");
                 }
             }
         }
         
         /// <summary>
         /// 카드 UI를 직접 업데이트 (CardUI 컴포넌트가 없는 경우)
         /// </summary>
         private void UpdateCardUIDirectly(GameObject cardObject, Card card)
         {
             if (card?.CardData == null) return;
             
             var cardData = card.CardData;
             
             // 메인 이미지 업데이트
             var cardImage = cardObject.GetComponent<Image>();
             if (cardImage == null)
             {
                 cardImage = cardObject.GetComponentInChildren<Image>();
             }
             
             if (cardImage != null && cardData.Image != null)
             {
                 cardImage.sprite = cardData.Image;
                 if (debugMode)
                     Debug.Log($"CardDrawAnimationManager: 카드 이미지 직접 업데이트 - {cardData.CardName}");
             }
             
             // 텍스트 컴포넌트들 업데이트
             var textComponents = cardObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
             foreach (var text in textComponents)
             {
                 if (text.name.Contains("Name") || text.name.Contains("CardName"))
                 {
                     text.text = cardData.CardName;
                 }
                 else if (text.name.Contains("Description"))
                 {
                     text.text = cardData.Description;
                 }
                 else if (text.name.Contains("Damage"))
                 {
                     text.text = cardData.BaseDamage.ToString();
                 }
                 else if (text.name.Contains("Mana") || text.name.Contains("Cost"))
                 {
                     text.text = cardData.ManaCost.ToString();
                 }
             }
             
             // Unity Text 컴포넌트들도 업데이트
             var unityTextComponents = cardObject.GetComponentsInChildren<UnityEngine.UI.Text>();
             foreach (var text in unityTextComponents)
             {
                 if (text.name.Contains("Name") || text.name.Contains("CardName"))
                 {
                     text.text = cardData.CardName;
                 }
                 else if (text.name.Contains("Description"))
                 {
                     text.text = cardData.Description;
                 }
                 else if (text.name.Contains("Damage"))
                 {
                     text.text = cardData.BaseDamage.ToString();
                 }
                 else if (text.name.Contains("Mana") || text.name.Contains("Cost"))
                 {
                     text.text = cardData.ManaCost.ToString();
                 }
             }
             
             if (debugMode)
                 Debug.Log($"CardDrawAnimationManager: 카드 UI 직접 업데이트 완료 - {cardData.CardName}");
         }
        
        /// <summary>
        /// 카드의 상호작용 기능 활성화
        /// </summary>
        private void EnableCardInteraction(GameObject cardObject)
        {
            if (cardObject == null) return;
            
            // CanvasGroup 설정 (상호작용 가능)
            var canvasGroup = cardObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: CanvasGroup 상호작용 활성화 - {cardObject.name}");
            }
            
            // Button 컴포넌트 활성화
            var button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = true;
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: Button 상호작용 활성화 - {cardObject.name}");
            }
            
            // CardDraggable 컴포넌트 활성화 확인
            var cardDraggable = cardObject.GetComponent<CardDraggable>();
            if (cardDraggable != null)
            {
                cardDraggable.enabled = true;
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: CardDraggable 활성화 - {cardObject.name}");
            }
            
            // CardUI 컴포넌트 상호작용 설정
            var cardUI = cardObject.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.SetInteractable(true);
                
                if (debugMode)
                    Debug.Log($"CardDrawAnimationManager: CardUI 상호작용 활성화 - {cardObject.name}");
            }
        }
        
        /// <summary>
        /// 카드 글로우 효과 시작
        /// </summary>
        private void StartCardGlowEffect(GameObject cardObject)
        {
            var cardImage = cardObject.GetComponent<Image>();
            if (cardImage != null)
            {
                // 글로우 효과: 색상을 밝게 만들었다가 원래대로
                Color originalColor = cardImage.color;
                Color glowColor = new Color(originalColor.r * 1.3f, originalColor.g * 1.3f, originalColor.b * 1.3f, originalColor.a);
                
                cardImage.DOColor(glowColor, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        cardImage.DOColor(originalColor, 0.3f).SetEase(Ease.InQuad);
                    });
            }
        }
        
        /// <summary>
        /// 카드 드로우 파티클 효과 생성
        /// </summary>
        private void CreateCardDrawParticle(Vector3 position)
        {
            if (cardDrawParticlePrefab != null)
            {
                GameObject particle = Instantiate(cardDrawParticlePrefab, position, Quaternion.identity);
                
                // 2초 후 파티클 제거
                DOVirtual.DelayedCall(2f, () => {
                    if (particle != null) Destroy(particle);
                });
            }
        }
        
        /// <summary>
        /// 카드 드로우 사운드 재생
        /// </summary>
        private void PlayCardDrawSound()
        {
            // AudioManager를 통해 사운드 재생
            if (Maglin.Audio.AudioManager.Instance != null)
            {
                // AudioManager.Instance.PlaySFX("CardDraw");
            }
        }
        
        /// <summary>
        /// 덱 월드 위치 가져오기
        /// </summary>
        private Vector3 GetDeckWorldPosition()
        {
            if (deckPosition != null)
            {
                return deckPosition.position + deckStartOffset;
            }
            
            // 덱 위치가 없으면 화면 오른쪽 위
            return new Vector3(Screen.width + 100f, Screen.height * 0.8f, 0);
        }
        
        /// <summary>
        /// 손 영역에서의 카드 위치 계산
        /// </summary>
        private Vector3 GetHandPosition(int cardIndex, int totalCardCount = 5)
        {
            if (handArea != null)
            {
                // 손 영역의 중앙을 기준으로 카드들을 좌우로 배치
                RectTransform handRect = handArea.GetComponent<RectTransform>();
                if (handRect != null)
                {
                    Vector3 centerPosition = handArea.position;
                    // 총 카드 개수를 고려하여 중앙 정렬
                    float centerIndex = (totalCardCount - 1) * 0.5f;
                    float offsetX = (cardIndex - centerIndex) * handCardSpacing;
                    
                    if (debugMode)
                        Debug.Log($"CardDrawAnimationManager: 카드 {cardIndex}번째 위치 계산 - 총 {totalCardCount}장, 중앙: {centerIndex}, 오프셋: {offsetX}");
                    
                    return centerPosition + new Vector3(offsetX, 0, 0);
                }
            }
            
            // 손 영역이 없으면 화면 하단 중앙
            float screenCenterX = Screen.width * 0.5f;
            float screenBottomY = Screen.height * 0.2f;
            float screenCenterIndex = (totalCardCount - 1) * 0.5f;
            float cardOffsetX = (cardIndex - screenCenterIndex) * handCardSpacing;
            
            return new Vector3(screenCenterX + cardOffsetX, screenBottomY, 0);
        }
        
        /// <summary>
        /// 현재 드로우 중인지 확인
        /// </summary>
        public bool IsDrawing => isDrawing;
        
        /// <summary>
        /// 모든 드로우 애니메이션 중단
        /// </summary>
        public void StopAllDrawAnimations()
        {
            foreach (var cardObject in activeDrawTweens.ToArray())
            {
                if (cardObject != null)
                {
                    cardObject.transform.DOKill();
                }
            }
            
            activeDrawTweens.Clear();
            isDrawing = false;
            
            if (debugMode)
                Debug.Log("CardDrawAnimationManager: 모든 드로우 애니메이션 중단");
        }
        
        /// <summary>
        /// 애니메이션 설정 업데이트
        /// </summary>
        public void UpdateAnimationSettings(float duration, float interval, Ease ease)
        {
            cardDrawDuration = duration;
            cardDrawInterval = interval;
            cardDrawEase = ease;
            
            if (debugMode)
                Debug.Log($"CardDrawAnimationManager: 애니메이션 설정 업데이트 - Duration: {duration}, Interval: {interval}, Ease: {ease}");
        }
        
        private void OnDestroy()
        {
            // 모든 DOTween 애니메이션 정리
            transform.DOKill();
            StopAllDrawAnimations();
        }

        /// <summary>
        /// BattleUIManager에 드로우된 카드 UI들을 등록합니다.
        /// </summary>
        private void RegisterCardsWithBattleUIManager(List<GameObject> cardObjects)
        {
            if (BattleUIManager.Instance != null)
            {
                foreach (var cardObject in cardObjects)
                {
                    if (cardObject != null)
                    {
                        var cardUI = cardObject.GetComponent<CardUI>();
                        if (cardUI != null)
                        {
                            BattleUIManager.Instance.RegisterCardUI(cardUI);
                            if (debugMode)
                                Debug.Log($"CardDrawAnimationManager: 카드 UI 등록 - {cardUI.AssociatedCard?.CardName ?? "카드 데이터 없음"}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("CardDrawAnimationManager: BattleUIManager가 없어 카드 UI를 등록할 수 없습니다.");
            }
        }
    }
} 