﻿using System.Windows;
using ME3TweaksCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using static ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;

namespace ME3TweaksModManager.modmanager.loaders
{
    /// <summary>
    /// Class that contains logic for checking for updates to mods.
    /// </summary>
    public class ModUpdater
    {
        public MainWindow mainWindow { get; set; }

        public static ModUpdater Instance { get; private set; }
        private ModUpdater() { }
        internal void CheckAllModsForUpdates()
        {
            var updatableMods = M3LoadedMods.Instance.AllLoadedMods.Where(x => x.IsUpdatable).ToList();
            if (updatableMods.Count > 0)
            {
                CheckModsForUpdates(updatableMods);
            }
        }

        internal void CheckNonWhitelistedNexusModsForUpdates()
        {
            var nonWhiteListed = M3LoadedMods.Instance.AllLoadedMods.Where(x => x.NexusModID > 0 && !x.IsUpdatable && x.NexusUpdateCheck).ToList();
            if (nonWhiteListed.Count > 0)
            {
                var updates = CheckForModUpdatesAgainstNexusAPI(nonWhiteListed).Distinct().ToList();
                if (updates.Any())
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var modUpdatesNotificationDialog = new ModUpdateInformationPanel(updates);
                        modUpdatesNotificationDialog.Close += (sender, args) =>
                        {
                            if (!mainWindow.HasQueuedPanel())
                            {
                                // No more batch panels so we should handle the result on Release
                                mainWindow.HandleBatchPanelResult = true;
                            }
                            mainWindow.ReleaseBusyControl();
                        };
                        mainWindow.ShowBusyControl(modUpdatesNotificationDialog);
                    });
                }
            }
        }

        internal void CheckModsForUpdates(List<Mod> updatableMods, bool restoreMode = false)
        {
            M3Log.Information($@"Checking {updatableMods.Count} eligible mods for updates.");
            if (Settings.LogModUpdater)
            {
                foreach (var m in updatableMods)
                {
                    M3Log.Information($@" >> Checking for updates to {m.Game} {m.ModName} {m.ParsedModVersion}");
                }
            }

            foreach (var m in updatableMods)
            {
                m.IsCheckingForUpdates = true;
            }

            BackgroundTask bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ModCheckForUpdates", M3L.GetString(M3L.string_checkingModsForUpdates), M3L.GetString(M3L.string_modUpdateCheckCompleted));
            void updateCheckProgressCallback(string newStr)
            {
                BackgroundTaskEngine.SubmitBackgroundTaskUpdate(bgTask, newStr);
            }

            var updateManifestModInfos = M3OnlineContent.CheckForModUpdates(updatableMods, restoreMode, updateCheckProgressCallback);
            if (updateManifestModInfos != null)
            {
                //Calculate CLASSIC Updates
                var updates = updateManifestModInfos.Where(x => x.updatecode > 0 && (x.applicableUpdates.Count > 0 || x.filesToDelete.Count > 0)).ToList();
                foreach (var v in updates)
                {
                    M3Log.Information($@"Classic mod out of date: {v.mod.ModName} {v.mod.ParsedModVersion}, server version: {v.LocalizedServerVersionString}");
                }

                //Calculate MODMAKER Updates
                foreach (var mm in updatableMods.Where(x => x.ModModMakerID > 0))
                {
                    var matchingServerMod = updateManifestModInfos.FirstOrDefault(x => x is M3OnlineContent.ModMakerModUpdateInfo mmui && mmui.ModMakerId == mm.ModModMakerID);
                    if (matchingServerMod != null)
                    {
                        var serverVer = Version.Parse(matchingServerMod.versionstr + @".0"); //can't have single digit version
                        if (ProperVersion.IsGreaterThan(serverVer, mm.ParsedModVersion) || restoreMode)
                        {
                            if (!restoreMode)
                            {
                                M3Log.Information($@"ModMaker mod out of date: {mm.ModName} {mm.ParsedModVersion}, server version: {serverVer}");
                            }
                            else
                            {
                                M3Log.Information($@"Restore mode: Show ModMaker mod {mm.ModName} as out of date. Server version: {serverVer}");
                            }
                            matchingServerMod.mod = mm;
                            updates.Add(matchingServerMod);
                            matchingServerMod.SetLocalizedInfo();
                        }
                    }
                }

                //Calculate NEXUSMOD Updates
                foreach (var mm in updatableMods.Where(x => x.NexusModID > 0 && x.ModClassicUpdateCode == 0)) //check zero as Mgamerz's mods will list me3tweaks with a nexus code still for integrations
                {
                    var matchingUpdateInfoForMod = updateManifestModInfos.OfType<M3OnlineContent.NexusModUpdateInfo>().FirstOrDefault(x => x.NexusModsId == mm.NexusModID
                                                                                                                                   && M3Utilities.GetGameFromNumber(x.GameId) == mm.Game
                                                                                                                                   && updates.All(y => !y.mod.Equals(x.mod)));
                    if (matchingUpdateInfoForMod != null)
                    {
                        if (Version.TryParse(matchingUpdateInfoForMod.versionstr, out var serverVer))
                        {
                            if (ProperVersion.IsGreaterThan(serverVer, mm.ParsedModVersion) || restoreMode)
                            {
                                // We need to make a clone in the event a mod uses duplicate code, such as Project Variety
                                M3OnlineContent.NexusModUpdateInfo clonedInfo = new M3OnlineContent.NexusModUpdateInfo(matchingUpdateInfoForMod) { mod = mm, IsRestoreMode = restoreMode };
                                updates.Add(clonedInfo);
                                clonedInfo.SetLocalizedInfo();
                                M3Log.Information($@"NexusMods mod out of date: {mm.ModName} {mm.ParsedModVersion}, server version: {serverVer}");

                            }
                        }
                        else
                        {
                            M3Log.Error($@"Cannot parse nexusmods version of mod, skipping update check for {mm.ModName}. Server version string is {matchingUpdateInfoForMod.versionstr}");
                        }
                    }
                }

                updates = updates.Distinct().ToList();
                if (updates.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var modUpdatesNotificationDialog = new ModUpdateInformationPanel(updates);
                        modUpdatesNotificationDialog.Close += (sender, args) =>
                        {
                            if (!mainWindow.HasQueuedPanel())
                            {
                                // No more batch panels so we should handle the result on Release
                                mainWindow.HandleBatchPanelResult = true;
                            }
                            mainWindow.ReleaseBusyControl();
                        };
                        mainWindow.ShowBusyControl(modUpdatesNotificationDialog);
                    });
                }
            }
            else
            {
                bgTask.FinishedUIText = M3L.GetString(M3L.string_errorCheckingForModUpdates);
            }
            foreach (var m in updatableMods)
            {
                m.IsCheckingForUpdates = false;
            }
            BackgroundTaskEngine.SubmitJobCompletion(bgTask);
        }

        public static List<ModUpdateInfo> CheckForModUpdatesAgainstNexusAPI(List<Mod> updatableMods)
        {
            BackgroundTask bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"NexusModCheckForUpdates", M3L.GetString(M3L.string_checkingModsForUpdates), M3L.GetString(M3L.string_modUpdateCheckCompleted));
            void updateCheckProgressCallback(string newStr)
            {
                BackgroundTaskEngine.SubmitBackgroundTaskUpdate(bgTask, newStr);
            }

            M3Log.Information($@"Checking {updatableMods.Count} non-whitelisted mods for updates.");
            if (Settings.LogModUpdater)
            {
                foreach (var m in updatableMods)
                {
                    M3Log.Information($@" >> Checking for updates to {m.Game} {m.ModName} {m.ParsedModVersion}");
                }
            }

            foreach (var m in updatableMods)
            {
                m.IsCheckingForUpdates = true;
            }

            var mui = new List<ModUpdateInfo>();
            var client = NexusModsUtilities.GetClient();
            foreach (var m in updatableMods)
            {
                BackgroundTaskEngine.SubmitBackgroundTaskUpdate(bgTask, M3L.GetString(M3L.string_interp_checkingForModUpdatesX, m.ModName));
                var updateInfo = NexusModsUtilities.GetLatestVersion(m);
                if (updateInfo != null)
                {
                    mui.Add(updateInfo);
                }
                m.IsCheckingForUpdates = false;
            }

            BackgroundTaskEngine.SubmitJobCompletion(bgTask);
            return mui;
        }

        public static void InitializeModUpdater(MainWindow mainWindow)
        {
            Instance = new ModUpdater() { mainWindow = mainWindow };
        }
    }
}
