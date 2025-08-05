using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Maglin.Cards;
using Maglin.Battle;

namespace Maglin.UI
{
    /// <summary>
    /// 전투 필드의 속성 상태와 효과를 시각적으로 표시하는 UI
    /// </summary>
    public class FieldUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Image fieldBackgroundImage;
        [SerializeField] private Image fieldIconImage;
        [SerializeField] private Text fieldNameText;
        [SerializeField] private Text remainingTurnsText;
        [SerializeField] private GameObject fieldEffectPanel;

        [Header("애니메이션")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        [Header("속성별 기본 색상")]
        [SerializeField] private Color fireColor = new Color(1f, 0.3f, 0.3f, 0.7f);
        [SerializeField] private Color waterColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        [SerializeField] private Color grassColor = new Color(0.3f, 1f, 0.3f, 0.7f);
        [SerializeField] private Color lightColor = new Color(1f, 1f, 0.8f, 0.7f);
        [SerializeField] private Color darkColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
        [SerializeField] private Color defaultColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        [Header("속성별 아이콘 (선택사항)")]
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite waterIcon;
        [SerializeField] private Sprite grassIcon;
        [SerializeField] private Sprite lightIcon;
        [SerializeField] private Sprite darkIcon;

        // 참조
        private FieldManager fieldManager;
        private Coroutine fadeCoroutine;

        #region Unity Events

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 필드 UI 초기화
        /// </summary>
        public void Initialize()
        {
            fieldManager = FieldManager.Instance;

            // 초기 상태 설정
            UpdateFieldDisplay(ElementType.None, null);

            if (fieldEffectPanel != null)
                fieldEffectPanel.SetActive(false);
        }

        /// <summary>
        /// 필드 상태 강제 업데이트
        /// </summary>
        public void RefreshFieldDisplay()
        {
            if (fieldManager != null)
            {
                UpdateFieldDisplay(fieldManager.CurrentFieldElement, fieldManager.CurrentFieldEffect);
                UpdateRemainingTurns(fieldManager.RemainingTurns);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            FieldManager.OnFieldChanged += OnFieldChanged;
            FieldManager.OnFieldEffectApplied += OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved += OnFieldEffectRemoved;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            FieldManager.OnFieldChanged -= OnFieldChanged;
            FieldManager.OnFieldEffectApplied -= OnFieldEffectApplied;
            FieldManager.OnFieldEffectRemoved -= OnFieldEffectRemoved;
        }

        /// <summary>
        /// 필드 표시 업데이트
        /// </summary>
        private void UpdateFieldDisplay(ElementType fieldElement, FieldEffectSO fieldEffect)
        {
            // 배경색 설정
            if (fieldBackgroundImage != null)
            {
                Color backgroundColor = GetElementColor(fieldElement);
                if (fieldEffect != null)
                {
                    // 필드 효과가 있다면 해당 색상 사용
                    backgroundColor = fieldEffect.BackgroundColor;
                }
                fieldBackgroundImage.color = backgroundColor;
            }

            // 아이콘 설정
            if (fieldIconImage != null)
            {
                Sprite iconSprite = GetElementIcon(fieldElement);
                if (fieldEffect != null && fieldEffect.Icon != null)
                {
                    // 필드 효과 전용 아이콘이 있다면 우선 사용
                    iconSprite = fieldEffect.Icon;
                }

                if (iconSprite != null)
                {
                    fieldIconImage.sprite = iconSprite;
                    fieldIconImage.gameObject.SetActive(true);
                }
                else
                {
                    fieldIconImage.gameObject.SetActive(false);
                }
            }

            // 필드 이름 설정
            if (fieldNameText != null)
            {
                if (fieldEffect != null)
                {
                    fieldNameText.text = fieldEffect.EffectName;
                }
                else if (fieldElement != ElementType.None)
                {
                    fieldNameText.text = GetElementDisplayName(fieldElement);
                }
                else
                {
                    fieldNameText.text = "중립 필드";
                }
            }
        }

        /// <summary>
        /// 남은 턴 수 표시 업데이트
        /// </summary>
        private void UpdateRemainingTurns(int remainingTurns)
        {
            if (remainingTurnsText == null) return;

            if (remainingTurns > 0)
            {
                remainingTurnsText.text = $"남은 턴: {remainingTurns}";
                remainingTurnsText.gameObject.SetActive(true);
            }
            else
            {
                remainingTurnsText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 속성에 따른 색상 반환
        /// </summary>
        private Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => fireColor,
                ElementType.Water => waterColor,
                ElementType.Grass => grassColor,
                ElementType.Light => lightColor,
                ElementType.Dark => darkColor,
                _ => defaultColor
            };
        }

        /// <summary>
        /// 속성에 따른 아이콘 반환
        /// </summary>
        private Sprite GetElementIcon(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => fireIcon,
                ElementType.Water => waterIcon,
                ElementType.Grass => grassIcon,
                ElementType.Light => lightIcon,
                ElementType.Dark => darkIcon,
                _ => null
            };
        }

        /// <summary>
        /// 속성 표시 이름 반환
        /// </summary>
        private string GetElementDisplayName(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => "불 필드",
                ElementType.Water => "물 필드",
                ElementType.Grass => "풀 필드",
                ElementType.Light => "빛 필드",
                ElementType.Dark => "어둠 필드",
                _ => "중립 필드"
            };
        }

        /// <summary>
        /// 페이드 인 애니메이션
        /// </summary>
        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// 페이드 아웃 애니메이션
        /// </summary>
        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0.3f, t);
                yield return null;
            }

            canvasGroup.alpha = 0.3f;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 필드 변경 이벤트 처리
        /// </summary>
        private void OnFieldChanged(ElementType newElement, ElementType previousElement)
        {
            UpdateFieldDisplay(newElement, fieldManager?.CurrentFieldEffect);
        }

        /// <summary>
        /// 필드 효과 적용 이벤트 처리
        /// </summary>
        private void OnFieldEffectApplied(FieldEffectSO fieldEffect)
        {
            UpdateFieldDisplay(fieldEffect.FieldElement, fieldEffect);
            UpdateRemainingTurns(fieldEffect.Duration);

            if (fieldEffectPanel != null)
                fieldEffectPanel.SetActive(true);

            // 페이드 인 애니메이션
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 필드 효과 제거 이벤트 처리
        /// </summary>
        private void OnFieldEffectRemoved(FieldEffectSO fieldEffect)
        {
            UpdateFieldDisplay(ElementType.None, null);
            UpdateRemainingTurns(0);

            if (fieldEffectPanel != null)
                fieldEffectPanel.SetActive(false);

            // 페이드 아웃 애니메이션
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        #endregion
    }
}