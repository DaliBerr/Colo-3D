using System;
using System.Collections.Generic;
using Lonize.Events;
using Lonize.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public abstract class DropdownHolder : MonoBehaviour
{
    public abstract TMP_Dropdown dropdown { get; }

    public abstract List<string> Options { get; set; }

    [SerializeField] private int _defaultIndex = 0;
    [SerializeField] private bool _autoInitOnStart = true;

    // 用于把 Action<int> 映射成 UnityAction<int>，方便后续移除监听
    private readonly Dictionary<Action<int>, UnityAction<int>> _listenerMap = new();

    /// <summary>
    /// Unity生命周期：自动初始化下拉框（可通过_autoInitOnStart开关控制）。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    protected virtual void Start()
    {
        if (_autoInitOnStart)
        {
            Init(_defaultIndex);
        }
    }

    /// <summary>
    /// 初始化下拉框：使用当前 Options 设定条目与默认项。
    /// </summary>
    /// <param name="defaultIndex">默认选中项索引（从0开始）</param>
    /// <return>无</return>
    public void Init(int defaultIndex = 0)
    {
        if (dropdown == null)
        {
            Debug.LogWarning($"[{nameof(DropdownHolder)}] dropdown 为空：{name}", this);
            return;
        }

        var items = Options ?? new List<string>();
        SetOptions(items, defaultIndex);
    }

    /// <summary>
    /// 设置下拉框条目，并指定默认选中项（默认不触发回调）。
    /// </summary>
    /// <param name="items">条目文本列表</param>
    /// <param name="defaultIndex">默认选中索引（从0开始）</param>
    /// <return>无</return>
    public void SetOptions(List<string> items, int defaultIndex = 0)
    {
        if (dropdown == null) return;

        items ??= new List<string>();

        dropdown.ClearOptions();
        dropdown.AddOptions(items);

        if (items.Count <= 0)
        {
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            return;
        }

        defaultIndex = Mathf.Clamp(defaultIndex, 0, items.Count - 1);
        dropdown.SetValueWithoutNotify(defaultIndex); // 不触发回调，避免初始化时误触发
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// 获取当前选中索引（从0开始）。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>当前选中索引；若dropdown为空返回-1</return>
    public int GetIndex()
    {
        if (dropdown == null) return -1;
        return dropdown.value;
    }

    /// <summary>
    /// 获取当前选中项文本。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>当前选中文本；若无有效选项则返回空字符串</return>
    public string GetText()
    {
        if (dropdown == null) return string.Empty;

        int idx = dropdown.value;
        if (idx < 0 || idx >= dropdown.options.Count) return string.Empty;

        return dropdown.options[idx].text ?? string.Empty;
    }

    /// <summary>
    /// 设置当前选中项（可选择是否触发回调）。
    /// </summary>
    /// <param name="index">要选中的索引（从0开始）</param>
    /// <param name="notify">是否触发 OnValueChanged 回调</param>
    /// <return>无</return>
    public void SetValue(int index, bool notify = false)
    {
        if (dropdown == null) return;
        if (dropdown.options.Count <= 0) return;

        index = Mathf.Clamp(index, 0, dropdown.options.Count - 1);

        if (notify) dropdown.value = index;
        else dropdown.SetValueWithoutNotify(index);

        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// 注册下拉框选项变化回调（参数为选中索引）。
    /// </summary>
    /// <param name="callback">回调函数（Action&lt;int&gt;）</param>
    /// <return>无</return>
    public void onValueChanged(Action<int> callback)
    {
        if (dropdown == null || callback == null) return;

        // 避免重复添加同一个回调
        if (_listenerMap.ContainsKey(callback)) return;

        UnityAction<int> ua = callback.Invoke;
        _listenerMap.Add(callback, ua);
        dropdown.onValueChanged.AddListener(ua);
        
    }

    /// <summary>
    /// 移除已注册的选项变化回调。
    /// </summary>
    /// <param name="callback">之前注册过的回调</param>
    /// <return>是否成功移除</return>
    public bool RemoveValueChanged(Action<int> callback)
    {
        if (dropdown == null || callback == null) return false;

        if (_listenerMap.TryGetValue(callback, out var ua))
        {
            dropdown.onValueChanged.RemoveListener(ua);
            _listenerMap.Remove(callback);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 清空所有通过本类注册的回调监听。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    public void ClearValueChanged()
    {
        if (dropdown == null) return;

        foreach (var kv in _listenerMap)
            dropdown.onValueChanged.RemoveListener(kv.Value);

        _listenerMap.Clear();
    }
}
