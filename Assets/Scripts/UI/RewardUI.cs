using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Maglin.Battle;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.UI
{
    /// <summary>
    /// 개별 보상 아이템 UI
    /// </summary>
    [System.Serializable]
    public class RewardItemUI
    {
        [Header("UI 컴포넌트")]
        public GameObject rewardPanel;              // 보상 패널
        public Button selectButton;                 // 선택 버튼
        public Image rewardIcon;                    // 보상 아이콘
        public TextMeshProUGUI rewardNameText;      // 보상 이름
        public TextMeshProUGUI rewardDescText;      // 보상 설명
        public TextMeshProUGUI rewardAmountText;    // 수량 텍스트 (골드용)

        [Header("시각적 효과")]
        public Image backgroundImage;               // 배경 이미지
        public Image borderImage;                   // 테두리 이미지
        public GameObject hoverEffect;              // 호버 효과
        public GameObject selectedEffect;           // 선택 효과

        // 현재 보상 데이터
        public RewardItem currentReward;
    }

    /// <summary>
    /// 전투 보상 선택 UI를 관리하는 클래스
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("메인 UI")]
        [SerializeField] private GameObject rewardPanel;            // 전체 보상 패널
        [SerializeField] private GameObject rewardBackground;       // 배경 패널
        [SerializeField] private TextMeshProUGUI titleText;         // 제목 텍스트
        [SerializeField] private TextMeshProUGUI instructionText;   // 안내 텍스트

        [Header("보상 아이템 UI")]
        [SerializeField] private Transform rewardContainer;         // 보상 아이템 컨테이너
        [SerializeField] private GameObject rewardItemPrefab;       // 보상 아이템 프리팹
        [SerializeField] private RewardItemUI[] rewardSlots;        // 보상 슬롯 배열

        [Header("버튼")]
        [SerializeField] private Button skipButton;                 // 건너뛰기 버튼
        [SerializeField] private Button confirmButton;              // 확정 버튼

        [Header("색상 설정")]
        [SerializeField] private Color goldColor = Color.yellow;
        [SerializeField] private Color cardColor = Color.cyan;
        [SerializeField] private Color relicColor = Color.magenta;
        [SerializeField] private Color expColor = Color.green;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color hoverColor = Color.gray;

        [Header("스프라이트 설정")]
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite cardIcon;
        [SerializeField] private Sprite relicIcon;
        [SerializeField] private Sprite expIcon;

        [Header("애니메이션 설정")]
        [SerializeField] private float showDuration = 0.5f;
        [SerializeField] private float hideDuration = 0.3f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("디버그 설정")]
        [SerializeField] private bool debugMode = false;

        // 현재 상태
        private RewardItem[] currentRewards;
        private RewardItem selectedReward;
        private bool isShowing = false;
        private List<RewardItemUI> activeRewardUIs = new List<RewardItemUI>();

        private void Awake()
        {
            // UI 초기화
            InitializeUI();
        }

        private void OnEnable()
        {
            // RewardManager 이벤트 구독
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnBattleRewardsGenerated += OnBattleRewardsGenerated;
                RewardManager.Instance.OnRewardSelected += OnRewardSelected;
                RewardManager.Instance.OnRewardsCompleted += OnRewardsCompleted;
            }
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnBattleRewardsGenerated -= OnBattleRewardsGenerated;
                RewardManager.Instance.OnRewardSelected -= OnRewardSelected;
                RewardManager.Instance.OnRewardsCompleted -= OnRewardsCompleted;
            }
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 처음에는 숨김
            if (rewardPanel != null)
                rewardPanel.SetActive(false);

            // 버튼 이벤트 연결
            if (skipButton != null)
                skipButton.onClick.AddListener(OnSkipButtonClicked);

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
                confirmButton.interactable = false;
            }

            // 제목 설정
            if (titleText != null)
                titleText.text = "전투 보상";

            // 안내 텍스트 설정
            if (instructionText != null)
                instructionText.text = "보상을 선택하세요";
        }

        /// <summary>
        /// 전투 보상이 생성될 때 호출
        /// </summary>
        private void OnBattleRewardsGenerated(BattleRewardData rewardData)
        {
            if (rewardData == null) return;

            // 카드와 유물 보상을 합쳐서 표시 (골드는 자동 지급되므로 제외)
            var allRewards = new List<RewardItem>();

            if (rewardData.cardChoices != null)
                allRewards.AddRange(rewardData.cardChoices);

            if (rewardData.relicChoices != null)
                allRewards.AddRange(rewardData.relicChoices);

            currentRewards = allRewards.ToArray();
            selectedReward = null;

            if (debugMode)
                Debug.Log($"[RewardUI] 보상 {currentRewards.Length}개 표시 (골드 {rewardData.goldAmount} 자동 지급됨)");

            ShowRewards(currentRewards);
        }

        /// <summary>
        /// 보상이 선택될 때 호출
        /// </summary>
        private void OnRewardSelected(RewardItem reward)
        {
            if (debugMode)
                Debug.Log($"[RewardUI] 보상 선택됨: {reward.rewardType}");

            // UI 업데이트 (선택 효과 등)
            UpdateSelectionUI(reward);
        }

        /// <summary>
        /// 보상 선택이 완료될 때 호출
        /// </summary>
        private void OnRewardsCompleted()
        {
            if (debugMode)
                Debug.Log("[RewardUI] 보상 선택 완료");

            HideRewards();
        }

        /// <summary>
        /// 보상 UI 표시
        /// </summary>
        public void ShowRewards(RewardItem[] rewards)
        {
            if (isShowing) return;

            isShowing = true;

            // 패널 활성화
            if (rewardPanel != null)
                rewardPanel.SetActive(true);

            // 기존 보상 UI 정리
            ClearRewardUIs();

            // 새 보상 UI 생성
            CreateRewardUIs(rewards);

            // 애니메이션 시작
            StartCoroutine(ShowAnimation());
        }

        /// <summary>
        /// 보상 UI 숨김
        /// </summary>
        public void HideRewards()
        {
            if (!isShowing) return;

            StartCoroutine(HideAnimation());
        }

        /// <summary>
        /// 보상 UI 생성
        /// </summary>
        private void CreateRewardUIs(RewardItem[] rewards)
        {
            if (rewards == null || rewardContainer == null) return;

            for (int i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                var rewardUI = CreateRewardItemUI(reward, i);

                if (rewardUI != null)
                {
                    activeRewardUIs.Add(rewardUI);
                }
            }
        }

        /// <summary>
        /// 개별 보상 아이템 UI 생성
        /// </summary>
        private RewardItemUI CreateRewardItemUI(RewardItem reward, int index)
        {
            if (rewardItemPrefab == null || rewardContainer == null)
            {
                // 프리팹이 없으면 기존 슬롯 사용
                if (rewardSlots != null && index < rewardSlots.Length)
                {
                    var slot = rewardSlots[index];
                    SetupRewardItemUI(slot, reward);
                    return slot;
                }
                return null;
            }

            // 프리팹에서 생성
            GameObject rewardObj = Instantiate(rewardItemPrefab, rewardContainer);
            var rewardUI = new RewardItemUI();

            // 컴포넌트 찾기
            rewardUI.rewardPanel = rewardObj;
            rewardUI.selectButton = rewardObj.GetComponent<Button>();
            rewardUI.rewardIcon = rewardObj.transform.Find("Icon")?.GetComponent<Image>();
            rewardUI.rewardNameText = rewardObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            rewardUI.rewardDescText = rewardObj.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            rewardUI.rewardAmountText = rewardObj.transform.Find("AmountText")?.GetComponent<TextMeshProUGUI>();

            SetupRewardItemUI(rewardUI, reward);
            return rewardUI;
        }

        /// <summary>
        /// 보상 아이템 UI 설정
        /// </summary>
        private void SetupRewardItemUI(RewardItemUI rewardUI, RewardItem reward)
        {
            if (rewardUI == null || reward == null) return;

            rewardUI.currentReward = reward;

            // 아이콘 설정
            SetRewardIcon(rewardUI, reward);

            // 텍스트 설정
            SetRewardTexts(rewardUI, reward);

            // 색상 설정
            SetRewardColors(rewardUI, reward);

            // 버튼 이벤트 연결
            if (rewardUI.selectButton != null)
            {
                rewardUI.selectButton.onClick.RemoveAllListeners();
                rewardUI.selectButton.onClick.AddListener(() => OnRewardItemClicked(reward));
            }

            // 패널 활성화
            if (rewardUI.rewardPanel != null)
                rewardUI.rewardPanel.SetActive(true);
        }

        /// <summary>
        /// 보상 아이콘 설정
        /// </summary>
        private void SetRewardIcon(RewardItemUI rewardUI, RewardItem reward)
        {
            if (rewardUI.rewardIcon == null) return;

            switch (reward.rewardType)
            {
                case RewardType.Card:
                    if (reward.cardReward != null && reward.cardReward.Image != null)
                        rewardUI.rewardIcon.sprite = reward.cardReward.Image;
                    else
                        rewardUI.rewardIcon.sprite = cardIcon;
                    break;

                case RewardType.Relic:
                    if (reward.relicReward != null && reward.relicReward.Image != null)
                        rewardUI.rewardIcon.sprite = reward.relicReward.Image;
                    else
                        rewardUI.rewardIcon.sprite = relicIcon;
                    break;
            }
        }

        /// <summary>
        /// 보상 텍스트 설정
        /// </summary>
        private void SetRewardTexts(RewardItemUI rewardUI, RewardItem reward)
        {
            // 이름 설정
            if (rewardUI.rewardNameText != null)
            {
                switch (reward.rewardType)
                {
                    case RewardType.Card:
                        rewardUI.rewardNameText.text = reward.cardReward?.CardName ?? "카드";
                        break;

                    case RewardType.Relic:
                        rewardUI.rewardNameText.text = reward.relicReward?.RelicName ?? "유물";
                        break;
                }
            }

            // 설명 설정
            if (rewardUI.rewardDescText != null)
            {
                switch (reward.rewardType)
                {
                    case RewardType.Card:
                        rewardUI.rewardDescText.text = reward.cardReward?.Description ?? "새로운 카드를 획득합니다";
                        break;

                    case RewardType.Relic:
                        rewardUI.rewardDescText.text = reward.relicReward?.Description ?? "새로운 유물을 획득합니다";
                        break;
                }
            }

            // 수량 설정 (카드와 유물은 수량이 없으므로 숨김)
            if (rewardUI.rewardAmountText != null)
            {
                rewardUI.rewardAmountText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 보상 색상 설정
        /// </summary>
        private void SetRewardColors(RewardItemUI rewardUI, RewardItem reward)
        {
            Color themeColor = GetRewardThemeColor(reward.rewardType);

            if (rewardUI.backgroundImage != null)
                rewardUI.backgroundImage.color = themeColor;

            if (rewardUI.borderImage != null)
                rewardUI.borderImage.color = themeColor;
        }

        /// <summary>
        /// 보상 타입별 테마 색상 반환
        /// </summary>
        private Color GetRewardThemeColor(RewardType rewardType)
        {
            return rewardType switch
            {
                RewardType.Card => cardColor,
                RewardType.Relic => relicColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// 보상 아이템 클릭 처리
        /// </summary>
        private void OnRewardItemClicked(RewardItem reward)
        {
            if (selectedReward == reward) return;

            selectedReward = reward;

            if (debugMode)
                Debug.Log($"[RewardUI] 보상 아이템 클릭: {reward.rewardType}");

            // 선택 UI 업데이트
            UpdateSelectionUI(reward);

            // 확정 버튼 활성화
            if (confirmButton != null)
                confirmButton.interactable = true;
        }

        /// <summary>
        /// 선택 UI 업데이트
        /// </summary>
        private void UpdateSelectionUI(RewardItem selectedReward)
        {
            foreach (var rewardUI in activeRewardUIs)
            {
                bool isSelected = rewardUI.currentReward == selectedReward;

                // 선택 효과 표시
                if (rewardUI.selectedEffect != null)
                    rewardUI.selectedEffect.SetActive(isSelected);

                // 색상 변경
                if (rewardUI.borderImage != null)
                {
                    Color color = isSelected ? selectedColor : GetRewardThemeColor(rewardUI.currentReward.rewardType);
                    rewardUI.borderImage.color = color;
                }
            }
        }

        /// <summary>
        /// 기존 보상 UI 정리
        /// </summary>
        private void ClearRewardUIs()
        {
            // 동적으로 생성된 UI들 삭제
            foreach (var rewardUI in activeRewardUIs)
            {
                if (rewardUI.rewardPanel != null && rewardUI.rewardPanel.transform.parent == rewardContainer)
                {
                    Destroy(rewardUI.rewardPanel);
                }
            }

            // 고정 슬롯들 비활성화
            if (rewardSlots != null)
            {
                foreach (var slot in rewardSlots)
                {
                    if (slot.rewardPanel != null)
                        slot.rewardPanel.SetActive(false);
                }
            }

            activeRewardUIs.Clear();
        }

        /// <summary>
        /// 건너뛰기 버튼 클릭
        /// </summary>
        private void OnSkipButtonClicked()
        {
            if (debugMode)
                Debug.Log("[RewardUI] 건너뛰기 버튼 클릭");

            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.SkipRewards();
            }
        }

        /// <summary>
        /// 확정 버튼 클릭
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            if (selectedReward == null)
            {
                Debug.LogWarning("[RewardUI] 선택된 보상이 없습니다.");
                return;
            }

            if (debugMode)
                Debug.Log($"[RewardUI] 확정 버튼 클릭: {selectedReward.rewardType}");

            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.SelectReward(selectedReward);
            }
        }

        /// <summary>
        /// 표시 애니메이션
        /// </summary>
        private System.Collections.IEnumerator ShowAnimation()
        {
            if (rewardBackground == null) yield break;

            float elapsed = 0f;
            Vector3 initialScale = Vector3.zero;
            Vector3 targetScale = Vector3.one;

            rewardBackground.transform.localScale = initialScale;

            while (elapsed < showDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / showDuration;
                float curveValue = showCurve.Evaluate(t);

                rewardBackground.transform.localScale = Vector3.Lerp(initialScale, targetScale, curveValue);

                yield return null;
            }

            rewardBackground.transform.localScale = targetScale;
        }

        /// <summary>
        /// 숨김 애니메이션
        /// </summary>
        private System.Collections.IEnumerator HideAnimation()
        {
            if (rewardBackground == null) yield break;

            float elapsed = 0f;
            Vector3 initialScale = rewardBackground.transform.localScale;
            Vector3 targetScale = Vector3.zero;

            while (elapsed < hideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hideDuration;

                rewardBackground.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);

                yield return null;
            }

            rewardBackground.transform.localScale = targetScale;

            // 패널 비활성화
            if (rewardPanel != null)
                rewardPanel.SetActive(false);

            isShowing = false;
            selectedReward = null;

            if (confirmButton != null)
                confirmButton.interactable = false;
        }

        /// <summary>
        /// 강제로 보상 UI 새로고침 (테스트용)
        /// </summary>
        [ContextMenu("Refresh Reward UI")]
        public void RefreshRewardUI()
        {
            if (RewardManager.Instance != null && RewardManager.Instance.IsShowingRewards())
            {
                var rewardData = RewardManager.Instance.GetCurrentBattleRewards();
                if (rewardData != null)
                {
                    OnBattleRewardsGenerated(rewardData);
                }
            }
        }
    }
}