using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using NLog;

// largely based on https://github.com/ferventcoder/this.Log/blob/master/LoggingExtensions.NLog/NLogLog.cs

namespace CitizenMP.Server.Logging
{
    public class BaseLog
    {
        private static Logger ms_logger;

        public BaseLog(string typeName, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            if (ms_logger == null)
            {
                ms_logger = LogManager.GetLogger("CitizenMP.Server");
            }

            MappedDiagnosticsContext.Set("typeName", typeName.Split('.').Last());
            MappedDiagnosticsContext.Set("memberName", memberName);
            MappedDiagnosticsContext.Set("sourceFile", sourceFilePath.Replace(ms_basePath, ""));
            MappedDiagnosticsContext.Set("sourceLine", sourceLineNumber.ToString());
        }

        static string ms_basePath = null;

        internal static void SetStripSourceFilePath([CallerFilePath] string sourcePath = "")
        {
            ms_basePath = sourcePath.Replace("Program.cs", "");
        }

        public void Debug(string message, params object[] formatting)
        {
            if (ms_logger.IsDebugEnabled) ms_logger.Debug(message, formatting);
        }

        public void Debug(Func<string> message)
        {
            if (ms_logger.IsDebugEnabled) ms_logger.Debug(message());
        }

        public void Info(string message, params object[] formatting)
        {
            if (ms_logger.IsInfoEnabled) ms_logger.Info(message, formatting);
        }

        public void Info(Func<string> message)
        {
            if (ms_logger.IsInfoEnabled) ms_logger.Info(message());
        }

        public void Warn(string message, params object[] formatting)
        {
            if (ms_logger.IsWarnEnabled) ms_logger.Warn(message, formatting);
        }

        public void Warn(Func<string> message)
        {
            if (ms_logger.IsWarnEnabled) ms_logger.Warn(message());
        }

        public void Error(string message, params object[] formatting)
        {
            // don't check for enabled at this level
            ms_logger.Error(message, formatting);
        }

        public void Error(Func<string> message)
        {
            // don't check for enabled at this level
            ms_logger.Error(message());
        }

        public void Error(Func<string> message, Exception exception)
        {
            ms_logger.Error(message(), exception);
        }

        public void Fatal(string message, params object[] formatting)
        {
            // don't check for enabled at this level
            ms_logger.Fatal(message, formatting);

            WindowedLogger.Fatal(string.Format(message, formatting));
        }

        public void Fatal(Func<string> message)
        {
            // don't check for enabled at this level
            ms_logger.Fatal(message());

            WindowedLogger.Fatal(message());
        }

        public void Fatal(Func<string> message, Exception exception)
        {
            ms_logger.Fatal(message(), exception);

            WindowedLogger.Fatal(message());
        }

    }
}
