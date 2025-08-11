using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 스폰 애니메이션을 관리하는 클래스
    /// </summary>
    public class MonsterSpawnAnimationManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterSpawnAnimationManager _instance;

        public static MonsterSpawnAnimationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterSpawnAnimationManager>();
                }
                return _instance;
            }
        }
        #endregion

        #region Animation Settings
        [Header("스폰 애니메이션 설정")]
        [SerializeField] private float spawnDelay = 0.3f;
        [SerializeField] private float spawnDuration = 0.8f;
        [SerializeField] private float scaleStartSize = 0.1f;
        [SerializeField] private Ease spawnEase = Ease.OutBack;



        [Header("추가 효과 설정")]
        [SerializeField] private bool useSlideUpEffect = true; // 아래에서 위로 슬라이드
        [SerializeField] private float slideDistance = 2.0f; // 슬라이드 거리
        [SerializeField] private bool useColorFadeEffect = true;
        [SerializeField] private bool preserveOriginalColor = true; // 원본 색상 보존

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 스폰 애니메이션 시작 이벤트
        /// </summary>
        public static event System.Action<GameObject> OnSpawnAnimationStarted;

        /// <summary>
        /// 몬스터 스폰 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<GameObject> OnSpawnAnimationCompleted;

        /// <summary>
        /// 모든 몬스터 스폰 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action OnAllSpawnAnimationsCompleted;
        #endregion

        #region Fields
        private Queue<GameObject> spawnQueue = new Queue<GameObject>();
        private bool isSpawning = false;
        private int totalMonstersToSpawn = 0;
        private int spawnedMonstersCount = 0;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정 (씬 기반)
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[MonsterSpawnAnimationManager] 씬에 여러 개의 MonsterSpawnAnimationManager가 있습니다. 중복을 제거합니다.");
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

        #region Public Methods
        /// <summary>
        /// 단일 몬스터 스폰 애니메이션 실행
        /// </summary>
        public void PlaySpawnAnimation(GameObject monster)
        {
            if (monster == null) return;

            StartCoroutine(ExecuteSpawnAnimation(monster));
        }

        /// <summary>
        /// 여러 몬스터들의 순차적 스폰 애니메이션 실행
        /// </summary>
        public void PlaySequentialSpawnAnimations(List<GameObject> monsters)
        {
            if (monsters == null || monsters.Count == 0) return;

            // 기존 큐 클리어
            spawnQueue.Clear();
            totalMonstersToSpawn = monsters.Count;
            spawnedMonstersCount = 0;

            // 몬스터들을 큐에 추가
            foreach (var monster in monsters)
            {
                if (monster != null)
                {
                    spawnQueue.Enqueue(monster);
                }
            }

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 순차 스폰 시작: {spawnQueue.Count}마리");

            // 순차 스폰 시작
            if (!isSpawning)
            {
                StartCoroutine(ProcessSpawnQueue());
            }
        }

        /// <summary>
        /// 현재 진행 중인 모든 스폰 애니메이션 중단
        /// </summary>
        public void StopAllSpawnAnimations()
        {
            StopAllCoroutines();
            spawnQueue.Clear();
            isSpawning = false;
            totalMonstersToSpawn = 0;
            spawnedMonstersCount = 0;

            if (debugMode)
                Debug.Log("[MonsterSpawnAnimationManager] 모든 스폰 애니메이션 중단");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 스폰 큐 처리 (순차 스폰)
        /// </summary>
        private IEnumerator ProcessSpawnQueue()
        {
            isSpawning = true;

            while (spawnQueue.Count > 0)
            {
                GameObject monster = spawnQueue.Dequeue();

                if (monster != null)
                {
                    // 스폰 애니메이션 실행
                    yield return StartCoroutine(ExecuteSpawnAnimation(monster));

                    spawnedMonstersCount++;

                    // 다음 몬스터 스폰까지 대기
                    if (spawnQueue.Count > 0)
                    {
                        yield return new WaitForSeconds(spawnDelay);
                    }
                }
            }

            isSpawning = false;

            // 모든 스폰 완료 이벤트 발생
            OnAllSpawnAnimationsCompleted?.Invoke();

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 모든 몬스터 스폰 완료: {spawnedMonstersCount}/{totalMonstersToSpawn}");
        }

        /// <summary>
        /// 개별 몬스터 스폰 애니메이션 실행
        /// </summary>
        private IEnumerator ExecuteSpawnAnimation(GameObject monster)
        {
            if (monster == null) yield break;

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 스폰 애니메이션 시작: {monster.name}");

            // 이벤트 발생
            OnSpawnAnimationStarted?.Invoke(monster);

            // 초기 상태 설정 (원본 위치 저장)
            Vector3 originalPosition = monster.transform.position;
            SetupInitialState(monster, originalPosition);

            // 스폰 애니메이션 시퀀스 생성
            Sequence spawnSequence = DOTween.Sequence();

            // 1. 스케일 애니메이션 (작은 크기에서 정상 크기로) - 부드럽게
            spawnSequence.Append(monster.transform.DOScale(Vector3.one, spawnDuration).SetEase(spawnEase));

            // 2. 슬라이드 업 애니메이션 (선택적) - 아래에서 위로 자연스럽게
            if (useSlideUpEffect)
            {
                spawnSequence.Join(monster.transform.DOMoveY(originalPosition.y, spawnDuration).SetEase(Ease.OutQuart));
            }

            // 3. 색상 페이드 애니메이션 (선택적) - 투명에서 불투명으로
            if (useColorFadeEffect)
            {
                SpriteRenderer spriteRenderer = monster.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Color targetColor;
                    if (preserveOriginalColor)
                    {
                        // 원본 색상으로 페이드 (알파만 1로)
                        Color originalColor = spriteRenderer.color;
                        targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
                    }
                    else
                    {
                        // 흰색으로 페이드
                        targetColor = new Color(1f, 1f, 1f, 1f);
                    }

                    spawnSequence.Join(spriteRenderer.DOColor(targetColor, spawnDuration).SetEase(Ease.OutQuad));
                }
            }

            // 애니메이션 완료 대기
            yield return spawnSequence.WaitForCompletion();

            // 최종 상태 보정
            FinalizeMonsterState(monster, originalPosition);

            // 이벤트 발생
            OnSpawnAnimationCompleted?.Invoke(monster);

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 스폰 애니메이션 완료: {monster.name}");
        }

        /// <summary>
        /// 몬스터 초기 상태 설정
        /// </summary>
        private void SetupInitialState(GameObject monster, Vector3 originalPosition)
        {
            // 스케일을 작게 설정
            monster.transform.localScale = Vector3.one * scaleStartSize;

            // 슬라이드 업 효과 사용 시 시작 위치를 아래로 설정
            if (useSlideUpEffect)
            {
                Vector3 startPosition = originalPosition - Vector3.up * slideDistance;
                monster.transform.position = startPosition;
            }

            // 색상 설정 (페이드 효과 사용 시)
            if (useColorFadeEffect)
            {
                SpriteRenderer spriteRenderer = monster.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    if (preserveOriginalColor)
                    {
                        // 원본 색상의 알파만 0으로 설정
                        Color originalColor = spriteRenderer.color;
                        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
                    }
                    else
                    {
                        // 완전 투명으로 설정
                        spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
                    }
                }
            }

            // 몬스터를 활성화 (혹시 비활성화되어 있다면)
            if (!monster.activeInHierarchy)
            {
                monster.SetActive(true);
            }
        }

        /// <summary>
        /// 몬스터 최종 상태 보정
        /// </summary>
        private void FinalizeMonsterState(GameObject monster, Vector3 originalPosition)
        {
            if (monster == null) return;

            // 스케일 정상화
            monster.transform.localScale = Vector3.one;

            // 위치 정상화 (원본 위치로 확실히 설정)
            monster.transform.position = originalPosition;

            // 회전 정상화
            monster.transform.rotation = Quaternion.identity;

            // 색상 정상화 (원본 색상 보존하면서 알파만 1로)
            if (useColorFadeEffect)
            {
                SpriteRenderer spriteRenderer = monster.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Color currentColor = spriteRenderer.color;
                    spriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
                }
            }
        }


        #endregion

        #region Settings
        /// <summary>
        /// 스폰 애니메이션 설정 업데이트
        /// </summary>
        public void UpdateAnimationSettings(float delay, float duration, Ease ease)
        {
            spawnDelay = delay;
            spawnDuration = duration;
            spawnEase = ease;

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 애니메이션 설정 업데이트: 지연={delay}, 지속시간={duration}, 이징={ease}");
        }

        /// <summary>
        /// 슬라이드 효과 설정 업데이트
        /// </summary>
        public void UpdateSlideSettings(bool useSlide, float slideDistance)
        {
            useSlideUpEffect = useSlide;
            this.slideDistance = slideDistance;

            if (debugMode)
                Debug.Log($"[MonsterSpawnAnimationManager] 슬라이드 설정 업데이트: 사용={useSlide}, 거리={slideDistance}");
        }
        #endregion
    }
}