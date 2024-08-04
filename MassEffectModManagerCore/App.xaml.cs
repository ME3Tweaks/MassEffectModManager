﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuthenticodeExaminer;
using CommandLine;
using Dark.Net;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.tutorial;
using ME3TweaksModManager.modmanager.windows;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using SevenZip;
using SingleInstanceCore;

namespace ME3TweaksModManager
{
    [Localizable(false)]
    public partial class App : Application, ISingleInstance
    {
        /// <summary>
        /// If the appdata folder existed at boot time. If it didn't, this is a very fresh install
        /// </summary>
        public static bool AppDataExistedAtBoot = Directory.Exists(M3Filesystem.GetAppDataFolder(false)); //alphabetically this must come first in App!

        /// <summary>
        /// ME3Tweaks Shared Registry Key
        /// </summary>
        internal const string REGISTRY_KEY_ME3TWEAKS = @"HKEY_CURRENT_USER\Software\ME3Tweaks";

        /// <summary>
        /// If we have begun loading the interface
        /// </summary>
        private static bool POST_STARTUP = false;

        /// <summary>
        /// The link to the Discord server
        /// </summary>
        public const string DISCORD_INVITE_LINK = "https://discord.gg/s8HA6dc";

        public static Visibility DebugOnlyVisibility
        {
#if DEBUG
            get { return Visibility.Visible; }
#else
            get { return Visibility.Collapsed; }
#endif
        }

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif

        /// <summary>
        /// If the application is exiting due to SingleInstance - don't do cleanup
        /// </summary>
        public static bool SingleInstanceExit = false;

        /// <summary>
        /// The highest version of ModDesc that this version of Mod Manager can support. The maximum precision allowed is tenths, the rest will be truncated.
        /// </summary>
#if DEBUG
        public const double HighestSupportedModDesc = 9.1;
#else
        public const double HighestSupportedModDesc = 9.0;
#endif

        public static readonly Version MIN_SUPPORTED_OS = new Version(@"10.0.19045"); // Windows 10 22H2

        internal static readonly string[] SupportedOperatingSystemVersions =
        {
            @"Windows 10 (not EOL versions)",
            @"Windows 11 (not EOL versions)"
        };

        /// <summary>
        /// If telemetry has been flushed after checking if it is enabled.
        /// </summary>
        public static bool FlushedTelemetry;

        public void OnInstanceInvoked(string[] args)
        {
            // Another exe was launched
            Debug.WriteLine($"Instance args: {string.Join(" ", args)}");
            Dispatcher?.Invoke(() =>
            {
                if (Current.MainWindow is MainWindow mw)
                {
                    if (mw.HandleInstanceArguments(args))
                    {
                        // Bring to front
                        mw.Activate();
                    }
                }
            });

        }

        public App() : base()
        {
            ExecutableLocation = Process.GetCurrentProcess().MainModule.FileName;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            ObservableCollectionExtendedThreading.EnableCrossThreadUpdatesDelegate = (collection, syncLock) =>
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    BindingOperations.EnableCollectionSynchronization(collection, syncLock);
                });

            // We use our own implementation of this as we store data in ProgramData.
            MCoreFilesystem.GetAppDataFolder = M3Filesystem.GetAppDataFolder; // Do not change

            var settingsExist = File.Exists(Settings.SettingsPath); //for init language
            try
            {
                string exeFolder = Directory.GetParent(ExecutableLocation).FullName;
                try
                {
                    Log.Logger = M3Log.CreateLogger();
                }
                catch (Exception)
                {
                    //Unable to create logger...!
                }

                string[] args = Environment.GetCommandLineArgs();

                #region Command line

                if (args.Length > 1)
                {
                    var result = Parser.Default.ParseArguments<CLIOptions>(args);
                    if (result is Parsed<CLIOptions> parsedCommandLineArgs)
                    {
                        //Parsing completed
                        if (parsedCommandLineArgs.Value.DebugLogging)
                        {
                            M3Log.DebugLogging = true;
                        }
                        if (parsedCommandLineArgs.Value.UpdateBoot)
                        {
                            //Update unpacked and process was run.
                            //Extract ME3TweaksUpdater.exe to ensure we have newest update executable in case we need to do update hotfixes

                            // The swapper executable is a directory above as M3 is packaged in, as updates are shipped in a subfolder named ME3TweaksModManager
                            var updaterExe = Path.Combine(Directory.GetParent(exeFolder).FullName, @"ME3TweaksUpdater.exe");

                            //write updated updater executable
                            M3Utilities.ExtractInternalFile(@"ME3TweaksModManager.updater.ME3TweaksUpdater.exe", updaterExe, true);

                            if (!File.Exists(updaterExe))
                            {
                                // Error like this has no need being localized
                                Xceed.Wpf.Toolkit.MessageBox.Show(null, $"Updater shim missing!\nThe swapper executable should have been located at:\n{updaterExe}\n\nPlease report this to ME3Tweaks.", @"Error updating", MessageBoxButton.OK, MessageBoxImage.Error); //do not localize
                            }

                            SingleInstanceExit = true;
                            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                            return;
                        }

                        if (parsedCommandLineArgs.Value.UpdateFromBuild != 0)
                        {
                            App.UpdatedFrom = parsedCommandLineArgs.Value.UpdateFromBuild;
                        }

                        if (parsedCommandLineArgs.Value.BootingNewUpdate)
                        {
                            App.BootingUpdate = true;
                        }

                        CommandLinePending.UpgradingFromME3CMM = parsedCommandLineArgs.Value.UpgradingFromME3CMM;

                        if (parsedCommandLineArgs.Value.NXMLink != null)
                        {
                            CommandLinePending.PendingNXMLink = parsedCommandLineArgs.Value.NXMLink;
                        }

                        if (parsedCommandLineArgs.Value.M3Link != null)
                        {
                            CommandLinePending.PendingM3Link = parsedCommandLineArgs.Value.M3Link;
                        }

                        if (parsedCommandLineArgs.Value.AutoInstallModdescPath != null)
                        {
                            CommandLinePending.PendingAutoModInstallPath = parsedCommandLineArgs.Value.AutoInstallModdescPath;
                        }

                        if (parsedCommandLineArgs.Value.GameBoot)
                        {
                            CommandLinePending.PendingGameBoot = parsedCommandLineArgs.Value.GameBoot;
                        }

                        if (parsedCommandLineArgs.Value.CreateMergeDLC)
                        {
                            CommandLinePending.PendingMergeDLCCreation = parsedCommandLineArgs.Value.CreateMergeDLC;
                        }

                        if (parsedCommandLineArgs.Value.MergeModManifestToCompile != null)
                        {
                            CommandLinePending.PendingMergeModCompileManifest = parsedCommandLineArgs.Value.MergeModManifestToCompile;
                        }

                        if (parsedCommandLineArgs.Value.FeatureLevel > 0)
                        {
                            CommandLinePending.PendingFeatureLevel = parsedCommandLineArgs.Value.FeatureLevel;
                        }
                    }
                    else
                    {
                        M3Log.Error(@"Could not parse command line arguments! Args: " + string.Join(' ', args));
                    }
                }

                #endregion

                // Single instance occurs AFTER command line params as to not break the updater which requires simultaneous boot
                bool isFirstInstance = this.InitializeAsFirstInstance(@"ME3TweaksModManager6"); // do not change this string
                if (!isFirstInstance)
                {
                    //If it's not the first instance, arguments are automatically passed to the first instance
                    //OnInstanceInvoked will be raised on the first instance

                    // Kill this new loading instance.
                    SingleInstanceExit = true;
                    Current.Shutdown();
                    return;
                }


                this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
                ToolTipService.ShowDurationProperty.OverrideMetadata(
                    typeof(DependencyObject), new FrameworkPropertyMetadata(20000));

                M3Log.Information(@"===========================================================================");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutableLocation);
                string version = fvi.FileVersion;
                M3Log.Information(@"ME3Tweaks Mod Manager " + version);
                M3Log.Information(@"Application boot: " + DateTime.UtcNow);
                M3Log.Information(@"Running as " + Environment.UserName);
                M3Log.Information(@"Executable location: " + ExecutableLocation);
                M3Log.Information(@"Operating system: " + RuntimeInformation.OSDescription);

                //Get build date
                BuildHelper.ReadRuildInfo(new BuildHelper.BuildSigner[] { new BuildHelper.BuildSigner() { SigningName = @"Michael Perez", DisplayName = @"ME3Tweaks" } });

                if (args.Length > 0)
                {
                    M3Log.Information($@"Application arguments: {string.Join(" ", args)}");
                }

                // Load NXM handlers
                try
                {
                    NexusDomainHandler.LoadExternalHandlers();
                    if (CommandLinePending.PendingNXMLink != null && NexusDomainHandler.HandleExternalLink(CommandLinePending.PendingNXMLink))
                    {
                        // Externally handled
                        M3Log.Information(@"Exiting application");
                        Environment.Exit(0);
                        return; // Nothing else to do
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Error loading external nxm handlers: {e.Message}");
                }

                System.Windows.Controls.ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(true));

                try
                {
                    var avs = M3Utilities.GetListOfInstalledAV();
                    M3Log.Information(@"Detected the following antivirus products:");
                    foreach (var av in avs)
                    {
                        M3Log.Information(" - " + av);
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Unable to get the list of installed antivirus products: " + e.Message);
                }

                //Build 104 changed location of settings from AppData to ProgramData.
                if (!AppDataExistedAtBoot)
                {
                    //First time booting something that uses ProgramData
                    //see if data exists in AppData
                    var oldDir = M3Filesystem.GetPre104DataFolder();
                    if (oldDir != null)
                    {
                        //Exists. We should migrate it
                        try
                        {
                            CopyDir.CopyAll_ProgressBar(new DirectoryInfo(oldDir), new DirectoryInfo(M3Filesystem.GetAppDataFolder()), aboutToCopyCallback: (a) =>
                            {
                                M3Log.Information(@"Migrating file from AppData to ProgramData: " + a);
                                return true;
                            });

                            M3Log.Information(@"Deleting old data directory: " + oldDir);
                            MUtilities.DeleteFilesAndFoldersRecursively(oldDir);
                            M3Log.Information(@"Migration from pre 104 settings to 104+ settings completed");
                        }
                        catch (Exception e)
                        {
                            M3Log.Error(@"Unable to migrate old settings: " + e.Message);
                        }
                    }
                }


                M3Log.Information("Loading settings");
                Settings.Load();

                // Set title bar color
                DarkNet.Instance.SetCurrentProcessTheme(Settings.DarkTheme ? Theme.Dark : Theme.Light);

                if (Settings.ShowedPreviewPanel && !Settings.EnableTelemetry)
                {
                    M3Log.Warning("Telemetry is disabled :(");
                }
                else if (Settings.ShowedPreviewPanel)
                {
                    // Telemetry is on and we've shown the preview panel. Start appcenter
                    InitAppCenter();
                }
                else
                {
                    // We haven't shown the preview panel. Telemetry setting is 'on' but until
                    // the user has configured their options nothing will be sent.
                    // If option is not selected the items will be discarded
                }

                if (Settings.Language != @"int" && M3Localization.SupportedLanguages.Contains(Settings.Language))
                {
                    InitialLanguage = Settings.Language;
                }
                if (!settingsExist)
                {
                    //first boot?
                    var currentCultureLang = CultureInfo.InstalledUICulture.Name;
                    if (IsLanguageSupported(@"deu") && currentCultureLang.StartsWith(@"de")) InitialLanguage = Settings.Language = @"deu";
                    if (IsLanguageSupported(@"rus") && currentCultureLang.StartsWith(@"ru")) InitialLanguage = Settings.Language = @"rus";
                    if (IsLanguageSupported(@"pol") && currentCultureLang.StartsWith(@"pl")) InitialLanguage = Settings.Language = @"pol";
                    if (IsLanguageSupported(@"bra") && currentCultureLang.StartsWith(@"pt")) InitialLanguage = Settings.Language = @"bra";
                    if (IsLanguageSupported(@"ita") && currentCultureLang.StartsWith(@"it")) InitialLanguage = Settings.Language = @"ita";
                    SubmitAnalyticTelemetryEvent(@"Auto set startup language", new Dictionary<string, string>() { { @"Language", InitialLanguage } });
                    M3Log.Information(@"This is a first boot. The system language code is " + currentCultureLang);
                }

                M3Log.Information(@"Deleting temp files (if any)");
                try
                {
                    MUtilities.DeleteFilesAndFoldersRecursively(MCoreFilesystem.GetTempDirectory());
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, $@"Unable to delete temporary files directory {MCoreFilesystem.GetTempDirectory()}:");
                }


                // 02/06/2023
                // Trying to solve first boot 7z issues
                try
                {
                    var sevenZpath = SevenZipLibraryManagerExt.DetermineLibraryFilePath();
                    if (sevenZpath != null)
                    {
                        if (!File.Exists(sevenZpath))
                        {
                            SubmitAnalyticTelemetryEvent("7z dll not found", new Dictionary<string, string>()
                            {
                                {@"Library path", sevenZpath}
                            });
                            M3Log.Error($@"The 7z library path {sevenZpath} was not found. 7z probably won't work - maybe requires an app reboot");
                        }
                        else
                        {
                            M3Log.Information(@"7z library was located on disk");
                        }
                    }
                    else
                    {
                        M3Log.Error(@"Unable to determine 7z library path using library code!");
                    }
                }
                catch (Exception ex)
                {
                    M3Log.Exception(ex, @"Unable to determine 7z library path:");
                }


                M3Log.Information(@"Mod Manager pre-UI startup has completed. The UI will now load.");
                M3Log.Information(@"If the UI fails to start, it may be that a third party tool is injecting itself into Mod Manager, such as RivaTuner or Afterburner, and is corrupting the process.");
                POST_STARTUP = true; //this could be earlier but i'm not sure when crash handler actually is used, doesn't seem to be after setting it...
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw;
            }
        }

        /// <summary>
        /// A datetime object representing the signing date of this executable
        /// </summary>
        public static DateTime? BuildDateTime { get; set; }

        /// <summary>
        /// If a language localization is supported in M3 for use
        /// </summary>
        /// <param name="lang">The lang code</param>
        /// <returns></returns>
        public static bool IsLanguageSupported(string lang)
        {
            lang = lang.ToLower();

            if (lang == @"deu") // deu is up to date
            {
                ServerManifest.TryGetBool(ServerManifest.LOCALIZATION_ENABLED_DEU, out var enabled, true);
                return enabled;
            }

            if (lang == @"rus") // Localization is up to date
            {
                ServerManifest.TryGetBool(ServerManifest.LOCALIZATION_ENABLED_RUS, out var enabled, true);
                return enabled;
            }

            if (lang == @"ita") // Localization is up to date
            {
                ServerManifest.TryGetBool(ServerManifest.LOCALIZATION_ENABLED_ITA, out var enabled, true);
                return enabled;
            }

            // These localizations have been abandoned; if they are updated serverside, they can be dynamically re-enabled, for the most part
            //if (lang == @"pol") // Localization was abandoned
            //{
            //    // This may not be available on first load
            //    ServerManifest.TryGetBool(ServerManifest.LOCALIZATION_ENABLED_POL, out var enabled, false);
            //    return enabled;
            //}


            //if (lang == @"bra") // Localization was abandoned
            //{
            //    // This may not be available on first load
            //    ServerManifest.TryGetBool(ServerManifest.LOCALIZATION_ENABLED_BRA, out var enabled, false);
            //    return enabled;
            //}

            if (lang == @"int") return true; // Just in case
            return false;
        }

        private static List<(string, Dictionary<string, string>)> QueuedTelemetryItems = new List<(string, Dictionary<string, string>)>();

        /// <summary>
        /// Submits a telemetry event. Queues them if the first run panel has not shown yet. All calls to TrackEvent should route through here to respect user settings.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void SubmitAnalyticTelemetryEvent(string name, Dictionary<string, string> data = null)
        {
            if (!Settings.ShowedPreviewPanel && !FlushedTelemetry && QueuedTelemetryItems != null)
            {
                // Queue a telemetry item until the panel has closed
                QueuedTelemetryItems.Add((name, data));
            }
            else
            {
                // if telemetry is not enabled this will not do anything.
                Analytics.TrackEvent(name, data);
            }
        }

        /// <summary>
        /// Flushes the startup telemetry events and disables the queue.
        /// </summary>
        public static void FlushTelemetryItems()
        {
            FlushedTelemetry = true;
            if (Settings.EnableTelemetry && QueuedTelemetryItems != null)
            {
                foreach (var v in QueuedTelemetryItems)
                {
                    TelemetryInterposer.TrackEvent(v.Item1, v.Item2);
                }
            }

            QueuedTelemetryItems = null; // Just release the memory. This variable is never used again
        }

        internal static void InitAppCenter()
        {
            if (!new NickStrupat.ComputerInfo().ActuallyPlatform)
            {
                M3Log.Warning(@"This does not appear to be an actually supported platform, disabling telemetry");
                return;
            }
#if !DEBUG
            if (APIKeys.HasAppCenterKey)
            {
                Crashes.GetErrorAttachments = (ErrorReport report) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    // Attach some text.
                    string errorMessage = "ME3Tweaks Mod Manager has crashed! This is the exception that caused the crash:";
                    M3Log.Fatal(report.StackTrace);
                    M3Log.Fatal(errorMessage);
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                    if (log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"crashlog.txt"));
                    }
                    else
                    {
                        //Compress log
                        var compressedLog = LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(log));
                        attachments.Add(ErrorAttachmentLog.AttachmentWithBinary(compressedLog, @"crashlog.txt.lzma", @"application/x-lzma"));
                    }
                    return attachments;
                };
                M3Log.Information(@"Initializing AppCenter");
                AppCenter.Start(APIKeys.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
            else
            {
                M3Log.Error(@"This build is not configured correctly for AppCenter!");
            }
#else
            if (!APIKeys.HasAppCenterKey)
            {
                Debug.WriteLine(@" >>> This build is missing an API key for AppCenter!");
            }
            else
            {
                Debug.WriteLine(@"This build has an API key for AppCenter");
            }
#endif
        }

        /// <summary>
        /// If the application is running on an AMD processor; use for 'requireamdprocessor' flag
        /// </summary>
        public static bool IsRunningOnAMD;

        public static int BuildNumber = Assembly.GetEntryAssembly().GetName().Version.Revision;
        public static bool BootingUpdate;
        public static int UpdatedFrom = 0;

        /// <summary>
        /// The initial language code used on boot
        /// </summary>
        public static string InitialLanguage = @"int";

        /// <summary>
        /// The currently used language code
        /// </summary>
        internal static string CurrentLanguage = InitialLanguage;

        public static string AppVersion
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $@"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += @"." + assemblyVersion.Build;
                }

                return version;
            }
        }

        public static string AppVersionAbout
        {
            get
            {
                string version = AppVersion;
#if DEBUG
                version += @" DEBUG";
#elif PRERELEASE
                 version += " PRERELEASE";
#endif
                // TODO CHANGE THIS
                return $"{version}, Build {BuildNumber}";
            }
        }

        public static string AppVersionHR
        {
            get
            {
                string version = AppVersion;
#if DEBUG
                version += @" DEBUG";
#elif PRERELEASE
                 version += " PRERELEASE";
#endif
                return $"ME3Tweaks Mod Manager {version} (Build {BuildNumber})";
            }
        }

        /// <summary>
        /// The executable location for this application
        /// </summary>
        public static string ExecutableLocation { 
            get;
#if !AZURE
            private 
#endif
            set;
        }

        public static List<NexusDomainHandler> NexusDomainHandlers { get; } = new();

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// Note! This method is called AFTER it is called from the Crashes library.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (Settings.BetaMode)
            {
                // Don't show this to users who are not on beta.
                MessageBox.Show(e.Exception.FlattenException());
            }

            M3Log.Exception(e.Exception, @"ME3Tweaks Mod Manager has crashed! This is the exception that caused the crash:", true);
        }

        /// <summary>
        /// Called when a fatal crash occurs. Only does something if startup has not completed.
        /// </summary>
        /// <param name="e">The fatal exception.</param>
        public static void OnFatalCrash(Exception e)
        {
            if (!POST_STARTUP)
            {
                M3Log.Fatal(@"ME3Tweaks Mod Manager has encountered a fatal startup crash:");
                M3Log.Fatal(FlattenException(e));
            }
        }

        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <param name="exception">Exception to flatten</param>
        /// <returns>Printable string</returns>
        public static string FlattenException(Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (e.ApplicationExitCode == 0)
            {
                if (!SingleInstanceExit)
                {
                    if (Settings.ModDownloadCacheFolder == null)
                    {
                        try
                        {
                            MUtilities.DeleteFilesAndFoldersRecursively(M3Filesystem.GetModDownloadCacheDirectory(),
                                false);
                            M3Log.Information(@"Deleted mod download cache");
                        }
                        catch
                        {
                            // Don't care
                        }
                    }

                    M3Log.Information(@"Application exiting normally");
                }
                else
                {
                    // We don't log this anymore cause it causes too many duplicate log files to be opened
                    //M3Log.Information(@"Application exiting (duplicate instance)");
                }

                Log.CloseAndFlush();
            }

            // Clean up single instance
            SingleInstance.Cleanup();
        }

        public static bool IsOperatingSystemSupported()
        {
            OperatingSystem os = Environment.OSVersion;
            return os.Version >= App.MIN_SUPPORTED_OS;
        }

#if DEBUG
        private static void ForceImports()
        {
            // This method makes references to some imports that are only actually used by !DEBUG
            // This method is purposely never called. It is so when unnecessary imports are removed,
            // the ones needed by the items in the !DEBUG block remain
            Crashes.TrackError(new Exception("TEST DUMMY"));
            LZMA.Compress(new byte[0], 0);
            var nothing = LogCollector.SessionStartString;
            var nothing2 = FileSize.GibiByte;
            var nothing3 = AppCenter.Configured;
        }
#endif
    }

    class CLIOptions
    {
        public string UpdateDest { get; set; }

        [Option('c', @"completing-update",
            HelpText = @"Indicates that we are booting a new copy of ME3Tweaks Mod Manager that has just been upgraded. --update-from should be included when calling this parameter.")]
        public bool BootingNewUpdate { get; set; }

        [Option(@"update-from",
            HelpText = @"Indicates what build of Mod Manager we are upgrading from.")]
        public int UpdateFromBuild { get; set; }

        [Option(@"update-boot",
            HelpText = @"Indicates that the process should run in update mode for a single file .net executable. The process will exit upon starting because the platform extraction process will have completed.")]
        public bool UpdateBoot { get; set; }

        [Option(@"upgrade-from-me3cmm",
            HelpText = @"Indicates that this is an upgrade from ME3CMM, and that a migration should take place.")]
        public bool UpgradingFromME3CMM { get; set; }

        [Option(@"nxmlink", HelpText = "Instructs Mod Manager to handle an nxm:// link from nexusmods")]
        public string NXMLink { get; set; }

        [Option(@"installmod", HelpText = "Instructs Mod Manager to automatically install the mod from the specified mod path after initialization, to the default target")]
        public string AutoInstallModdescPath { get; set; }

        [Option(@"bootgame", HelpText = "Instructs Mod Manager to automatically boot the specified game after initialization (and after other installation options)")]
        public bool GameBoot { get; set; }

        [Option(@"game", HelpText = "Game to use with various other command line options")]
        public MEGame? RelevantGame { get; set; }

        [Option(@"installasi", HelpText = "Instructs Mod Manager to automatically install the ASI with the specified group ID to the specified game")]
        public int AutoInstallASIGroupID { get; set; }

        [Option(@"asiversion", HelpText = "Chooses a specific version of an ASI when paired with --installasi")]
        public int AutoInstallASIVersion { get; set; }

        [Option(@"installbink", HelpText = "Instructs Mod Manager to automatically install the bink asi loader to the specified game")]
        public bool AutoInstallBink { get; set; }

        [Option(@"createmergedlc", HelpText = "Instructs Mod Manager to automatically (re)create a merge DLC for the given game")]
        public bool CreateMergeDLC { get; set; }

        [Option(@"m3link", HelpText = "Instructs Mod Manager to perform a task based on the contents of a me3tweaksmodmanager:// link")]
        public string M3Link { get; set; }
        
        [Option(@"debuglogging", HelpText = "Enables verbose debug logs")]
        public bool DebugLogging { get; set; }


        [Option(@"compilemergemod", HelpText = "Instructs Mod Manager to compile a mergemod manifest file. Requires providing a moddesc version with the 'featurelevel' option")]
        public string MergeModManifestToCompile { get; set; }

        [Option(@"featurelevel", HelpText = "Indicates the feature level a command line operation uses")]
        public double FeatureLevel { get; set; }
    }
}
