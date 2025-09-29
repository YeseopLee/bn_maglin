using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    public Transform target;      // 따라갈 오브젝트
    public Vector3 offset = new Vector3(0, 2, -5);  // 오브젝트로부터의 상대 위치

    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target); // 카메라가 항상 타겟을 바라보게
        }
    }
}