using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分辨率自适应与 16:9 黑边视口适配器 (Letterbox & Pillarbox Controller)
/// 保证当玩家屏幕/分辨率不是 16:9 时，画面始终以 16:9 (1920x1080) 居中完整呈现，并在四周自动填充纯黑黑边。
/// </summary>
[DisallowMultipleComponent]
public class ResolutionAdapter : MonoBehaviour
{
    public const float TARGET_ASPECT_WIDTH = 16f;
    public const float TARGET_ASPECT_HEIGHT = 9f;
    public const float TARGET_ASPECT = TARGET_ASPECT_WIDTH / TARGET_ASPECT_HEIGHT;

    private static Camera uiCamera;
    private static Camera backgroundClearCamera;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private Canvas targetCanvas;
    private CanvasScaler targetScaler;

    private void Awake()
    {
        SetupAdapter();
    }

    private void Start()
    {
        ApplyResolutionCorrection();
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyResolutionCorrection();
        }
    }

    private static void EnsureBackgroundCamera()
    {
        if (backgroundClearCamera == null)
        {
            GameObject bgCamGo = GameObject.Find("BackgroundClearCamera");
            if (bgCamGo == null)
            {
                bgCamGo = new GameObject("BackgroundClearCamera");
                DontDestroyOnLoad(bgCamGo);
            }
            backgroundClearCamera = bgCamGo.GetComponent<Camera>();
            if (backgroundClearCamera == null) backgroundClearCamera = bgCamGo.AddComponent<Camera>();
            backgroundClearCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundClearCamera.backgroundColor = Color.black;
            backgroundClearCamera.cullingMask = 0; // 不渲染任何物体，仅负责纯黑清底
            backgroundClearCamera.depth = -100;
            backgroundClearCamera.orthographic = true;
            backgroundClearCamera.rect = new Rect(0, 0, 1, 1);
        }
    }

    public void SetupAdapter()
    {
        EnsureBackgroundCamera();

        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) return;

        // 确保有 UI 主相机
        if (uiCamera == null)
        {
            GameObject uiCamGo = GameObject.Find("GameUICamera");
            if (uiCamGo == null)
            {
                uiCamGo = new GameObject("GameUICamera");
                DontDestroyOnLoad(uiCamGo);
            }
            uiCamera = uiCamGo.GetComponent<Camera>();
            if (uiCamera == null) uiCamera = uiCamGo.AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.depth = 1;
            uiCamera.orthographic = true;
            uiCamera.cullingMask = ~0; // 渲染所有图层
        }

        // 配置 Canvas 模式为 ScreenSpaceCamera
        targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        targetCanvas.worldCamera = uiCamera;
        targetCanvas.planeDistance = 100f;

        // 统一锁定 CanvasScaler 为 1920x1080 等比缩放
        targetScaler = targetCanvas.GetComponent<CanvasScaler>();
        if (targetScaler == null) targetScaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        targetScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        targetScaler.referenceResolution = new Vector2(1920, 1080);
        targetScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        targetScaler.matchWidthOrHeight = 0.5f;

        ApplyResolutionCorrection();
    }

    public void ApplyResolutionCorrection()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (uiCamera == null) return;

        float currentAspect = (float)lastScreenWidth / (float)lastScreenHeight;

        // 标准 16:9 比例 (允许 0.005 浮点误差)
        if (Mathf.Abs(currentAspect - TARGET_ASPECT) < 0.005f)
        {
            uiCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
        else if (currentAspect > TARGET_ASPECT)
        {
            // 屏幕过宽（如 21:9 超宽屏、18:9 等）：左右两侧留黑边 (Pillarbox)
            float insetWidth = TARGET_ASPECT / currentAspect;
            float leftMargin = (1.0f - insetWidth) * 0.5f;
            uiCamera.rect = new Rect(leftMargin, 0.0f, insetWidth, 1.0f);
        }
        else
        {
            // 屏幕过高/过窄（如 16:10、4:3、Steam Deck 1280x800 等）：上下两侧留黑边 (Letterbox)
            float insetHeight = currentAspect / TARGET_ASPECT;
            float topMargin = (1.0f - insetHeight) * 0.5f;
            uiCamera.rect = new Rect(0.0f, topMargin, 1.0f, insetHeight);
        }
    }
}
