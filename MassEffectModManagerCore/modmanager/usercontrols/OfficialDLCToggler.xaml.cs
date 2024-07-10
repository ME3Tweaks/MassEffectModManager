﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OfficialDLCToggler.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OfficialDLCToggler : MMBusyPanelBase
    {
        public OfficialDLCToggler()
        {
            LoadCommands();
        }

        public ObservableCollectionExtended<GameTargetWPF> AvailableTargets { get; } = new();
        public ObservableCollectionExtended<InstalledDLC> InstalledDLCs { get; } = new();
        public GameTargetWPF SelectedTarget { get; set; }
        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClosePanel();
            }
        }

        public void OnSelectedTargetChanged()
        {
            InstalledDLCs.ClearEx();
            if (SelectedTarget != null)
            {
                // maps DLC folder name -> mount number
                var installedDlc = VanillaDatabaseService.GetInstalledOfficialDLC(SelectedTarget, true);
                foreach (var dlc in installedDlc)
                {
                    Debug.WriteLine(dlc);
                    var foldername = dlc.TrimStart('x');
                    InstalledDLCs.Add(new InstalledDLC()
                    {
                        target = SelectedTarget,
                        DLCFolderName = dlc,
                        UIDLCFolderName = foldername,
                        Enabled = !dlc.StartsWith('x'),
                        HumanName = TPMIService.GetThirdPartyModInfo(foldername, SelectedTarget.Game).modname
                    });
                }
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            AvailableTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Selectable && x.Game.IsOTGame()));
            SelectedTarget = AvailableTargets.FirstOrDefault();
        }

        [AddINotifyPropertyChangedInterface]
        public class InstalledDLC
        {
            public GameTargetWPF target { get; set; }
            /// <summary>
            /// Current DLC Folder Name
            /// </summary>
            public string DLCFolderName { get; set; }
            /// <summary>
            /// Doesn't show 'disabled' X, the always enabled one
            /// </summary>
            public string UIDLCFolderName { get; set; }
            public bool Enabled { get; set; }
            public string HumanName { get; set; }
            public string ToggleText => Enabled ? M3L.GetString(M3L.string_toggleOff) : M3L.GetString(M3L.string_toggleOn);

            public ICommand ToggleCommand { get; }
            public InstalledDLC()
            {
                ToggleCommand = new GenericCommand(ToggleDLC);
            }

            private void ToggleDLC()
            {
                try
                {
                    var dlcFPath = M3Directories.GetDLCPath(target);
                    var currentDLCPath = Path.Combine(dlcFPath, DLCFolderName);
                    string destPath = Path.Combine(dlcFPath, Enabled ? @"x" + UIDLCFolderName : UIDLCFolderName);
                    Directory.Move(currentDLCPath, destPath);
                    Enabled = !Enabled;
                    DLCFolderName = Enabled ? UIDLCFolderName : @"x" + UIDLCFolderName;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Error toggling DLC {DLCFolderName}: {e.Message}");
                    M3L.ShowDialog(Application.Current?.MainWindow, M3L.GetString(M3L.string_interp_errorTogglingDLC, e.Message), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error); //this needs updated to be better
                }
            }
        }
    }
}
