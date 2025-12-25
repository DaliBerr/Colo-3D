using System;
using System.Collections.Generic;
using Lonize.Events;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class SliderHolder : MonoBehaviour
{
    public abstract Slider slider { get; }

    [SerializeField] private float _defaultValue = 0f;
    [SerializeField] private bool _autoInitOnStart = true;

    // 用于把 Action<float> 映射成 UnityAction<float>，方便后续移除监听
    private readonly Dictionary<Action<float>, UnityAction<float>> _listenerMap = new();

    /// <summary>
    /// Unity生命周期：自动初始化滑动条（可通过_autoInitOnStart开关控制）。
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
    /// 初始化滑动条：设置默认值（默认不触发回调）。
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <return>无</return>
    public void Init(float defaultValue = 0f)
    {
        if (slider == null)
        {
            Debug.LogWarning($"[{nameof(SliderHolder)}] slider 为空：{name}", this);
            return;
        }

        SetValue(defaultValue, notify: false);
    }

    /// <summary>
    /// 设置滑动条范围，并可选择保持当前值在范围内。
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="keepCurrent">是否将当前值Clamp进新范围</param>
    /// <return>无</return>
    public void SetRange(float min, float max, bool keepCurrent = true)
    {
        if (slider == null) return;

        slider.minValue = min;
        slider.maxValue = max;

        if (keepCurrent)
        {
            SetValue(slider.value, notify: false);
        }
    }

    /// <summary>
    /// 获取当前滑动条数值。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>当前值；若slider为空返回0</return>
    public float GetValue()
    {
        if (slider == null) return 0f;
        return slider.value;
    }

    /// <summary>
    /// 设置滑动条数值（可选择是否触发回调）。
    /// </summary>
    /// <param name="value">要设置的值</param>
    /// <param name="notify">是否触发 OnValueChanged 回调</param>
    /// <return>无</return>
    public void SetValue(float value, bool notify = false)
    {
        if (slider == null) return;

        float clamped = Mathf.Clamp(value, slider.minValue, slider.maxValue);

        if (notify) slider.value = clamped;
        else slider.SetValueWithoutNotify(clamped);
    }

    /// <summary>
    /// 注册滑动条数值变化回调（参数为float）。
    /// </summary>
    /// <param name="callback">回调函数（Action&lt;float&gt;）</param>
    /// <return>无</return>
    public void onValueChanged(Action<float> callback)
    {
        if (slider == null || callback == null) return;

        // 避免重复添加同一个回调
        if (_listenerMap.ContainsKey(callback)) return;

        // 包一层：先执行你的逻辑，再发SettingChanged事件
        UnityAction<float> ua = v =>
        {
            callback(v);
            Events.eventBus?.Publish(new SettingChanged(true));
        };

        _listenerMap.Add(callback, ua);
        slider.onValueChanged.AddListener(ua);

        GameDebug.Log($"[{nameof(SliderHolder)}] 注册滑动条变化回调：{name}");
    }

    /// <summary>
    /// 移除已注册的滑动条变化回调。
    /// </summary>
    /// <param name="callback">之前注册过的回调</param>
    /// <return>是否成功移除</return>
    public bool RemoveValueChanged(Action<float> callback)
    {
        if (slider == null || callback == null) return false;

        if (_listenerMap.TryGetValue(callback, out var ua))
        {
            slider.onValueChanged.RemoveListener(ua);
            _listenerMap.Remove(callback);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 清空所有通过本类注册的滑动条回调监听。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>无</return>
    public void ClearValueChanged()
    {
        if (slider == null) return;

        foreach (var kv in _listenerMap)
            slider.onValueChanged.RemoveListener(kv.Value);

        _listenerMap.Clear();
    }
}
