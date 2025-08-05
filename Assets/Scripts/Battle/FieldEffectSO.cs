using UnityEngine;
using Maglin.Cards;

namespace Maglin.Battle
{
    [CreateAssetMenu(fileName = "New Field Effect", menuName = "Maglin/Battle/FieldEffectSO")]
    public class FieldEffectSO : ScriptableObject
    {
        [Header("필드 효과 기본 정보")]
        [SerializeField] private string effectName;
        [SerializeField] private ElementType fieldElement;
        [SerializeField] private string effectDescription;

        [Header("시각적 효과")]
        [SerializeField] private Color fieldBackgroundColor = Color.white;
        [SerializeField] private Sprite fieldIcon;
        [SerializeField] private GameObject fieldVisualEffect;

        [Header("지속 시간")]
        [SerializeField] private int duration = -1; // -1이면 영구, 0 이상이면 턴 수

        // Properties
        public string EffectName => effectName;
        public ElementType FieldElement => fieldElement;
        public string Description => effectDescription;
        public Color BackgroundColor => fieldBackgroundColor;
        public Sprite Icon => fieldIcon;
        public GameObject VisualEffect => fieldVisualEffect;
        public int Duration => duration;

        /// <summary>
        /// 영구 지속 효과인지 확인
        /// </summary>
        public bool IsPermanent => duration == -1;
    }
}