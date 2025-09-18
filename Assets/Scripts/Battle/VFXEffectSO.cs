using UnityEngine;
using Maglin.Cards;

namespace Maglin.Battle
{
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
        }
#endif
    }
}
