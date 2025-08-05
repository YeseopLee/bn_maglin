using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maglin.Core;

namespace Maglin.UI
{
    /// <summary>
    /// 층 정보를 표시하는 UI 클래스
    /// </summary>
    public class FloorUI : MonoBehaviour
    {
        [Header("현재 층 표시")]
        [SerializeField] private TextMeshProUGUI currentFloorText;
        [SerializeField] private TextMeshProUGUI currentFloorTypeText;
        [SerializeField] private Image currentFloorIcon;

        [Header("다음 층 미리보기")]
        [SerializeField] private TextMeshProUGUI nextFloorText;
        [SerializeField] private TextMeshProUGUI nextFloorTypeText;
        [SerializeField] private Image nextFloorIcon;

        [Header("진행도 표시")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("층 아이콘 설정")]
        [SerializeField] private Sprite normalFloorIcon;
        [SerializeField] private Sprite eliteFloorIcon;
        [SerializeField] private Sprite shopFloorIcon;
        [SerializeField] private Sprite bossFloorIcon;
        [SerializeField] private Sprite eventFloorIcon;
        [SerializeField] private Sprite startFloorIcon;

        [Header("층 타입 색상")]
        [SerializeField] private Color normalFloorColor = Color.white;
        [SerializeField] private Color eliteFloorColor = Color.yellow;
        [SerializeField] private Color shopFloorColor = Color.green;
        [SerializeField] private Color bossFloorColor = Color.red;
        [SerializeField] private Color eventFloorColor = Color.cyan;
        [SerializeField] private Color startFloorColor = Color.blue;

        private void OnEnable()
        {
            // FloorManager 이벤트 구독
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnFloorChanged += OnFloorChanged;
                FloorManager.Instance.OnFloorTypeChanged += OnFloorTypeChanged;
                FloorManager.Instance.OnFloorStarted += OnFloorStarted;
                FloorManager.Instance.OnFloorCompleted += OnFloorCompleted;
                FloorManager.Instance.OnGameReset += OnGameReset;

                // 현재 상태로 초기화
                UpdateFloorDisplay();
            }
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (FloorManager.Instance != null)
            {
                FloorManager.Instance.OnFloorChanged -= OnFloorChanged;
                FloorManager.Instance.OnFloorTypeChanged -= OnFloorTypeChanged;
                FloorManager.Instance.OnFloorStarted -= OnFloorStarted;
                FloorManager.Instance.OnFloorCompleted -= OnFloorCompleted;
                FloorManager.Instance.OnGameReset -= OnGameReset;
            }
        }

        /// <summary>
        /// 층이 변경될 때 호출
        /// </summary>
        private void OnFloorChanged(int newFloor)
        {
            UpdateFloorDisplay();
        }

        /// <summary>
        /// 층 타입이 변경될 때 호출
        /// </summary>
        private void OnFloorTypeChanged(FloorType newType)
        {
            UpdateFloorDisplay();
        }

        /// <summary>
        /// 층이 시작될 때 호출
        /// </summary>
        private void OnFloorStarted(FloorInfo floorInfo)
        {
            UpdateFloorDisplay();

            // 층 시작 애니메이션이나 효과 추가 가능
            Debug.Log($"Floor started UI update: {floorInfo.floorName}");
        }

        /// <summary>
        /// 층이 완료될 때 호출
        /// </summary>
        private void OnFloorCompleted(FloorInfo floorInfo)
        {
            // 층 완료 애니메이션이나 효과 추가 가능
            Debug.Log($"Floor completed UI update: {floorInfo.floorName}");
        }

        /// <summary>
        /// 게임이 리셋될 때 호출
        /// </summary>
        private void OnGameReset()
        {
            UpdateFloorDisplay();
        }

        /// <summary>
        /// 층 표시 업데이트
        /// </summary>
        public void UpdateFloorDisplay()
        {
            if (FloorManager.Instance == null)
                return;

            FloorInfo currentFloor = FloorManager.Instance.CurrentFloorInfo;
            FloorType nextFloorType = FloorManager.Instance.GetNextFloorType();

            // 현재 층 정보 업데이트
            UpdateCurrentFloorDisplay(currentFloor);

            // 다음 층 정보 업데이트
            UpdateNextFloorDisplay(FloorManager.Instance.CurrentFloor + 1, nextFloorType);

            // 진행도 업데이트
            UpdateProgressDisplay();
        }

        /// <summary>
        /// 현재 층 표시 업데이트
        /// </summary>
        private void UpdateCurrentFloorDisplay(FloorInfo floorInfo)
        {
            // 층 번호
            if (currentFloorText != null)
                currentFloorText.text = $"{floorInfo.floorNumber}층";

            // 층 타입
            if (currentFloorTypeText != null)
            {
                currentFloorTypeText.text = GetFloorTypeDisplayName(floorInfo.floorType);
                currentFloorTypeText.color = GetFloorTypeColor(floorInfo.floorType);
            }

            // 층 아이콘
            if (currentFloorIcon != null)
            {
                currentFloorIcon.sprite = GetFloorTypeIcon(floorInfo.floorType);
                currentFloorIcon.color = GetFloorTypeColor(floorInfo.floorType);
            }
        }

        /// <summary>
        /// 다음 층 표시 업데이트
        /// </summary>
        private void UpdateNextFloorDisplay(int nextFloorNumber, FloorType nextFloorType)
        {
            // 층 번호
            if (nextFloorText != null)
                nextFloorText.text = $"{nextFloorNumber}층";

            // 층 타입
            if (nextFloorTypeText != null)
            {
                nextFloorTypeText.text = GetFloorTypeDisplayName(nextFloorType);
                nextFloorTypeText.color = GetFloorTypeColor(nextFloorType);
            }

            // 층 아이콘
            if (nextFloorIcon != null)
            {
                nextFloorIcon.sprite = GetFloorTypeIcon(nextFloorType);
                nextFloorIcon.color = GetFloorTypeColor(nextFloorType);
            }
        }

        /// <summary>
        /// 진행도 표시 업데이트
        /// </summary>
        private void UpdateProgressDisplay()
        {
            if (FloorManager.Instance == null)
                return;

            float progress = FloorManager.Instance.GetProgressPercentage();

            // 진행도 슬라이더
            if (progressSlider != null)
                progressSlider.value = progress / 100f;

            // 진행도 텍스트
            if (progressText != null)
                progressText.text = $"{progress:F1}%";
        }

        /// <summary>
        /// 층 타입 표시 이름 반환
        /// </summary>
        private string GetFloorTypeDisplayName(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Start => "시작",
                FloorType.Normal => "일반 전투",
                FloorType.Elite => "엘리트 전투",
                FloorType.Shop => "상점",
                FloorType.Boss => "보스 전투",
                FloorType.Event => "이벤트",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// 층 타입별 색상 반환
        /// </summary>
        private Color GetFloorTypeColor(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Start => startFloorColor,
                FloorType.Normal => normalFloorColor,
                FloorType.Elite => eliteFloorColor,
                FloorType.Shop => shopFloorColor,
                FloorType.Boss => bossFloorColor,
                FloorType.Event => eventFloorColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// 층 타입별 아이콘 반환
        /// </summary>
        private Sprite GetFloorTypeIcon(FloorType floorType)
        {
            return floorType switch
            {
                FloorType.Start => startFloorIcon,
                FloorType.Normal => normalFloorIcon,
                FloorType.Elite => eliteFloorIcon,
                FloorType.Shop => shopFloorIcon,
                FloorType.Boss => bossFloorIcon,
                FloorType.Event => eventFloorIcon,
                _ => normalFloorIcon
            };
        }

        /// <summary>
        /// 강제로 UI 새로고침 (테스트용)
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateFloorDisplay();
        }
    }
}