using UnityEngine;
using Maglin.Player;
using Maglin.Battle;

namespace Maglin.Event
{
    /// <summary>
    /// 이벤트 테스트를 위한 컨트롤러 - EventManager의 실제 설정을 사용
    /// </summary>
    public class EventTestController : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private int testFloor = 1;

        [Header("플레이어 입장 애니메이션")]
        [SerializeField] private bool enablePlayerEntrance = true;
        [SerializeField] private Vector2Int playerGridPosition = new Vector2Int(0, 0); // 플레이어 그리드 위치 (BattleTestController와 동일)

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool autoStartEvent = true;

        private void Start()
        {
            StartCoroutine(InitializeEventScene());
        }

        /// <summary>
        /// 이벤트 씬 초기화 코루틴
        /// </summary>
        private System.Collections.IEnumerator InitializeEventScene()
        {
            if (debugMode)
                Debug.Log("[EventTestController] 이벤트 씬 초기화 시작");

            // PlayerEventManager 초기화
            if (enablePlayerEntrance)
            {
                yield return StartCoroutine(InitializePlayerEventManager());
            }

            // EventUIManager는 자체적으로 UI 참조를 찾아서 설정함 (FindUIReferences)
            if (debugMode)
                Debug.Log("[EventTestController] EventUIManager가 자동으로 UI 참조를 설정합니다.");

            // 플레이어 입장 애니메이션 실행
            if (enablePlayerEntrance && PlayerEventManager.Instance != null)
            {
                if (debugMode)
                    Debug.Log("[EventTestController] 플레이어 입장 애니메이션 시작");

                yield return StartCoroutine(PlayerEventManager.Instance.PlayPlayerEntranceAnimation());

                if (debugMode)
                    Debug.Log("[EventTestController] 플레이어 입장 애니메이션 완료");
            }

            // 입장 애니메이션 완료 후 이벤트 시작
            if (autoStartEvent)
            {
                if (debugMode)
                    Debug.Log("[EventTestController] 입장 애니메이션 완료 후 이벤트 시작 대기");

                yield return new WaitForSeconds(0.5f); // 잠시 대기

                if (debugMode)
                    Debug.Log("[EventTestController] 이벤트 시작");

                StartRandomEventForTest();
            }

            if (debugMode)
                Debug.Log("[EventTestController] 이벤트 씬 초기화 완료");
        }

        /// <summary>
        /// PlayerEventManager 초기화
        /// </summary>
        private System.Collections.IEnumerator InitializePlayerEventManager()
        {
            if (debugMode)
                Debug.Log("[EventTestController] PlayerEventManager 초기화 시작");

            // PlayerEventManager가 있는지 확인
            if (PlayerEventManager.Instance == null)
            {
                if (debugMode)
                    Debug.LogWarning("[EventTestController] PlayerEventManager.Instance가 null입니다. 씬에 PlayerEventManager가 있는지 확인하세요.");
                yield break;
            }

            // GridFieldManager 초기화
            if (debugMode)
                Debug.Log("[EventTestController] GridFieldManager 초기화 시작");

            if (GridFieldManager.Instance != null)
            {
                if (!GridFieldManager.Instance.IsInitialized)
                {
                    if (debugMode)
                        Debug.Log("[EventTestController] GridFieldManager.InitializeGridField() 호출");

                    GridFieldManager.Instance.InitializeGridField();

                    // 한 프레임 대기 후 초기화 확인
                    yield return null;
                }

                if (GridFieldManager.Instance.IsInitialized)
                {
                    if (debugMode)
                        Debug.Log("[EventTestController] GridFieldManager 초기화 완료 확인됨");
                }
                else
                {
                    if (debugMode)
                        Debug.LogWarning("[EventTestController] GridFieldManager 초기화 실패");
                }
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[EventTestController] GridFieldManager.Instance가 null입니다. 씬에 GridFieldManager가 있는지 확인하세요.");
            }

            // PlayerEventManager 초기화
            PlayerEventManager.Instance.InitializePlayerEventManager();

            // 그리드 위치 설정
            PlayerEventManager.Instance.SetPlayerGridPosition(playerGridPosition);

            // 한 프레임 대기
            yield return null;

            if (debugMode)
                Debug.Log("[EventTestController] PlayerEventManager 초기화 완료");
        }


        /// <summary>
        /// EventManager의 실제 설정을 사용한 이벤트 시작
        /// </summary>
        private void StartRandomEventForTest()
        {
            if (EventManager.Instance == null)
            {
                Debug.LogError("[EventTestController] EventManager.Instance가 null입니다!");
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[EventTestController] {testFloor}층에서 실제 이벤트 시스템 테스트 시작");
                LogCurrentStatus(); // 시작 시 현재 상태 출력
                LogAvailableEvents(); // 사용 가능한 이벤트 로그
            }

            // EventManager에서 실제 이벤트 선택 로직 사용
            EventSO selectedEvent = EventManager.Instance.SelectEventForFloor(testFloor);

            if (selectedEvent != null)
            {
                if (debugMode)
                    Debug.Log($"[EventTestController] 선택된 이벤트: {selectedEvent.EventName} (가중치: {selectedEvent.SpawnWeight})");

                EventManager.Instance.ForceStartEvent(selectedEvent);
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning($"[EventTestController] {testFloor}층에서 사용 가능한 이벤트가 없습니다!");
            }
        }

        /// <summary>
        /// 특정 이벤트 강제 시작 (UI 버튼용) - EventManager의 availableEvents에서 선택
        /// </summary>
        public void ForceStartEvent(int eventIndex)
        {
            if (EventManager.Instance == null)
            {
                Debug.LogError("[EventTestController] EventManager.Instance가 null입니다!");
                return;
            }

            var availableEvents = EventManager.Instance.GetAvailableEvents();
            if (availableEvents == null || eventIndex < 0 || eventIndex >= availableEvents.Length)
            {
                Debug.LogError($"[EventTestController] 잘못된 이벤트 인덱스: {eventIndex} (사용 가능한 이벤트 수: {availableEvents?.Length ?? 0})");
                return;
            }

            EventSO selectedEvent = availableEvents[eventIndex];
            if (selectedEvent != null)
            {
                if (debugMode)
                    Debug.Log($"[EventTestController] 강제 이벤트 시작: {selectedEvent.EventName}");

                EventManager.Instance.ForceStartEvent(selectedEvent);
            }
            else
            {
                Debug.LogError($"[EventTestController] {eventIndex}번 이벤트가 null입니다!");
            }
        }

        /// <summary>
        /// 첫 번째 이벤트 시작 (UI 버튼용)
        /// </summary>
        public void StartFirstEvent()
        {
            ForceStartEvent(0);
        }

        /// <summary>
        /// 두 번째 이벤트 시작 (UI 버튼용)
        /// </summary>
        public void StartSecondEvent()
        {
            ForceStartEvent(1);
        }

        /// <summary>
        /// 랜덤 이벤트 시작 (UI 버튼용)
        /// </summary>
        public void StartRandomEvent()
        {
            StartRandomEventForTest();
        }

        /// <summary>
        /// 확률 기반 이벤트 발생 시도 (UI 버튼용)
        /// </summary>
        public void TryTriggerEvent()
        {
            if (EventManager.Instance != null)
            {
                bool eventTriggered = EventManager.Instance.TryTriggerEvent(testFloor);
                if (debugMode)
                {
                    Debug.Log($"[EventTestController] 이벤트 발생 시도 결과: {(eventTriggered ? "발생" : "발생 안됨")}");
                }
            }
        }

        /// <summary>
        /// 테스트 골드 추가 (UI 버튼용)
        /// </summary>
        public void AddTestGold()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.AddGold(50);
                if (debugMode)
                    Debug.Log("[EventTestController] 테스트 골드 50 추가");
            }
        }

        /// <summary>
        /// 테스트 체력 감소 (UI 버튼용)
        /// </summary>
        public void ReduceTestHealth()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.TakeDamage(10);
                if (debugMode)
                    Debug.Log("[EventTestController] 테스트 체력 10 감소");
            }
        }

        /// <summary>
        /// 테스트 체력 회복 (UI 버튼용)
        /// </summary>
        public void RestoreTestHealth()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.Heal(15);
                if (debugMode)
                    Debug.Log("[EventTestController] 테스트 체력 15 회복");
            }
        }

        /// <summary>
        /// 이벤트 발생 확률 설정 (UI 버튼용)
        /// </summary>
        public void SetEventChanceHigh()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.SetEventChance(0.8f);
                if (debugMode)
                    Debug.Log("[EventTestController] 이벤트 발생 확률을 80%로 설정");
            }
        }

        /// <summary>
        /// 이벤트 발생 확률 설정 (UI 버튼용)
        /// </summary>
        public void SetEventChanceLow()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.SetEventChance(0.2f);
                if (debugMode)
                    Debug.Log("[EventTestController] 이벤트 발생 확률을 20%로 설정");
            }
        }

        /// <summary>
        /// 현재 상태 로그 출력 (UI 버튼용)
        /// </summary>
        public void LogCurrentStatus()
        {
            Debug.Log($"[EventTestController] 현재 상태:");

            if (PlayerManager.Instance != null)
            {
                Debug.Log($"  - 골드: {PlayerManager.Instance.CurrentGold}");
                Debug.Log($"  - 체력: {PlayerManager.Instance.CurrentHealth}/{PlayerManager.Instance.MaxHealth}");

                var relics = PlayerManager.Instance.CurrentRelics;
                Debug.Log($"  - 유물 수: {relics.Count}");
                foreach (var relic in relics)
                {
                    Debug.Log($"    * {relic.RelicName}");
                }
            }
            else
            {
                Debug.LogWarning("  - PlayerManager.Instance가 null입니다!");
            }

            if (EventManager.Instance != null)
            {
                Debug.Log($"  - 현재 이벤트 활성: {EventManager.Instance.IsEventActive}");
                if (EventManager.Instance.CurrentEvent != null)
                {
                    Debug.Log($"  - 현재 이벤트: {EventManager.Instance.CurrentEvent.EventName}");
                }
            }
            else
            {
                Debug.LogWarning("  - EventManager.Instance가 null입니다!");
            }

            LogAvailableEvents();
        }

        /// <summary>
        /// EventManager의 사용 가능한 이벤트 로그 출력
        /// </summary>
        private void LogAvailableEvents()
        {
            if (EventManager.Instance != null)
            {
                var availableEvents = EventManager.Instance.GetAvailableEvents();
                if (availableEvents != null && availableEvents.Length > 0)
                {
                    Debug.Log($"  - EventManager 사용 가능한 이벤트: {availableEvents.Length}개");
                    for (int i = 0; i < availableEvents.Length; i++)
                    {
                        if (availableEvents[i] != null)
                        {
                            var eventSO = availableEvents[i];
                            bool canAppear = eventSO.CanAppearOnFloor(testFloor);
                            Debug.Log($"    {i}: {eventSO.EventName} (층 {eventSO.MinFloor}-{eventSO.MaxFloor}, 가중치 {eventSO.SpawnWeight}) " +
                                    $"[{testFloor}층 출현 가능: {(canAppear ? "O" : "X")}]");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("  - EventManager에 설정된 이벤트가 없습니다!");
                }
            }
        }

        /// <summary>
        /// 일회성 이벤트 초기화 (UI 버튼용)
        /// </summary>
        public void ResetOneTimeEvents()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.ResetOneTimeEvents();
                if (debugMode)
                    Debug.Log("[EventTestController] 일회성 이벤트 초기화");
            }
        }

        /// <summary>
        /// 테스트 층 설정
        /// </summary>
        public void SetTestFloor(int floor)
        {
            testFloor = Mathf.Max(1, floor);
            if (debugMode)
                Debug.Log($"[EventTestController] 테스트 층을 {testFloor}로 설정");
        }

        /// <summary>
        /// 자동 시작 토글
        /// </summary>
        public void ToggleAutoStart()
        {
            autoStartEvent = !autoStartEvent;
            if (debugMode)
                Debug.Log($"[EventTestController] 자동 시작: {autoStartEvent}");
        }

        /// <summary>
        /// 플레이어 입장 애니메이션 토글
        /// </summary>
        public void TogglePlayerEntrance()
        {
            enablePlayerEntrance = !enablePlayerEntrance;
            if (debugMode)
                Debug.Log($"[EventTestController] 플레이어 입장 애니메이션: {enablePlayerEntrance}");
        }

        /// <summary>
        /// 플레이어 입장 애니메이션 수동 실행 (UI 버튼용)
        /// </summary>
        public void PlayPlayerEntranceAnimation()
        {
            if (PlayerEventManager.Instance != null)
            {
                StartCoroutine(PlayerEventManager.Instance.PlayPlayerEntranceAnimation());
                if (debugMode)
                    Debug.Log("[EventTestController] 플레이어 입장 애니메이션 수동 실행");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[EventTestController] PlayerEventManager.Instance가 null입니다!");
            }
        }

        /// <summary>
        /// 플레이어 입장 애니메이션 상태 리셋 (UI 버튼용)
        /// </summary>
        public void ResetPlayerEntranceState()
        {
            if (PlayerEventManager.Instance != null)
            {
                PlayerEventManager.Instance.ResetEntranceAnimationState();
                if (debugMode)
                    Debug.Log("[EventTestController] 플레이어 입장 애니메이션 상태 리셋");
            }
        }

        /// <summary>
        /// 플레이어 위치 설정 (UI 버튼용)
        /// </summary>
        public void SetPlayerToGridPosition()
        {
            if (PlayerEventManager.Instance != null)
            {
                PlayerEventManager.Instance.SetPlayerGridPosition(playerGridPosition);
                if (debugMode)
                    Debug.Log($"[EventTestController] 플레이어를 그리드 위치로 설정: {playerGridPosition}");
            }
        }

        /// <summary>
        /// PlayerEventManager 디버그 정보 출력 (UI 버튼용)
        /// </summary>
        public void DebugPlayerEventManager()
        {
            if (PlayerEventManager.Instance != null)
            {
                PlayerEventManager.Instance.DebugPlayerEventInfo();
            }
            else
            {
                Debug.LogWarning("[EventTestController] PlayerEventManager.Instance가 null입니다!");
            }
        }

        // OnValidate 제거 - 이제 EventManager의 availableEvents를 직접 사용
    }
}