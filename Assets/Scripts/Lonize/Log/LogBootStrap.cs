
using System.IO;
using UnityEngine;

namespace Lonize.Logging
{
        /// <summary>
    /// summary: 游戏启动前初始化日志系统（配置输出目标）
    /// </summary>
    /// <returns>无</returns>
    public static class LogBootstrap
    {
        /// <summary>
        /// summary: 在加载首个场景前设置日志等级和输出目标
        /// </summary>
        /// <returns>无</returns>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 开发阶段可以用 Debug，正式版可以调成 Info/Warn
            Log.MinLevel = LogLevel.Debug;

            // 输出到 System.Console（主要是编辑器外的控制台）
            Log.AddSink(new ConsoleSink());

            // 输出到本地日志文件
            string logPath = Path.Combine(Application.persistentDataPath, "game.log");
            Log.AddSink(new FileSink(logPath));

            // ❗注意：这里**不再**添加 UnitySink，
            // 这样 Log 的输出不会再回到 Unity Console，避免抢占双击跳转。
        }
    }
}