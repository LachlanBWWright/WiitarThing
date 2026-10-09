using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WiinUSoft
{
    class WiitarDebug
    {
        private static readonly object Sync = new();
        public enum LogLevel
        {
            TooMuch = 0,
            Warning = 1,
            Error = 2,
        }

        public static void Log(string message, LogLevel level = LogLevel.TooMuch)
        {
#if !DEBUG
            if (level < LogLevel.Error)
                return;
#endif
            var utcDate = DateTime.Now.ToUniversalTime();
            string date = $"{utcDate.Hour:D2}:{utcDate.Minute:D2}:{utcDate.Second:D2}.{utcDate.Millisecond:D3}";
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiitarThing");
            string logFilePath = Path.Combine(logDirectory, "WiitarLog.log");

            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(logDirectory);
                    using var logFile = File.Open(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var logFileWriter = new StreamWriter(logFile);
                    logFileWriter.WriteLine($"[{date}] {message}");
                    if (new FileInfo(logFilePath).Length > 2 * 1024 * 1024)
                    {
                        File.Copy(logFilePath, logFilePath + ".1", true);
                        File.WriteAllText(logFilePath, string.Empty);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }

        }

        public static string Frankenpath(params string[] pathParts)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < pathParts.Length; i++)
            {
                sb.Append(pathParts[i].Trim('\\'));
                if (i < pathParts.Length - 1)
                    sb.Append('\\');
            }

            return sb.ToString();
        }

    }
}
