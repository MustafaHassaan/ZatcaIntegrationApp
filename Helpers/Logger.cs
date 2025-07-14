using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZatcaIntegrationApp.Helpers
{
    public static class Logger
    {
        private const string LogPath = "Output/log.txt";
        public static void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCCESS] " + message);
            Console.ResetColor();
        }
        public static void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[INFO] " + message);
            Console.ResetColor();
            AppendToLog("[INFO] " + message);
        }

        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] " + message);
            Console.ResetColor();
            AppendToLog("[ERROR] " + message);
        }

        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARN] " + message);
            Console.ResetColor();
            AppendToLog("[WARN] " + message);
        }

        private static void AppendToLog(string message)
        {
            if (!Directory.Exists("Output"))
                Directory.CreateDirectory("Output");

            File.AppendAllText(LogPath, $"[{DateTime.Now}] {message}{Environment.NewLine}");
        }
    }
}
