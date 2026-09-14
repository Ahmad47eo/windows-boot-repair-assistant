using System.IO;
using WindowsBootRepair.Logging;

namespace WindowsBootRepair.Logging
{
    public static class BootRepairLogger
    {
        private static readonly string LogFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BootRepair.log");

        private static readonly object LockObject = new();

        public static void Initialize()
        {
            try
            {
                lock (LockObject)
                {
                    if (!File.Exists(LogFile))
                    {
                        File.Create(LogFile).Dispose();
                    }

                    Log("=== LOG INITIALIZED ===");
                    Log($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                lock (LockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var logLine = $"[{timestamp}] {message}";

                    File.AppendAllText(LogFile, logLine + Environment.NewLine);
                }
            }
            catch { }
        }

        public static string GetLogContent()
        {
            try
            {
                lock (LockObject)
                {
                    if (!File.Exists(LogFile))
                        return "Log file not found.";

                    return File.ReadAllText(LogFile);
                }
            }
            catch
            {
                return "Error reading log file.";
            }
        }

        public static void ClearLog()
        {
            try
            {
                lock (LockObject)
                {
                    File.WriteAllText(LogFile, "");
                    Log("=== LOG CLEARED ===");
                }
            }
            catch { }
        }
    }
}
