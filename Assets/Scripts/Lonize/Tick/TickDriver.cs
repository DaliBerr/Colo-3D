using UnityEngine;

namespace Lonize.Tick
{
    /// <summary>
    /// 把 Unity 帧循环与 TickManager 连接起来的 MonoBehaviour。
    /// </summary>
    public sealed class TickDriver : MonoBehaviour
    {
        /// <summary>全局单例引用。</summary>
        public static TickDriver Instance { get; private set; }

        [Tooltip("驱动用的 TickManager，可在外部注入；为空则内部创建一个。")]
        public TickManager tickManager;

        [Tooltip("使用 Unscaled 时间，避免与 Time.timeScale 双重叠加。")]
        public bool useUnscaledTime = true;

        [Tooltip("是否让 Unity 的 Time.timeScale 跟随速度倍数（受限于 minTimeScale）。")]
        public bool affectUnityTimeScale = false;

        [Tooltip("最小 Time.timeScale，避免设置为 0 导致协程/动画逻辑特例。")]
        public float minTimeScale = 0f;

        [Header("Delta Clamp")]
        [Tooltip("单帧最大可计入仿真的真实时间（秒）。用于避免切窗/卡顿造成巨额追帧欠账。<=0 表示不限制。")]
        public float maxFrameDeltaSeconds = 0.1f;

        /// <summary>
        /// 获取当前渲染插值系数 alpha（0~1），用于渲染层插值。
        /// </summary>
        /// <returns>alpha（0~1）。</returns>
        public float GetRenderAlpha()
        {
            return tickManager?.TimeCtrl.Alpha ?? 0f;
        }

        /// <summary>
        /// Unity Awake：初始化单例并确保 TickManager 存在。
        /// </summary>
        /// <returns>无。</returns>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (tickManager == null)
                tickManager = new TickManager();
        }

        /// <summary>
        /// Unity Update：采样 dt、可选同步 timeScale，并驱动 TickManager。
        /// </summary>
        /// <returns>无。</returns>
        private void Update()
        {
            float dtRaw = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            // 改动 1：dt clamp，避免切窗/卡顿带来巨额追帧欠账
            float dt = (maxFrameDeltaSeconds > 0f) ? Mathf.Min(dtRaw, maxFrameDeltaSeconds) : dtRaw;

            if (affectUnityTimeScale && tickManager != null)
            {
                // 注意：Paused 时把 timeScale 设为 0 可能会影响协程/动画/物理
                float ts = tickManager.TimeCtrl.SpeedMultiplier;
                if (ts < minTimeScale) ts = minTimeScale;
                Time.timeScale = ts;
            }

            tickManager?.Update(dt);
        }

        /// <summary>
        /// Unity OnDestroy：清理单例引用。
        /// </summary>
        /// <returns>无。</returns>
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
