﻿using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for InstallationInformation.xaml
    /// </summary>
    public partial class InstallationInformation : MMBusyPanelBase
    {
        public GameTargetWPF SelectedTarget { get; set; }
        public GameTargetWPF PreviousTarget { get; set; }
        public string BackupLocationString { get; set; }
        public ObservableCollectionExtended<GameTargetWPF> InstallationTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public ObservableCollectionExtended<InstalledDLCMod> DLCModsInstalled { get; } = new ObservableCollectionExtended<InstalledDLCMod>();
        public bool SFARBeingRestored { get; private set; }
        public bool BasegameFilesBeingRestored { get; private set; }
        public bool ShowInstalledOptions { get; private set; }
        public InstallationInformation(List<GameTargetWPF> targetsList, GameTargetWPF selectedTarget)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Installation Information Panel", this);
            DataContext = this;
            InstallationTargets.AddRange(targetsList);
            LoadCommands();
            SelectedTarget = selectedTarget;
        }
        public ICommand RestoreAllModifiedSFARs { get; set; }
        public ICommand RestoreSPModifiedSFARs { get; set; }
        public ICommand RestoreMPModifiedSFARs { get; set; }
        public ICommand RestoreAllModifiedBasegame { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand RemoveTargetCommand { get; set; }

        private void LoadCommands()
        {
            RestoreAllModifiedSFARs = new GenericCommand(RestoreAllSFARs, CanRestoreAllSFARs);
            RestoreMPModifiedSFARs = new GenericCommand(RestoreMPSFARs, CanRestoreMPSFARs);
            RestoreSPModifiedSFARs = new GenericCommand(RestoreSPSFARs, CanRestoreSPSFARs);
            RestoreAllModifiedBasegame = new GenericCommand(RestoreAllBasegame, CanRestoreAllBasegame);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
            RemoveTargetCommand = new GenericCommand(RemoveTarget, CanRemoveTarget);
        }

        private bool CanRestoreMPSFARs()
        {
            return IsPanelOpen && SelectedTarget != null && SelectedTarget.Game != MEGame.Unknown && !MUtilities.IsGameRunning(SelectedTarget.Game) && SelectedTarget.HasModifiedMPSFAR() && !SFARBeingRestored;
        }
        private bool CanRestoreSPSFARs()
        {
            return IsPanelOpen && SelectedTarget.Game != MEGame.Unknown && !MUtilities.IsGameRunning(SelectedTarget.Game) && SelectedTarget.HasModifiedSPSFAR() && !SFARBeingRestored;
        }

        private bool CanRemoveTarget() => SelectedTarget != null && SelectedTarget.Game != MEGame.Unknown && !SelectedTarget.RegistryActive;

        private void RemoveTarget()
        {
            M3Utilities.RemoveCachedTarget(SelectedTarget);
            Result.ReloadTargets = true;
            ClosePanel();
        }

        private void ClosePanel()
        {
            foreach (var installationTarget in InstallationTargets)
            {
                SelectedTarget.ModifiedBasegameFilesView.Filter = null;
                installationTarget.DumpModifiedFilesFromMemory(); //will prevent memory leak
            }

            Result.SelectedTarget = SelectedTarget;
            OnClosing(new DataEventArgs(Result));
        }

        private bool RestoreAllBasegameInProgress;

        /// <summary>
        /// If 'RestoreAllBasegame' can be clicked
        /// </summary>
        /// <returns></returns>
        private bool CanRestoreAllBasegame()
        {
            return IsPanelOpen && SelectedTarget != null && SelectedTarget.Game != MEGame.Unknown
                   && BackupService.GetBackupStatus(SelectedTarget.Game).BackedUp
                   && !RestoreAllBasegameInProgress
                   && !MUtilities.IsGameRunning(SelectedTarget.Game)
                   // Check there is at least one file we can restore
                   && SelectedTarget.ModifiedBasegameFiles.Count(x =>
                       !SelectedTarget.TextureModded || !x.FilePath.RepresentsPackageFilePath()) > 0;
        }

        public string ModifiedFilesFilterText { get; set; }

        private bool FilterBasegameObject(object obj)
        {
            if (!string.IsNullOrWhiteSpace(ModifiedFilesFilterText) && obj is ModifiedFileObject mobj)
            {
                return mobj.FilePath.Contains(ModifiedFilesFilterText, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        public void OnModifiedFilesFilterTextChanged()
        {
            SelectedTarget?.ModifiedBasegameFilesView.Refresh();
        }

        private void RestoreAllBasegame()
        {
            bool restorePackages = true;
            bool restore = false;
            if (SelectedTarget.TextureModded)
            {
                if (!Settings.DeveloperMode)
                {
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogRestoringFilesWhileAlotIsInstalledNotAllowed), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    // Developer mode: Allow bypass.
                    var res = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogRestoringFilesWhileAlotIsInstalledNotAllowedDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                    
                }
            }
            else
            {
                restore = M3L.ShowDialog(window, M3L.GetString(M3L.string_restoreAllModifiedFilesQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"RestoreAllBasegameFilesThread");
                nbw.DoWork += (a, b) =>
                {
                    RestoreAllBasegameInProgress = true;
                    var restorableFiles = SelectedTarget.ModifiedBasegameFiles.Where(x => x.CanRestoreFile()).ToList();
                    //Set UI states
                    foreach (var v in restorableFiles)
                    {
                        v.Restoring = true;
                    }
                    //Restore files
                    foreach (var v in restorableFiles) //to list will make sure this doesn't throw concurrent modification
                    {
                        v.RestoreFile(true);
                    }
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    RestoreAllBasegameInProgress = false;
                    if (SelectedTarget.Game == MEGame.ME3 || SelectedTarget.Game.IsLEGame())
                    {
                        Result.TargetsToAutoTOC.Add(SelectedTarget);
                    }
                    CommandManager.InvalidateRequerySuggested();
                };
                nbw.RunWorkerAsync();
            }
        }

        private void RestoreSPSFARs()
        {
            bool restore;
            checkSFARRestoreForBackup();
            if (SelectedTarget.TextureModded)
            {
                if (!Settings.DeveloperMode)
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoreSPSfarsQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                foreach (var v in SelectedTarget.ModifiedSFARFiles)
                {
                    if (v.IsSPSFAR)
                    {
                        SelectedTarget.RestoreSFAR(v, true);
                    }
                }
            }
        }

        private void RestoreMPSFARs()
        {
            bool restore;
            checkSFARRestoreForBackup();
            if (SelectedTarget.TextureModded)
            {
                if (!Settings.DeveloperMode)
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoreMPSfarsQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                foreach (var v in SelectedTarget.ModifiedSFARFiles)
                {
                    if (v.IsMPSFAR)
                    {
                        SelectedTarget.RestoreSFAR(v, true);
                    }
                }
            }
        }

        private void RestoreAllSFARs()
        {
            bool restore;
            checkSFARRestoreForBackup();
            if (SelectedTarget.TextureModded)
            {
                if (!Settings.DeveloperMode)
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;
                }
            }
            else
            {
                restore = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoreAllModifiedSfarsQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }
            if (restore)
            {
                foreach (var v in SelectedTarget.ModifiedSFARFiles)
                {
                    SelectedTarget.RestoreSFAR(v, true);
                }
            }
        }

        private void checkSFARRestoreForBackup()
        {
            var bup = BackupService.GetGameBackupPath(SelectedTarget.Game);
            if (bup == null)
            {
                M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialog_restoringSFARWithoutBackup),
                    M3L.GetString(M3L.string_backupNotAvailable), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanRestoreAllSFARs()
        {
            return IsPanelOpen && SelectedTarget != null && SelectedTarget.Game != MEGame.Unknown && !MUtilities.IsGameRunning(SelectedTarget.Game) && SelectedTarget.ModifiedSFARFiles.Count > 0 && !SFARBeingRestored;
        }

        /// <summary>
        /// This method is run on a background thread so all UI calls needs to be wrapped
        /// </summary>
        private void PopulateUI()
        {
            var selectedTarget = SelectedTarget; // We cache this here because there is a lot of accessing of this var on async thread. And it can change
            if (selectedTarget == null || selectedTarget.Game == MEGame.Unknown) return; // Do not populate anything
            NamedBackgroundWorker bw = new NamedBackgroundWorker($@"InstallationInformation-{nameof(PopulateUI)}");
            bw.DoWork += (a, b) =>
            {
                bool deleteConfirmationCallback(InstalledDLCMod mod)
                {
                    if (MUtilities.IsGameRunning(selectedTarget.Game))
                    {
                        M3L.ShowDialog(Window.GetWindow(this),
                            M3L.GetString(M3L.string_interp_cannotDeleteModsWhileXIsRunning,
                                selectedTarget.Game.ToGameName()), M3L.GetString(M3L.string_gameRunning),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    if (selectedTarget.TextureModded)
                    {
                        var res = M3L.ShowDialog(Window.GetWindow(this),
                            M3L.GetString(M3L.string_interp_deletingXwhileTexturesInstalled, mod.ModName),
                            M3L.GetString(M3L.string_deletingWillPutGameInUnsupportedConfig), MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        return res == MessageBoxResult.Yes;
                    }

                    return M3L.ShowDialog(Window.GetWindow(this),
                        M3L.GetString(M3L.string_interp_removeXFromTheGameInstallationQuestion, mod.ModName),
                        M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes;
                }

                void notifyDLCModDeleted()
                {
                    if (selectedTarget.Game.IsGame1() || selectedTarget.Game.IsGame2())
                    {
                        Result.TargetsToPlotManagerSync.Add(selectedTarget);
                    }

                    if (selectedTarget.Game == MEGame.LE1)
                    {
                        Result.TargetsToLE1Merge.Add(selectedTarget); // Rebuild coalesced merge
                    }

                    if (selectedTarget.Game.IsGame2())
                    {
                        Result.TargetsToEmailMergeSync.Add(selectedTarget);
                    }

                    if (selectedTarget.Game.IsGame3() || selectedTarget.Game == MEGame.LE2) // ME2 is not supported for merge due to having to edit the SWF
                    {
                        Result.TargetsToSquadmateMergeSync.Add(selectedTarget);
                    }
                    selectedTarget.PopulateDLCMods(true, deleteConfirmationCallback, notifyDLCModDeleted, notifyToggled);
                }

                void notifyToggled()
                {
                    if (selectedTarget.Game.IsGame1() || selectedTarget.Game.IsGame2())
                    {
                        Result.TargetsToPlotManagerSync.Add(selectedTarget);
                    }

                    if (selectedTarget.Game == MEGame.LE1)
                    {
                        Result.TargetsToLE1Merge.Add(selectedTarget); // Rebuild coalesced merge
                    }

                    if (selectedTarget.Game.IsGame2())
                    {
                        Result.TargetsToEmailMergeSync.Add(selectedTarget);
                    }

                    if (selectedTarget.Game.IsGame3() || selectedTarget.Game == MEGame.LE2) // ME2 is not supported for merge due to having to edit the SWF
                    {
                        Result.TargetsToSquadmateMergeSync.Add(selectedTarget);
                    }
                }

                selectedTarget.PopulateDLCMods(true, deleteConfirmationCallback, notifyDLCModDeleted, notifyToggled);
                selectedTarget.PopulateExtras();
                selectedTarget.PopulateTextureInstallHistory();
                bool restoreBasegamefileConfirmationCallback(string filepath)
                {
                    if (MUtilities.IsGameRunning(selectedTarget.Game))
                    {
                        M3L.ShowDialog(Window.GetWindow(this),
                            M3L.GetString(M3L.string_interp_cannotRestoreFilesWhileXIsRunning, selectedTarget.Game.ToGameName()), M3L.GetString(M3L.string_gameRunning),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    if (selectedTarget.TextureModded && filepath.RepresentsPackageFilePath())
                    {
                        if (!Settings.DeveloperMode)
                        {
                            M3L.ShowDialog(Window.GetWindow(this),
                                M3L.GetString(M3L.string_interp_restoringXWhileAlotInstalledIsNotAllowed,
                                    Path.GetFileName(filepath)), M3L.GetString(M3L.string_cannotRestorePackageFiles),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return false;
                        }
                        else
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this),
                                M3L.GetString(M3L.string_interp_restoringXwhileAlotInstalledLikelyBreaksThingsDevMode,
                                    Path.GetFileName(filepath)),
                                M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            return res == MessageBoxResult.Yes;

                        }
                    }

                    bool? holdingShift = Keyboard.Modifiers == ModifierKeys.Shift;
                    if (!holdingShift.Value) holdingShift = null;
                    return holdingShift ?? M3L.ShowDialog(Window.GetWindow(this),
                        M3L.GetString(M3L.string_interp_restoreXquestion, Path.GetFileName(filepath)),
                        M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes;
                }

                bool restoreSfarConfirmationCallback(string sfarPath)
                {
                    if (MUtilities.IsGameRunning(selectedTarget.Game))
                    {
                        M3L.ShowDialog(Window.GetWindow(this),
                            M3L.GetString(M3L.string_interp_cannotRestoreFilesWhileXIsRunning,
                                selectedTarget.Game.ToGameName()), M3L.GetString(M3L.string_gameRunning),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    if (selectedTarget.TextureModded)
                    {
                        if (!Settings.DeveloperMode)
                        {
                            M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked),
                                M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return false;
                        }
                        else
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this),
                                M3L.GetString(M3L.string_restoringSfarsAlotDevMode),
                                M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            return res == MessageBoxResult.Yes;

                        }
                    }

                    //Todo: warn of unpacked file deletion
                    bool? holdingShift = Keyboard.Modifiers == ModifierKeys.Shift;
                    if (!holdingShift.Value) holdingShift = null;
                    return holdingShift ?? M3L.ShowDialog(Window.GetWindow(this),
                        M3L.GetString(M3L.string_interp_restoreXquestion, sfarPath),
                        M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes;
                }

                void notifyStartingSfarRestoreCallback()
                {
                    SFARBeingRestored = true;
                }

                void notifyStartingBasegameFileRestoreCallback()
                {
                    BasegameFilesBeingRestored = true;
                }

                void notifyRestoredCallback(object itemRestored)
                {
                    if (selectedTarget == null)
                        return; // User may have closed panel or something
                    if (itemRestored is ModifiedFileObject mf)
                    {

                        if (selectedTarget.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3)
                            Result.TargetsToAutoTOC.Add(selectedTarget);
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            selectedTarget.ModifiedBasegameFiles.Remove(mf);
                        });
                        bool resetBasegameFilesBeingRestored = selectedTarget.ModifiedBasegameFiles.Count == 0;
                        if (!resetBasegameFilesBeingRestored)
                        {
                            resetBasegameFilesBeingRestored =
                                !selectedTarget.ModifiedBasegameFiles.Any(x => x.Restoring);
                        }

                        if (resetBasegameFilesBeingRestored)
                        {
                            BasegameFilesBeingRestored = false;
                        }
                    }
                    else if (itemRestored is SFARObject ms)
                    {
                        if (!ms.IsModified)
                        {
                            selectedTarget.ModifiedSFARFiles.Remove(ms);
                        }

                        bool resetSfarsBeingRestored = selectedTarget.ModifiedSFARFiles.Count == 0;
                        if (!resetSfarsBeingRestored)
                        {
                            resetSfarsBeingRestored = !selectedTarget.ModifiedSFARFiles.Any(x => x.Restoring);
                        }

                        if (resetSfarsBeingRestored)
                        {
                            SFARBeingRestored = false;
                        }
                    }
                    else if (itemRestored == null)
                    {
                        //restore failed.
                        bool resetSfarsBeingRestored = selectedTarget.ModifiedSFARFiles.Count == 0;
                        if (!resetSfarsBeingRestored)
                        {
                            resetSfarsBeingRestored = !selectedTarget.ModifiedSFARFiles.Any(x => x.Restoring);
                        }

                        if (resetSfarsBeingRestored)
                        {
                            SFARBeingRestored = false;
                        }

                        bool resetBasegameFilesBeingRestored = selectedTarget.ModifiedBasegameFiles.Count == 0;
                        if (!resetBasegameFilesBeingRestored)
                        {
                            resetBasegameFilesBeingRestored =
                                !selectedTarget.ModifiedBasegameFiles.Any(x => x.Restoring);
                        }

                        if (resetBasegameFilesBeingRestored)
                        {
                            BasegameFilesBeingRestored = false;
                        }
                    }
                }

                if (selectedTarget != null)
                {
                    // 06/16/2022 - Change from not populating at all if texture modded
                    // to filtering out package files and tfc files since they will all be modified.

                    // 06/01/2024 - Change to allow showing packages files if texture modded,
                    // but only if they are tracked. Untracked will show nothing. I guess we
                    // could technically track non-vanilla before texture install.
                    selectedTarget?.PopulateModifiedBasegameFiles(restoreBasegamefileConfirmationCallback,
                        restoreSfarConfirmationCallback,
                        notifyStartingSfarRestoreCallback,
                        notifyStartingBasegameFileRestoreCallback,
                        notifyRestoredCallback);
                }

                SFARBeingRestored = false;

                selectedTarget.PopulateASIInfo();
                selectedTarget.PopulateBinkInfo();

                // Look for all changes
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"BasegameSourceIdentifier");
                nbw.DoWork += (a, b) =>
                {
                    foreach (var v in selectedTarget.ModifiedBasegameFiles.ToList())
                    {
                        v.DetermineSource();
                    }
                };
                nbw.RunWorkerAsync();
            };
            bw.RunWorkerCompleted += (a, b) => { DataIsLoading = false; };
            DataIsLoading = true;
            bw.RunWorkerAsync();
        }

        public void OnSelectedTargetChanged()
        {
            if (PreviousTarget != null)
            {
                PreviousTarget.ModifiedBasegameFilesView.Filter = null;
            }
            DLCModsInstalled.ClearEx();

            if (SelectedTarget != null && SelectedTarget.Game == MEGame.LELauncher && SelectedTarget_TabControl != null)
            {
                // Launcher can only select modified basegame files.
                SelectedTarget_TabControl.SelectedItem = ModifiedBasegameFiles_Tab;
            }

            //Get installed mod information
            //NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"InstallationInformationDataPopulator");
            //nbw.DoWork += (a, b) =>
            //{
            if (SelectedTarget != null)
            {
                PopulateUI();
                var backupLoc = BackupService.GetGameBackupPath(SelectedTarget.Game);
                if (backupLoc != null)
                {
                    BackupLocationString = M3L.GetString(M3L.string_interp_backupAtX, backupLoc);
                }
                else
                {
                    BackupLocationString = M3L.GetString(M3L.string_noBackupForThisGame);
                }

                SelectedTarget.ModifiedBasegameFilesView.Filter = FilterBasegameObject;
            }
            else
            {
                BackupLocationString = null;
            }
            //};
            //nbw.RunWorkerCompleted += (await, b) =>
            //{
            //    if (b.Error != null)
            //    {
            //        M3Log.Error($@"Error in installation information data populator: {b.Error.Message}");
            //    }
            //};
            //nbw.RunWorkerAsync();
            PreviousTarget = SelectedTarget;
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenExplorer(SelectedTarget.TargetPath);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                ClosePanel();
            }
        }

        private bool CanClose()
        {
            return !SFARBeingRestored && !BasegameFilesBeingRestored;
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
        }

        private void OpenASIManager_Click(object sender, RequestNavigateEventArgs e)
        {
            Result.PanelToOpen = EPanelID.ASI_MANAGER;
            ClosePanel();
        }

        private void OpenALOTInstaller_Click(object sender, RoutedEventArgs e)
        {
            Result.ToolToLaunch = ExternalToolLauncher.ALOTInstaller;
            ClosePanel();
        }

        public string ShowInstalledOptionsText { get; private set; } = M3L.GetString(M3L.string_showInstalledOptions);

        /// <summary>
        /// If data is populating into the interface still
        /// </summary>
        public bool DataIsLoading { get; set; }

        private void ToggleShowingInstallOptions_Click(object sender, RoutedEventArgs e)
        {
            ShowInstalledOptions = !ShowInstalledOptions;
            ShowInstalledOptionsText = ShowInstalledOptions ? M3L.GetString(M3L.string_hideInstalledOptions) : M3L.GetString(M3L.string_showInstalledOptions);
        }

        private void OpenMEMLE_Click(object sender, RoutedEventArgs e)
        {
            Result.ToolToLaunch = ExternalToolLauncher.MEM_LE;
            ClosePanel();
        }
    }
}
