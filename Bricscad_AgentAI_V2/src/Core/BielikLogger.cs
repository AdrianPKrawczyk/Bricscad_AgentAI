using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Klasa statyczna BielikLogger odpowiada za zapisywanie lekkich, crash-safe
    /// logów diagnostycznych z informacją o wątkach i obsłudze wyjątków.
    /// </summary>
    public static class BielikLogger
    {
        private static readonly object _lock = new object();
        private static int _mainThreadId = -1;

        /// <summary>
        /// Globalny przełącznik włączania logowania.
        /// </summary>
        public static bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Rejestruje ID głównego wątku CAD w celach diagnostycznych.
        /// </summary>
        public static void RegisterMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Zwraca bezwzględną ścieżkę do pliku logu.
        /// </summary>
        public static string GetLogPath()
        {
            try
            {
                return Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "bielik_debug.log"
                );
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bielik_debug.log");
            }
        }

        public static void LogInfo(string message) => Write("INFO", message);
        public static void LogWarn(string message) => Write("WARN", message);
        public static void LogError(string message, Exception ex = null) => Write("ERROR", message, ex);
        public static void LogCritical(string message, Exception ex = null) => Write("CRITICAL", message, ex);

        private static string GetThreadInfo()
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            string tname = Thread.CurrentThread.Name;
            if (string.IsNullOrEmpty(tname))
            {
                tname = Thread.CurrentThread.IsThreadPoolThread ? "Pool" : "Task";
            }
            bool isUi = (tid == _mainThreadId);
            return $"[Thread: {tid} ({tname}){(isUi ? " [UI]" : " [Worker]")}]";
        }

        private static void RotateLogIfNeeded(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length > 1024 * 1024) // Limit 1 MB
                    {
                        string backupPath = path + ".bak";
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }
                        File.Move(path, backupPath);
                    }
                }
            }
            catch { }
        }

        private static void Write(string level, string message, Exception ex = null)
        {
            if (!IsEnabled) return;

            lock (_lock)
            {
                try
                {
                    string path = GetLogPath();
                    RotateLogIfNeeded(path);

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string threadInfo = GetThreadInfo();
                    string logEntry = $"[{timestamp}] {threadInfo} [{level}] {message}";

                    if (ex != null)
                    {
                        logEntry += $"\n[EXCEPTION] {ex.GetType().FullName}: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
                        if (ex.InnerException != null)
                        {
                            logEntry += $"\n[INNER EXCEPTION] {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\nStack Trace:\n{ex.InnerException.StackTrace}";
                        }
                    }

                    File.AppendAllText(path, logEntry + Environment.NewLine);
                }
                catch { }
            }
        }

        /// <summary>
        /// Odczytuje ostatnie N linii pliku logu.
        /// </summary>
        public static string ReadLastLines(int count)
        {
            lock (_lock)
            {
                try
                {
                    string path = GetLogPath();
                    if (!File.Exists(path)) return "Brak pliku logu. Diagnostyka nie została jeszcze uruchomiona.";

                    var lines = File.ReadAllLines(path);
                    if (lines.Length <= count)
                    {
                        return string.Join(Environment.NewLine, lines);
                    }
                    else
                    {
                        var lastLines = new string[count];
                        Array.Copy(lines, lines.Length - count, lastLines, 0, count);
                        return string.Join(Environment.NewLine, lastLines);
                    }
                }
                catch (Exception ex)
                {
                    return $"Błąd odczytu logu: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// Czyści zawartość pliku logu.
        /// </summary>
        public static void ClearLog()
        {
            lock (_lock)
            {
                try
                {
                    string path = GetLogPath();
                    File.WriteAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] Plik logu został wyczyszczony." + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
