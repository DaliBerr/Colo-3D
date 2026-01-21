using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Lonize.Tick
{
    /// <summary>
    /// 游戏速度档位。
    /// </summary>
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,   // 1x
        Fast = 2,     // 2x
        SuperFast = 3,
        Custom = 99
    }

    /// <summary>
    /// 可被 TickManager 驱动的对象接口。
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// 执行一次或多次 Tick（本实现默认每次调用传入 1）。
        /// </summary>
        /// <param name="ticks">Tick 次数（通常为 1）。</param>
        /// <returns>无。</returns>
        void Tick(int ticks);
    }

    /// <summary>
    /// 控制仿真流速与 Tick 累积，不直接依赖 Unity 的时间缩放。
    /// </summary>
    public sealed class TickController
    {
        /// <summary>基础仿真 Tick 长度（秒），例如 1/60 = 0.0166667。</summary>
        public float BaseTickSeconds = 1f / 60f;

        /// <summary>每帧最多“允许执行”的 Tick 数（安全阀）。0 或负数=不限制（不推荐）。</summary>
        public int MaxTicksPerFrame = 8;

        /// <summary>
        /// 每帧仿真预算（毫秒）。用于限制追帧成本，<=0 表示不限制。
        /// </summary>
        public float MaxSimulateMillisecondsPerFrame = 2.0f;

        /// <summary>当前速度倍数（0=暂停）。</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>当前速度档位。</summary>
        public GameSpeed SpeedPreset { get; private set; } = GameSpeed.Normal;

        /// <summary>仿真累计时间（从启动以来）。</summary>
        public double SimulatedSeconds { get; private set; }

        /// <summary>速度变化事件（档位，倍速）。</summary>
        public event Action<GameSpeed, float> OnSpeedChanged;

        private float _acc; // 累积器（已经乘过速度）

        /// <summary>
        /// 获取渲染插值系数 alpha（0~1），表示当前帧位于“上一 Tick”到“下一 Tick”的进度。
        /// </summary>
        /// <returns>alpha（0~1）。</returns>
        public float GetAlpha()
        {
            if (BaseTickSeconds <= 1e-6f) return 0f;
            return Mathf.Clamp01(_acc / BaseTickSeconds);
        }

        /// <summary>
        /// alpha 的便捷属性访问（0~1）。
        /// </summary>
        public float Alpha => GetAlpha();

        /// <summary>
        /// 设置预设速度档位。
        /// </summary>
        /// <param name="preset">目标档位。</param>
        /// <returns>无。</returns>
        public void SetPreset(GameSpeed preset)
        {
            switch (preset)
            {
                case GameSpeed.Paused:    SetCustomMultiplier(0f, false); break;
                case GameSpeed.Normal:    SetCustomMultiplier(1f, false); break;
                case GameSpeed.Fast:      SetCustomMultiplier(2f, false); break;
                case GameSpeed.SuperFast: SetCustomMultiplier(3f, false); break;
                default:                  SetCustomMultiplier(1f, false); break;
            }
            SpeedPreset = preset;
            OnSpeedChanged?.Invoke(SpeedPreset, SpeedMultiplier);
        }

        /// <summary>
        /// 设置自定义速度倍数。
        /// </summary>
        /// <param name="mult">速度倍数（>=0）。</param>
        /// <param name="setPresetToCustom">是否将档位标记为 Custom。</param>
        /// <returns>无。</returns>
        public void SetCustomMultiplier(float mult, bool setPresetToCustom = true)
        {
            SpeedMultiplier = Mathf.Max(0f, mult);
            if (setPresetToCustom) SpeedPreset = GameSpeed.Custom;
            OnSpeedChanged?.Invoke(SpeedPreset, SpeedMultiplier);
        }

        /// <summary>
        /// 将外部经过的真实时间累加到内部累积器（内部会乘以速度倍数）。
        /// </summary>
        /// <param name="deltaUnscaledSeconds">外部真实秒数（建议 unscaled）。</param>
        /// <returns>无。</returns>
        public void Accumulate(float deltaUnscaledSeconds)
        {
            if (BaseTickSeconds <= 1e-6f) return;
            if (deltaUnscaledSeconds <= 0f) return;

            float dt = deltaUnscaledSeconds * SpeedMultiplier;
            _acc += dt;
        }

        /// <summary>
        /// 计算当前累积器中可执行的 Tick 数（不消耗，只计算）。
        /// </summary>
        /// <returns>可执行 Tick 数。</returns>
        public int GetAvailableTicks()
        {
            if (BaseTickSeconds <= 1e-6f) return 0;
            if (_acc < BaseTickSeconds) return 0;

            int available = (int)(_acc / BaseTickSeconds);
            int cap = (MaxTicksPerFrame <= 0) ? int.MaxValue : MaxTicksPerFrame;
            return Mathf.Min(available, cap);
        }

        /// <summary>
        /// 消耗已实际执行的 Tick 数，并推进仿真累计时间。
        /// </summary>
        /// <param name="executedTicks">已执行 Tick 数。</param>
        /// <returns>无。</returns>
        public void ConsumeExecutedTicks(int executedTicks)
        {
            if (executedTicks <= 0) return;
            if (BaseTickSeconds <= 1e-6f) return;

            float consume = executedTicks * BaseTickSeconds;
            _acc = Mathf.Max(0f, _acc - consume);
            SimulatedSeconds += consume;
        }

        /// <summary>
        /// 在暂停或任意档位下，强制推进一个基础 Tick（用于单步）。
        /// </summary>
        /// <returns>无。</returns>
        public void StepOneTick()
        {
            _acc += BaseTickSeconds;
        }

        /// <summary>
        /// 清空累积器（常用于切场景或长任务后重置）。
        /// </summary>
        /// <returns>无。</returns>
        public void ResetAccumulation()
        {
            _acc = 0f;
        }
    }

    /// <summary>
    /// 统一驱动所有 ITickable。由 TickDriver 从 Unity 帧循环里喂时间。
    /// </summary>
    public sealed class TickManager
    {
        private readonly List<ITickable> _list = new(64);

        // 改动 2：缓存快照，避免每帧 ToArray 产生 GC
        private ITickable[] _snapshot = Array.Empty<ITickable>();
        private bool _dirtySnapshot = true;

        // 改动 4：预算驱动追帧（Stopwatch 复用避免分配）
        private readonly Stopwatch _budgetWatch = new Stopwatch();

        /// <summary>时间控制器（速度、累积、alpha 等）。</summary>
        public readonly TickController TimeCtrl = new();

        /// <summary>
        /// 注册一个可被 Tick 驱动的对象。
        /// </summary>
        /// <param name="t">要注册的对象。</param>
        /// <returns>无。</returns>
        public void Register(ITickable t)
        {
            if (t == null) return;
            if (_list.Contains(t)) return;

            _list.Add(t);
            _dirtySnapshot = true;
        }

        /// <summary>
        /// 注销一个可被 Tick 驱动的对象。
        /// </summary>
        /// <param name="t">要注销的对象。</param>
        /// <returns>无。</returns>
        public void Unregister(ITickable t)
        {
            if (t == null) return;

            if (_list.Remove(t))
                _dirtySnapshot = true;
        }

        /// <summary>
        /// 把 Unity 的 delta（推荐 unscaled）交进来，内部按速度换算并派发 Tick。
        /// </summary>
        /// <param name="unscaledDeltaSeconds">真实经过时间（建议 unscaled）。</param>
        /// <returns>无。</returns>
        public void Update(float unscaledDeltaSeconds)
        {
            // 先累积时间（内部会乘速度倍数）
            TimeCtrl.Accumulate(unscaledDeltaSeconds);

            int planned = TimeCtrl.GetAvailableTicks();
            if (planned <= 0) return;

            // 刷新快照（仅在注册/注销发生时）
            if (_dirtySnapshot)
            {
                _snapshot = _list.ToArray();
                _dirtySnapshot = false;
            }

            float budgetMs = TimeCtrl.MaxSimulateMillisecondsPerFrame;
            bool useBudget = budgetMs > 0f;

            if (useBudget)
                _budgetWatch.Restart();

            int executed = 0;

            // 注意：按你的要求保留外层逐 Tick 调用（不做 Tick(n) 批处理）
            for (int tick = 0; tick < planned; tick++)
            {
                // 预算检查：尽量不在追帧时把自己卡死
                if (useBudget && _budgetWatch.Elapsed.TotalMilliseconds >= budgetMs)
                    break;

                for (int i = 0; i < _snapshot.Length; i++)
                    _snapshot[i]?.Tick(1);

                executed++;
            }

            // 只消耗“真正执行了”的 Tick，剩余会留在累积器里下帧继续追
            TimeCtrl.ConsumeExecutedTicks(executed);
        }

        /// <summary>
        /// 订阅速度变化事件（档位与倍速）。
        /// </summary>
        /// <param name="handler">事件处理器。</param>
        /// <returns>无。</returns>
        public void OnSpeedChanged(Action<GameSpeed, float> handler)
        {
            TimeCtrl.OnSpeedChanged += handler;
        }

        /// <summary>
        /// 获取渲染插值系数 alpha（0~1），用于渲染层插值。
        /// </summary>
        /// <returns>alpha（0~1）。</returns>
        public float GetRenderAlpha()
        {
            return TimeCtrl.Alpha;
        }
    }
}
