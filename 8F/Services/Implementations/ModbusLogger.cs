using System;
using System.Diagnostics;
using System.IO;

namespace _8F.Services.Implementations
{
    public static class ModbusLogger
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string _logFilePath = Path.Combine(_logDirectory, "modbus.log");
        private const long MaxLogFileSizeBytes = 5 * 1024 * 1024; // 5 MB roll-over limit

        static ModbusLogger()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch { }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            string formatted = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
            Write("ERROR", formatted);
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            Debug.WriteLine(line);
            Trace.WriteLine(line);

            lock (_lock)
            {
                try
                {
                    FileInfo fi = new FileInfo(_logFilePath);
                    if (fi.Exists && fi.Length > MaxLogFileSizeBytes)
                    {
                        string oldLogPath = Path.Combine(_logDirectory, "modbus.log.old");
                        if (File.Exists(oldLogPath))
                        {
                            File.Delete(oldLogPath);
                        }
                        File.Move(_logFilePath, oldLogPath);
                    }

                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
