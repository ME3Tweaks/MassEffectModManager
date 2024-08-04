﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save.shared;
using ME3TweaksModManager.modmanager.windows.input;
using ME3TweaksModManager.ui;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for LogUploaderPanel.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class LogUploaderPanel : MMBusyPanelBase
    {
        public bool UploadingLog { get; private set; }
        public string CollectionStatusMessage { get; set; }
        //public string TopText { get; private set; } = M3L.GetString(M3L.string_selectALogToView);
        public ObservableCollectionExtended<LogItem> AvailableLogs { get; } = new ObservableCollectionExtended<LogItem>();
        public ObservableCollectionExtended<GameTargetWPF> DiagnosticTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public LogUploaderPanel()
        {
            DataContext = this;
            LoadCommands();
        }

        private void InitLogUploaderUI()
        {
            AvailableLogs.ClearEx();
            var directory = new DirectoryInfo(MCoreFilesystem.GetLogDir());
            var logfiles = directory.GetFiles(@"modmanagerlog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            AvailableLogs.Add(new LogItem(M3L.GetString(M3L.string_noApplicationLog)) { Selectable = false });
            AvailableLogs.AddRange(logfiles.Select(x => new LogItem(x.FullName) { IsActiveLog = x.FullName.Equals(M3Log.CurrentLogFilePath, StringComparison.InvariantCultureIgnoreCase) }));
            SelectedLog = AvailableLogs.FirstOrDefault(x => x.IsActiveLog);
            var targets = mainwindow.InstallationTargets.Where(x => x.Selectable);
            DiagnosticTargets.Add(new GameTargetWPF(MEGame.Unknown, M3L.GetString(M3L.string_selectAGameTargetToGenerateDiagnosticsFor), false, true));
            DiagnosticTargets.AddRange(targets.Where(x => x.Game != MEGame.LELauncher));
            SelectedDiagnosticTarget = DiagnosticTargets.FirstOrDefault();
            //if (LogSelector_ComboBox.Items.Count > 0)
            //{
            //    LogSelector_ComboBox.SelectedIndex = 0;
            //}
        }

        public ICommand UploadLogCommand { get; set; }
        public ICommand CancelUploadCommand { get; set; }
        public ICommand SelectSaveCommand { get; set; }
        public LogItem SelectedLog { get; set; }
        public ISaveFile SelectedSaveFile { get; set; }
        public string SelectedSaveText { get; set; } = M3L.GetString(M3L.string_noSaveSelected);
        public GameTargetWPF SelectedDiagnosticTarget { get; set; }

        private void LoadCommands()
        {
            UploadLogCommand = new GenericCommand(StartLogUploadManual, CanUploadLog);
            CancelUploadCommand = new GenericCommand(CancelUpload, CanCancelUpload);
            SelectSaveCommand = new GenericCommand(SelectSave, CanSelectSave);
        }

        private void SelectSave()
        {
            SaveSelectorUI ssui = new SaveSelectorUI(window, SelectedDiagnosticTarget,
                M3L.GetString(M3L.string_selectASaveToIncludeWithDiagnostic));
            ssui.Show();
            ssui.Closed += (sender, args) =>
            {
                if (ssui.SaveWasSelected && ssui.SelectedSaveFile != null)
                {
                    SelectedSaveFile = ssui.SelectedSaveFile;
                    SelectedSaveText = Path.GetFileName(SelectedSaveFile.SaveFilePath);
                }
            };
        }

        private bool CanSelectSave()
        {
            if (UploadingLog) return false;
            if (SelectedDiagnosticTarget == null) return false;
            return SelectedDiagnosticTarget.Game.IsLEGame();
        }

        private void StartLogUploadManual()
        {
            StartLogUpload();
        }

        private void CancelUpload()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancelUpload()
        {
            return !UploadingLog;
        }

        private void StartLogUpload(bool isPreviousCrashLog = false)
        {
            UploadingLog = true;
            //TopText = M3L.GetString(M3L.string_collectingLogInformation);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"LogUpload");
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);

                }
                else if (b.UserState is MTaskbarState tbps)
                {
                    TaskbarHelper.SetProgressState(tbps);
                }
            };
            nbw.DoWork += (a, b) =>
            {
                void updateStatusCallback(string status)
                {
                    CollectionStatusMessage = status;
                }

                void updateProgressCallback(int progress)
                {
                    nbw.ReportProgress(0, progress / 100.0);
                }

                void updateTaskbarProgressStateCallback(MTaskbarState state)
                {
                    nbw.ReportProgress(-1, state);
                }

                LogUploadPackage package = new LogUploadPackage()
                {
                    DiagnosticTarget = SelectedDiagnosticTarget,
                    SelectedLog = SelectedLog,
                    Attachments = GetAttachments(),
                    PerformFullTexturesCheck = TextureCheck,
                    UpdateTaskbarProgressStateCallback = updateTaskbarProgressStateCallback,
                    UpdateProgressCallback = updateProgressCallback,
                    SelectedSaveFilePath = SelectedSaveFile?.SaveFilePath,
                    UpdateStatusCallback = updateStatusCallback,
                };

                b.Result = LogCollector.SubmitDiagnosticLog(package);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                if (b.Error == null && b.Result is string response)
                {
                    if (response.StartsWith(@"http"))
                    {
                        M3Utilities.OpenWebpage(response);
                    }
                    else
                    {
                        OnClosing(DataEventArgs.Empty);
                        var res = M3L.ShowDialog(Window.GetWindow(this), response, M3L.GetString(M3L.string_logUploadFailed), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        private Dictionary<string, byte[]> GetAttachments()
        {
            var attachments = new Dictionary<string, byte[]>();

            if (SelectedSaveFile != null && File.Exists(SelectedSaveFile.SaveFilePath))
            {
                attachments.Add(Path.GetFileName(SelectedSaveFile.SaveFilePath), File.ReadAllBytes(SelectedSaveFile.SaveFilePath));
            }

            return attachments;
        }

        public bool TextureCheck { get; set; } = true;

        private bool CanUploadLog() => !UploadingLog && ((SelectedDiagnosticTarget != null && SelectedDiagnosticTarget.Game > MEGame.Unknown) || (SelectedLog != null && SelectedLog.Selectable));

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !UploadingLog)
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }

            if (e.Key == Key.Space)
            {
                Debugger.Break();
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            InitLogUploaderUI();
        }
    }
}
