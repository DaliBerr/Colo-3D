using System;
using Lonize.Events;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑的“基础显示模式”（互斥）。
    /// param: 无
    /// return: 无
    /// </summary>
    public enum BuildingViewBaseMode
    {
        Normal,
        Ghost,
        Disabled
    }

    /// <summary>
    /// summary: 旧的显示模式（含 Selected），用于兼容历史调用；新代码请改用 BaseMode + SetSelected。
    /// param: 无
    /// return: 无
    /// </summary>
    [Obsolete("Use BuildingViewBaseMode + SetSelected instead.")]
    public enum BuildingViewMode
    {
        Normal,
        Ghost,
        Selected,
        Disabled
    }

    /// <summary>
    /// summary: 建筑外观控制器（颜色/幽灵态/选中 Overlay）。
    /// param: 无
    /// return: 无
    /// </summary>
    [RequireComponent(typeof(BuildingRuntimeHost))]
    public class BuildingView : MonoBehaviour
    {
        [Header("基础引用（可空）")]
        [SerializeField] private SpriteRenderer _mainSpriteRenderer;
        [SerializeField] private Renderer[] _renderers; // 3D/2D 都可（SpriteRenderer 也继承 Renderer，但这里不强制）

        [Header("Overlay（选中轮廓/光圈等，可用一个额外模型/特效）")]
        [SerializeField] private GameObject _selectionHighlight;

        [Header("Ghost（放置预览辅助显示，可选）")]
        [SerializeField] private GameObject _ghostVisual;

        [Header("交互（建议由 BuildingSelectionController 统一处理）")]
        [SerializeField] private bool _enableOnMouseDownSelect = false;

        [Header("颜色配置")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _ghostOkColor = new Color(0.7f, 1f, 0.7f, 0.6f);
        [SerializeField] private Color _ghostBlockedColor = new Color(1f, 0.5f, 0.5f, 0.6f);
        [SerializeField] private Color _disabledColor = Color.gray;

        private BuildingRuntimeHost _host;
        private MaterialPropertyBlock _mpb;

        private static int ColorId_BaseColor = -1;
        private static int ColorId_Color = -1;

        private BuildingViewBaseMode _baseMode = BuildingViewBaseMode.Normal;
        private bool _isSelected;
        private bool _ghostBlocked;

        /// <summary>
        /// summary: Unity 生命周期入口，初始化渲染引用并刷新默认外观。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (ColorId_BaseColor == -1) ColorId_BaseColor = Shader.PropertyToID("_BaseColor");
            if (ColorId_Color == -1) ColorId_Color = Shader.PropertyToID("_Color");

            _host = GetComponent<BuildingRuntimeHost>();

            // 自动填充 renderers：优先用手动配置，没配就抓子节点
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);

            _baseMode = BuildingViewBaseMode.Normal;
            _isSelected = false;
            _ghostBlocked = false;
            RefreshVisual();
        }

        /// <summary>
        /// summary: Unity 鼠标点击回调（可选启用），仅发布选中事件。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnMouseDown()
        {
            if (!_enableOnMouseDownSelect)
                return;

            // 如果 runtimeHost 还没准备好，就别选了，避免空引用
            if (_host == null || _host.Runtime == null)
            {
                Log.Warn("[BuildingView] OnMouseDown but runtimeHost / Runtime is null.");
                GameDebug.LogWarning("[BuildingView] OnMouseDown but runtimeHost / Runtime is null.");
                return;
            }

            // 仅发布选中事件：实际选中状态应由 SelectionController 统一驱动回写到 View。
            Events.eventBus.Publish(new BuildingSelected
            {
                buildingRuntime = _host.Runtime,
                isSelected = true,
            });
        }

        /// <summary>
        /// summary: 设置基础显示模式（互斥：Normal/Ghost/Disabled）。
        /// param: mode 目标基础模式
        /// return: 无
        /// </summary>
        public void SetBaseMode(BuildingViewBaseMode mode)
        {
            if (_baseMode == mode)
                return;
            
            _baseMode = mode;
        
            RefreshVisual();
        }

        /// <summary>
        /// summary: 设置选中 Overlay（轮廓线/光圈等）。
        /// param: isSelected 是否选中
        /// return: 无
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            if (_isSelected == isSelected)
                return;

            _isSelected = isSelected;
            RefreshVisual();
        }

        /// <summary>
        /// summary: 仅在 Ghost 模式下生效的“阻挡”提示（红/绿切换）。
        /// param: blocked 是否阻挡/不可放置
        /// return: 无
        /// </summary>
        public void SetGhostBlocked(bool blocked)
        {
            if (_ghostBlocked == blocked)
                return;

            _ghostBlocked = blocked;

            // 非 Ghost 下不改变基础颜色，但仍保留状态（进入 Ghost 时可立即反映）。
            if (_baseMode == BuildingViewBaseMode.Ghost)
                RefreshVisual();
        }

        /// <summary>
        /// summary: 获取当前基础模式。
        /// param: 无
        /// return: 当前基础模式
        /// </summary>
        public BuildingViewBaseMode GetBaseMode()
        {
            return _baseMode;
        }

        /// <summary>
        /// summary: 获取是否选中。
        /// param: 无
        /// return: 是否选中
        /// </summary>
        public bool GetIsSelected()
        {
            return _isSelected;
        }

        /// <summary>
        /// summary: 运行时替换精灵（仅对 SpriteRenderer 生效）。
        /// param: sprite 新精灵
        /// return: 无
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            if (_mainSpriteRenderer && sprite != null)
                _mainSpriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// summary: 兼容旧接口：将旧 Mode 映射到 BaseMode + Selected。
        /// param: mode 旧模式
        /// return: 无
        /// </summary>
        [Obsolete("Use SetBaseMode(BuildingViewBaseMode) + SetSelected(bool) instead.")]
        public void SetMode(BuildingViewMode mode)
        {
            switch (mode)
            {
                case BuildingViewMode.Normal:
                    _isSelected = false;
                    _baseMode = BuildingViewBaseMode.Normal;
                    break;
                case BuildingViewMode.Ghost:
                    _isSelected = false;
                    _baseMode = BuildingViewBaseMode.Ghost;
                    break;
                case BuildingViewMode.Selected:
                    _isSelected = true;
                    _baseMode = BuildingViewBaseMode.Normal;
                    break;
                case BuildingViewMode.Disabled:
                    _isSelected = false;
                    _baseMode = BuildingViewBaseMode.Disabled;
                    break;
            }

            RefreshVisual();
        }

        /// <summary>
        /// summary: 刷新当前显示（颜色、Ghost 对象、Selected Overlay）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshVisual()
        {
            if (_ghostVisual)
                _ghostVisual.SetActive(_baseMode == BuildingViewBaseMode.Ghost);

            if (_selectionHighlight){
                GameDebug.Log($"BuildingView: Setting selection highlight active: {_isSelected}");
                _selectionHighlight.SetActive(_isSelected);
            }

            ApplyColor(GetBaseColor());
        }

        /// <summary>
        /// summary: 计算基础模式对应的颜色（不包含 Selected Overlay）。
        /// param: 无
        /// return: 目标颜色
        /// </summary>
        private Color GetBaseColor()
        {
            switch (_baseMode)
            {
                case BuildingViewBaseMode.Ghost:
                    return _ghostBlocked ? _ghostBlockedColor : _ghostOkColor;
                case BuildingViewBaseMode.Disabled:
                    return _disabledColor;
                default:
                    return _normalColor;
            }
        }

        /// <summary>
        /// summary: 将颜色应用到 SpriteRenderer 和所有 Renderer（使用 MPB，避免实例化材质）。
        /// param: c 目标颜色
        /// return: 无
        /// </summary>
        private void ApplyColor(Color c)
        {
            // 兼容旧版 2D
            if (_mainSpriteRenderer != null)
                _mainSpriteRenderer.color = c;

            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                // URP Lit 一般是 _BaseColor，旧 Standard 是 _Color，两者都写
                _mpb.SetColor(ColorId_BaseColor, c);
                _mpb.SetColor(ColorId_Color, c);

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
