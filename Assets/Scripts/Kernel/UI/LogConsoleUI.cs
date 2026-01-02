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

        [Header("Repeat Collapse")]
        public bool CollapseConsecutiveDuplicates = true;

        [Header("Level Colors (Text/Stripe)")]
        public Color TraceColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        public Color DebugColor = new Color(0.55f, 0.75f, 1f, 1f);
        public Color InfoColor  = new Color(0.95f, 0.95f, 0.95f, 1f);
        public Color WarnColor  = new Color(1f, 0.85f, 0.35f, 1f);
        public Color ErrorColor = new Color(1f, 0.45f, 0.45f, 1f);
        public Color FatalColor = new Color(1f, 0.2f, 0.2f, 1f);

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        private readonly ConcurrentQueue<LogEvent> _pending = new();

        private readonly List<EventRecord> _events = new();
        private readonly List<int> _visibleEventIndices = new();

        private readonly List<LogEntryItemView> _activeViews = new();
        private readonly Stack<LogEntryItemView> _pool = new();

        private ILogSink _uiSink;
        private bool _paused;
        private LogLevel _minLevel = LogLevel.Info;
        private int _selectedVisibleIndex = -1;

        private enum AppendOutcome
        {
            None,
            AddedNew,
            IncrementedLast,
            Trimmed
        }

        private struct EventRecord
        {
            public LogEvent Event;
            public int Repeat;
        }

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
            RefreshList(autoScrollHint: false);

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

            bool wasNearBottom = IsListNearBottom();

            bool anyAdded = false;
            bool anyTrimmed = false;
            bool onlyIncrementedLast = true;

            while (_pending.TryDequeue(out var e))
            {
                var outcome = AppendOrIncrement(e);

                if (outcome == AppendOutcome.AddedNew) anyAdded = true;
                if (outcome == AppendOutcome.Trimmed) anyTrimmed = true;

                if (outcome != AppendOutcome.IncrementedLast)
                    onlyIncrementedLast = false;
            }

            if (!anyAdded && !anyTrimmed && !onlyIncrementedLast)
                return;

            bool shouldAutoScroll = AutoScrollWhenNearBottom && wasNearBottom;

            if (anyAdded || anyTrimmed)
            {
                // 新增条目或裁剪，直接全刷新最稳
                RefreshList(autoScrollHint: shouldAutoScroll);
            }
            else if (onlyIncrementedLast)
            {
                // 只有最后一条重复次数变化：尝试只更新最后一项，避免全量重建
                if (!TryUpdateLastRepeatVisual(shouldAutoScroll))
                {
                    RefreshList(autoScrollHint: shouldAutoScroll);
                }
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
        /// summary: 追加日志或合并到最后一条（连续重复计数，最大 99）
        /// param: e 日志事件
        /// return: 追加结果
        /// </summary>
        private AppendOutcome AppendOrIncrement(in LogEvent e)
        {
            if (CollapseConsecutiveDuplicates && _events.Count > 0)
            {
                int lastIndex = _events.Count - 1;
                var last = _events[lastIndex];

                if (IsSameKey(last.Event, e))
                {
                    if (last.Repeat < 99) last.Repeat++;
                    _events[lastIndex] = last;
                    return AppendOutcome.IncrementedLast;
                }
            }

            _events.Add(new EventRecord { Event = e, Repeat = 1 });

            if (MaxLines > 0 && _events.Count > MaxLines)
            {
                int removeCount = _events.Count - MaxLines;
                _events.RemoveRange(0, removeCount);

                _selectedVisibleIndex = -1;
                if (DetailText != null) DetailText.text = string.Empty;

                return AppendOutcome.Trimmed;
            }

            return AppendOutcome.AddedNew;
        }

        /// <summary>
        /// summary: 判定两条日志是否视为“同一条”（用于连续合并）
        /// param: a 前一条
        /// param: b 新一条
        /// return: 是否相同
        /// </summary>
        private bool IsSameKey(in LogEvent a, in LogEvent b)
        {
            // 只比较“内容/来源”字段，排除时间/线程等会变化的字段
            if (a.Level != b.Level) return false;
            if (!string.Equals(a.Category, b.Category, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Message, b.Message, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.File, b.File, StringComparison.Ordinal)) return false;
            if (a.Line != b.Line) return false;
            if (!string.Equals(a.Member, b.Member, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// summary: 加载最近日志快照（按时间从旧到新放入，避免顺序混乱）
        /// param: 无
        /// return: 无
        /// </summary>
        private void LoadRecent()
        {
            _events.Clear();

            var recent = Log.SnapshotRecent(Mathf.Max(1, LoadRecentCount));
            // SnapshotRecent 通常是“新 -> 旧”，这里反向插入，让列表“旧 -> 新”
            for (int i = recent.Length - 1; i >= 0; i--)
            {
                AppendOrIncrement(recent[i]); // 顺便把连续重复压缩掉
            }
        }

        /// <summary>
        /// summary: 刷新列表（重建可见索引 + 复用/回收条目，可选自动滚动）
        /// param: autoScrollHint 是否提示滚动到底部
        /// return: 无
        /// </summary>
        private void RefreshList(bool autoScrollHint)
        {
            BuildVisibleIndices();
            RebuildViews();
            EnsureSelectionValid();

            if (autoScrollHint)
                ScrollListToBottom();
        }

        /// <summary>
        /// summary: 仅更新最后一条的重复次数显示（成功则不需要全刷新）
        /// param: autoScrollHint 是否滚动到底部
        /// return: 是否成功更新
        /// </summary>
        private bool TryUpdateLastRepeatVisual(bool autoScrollHint)
        {
            if (_events.Count <= 0) return false;

            int lastEventIndex = _events.Count - 1;
            var rec = _events[lastEventIndex];

            // 最后一条如果被过滤掉，就没法只更新可见项
            if (rec.Event.Level < _minLevel) return false;

            // 可见索引必须已存在并且最后一项正好指向 lastEventIndex
            if (_visibleEventIndices.Count <= 0) return false;

            int lastVisibleIndex = _visibleEventIndices.Count - 1;
            if (_visibleEventIndices[lastVisibleIndex] != lastEventIndex) return false;
            if (lastVisibleIndex < 0 || lastVisibleIndex >= _activeViews.Count) return false;

            var view = _activeViews[lastVisibleIndex];
            if (view == null) return false;

            Color c = ResolveLevelColor(rec.Event.Level);
            view.Bind(lastVisibleIndex, rec.Event, rec.Repeat, PreviewMaxChars, c, c, OnClickEntry);
            view.SetSelected(lastVisibleIndex == _selectedVisibleIndex);

            if (_selectedVisibleIndex == lastVisibleIndex)
                UpdateDetail(lastVisibleIndex);

            if (autoScrollHint)
                ScrollListToBottom();

            return true;
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

            if (contentH <= viewH + 0.5f)
                return true;

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
                if (_events[i].Event.Level < _minLevel) continue;
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
                var rec = _events[eventIndex];

                Color c = ResolveLevelColor(rec.Event.Level);
                view.Bind(v, rec.Event, rec.Repeat, PreviewMaxChars, c, c, OnClickEntry);
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
        /// summary: 更新详情面板（并按等级给详情整体上色；重复次数>1时显示次数）
        /// param: visibleIndex 可见列表索引
        /// return: 无
        /// </summary>
        private void UpdateDetail(int visibleIndex)
        {
            if (DetailText == null) return;
            if (visibleIndex < 0 || visibleIndex >= _visibleEventIndices.Count) return;

            int eventIndex = _visibleEventIndices[visibleIndex];
            var rec = _events[eventIndex];

            DetailText.text = FormatDetail(rec);
            DetailText.color = ResolveLevelColor(rec.Event.Level);

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
        /// summary: 格式化详情文本（通用格式 + exception + 重复次数）
        /// param: rec 日志记录（含重复次数）
        /// return: 详情字符串
        /// </summary>
        private string FormatDetail(in EventRecord rec)
        {
            var e = rec.Event;
            var sb = new StringBuilder(640);

            if (rec.Repeat > 1)
                sb.AppendLine($"重复次数：{Math.Min(rec.Repeat, 99)}");

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
            StartCoroutine(UIManager.Instance.PopScreenAndWait());
        }

        /// <summary>
        /// summary: 清空缓存并刷新
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClickClear()
        {
            _events.Clear();
            _selectedVisibleIndex = -1;
            if (DetailText != null) DetailText.text = string.Empty;
            RefreshList(autoScrollHint: false);
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
            RefreshList(autoScrollHint: false);
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
