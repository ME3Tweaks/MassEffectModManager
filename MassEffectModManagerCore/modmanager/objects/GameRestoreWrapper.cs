﻿using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Services.Restore;
using ME3TweaksCore.Services.Shared.BasegameFileIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.AppCenter.Crashes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// M3 wrapper class that handles the UI flow of a game restore
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class GameRestoreWrapper
    {
        /// <summary>
        /// Window to center dialogs onto
        /// </summary>
        private MainWindow window;

        /// <summary>
        /// The main restore controller that handles the actual restore operation.
        /// </summary>
        public GameRestore RestoreController { get; init; }

        /// <summary>
        /// The status object for the backup
        /// </summary>
        public GameBackupStatus BackupStatus { get; init; }

        /// <summary>
        /// The list of targets in the dropdown.
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> AvailableRestoreTargets { get; } = new();

        /// <summary>
        /// The current selected target in the dropdown
        /// </summary>
        public GameTargetWPF RestoreTarget { get; set; }

        /// <summary>
        /// User displayable name of the game.
        /// </summary>
        public string GameTitle => RestoreController.Game.ToGameName();

        /// <summary>
        /// If the progress bar shown should be indeterminate
        /// </summary>
        public bool ProgressIndeterminate { get; private set; }

        /// <summary>
        /// If restoring the game will restore Texture LOD settings
        /// </summary>
        [AlsoNotifyFor(nameof(RestoreTarget))]
        public bool WillRestoreTextureLODs
        {
            get
            {
                if (RestoreTarget == null || RestoreTarget.Game.IsLEGame() || RestoreTarget.Game == MEGame.LELauncher) return false;
                return RestoreTarget.TextureModded;
            }
        }

        /// <summary>
        /// If the user can select anything in the dropdown
        /// </summary>
        public bool CanOpenDropdown => RestoreController != null && !RestoreController.RestoreInProgress && BackupStatus.BackedUp;

        public string RestoreButtonText
        {
            get
            {
                if (RestoreTarget != null && BackupStatus.BackedUp) return M3L.GetString(M3L.string_restoreThisTarget);
                if (RestoreTarget == null && BackupStatus.BackedUp) return M3L.GetString(M3L.string_selectTarget);
                if (!BackupStatus.BackedUp) return M3L.GetString(M3L.string_noBackup);
                return M3L.GetString(M3L.string_error);
            }
        }

        public GenericCommand RestoreButtonCommand { get; init; }

        private object syncObj = new object();

        public GameRestoreWrapper(MEGame game, IEnumerable<GameTargetWPF> availableTargets, MainWindow window, Action restoreCompletedCallback = null)
        {
            this.window = window;
            RestoreCompletedCallback = restoreCompletedCallback;
            RestoreButtonCommand = new GenericCommand(BeginRestore, () => RestoreTarget != null && RestoreController != null && !RestoreController.RestoreInProgress);
            BackupStatus = BackupService.GetBackupStatus(game);
            RestoreController = new GameRestore(game)
            {
                SetProgressIndeterminateCallback = (indeterminate) => ProgressIndeterminate = indeterminate,
                BlockingErrorCallback = (message, title) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        M3L.ShowDialog(window, title, message, MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                },
                ConfirmationCallback = (message, title) =>
                {
                    bool response = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        response = M3L.ShowDialog(window, title, message, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
                        //lock (syncObj)
                        //{
                        //    Monitor.Pulse(syncObj);
                        //}
                    });
                    //lock (syncObj)
                    //{
                    //    Monitor.Wait(syncObj);
                    //}

                    return response;
                },
                SelectDestinationDirectoryCallback = (title, message) =>
                {
                    string selectedPath = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Not sure if this has to be synced
                        CommonOpenFileDialog ofd = new CommonOpenFileDialog()
                        {
                            Title = M3L.GetString(M3L.string_selectNewRestoreDestination),
                            IsFolderPicker = true,
                            EnsurePathExists = true
                        };
                        if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            selectedPath = ofd.FileName;
                        }
                    });
                    return selectedPath;
                },
                RestoreErrorCallback = (title, message) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        M3L.ShowDialog(window, title, message, MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                },
                GetRestoreEverythingString = (promptGame =>
                {
                    // Specific text for 'Manage Target' is M3 only
                    if (promptGame.IsOTGame()) return M3L.GetString(M3L.string_entireGameDirectoryWillBeDeletedOT);
                    return M3L.GetString(M3L.string_entireGameDirectoryWillBeDeletedLE);
                }),
                UseOptimizedTextureRestore = () => Settings.UseOptimizedTextureRestore,
                ShouldLogEveryCopiedFile = () => Settings.LogBackupAndRestore,
            };
            AvailableRestoreTargets.AddRange(availableTargets);
            RestoreTarget = AvailableRestoreTargets.FirstOrDefault();

            AvailableRestoreTargets.Add(new GameTargetWPF(game, M3L.GetString(M3L.string_restoreToCustomLocation), false, true));
            //RestoreTarget = AvailableRestoreTargets.FirstOrDefault(); // Leave it so it's blank default otherwise we get the 'Restoring from backup will reset LODs' thing.
        }

        /// <summary>
        /// Delegate to invoke when a restore operation has completed
        /// </summary>
        public Action RestoreCompletedCallback { get; set; }

        private void BeginRestore()
        {
            Task.Run(() =>
            {
                RestoreController.PerformRestore(RestoreTarget, RestoreTarget.IsCustomOption ? null : RestoreTarget.TargetPath);
            }).ContinueWith(x =>
            {
                if (x.Exception != null)
                {
                    M3Log.Exception(x.Exception, @"Error restoring game:");
                    Crashes.TrackError(x.Exception, new Dictionary<string, string>()
                    {
                        {@"CustomOption", RestoreTarget.IsCustomOption.ToString()},
                        {@"TargetPath", RestoreTarget.TargetPath},
                    });
                    // There was an error
                    RestoreTarget.StripCmmVanilla(); // do this to ensure target can still attempt to load.
                    RestoreController.SetRestoreInProgress(false);
                    BackupService.RefreshBackupStatus(game: RestoreTarget.Game);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_failedToRestoreGameDueToErrorX, x.Exception.Message),
                            M3L.GetString(M3L.string_fullGameRestore), MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                else
                {
                    // restore completed
                    if (AvailableRestoreTargets.Count(x => !x.IsCustomOption) == 1 && RestoreTarget != null)
                    {
                        // 04/16/2023: If we have only one target for this game,
                        // delete the basegame file database for this specific game
                        // so that as new mods are installed we generate new entries
                        // and stale ones are purged.

                        BasegameFileIdentificationService.PurgeEntriesForGame(RestoreTarget.Game);
                        foreach (var f in M3LoadedMods.GetModsForGame(RestoreTarget.Game))
                        {
                            f.IsInstalledToTarget = false;
                        }
                    }

                    RestoreCompletedCallback?.Invoke();
                }
            });
        }
    }
}
