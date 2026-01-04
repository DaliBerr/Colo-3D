using Lonize.Logging;
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
    [SerializeField] private Vector2 baseCellSize = new Vector2(250, 250);
    private GridLayoutGroup _grid;
    private Canvas _canvas;

    /// <summary>
    /// summary: 初始化引用与基准值（基准 cellSize 与基准 Canvas scaleFactor）。
    /// param: 无
    /// return: 无
    /// </summary>
    private void Awake()
    {
        InitRefs();
        // CaptureBaseIfNeeded();
        // ApplyCompensation(true);
        _grid.cellSize = new Vector2(baseCellSize.x / _canvas.scaleFactor, baseCellSize.y / _canvas.scaleFactor);
    }
    /// <summary>
    /// summary: 获取组件引用（Grid/RectTransform/父级 Canvas）。
    /// param: 无
    /// return: 无
    /// </summary>
    private void InitRefs()
    {
        if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
        _canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length > 0 ? FindObjectsByType<Canvas>(FindObjectsSortMode.None)[0] : null;

    }

}
