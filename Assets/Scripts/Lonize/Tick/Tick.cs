using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lonize.Tick
{
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,   // 1x
        Fast = 2,     // 2x
        SuperFast = 3,
        Custom = 99
    }

    public interface ITickable
    {
        // ticks 参数可用于累加内部计数（一般=1）
        void Tick(int ticks);
    }

    /// <summary>
    /// 控制仿真流速与 Tick 累积，不直接依赖 Unity 的时间缩放。
    /// </summary>
    public sealed class TickController
    {
        /// <summary>基础仿真 Tick 长度（秒），例如 1/60 = 0.0166667。</summary>
        public float BaseTickSeconds = 1f / 60f;

        /// <summary>每帧最多执行多少个 Tick，防止追帧过多导致卡顿。0 或负数=不限制（不推荐）。</summary>
        public int MaxTicksPerFrame = 8;

        /// <summary>当前速度倍数（0=暂停）。</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>当前速度档位。</summary>
        public GameSpeed SpeedPreset { get; private set; } = GameSpeed.Normal;

        /// <summary>仿真累计时间（从启动以来）。</summary>
        public double SimulatedSeconds { get; private set; }

        /// <summary>速度变化事件（档位，倍速）。</summary>
        public event Action<GameSpeed, float> OnSpeedChanged;

        private float _acc; // 累积器

        public void SetPreset(GameSpeed preset)
        {
            switch (preset)
            {
                case GameSpeed.Paused:     SetCustomMultiplier(0f, false); break;
                case GameSpeed.Normal:     SetCustomMultiplier(1f, false); break;
                case GameSpeed.Fast:       SetCustomMultiplier(2f, false); break;
                case GameSpeed.SuperFast:  SetCustomMultiplier(3f, false); break;
                default:                   SetCustomMultiplier(1f, false); break;
            }
            SpeedPreset = preset;
            OnSpeedChanged?.Invoke(SpeedPreset, SpeedMultiplier);
        }

        public void SetCustomMultiplier(float mult, bool setPresetToCustom = true)
        {
            SpeedMultiplier = Mathf.Max(0f, mult);
            if (setPresetToCustom) SpeedPreset = GameSpeed.Custom;
            OnSpeedChanged?.Invoke(SpeedPreset, SpeedMultiplier);
        }

        /// <summary>
        /// 把“外部经过的真实秒数”乘以当前倍速后，换算成应当执行的 Tick 数。
        /// 建议传入 Time.unscaledDeltaTime，避免和 Unity 的 timeScale 双重叠加。
        /// </summary>
        public int ConsumeAndComputeTicks(float deltaUnscaledSeconds)
        {
            var dt = deltaUnscaledSeconds * SpeedMultiplier;
            _acc += dt;

            int produced = 0;
            int cap = MaxTicksPerFrame <= 0 ? int.MaxValue : MaxTicksPerFrame;

            while (_acc >= BaseTickSeconds && produced < cap)
            {
                _acc -= BaseTickSeconds;
                produced++;
                SimulatedSeconds += BaseTickSeconds;
            }
            return produced;
        }

        /// <summary>在暂停或任意档位下，强制推进一个基础 Tick（用于单步）。</summary>
        public void StepOneTick()
        {
            _acc += BaseTickSeconds;
        }

        /// <summary>清空累积，常用于切场景或长任务后重置。</summary>
        public void ResetAccumulation() { _acc = 0f; }
    }

    /// <summary>
    /// 统一驱动所有 ITickable。由 TickDriver 从 Unity 帧循环里喂时间。
    /// </summary>
    public sealed class TickManager
    {
        private readonly List<ITickable> _list = new(64);
        public readonly TickController TimeCtrl = new();

        public void Register(ITickable t)
        {
            if (t != null && !_list.Contains(t)) _list.Add(t);
        }
        public void Unregister(ITickable t)
        {
            if (t != null) _list.Remove(t);
        }

        /// <summary>把 Unity 的 delta（推荐 unscaled）交进来，内部按速度换算并派发 Tick。</summary>
        public void Update(float unscaledDeltaSeconds)
        {
            int n = TimeCtrl.ConsumeAndComputeTicks(unscaledDeltaSeconds);
            if (n <= 0) return;

            // 为避免订阅过程修改列表造成遍历问题，这里快照一次
            var snapshot = _list.ToArray();
            for (int tick = 0; tick < n; tick++)
            {
                for (int i = 0; i < snapshot.Length; i++)
                    snapshot[i]?.Tick(1);
            }
        }

        public void OnSpeedChanged(Action<GameSpeed, float> handler)
        {
            TimeCtrl.OnSpeedChanged += handler;
        }
    }




}