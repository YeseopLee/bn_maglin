using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 사망 시 조각나는 애니메이션을 관리하는 클래스
    /// </summary>
    public class MonsterDeathAnimationManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static MonsterDeathAnimationManager _instance;

        public static MonsterDeathAnimationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterDeathAnimationManager>();
                }
                return _instance;
            }
        }
        #endregion

        #region Animation Settings
        [Header("사망 애니메이션 설정")]
        [SerializeField] private float fragmentationDuration = 2.0f; // 조각 애니메이션 시간
        [SerializeField] private int fragmentCount = 16; // 조각 개수 더 증가 (4x4 그리드)
        [SerializeField] private float explosionForce = 0.7f; // 폭발력 대폭 감소 (가까이에서 부서지도록)
        [SerializeField] private float fragmentLifetime = 5.0f; // 조각 생존 시간
        [SerializeField] private Ease fragmentEase = Ease.OutCubic; // 더 자연스러운 이징



        [Header("색상 및 시각 효과")]
        [SerializeField] private bool useColorFading = true;
        [SerializeField] private Color deathColor = Color.red;
        [SerializeField] private bool useScaleEffect = true;
        [SerializeField] private bool useRotationEffect = true;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Events
        /// <summary>
        /// 몬스터 사망 애니메이션 시작 이벤트
        /// </summary>
        public static event System.Action<GameObject> OnDeathAnimationStarted;

        /// <summary>
        /// 몬스터 사망 애니메이션 완료 이벤트
        /// </summary>
        public static event System.Action<GameObject> OnDeathAnimationCompleted;
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
                Debug.LogWarning("[MonsterDeathAnimationManager] 씬에 여러 개의 MonsterDeathAnimationManager가 있습니다. 중복을 제거합니다.");
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
        /// 몬스터 사망 애니메이션 실행
        /// </summary>
        public void PlayDeathAnimation(GameObject monster)
        {
            if (monster == null) return;

            StartCoroutine(ExecuteDeathAnimation(monster));
        }

        /// <summary>
        /// 현재 진행 중인 모든 사망 애니메이션 중단
        /// </summary>
        public void StopAllDeathAnimations()
        {
            StopAllCoroutines();

            if (debugMode)
                Debug.Log("[MonsterDeathAnimationManager] 모든 사망 애니메이션 중단");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 개별 몬스터 사망 애니메이션 실행
        /// </summary>
        private IEnumerator ExecuteDeathAnimation(GameObject monster)
        {
            if (monster == null) yield break;

            if (debugMode)
                Debug.Log($"[MonsterDeathAnimationManager] 사망 애니메이션 시작: {monster.name}");

            // 이벤트 발생
            OnDeathAnimationStarted?.Invoke(monster);

            // 원본 스프라이트 정보 수집
            SpriteRenderer originalRenderer = monster.GetComponent<SpriteRenderer>();
            if (originalRenderer == null || originalRenderer.sprite == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[MonsterDeathAnimationManager] {monster.name}에 SpriteRenderer나 Sprite가 없습니다.");
                yield break;
            }

            Sprite originalSprite = originalRenderer.sprite;
            Color originalColor = originalRenderer.color;
            Vector3 originalPosition = monster.transform.position;
            Vector3 originalScale = monster.transform.localScale;



            // 원본 몬스터 숨기기 (조각들로 대체)
            originalRenderer.enabled = false;

            // 몬스터의 체력바나 기타 UI 숨기기
            HideMonsterUI(monster);

            // 조각들 생성 (실제 스프라이트 조각화 사용)
            List<GameObject> fragments = CreateRealSpriteFragments(originalSprite, originalColor, originalPosition, originalScale);

            // 조각 애니메이션 시퀀스 실행
            yield return StartCoroutine(AnimateFragments(fragments, originalPosition));

            // 조각들 정리
            foreach (var fragment in fragments)
            {
                if (fragment != null)
                {
                    Destroy(fragment);
                }
            }

            // 메모리 정리 (잠시 후에 실행)
            StartCoroutine(CleanupFragmentSpritesDelayed());

            // 이벤트 발생
            OnDeathAnimationCompleted?.Invoke(monster);

            if (debugMode)
                Debug.Log($"[MonsterDeathAnimationManager] 사망 애니메이션 완료: {monster.name}");
        }

        /// <summary>
        /// 실제 스프라이트 조각화를 사용한 조각들 생성
        /// </summary>
        private List<GameObject> CreateRealSpriteFragments(Sprite originalSprite, Color originalColor, Vector3 position, Vector3 scale)
        {
            if (originalSprite == null)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterDeathAnimationManager] 원본 스프라이트가 null입니다.");
                return new List<GameObject>();
            }

            // 실제 스프라이트를 조각으로 분할
            List<Sprite> fragmentSprites = SpriteTextureFragmenter.FragmentSprite(originalSprite, fragmentCount);

            if (fragmentSprites.Count == 0)
            {
                if (debugMode)
                    Debug.LogWarning("[MonsterDeathAnimationManager] 조각 스프라이트가 생성되지 않았습니다.");
                return new List<GameObject>();
            }

            // 조각 스프라이트들을 GameObject로 변환
            List<GameObject> fragmentObjects = SpriteTextureFragmenter.CreateFragmentGameObjects(
                fragmentSprites, position, scale, originalColor
            );

            // 메모리 정리를 위해 나중에 정리할 스프라이트 목록 저장
            StoreFragmentSpritesForCleanup(fragmentSprites);

            if (debugMode)
                Debug.Log($"[MonsterDeathAnimationManager] 실제 조각화된 {fragmentObjects.Count}개의 조각 생성됨");

            return fragmentObjects;
        }

        /// <summary>
        /// 정리할 조각 스프라이트들 저장
        /// </summary>
        private List<List<Sprite>> fragmentSpritesToCleanup = new List<List<Sprite>>();

        private void StoreFragmentSpritesForCleanup(List<Sprite> fragmentSprites)
        {
            // 나중에 정리할 수 있도록 저장
            fragmentSpritesToCleanup.Add(new List<Sprite>(fragmentSprites));

            // 너무 많이 쌓이지 않도록 오래된 것들 정리
            if (fragmentSpritesToCleanup.Count > 10)
            {
                var oldSprites = fragmentSpritesToCleanup[0];
                SpriteTextureFragmenter.CleanupFragmentSprites(oldSprites);
                fragmentSpritesToCleanup.RemoveAt(0);
            }
        }

        /// <summary>
        /// 스프라이트를 조각들로 분할하여 생성 (기존 방식 - 백업용)
        /// </summary>
        private List<GameObject> CreateSpriteFragments(Sprite originalSprite, Color originalColor, Vector3 position, Vector3 scale)
        {
            List<GameObject> fragments = new List<GameObject>();

            if (originalSprite == null) return fragments;

            // 스프라이트 크기 계산
            Bounds spriteBounds = originalSprite.bounds;
            float spriteWidth = spriteBounds.size.x * scale.x;
            float spriteHeight = spriteBounds.size.y * scale.y;

            // 조각 크기 계산 (가로, 세로로 나누기)
            int cols = Mathf.CeilToInt(Mathf.Sqrt(fragmentCount));
            int rows = Mathf.CeilToInt((float)fragmentCount / cols);

            float fragmentWidth = spriteWidth / cols;
            float fragmentHeight = spriteHeight / rows;

            for (int i = 0; i < fragmentCount; i++)
            {
                int col = i % cols;
                int row = i / cols;

                if (row >= rows) break; // 범위 초과 방지

                // 조각 위치 계산
                float x = position.x - spriteWidth * 0.5f + fragmentWidth * (col + 0.5f);
                float y = position.y - spriteHeight * 0.5f + fragmentHeight * (row + 0.5f);
                Vector3 fragmentPosition = new Vector3(x, y, position.z);

                // 조각 생성
                GameObject fragment = CreateFragment(originalSprite, originalColor, fragmentPosition, scale, col, row, cols, rows);
                if (fragment != null)
                {
                    fragments.Add(fragment);
                }
            }

            if (debugMode)
                Debug.Log($"[MonsterDeathAnimationManager] {fragments.Count}개의 조각 생성됨");

            return fragments;
        }

        /// <summary>
        /// 개별 조각 생성
        /// </summary>
        private GameObject CreateFragment(Sprite originalSprite, Color originalColor, Vector3 position, Vector3 scale, int col, int row, int totalCols, int totalRows)
        {
            GameObject fragment = new GameObject($"Fragment_{col}_{row}");
            fragment.transform.position = position;
            fragment.transform.localScale = scale;

            // SpriteRenderer 추가
            SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
            renderer.sprite = originalSprite;
            renderer.color = originalColor;
            renderer.sortingOrder = 100; // 다른 오브젝트보다 앞에 표시

            // 조각 크기에 맞게 스케일 조정
            float scaleX = 1.0f / totalCols;
            float scaleY = 1.0f / totalRows;
            fragment.transform.localScale = new Vector3(scale.x * scaleX, scale.y * scaleY, scale.z);

            // UV 오프셋 적용 (조각 위치에 맞게 스프라이트 일부만 보이도록)
            // 주의: 이것은 간단한 구현이며, 실제로는 Material Property Block을 사용하는 것이 좋습니다.

            return fragment;
        }

        /// <summary>
        /// 조각들 애니메이션 실행
        /// </summary>
        private IEnumerator AnimateFragments(List<GameObject> fragments, Vector3 centerPosition)
        {
            if (fragments.Count == 0) yield break;

            // 모든 조각에 동시에 애니메이션 적용
            Sequence fragmentSequence = DOTween.Sequence();

            foreach (var fragment in fragments)
            {
                if (fragment == null) continue;

                // 폭발 방향 계산 (중심에서 바깥쪽으로, 더 자연스럽게)
                Vector3 explosionDirection = (fragment.transform.position - centerPosition).normalized;
                if (explosionDirection == Vector3.zero)
                {
                    explosionDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                }

                // 거리 기반 폭발력 조정 (중심에서 가까운 조각은 덜 날아가도록)
                float distanceFromCenter = Vector3.Distance(fragment.transform.position, centerPosition);
                float adjustedForce = explosionForce * (0.1f + distanceFromCenter * 0.5f);

                // 랜덤성 추가로 더 자연스러운 효과
                float randomFactor = Random.Range(0.2f, 0.4f);
                adjustedForce *= randomFactor;

                // 목표 위치 계산 (위아래로도 약간 움직이도록)
                Vector3 randomOffset = new Vector3(
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.1f, 0.1f),
                    0
                );
                Vector3 targetPosition = fragment.transform.position + explosionDirection * adjustedForce + randomOffset;

                // 개별 조각 애니메이션 설정
                var fragmentTween = DOTween.Sequence();

                // 1. 이동 애니메이션 - 포물선 궤적으로 자연스러운 움직임
                // 중력 효과를 포함한 최종 목표 위치
                Vector3 finalTarget = new Vector3(
                    targetPosition.x,
                    targetPosition.y - Random.Range(0.2f, 0.5f), // 아래로 살짝 떨어지도록
                    targetPosition.z
                );

                // 한 번에 자연스럽게 이동 (멈춤 없이)
                fragmentTween.Join(fragment.transform.DOMove(finalTarget, fragmentationDuration).SetEase(Ease.OutQuart));

                // 2. 회전 애니메이션 (선택적) - 더 자연스러운 회전
                if (useRotationEffect)
                {
                    float randomRotation = Random.Range(-180f, 180f); // 최대 반바퀴 회전 (더 자연스럽게)
                    fragmentTween.Join(fragment.transform.DORotate(new Vector3(0, 0, randomRotation), fragmentationDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad));
                }

                // 3. 스케일 애니메이션 (선택적) - 점진적으로 작아지기
                if (useScaleEffect)
                {
                    // 처음에는 약간 커졌다가 천천히 작아지도록
                    fragmentTween.Append(fragment.transform.DOScale(Vector3.one * 1.1f, fragmentationDuration * 0.2f).SetEase(Ease.OutQuad))
                                .Append(fragment.transform.DOScale(Vector3.zero, fragmentationDuration * 0.8f).SetEase(Ease.InQuad));
                }

                // 4. 색상 페이드 애니메이션 (선택적) - 더 자연스러운 페이드
                if (useColorFading)
                {
                    SpriteRenderer renderer = fragment.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        // 원본 색상을 유지하면서 천천히 투명해지도록
                        Color originalColor = renderer.color;
                        fragmentTween.Join(renderer.DOFade(0f, fragmentationDuration * 0.9f)
                            .SetDelay(fragmentationDuration * 0.1f)
                            .SetEase(Ease.InQuad));
                    }
                }

                // 시퀀스에 추가
                fragmentSequence.Join(fragmentTween);
            }

            // 모든 조각 애니메이션 완료 대기
            yield return fragmentSequence.WaitForCompletion();

            if (debugMode)
                Debug.Log("[MonsterDeathAnimationManager] 조각 애니메이션 완료");
        }



        /// <summary>
        /// 조각 스프라이트 메모리 정리 (지연 실행)
        /// </summary>
        private IEnumerator CleanupFragmentSpritesDelayed()
        {
            yield return new WaitForSeconds(6.0f); // 애니메이션 완료 후 6초 대기 (충분히 볼 수 있도록)

            // 저장된 조각 스프라이트들 정리
            foreach (var spriteList in fragmentSpritesToCleanup)
            {
                SpriteTextureFragmenter.CleanupFragmentSprites(spriteList);
            }
            fragmentSpritesToCleanup.Clear();

            if (debugMode)
                Debug.Log("[MonsterDeathAnimationManager] 조각 스프라이트 메모리 정리 완료");
        }

        /// <summary>
        /// 몬스터 UI 숨기기
        /// </summary>
        private void HideMonsterUI(GameObject monster)
        {
            // 체력바 숨기기
            Transform healthBarCanvas = monster.transform.Find("HealthBarCanvas");
            if (healthBarCanvas != null)
            {
                healthBarCanvas.gameObject.SetActive(false);
            }

            // 기타 UI 요소들 숨기기
            Transform healthText = monster.transform.Find("HealthText");
            if (healthText != null)
            {
                healthText.gameObject.SetActive(false);
            }
        }
        #endregion

        #region Settings
        /// <summary>
        /// 사망 애니메이션 설정 업데이트
        /// </summary>
        public void UpdateDeathAnimationSettings(float duration, int fragments, float force)
        {
            fragmentationDuration = duration;
            fragmentCount = fragments;
            explosionForce = force;

            if (debugMode)
                Debug.Log($"[MonsterDeathAnimationManager] 사망 애니메이션 설정 업데이트: 지속시간={duration}, 조각수={fragments}, 폭발력={force}");
        }


        #endregion
    }
}