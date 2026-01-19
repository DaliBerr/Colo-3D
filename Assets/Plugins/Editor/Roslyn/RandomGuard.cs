#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

namespace RandomGuard
{
    /// <summary>
    /// 在 Unity Editor 中扫描 C# 源码，检测并禁止对 System.Random 的直接使用。
    /// 通过监听脚本编译完成事件与进入 Play 模式事件，在控制台输出错误并可阻止进入 Play。
    /// </summary>
    [InitializeOnLoad]
    public static class SystemRandomBlocker
    {
        // 你可以按需改成自己的随机入口，例如 "Lonize.Random" 或 "MyRandom"
        private const string ReplacementHint = "请改用：Lonize.Random";

        // 是否在发现违规时阻止进入 Play 模式
        private const bool BlockEnterPlayMode = true;

        // 扫描范围：可按需排除第三方目录
        private static readonly string[] ExcludedPathContains = new[]
        {
            "/Packages/",
            "/Library/",
            "/Temp/",
            "/obj/",
            "/Logs/",
            "/Assets/Plugins/",
            "/Assets/ThirdParty/",
        };

        // 违规匹配：尽量避免误报（比如 "MySystem.Randomizer"）
        // 1) new System.Random(...)
        // 2) typeof(System.Random)
        // 3) System.Random 作为类型/成员访问
        private static readonly Regex[] ForbiddenPatterns = new[]
        {
            new Regex(@"\bnew\s+System\.Random\s*\(", RegexOptions.Compiled),
            new Regex(@"\btypeof\s*\(\s*System\.Random\s*\)", RegexOptions.Compiled),
            new Regex(@"\bSystem\.Random\b", RegexOptions.Compiled),
        };

        static SystemRandomBlocker()
        {
            // 脚本编译完成后扫描一次
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // 进入 Play 模式前再扫描一次（更强约束）
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// 编译完成回调：扫描项目 C# 源码，输出违规错误信息。
        /// </summary>
        /// <param name="obj">回调参数（未使用）。</param>
        private static void OnCompilationFinished(object obj)
        {
            ScanAndReport(blockPlayMode: false);
        }

        /// <summary>
        /// PlayMode 状态变更回调：在即将进入 Play 时扫描并可阻止进入。
        /// </summary>
        /// <param name="state">PlayMode 状态。</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            // if (!BlockEnterPlayMode)
            //     return;

            bool hasViolation = ScanAndReport(blockPlayMode: true);
            if (hasViolation)
            {
                // 阻止进入 Play
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>
        /// 扫描所有 C# 文件并输出违规位置。可选择是否阻止进入 Play。
        /// </summary>
        /// <param name="blockPlayMode">是否作为阻止进入 Play 的扫描。</param>
        /// <returns>是否发现违规。</returns>
        private static bool ScanAndReport(bool blockPlayMode)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
                string assetsPath = Application.dataPath.Replace("\\", "/");

                // 扫描 Assets 下的 .cs
                var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
                bool hasViolation = false;

                foreach (var filePath in csFiles)
                {
                    string normalized = filePath.Replace("\\", "/");

                    if (IsExcluded(normalized))
                        continue;

                    string text = File.ReadAllText(filePath);
                    string scanText = StripCommentsPreserveLines(text, stripStrings: true);

                    // 快速跳过：如果连 "Random" 都不包含，没必要跑正则
                    if (!scanText.Contains("Random", StringComparison.Ordinal))
                        continue;

                    foreach (var pattern in ForbiddenPatterns)
                    {
                        var matches = pattern.Matches(scanText);
                        if (matches.Count <= 0) continue;

                        // 输出每个匹配的行号，方便定位
                        foreach (Match m in matches)
                        {
                            int line = GetLineNumber(scanText, m.Index);
                            string rel = ToRelativePath(projectRoot, filePath);

                            Debug.LogError(
                                $"[SystemRandomBlocker] 检测到对 System.Random 的引用：{rel}:{line}\n{ReplacementHint}"
                            );

                            hasViolation = true;
                        }
                    }
                }

                if (hasViolation && blockPlayMode)
                {
                    Debug.LogError("[SystemRandomBlocker] 已阻止进入 Play 模式：请先清理所有 System.Random 引用。");
                }

                return hasViolation;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SystemRandomBlocker] 扫描失败：{e}");
                return false;
            }
        }

        /// <summary>
        /// 判断路径是否属于排除目录（第三方/缓存等）。
        /// </summary>
        /// <param name="normalizedPath">规范化后的路径（使用 /）。</param>
        /// <returns>是否排除。</returns>
        private static bool IsExcluded(string normalizedPath)
        {
            foreach (var s in ExcludedPathContains)
            {
                if (normalizedPath.Contains(s, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 将绝对路径转换为相对项目根目录的路径。
        /// </summary>
        /// <param name="projectRoot">项目根目录。</param>
        /// <param name="filePath">绝对文件路径。</param>
        /// <returns>相对路径。</returns>
        private static string ToRelativePath(string projectRoot, string filePath)
        {
            projectRoot = projectRoot.Replace("\\", "/").TrimEnd('/') + "/";
            string fp = filePath.Replace("\\", "/");
            return fp.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? fp.Substring(projectRoot.Length)
                : fp;
        }

        /// <summary>
        /// 通过字符索引估算行号（1-based）。
        /// </summary>
        /// <param name="text">全文内容。</param>
        /// <param name="index">匹配起始索引。</param>
        /// <returns>行号（从 1 开始）。</returns>
        private static int GetLineNumber(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }


        /// <summary>
        /// 移除 C# 源码中的注释（// 与 /* */），并可选移除字符串/字符字面量内容。
        /// 会保留换行符，以保证后续行号计算仍然准确。
        /// </summary>
        /// <param name="code">原始源码文本。</param>
        /// <param name="stripStrings">是否同时移除字符串与字符字面量内容以避免误报。</param>
        /// <returns>处理后的文本（注释/字面量内容被空格替换，换行保留）。</returns>
        private static string StripCommentsPreserveLines(string code, bool stripStrings = true)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            var chars = code.ToCharArray();

            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;          // "..."
            bool inVerbatimString = false;  // @"..."
            bool inChar = false;            // '...'
            bool escape = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = (i + 1 < chars.Length) ? chars[i + 1] : '\0';

                // 行注释：直到换行结束
                if (inLineComment)
                {
                    if (c != '\n' && c != '\r')
                        chars[i] = ' ';
                    else
                        inLineComment = false;
                    continue;
                }

                // 块注释：直到 */ 结束（保留换行）
                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++; // 跳过 '/'
                        inBlockComment = false;
                    }
                    else if (c != '\n' && c != '\r')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                // 字符字面量 'x'（可选移除，保留换行）
                if (inChar)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '\'')
                        inChar = false;

                    continue;
                }

                // 普通字符串 "..."（可选移除，保留换行）
                if (inString)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                // 逐字字符串 @"..."（"" 表示转义引号）
                if (inVerbatimString)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (c == '"' && next == '"')
                    {
                        // "" 视为一个引号字符，继续保持在逐字字符串里
                        if (stripStrings)
                            chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (c == '"')
                        inVerbatimString = false;

                    continue;
                }

                // 进入注释？
                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                // 进入字符串/字符？
                if (c == '@' && next == '"')
                {
                    // @"..."
                    if (stripStrings)
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                    }
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    if (stripStrings) chars[i] = ' ';
                    inString = true;
                    escape = false;
                    continue;
                }

                if (c == '\'')
                {
                    if (stripStrings) chars[i] = ' ';
                    inChar = true;
                    escape = false;
                    continue;
                }
            }

            return new string(chars);
        }

    }
}
#endif
