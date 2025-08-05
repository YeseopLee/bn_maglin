using UnityEngine;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터 배치 정보
    /// </summary>
    [System.Serializable]
    public class EnemyPlacement
    {
        [Header("몬스터 정보")]
        public EnemySO enemyData;           // 배치할 몬스터
        public int startingPosition;       // 시작 위치 (1~10)

        [Header("배치 설정")]
        public bool isFixed = false;       // 고정 위치인지 (false면 오른쪽 끝에서 시작)
        public int customHealth = -1;      // 커스텀 체력 (-1이면 기본값 사용)
    }

    /// <summary>
    /// 중립 오브젝트 배치 정보
    /// </summary>
    [System.Serializable]
    public class ObjectPlacement
    {
        [Header("오브젝트 정보")]
        public GameObject objectPrefab;     // 배치할 오브젝트 프리팹
        public int position;                // 배치 위치 (1~10)
        public string objectName;          // 오브젝트 이름

        [Header("오브젝트 속성")]
        public int objectHealth = 1;       // 오브젝트 체력
        public bool isDestructible = true; // 파괴 가능 여부
    }

    [CreateAssetMenu(fileName = "New Battle", menuName = "Maglin/Battle/BattleSO")]
    public class BattleSO : ScriptableObject
    {
        [Header("전투 기본 정보")]
        [SerializeField] private string battleName;
        [SerializeField] private string battleDescription;

        [Header("몬스터 배치")]
        [SerializeField] private EnemyPlacement[] enemyPlacements;

        [Header("중립 오브젝트 배치")]
        [SerializeField] private ObjectPlacement[] objectPlacements;

        [Header("필드 설정")]
        [SerializeField] private FieldEffectSO initialFieldEffect;   // 시작 필드 속성

        [Header("보상 설정")]
        [SerializeField] private int goldReward;
        [SerializeField] private int baseExperienceReward;

        [Header("특수 설정")]
        [SerializeField] private bool hasCustomMechanics = false;    // 커스텀 전투 메커니즘 여부
        [TextArea(2, 4)]
        [SerializeField] private string customMechanicsDescription;

        // Properties
        public string BattleName => battleName;
        public string Description => battleDescription;
        public EnemyPlacement[] EnemyPlacements => enemyPlacements;
        public ObjectPlacement[] ObjectPlacements => objectPlacements;
        public FieldEffectSO InitialFieldEffect => initialFieldEffect;
        public int GoldReward => goldReward;
        public int BaseExperienceReward => baseExperienceReward;
        public bool HasCustomMechanics => hasCustomMechanics;
        public string CustomMechanicsDescription => customMechanicsDescription;

        /// <summary>
        /// 전투에 참가하는 몬스터 수
        /// </summary>
        public int EnemyCount => enemyPlacements?.Length ?? 0;

        /// <summary>
        /// 배치된 중립 오브젝트 수
        /// </summary>
        public int ObjectCount => objectPlacements?.Length ?? 0;

        /// <summary>
        /// 보스 전투인지 확인 (보스 몬스터가 포함된 전투)
        /// </summary>
        public bool IsBossBattle
        {
            get
            {
                if (enemyPlacements == null) return false;

                foreach (var placement in enemyPlacements)
                {
                    if (placement.enemyData != null && placement.enemyData.IsBoss)
                        return true;
                }
                return false;
            }
        }
    }
}