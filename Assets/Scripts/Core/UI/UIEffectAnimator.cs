using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 通用 UI 特效动画组件
/// 挂载在特效预制体上，即可自动获得弹入、停留、淡出等平滑的 DOTween 动效表现
/// </summary>
public class UIEffectAnimator : MonoBehaviour
{
    [Header("1. 入场弹入效果 (Pop In)")]
    [Tooltip("是否开启入场缩放弹入")]
    public bool enablePopIn = true;
    [Tooltip("初始起始缩放比例")]
    public Vector3 startScale = new Vector3(0.4f, 0.4f, 1f);
    [Tooltip("弹入目标缩放比例")]
    public Vector3 targetScale = Vector3.one;
    [Tooltip("弹入动画耗时 (秒)")]
    public float popDuration = 0.25f;
    [Tooltip("弹入缓动曲线 (OutBack 带有轻微 Q 弹回弹感)")]
    public Ease popEase = Ease.OutBack;

    [Header("2. 停留展示时长 (Hold Duration)")]
    [Tooltip("弹入完成后在屏幕上完整停留展示的时间 (秒)")]
    public float holdDuration = 1.0f;

    [Header("3. 持续呼吸微动 (Pulse / Breath)")]
    [Tooltip("是否在停留期间启用轻微缩放呼吸动效")]
    public bool enablePulse = false;
    public float pulseScaleMultiplier = 1.08f;
    public float pulseDuration = 0.4f;

    [Header("4. 持续自旋转 (Rotation)")]
    [Tooltip("是否持续缓慢自旋转 (适合魔法阵/光圈/护盾)")]
    public bool enableRotate = false;
    public float rotateSpeed = 90f; // 度/秒

    [Header("5. 退场淡出效果 (Fade Out)")]
    [Tooltip("是否在停留结束后渐隐淡出")]
    public bool enableFadeOut = true;
    [Tooltip("淡出动画耗时 (秒)")]
    public float fadeDuration = 0.35f;
    [Tooltip("淡出缓动曲线")]
    public Ease fadeEase = Ease.InQuad;

    [Header("6. 生命周期管理")]
    [Tooltip("动画播放完毕后是否自动销毁该特效 GameObject")]
    public bool autoDestroy = true;

    private CanvasGroup canvasGroup;
    private Graphic[] childGraphics;
    private Sequence animSequence;
    private Tween rotateTween;

    private void Awake()
    {
        // 优先使用 CanvasGroup（整体淡出最高效），若无则尝试获取自身或子物体上的所有 Graphic (Image/Text)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            childGraphics = GetComponentsInChildren<Graphic>(true);
        }
    }

    private void Start()
    {
        PlayAnimation();
    }

    /// <summary>
    /// 开始播放整套特效动画流程
    /// </summary>
    public void PlayAnimation()
    {
        // 杀掉旧的动画防止重复
        KillTweens();

        // 1. 初始化入场状态
        if (enablePopIn)
        {
            transform.localScale = startScale;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        else if (childGraphics != null)
        {
            foreach (var g in childGraphics)
            {
                if (g != null)
                {
                    Color c = g.color;
                    c.a = 1f;
                    g.color = c;
                }
            }
        }

        // 2. 自旋转动效 (独立并行运行)
        if (enableRotate)
        {
            rotateTween = transform.DORotate(new Vector3(0, 0, -360f), 360f / Mathf.Max(rotateSpeed, 1f), RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }

        // 3. 主动画时间线 (Sequence)
        animSequence = DOTween.Sequence();

        // ① 入场：弹入放大
        if (enablePopIn)
        {
            animSequence.Append(transform.DOScale(targetScale, popDuration).SetEase(popEase));
        }

        // ② 停留与呼吸
        if (enablePulse)
        {
            int loops = Mathf.Max(1, Mathf.RoundToInt(holdDuration / (pulseDuration * 2)));
            animSequence.Append(
                transform.DOScale(targetScale * pulseScaleMultiplier, pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(loops * 2, LoopType.Yoyo)
            );
        }
        else if (holdDuration > 0f)
        {
            animSequence.AppendInterval(holdDuration);
        }

        // ③ 退场：渐隐淡出
        if (enableFadeOut)
        {
            if (canvasGroup != null)
            {
                animSequence.Append(canvasGroup.DOFade(0f, fadeDuration).SetEase(fadeEase));
            }
            else if (childGraphics != null && childGraphics.Length > 0)
            {
                for (int i = 0; i < childGraphics.Length; i++)
                {
                    var g = childGraphics[i];
                    if (g != null)
                    {
                        if (i == 0)
                            animSequence.Append(g.DOFade(0f, fadeDuration).SetEase(fadeEase));
                        else
                            animSequence.Join(g.DOFade(0f, fadeDuration).SetEase(fadeEase));
                    }
                }
            }
        }

        // ④ 播放结束自动销毁
        if (autoDestroy)
        {
            animSequence.OnComplete(() =>
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
            });
        }
    }

    private void KillTweens()
    {
        if (animSequence != null && animSequence.IsActive())
        {
            animSequence.Kill();
            animSequence = null;
        }
        if (rotateTween != null && rotateTween.IsActive())
        {
            rotateTween.Kill();
            rotateTween = null;
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
