﻿using System.Text;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using Serilog;
using Serilog.Sinks.File;

namespace ME3TweaksModManager.modmanager.diagnostics
{
    /// <summary>
    /// Hook used to capture what log is currently being used
    /// </summary>
    public class CaptureFilePathHook : FileLifecycleHooks
    {
        public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
        {
            M3Log.CurrentLogFilePath = path;
            return base.OnFileOpened(path, underlyingStream, encoding);
        }
    }

    /// <summary>
    /// Interposer used to prefix M3Log messages with their source component. Call only from M3 code
    /// </summary>
    public static class M3Log
    {
        private const string Prefix = @"M3";

        /// <summary>
        /// The path of the current log file
        /// </summary>
        public static string CurrentLogFilePath { get; internal set; }
        /// <summary>
        /// If Debug level logs should be enabled
        /// </summary>
        public static bool DebugLogging { get; set; }

        public static void Exception(Exception exception, string preMessage, bool fatal = false, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Error($@"{prefix}{preMessage}");

                // Log exception
                while (exception != null)
                {
                    var line1 = exception.GetType().Name + @": " + exception.Message;
                    foreach (var line in line1.Split("\n")) // do not localize
                    {
                        if (fatal)
                            Log.Fatal(prefix + line);
                        else
                            Log.Error(prefix + line);

                    }

                    if (exception.StackTrace != null)
                    {
                        foreach (var line in exception.StackTrace.Split("\n")) // do not localize
                        {
                            if (fatal)
                                Log.Fatal(prefix + line);
                            else
                                Log.Error(prefix + line);
                        }
                    }

                    exception = exception.InnerException;
                }
            }
        }

        public static void Information(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Information($@"{prefix}{message}");
            }
        }

        public static void Warning(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Warning($@"{prefix}{message}");
            }
        }

        public static void Error(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Error($@"{prefix}{message}");
            }
        }

        public static void Fatal(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Fatal($@"{prefix}{message}");
            }
        }

        public static void Debug(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Debug($@"{prefix}{message}");
            }
        }

        /// <summary>
        /// Creates an ILogger for ME3Tweaks Mod Manager. This does NOT assign it to the Log.Logger instance.
        /// </summary>
        /// <returns></returns>
        public static ILogger CreateLogger()
        {
            var loggerConfig = new LoggerConfiguration().WriteTo
                .File(Path.Combine(MCoreFilesystem.GetLogDir(), @"modmanagerlog-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileTimeLimit: TimeSpan.FromDays(7), // Retain only one week of logs.
                    fileSizeLimitBytes: FileSize.MebiByte * 10, // 10 MB
                                                                // shared: true, // Allow us to read log without closing it // doesn't work in shared mode
                    hooks: new CaptureFilePathHook()); // Allow us to capture current log path 
#if DEBUG
            loggerConfig = loggerConfig.WriteTo.Debug();
#endif
            // Enable Debug Logging if app booted with flag
            if (DebugLogging)
            {
                loggerConfig = loggerConfig.MinimumLevel.Debug();
            }

            return loggerConfig.CreateLogger();
        }

        /// <summary>
        /// Returns the current logger.
        /// </summary>
        /// <returns></returns>
        public static ILogger GetLogger()
        {
            return Log.Logger;
        }
    }
}
