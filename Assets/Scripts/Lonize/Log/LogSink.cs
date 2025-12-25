// File: YourStudio.Verse/Core/LogSinks.cs
#nullable enable
using System;
using System.IO;
using System.Text;
using Lonize;
using UnityEngine;
namespace Lonize.Logging
{
    public sealed class ConsoleSink : ILogSink
    {
        public void Emit(in LogEvent e)
        {
            var sb = FormatCommon(e);
            if (e.Exception != null) sb.AppendLine().Append(e.Exception);
            Console.WriteLine(sb.ToString());
        }

        private static StringBuilder FormatCommon(in LogEvent e)
        {
            var t = e.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff");
            var sb = new StringBuilder(256);
            sb.Append('[').Append(t).Append("] [").Append(e.Level).Append(']');
            if (!string.IsNullOrEmpty(e.Category)) sb.Append(" [").Append(e.Category).Append(']');
            if (e.Scope is { Count: >0 }) {
                sb.Append(" {");
                bool first = true;
                foreach (var kv in e.Scope) {
                    if (!first) sb.Append(", ");
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                    first = false;
                }
                sb.Append('}');
            }
            sb.Append(' ').Append(e.Message)
              .Append("  <").Append(Path.GetFileName(e.File)).Append(':').Append(e.Line)
              .Append(" @").Append(e.Member).Append(" T").Append(e.ThreadId).Append('>');
            return sb;
        }
    }

    // 仅在 Unity 下可用（防止 Editor 外部编译器报错）
#if UNITY_EDITOR
    
    public sealed class UnitySink : ILogSink
    {
        public void Emit(in LogEvent e)
        {
            string msg = $"[{e.Level}] {e.Message}";
            if (e.Scope is { Count: >0 }) msg += " " + string.Join(", ", e.Scope);
            if (e.Exception != null) msg += "\n" + e.Exception;
            switch (e.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info: Debug.Log(msg); break;
                case LogLevel.Warn: Debug.LogWarning(msg); break;
                default: Debug.LogError(msg); break;
            }
        }
    }
#endif

    public sealed class FileSink : ILogSink, IDisposable
    {
        private readonly object _gate = new();
        private readonly string _path;
        private readonly long _rollSizeBytes;
        private StreamWriter _writer;

        public FileSink(string path, long rollSizeBytes = 8 * 1024 * 1024)
        {
            _path = path; _rollSizeBytes = rollSizeBytes;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            { AutoFlush = true, NewLine = "\n" };
        }

        public void Emit(in LogEvent e)
        {
            var t = e.UtcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            var cat = string.IsNullOrEmpty(e.Category) ? "" : $" [{e.Category}]";
            var scope = (e.Scope is { Count: >0 }) ? " {" + string.Join(", ", e.Scope) + "}" : "";
            string line = $"{t} [{e.Level}]{cat}{scope} {e.Message} <{Path.GetFileName(e.File)}:{e.Line}@{e.Member}>\n";
            if (e.Exception != null) line += e.Exception + "\n";
            lock (_gate)
            {
                _writer.Write(line);
                TryRoll();
            }
        }

        public void Flush() { lock (_gate) _writer.Flush(); }

        private void TryRoll()
        {
            if (_writer.BaseStream.Length < _rollSizeBytes) return;
            _writer.Dispose();
            string rolled = _path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (File.Exists(rolled))
                File.Delete(rolled);
            File.Move(_path, rolled);
            _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            { AutoFlush = true, NewLine = "\n" };
        }

        public void Dispose() { lock (_gate) _writer.Dispose(); }
    }
}
