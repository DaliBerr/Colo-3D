using System;
using System.Collections.Generic;
using Lonize.Events;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class ToggleHolder : MonoBehaviour
{
    public abstract Toggle toggle { get; }

    [SerializeField] private bool _defaultValue = false;
    [SerializeField] private bool _autoInitOnStart = true;

    // 用于把 Action<bool> 映射成 UnityAction<bool>，方便后续移除监听
    private readonly Dictionary<Action<bool>, UnityAction<bool>> _listenerMap = new();

    /// <summary>
    /// Unity生命周期：自动初始化开关（可通过_autoInitOnStart开关控制）。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    protected virtual void Start()
    {
        if (_autoInitOnStart)
        {
            Init(_defaultValue);
        }
    }

    /// <summary>
    /// 初始化开关：设置默认值（默认不触发回调）。
    /// </summary>
    /// <param name="defaultValue">默认开关状态</param>
    /// <return>无</return>
    public void Init(bool defaultValue = false)
    {
        if (toggle == null)
        {
            Debug.LogWarning($"[{nameof(ToggleHolder)}] toggle 为空：{name}", this);
            return;
        }

        SetValue(defaultValue, notify: false);
    }

    /// <summary>
    /// 获取当前开关状态。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>当前状态；若toggle为空返回false</return>
    public bool GetValue()
    {
        if (toggle == null) return false;
        return toggle.isOn;
    }

    /// <summary>
    /// 设置开关状态（可选择是否触发回调）。
    /// </summary>
    /// <param name="value">要设置的状态</param>
    /// <param name="notify">是否触发 OnValueChanged 回调</param>
    /// <return>无</return>
    public void SetValue(bool value, bool notify = false)
    {
        if (toggle == null) return;

        if (notify) toggle.isOn = value;
        else toggle.SetIsOnWithoutNotify(value);
    }

    /// <summary>
    /// 注册开关变化回调（参数为bool）。
    /// </summary>
    /// <param name="callback">回调函数（Action&lt;bool&gt;）</param>
    /// <return>无</return>
    public void onValueChanged(Action<bool> callback)
    {
        if (toggle == null || callback == null) return;

        // 避免重复添加同一个回调
        if (_listenerMap.ContainsKey(callback)) return;

        // 包一层：先执行你的逻辑，再发SettingChanged事件
        UnityAction<bool> ua = v =>
        {
            callback(v);
            Events.eventBus?.Publish(new SettingChanged(true));
        };

        _listenerMap.Add(callback, ua);
        toggle.onValueChanged.AddListener(ua);

        GameDebug.Log($"[{nameof(ToggleHolder)}] 注册Toggle变化回调：{name}");
    }

    /// <summary>
    /// 移除已注册的开关变化回调。
    /// </summary>
    /// <param name="callback">之前注册过的回调</param>
    /// <return>是否成功移除</return>
    public bool RemoveValueChanged(Action<bool> callback)
    {
        if (toggle == null || callback == null) return false;

        if (_listenerMap.TryGetValue(callback, out var ua))
        {
            toggle.onValueChanged.RemoveListener(ua);
            _listenerMap.Remove(callback);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 清空所有通过本类注册的开关回调监听。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    public void ClearValueChanged()
    {
        if (toggle == null) return;

        foreach (var kv in _listenerMap)
            toggle.onValueChanged.RemoveListener(kv.Value);

        _listenerMap.Clear();
    }
}
