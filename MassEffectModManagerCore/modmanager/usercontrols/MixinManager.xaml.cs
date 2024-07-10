﻿using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IniParser.Model;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MixinManager.xaml
    /// </summary>
    public partial class MixinManager : MMBusyPanelBase
    {
        public ObservableCollectionExtended<Mixin> AvailableOfficialMixins { get; set; } = new ObservableCollectionExtended<Mixin>();
        public ObservableCollectionExtended<GameTargetWPF> AvailableInstallTargets { get; set; } = new ObservableCollectionExtended<GameTargetWPF>();
        public GameTargetWPF SelectedInstallTarget { get; set; }
        public Mixin SelectedMixin { get; set; }
        public bool OperationInProgress { get; set; }

        public long ProgressBarValue { get; set; }
        public long ProgressBarMax { get; set; } = 100; //default
        public string BottomLeftMessage { get; set; } = M3L.GetString(M3L.string_selectMixinsToCompile);
        public string NewModName { get; set; }
        public bool AtLeastOneMixinSelected => AvailableOfficialMixins.Any(x => x.UISelectedForUse);

        public MixinManager()
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mixin Library Panel", this);
            MixinHandler.LoadME3TweaksPackage();
            AvailableOfficialMixins.ReplaceAll(MixinHandler.ME3TweaksPackageMixins.OrderBy(x => x.PatchName));

            var backupPath = BackupService.GetGameBackupPath(MEGame.ME3);
            if (backupPath != null)
            {
                var dlcPath = MEDirectories.GetDLCPath(MEGame.ME3, backupPath);
                var headerTranslation = ModJob.GetHeadersToDLCNamesMap(MEGame.ME3);
                foreach (var mixin in AvailableOfficialMixins)
                {
                    mixin.UIStatusChanging += MixinUIStatusChanging;
                    if (mixin.TargetModule == ModJob.JobHeader.TESTPATCH)
                    {
                        if (File.Exists(ME3Directory.GetTestPatchSFARPath(backupPath)))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else if (mixin.TargetModule != ModJob.JobHeader.BASEGAME)
                    {
                        //DLC
                        var resolvedPath = Path.Combine(dlcPath, headerTranslation[mixin.TargetModule]);
                        if (Directory.Exists(resolvedPath))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else
                    {
                        //BASEGAME
                        mixin.CanBeUsed = true;
                    }
                }
            }
            else
            {
                BottomLeftMessage = M3L.GetString(M3L.string_noGameBackupOfME3IsAvailableMixinsCannotBeUsedWithoutABackup);
            }

            ResetMixinsUIState();
            LoadCommands();
        }

        private void MixinUIStatusChanging(object sender, EventArgs e)
        {
            TriggerPropertyChangedFor(nameof(AtLeastOneMixinSelected));
        }

        private void ResetMixinsUIState()
        {
            foreach (var m in AvailableOfficialMixins)
            {
                m.UISelectedForUse = false; //DEBUG ONLY
            }
        }

        public ICommand CloseCommand { get; set; }
        public ICommand ToggleSelectedMixinCommand { get; set; }
        public ICommand CompileAsNewModCommand { get; set; }
        public ICommand InstallIntoGameCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            ToggleSelectedMixinCommand = new GenericCommand(ToggleSelectedMixin, MixinIsSelected);
            CompileAsNewModCommand = new GenericCommand(CompileAsNewMod, CanCompileAsNewMod);
            InstallIntoGameCommand = new GenericCommand(CompileIntoGame, CanInstallIntoGame);
        }

        private bool CanInstallIntoGame() => SelectedInstallTarget != null && !SelectedInstallTarget.TextureModded;

        private bool CanCompileAsNewMod()
        {
            if (OperationInProgress || !AtLeastOneMixinSelected) return false;
            if (string.IsNullOrWhiteSpace(NewModName) || string.IsNullOrWhiteSpace(MUtilities.SanitizePath(NewModName))) return false;
            return true;
        }

        private void CompileAsNewMod()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"MixinManager CompileAsNewModThread");
            List<string> failedApplications = new List<string>();
            var modname = NewModName;
            var modpath = Path.Combine(M3LoadedMods.GetME3ModsDirectory(), MUtilities.SanitizePath(modname));
            if (Directory.Exists(modpath))
            {
                var result = M3L.ShowDialog(mainwindow,
                    M3L.GetString(M3L.string_interp_dialogCreatingNewModWithExistingName, NewModName, modpath),
                    M3L.GetString(M3L.string_modAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (result == MessageBoxResult.No)
                {
                    M3Log.Information(@"User has aborted mixin compilation due to same-named mod existing");
                    return; //abort.
                }
            }



            nbw.DoWork += (a, b) =>
            {
                BottomLeftMessage = M3L.GetString(M3L.string_compilingMixins);
                OperationInProgress = true;
                //DEBUG STUFF
#if DEBUG
                int numCoresToApplyWith = 1;
#else
                var numCoresToApplyWith = Environment.ProcessorCount;
                if (numCoresToApplyWith > 4) numCoresToApplyWith = 4; //no more than 4 as this uses a lot of memory
#endif

                var mixins = AvailableOfficialMixins.Where(x => x.UISelectedForUse).ToList();
                MixinHandler.LoadPatchDataForMixins(mixins); //before dynamic
                void failedApplicationCallback(string str)
                {
                    failedApplications.Add(str);
                }
                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(mixins, failedApplicationCallback);
                if (Enumerable.Any(failedApplications))
                {
                    //Error building list
                    modpath = null;
                    M3Log.Information(@"Aborting mixin compiling due to incompatible selection of mixins");
                    return;
                }

                if (Directory.Exists(modpath))
                {
                    MUtilities.DeleteFilesAndFoldersRecursively(modpath);
                }

                ProgressBarMax = mixins.Count();
                ProgressBarValue = 0;
                int numdone = 0;

                void completedSingleApplicationCallback()
                {
                    var val = Interlocked.Increment(ref numdone);
                    ProgressBarValue = val;
                }


                //Mixins are ready to be applied
                Parallel.ForEach(compilingListsPerModule,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount > numCoresToApplyWith
                        ? numCoresToApplyWith
                        : Environment.ProcessorCount
                }, mapping =>
                {
                    ApplyMixinsToModule(mapping, modpath, completedSingleApplicationCallback, failedApplicationCallback);
                });

                MixinHandler.FreeME3TweaksPatchData();

                //Generate moddesc
                IniData ini = new IniData();
                ini[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME] = @"ME3";
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME] = modname;
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER] = App.AppVersionHR;
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION] = M3L.GetString(M3L.string_compiledFromTheFollowingMixins);
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION] = @"1.0";

                generateRepaceFilesMapping(ini, modpath);
                File.WriteAllText(Path.Combine(modpath, @"moddesc.ini"), ini.ToString());

            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OperationInProgress = false;
                ClearMixinHandler();
                if (failedApplications.Count > 0)
                {
                    var ld = new ListDialog(failedApplications, M3L.GetString(M3L.string_failedToApplyAllMixins), M3L.GetString(M3L.string_theFollowingMixinsFailedToApply), mainwindow);
                    ld.ShowDialog();
                }

                if (modpath != null)
                {
                    OnClosing(new DataEventArgs(modpath));
                }
                else
                {
                    BottomLeftMessage = M3L.GetString(M3L.string_selectMixinsToCompile);
                }
            };
            CompilePanelButton.IsOpen = false;
            nbw.RunWorkerAsync();
        }

        public static void ApplyMixinsToModule(KeyValuePair<ModJob.JobHeader, Dictionary<string, List<Mixin>>> mapping, string modpath, Action completedSingleApplicationCallback, Action<string> failedApplicationCallback)
        {
            var dlcFolderName = ModMakerCompiler.ModmakerChunkNameToDLCFoldername(mapping.Key.ToString());
            var outdir = Path.Combine(modpath, ModMakerCompiler.HeaderToDefaultFoldername(mapping.Key), @"CookedPCConsole");
            Directory.CreateDirectory(outdir);
            if (mapping.Key == ModJob.JobHeader.BASEGAME)
            {
                //basegame
                foreach (var file in mapping.Value)
                {
                    try
                    {
                        using var packageAsStream =
                            VanillaDatabaseService.FetchBasegameFile(MEGame.ME3,
                                Path.GetFileName(file.Key));
                        //packageAsStream.WriteToFile(@"C:\users\dev\desktop\compressed.pcc");
                        using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream, false, true);
                        using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, true, completedSingleApplicationCallback, failedApplicationCallback);
                        M3Log.Information(@"Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                        finalStream.Position = 0;
                        var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                        var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                        package.Save(outfile, false, includeAdditionalPackagesToCook: false, includeDependencyTable: true); // don't compress, use mixin saving rules for basegame files
                    }
                    catch (Exception e)
                    {
                        var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                        M3Log.Error($@"Error in mixin application for file {file.Key}: {e.Message}");
                        failedApplicationCallback(M3L.GetString(M3L.string_interp_errorApplyingMixinsForFile, mixinsStr, file.Key, e.Message));
                    }
                }
            }
            else
            {
                //dlc
                var dlcPackage = VanillaDatabaseService.FetchVanillaSFAR(dlcFolderName); //do not have to open file multiple times.
                foreach (var file in mapping.Value)
                {
                    try
                    {
                        using var packageAsStream = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, file.Key, forcedDLC: dlcPackage);
                        //as file comes from backup, we don't need to decompress it, it will always be decompressed in sfar
                        using var finalStream = MixinHandler.ApplyMixins(packageAsStream, file.Value, true, completedSingleApplicationCallback, failedApplicationCallback);

                        var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));

                        if (mapping.Key != ModJob.JobHeader.TESTPATCH)
                        {
                            // TestPatch is never unpacked. So there is not really point to 
                            // compressing it's rather small files. The other DLC jobs likely will be packed still, but this will save some disk space.

                            M3Log.Information($@"Compressing package to mod directory: {outfile}", Settings.LogModMakerCompiler);
                            finalStream.Position = 0;
                            var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                            package.Save(outfile, true);
                        }
                        else
                        {
                            M3Log.Information($@"Writing patched file to disk: {outfile}");
                            finalStream.WriteToFile(outfile);
                        }
                    }
                    catch (Exception e)
                    {
                        var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                        M3Log.Error($@"Error in mixin application for file {file.Key}: {e.Message}");
                        failedApplicationCallback(M3L.GetString(M3L.string_interp_errorApplyingMixinsForFile, mixinsStr, file.Key, e.Message));
                    }

                    //finalStream.WriteToFile(outfile);
                }
            }
        }

        private void CompileIntoGame()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"MixinManager CompileIntoGameThread");
            List<string> failedApplications = new List<string>();

            nbw.DoWork += (a, b) =>
            {
                BottomLeftMessage = M3L.GetString(M3L.string_compilingMixins);
                OperationInProgress = true;
                //DEBUG STUFF
#if DEBUG
                int numCoresToApplyWith = 1;
#else
                var numCoresToApplyWith = Environment.ProcessorCount;
                if (numCoresToApplyWith > 4) numCoresToApplyWith = 4; //no more than 4 as this uses a lot of memory
#endif

                var mixins = AvailableOfficialMixins.Where(x => x.UISelectedForUse).ToList();
                MixinHandler.LoadPatchDataForMixins(mixins); //before dynamic
                void failedApplicationCallback(string str)
                {
                    failedApplications.Add(str);
                }
                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(mixins, failedApplicationCallback);
                if (Enumerable.Any(failedApplications))
                {
                    //Error building list
                    M3Log.Information(@"Aborting mixin install due to incompatible selection of mixins");
                    return;
                }

                ProgressBarMax = mixins.Count();
                ProgressBarValue = 0;
                int numdone = 0;

                void completedSingleApplicationCallback()
                {
                    var val = Interlocked.Increment(ref numdone);
                    ProgressBarValue = val;
                }


                //Mixins are ready to be applied
                Parallel.ForEach(compilingListsPerModule,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount > numCoresToApplyWith
                    ? numCoresToApplyWith
                    : Environment.ProcessorCount
            }, mapping =>
            {
                var dlcFolderName = ModMakerCompiler.ModmakerChunkNameToDLCFoldername(mapping.Key.ToString());
                //var outdir = Path.Combine(modpath, ModMakerCompiler.HeaderToDefaultFoldername(mapping.Key), @"CookedPCConsole");
                //Directory.CreateDirectory(outdir);
                if (mapping.Key == ModJob.JobHeader.BASEGAME)
                {
                    //basegame
                    foreach (var file in mapping.Value)
                    {
                        try
                        {
                            using var vanillaPackageAsStream = VanillaDatabaseService.FetchBasegameFile(MEGame.ME3, Path.GetFileName(file.Key));
                            //packageAsStream.WriteToFile(@"C:\users\dev\desktop\compressed.pcc");
                            using var decompressedStream = MEPackage.GetDecompressedPackageStream(vanillaPackageAsStream, false, true);
                            decompressedStream.Position = 0;
                            var vanillaPackage = MEPackageHandler.OpenMEPackageFromStream(decompressedStream, $@"Vanilla - {Path.GetFileName(file.Key)}");
                            //decompressedStream.WriteToFile(@"C:\users\dev\desktop\decompressed.pcc");

                            using var mixinModifiedStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, true,
                                completedSingleApplicationCallback, failedApplicationCallback);
                            mixinModifiedStream.Position = 0;
                            var modifiedPackage = MEPackageHandler.OpenMEPackageFromStream(mixinModifiedStream, $@"Mixin Modified - {Path.GetFileName(file.Key)}");

                            // three way merge: get target stream
                            var targetFile = Path.Combine(M3Directories.GetCookedPath(SelectedInstallTarget), Path.GetFileName(file.Key));
                            var targetPackage = MEPackageHandler.OpenMEPackage(targetFile);

                            var merged = ThreeWayPackageMerge.AttemptMerge(vanillaPackage, modifiedPackage, targetPackage);
                            if (merged)
                            {
                                targetPackage.Save(compress: true);
                                M3Log.Information(@"Three way merge succeeded for " + targetFile);
                            }
                            else
                            {
                                M3Log.Error(@"Could not merge three way merge into " + targetFile);
                            }
                            //var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                            //package.save(outfile, false); // don't compress
                            //finalStream.WriteToFile(outfile);
                            //File.WriteAllBytes(outfile, finalStream.ToArray());
                        }
                        catch (Exception e)
                        {
                            var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                            M3Log.Error($@"Error in mixin application for file {file.Key}: {e.Message}");
                            failedApplicationCallback(M3L.GetString(M3L.string_interp_errorApplyingMixinsForFile, mixinsStr, file.Key, e.Message));
                        }
                    }
                }
                else
                {
                    //dlc
                    var dlcPackage = VanillaDatabaseService.FetchVanillaSFAR(dlcFolderName); //do not have to open file multiple times.
                    var targetCookedPCDir = Path.Combine(M3Directories.GetDLCPath(SelectedInstallTarget), dlcFolderName, @"CookedPCConsole");
                    var sfar = mapping.Key == ModJob.JobHeader.TESTPATCH ? M3Directories.GetTestPatchSFARPath(SelectedInstallTarget) : Path.Combine(targetCookedPCDir, @"Default.sfar");
                    bool unpacked = new FileInfo(sfar).Length == 32;
                    DLCPackage targetDLCPackage = unpacked ? null : new DLCPackage(sfar); //cache SFAR target

                    foreach (var file in mapping.Value)
                    {
                        try
                        {
                            using var vanillaPackageAsStream = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, file.Key, forcedDLC: dlcPackage);
                            using var decompressedStream = MEPackage.GetDecompressedPackageStream(vanillaPackageAsStream);
                            decompressedStream.Position = 0;
                            var vanillaPackage = MEPackageHandler.OpenMEPackageFromStream(decompressedStream, $@"VanillaDLC - {Path.GetFileName(file.Key)}");
                            using var mixinModifiedStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, true, completedSingleApplicationCallback, failedApplicationCallback);
                            mixinModifiedStream.Position = 0;
                            var modifiedPackage = MEPackageHandler.OpenMEPackageFromStream(mixinModifiedStream, $@"Mixin Modified - {Path.GetFileName(file.Key)}");

                            // three way merge: get target stream
                            // must see if DLC is unpacked first

                            MemoryStream targetFileStream = null;

                            //Packed
                            if (unpacked)
                            {
                                targetFileStream = new MemoryStream(File.ReadAllBytes(Path.Combine(targetCookedPCDir, file.Key)));
                            }
                            else
                            {
                                targetFileStream = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, Path.GetFileName(file.Key), forcedDLC: targetDLCPackage);
                            }

                            var targetPackage = MEPackageHandler.OpenMEPackageFromStream(targetFileStream, $@"Target package {dlcFolderName} - {file.Key}, from SFAR: {unpacked}");

                            var merged = ThreeWayPackageMerge.AttemptMerge(vanillaPackage, modifiedPackage, targetPackage);
                            if (merged)
                            {
                                if (unpacked)
                                {
                                    targetPackage.Save(Path.Combine(targetCookedPCDir, file.Key));
                                    M3Log.Information(@"Three way merge succeeded for " + targetPackage.FilePath);
                                }
                                else
                                {
                                    var finalSTream = targetPackage.SaveToStream(false); // No compress. Not sure if we should support doing that though.
                                    targetDLCPackage.ReplaceEntry(finalSTream.ToArray(), targetDLCPackage.FindFileEntry(Path.GetFileName(file.Key)));
                                    M3Log.Information(@"Three way merge succeeded for " + targetPackage.FilePath);

                                }
                            }
                            else
                            {
                                M3Log.Error(@"Could not three way merge into: " + targetFileStream);
                            }
                        }
                        catch (Exception e)
                        {
                            var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                            M3Log.Error($@"Error in mixin application for file {file.Key}: {e.Message}");
                            failedApplicationCallback(M3L.GetString(M3L.string_interp_errorApplyingMixinsForFile, mixinsStr, file.Key, e.Message));
                        }

                        //finalStream.WriteToFile(outfile);
                    }
                }
            });

                MixinHandler.FreeME3TweaksPatchData();
                var percent = 0; //this is used to save a localization
                BottomLeftMessage = M3L.GetString(M3L.string_interp_runningAutoTOCOnGamePercentX, percent);

                //Run autotoc
                void tocingUpdate(int percent)
                {
                    BottomLeftMessage = M3L.GetString(M3L.string_interp_runningAutoTOCOnGamePercentX, percent);
                }
                AutoTOC.RunTOCOnGameTarget(SelectedInstallTarget, tocingUpdate);

                //Generate moddesc
                //IniData ini = new IniData();
                //ini[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
                //ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME] = @"ME3";
                //ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME] = modname;
                //ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER] = App.AppVersionHR;
                //ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION] = M3L.GetString(M3L.string_compiledFromTheFollowingMixins);
                //ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION] = @"1.0";

                //generateRepaceFilesMapping(ini, modpath);
                //File.WriteAllText(Path.Combine(modpath, @"moddesc.ini"), ini.ToString());

            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OperationInProgress = false;
                ClearMixinHandler();
                if (failedApplications.Count > 0)
                {
                    var ld = new ListDialog(failedApplications, M3L.GetString(M3L.string_failedToApplyAllMixins), M3L.GetString(M3L.string_theFollowingMixinsFailedToApply), mainwindow);
                    ld.ShowDialog();
                }

                /*if (modpath != null)
                {
                    OnClosing(new DataEventArgs(modpath));
                }
                else
                {*/
                BottomLeftMessage = M3L.GetString(M3L.string_mixinsInstalledMaybe);
                //}
            };
            CompilePanelButton.IsOpen = false;
            nbw.RunWorkerAsync();
        }

        private void generateRepaceFilesMapping(IniData ini, string modpath)
        {
            var dirs = Directory.GetDirectories(modpath);
            foreach (var dir in dirs)
            {
                //automap
                var dirname = Path.GetFileName(dir);
                var headername = ModMakerCompiler.DefaultFoldernameToHeader(dirname).ToString();
                ini[headername][Mod.MODDESC_DESCRIPTOR_JOB_DIR] = dirname;
                if (dirname != @"BALANCE_CHANGES")
                {
                    ini[headername][Mod.MODDESC_DESCRIPTOR_JOB_NEWFILES] = @"CookedPCConsole";

                    string inGameDestdir;
                    if (dirname == @"BASEGAME")
                    {
                        inGameDestdir = @"BIOGame/CookedPCConsole";
                    }
                    else
                    {
                        //DLC
                        inGameDestdir = $@"BIOGame/DLC/{ModMakerCompiler.ModmakerChunkNameToDLCFoldername(dirname)}/CookedPCConsole";
                    }

                    ini[headername][Mod.MODDESC_DESCRIPTOR_JOB_REPLACEFILES] = inGameDestdir;
                    ini[headername][Mod.MODDESC_DESCRIPTOR_JOB_GAMEDIRECTORYSTRUCTURE] = Mod.MODDESC_VALUE_TRUE;
                }
                else
                {
                    ini[headername][Mod.MODDESC_DESCRIPTOR_JOB_NEWFILES] = @"ServerCoalesced.bin"; //BALANCE_CHANGES
                }
            }
        }

        private void ToggleSelectedMixin()
        {
            SelectedMixin.UISelectedForUse = !SelectedMixin.UISelectedForUse;
        }

        private bool MixinIsSelected() => SelectedMixin != null;
        private bool CanClosePanel() => true;

        private void ClosePanel()
        {
            ClearMixinHandler();
            OnClosing(DataEventArgs.Empty);
        }

        private void ClearMixinHandler()
        {
            foreach (var mixin in AvailableOfficialMixins)
            {
                mixin.UIStatusChanging -= MixinUIStatusChanging;
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (!OperationInProgress && !AtLeastOneMixinSelected && e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            AvailableInstallTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == MEGame.ME3));
            SelectedInstallTarget = AvailableInstallTargets.FirstOrDefault();
        }

        public void OnSelectedMixinChanged()
        {

        }

        private void ModName_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (CanCompileAsNewMod())
                {
                    CompileAsNewMod();
                }
            }
        }

        //private void MixinList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.AddedItems.Count > 0)
        //    {
        //        SelectedMixin = (Mixin)e.AddedItems[0];
        //    }
        //    else
        //    {
        //        SelectedMixin = null;
        //    }
        //}
    }
}
