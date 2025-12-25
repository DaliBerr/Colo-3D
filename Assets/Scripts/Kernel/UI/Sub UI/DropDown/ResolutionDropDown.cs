using System;
using System.Collections.Generic;
using Kernel;
using Lonize.Events;
using Lonize.Logging;
using Lonize.UI;
using TMPro;
using UnityEngine;

public class ResolutionDropDown : DropdownHolder
{
    public TMP_Dropdown _dropdown;

    public Vector2Int prev = new Vector2Int(1920, 1080);

    // 不再手写：运行时自动生成
    public List<string> _options = new List<string>();

    // 真实分辨率映射：index -> (width,height)
    private readonly List<Vector2Int> _resolutionMap = new List<Vector2Int>();
    private bool _suppressCallback;
    public override TMP_Dropdown dropdown => _dropdown;
    public override List<string> Options { get => _options; set => _options = value; }

    protected override void Start()
    {
        // 1) 从系统读取可用分辨率，生成 _options + _resolutionMap
        BuildResolutionOptions();

        // 2) 从设置里取上次分辨率，找默认索引
        prev = OptionsManager.Instance.Settings.Resolution;

        int defaultIndex = FindIndex(prev);

        // 如果设置里没有这个分辨率，就回退到当前窗口/当前分辨率
        if (defaultIndex < 0)
        {
            var current = new Vector2Int(Screen.width, Screen.height);
            defaultIndex = FindIndex(current);
        }

        if (defaultIndex < 0) defaultIndex = 0;

        // 3) 设置下拉框内容和默认项
        SetOptions(_options, defaultIndex);

        // 4) 监听变化：直接用映射表拿到真实分辨率，不再 Split('x') 解析字符串
        onValueChanged(OnResolutionChanged);
    }

    private void SetDropdownIndexSilently(int index)
    {
        if (index < 0) return;

        _suppressCallback = true;
        _dropdown.SetValueWithoutNotify(index);
        _dropdown.RefreshShownValue();
        _suppressCallback = false;
    }
    /// <summary>
    /// 从 Screen.resolutions 生成可选分辨率列表（去重并排序），并构建 index->分辨率映射。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    private void BuildResolutionOptions()
    {
        _options.Clear();
        _resolutionMap.Clear();

        // 去重：同分辨率可能因刷新率不同出现多条
        var set = new HashSet<Vector2Int>();

        foreach (var r in Screen.resolutions)
        {
            set.Add(new Vector2Int(r.width, r.height));
        }

        // 确保当前窗口尺寸也在列表里（有时 Windowed 下 Screen.resolutions 不包含它）
        set.Add(new Vector2Int(Screen.width, Screen.height));

        // 转成列表并排序（从大到小）
        var list = new List<Vector2Int>(set);
        list.Sort((a, b) =>
        {
            int cmpW = b.x.CompareTo(a.x);
            return cmpW != 0 ? cmpW : b.y.CompareTo(a.y);
        });

        foreach (var res in list)
        {
            _resolutionMap.Add(res);
            _options.Add($"{res.x} x {res.y}");
        }
    }

    /// <summary>
    /// 在映射表中查找某个分辨率对应的索引。
    /// </summary>
    /// <param name="res">目标分辨率</param>
    /// <return>索引；找不到返回 -1</return>
    private int FindIndex(Vector2Int res)
    {
        for (int i = 0; i < _resolutionMap.Count; i++)
        {
            if (_resolutionMap[i].x == res.x && _resolutionMap[i].y == res.y)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Dropdown 变化回调：将选中分辨率写入设置，并发布 SettingChanged 事件。
    /// </summary>
    /// <param name="index">选中项索引</param>
    /// <return>无</return>
    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _resolutionMap.Count) return;

        Vector2Int chosen = _resolutionMap[index];
        OptionsManager.Instance.Settings.Resolution = chosen;

        Events.eventBus?.Publish(new SettingChanged(true));
        // TODO: 添加确认弹窗，并计时回退
        UIManager.Instance.ShowModal<OptionConfirmPopupModal>();
    }

    private void Awake()
    {
        Events.eventBus.Subscribe<CancelSettingChange>(OnCancelSettingChange);
    }
    private void OnDestroy()
    {
        Events.eventBus.Unsubscribe<CancelSettingChange>(OnCancelSettingChange);
    }
    private void OnCancelSettingChange(CancelSettingChange evt)
    {
        // CancelChanges 后 Settings.Resolution 应该已经回到旧值
        // GameDebug.Log("[ResolutionDropDown] Reverting resolution selection due to CancelSettingChange event.");
        Vector2Int undoRes = OptionsManager.Instance.Settings.Resolution;
        // GameDebug.Log($"[ResolutionDropDown] Undo to resolution: {undoRes.x} x {undoRes.y}");
        int undoIndex = FindIndex(undoRes);
        if (undoIndex >= 0)
        {
            SetDropdownIndexSilently(undoIndex);
            _dropdown.RefreshShownValue();
        }
    }
}
