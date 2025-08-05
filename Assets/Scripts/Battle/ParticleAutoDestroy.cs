using UnityEngine;

namespace Maglin.Battle
{
    /// <summary>
    /// 파티클 자동 파괴 컴포넌트
    /// </summary>
    public class ParticleAutoDestroy : MonoBehaviour
    {
        [SerializeField] private float lifetime = 2.0f;
        
        private void Start()
        {
            Destroy(gameObject, lifetime);
        }
        
        public void SetLifetime(float time)
        {
            lifetime = time;
        }
    }
} 