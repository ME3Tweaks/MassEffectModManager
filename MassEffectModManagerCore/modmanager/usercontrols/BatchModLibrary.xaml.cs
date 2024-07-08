﻿using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.FileSource;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using SevenZip;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BatchModLibrary.xaml
    /// </summary>
    public partial class BatchModLibrary : MMBusyPanelBase
    {
        public BatchLibraryInstallQueue SelectedBatchQueue { get; set; }
        public object SelectedModInGroup { get; set; }
        public ObservableCollectionExtended<BatchLibraryInstallQueue> AvailableBatchQueues { get; } = new ObservableCollectionExtended<BatchLibraryInstallQueue>();
        public ObservableCollectionExtended<GameTargetWPF> InstallationTargetsForGroup { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public BatchModLibrary()
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Installer Panel", this);
            LoadCommands();
        }

        /// <summary>
        /// If the batch library is loading biq files
        /// </summary>
        public bool IsLoading { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand CreateNewGroupCommand { get; private set; }
        public ICommand InstallGroupCommand { get; private set; }
        public ICommand EditGroupCommand { get; private set; }
        public ICommand DuplicateGroupCommand { get; private set; }
        public ICommand DeleteGroupCommand { get; private set; }
        public ICommand TriggerDataReloadCommand { get; private set; }
        public ICommand DeployQueueCommand { get; private set; }

        public bool CanCompressPackages => SelectedBatchQueue != null && SelectedBatchQueue.Game is MEGame.ME2 or MEGame.ME3;

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
            CreateNewGroupCommand = new GenericCommand(CreateNewGroup, () => !IsLoading);
            InstallGroupCommand = new GenericCommand(InstallGroup, CanInstallGroup);
            EditGroupCommand = new GenericCommand(EditGroup, CanOperateOnBatchQueue);
            DeleteGroupCommand = new GenericCommand(DeleteGroup, CanOperateOnBatchQueue);
            DuplicateGroupCommand = new GenericCommand(DuplicateGroup, CanOperateOnBatchQueue);
            TriggerDataReloadCommand = new GenericCommand(ReloadModData, CanTriggerReload);
            DeployQueueCommand = new GenericCommand(DeployQueue, CanDeployQueue);
        }

        private bool CanDeployQueue()
        {
            return SelectedBatchQueue != null && SelectedBatchQueue.QueueFormatVersion >= 3; // Must be saved new
        }

        private void DeployQueue()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Title = M3L.GetString(M3L.string_selectDeploymentDestination),
                Filter = $@"{M3L.GetString(M3L.string_7zipArchiveFile)}|*.7z",
                FileName = $@"{SelectedBatchQueue.ModName}_installgroup.7z"
            };
            if (d.ShowDialog() != true)
            {
                return;
            }

            var sourceFile = Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), SelectedBatchQueue.BackingFilename);

            // Compression
            var compressor = new SevenZipCompressor();

            // Pass 1: Directories
            // Stored uncompressed
            compressor.CustomParameters.Add(@"s", @"off");
            compressor.CompressionMode = CompressionMode.Create;
            compressor.CompressionLevel = CompressionLevel.Ultra;
            compressor.CompressFiles(d.FileName, sourceFile);

            M3Utilities.OpenExplorer(Directory.GetParent(d.FileName).FullName);
        }

        private bool CanTriggerReload()
        {
            return SelectedBatchQueue != null && App.IsDebug;
        }

        private void DuplicateGroup()
        {
            if (SelectedBatchQueue == null) return;

            var result = PromptDialog.Prompt(window, M3L.GetString(M3L.string_enterANewNameForTheDuplicatedInstallGroup), M3L.GetString(M3L.string_enterNewName),
                            M3L.GetString(M3L.string_interp_defaultDuplicateName, SelectedBatchQueue.ModName), true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                var originalQueue = SelectedBatchQueue; // Cache in event we lose reference to this after possible reload. We don't want to set name on wrong object.
                var originalName = SelectedBatchQueue.ModName;
                try
                {
                    var destPath = Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(),
                        result + Path.GetExtension(SelectedBatchQueue.BackingFilename));
                    if (!File.Exists(destPath))
                    {
                        SelectedBatchQueue.ModName = result;
                        SelectedBatchQueue.Save(false, destPath);
                        parseBatchFiles(destPath);
                    }
                    else
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_anInstallGroupWithThisNameAlreadyExists), M3L.GetString(M3L.string_error),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, @"Error duplicating batch queue: ");
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_errorDuplicatingInstallGroupX, e.Message), M3L.GetString(M3L.string_error),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // If we reload list this variable may become null.
                    originalQueue.ModName = originalName; // Restore if we had an error
                }
            }

        }

        private void DeleteGroup()
        {
            var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_deleteTheSelectedBatchQueue, SelectedBatchQueue.ModName), M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                File.Delete(Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), SelectedBatchQueue.BackingFilename));
                AvailableBatchQueues.Remove(SelectedBatchQueue);
                SelectedBatchQueue = null;
            }
        }

        private void EditGroup()
        {
            var editGroupUI = new BatchModQueueEditor(mainwindow, SelectedBatchQueue);
            // Original code.
            editGroupUI.ShowDialog();
            var newPath = editGroupUI.SavedPath;
            if (newPath != null)
            {
                //file was saved, reload
                Task.Run(() =>
                {
                    parseBatchFiles(newPath);
                });
            }


#if DEBUG
            // Debug code. Requires commenting out the above.
            //editGroupUI.Show();
#endif
        }

        private bool CanOperateOnBatchQueue() => SelectedBatchQueue != null && !IsLoading;

        private void InstallGroup()
        {
            // Has user saved options before?
            if (SelectedBatchQueue.ModsToInstall.Any(x => !x.IsStandalone && x.HasChosenOptions))
            {
                if (SelectedBatchQueue.ModsToInstall.Any(x => x.ChosenOptionsDesync || !x.HasChosenOptions))
                {
                    M3L.ShowDialog(window,
                        M3L.GetString(M3L.string_tooltip_batchQueueDesync),
                        M3L.GetString(M3L.string_batchQueueDesync), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    SelectedBatchQueue.UseSavedOptions = M3L.ShowDialog(window, M3L.GetString(M3L.string_usePreviouslySavedModOptionsQuestion), M3L.GetString(M3L.string_savedOptionsFound),
                                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                }
            }

            TelemetryInterposer.TrackEvent(@"Installing Batch Group", new Dictionary<string, string>()
            {
                {@"Group name", SelectedBatchQueue.ModName},
                {@"Group size", SelectedBatchQueue.AllModsToInstall.Count.ToString()},
                {@"Game", SelectedBatchQueue.Game.ToString()},
                {@"TargetPath", SelectedGameTarget?.TargetPath}
            });
            OnClosing(new DataEventArgs(SelectedBatchQueue));
        }

        private bool CanInstallGroup()
        {
            if (SelectedGameTarget == null || SelectedBatchQueue == null) return false;
            return SelectedBatchQueue.AllModsToInstall.Any(x => x.IsAvailableForInstall());
        }

        private void CreateNewGroup()
        {
            var gameDialog = DropdownSelectorDialog.GetSelection<MEGame>(window, M3L.GetString(M3L.string_gameSelector), MEGameSelector.GetEnabledGames(), M3L.GetString(M3L.string_dialog_selectGameInstallGroup), null);
            if (gameDialog is MEGame game)
            {
                var editGroupUI = new BatchModQueueEditor(mainwindow) { SelectedGame = game };
                editGroupUI.ShowDialog();
                var newPath = editGroupUI.SavedPath;
                if (newPath != null)
                {
                    //file was saved, reload
                    Task.Run(() =>
                    {
                        parseBatchFiles(newPath);
                    });
                }
            }
        }

        private void ClosePanel()
        {
            // Release all assets
            var memMods = M3LoadedMods.GetAllM3ManagedMEMs(m3mmOnly: true);
            foreach (var memMod in memMods.OfType<M3MEMMod>())
            {
                memMod.ImageBitmap = null; // Lose reference so GC can take it
            }

            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public void OnIsLoadingChanged()
        {

            ClipperHelper.ShowHideVerticalContent(LoadingStatusPanel, IsLoading);
            if (IsLoading)
            {
                SelectedBatchQueue = null;
            }
            else if (SelectedBatchQueue == null)
            {
                // Select first one to populate the UI.
                SelectedBatchQueue = AvailableBatchQueues.FirstOrDefault();
            }
            Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested); // Refresh bindings... if only you could invoke directly.
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            if (RefreshContentsOnVisible)
            {
                ReloadModData();
            }
            else
            {
                Task.Run(() =>
                {
                    parseBatchFiles();
                });
            }

            RefreshContentsOnVisible = false;
        }

        private void ReloadModData()
        {
            IsEnabled = false;
            M3LoadedMods.ModsReloaded += OnModLibraryReloaded;
            List<string> scopedModsToReload = new List<string>();
            var libraryRoot = M3LoadedMods.GetCurrentModLibraryDirectory(); // biq2 stores relative to library root. biq stores to library root FOR GAME

            if (SelectedBatchQueue != null)
            {
                foreach (var bm in SelectedBatchQueue.ModsToInstall)
                {
                    var fullModdescPath = Path.Combine(libraryRoot, bm.ModDescPath);
                    if (File.Exists(fullModdescPath))
                    {
                        scopedModsToReload.Add(fullModdescPath);
                    }
                }
            }
            M3LoadedMods.Instance.LoadMods(scopedModsToReload: scopedModsToReload);
        }

        private void OnModLibraryReloaded(object sender, EventArgs e)
        {
            M3LoadedMods.ModsReloaded -= OnModLibraryReloaded;
            TriggerResize();

            var registeredTextureMods = M3LoadedMods.GetAllM3ManagedMEMs();
            foreach (var queue in AvailableBatchQueues)
            {
                foreach (var mod in queue.ModsToInstall)
                {
                    mod.Init(false);
                }

                bool rebuildTextureList = false;
                for (int i = 0; i < queue.TextureModsToInstall.Count; i++)
                {
                    var existingEntry = queue.TextureModsToInstall[i];
                    if (existingEntry is not M3MEMMod && existingEntry != null) // Are we a MEMMod but not an M3MEMMod
                    {
                        // See if we need to re-associate it to an M3MEMMod
                        var matchingEntry = registeredTextureMods.FirstOrDefault(x => x.GetFilePathToMEM().CaseInsensitiveEquals(existingEntry.GetFilePathToMEM())); // Find registered M3MM with same filepath
                        if (matchingEntry != null)
                        {
                            queue.TextureModsToInstall.RemoveAt(i);
                            queue.TextureModsToInstall.Insert(i, matchingEntry);
                            rebuildTextureList = true;
                        }
                    }
                    else
                    {
                        existingEntry.ParseMEMData();
                    }
                }

                if (rebuildTextureList)
                {
                    // Re-build the all mods list
                    queue.AllModsToInstall.RemoveAll(x => x is MEMMod);
                    queue.AllModsToInstall.AddRange(queue.TextureModsToInstall);
                }
            }

            OnSelectedModInGroupChanged(); // Refresh UI
            IsEnabled = true;
        }

        private void parseBatchFiles(string pathToHighlight = null)
        {
            IsLoading = true;

            #region MIGRATION
            // Mod Manager 8.0.1 moved these to the mod library
            var batchDirOld = M3LoadedMods.GetBatchInstallGroupsDirectoryPre801();
            if (Directory.Exists(batchDirOld))
            {
                var oldFiles = Directory.GetFiles(batchDirOld);
                foreach (var f in oldFiles)
                {
                    M3Log.Information($@"Migrating batch queue {f} to library");
                    try
                    {
                        File.Move(f, Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), Path.GetFileName(f)),
                            true);
                    }
                    catch (Exception ex)
                    {
                        M3Log.Exception(ex, @"Failed to migrate:");
                    }
                }
            }
            IsLoading = true;
            #endregion

            AvailableBatchQueues.ClearEx();
            var batchDir = M3LoadedMods.GetBatchInstallGroupsDirectory();
            var files = Directory.GetFiles(batchDir).ToList();

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension is @".biq2" or @".biq" or @".txt")
                {
                    try
                    {
                        var queue = BatchLibraryInstallQueue.LoadInstallQueue(file);
                        if (queue != null && queue.Game.IsEnabledGeneration())
                        {
                            AvailableBatchQueues.Add(queue);
                            if (file == pathToHighlight)
                            {
                                SelectedBatchQueue = queue;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Exception(e, @"Error occurred parsing batch queue file:");
                        Crashes.TrackError(new Exception(@"Error parsing batch queue file", e), new Dictionary<string, string>()
                        {
                            {@"Filename", file}
                        });
                        App.SubmitAnalyticTelemetryEvent("");
                    }
                }
            }

            IsLoading = false;
        }

        public GameTargetWPF SelectedGameTarget { get; set; }

        private void OnSelectedBatchQueueChanged()
        {
            GameTargetWPF currentTarget = SelectedGameTarget;
            SelectedGameTarget = null;
            InstallationTargetsForGroup.ClearEx();
            if (SelectedBatchQueue != null)
            {
                // Telemetry shows null access here... mainwindow? Is this being called before the panel loads somehow? Or maybe it's during a target reload?
                // Lock access to installation targets
                lock (MainWindow.targetRepopulationSyncObj)
                {
                    InstallationTargetsForGroup.AddRange(mainwindow.InstallationTargets.Where(x => x.Game == SelectedBatchQueue.Game));
                }

                if (InstallationTargetsForGroup.Contains(currentTarget))
                {
                    SelectedGameTarget = currentTarget;
                }
                else
                {
                    SelectedGameTarget = InstallationTargetsForGroup.FirstOrDefault();
                }

                if (SelectedBatchQueue.ModsToInstall.Any())
                {
                    SelectedModInGroup = SelectedBatchQueue.ModsToInstall.First();
                }

                if (SelectedBatchQueue.Game == MEGame.ME1) SelectedBatchQueue.InstallCompressed = false;
            }
            TriggerPropertyChangedFor(nameof(CanCompressPackages));
        }

        public string ModDescriptionText { get; set; }

        /// <summary>
        /// If the website panel is open or not
        /// </summary>
        private bool WebsitePanelStatus;

        private void SetWebsitePanelVisibility(object mod)
        {
            bool open = false; // If panel is to open or not

            if (mod is IBatchQueueMod bm && bm.IsAvailableForInstall() == false && bm.Hash != null)
            {
                if (FileSourceService.TryGetSource(bm.Size, bm.Hash, out var link))
                {
                    open = true;
                    SelectedUnavailableModLink = link;
                }
            }

            if (open != WebsitePanelStatus)
            {
                void done()
                {
                    WebsitePanelStatus = open;
                }

                ClipperHelper.ShowHideVerticalContent(VisitWebsitePanel, open, completionDelegate: done);
            }
        }

        /// <summary>
        /// The link that clicking the download link will go to
        /// </summary>
        public string SelectedUnavailableModLink { get; set; }

        public void OnSelectedModInGroupChanged()
        {
            SelectedUnavailableModLink = null; // Reset
            SetWebsitePanelVisibility(SelectedModInGroup); // Update state
            if (SelectedModInGroup == null)
            {
                ModDescriptionText = "";
            }
            else
            {
                if (SelectedModInGroup is BatchMod bm)
                {
                    ModDescriptionText = bm.Mod?.DisplayedModDescription ?? M3L.GetString(M3L.string_modNotAvailableForInstall);
                }
                else if (SelectedModInGroup is BatchASIMod bam)
                {
                    ModDescriptionText = bam.AssociatedMod?.Description ?? M3L.GetString(M3L.string_modNotAvailableForInstall);
                }
                else if (SelectedModInGroup is MEMMod mm)
                {
                    ModDescriptionText = mm.FileExists ? M3L.GetString(M3L.string_interp_textureModModifiesExportsX, string.Join('\n', mm.GetModifiedExportNames())) : M3L.GetString(M3L.string_modNotAvailableForInstall); ;
                    if (mm is M3MEMMod m3mm)
                    {
                        // Todo: Store hash of moddesc with the M3MM mod in the BIQ
                        // Todo: Handle hash of standalone MEMMod objects for non-managed textures
                        // SetWebsitePanelVisibility(m); // Show website link
                    }
                }
                else if (SelectedModInGroup is BatchGameRestore bgr)
                {
                    ModDescriptionText = bgr.UIDescription;
                }
                else
                {
                    ModDescriptionText = @"This batch mod type is not yet implemented"; // This doesn't need localized right now
                }
            }
        }

        // ISizeAdjustbale Interface
        public override double MaxWindowWidthPercent { get; set; } = 0.85;
        public override double MaxWindowHeightPercent { get; set; } = 0.85;

        private void RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (SelectedUnavailableModLink == null) return;
            var baseUrl = SelectedUnavailableModLink;
            if (NexusModsUtilities.HasAPIKey)
            {
                baseUrl += @"&nmm=1";
            }
            M3Utilities.OpenWebpage(baseUrl);
        }


        private bool RefreshContentsOnVisible;

        /// <summary>
        /// Indicates the panel should have contents updated on display
        /// </summary>
        public void RefreshContentsOnDisplay()
        {
            RefreshContentsOnVisible = true;
        }

        private void DownloadMod_Click(object sender, MouseButtonEventArgs e)
        {
            if (SelectedUnavailableModLink != Mod.DefaultWebsite)
            {
                M3Utilities.OpenWebpage(SelectedUnavailableModLink);
            }
        }
    }
}
