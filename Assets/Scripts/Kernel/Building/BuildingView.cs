using System.Collections.Generic;
using Lonize;
using Lonize.Events;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑视图基础模式（互斥）。
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
    /// summary: 建筑外观控制器（3D）：负责 Ghost/Disabled 染色、选中 Overlay 显隐，可选发布点击选中事件。
    /// param: 无
    /// return: 无
    /// </summary>
    [RequireComponent(typeof(BuildingRuntimeHost))]
    public class BuildingView : MonoBehaviour
    {
        [Header("渲染目标（不填则自动抓取子节点 Renderer）")]
        [SerializeField] private Renderer[] _tintRenderers;

        [Header("Overlay（选中轮廓/光圈等）")]
        [SerializeField] private GameObject _selectionHighlight;

        [Header("Ghost（可选：独立 ghost 视觉对象）")]
        [SerializeField] private GameObject _ghostVisual;

        [Header("交互（建议由 BuildingSelectionController 统一处理）")]
        [SerializeField] private bool _enableOnMouseDownSelect = false;

        [Header("颜色配置")]
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

        private struct TintTarget
        {
            public Renderer Renderer;
            public bool HasBaseColor;
            public bool HasColor;
            public Color OrigBaseColor;
            public Color OrigColor;
        }

        private TintTarget[] _targets;

        /// <summary>
        /// summary: Unity 生命周期入口，初始化并缓存渲染目标与原始颜色。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (ColorId_BaseColor == -1) ColorId_BaseColor = Shader.PropertyToID("_BaseColor");
            if (ColorId_Color == -1) ColorId_Color = Shader.PropertyToID("_Color");

            _host = GetComponent<BuildingRuntimeHost>();

            BuildTintTargets();

            // 初始刷新
            _baseMode = BuildingViewBaseMode.Normal;
            _isSelected = false;
            _ghostBlocked = false;
            RefreshVisual();
        }

        /// <summary>
        /// summary: 可选的鼠标点击事件（仅发布选中事件，不直接改状态）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnMouseDown()
        {
            if (!_enableOnMouseDownSelect)
                return;

            if (_host == null || _host.Runtime == null)
            {
                Log.Warn("[BuildingView] OnMouseDown but runtimeHost / Runtime is null.");
                GameDebug.LogWarning("[BuildingView] OnMouseDown but runtimeHost / Runtime is null.");
                return;
            }

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
        /// summary: 设置 Ghost 是否阻挡（仅在 Ghost 模式下改变颜色：绿/红）。
        /// param: blocked true=阻挡/不可放置（红），false=可放置（绿）
        /// return: 无
        /// </summary>
        public void SetGhostBlocked(bool blocked)
        {
            if (_ghostBlocked == blocked)
                return;

            _ghostBlocked = blocked;

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
        /// summary: 重新构建可染色 Renderer 列表与原始颜色缓存（需要时手动调用）。
        /// param: 无
        /// return: 无
        /// </summary>
        public void RebuildTintTargets()
        {
            BuildTintTargets();
            RefreshVisual();
        }

        /// <summary>
        /// summary: 刷新显示（Ghost 对象显隐、选中 Overlay 显隐、颜色应用/恢复）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshVisual()
        {
            if (_ghostVisual != null)
                _ghostVisual.SetActive(_baseMode == BuildingViewBaseMode.Ghost);

            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(_isSelected);

            // Normal：恢复材质原始颜色（核心修复点：不再刷白）
            if (_baseMode == BuildingViewBaseMode.Normal)
            {
                RestoreOriginalColor();
                return;
            }

            ApplyTintColor(GetBaseTintColor());
        }

        /// <summary>
        /// summary: 计算当前基础模式的 tint 颜色（Normal 不使用 tint）。
        /// param: 无
        /// return: tint 颜色
        /// </summary>
        private Color GetBaseTintColor()
        {
            switch (_baseMode)
            {
                case BuildingViewBaseMode.Ghost:
                    return _ghostBlocked ? _ghostBlockedColor : _ghostOkColor;
                case BuildingViewBaseMode.Disabled:
                    return _disabledColor;
                default:
                    return Color.white;
            }
        }
        /// <summary>
        /// summary: 覆盖 Ghost 的可放置/不可放置两种颜色（用于放置预览实例）。
        /// param: ok 可放置时颜色（绿色）
        /// param: blocked 不可放置时颜色（红色）
        /// return: 无
        /// </summary>
        public void OverrideGhostColors(Color ok, Color blocked)
        {
            _ghostOkColor = ok;
            _ghostBlockedColor = blocked;

            if (_baseMode == BuildingViewBaseMode.Ghost)
                RefreshVisual();
        }
        /// <summary>
        /// summary: 构建可染色 Renderer 列表，并缓存其材质原始颜色；同时排除 overlay/ghost 子树下的 renderer。
        /// param: 无
        /// return: 无
        /// </summary>
        private void BuildTintTargets()
        {
            Renderer[] source;
            if (_tintRenderers != null && _tintRenderers.Length > 0)
                source = _tintRenderers;
            else
                source = this.transform.parent.GetComponentsInChildren<Renderer>(true);

            bool IsUnder(GameObject root, Transform t)
            {
                if (root == null) return false;
                var rt = root.transform;
                return t == rt || t.IsChildOf(rt);
            }

            var list = new List<TintTarget>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var r = source[i];
                if (r == null) continue;

                // 排除 selectionHighlight / ghostVisual 子树，避免 overlay 被染色
                if (IsUnder(_selectionHighlight, r.transform)) continue;
                if (IsUnder(_ghostVisual, r.transform)) continue;

                var mat = r.sharedMaterial;
                if (mat == null) continue;

                var target = new TintTarget
                {
                    Renderer = r,
                    HasBaseColor = mat.HasProperty(ColorId_BaseColor),
                    HasColor = mat.HasProperty(ColorId_Color),
                    OrigBaseColor = Color.white,
                    OrigColor = Color.white
                };

                if (target.HasBaseColor) target.OrigBaseColor = mat.GetColor(ColorId_BaseColor);
                if (target.HasColor) target.OrigColor = mat.GetColor(ColorId_Color);

                list.Add(target);
            }

            _targets = list.ToArray();
        }

        /// <summary>
        /// summary: 应用 tint 颜色到所有渲染目标（使用 MaterialPropertyBlock，避免实例化材质）。
        /// param: c tint 颜色
        /// return: 无
        /// </summary>
        private void ApplyTintColor(Color c)
        {
            if (_targets == null) return;

            for (int i = 0; i < _targets.Length; i++)
            {
                var t = _targets[i];
                var r = t.Renderer;
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (t.HasBaseColor) _mpb.SetColor(ColorId_BaseColor, c);
                if (t.HasColor) _mpb.SetColor(ColorId_Color, c);

                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// summary: 恢复所有渲染目标的原始颜色（仅恢复 _BaseColor/_Color，不清空其他 MPB 属性）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RestoreOriginalColor()
        {
            if (_targets == null) return;

            for (int i = 0; i < _targets.Length; i++)
            {
                var t = _targets[i];
                var r = t.Renderer;
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (t.HasBaseColor) _mpb.SetColor(ColorId_BaseColor, t.OrigBaseColor);
                if (t.HasColor) _mpb.SetColor(ColorId_Color, t.OrigColor);

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
