using UnityEngine;

namespace Maglin.Battle
{
    /// <summary>
    /// 몬스터가 이번 턴에 외부 요청으로 이동했는지 추적하는 컴포넌트
    /// AI 이동과 카드 효과 이동의 충돌을 방지하기 위함
    /// </summary>
    public class MonsterMoveFlag : MonoBehaviour
    {
        [SerializeField] private bool movedThisTurn = false;

        /// <summary>
        /// 이번 턴에 이동했는지 반환
        /// </summary>
        public bool HasMovedThisTurn()
        {
            return movedThisTurn;
        }

        /// <summary>
        /// 이번 턴 이동 상태 설정
        /// </summary>
        public void SetMovedThisTurn(bool moved)
        {
            movedThisTurn = moved;
        }

        /// <summary>
        /// 이동 플래그 초기화
        /// </summary>
        public void ResetMoveFlag()
        {
            movedThisTurn = false;
        }
    }
}
