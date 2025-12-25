using UnityEngine;

namespace Lonize.Tick
{
        /// <summary>
    /// 把 Unity 帧循环与 TickManager 连接起来的 MonoBehaviour。
    /// </summary>
    public sealed class TickDriver : MonoBehaviour
    {
        [Tooltip("驱动用的 TickManager，可在外部注入；为空则内部创建一个。")]
        public TickManager tickManager;

        [Tooltip("使用 Unscaled 时间，避免与 Time.timeScale 双重叠加。")]
        public bool useUnscaledTime = true;

        [Tooltip("是否让 Unity 的 Time.timeScale 跟随速度倍数（受限于 minTimeScale）。")]
        public bool affectUnityTimeScale = false;

        [Tooltip("最小 Time.timeScale，避免设置为 0 导致协程/动画逻辑特例。")]
        public float minTimeScale = 0f;

        private void Awake()
        {
            if (tickManager == null) tickManager = new TickManager();
        }

        private void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (affectUnityTimeScale)
            {
                // 注意：Paused 时把 timeScale 设为 0 可能会影响协程/动画/物理
                float ts = tickManager.TimeCtrl.SpeedMultiplier;
                if (ts < minTimeScale) ts = minTimeScale;
                Time.timeScale = ts;
            }

            tickManager?.Update(dt);
        }
    }
}