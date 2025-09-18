using UnityEngine;
using Maglin.Cards;

namespace Maglin.Battle
{
    /// <summary>
    /// 투사체 움직임 타입
    /// </summary>
    public enum ProjectileMovementType
    {
        Linear,      // 선형 움직임 (일정한 속도)
        Accelerate   // 가속 움직임 (느리게 시작해서 빨라짐)
    }

    /// <summary>
    /// 개별 투사체 설정 정보
    /// </summary>
    [System.Serializable]
    public struct ProjectileConfig
    {
        [Header("타이밍 설정")]
        [SerializeField] private float launchDelay;                // 투사체 발사 딜레이 (초)

        [Header("생성 애니메이션 설정")]
        [SerializeField] private bool enableSpawnAnimation;  // 솟아오르는 생성 애니메이션 사용 여부
        [SerializeField] private float spawnAnimationDuration; // 솟아오르는 애니메이션 지속 시간
        [SerializeField] private float spawnHeightOffset;   // 시작 위치 오프셋 (음수면 아래에서 시작)

        [Header("투사체 설정")]
        [SerializeField] private float speed;                      // 투사체 속도
        [SerializeField] private Vector3 startOffset;              // 시작 위치 오프셋 (플레이어 중심 기준)
        [SerializeField] private Vector3 scale;                    // 투사체 스케일

        [Header("움직임 설정")]
        [SerializeField] private ProjectileMovementType movementType; // 움직임 타입
        [SerializeField] private float accelerationFactor;         // 가속 배율 (Accelerate 타입용)
        [SerializeField] private float initialSpeedRatio;          // 초기 속도 비율 (Accelerate 타입용)

        [Header("회전 설정")]
        [SerializeField] private bool rotate;                      // 회전 여부
        [SerializeField] private float rotateAmount;               // 회전량

        public float LaunchDelay => launchDelay;
        public bool EnableSpawnAnimation => enableSpawnAnimation;
        public float SpawnAnimationDuration => spawnAnimationDuration;
        public float SpawnHeightOffset => spawnHeightOffset;
        public float Speed => speed;
        public Vector3 StartOffset => startOffset;
        public Vector3 Scale => scale;
        public ProjectileMovementType MovementType => movementType;
        public float AccelerationFactor => accelerationFactor;
        public float InitialSpeedRatio => initialSpeedRatio;
        public bool Rotate => rotate;
        public float RotateAmount => rotateAmount;

        public ProjectileConfig(float launchDelay = 0f, bool enableSpawnAnimation = true, float spawnAnimationDuration = 0.3f, float spawnHeightOffset = -0.5f,
                               float speed = 10f, Vector3 startOffset = default, Vector3 scale = default,
                               ProjectileMovementType movementType = ProjectileMovementType.Linear,
                               float accelerationFactor = 2f, float initialSpeedRatio = 0.2f,
                               bool rotate = false, float rotateAmount = 45f)
        {
            this.launchDelay = launchDelay;
            this.enableSpawnAnimation = enableSpawnAnimation;
            this.spawnAnimationDuration = spawnAnimationDuration == 0f ? 0.3f : spawnAnimationDuration;
            this.spawnHeightOffset = spawnHeightOffset == 0f ? -0.5f : spawnHeightOffset;
            this.speed = speed == 0f ? 10f : speed;
            this.startOffset = startOffset;
            this.scale = scale == Vector3.zero ? Vector3.one : scale;
            this.movementType = movementType;
            this.accelerationFactor = accelerationFactor == 0f ? 2f : accelerationFactor;
            this.initialSpeedRatio = initialSpeedRatio == 0f ? 0.2f : initialSpeedRatio;
            this.rotate = rotate;
            this.rotateAmount = rotateAmount == 0f ? 45f : rotateAmount;
        }
    }

    /// <summary>
    /// VFX 히트 타이밍 정보
    /// </summary>
    [System.Serializable]
    public struct HitTiming
    {
        [Header("히트 타이밍")]
        [SerializeField] private float delay;           // 이펙트 시작 후 몇 초 뒤에 히트
        [SerializeField] private bool isDamageHit;      // 데미지를 주는 히트인지 여부
        [SerializeField] private float damageMultiplier; // 데미지 배율 (1.0이 기본)

        public float Delay => delay;
        public bool IsDamageHit => isDamageHit;
        public float DamageMultiplier => damageMultiplier;

        public HitTiming(float delay, bool isDamageHit = true, float damageMultiplier = 1.0f)
        {
            this.delay = delay;
            this.isDamageHit = isDamageHit;
            this.damageMultiplier = damageMultiplier;
        }
    }

    /// <summary>
    /// VFX 이펙트 데이터를 관리하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "New VFX Effect", menuName = "Maglin/Battle/VFX Effect")]
    public class VFXEffectSO : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string effectName;
        [SerializeField] private string description;

        [Header("이펙트 설정")]
        [SerializeField] private GameObject effectPrefab;           // 이펙트 프리팹
        [SerializeField] private float effectDuration = 2f;        // 이펙트 지속시간
        [SerializeField] private bool autoDestroy = true;          // 자동 삭제 여부

        [Header("히트 타이밍")]
        [SerializeField] private HitTiming[] hitTimings;           // 히트 타이밍 배열

        [Header("타겟팅 오버라이드")]
        [SerializeField] private bool overrideTargeting = false;   // 카드의 타겟팅을 오버라이드할지
        [SerializeField] private TargetType customTargetType;      // 커스텀 타겟 타입
        [SerializeField] private int customTargetCount = 1;        // 커스텀 타겟 수

        [Header("위치 설정")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;  // 위치 오프셋
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;  // 회전 오프셋
        [SerializeField] private Vector3 scale = Vector3.one;            // 스케일

        [Header("사운드")]
        [SerializeField] private AudioClip soundEffect;           // 이펙트 사운드

        [Header("투사체 설정")]
        [SerializeField] private bool isProjectile = false;       // 투사체인지 여부
        [SerializeField] private GameObject projectileHitEffect;  // 투사체 충돌 시 이펙트
        [SerializeField] private AudioClip projectileHitSound;    // 투사체 충돌 사운드

        [Header("다중 투사체 설정")]
        [SerializeField] private ProjectileConfig[] projectileConfigs = new ProjectileConfig[1]; // 투사체 설정 배열

        // Properties
        public string EffectName => effectName;
        public string Description => description;
        public GameObject EffectPrefab => effectPrefab;
        public float EffectDuration => effectDuration;
        public bool AutoDestroy => autoDestroy;
        public HitTiming[] HitTimings => hitTimings;
        public bool OverrideTargeting => overrideTargeting;
        public TargetType CustomTargetType => customTargetType;
        public int CustomTargetCount => customTargetCount;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 Scale => scale;
        public AudioClip SoundEffect => soundEffect;

        // 투사체 관련 Properties
        public bool IsProjectile => isProjectile;
        public GameObject ProjectileHitEffect => projectileHitEffect;
        public AudioClip ProjectileHitSound => projectileHitSound;
        public ProjectileConfig[] ProjectileConfigs => projectileConfigs;

        // 투사체 개수
        public int ProjectileCount => isProjectile && projectileConfigs != null ? projectileConfigs.Length : 0;

        // 유효한 투사체 설정이 있는지 확인
        public bool HasValidProjectileConfigs => isProjectile && projectileConfigs != null && projectileConfigs.Length > 0;

        // 하위 호환성을 위한 레거시 Properties (첫 번째 투사체 설정 반환)
        public float ProjectileSpeed => HasValidProjectileConfigs ? projectileConfigs[0].Speed : 10f;
        public bool ProjectileRotate => HasValidProjectileConfigs ? projectileConfigs[0].Rotate : false;
        public float ProjectileRotateAmount => HasValidProjectileConfigs ? projectileConfigs[0].RotateAmount : 45f;
        public Vector3 ProjectileStartOffset => HasValidProjectileConfigs ? projectileConfigs[0].StartOffset : Vector3.zero;
        public Vector3 ProjectileScale => HasValidProjectileConfigs ? projectileConfigs[0].Scale : Vector3.one;
        public ProjectileMovementType MovementType => HasValidProjectileConfigs ? projectileConfigs[0].MovementType : ProjectileMovementType.Linear;
        public float AccelerationFactor => HasValidProjectileConfigs ? projectileConfigs[0].AccelerationFactor : 2f;
        public float InitialSpeedRatio => HasValidProjectileConfigs ? projectileConfigs[0].InitialSpeedRatio : 0.2f;

        /// <summary>
        /// 유효한 히트 타이밍이 있는지 확인
        /// </summary>
        public bool HasValidHitTimings => hitTimings != null && hitTimings.Length > 0;

        /// <summary>
        /// 데미지를 주는 히트 타이밍들만 반환
        /// </summary>
        public HitTiming[] GetDamageHitTimings()
        {
            if (!HasValidHitTimings) return new HitTiming[0];

            var damageHits = new System.Collections.Generic.List<HitTiming>();
            foreach (var timing in hitTimings)
            {
                if (timing.IsDamageHit)
                {
                    damageHits.Add(timing);
                }
            }
            return damageHits.ToArray();
        }

        /// <summary>
        /// 특정 시점의 히트 타이밍 반환
        /// </summary>
        public HitTiming? GetHitTimingAt(float time, float tolerance = 0.1f)
        {
            if (!HasValidHitTimings) return null;

            foreach (var timing in hitTimings)
            {
                if (Mathf.Abs(timing.Delay - time) <= tolerance)
                {
                    return timing;
                }
            }
            return null;
        }

        /// <summary>
        /// 이펙트가 유효한지 확인
        /// </summary>
        public bool IsValid => effectPrefab != null;

        /// <summary>
        /// 실제 사용할 타겟 타입 반환 (오버라이드 고려)
        /// </summary>
        public TargetType GetTargetType(TargetType cardTargetType)
        {
            return overrideTargeting ? customTargetType : cardTargetType;
        }

        /// <summary>
        /// 실제 사용할 타겟 수 반환 (오버라이드 고려)
        /// </summary>
        public int GetTargetCount(int cardTargetCount)
        {
            return overrideTargeting ? customTargetCount : cardTargetCount;
        }

        /// <summary>
        /// 특정 인덱스의 투사체 설정 반환
        /// </summary>
        public ProjectileConfig GetProjectileConfig(int index)
        {
            if (!HasValidProjectileConfigs || index < 0 || index >= projectileConfigs.Length)
            {
                return new ProjectileConfig(); // 기본값 반환
            }
            return projectileConfigs[index];
        }

        /// <summary>
        /// 모든 투사체 설정 반환 (복사본)
        /// </summary>
        public ProjectileConfig[] GetAllProjectileConfigs()
        {
            if (!HasValidProjectileConfigs) return new ProjectileConfig[0];

            var configs = new ProjectileConfig[projectileConfigs.Length];
            System.Array.Copy(projectileConfigs, configs, projectileConfigs.Length);
            return configs;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 유효성 검사
        /// </summary>
        private void OnValidate()
        {
            // 히트 타이밍 정렬 (시간 순서대로)
            if (hitTimings != null && hitTimings.Length > 1)
            {
                System.Array.Sort(hitTimings, (a, b) => a.Delay.CompareTo(b.Delay));
            }

            // 이펙트 지속시간이 0보다 큰지 확인
            if (effectDuration <= 0f)
            {
                effectDuration = 1f;
            }

            // 스케일이 0이면 1로 설정
            if (scale == Vector3.zero)
            {
                scale = Vector3.one;
            }

            // 투사체 설정 배열 유효성 검사
            if (isProjectile && (projectileConfigs == null || projectileConfigs.Length == 0))
            {
                projectileConfigs = new ProjectileConfig[1];
                projectileConfigs[0] = new ProjectileConfig(
                    launchDelay: 0f,
                    enableSpawnAnimation: true,
                    spawnAnimationDuration: 0.3f,
                    spawnHeightOffset: -0.5f,
                    speed: 10f,
                    startOffset: Vector3.zero,
                    scale: Vector3.one
                ); // 기본값으로 초기화
            }
            
            // 투사체 설정 배열의 각 항목 유효성 검사
            if (projectileConfigs != null)
            {
                for (int i = 0; i < projectileConfigs.Length; i++)
                {
                    var config = projectileConfigs[i];
                    bool needsUpdate = false;
                    
                    // 기본값이 필요한 필드들 확인
                    var newConfig = config;
                    
                    if (config.Scale == Vector3.zero)
                    {
                        needsUpdate = true;
                    }
                    
                    if (config.Speed == 0f)
                    {
                        needsUpdate = true;
                    }
                    
                    if (config.SpawnAnimationDuration == 0f)
                    {
                        needsUpdate = true;
                    }
                    
                    if (needsUpdate)
                    {
                        projectileConfigs[i] = new ProjectileConfig(
                            config.LaunchDelay,
                            config.EnableSpawnAnimation,
                            config.SpawnAnimationDuration == 0f ? 0.3f : config.SpawnAnimationDuration,
                            config.SpawnHeightOffset == 0f ? -0.5f : config.SpawnHeightOffset,
                            config.Speed == 0f ? 10f : config.Speed,
                            config.StartOffset,
                            config.Scale == Vector3.zero ? Vector3.one : config.Scale,
                            config.MovementType,
                            config.AccelerationFactor == 0f ? 2f : config.AccelerationFactor,
                            config.InitialSpeedRatio == 0f ? 0.2f : config.InitialSpeedRatio,
                            config.Rotate,
                            config.RotateAmount == 0f ? 45f : config.RotateAmount
                        );
                    }
                }
            }
        }
#endif
    }
}
