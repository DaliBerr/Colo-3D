using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kernel.GameState;
using Lonize.Logging;
using Lonize.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Log UI")]
    public sealed class LogConsoleUI_List : UIScreen
    {
        [Header("Header Buttons")]
        public Button BtnClose;
        public Button BtnClear;
        public Button BtnPause;
        public Button BtnCopySelected;

        [Header("Filter")]
        public Dropdown DropdownMinLevel;

        [Header("List (ScrollRect A)")]
        public ScrollRect ListScroll;
        public Transform ListContent;
        public LogEntryItemView EntryPrefab;

        [Header("Detail (ScrollRect B)")]
        public ScrollRect DetailScroll;
        public TextMeshProUGUI DetailText;

        [Header("Config")]
        public int LoadRecentCount = 200;
        public int MaxLines = 400;
        public int PreviewMaxChars = 50;

        [Header("Auto Scroll")]
        public bool AutoScrollWhenNearBottom = true;
        [Range(0.001f, 0.2f)]
        public float NearBottomThreshold = 0.02f;

        [Header("Level Colors (Text/Stripe)")]
        public Color TraceColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        public Color DebugColor = new Color(0.55f, 0.75f, 1f, 1f);
        public Color InfoColor  = new Color(0.95f, 0.95f, 0.95f, 1f);
        public Color WarnColor  = new Color(1f, 0.85f, 0.35f, 1f);
        public Color ErrorColor = new Color(1f, 0.45f, 0.45f, 1f);
        public Color FatalColor = new Color(1f, 0.2f, 0.2f, 1f);

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        private readonly ConcurrentQueue<LogEvent> _pending = new();
        private readonly List<LogEvent> _events = new();
        private readonly List<int> _visibleEventIndices = new();

        private readonly List<LogEntryItemView> _activeViews = new();
        private readonly Stack<LogEntryItemView> _pool = new();

        private ILogSink _uiSink;
        private bool _paused;
        private LogLevel _minLevel = LogLevel.Info;
        private int _selectedVisibleIndex = -1;

        /// <summary>
        /// summary: 初始化入口（绑定按钮、加载历史、注册 UI Sink）
        /// param: 无
        /// return: 无
        /// </summary>
        protected override void OnInit()
        {
            BindButtons();
            InitDropdown();

            LoadRecent();
            RefreshList(fullRefresh: true, autoScrollHint: false);

            _uiSink = new UILogSink(_pending);
            Log.AddSink(_uiSink);
        }

        /// <summary>
        /// summary: 每帧主线程刷新（从队列取日志并更新列表）
        /// param: 无
        /// return: 无
        /// </summary>
        private void Update()
        {
            if (_paused) return;

            // 关键：刷新前先判断用户是否接近底部
            bool wasNearBottom = IsListNearBottom();

            bool got = false;
            while (_pending.TryDequeue(out var e))
            {
                got = true;
                AppendEvent(e);
            }

            if (got)
            {
                bool shouldAutoScroll = AutoScrollWhenNearBottom && wasNearBottom;
                RefreshList(fullRefresh: true, autoScrollHint: shouldAutoScroll);
            }
        }

        /// <summary>
        /// summary: 销毁时移除 sink 并解绑按钮，避免重复订阅
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnDestroy()
        {
            UnbindButtons();

            if (_uiSink != null)
                Log.RemoveSink(_uiSink);
            _uiSink = null;

            ClearAllViews();
        }

        /// <summary>
        /// summary: 追加日志到缓存并做最大条数裁剪
        /// param: e 日志事件
        /// return: 无
        /// </summary>
        private void AppendEvent(in LogEvent e)
        {
            _events.Add(e);

            if (MaxLines > 0 && _events.Count > MaxLines)
            {
                int removeCount = _events.Count - MaxLines;
                _events.RemoveRange(0, removeCount);

                // 裁剪后选中可能失效，直接清掉
                _selectedVisibleIndex = -1;
                if (DetailText != null) DetailText.text = string.Empty;
            }
        }

        /// <summary>
        /// summary: 加载最近日志快照
        /// param: 无
        /// return: 无
        /// </summary>
        private void LoadRecent()
        {
            _events.Clear();
            var recent = Log.SnapshotRecent(Mathf.Max(1, LoadRecentCount));
            for (int i = 0; i < recent.Length; i++)
                _events.Add(recent[i]);
        }

        /// <summary>
        /// summary: 刷新列表（重建可见索引 + 复用/回收条目，可选自动滚动）
        /// param: fullRefresh 是否全量刷新
        /// param: autoScrollHint 是否提示滚动到底部
        /// return: 无
        /// </summary>
        private void RefreshList(bool fullRefresh, bool autoScrollHint)
        {
            BuildVisibleIndices();
            RebuildViews();
            EnsureSelectionValid();

            if (autoScrollHint)
                ScrollListToBottom();
        }

        /// <summary>
        /// summary: 判断用户是否接近底部（用于“只在底部附近才自动滚动”）
        /// param: 无
        /// return: 是否接近底部
        /// </summary>
        private bool IsListNearBottom()
        {
            if (ListScroll == null) return true;

            var content = ListScroll.content;
            var viewport = ListScroll.viewport;

            if (content == null) return true;

            float contentH = content.rect.height;
            float viewH = viewport != null ? viewport.rect.height : ((RectTransform)ListScroll.transform).rect.height;

            // 内容不够长，无法滚动时视作在底部
            if (contentH <= viewH + 0.5f)
                return true;

            // Unity: verticalNormalizedPosition 0=底部, 1=顶部
            return ListScroll.verticalNormalizedPosition <= NearBottomThreshold;
        }

        /// <summary>
        /// summary: 滚动列表到底部
        /// param: 无
        /// return: 无
        /// </summary>
        private void ScrollListToBottom()
        {
            if (ListScroll == null) return;
            Canvas.ForceUpdateCanvases();
            ListScroll.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// summary: 根据过滤等级生成“可见事件索引列表”
        /// param: 无
        /// return: 无
        /// </summary>
        private void BuildVisibleIndices()
        {
            _visibleEventIndices.Clear();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Level < _minLevel) continue;
                _visibleEventIndices.Add(i);
            }
        }

        /// <summary>
        /// summary: 重建条目视图（对象池复用）
        /// param: 无
        /// return: 无
        /// </summary>
        private void RebuildViews()
        {
            if (EntryPrefab == null || ListContent == null) return;

            for (int i = 0; i < _activeViews.Count; i++)
                RecycleView(_activeViews[i]);
            _activeViews.Clear();

            for (int v = 0; v < _visibleEventIndices.Count; v++)
            {
                var view = GetView();
                view.gameObject.SetActive(true);
                view.transform.SetParent(ListContent, false);

                int eventIndex = _visibleEventIndices[v];
                var e = _events[eventIndex];

                Color c = ResolveLevelColor(e.Level);
                view.Bind(v, e, PreviewMaxChars, c, c, OnClickEntry);
                view.SetSelected(v == _selectedVisibleIndex);

                _activeViews.Add(view);
            }
        }

        /// <summary>
        /// summary: 确保选中项合法，必要时自动选中最新一条
        /// param: 无
        /// return: 无
        /// </summary>
        private void EnsureSelectionValid()
        {
            if (_visibleEventIndices.Count <= 0)
            {
                _selectedVisibleIndex = -1;
                if (DetailText != null) DetailText.text = string.Empty;
                return;
            }

            if (_selectedVisibleIndex < 0 || _selectedVisibleIndex >= _visibleEventIndices.Count)
                OnClickEntry(_visibleEventIndices.Count - 1);
            else
                UpdateDetail(_selectedVisibleIndex);
        }

        /// <summary>
        /// summary: 点击某条日志（visible index）
        /// param: visibleIndex 可见列表中的索引
        /// return: 无
        /// </summary>
        private void OnClickEntry(int visibleIndex)
        {
            _selectedVisibleIndex = visibleIndex;

            for (int i = 0; i < _activeViews.Count; i++)
                _activeViews[i].SetSelected(i == _selectedVisibleIndex);

            UpdateDetail(visibleIndex);
        }

        /// <summary>
        /// summary: 更新详情面板（并按等级给详情整体上色）
        /// param: visibleIndex 可见列表索引
        /// return: 无
        /// </summary>
        private void UpdateDetail(int visibleIndex)
        {
            if (DetailText == null) return;
            if (visibleIndex < 0 || visibleIndex >= _visibleEventIndices.Count) return;

            int eventIndex = _visibleEventIndices[visibleIndex];
            var e = _events[eventIndex];

            DetailText.text = FormatDetail(e);
            DetailText.color = ResolveLevelColor(e.Level);

            if (DetailScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                DetailScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// summary: 按等级返回颜色
        /// param: level 日志等级
        /// return: 对应颜色
        /// </summary>
        private Color ResolveLevelColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => TraceColor,
                LogLevel.Debug => DebugColor,
                LogLevel.Info  => InfoColor,
                LogLevel.Warn  => WarnColor,
                LogLevel.Error => ErrorColor,
                LogLevel.Fatal => FatalColor,
                _ => InfoColor
            };
        }

        /// <summary>
        /// summary: 格式化详情文本（通用格式 + exception）
        /// param: e 日志事件
        /// return: 详情字符串
        /// </summary>
        private string FormatDetail(in LogEvent e)
        {
            var sb = new StringBuilder(512);
            string t = e.UtcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            sb.Append('[').Append(t).Append("] [").Append(e.Level).Append(']');

            if (!string.IsNullOrEmpty(e.Category))
                sb.Append(" [").Append(e.Category).Append(']');

            sb.AppendLine();
            sb.AppendLine(e.Message);

            sb.Append(" <")
              .Append(Path.GetFileName(e.File)).Append(':').Append(e.Line)
              .Append(" @").Append(e.Member)
              .Append(" T").Append(e.ThreadId)
              .Append('>');

            if (e.Exception != null)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(e.Exception);
            }

            return sb.ToString();
        }

        /// <summary>
        /// summary: 绑定按钮回调
        /// param: 无
        /// return: 无
        /// </summary>
        private void BindButtons()
        {
            if (BtnClose != null) BtnClose.onClick.AddListener(OnClickClose);
            if (BtnClear != null) BtnClear.onClick.AddListener(OnClickClear);
            if (BtnPause != null) BtnPause.onClick.AddListener(OnClickPause);
            if (BtnCopySelected != null) BtnCopySelected.onClick.AddListener(OnClickCopySelected);
            if (DropdownMinLevel != null) DropdownMinLevel.onValueChanged.AddListener(OnMinLevelChanged);
        }

        /// <summary>
        /// summary: 解绑按钮回调
        /// param: 无
        /// return: 无
        /// </summary>
        private void UnbindButtons()
        {
            if (BtnClose != null) BtnClose.onClick.RemoveListener(OnClickClose);
            if (BtnClear != null) BtnClear.onClick.RemoveListener(OnClickClear);
            if (BtnPause != null) BtnPause.onClick.RemoveListener(OnClickPause);
            if (BtnCopySelected != null) BtnCopySelected.onClick.RemoveListener(OnClickCopySelected);
            if (DropdownMinLevel != null) DropdownMinLevel.onValueChanged.RemoveListener(OnMinLevelChanged);
        }

        /// <summary>
        /// summary: 初始化等级下拉框
        /// param: 无
        /// return: 无
        /// </summary>
        private void InitDropdown()
        {
            if (DropdownMinLevel == null) return;

            DropdownMinLevel.ClearOptions();
            DropdownMinLevel.AddOptions(new List<string>
            {
                nameof(LogLevel.Trace),
                nameof(LogLevel.Debug),
                nameof(LogLevel.Info),
                nameof(LogLevel.Warn),
                nameof(LogLevel.Error),
                nameof(LogLevel.Fatal),
            });

            DropdownMinLevel.value = (int)LogLevel.Info;
            _minLevel = LogLevel.Info;
        }

        /// <summary>
        /// summary: 关闭窗口
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClickClose()
        {
            StartCoroutine(UIManager.Instance.PopModalAndWait());
        }

        /// <summary>
        /// summary: 清空缓存并刷新（不触发自动滚动逻辑）
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClickClear()
        {
            _events.Clear();
            _selectedVisibleIndex = -1;
            if (DetailText != null) DetailText.text = string.Empty;
            RefreshList(fullRefresh: true, autoScrollHint: false);
        }

        /// <summary>
        /// summary: 暂停/继续刷新
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClickPause()
        {
            _paused = !_paused;
        }

        /// <summary>
        /// summary: 复制当前选中条目的详情到剪贴板
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClickCopySelected()
        {
            if (DetailText == null) return;
            GUIUtility.systemCopyBuffer = DetailText.text ?? string.Empty;
        }

        /// <summary>
        /// summary: 修改最小显示等级并刷新（不强制滚动到底部）
        /// param: idx dropdown 索引
        /// return: 无
        /// </summary>
        private void OnMinLevelChanged(int idx)
        {
            idx = Mathf.Clamp(idx, 0, 5);
            _minLevel = (LogLevel)idx;
            RefreshList(fullRefresh: true, autoScrollHint: false);
        }

        /// <summary>
        /// summary: 获取一个条目视图（对象池优先）
        /// param: 无
        /// return: 条目视图实例
        /// </summary>
        private LogEntryItemView GetView()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            return Instantiate(EntryPrefab);
        }

        /// <summary>
        /// summary: 回收一个条目视图到对象池
        /// param: view 要回收的条目
        /// return: 无
        /// </summary>
        private void RecycleView(LogEntryItemView view)
        {
            if (view == null) return;

            view.Release();
            view.transform.SetParent(null, false);
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        /// <summary>
        /// summary: 清理所有已创建视图（销毁时调用）
        /// param: 无
        /// return: 无
        /// </summary>
        private void ClearAllViews()
        {
            for (int i = 0; i < _activeViews.Count; i++)
                if (_activeViews[i] != null) Destroy(_activeViews[i].gameObject);
            _activeViews.Clear();

            while (_pool.Count > 0)
            {
                var v = _pool.Pop();
                if (v != null) Destroy(v.gameObject);
            }
        }

        /// <summary>
        /// summary: UI Sink（线程安全）：把日志塞进队列，主线程 Update 再处理
        /// param: 无
        /// return: 无
        /// </summary>
        private sealed class UILogSink : ILogSink
        {
            private readonly ConcurrentQueue<LogEvent> _queue;

            /// <summary>
            /// summary: 构造 UI Sink
            /// param: queue 跨线程队列
            /// return: 无
            /// </summary>
            public UILogSink(ConcurrentQueue<LogEvent> queue)
            {
                _queue = queue;
            }

            /// <summary>
            /// summary: 接收日志事件（可能在任意线程），仅入队
            /// param: e 日志事件
            /// return: 无
            /// </summary>
            public void Emit(in LogEvent e)
            {
                _queue.Enqueue(e);
            }
        }
    }
}
