using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 스폰 애니메이션 관리
    /// DOTween을 사용하여 몬스터 등장 애니메이션 처리
    /// </summary>
    public class MonsterSpawnAnimationManager : MonoBehaviour
    {
        [Header("스폰 애니메이션 설정")]
        [SerializeField] private float spawnDuration = 1.0f;
        [SerializeField] private float spawnInterval = 0.3f;
        [SerializeField] private Ease spawnEase = Ease.OutBack;
        [SerializeField] private float entranceHeight = 3f;
        
        [Header("효과 설정")]
        [SerializeField] private bool enableSpawnParticles = true;
        [SerializeField] private bool enableSpawnSound = true;
        [SerializeField] private bool enableGroundSlam = true;
        [SerializeField] private bool enableScreenShake = false;
        [SerializeField] private GameObject spawnParticlePrefab;
        
        [Header("그라운드 슬램 설정")]
        [SerializeField] private float slamBounceHeight = 0.3f;
        [SerializeField] private float slamDuration = 0.5f;
        [SerializeField] private int slamVibrato = 3;
        
        [Header("화면 흔들림 설정")]
        [SerializeField] private float shakeStrength = 0.2f;
        [SerializeField] private float shakeDuration = 0.3f;
        
        [Header("디버그")]
        [SerializeField] private bool debugMode = true;
        
        public static MonsterSpawnAnimationManager Instance { get; private set; }
        
        // 이벤트
        public System.Action<GameObject> OnMonsterSpawnStarted;
        public System.Action<GameObject> OnMonsterSpawnCompleted;
        public System.Action OnAllMonstersSpawned;
        
        private bool isSpawning = false;
        private List<GameObject> activeSpawnTweens = new List<GameObject>();
        private Camera mainCamera;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeAnimationManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializeAnimationManager()
        {
            mainCamera = Camera.main;
            
            if (debugMode)
            {
                Debug.Log($"MonsterSpawnAnimationManager 초기화 완료");
                Debug.Log($"- MainCamera: {(mainCamera != null ? mainCamera.name : "없음")}");
            }
        }
        
        /// <summary>
        /// 여러 몬스터를 순차적으로 스폰하는 애니메이션
        /// </summary>
        /// <param name="monsterObjects">스폰할 몬스터 오브젝트들</param>
        public async Task SpawnMonstersSequentially(List<GameObject> monsterObjects)
        {
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: SpawnMonstersSequentially 호출됨 - 리스트: {(monsterObjects == null ? "null" : monsterObjects.Count.ToString())}, isSpawning: {isSpawning}");
            
            if (isSpawning)
            {
                Debug.LogWarning("MonsterSpawnAnimationManager: 이미 몬스터 스폰 중입니다.");
                return;
            }
            
            if (monsterObjects == null || monsterObjects.Count == 0)
            {
                Debug.LogWarning("MonsterSpawnAnimationManager: 스폰할 몬스터가 없습니다.");
                return;
            }
            
            // 각 몬스터의 상태 확인 및 임시 활성화
            for (int i = 0; i < monsterObjects.Count; i++)
            {
                var monster = monsterObjects[i];
                if (monster != null)
                {
                    Debug.Log($"MonsterSpawnAnimationManager: 몬스터 {i} - {monster.name}, 활성화: {monster.activeInHierarchy}, 위치: {monster.transform.position}");
                    
      
                }
                else
                {
                    Debug.LogWarning($"MonsterSpawnAnimationManager: 몬스터 {i}가 null입니다.");
                }
            }
            
            isSpawning = true;
            activeSpawnTweens.Clear();
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: {monsterObjects.Count}마리 몬스터 스폰 시작");
            
            try
            {
                if (debugMode)
                    Debug.Log($"MonsterSpawnAnimationManager: for 루프 시작 - Count: {monsterObjects.Count}");
                
                for (int i = 0; i < monsterObjects.Count; i++)
                {
                    if (monsterObjects[i] != null)
                    {
                        // 각 몬스터를 순차적으로 스폰
                        await SpawnSingleMonster(monsterObjects[i], i);
                        
                        // 다음 몬스터까지 대기
                        if (i < monsterObjects.Count - 1)
                        {
                            await Task.Delay((int)(spawnInterval * 1000));
                        }
                    }
                }
                
                OnAllMonstersSpawned?.Invoke();
                
                if (debugMode)
                    Debug.Log("MonsterSpawnAnimationManager: 모든 몬스터 스폰 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MonsterSpawnAnimationManager: 몬스터 스폰 중 오류 발생 - {e.Message}");
            }
            finally
            {
                isSpawning = false;
                activeSpawnTweens.Clear();
            }
        }
        
        /// <summary>
        /// 단일 몬스터 스폰 애니메이션
        /// </summary>
        /// <param name="monsterObject">스폰할 몬스터 오브젝트</param>
        /// <param name="spawnIndex">스폰 순서</param>
        private async Task SpawnSingleMonster(GameObject monsterObject, int spawnIndex)
        {
            if (monsterObject == null) return;
            
            OnMonsterSpawnStarted?.Invoke(monsterObject);
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 몬스터 스폰 시작 - {monsterObject.name} (순서: {spawnIndex})");
            
            // 1. 초기 설정
            Vector3 finalPosition = monsterObject.transform.position;
            Vector3 spawnPosition = finalPosition + Vector3.up * entranceHeight;
            
            // 몬스터를 스폰 위치로 이동하고 숨김
            monsterObject.transform.position = spawnPosition;
            monsterObject.transform.localScale = Vector3.zero;
            
            // 몬스터를 활성화 (이미 활성화되어 있을 수도 있음)
            monsterObject.SetActive(true);
            
            activeSpawnTweens.Add(monsterObject);
            
            // 2. 스폰 파티클 효과 (등장 전)
            if (enableSpawnParticles && spawnParticlePrefab != null)
            {
                CreateSpawnParticle(finalPosition);
            }
            
            // 3. 몬스터 등장 애니메이션 시퀀스
            Sequence spawnSequence = DOTween.Sequence();
            
            // 3-1. 크기 애니메이션 (서서히 나타남)
            spawnSequence.Append(monsterObject.transform.DOScale(Vector3.one, spawnDuration * 0.4f)
                .SetEase(Ease.OutBack));
            
            // 3-2. 위치 애니메이션 (하늘에서 떨어짐)
            spawnSequence.Join(monsterObject.transform.DOMove(finalPosition, spawnDuration)
                .SetEase(spawnEase));
            
            // 3-3. 착지 효과 (그라운드 슬램)
            if (enableGroundSlam)
            {
                spawnSequence.AppendCallback(() => CreateGroundSlamEffect(monsterObject));
            }
            
            // 3-4. 화면 흔들림 효과
            if (enableScreenShake && mainCamera != null)
            {
                spawnSequence.AppendCallback(() => CreateScreenShakeEffect());
            }
            
            // 3-5. 사운드 효과
            if (enableSpawnSound)
            {
                spawnSequence.AppendCallback(() => PlaySpawnSound(monsterObject));
            }
            
            // 3-6. 몬스터별 고유 등장 애니메이션
            spawnSequence.AppendCallback(() => PlayMonsterSpecificAnimation(monsterObject));
            
            // 4. 완료 콜백
            spawnSequence.OnComplete(() => {
                OnMonsterSpawnCompleted?.Invoke(monsterObject);
                activeSpawnTweens.Remove(monsterObject);
                
                if (debugMode)
                    Debug.Log($"MonsterSpawnAnimationManager: 몬스터 스폰 완료 - {monsterObject.name}");
            });
            
            // 5. 애니메이션 재생
            spawnSequence.Play();
            
            // 6. 애니메이션 완료까지 대기
            await spawnSequence.AsyncWaitForCompletion();
        }
        
        /// <summary>
        /// 스폰 파티클 효과 생성
        /// </summary>
        private void CreateSpawnParticle(Vector3 position)
        {
            if (spawnParticlePrefab != null)
            {
                GameObject particle = Instantiate(spawnParticlePrefab, position, Quaternion.identity);
                
                // 3초 후 파티클 제거
                DOVirtual.DelayedCall(3f, () => {
                    if (particle != null) Destroy(particle);
                });
                
                if (debugMode)
                    Debug.Log($"MonsterSpawnAnimationManager: 스폰 파티클 생성 - {position}");
            }
        }
        
        /// <summary>
        /// 그라운드 슬램 효과 생성
        /// </summary>
        private void CreateGroundSlamEffect(GameObject monster)
        {
            if (monster == null) return;
            
            // 착지 시 바운스 효과
            Sequence slamSequence = DOTween.Sequence();
            
            // 약간 위로 튀어올랐다가
            slamSequence.Append(monster.transform.DOMoveY(monster.transform.position.y + slamBounceHeight, slamDuration * 0.3f)
                .SetEase(Ease.OutQuad));
            
            // 다시 원래 위치로 (바운스 효과)
            slamSequence.Append(monster.transform.DOMoveY(monster.transform.position.y, slamDuration * 0.7f)
                .SetEase(Ease.OutBounce));
            
            // 펀치 스케일 효과 (착지감 강조)
            slamSequence.Join(monster.transform.DOPunchScale(Vector3.one * 0.2f, slamDuration, slamVibrato, 0.5f));
            
            slamSequence.Play();
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 그라운드 슬램 효과 - {monster.name}");
        }
        
        /// <summary>
        /// 화면 흔들림 효과 생성
        /// </summary>
        private void CreateScreenShakeEffect()
        {
            if (mainCamera != null)
            {
                mainCamera.transform.DOShakePosition(shakeDuration, shakeStrength, 10, 90f, false, true);
                
                if (debugMode)
                    Debug.Log("MonsterSpawnAnimationManager: 화면 흔들림 효과");
            }
        }
        
        /// <summary>
        /// 스폰 사운드 재생
        /// </summary>
        private void PlaySpawnSound(GameObject monster)
        {
                         // AudioManager를 통해 사운드 재생
             if (Maglin.Audio.AudioManager.Instance != null)
             {
                 // 몬스터 타입에 따라 다른 사운드 재생
                 var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
                 if (enemy != null && enemy.EnemyData != null)
                 {
                     // AudioManager.Instance.PlaySFX($"MonsterSpawn_{enemy.EnemyData.Type}");
                 }
                 else
                 {
                     // AudioManager.Instance.PlaySFX("MonsterSpawn_Default");
                 }
             }
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 스폰 사운드 재생 - {monster.name}");
        }
        
        /// <summary>
        /// 몬스터별 고유 등장 애니메이션
        /// </summary>
                 private void PlayMonsterSpecificAnimation(GameObject monster)
         {
             var enemy = monster.GetComponent<Maglin.Enemy.Enemy>();
             if (enemy == null || enemy.EnemyData == null) return;
            
                         // 몬스터 타입에 따라 다른 등장 애니메이션
             switch (enemy.EnemyData.Type)
             {
                 case EnemyType.Normal:
                     PlayNormalMonsterAnimation(monster);
                     break;
                 case EnemyType.Elite:
                     PlayEliteMonsterAnimation(monster);
                     break;
                 case EnemyType.Boss:
                     PlayBossMonsterAnimation(monster);
                     break;
                 default:
                     PlayDefaultMonsterAnimation(monster);
                     break;
             }
        }
        
        /// <summary>
        /// 일반 몬스터 애니메이션
        /// </summary>
        private void PlayNormalMonsterAnimation(GameObject monster)
        {
            // 가벼운 바운스 효과
            monster.transform.DOPunchScale(Vector3.one * 0.1f, 0.5f, 2, 0.3f);
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 일반 몬스터 애니메이션 - {monster.name}");
        }
        
        /// <summary>
        /// 엘리트 몬스터 애니메이션
        /// </summary>
        private void PlayEliteMonsterAnimation(GameObject monster)
        {
            Sequence eliteSequence = DOTween.Sequence();
            
            // 강력한 등장 효과
            eliteSequence.Append(monster.transform.DOPunchScale(Vector3.one * 0.3f, 0.8f, 3, 0.5f));
            
            // 회전 효과
            eliteSequence.Join(monster.transform.DORotate(new Vector3(0, 0, 360), 1f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            
            eliteSequence.Play();
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 엘리트 몬스터 애니메이션 - {monster.name}");
        }
        
        /// <summary>
        /// 보스 몬스터 애니메이션
        /// </summary>
        private void PlayBossMonsterAnimation(GameObject monster)
        {
            Sequence bossSequence = DOTween.Sequence();
            
            // 강력한 등장 효과
            bossSequence.Append(monster.transform.DOPunchScale(Vector3.one * 0.5f, 1.2f, 4, 0.7f));
            
            // 위협적인 흔들림
            bossSequence.Join(monster.transform.DOShakeRotation(1f, new Vector3(0, 0, 30), 10, 90f, true));
            
            // 추가 화면 흔들림
            if (mainCamera != null)
            {
                bossSequence.Join(mainCamera.transform.DOShakePosition(1f, shakeStrength * 2f, 10, 90f, false, true));
            }
            
            bossSequence.Play();
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 보스 몬스터 애니메이션 - {monster.name}");
        }
        
        /// <summary>
        /// 기본 몬스터 애니메이션
        /// </summary>
        private void PlayDefaultMonsterAnimation(GameObject monster)
        {
            // 기본 바운스 효과
            monster.transform.DOPunchScale(Vector3.one * 0.15f, 0.6f, 2, 0.4f);
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 기본 몬스터 애니메이션 - {monster.name}");
        }
        
        /// <summary>
        /// 몬스터 사라짐 애니메이션 (선택적)
        /// </summary>
        public async Task DespawnMonster(GameObject monster)
        {
            if (monster == null) return;
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 몬스터 사라짐 애니메이션 시작 - {monster.name}");
            
            Sequence despawnSequence = DOTween.Sequence();
            
            // 크기 축소와 페이드 아웃
            despawnSequence.Append(monster.transform.DOScale(Vector3.zero, 0.5f)
                .SetEase(Ease.InBack));
            
            // 약간 위로 올라가면서 사라짐
            despawnSequence.Join(monster.transform.DOMoveY(monster.transform.position.y + 1f, 0.5f)
                .SetEase(Ease.OutQuad));
            
            // 스프라이트 페이드 아웃
            var spriteRenderer = monster.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                despawnSequence.Join(spriteRenderer.DOFade(0f, 0.5f));
            }
            
            despawnSequence.Play();
            await despawnSequence.AsyncWaitForCompletion();
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 몬스터 사라짐 애니메이션 완료 - {monster.name}");
        }
        
        /// <summary>
        /// 현재 스폰 중인지 확인
        /// </summary>
        public bool IsSpawning => isSpawning;
        
        /// <summary>
        /// 모든 스폰 애니메이션 중단
        /// </summary>
        public void StopAllSpawnAnimations()
        {
            foreach (var monsterObject in activeSpawnTweens.ToArray())
            {
                if (monsterObject != null)
                {
                    monsterObject.transform.DOKill();
                }
            }
            
            activeSpawnTweens.Clear();
            isSpawning = false;
            
            if (debugMode)
                Debug.Log("MonsterSpawnAnimationManager: 모든 스폰 애니메이션 중단");
        }
        
        /// <summary>
        /// 애니메이션 설정 업데이트
        /// </summary>
        public void UpdateAnimationSettings(float duration, float interval, Ease ease, float height)
        {
            spawnDuration = duration;
            spawnInterval = interval;
            spawnEase = ease;
            entranceHeight = height;
            
            if (debugMode)
                Debug.Log($"MonsterSpawnAnimationManager: 애니메이션 설정 업데이트 - Duration: {duration}, Interval: {interval}, Ease: {ease}, Height: {height}");
        }
        
        private void OnDestroy()
        {
            // 모든 DOTween 애니메이션 정리
            transform.DOKill();
            StopAllSpawnAnimations();
        }
    }
} 