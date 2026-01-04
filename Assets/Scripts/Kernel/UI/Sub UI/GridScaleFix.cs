using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// summary: 让 GridLayoutGroup 的单元格视觉大小不随 UIScale（CanvasScaler）缩放变化而变化（通过 Canvas.scaleFactor 相对基准值反向补偿）。
/// param: 无
/// return: 无
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(GridLayoutGroup))]
public class GridLayoutFixedVisualSize : MonoBehaviour
{
    [Header("基准（想要的“视觉效果”）")]
    [SerializeField] private bool useManualBaseCellSize = false;

    [SerializeField] private Vector2 baseCellSize = new Vector2(250, 250);

    [Header("视觉缩放曲线（与 UIScale 的思路一致）")]
    [Range(0f, 1f)]
    [SerializeField] private float cellVisualScale = 0f; // 0=完全固定视觉大小；1=跟随整体缩放（等于不补偿）

    [Header("调试")]
    [SerializeField] private bool logDebug = false;

    private GridLayoutGroup _grid;
    private RectTransform _rt;
    private Canvas _canvas;

    // 基准：记录“我们认为是 1 倍视觉”的 Canvas scaleFactor
    private float _baseCanvasScaleFactor = -1f;

    // 避免每帧重复设置
    private float _lastAppliedRatio = -1f;

    /// <summary>
    /// summary: 初始化引用与基准值（基准 cellSize 与基准 Canvas scaleFactor）。
    /// param: 无
    /// return: 无
    /// </summary>
    private void Awake()
    {
        InitRefs();
        CaptureBaseIfNeeded();
        ApplyCompensation(true);
    }

    /// <summary>
    /// summary: 启用时刷新引用并应用一次补偿，保证编辑器/运行时一致。
    /// param: 无
    /// return: 无
    /// </summary>
    private void OnEnable()
    {
        InitRefs();
        CaptureBaseIfNeeded();
        Canvas.willRenderCanvases += OnWillRenderCanvases;
        ApplyCompensation(true);
    }

    /// <summary>
    /// summary: 禁用时取消回调，避免泄漏。
    /// param: 无
    /// return: 无
    /// </summary>
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    /// <summary>
    /// summary: 在 Canvas 即将渲染时执行（此时 CanvasScaler 往往已更新 scaleFactor，适配你的 UIScale 调整时序）。
    /// param: 无
    /// return: 无
    /// </summary>
    private void OnWillRenderCanvases()
    {
        ApplyCompensation(false);
    }

    /// <summary>
    /// summary: 获取组件引用（Grid/RectTransform/父级 Canvas）。
    /// param: 无
    /// return: 无
    /// </summary>
    private void InitRefs()
    {
        if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
        if (_rt == null) _rt = transform as RectTransform;

        // 找到控制该 UI 的 Canvas（最近的父 Canvas 即可；如果你有嵌套 Canvas，按你的结构可能需要改成 rootCanvas）
        _canvas = GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// summary: 如果没有手动指定基准值，则把当前 cellSize 当作基准；同时记录当前 Canvas.scaleFactor 作为基准。
    /// param: 无
    /// return: 无
    /// </summary>
    private void CaptureBaseIfNeeded()
    {
        if (_grid == null) return;

        if (!useManualBaseCellSize)
            baseCellSize = _grid.cellSize;

        if (_baseCanvasScaleFactor <= 0f)
        {
            float sf = GetCanvasScaleFactorSafe();
            _baseCanvasScaleFactor = sf > 0f ? sf : 1f;

            if (logDebug)
                Debug.Log($"[GridLayoutFixedVisualSize] Capture base: cell={baseCellSize}, baseCanvasScaleFactor={_baseCanvasScaleFactor}");
        }
    }

    /// <summary>
    /// summary: 安全获取 Canvas.scaleFactor（Canvas 未准备好时返回 1）。
    /// param: 无
    /// return: scaleFactor
    /// </summary>
    private float GetCanvasScaleFactorSafe()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) return 1f;

        // 一般情况下 Canvas.scaleFactor > 0
        float sf = _canvas.scaleFactor;
        if (sf <= 0f) sf = 1f;
        return sf;
    }

    /// <summary>
    /// summary: 计算当前相对缩放比（当前 Canvas scaleFactor / 基准 scaleFactor）。
    /// param: 无
    /// return: 相对缩放比（>= 0.001）
    /// </summary>
    private float GetRelativeScaleRatio()
    {
        float current = GetCanvasScaleFactorSafe();
        float ratio = current ; // 避免除0
        if (ratio < 0.001f) ratio = 0.001f;
        return ratio;
    }

    /// <summary>
    /// summary: 应用反向补偿到 cellSize（不碰 spacing/padding，避免与你的 UIScale 的 LayoutCompensation 冲突）。
    /// param: force 是否强制刷新
    /// return: 无
    /// </summary>
    private void ApplyCompensation(bool force)
    {
        if (_grid == null) return;
        if (_baseCanvasScaleFactor <= 0f) CaptureBaseIfNeeded();

        float ratio = GetRelativeScaleRatio();
        if (!force && Mathf.Abs(ratio - _lastAppliedRatio) < 0.0001f) return;
        _lastAppliedRatio = ratio;

        // 目标：视觉大小 ~ ratio^(cellVisualScale)
        // 由于 Canvas 会乘 ratio，这里设置：cellSize = baseCellSize * ratio^(cellVisualScale - 1)
        float cellFactor = Mathf.Pow(ratio, cellVisualScale - 1f);
        _grid.cellSize = baseCellSize * cellFactor;

        if (_rt != null)
            LayoutRebuilder.MarkLayoutForRebuild(_rt);

        if (logDebug)
            Debug.Log($"[GridLayoutFixedVisualSize] ratio={ratio}, cellFactor={cellFactor}, cellSize={_grid.cellSize}");
    }

    /// <summary>
    /// summary: 手动重新捕获“基准”（在你把 UIScale 调回 1 或你想作为默认的显示状态时点一下）。
    /// param: 无
    /// return: 无
    /// </summary>
    [ContextMenu("Capture Baseline Now")]
    private void CaptureBaselineNow()
    {
        InitRefs();

        if (_grid != null)
            baseCellSize = _grid.cellSize;

        _baseCanvasScaleFactor = GetCanvasScaleFactorSafe();
        _lastAppliedRatio = -1f;

        if (logDebug)
            Debug.Log($"[GridLayoutFixedVisualSize] Manual capture: baseCellSize={baseCellSize}, baseCanvasScaleFactor={_baseCanvasScaleFactor}");

        ApplyCompensation(true);
    }
}
