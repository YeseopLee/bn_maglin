using UnityEngine;
using System.Collections;
using Maglin.Cards;
using Maglin.Enemy;

namespace Maglin.Battle
{
    /// <summary>
    /// 게임 전용 투사체 컨트롤러
    /// 플레이어에서 타겟 몬스터로 정확하게 날아가는 투사체를 관리
    /// </summary>
    public class ProjectileController : MonoBehaviour
    {
        [Header("투사체 설정")]
        [SerializeField] private float speed = 10f;
        [SerializeField] private bool rotateProjectile = false;
        [SerializeField] private float rotateAmount = 45f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private AudioClip hitSound;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true; // 디버그 모드 기본 활성화

        // 내부 변수
        private Transform target;
        private Vector3 direction;
        private bool hasHit = false;
        private bool hitEffectCreated = false; // 히트 이펙트 생성 여부 추적
        private Rigidbody rb;
        private VFXEffectSO vfxData;
        private Card sourceCard;
        private System.Action<ProjectileController> onHitCallback;
        private GameObject customHitEffectPrefab; // ProjectileMoveScript에서 가져온 hit prefab
        private ProjectileConfig currentProjectileConfig; // 현재 사용중인 투사체 설정

        // 움직임 관련 변수
        private ProjectileMovementType movementType;
        private float baseSpeed;
        private float currentSpeed;
        private float accelerationFactor;
        private float launchTime;
        private Vector3 startPosition;

        // 이벤트
        public System.Action<ProjectileController, Transform> OnProjectileHit;

        private void Start()
        {
            // Rigidbody 컴포넌트 확인 및 추가
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.Log("[ProjectileController] 3D Rigidbody 추가");
                rb = gameObject.AddComponent<Rigidbody>();
            }

            // 3D Rigidbody 설정
            rb.useGravity = false;
            rb.drag = 0f;
            rb.angularDrag = 0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 2D Rigidbody도 추가 (3D Collider가 없는 경우만)
            Rigidbody2D rb2D = GetComponent<Rigidbody2D>();
            Collider existingCollider3D = GetComponent<Collider>();

            if (rb2D == null && existingCollider3D == null)
            {
                try
                {
                    Debug.Log("[ProjectileController] 2D Rigidbody 추가");
                    rb2D = gameObject.AddComponent<Rigidbody2D>();

                    // 2D Rigidbody 설정
                    rb2D.gravityScale = 0f;
                    rb2D.drag = 0f;
                    rb2D.angularDrag = 0f;
                    rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ProjectileController] 2D Rigidbody 추가 실패: {e.Message}");
                }
            }
            else if (rb2D != null)
            {
                // 기존 2D Rigidbody가 있으면 설정만 업데이트
                rb2D.gravityScale = 0f;
                rb2D.drag = 0f;
                rb2D.angularDrag = 0f;
                rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            // Collider 확인 및 설정 (VFXEffectManager에서 이미 처리되므로 여기서는 확인만)
            Collider collider3D = GetComponent<Collider>();
            Collider2D collider2D = GetComponent<Collider2D>();

            if (collider3D != null)
            {
                collider3D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[ProjectileController] 3D Collider 확인: {collider3D.GetType().Name}");
            }

            if (collider2D != null)
            {
                collider2D.isTrigger = true;
                if (debugMode)
                    Debug.Log($"[ProjectileController] 2D Collider 확인: {collider2D.GetType().Name}");
            }

            if (collider3D == null && collider2D == null)
            {
                Debug.LogWarning("[ProjectileController] Collider가 없습니다! VFXEffectManager에서 설정되어야 합니다.");
            }
        }

        private void FixedUpdate()
        {
            if (hasHit) return;

            // 투사체 이동
            if (target != null && !hasHit)
            {
                // 타겟을 향한 방향 업데이트 (실시간 추적)
                direction = (target.position - transform.position).normalized;

                // 움직임 타입에 따른 속도 업데이트
                UpdateSpeedByMovementType();

                // 백업: Transform으로 직접 이동
                Vector3 movement = direction * currentSpeed * Time.fixedDeltaTime;
                transform.position += movement;

                // Rigidbody가 있으면 velocity도 설정
                if (rb != null)
                {
                    rb.velocity = direction * currentSpeed;
                }

                // 2D Rigidbody로도 이동 (호환성)
                Rigidbody2D rb2DComp = GetComponent<Rigidbody2D>();
                if (rb2DComp != null)
                {
                    rb2DComp.velocity = new Vector2(direction.x, direction.y) * currentSpeed;
                }

                // 거리 기반 히트 감지 (Collider가 없거나 감지 실패 시 대비)
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget <= 0.8f) // 0.8유닛 이내에 도달하면 히트 처리 (더 넓게)
                {
                    if (debugMode)
                        Debug.Log($"[ProjectileController] 거리 기반 히트 감지: 거리={distanceToTarget:F2}");
                    HitTarget();
                    return;
                }

                // 디버그: 타겟과의 거리 지속 체크
                if (debugMode && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[ProjectileController] 타겟까지 거리: {distanceToTarget:F2} (타겟: {target.name})");
                    Debug.Log($"[ProjectileController] 투사체 위치: {transform.position}, 타겟 위치: {target.position}");

                    Rigidbody2D rb2DComponent = GetComponent<Rigidbody2D>();
                    Vector3 velocity3D = rb?.velocity ?? Vector3.zero;
                    Vector2 velocity2D = rb2DComponent?.velocity ?? Vector2.zero;
                    Debug.Log($"[ProjectileController] 투사체 속도: 3D={velocity3D} / 2D={velocity2D}");
                    Debug.Log($"[ProjectileController] 방향: {direction}, 속도: {speed}");
                }

                // 회전 처리
                if (rotateProjectile)
                {
                    transform.Rotate(0, 0, rotateAmount * Time.deltaTime, Space.Self);
                }
                else
                {
                    // 투사체가 날아가는 방향으로 회전
                    if (direction != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
            }
            else if (!hasHit)
            {
                // 타겟이 없으면 현재 방향으로 계속 이동
                UpdateSpeedByMovementType();

                Vector3 movement = direction * currentSpeed * Time.fixedDeltaTime;
                transform.position += movement;

                if (rb != null)
                {
                    rb.velocity = direction * currentSpeed;
                }

                Rigidbody2D rb2DComp = GetComponent<Rigidbody2D>();
                if (rb2DComp != null)
                {
                    rb2DComp.velocity = new Vector2(direction.x, direction.y) * currentSpeed;
                }
            }
        }

        /// <summary>
        /// 투사체 초기화 및 발사
        /// </summary>
        public void LaunchProjectile(Transform startPos, Transform targetPos, VFXEffectSO vfxEffectData, Card card, System.Action<ProjectileController> hitCallback = null, GameObject customHitPrefab = null, int configIndex = 0)
        {
            if (targetPos == null)
            {
                if (debugMode)
                    Debug.LogError("[ProjectileController] 타겟이 없습니다!");
                return;
            }

            // 초기 설정
            target = targetPos;
            vfxData = vfxEffectData;
            sourceCard = card;
            onHitCallback = hitCallback;
            customHitEffectPrefab = customHitPrefab;

            // VFX 데이터로부터 설정 적용
            if (vfxData != null)
            {
                // 개별 투사체 설정 가져오기 및 저장
                currentProjectileConfig = vfxData.GetProjectileConfig(configIndex);

                baseSpeed = currentProjectileConfig.Speed;
                speed = baseSpeed;
                rotateProjectile = currentProjectileConfig.Rotate;
                rotateAmount = currentProjectileConfig.RotateAmount;

                // Hit Effect 우선순위: customHitPrefab > VFXEffectSO의 ProjectileHitEffect
                hitEffectPrefab = customHitEffectPrefab != null ? customHitEffectPrefab : vfxData.ProjectileHitEffect;
                hitSound = vfxData.ProjectileHitSound;

                // 개별 투사체 스케일 적용
                transform.localScale = currentProjectileConfig.Scale;

                // ProjectileMoveScript 비활성화 (ProjectileController가 제어를 담당)
                DisableProjectileMoveScript();

                // 개별 움직임 타입 설정
                movementType = currentProjectileConfig.MovementType;
                accelerationFactor = currentProjectileConfig.AccelerationFactor;

                // 움직임 타입에 따른 초기 속도 설정
                switch (movementType)
                {
                    case ProjectileMovementType.Linear:
                        currentSpeed = baseSpeed;
                        break;
                    case ProjectileMovementType.Accelerate:
                        currentSpeed = baseSpeed * currentProjectileConfig.InitialSpeedRatio;
                        break;
                }

                if (debugMode)
                {
                    Debug.Log($"[ProjectileController] 투사체 설정 {configIndex} 적용:");
                    Debug.Log($"  - 속도: {baseSpeed}, 스케일: {currentProjectileConfig.Scale}");
                    Debug.Log($"  - 움직임 타입: {movementType}, 가속 배율: {accelerationFactor}");
                    Debug.Log($"  - 회전: {rotateProjectile} ({rotateAmount}도)");
                }
            }

            // 시작 위치 설정 (이미 VFXEffectManager에서 offset이 적용된 위치로 생성됨)
            if (startPos != null)
            {
                // 투사체는 이미 VFXEffectManager에서 offset이 적용된 위치에 생성되었으므로
                // 현재 위치를 시작 위치로 기록
                startPosition = transform.position;

                if (debugMode)
                    Debug.Log($"[ProjectileController] 투사체 시작 위치: {startPosition} (오프셋 적용됨)");
            }

            // 발사 시간 기록
            launchTime = Time.time;

            // Rigidbody 확인 (Start보다 먼저 호출될 수 있음)
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.drag = 0f;
                    rb.angularDrag = 0f;
                }
            }

            // 2D Rigidbody도 확인 (3D Collider가 있으면 추가하지 않음)
            Rigidbody2D rb2D = GetComponent<Rigidbody2D>();
            Collider existingCollider3D = GetComponent<Collider>();

            if (rb2D == null && existingCollider3D == null)
            {
                try
                {
                    rb2D = gameObject.AddComponent<Rigidbody2D>();
                    rb2D.gravityScale = 0f;
                    rb2D.drag = 0f;
                    rb2D.angularDrag = 0f;
                    if (debugMode)
                        Debug.Log("[ProjectileController] 2D Rigidbody 추가 성공");
                }
                catch (System.Exception e)
                {
                    if (debugMode)
                        Debug.LogWarning($"[ProjectileController] 2D Rigidbody 추가 실패: {e.Message}");
                }
            }
            else if (debugMode)
            {
                Debug.Log($"[ProjectileController] 2D Rigidbody 추가 스킵 - 3D Collider 존재: {existingCollider3D != null}");
            }

            // 초기 방향 계산
            direction = (target.position - transform.position).normalized;

            if (debugMode)
            {
                Debug.Log($"[ProjectileController] 투사체 발사: {sourceCard?.CardData?.CardName} -> {target.name}");
                Debug.Log($"[ProjectileController] 시작 위치: {transform.position}, 타겟 위치: {target.position}");
                Debug.Log($"[ProjectileController] 속도: {speed}, 방향: {direction}");
                Debug.Log($"[ProjectileController] 스케일: {transform.localScale}, 오프셋: {vfxData?.ProjectileStartOffset ?? Vector3.zero} (VFXEffectManager에서 적용됨)");
                Debug.Log($"[ProjectileController] Hit Effect: {(hitEffectPrefab != null ? hitEffectPrefab.name : "null")} (Custom: {customHitEffectPrefab != null}, VFX: {vfxData?.ProjectileHitEffect != null})");
                Debug.Log($"[ProjectileController] Rigidbody 상태: 3D={rb != null}, 2D={rb2D != null}");
                Debug.Log($"[ProjectileController] Collider 상태: 3D={GetComponent<Collider>() != null}, 2D={GetComponent<Collider2D>() != null}");
            }

            // 안전장치: 5초 후 자동 삭제
            StartCoroutine(DestroyProjectile(5f));
        }

        // 3D 충돌 감지
        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            if (debugMode)
                Debug.Log($"[ProjectileController] 3D 충돌 감지: {other.name}, 타겟: {target?.name}");

            CheckHit(other.transform);
        }

        // 2D 충돌 감지
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHit) return;

            if (debugMode)
                Debug.Log($"[ProjectileController] 2D 충돌 감지: {other.name}, 타겟: {target?.name}");

            CheckHit(other.transform);
        }

        /// <summary>
        /// 충돌한 오브젝트가 타겟인지 확인
        /// </summary>
        private void CheckHit(Transform hitTransform)
        {
            if (hasHit) return;

            // 타겟에 도달했는지 확인 (더 넓은 범위로)
            bool isTargetHit = false;

            // 직접적인 타겟 확인
            if (hitTransform == target)
            {
                isTargetHit = true;
            }
            // 부모-자식 관계 확인
            else if (hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform))
            {
                isTargetHit = true;
            }
            // 같은 게임오브젝트 확인
            else if (hitTransform.gameObject == target.gameObject)
            {
                isTargetHit = true;
            }
            // 몬스터 관련 컴포넌트가 있는지 확인
            else if (target != null && (
                hitTransform.GetComponent<Maglin.Enemy.Enemy>() != null ||
                hitTransform.GetComponentInParent<Maglin.Enemy.Enemy>() != null ||
                hitTransform.name.ToLower().Contains("monster") ||
                hitTransform.transform.root == target.transform.root))
            {
                isTargetHit = true;
            }

            if (isTargetHit)
            {
                if (debugMode)
                    Debug.Log($"[ProjectileController] 타겟 히트 확인됨: {hitTransform.name}");
                HitTarget();
            }
        }

        /// <summary>
        /// 타겟 히트 처리
        /// </summary>
        private void HitTarget()
        {
            if (hasHit) return;
            hasHit = true;

            if (debugMode)
                Debug.Log($"[ProjectileController] 타겟 히트: {target?.name}");

            // 즉시 투사체 이동 정지
            enabled = false;

            // 투사체 정지
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.isKinematic = true;
            }

            // 2D Rigidbody도 정지
            Rigidbody2D rb2DComponent = GetComponent<Rigidbody2D>();
            if (rb2DComponent != null)
            {
                rb2DComponent.velocity = Vector2.zero;
                rb2DComponent.isKinematic = true;
            }

            // 히트 이펙트 생성 (한 번만)
            if (hitEffectPrefab != null && !hitEffectCreated)
            {
                hitEffectCreated = true; // 플래그 설정

                Vector3 hitPosition = target.position;
                GameObject hitEffect = Instantiate(hitEffectPrefab, hitPosition, Quaternion.identity);

                // 개별 투사체 설정의 스케일을 히트 이펙트에 적용
                if (vfxData != null)
                {
                    // 루트 오브젝트에 스케일 적용
                    hitEffect.transform.localScale = currentProjectileConfig.Scale;

                    // 복잡한 이펙트의 경우 자식 오브젝트들에도 스케일 적용 (옵션)
                    // 필요시 주석 해제

                    Transform[] allChildren = hitEffect.GetComponentsInChildren<Transform>();
                    foreach (Transform child in allChildren)
                    {
                        if (child != hitEffect.transform) // 루트는 이미 적용했으므로 제외
                        {
                            child.localScale = Vector3.Scale(child.localScale, currentProjectileConfig.Scale);
                        }
                    }


                    if (debugMode)
                    {
                        Debug.Log($"[ProjectileController] 히트 이펙트 스케일 적용: {currentProjectileConfig.Scale}");
                        Debug.Log($"[ProjectileController] 히트 이펙트 소스: {(customHitEffectPrefab != null ? "ProjectileMoveScript" : "VFXEffectSO")}");
                        Debug.Log($"[ProjectileController] 사용된 Prefab: {hitEffectPrefab.name}");
                        Debug.Log($"[ProjectileController] 히트 이펙트 자식 오브젝트 수: {hitEffect.transform.childCount}");
                    }
                }

                if (debugMode)
                    Debug.Log($"[ProjectileController] 히트 이펙트 생성 완료: {hitEffect.name} (최종 스케일: {hitEffect.transform.localScale})");

                // 히트 이펙트에서 반복 재생을 방지
                // 파티클 시스템이 있다면 한 번만 재생하도록 설정
                ParticleSystem[] particles = hitEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var particle in particles)
                {
                    var main = particle.main;
                    main.loop = false; // 루프 비활성화
                    main.stopAction = ParticleSystemStopAction.Destroy; // 완료 시 자동 삭제

                    if (debugMode)
                        Debug.Log($"[ProjectileController] 파티클 루프 비활성화: {particle.name}");
                }

                // 애니메이터가 있다면 루프 방지
                Animator[] animators = hitEffect.GetComponentsInChildren<Animator>();
                foreach (var animator in animators)
                {
                    if (animator.runtimeAnimatorController != null)
                    {
                        // 현재 애니메이션을 한 번만 재생하도록 설정
                        animator.SetBool("Loop", false);

                        if (debugMode)
                            Debug.Log($"[ProjectileController] 애니메이터 루프 비활성화: {animator.name}");
                    }
                }

                // 커스텀 스크립트가 있다면 루프 비활성화
                var customAnimations = hitEffect.GetComponentsInChildren<MonoBehaviour>();
                foreach (var comp in customAnimations)
                {
                    // 루프 관련 필드가 있는지 확인
                    var loopField = comp.GetType().GetField("loop");
                    if (loopField != null && loopField.FieldType == typeof(bool))
                    {
                        loopField.SetValue(comp, false);
                        if (debugMode)
                            Debug.Log($"[ProjectileController] 커스텀 루프 비활성화: {comp.GetType().Name}");
                    }
                }

                // 히트 이펙트 자동 삭제 (더 짧은 시간으로)
                StartCoroutine(DestroyHitEffect(hitEffect, 1f));
            }

            // 히트 사운드 재생
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position);
            }

            // 히트 이벤트 발생 (한 번만)
            if (OnProjectileHit != null)
            {
                OnProjectileHit.Invoke(this, target);
                OnProjectileHit = null; // 중복 호출 방지
            }

            if (onHitCallback != null)
            {
                onHitCallback.Invoke(this);
                onHitCallback = null; // 중복 호출 방지
            }

            // 투사체 삭제
            StartCoroutine(DestroyProjectile(0.1f));
        }

        /// <summary>
        /// 히트 이펙트 삭제
        /// </summary>
        private IEnumerator DestroyHitEffect(GameObject hitEffect, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (hitEffect != null)
            {
                Destroy(hitEffect);
            }
        }

        /// <summary>
        /// 움직임 타입에 따른 속도 업데이트
        /// </summary>
        private void UpdateSpeedByMovementType()
        {
            switch (movementType)
            {
                case ProjectileMovementType.Linear:
                    // 선형 움직임: 일정한 속도 유지
                    currentSpeed = baseSpeed;
                    break;

                case ProjectileMovementType.Accelerate:
                    // 가속 움직임: 시간에 따라 점점 빨라짐
                    float elapsedTime = Time.time - launchTime;
                    float accelerationCurve = Mathf.Pow(elapsedTime * accelerationFactor, 2f);
                    currentSpeed = Mathf.Lerp(baseSpeed * currentProjectileConfig.InitialSpeedRatio, baseSpeed * accelerationFactor, accelerationCurve);

                    if (debugMode && Time.frameCount % 60 == 0) // 1초마다 로그
                    {
                        Debug.Log($"[ProjectileController] 가속 움직임 - 경과시간: {elapsedTime:F2}초, 현재속도: {currentSpeed:F2}");
                    }
                    break;
            }

            // 최대 속도 제한 (너무 빨라지지 않도록)
            currentSpeed = Mathf.Min(currentSpeed, baseSpeed * accelerationFactor);
        }

        /// <summary>
        /// 투사체 삭제
        /// </summary>
        private IEnumerator DestroyProjectile(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (debugMode)
                Debug.Log($"[ProjectileController] 투사체 삭제: {sourceCard?.CardData?.CardName}");

            Destroy(gameObject);
        }

        /// <summary>
        /// 강제로 투사체 삭제 (타임아웃 등)
        /// </summary>
        public void ForceDestroy()
        {
            if (debugMode)
                Debug.Log($"[ProjectileController] 투사체 강제 삭제: {sourceCard?.CardData?.CardName}");

            Destroy(gameObject);
        }

        /// <summary>
        /// Gabriel Productions ProjectileMoveScript 비활성화
        /// </summary>
        private void DisableProjectileMoveScript()
        {
            // ProjectileMoveScript 컴포넌트 찾기
            var projectileMoveScript = GetComponent<ProjectileMoveScript>();
            if (projectileMoveScript != null)
            {
                // ProjectileMoveScript 비활성화
                projectileMoveScript.enabled = false;

                if (debugMode)
                    Debug.Log("[ProjectileController] ProjectileMoveScript 비활성화 완료");
            }
            else if (debugMode)
            {
                Debug.Log("[ProjectileController] ProjectileMoveScript 컴포넌트를 찾을 수 없습니다.");
            }
        }

        // Public Properties
        public Transform Target => target;
        public Card SourceCard => sourceCard;
        public VFXEffectSO VFXData => vfxData;
        public bool HasHit => hasHit;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (target != null && !hasHit)
            {
                // 타겟을 향한 선 그리기
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, target.position);
                
                // 타겟 표시
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(target.position, 0.5f);
            }
        }
#endif
    }
}
