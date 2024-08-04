﻿using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.installer;
using ME3TweaksModManager.modmanager.objects.exceptions;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.batch;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Shows the options for installing a mod, which then advances to ModInstaller (if a mod is being installed)
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ModInstallOptionsPanel : MMBusyPanelBase
    {
        public Mod ModBeingInstalled { get; private set; }
        public GameTargetWPF SelectedGameTarget { get; set; }
        public bool CompressInstalledPackages { get; set; }
        public GenericCommand InstallCommand { get; private set; }

        private readonly ReadOnlyOption me1ConfigReadOnlyOption = new ReadOnlyOption();

        /// <summary>
        /// All alternate options to show to the user (groups can have 1 or more items)
        /// </summary>
        public ObservableCollectionExtended<AlternateGroup> AlternateGroups { get; } = new ObservableCollectionExtended<AlternateGroup>();
        /// <summary>
        /// List of available targets that can be installed to
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> InstallationTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();

        /// <summary>
        /// If a target change must occur before you can install the mod (the current target is not valid)
        /// </summary>
        public bool PreventInstallUntilTargetChange { get; set; }

        /// <summary>
        /// If all options that this mod supports configuring are automatic and cannot be changed by the user in this dialog
        /// </summary>
        public bool AllOptionsAreAutomatic { get; private set; }

        /// <summary>
        /// Result flag indicating that installation was canceled (maybe remove for 8.0?)
        /// </summary>
        public bool InstallationCancelled { get; private set; }

        /// <summary>
        /// The associated batch mode mod. If this is not a batch install, this will be null.
        /// </summary>
        public BatchMod BatchMod { get; private set; }

        /// <summary>
        /// The order in which options were chosen by the user when recording options
        /// </summary>
        private List<PlusMinusKey> InOrderRecordedOptions { get; set; } = new();

        public ModInstallOptionsPanel(Mod mod, GameTargetWPF gameTargetWPF, bool? installCompressed, BatchMod batchMod)
        {
            ModBeingInstalled = mod;

            if (!mod.IsInArchive)
            {
                BatchMod = batchMod; // Never allow a compressed batch mod
                foreach (var alt in mod.GetAllAlternates())
                {
                    if (!string.IsNullOrWhiteSpace(alt.ImageAssetName))
                    {
                        alt.LoadImageAsset(mod);
                    }
                }
            }
            LoadCommands();

            if (mod.BannerBitmap == null)
            {
                mod.LoadBannerImage(); // Method will check if it's null
            }
        }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(BeginInstallingMod, CanInstall);
        }

        /// <summary>
        /// Weave-called when SelectedGameTarget changes
        /// </summary>
        /// <param name="oldT"></param>
        /// <param name="newT"></param>
        public void OnSelectedGameTargetChanged(object oldT, object newT)
        {
            Result.SelectedTarget = newT as GameTargetWPF;
            if (oldT != null && newT != null)
            {
                PreventInstallUntilTargetChange = false;
                SetupOptions(false);
            }
        }

        private void SetupOptions(bool initialSetup)
        {
            var canInstall = SharedInstaller.ValidateModCanInstall(window, ModBeingInstalled, SelectedGameTarget);
            if (!canInstall)
            {
                PreventInstallUntilTargetChange = true;
                if (InstallationTargets.Count == 1)
                {
                    // There are no other options
                    OnClosing(DataEventArgs.Empty);
                }

                return;
            }

            // Installation can continue

            AlternateGroups.ClearEx();

            //Write check
            var canWrite = M3Utilities.IsDirectoryWritable(SelectedGameTarget.TargetPath);
            if (!canWrite)
            {
                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogNoWritePermissions), M3L.GetString(M3L.string_cannotWriteToGameDirectory), MessageBoxButton.OK, MessageBoxImage.Warning);
                if (initialSetup)
                {
                    //needs write permissions
                    InstallationCancelled = true;
                    OnClosing(DataEventArgs.Empty);
                }
                else
                {
                    PreventInstallUntilTargetChange = true;
                }
                return;
            }

            if (ModBeingInstalled.Game != MEGame.LELauncher)
            {
                //Detect incompatible DLC
                var dlcMods = SelectedGameTarget.GetInstalledDLCMods();
                if (ModBeingInstalled.IncompatibleDLC.Any())
                {
                    //Check for incompatible DLC.
                    List<string> incompatibleDLC = new List<string>();
                    foreach (var incompat in ModBeingInstalled.IncompatibleDLC)
                    {
                        if (dlcMods.Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                        {
                            var tpmi = TPMIService.GetThirdPartyModInfo(incompat, ModBeingInstalled.Game);
                            if (tpmi != null)
                            {
                                incompatibleDLC.Add($@" - {incompat} ({tpmi.modname})");
                            }
                            else
                            {
                                incompatibleDLC.Add(@" - " + incompat);
                            }
                        }
                    }

                    if (incompatibleDLC.Count > 0)
                    {
                        string message = M3L.GetString(M3L.string_dialogIncompatibleDLCDetectedHeader, ModBeingInstalled.ModName);
                        message += string.Join('\n', incompatibleDLC);
                        message += M3L.GetString(M3L.string_dialogIncompatibleDLCDetectedFooter, ModBeingInstalled.ModName);
                        M3L.ShowDialog(window, message, M3L.GetString(M3L.string_incompatibleDLCDetected), MessageBoxButton.OK, MessageBoxImage.Error);

                        if (initialSetup)
                        {
                            InstallationCancelled = true;
                            OnClosing(DataEventArgs.Empty);
                        }
                        else
                        {
                            PreventInstallUntilTargetChange = true;
                        }

                        return;
                    }
                }

                //Detect outdated DLC
                if (ModBeingInstalled.OutdatedCustomDLC.Count > 0)
                {
                    //Check for incompatible DLC.
                    List<string> outdatedDLC = new List<string>();
                    foreach (var outdatedItem in ModBeingInstalled.OutdatedCustomDLC)
                    {
                        if (dlcMods.Contains(outdatedItem, StringComparer.InvariantCultureIgnoreCase))
                        {
                            var tpmi = TPMIService.GetThirdPartyModInfo(outdatedItem, ModBeingInstalled.Game);
                            if (tpmi != null)
                            {
                                outdatedDLC.Add($@" - {outdatedItem} ({tpmi.modname})");
                            }
                            else
                            {
                                outdatedDLC.Add(@" - " + outdatedItem);
                            }
                        }
                    }

                    if (outdatedDLC.Count > 0)
                    {
                        string message = M3L.GetString(M3L.string_dialogOutdatedDLCHeader, ModBeingInstalled.ModName);
                        message += string.Join('\n', outdatedDLC);
                        message += M3L.GetString(M3L.string_dialogOutdatedDLCFooter, ModBeingInstalled.ModName);
                        InstallationCancelled = true;
                        var result = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_outdatedDLCDetected), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.No)
                        {
                            InstallationCancelled = true;
                            OnClosing(DataEventArgs.Empty);
                            return;
                        }
                    }
                }
            }

            //See if any alternate options are available and display them even if they are all autos
            AllOptionsAreAutomatic = true;
            if (ModBeingInstalled.GetJob(ModJob.JobHeader.ME1_CONFIG) != null)
            {
                me1ConfigReadOnlyOption.UIIsSelected = true;
                AlternateGroups.Add(new AlternateGroup(me1ConfigReadOnlyOption));
                AllOptionsAreAutomatic = false;
            }

            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                // To respect dev ordering we must enumerate in order
                List<AlternateOption> parsedOptions = new List<AlternateOption>();
                // We don't allow optiongroups to cross dlc/files so we have to parse them separate.
                for (int i = 0; i < job.AlternateDLCs.Count; i++)
                {
                    var alt = job.AlternateDLCs[i];
                    if (parsedOptions.Contains(alt))
                        continue;
                    if (alt.GroupName != null)
                    {
                        // Add the group
                        var groupOptions = new List<AlternateOption>(job.AlternateDLCs.Where(x => x.GroupName == alt.GroupName));
                        AlternateGroups.Add(new AlternateGroup(groupOptions)); // Add multimode group
                        parsedOptions.AddRange(groupOptions);
                    }
                    else
                    {
                        // Add only the option
                        AlternateGroups.Add(new AlternateGroup(alt)); // Add single mode group
                        parsedOptions.Add(alt);
                    }
                }

                for (int i = 0; i < job.AlternateFiles.Count; i++)
                {
                    var alt = job.AlternateFiles[i];
                    if (parsedOptions.Contains(alt))
                        continue;
                    if (alt.GroupName != null)
                    {
                        // Add the group
                        var groupOptions = new List<AlternateOption>(job.AlternateFiles.Where(x => x.GroupName == alt.GroupName));
                        AlternateGroups.Add(new AlternateGroup(groupOptions)); // Add multimode group
                        parsedOptions.AddRange(groupOptions);
                    }
                    else
                    {
                        // Add only the option
                        AlternateGroups.Add(new AlternateGroup(alt)); // Add single mode group
                        parsedOptions.Add(alt);
                    }
                }

                //var alternateDLCGroups = job.AlternateDLCs.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();
                //var alternateFileGroups = job.AlternateFiles.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();

                //foreach (var adlcg in alternateDLCGroups)
                //{
                //    AlternateGroups.Add(new AlternateGroup(job.AlternateDLCs.Where(x => x.GroupName == adlcg).OfType<AlternateOption>().ToList()));
                //}

                //foreach (var afileg in alternateFileGroups)
                //{
                //    AlternateGroups.Add(new AlternateGroup(job.AlternateFiles.Where(x => x.GroupName == afileg).OfType<AlternateOption>().ToList()));
                //}

                //// NON GROUP OPTIONS COME NEXT.
                //AlternateGroups.AddRange(job.AlternateDLCs.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
                //AlternateGroups.AddRange(job.AlternateFiles.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
            }

            // Set the initial states
            foreach (AlternateGroup o in AlternateGroups)
            {
                o.SetIsSelectedChangeHandlers(OnAlternateSelectionChanged, OnAlternateOptionChangedByUser);
                internalSetupInitialSelection(o);
            }

            SortOptions();

            int numAttemptsRemaining = 15;
            try
            {
                UpdateOptions(ref numAttemptsRemaining, ModBeingInstalled, SelectedGameTarget, initialSetup: true); // Update for DependsOnKeys.
            }
            catch (CircularDependencyException)
            {
                // uh oh
                M3Log.Warning(@"Circular dependency detected in logic for mod alternates");
            }
            // Done calculating options

            // This has to occur after UpdateOptions, otherwise some states won't be accurately reflected.
            foreach (var o in AlternateGroups)
            {
                if (o.GroupName != null)
                {
#if DEBUG
                    foreach (var v in o.AlternateOptions)
                    {
                        Debug.WriteLine($@"{v.FriendlyName} UIIsSelected: {v.UIIsSelected}");
                    }
                    Debug.WriteLine("");
#endif
                    // Deselect so UI doesn't show selected.
                    foreach (var v in o.AlternateOptions)
                    {
                        if (o.SelectedOption != v)
                        {
                            v.UIIsSelected = false;
                        }
                        else if (v.UIIsSelectable || v.IsAlways)
                        {
                            v.UIIsSelected = true;
                        }
                    }
#if DEBUG
                    foreach (var v in o.AlternateOptions)
                    {
                        Debug.WriteLine($@"{v.FriendlyName} UIIsSelected: {v.UIIsSelected}");
                    }
#endif
                }
            }

            if (AlternateGroups.Count == 0)
            {
                AllOptionsAreAutomatic = false; //Don't show the UI for this
            }

            var targets = mainwindow.InstallationTargets.Where(x => x.Game == ModBeingInstalled.Game).ToList();
            if (ModBeingInstalled.IsInArchive && targets.Count == 1 && AllOptionsAreAutomatic)
            {
                // All available options were chosen already (compression would come from import dialog)
                BeginInstallingMod();
            }
            else if ((targets.Count == 1 || BatchMod != null) && AlternateGroups.Count == 0 && (BatchMod != null || Settings.PreferCompressingPackages || ModBeingInstalled.Game == MEGame.ME1 || ModBeingInstalled.Game.IsLEGame() || ModBeingInstalled.Game == MEGame.LELauncher))
            {
                // ME1 and LE can't compress. If user has elected to compress packages, and there are no alternates/additional targets, just begin installation
                CompressInstalledPackages = Settings.PreferCompressingPackages && ModBeingInstalled.Game > MEGame.ME1;
                AllOptionsAreAutomatic = true;
                BeginInstallingMod();
            }
            else
            {
                // Set the list of targets.
                InstallationTargets.ReplaceAll(targets);
            }
        }

        void internalSetupInitialSelection(AlternateGroup o)
        {
            var metaInfo = SelectedGameTarget.GetMetaMappedInstalledDLC();
            foreach (var option in o.AlternateOptions)
            {
                // Suboptions.
                if (option is AlternateDLC altdlc)
                {
                    altdlc.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled, metaInfo);
                    if (altdlc.IsManual) AllOptionsAreAutomatic = false;
                }
                else if (option is AlternateFile altfile)
                {
                    altfile.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled, metaInfo);
                    if (altfile.IsManual) AllOptionsAreAutomatic = false;
                }
            }
        }

        private void SortOptions()
        {
            // ModDesc 8: Sorting option disable
            if (ModBeingInstalled.SortAlternateOptions)
            {
                var remainingOptionsToSort = new List<AlternateGroup>(AlternateGroups);
                List<AlternateGroup> newOptions = new List<AlternateGroup>();

                // Read only option is always top (ME1 only)
                var readOnly = remainingOptionsToSort.FirstOrDefault(x => x.AlternateOptions.Count == 1 && x.AlternateOptions[0] is ReadOnlyOption);
                if (readOnly != null)
                {
                    newOptions.Add(readOnly);
                    remainingOptionsToSort.Remove(readOnly);
                }

                // Put indexed items at the top in ascending order.
                var indexedOptions = remainingOptionsToSort.Where(x => x.SortIndex > 0);
                newOptions.AddRange(indexedOptions.OrderBy(x => x.SortIndex));
                remainingOptionsToSort = remainingOptionsToSort.Except(indexedOptions).ToList();

                // Put remaining options at the bottom.
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName != null));
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName == null && x.SelectedOption.UIIsSelectable));
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName == null && !x.SelectedOption.UIIsSelectable));

#if DEBUG
                if (newOptions.Count != AlternateGroups.Count)
                    throw new Exception(@"Error sorting options! The results was not the same length as the input.");
#endif

                AlternateGroups.ReplaceAll(newOptions);
            }
        }

        /// <summary>
        /// Records an option being selected by a user through the click handler
        /// </summary>
        /// <param name="newSelectedOption">The option that was changed by the user</param>
        private void OnAlternateOptionChangedByUser(AlternateOption newSelectedOption)
        {
            InOrderRecordedOptions.Add(new PlusMinusKey(newSelectedOption.UIIsSelected, newSelectedOption.OptionKey));
        }

        [SuppressPropertyChangedWarnings]
        private void OnAlternateSelectionChanged(object sender, EventArgs data)
        {
            if (sender is AlternateOption ao && data is DataEventArgs args && args.Data is bool newState)
            {
                var altsToUpdate = findOptionsDependentOn(ao);

                if (altsToUpdate.Any())
                {
                    altsToUpdate.Add(ao); // This is required for lookup
                    // An alternate option was changed by the user.
                    int numRemainingAttempts = 15;
                    try
                    {
                        UpdateOptions(ref numRemainingAttempts, ModBeingInstalled, SelectedGameTarget, altsToUpdate);
                    }
                    catch (CircularDependencyException)
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_circularDependencyDialogMessage), M3L.GetString(M3L.string_circularDependency), MessageBoxButton.OK, MessageBoxImage.Error);
                        InstallationCancelled = true;
                        OnClosing(DataEventArgs.Empty);
                    }
                }
            }
        }

        private void UpdateOptions(ref int numAttemptsRemaining, Mod mod, GameTargetWPF target, List<AlternateOption> optionsToUpdate = null, bool initialSetup = false)
        {
            numAttemptsRemaining--;
            if (numAttemptsRemaining <= 0)
            {
                // Tried too many times. This is probably some circular dependency the dev set
                throw new CircularDependencyException();
            }

            // If none were passed in, we parse all of them.
            optionsToUpdate ??= AlternateGroups.SelectMany(x => x.AlternateOptions).ToList();

            List<AlternateOption> secondPassOptions = new List<AlternateOption>();
            foreach (var v in optionsToUpdate)
            {
                // Add the list of options this one depends on so we can pass them through to the validation function, even if that one is not being updated.
                var allDependentOptions = AlternateGroups.SelectMany(x => x.AlternateOptions).Where(x => v.DependsOnKeys.Any(y => y.Key == x.OptionKey)).Concat(optionsToUpdate).Distinct().ToList();
                var stateChanged = v.UpdateSelectability(allDependentOptions, mod, target);
                if (stateChanged)
                {
                    Debug.WriteLine($@"State changed: {v.FriendlyName} to {v.UIIsSelected}");
                    secondPassOptions.AddRange(findOptionsDependentOn(v));
                    //UpdateOptions(ref numAttemptsRemaining, findOptionsDependentOn(v));
                    //break; // Don't parse it again.
                }
            }

            // If anything depends on options that changed, re-evaluate those specific options.
            if (secondPassOptions.Any())
            {
                UpdateOptions(ref numAttemptsRemaining, mod, target, secondPassOptions.Distinct().ToList());
            }

            secondPassOptions.Clear(); // We will reuse this

            if (initialSetup)
            {
                foreach (var group in AlternateGroups.Where(x => x.IsMultiSelector))
                {
                    var firstAutoForced = group.AlternateOptions.FirstOrDefault(x => x.UIRequired);
                    if (firstAutoForced != null)
                    {
                        group.SelectedOption = firstAutoForced;
                        group.SelectedOption.UIIsSelected = true;
                    }
                    else
                    {
                        if (group.SelectedOption.UINotApplicable)
                        {
                            // Find first option that is not marked as not-applicable
                            var option = group.AlternateOptions.FirstOrDefault(x => !x.UINotApplicable);
                            if (option != null)
                            {
                                // This is a bad setup in moddesc!
                                secondPassOptions.Add(group.SelectedOption);
                                secondPassOptions.Add(option);
                                secondPassOptions.AddRange(findOptionsDependentOn(option));
                                group.SelectedOption.UIIsSelected = false;
                                group.SelectedOption = option;
                                option.UIIsSelected = true;
                            }
                        }
                    }
                }

                // Re-evaluate again if any default options were not selectable and were changed.
                if (secondPassOptions.Any())
                {
                    Debug.WriteLine(@"Third re-evaluation pass");
                    UpdateOptions(ref numAttemptsRemaining, mod, target, secondPassOptions.Distinct().ToList());
                }
            }
        }

        /// <summary>
        /// Gets a list of alternates that have a state dependency on the specified key
        /// </summary>
        /// <param name="alternateOption"></param>
        /// <returns></returns>
        private List<AlternateOption> findOptionsDependentOn(AlternateOption alternateOption)
        {
            var allOptions = AlternateGroups.SelectMany(x => x.AlternateOptions).ToList();

#if DEBUG
            Debug.WriteLine($@"Matching on optionkey {alternateOption.OptionKey}");
            foreach (var op in allOptions)
            {
                foreach (var k in op.DependsOnKeys)
                {
                    Debug.WriteLine($@"{op.FriendlyName} | {k.Key} matches {alternateOption.OptionKey}: {k.Key == alternateOption.OptionKey}");
                }
            }
#endif

            var results = allOptions.Where(x => x.DependsOnKeys.Any(x => x.Key == alternateOption.OptionKey)).ToList();
            return results;
        }


        private bool CanInstall()
        {
            if (PreventInstallUntilTargetChange) return false;
            foreach (var group in AlternateGroups)
            {
                if (group.IsMultiSelector)
                {
                    // Multi mode

                    // 06/11/2022 - Change to only 8.0 or higher to prevent breaking old mods that abused the group not having a default
                    // option picked
                    // NEEDS A BIT MORE VALIDATION ON PASSING OPTIONS THROUGH
                    if (group.SelectedOption.UINotApplicable && ModBeingInstalled.ModDescTargetVersion >= 8.0) return false; // Option must be selectable by user in order for it to be chosen by multi selector
                }
                else
                {
                    // Single mode
                }
            }

            return true;
        }

        private bool IsInstallingMod;

        private void BeginInstallingMod()
        {
            // Do not install if double clicked or if install is disabled
            if (PreventInstallUntilTargetChange || IsInstallingMod) // Prevents double calls to this method
                return;
            IsInstallingMod = true;

            // Set the 'SelectedOption' on groups to have UIIsSelected = true so the enumeration works
            // This makes sure at most one option is set - (MM7 backcompat from 8.0 means there might be a state
            // where one option is not chosen due to use of radioboxes with autos...)
            foreach (var v in AlternateGroups.Where(x => x.IsMultiSelector))
            {
                if (v.SelectedOption != null && !v.SelectedOption.UINotApplicable)
                {
                    v.SelectedOption.UIIsSelected = true;
                }

                foreach (var e in v.OtherOptions)
                    e.UIIsSelected = false;
            }


            // Create a map of jobs to headers based on the selection options.
            // Makes sure this is done from the InstallationJobs header so it's in MODDESC order and not UI order
            // This means the UI should change the selection states of alternates
            var optionsMap = new Dictionary<ModJob.JobHeader, List<AlternateOption>>();
            M3Log.Information(@"Building list of alternates to pass to mod installer - they will apply in order", Settings.LogModInstallation);
            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                optionsMap[job.Header] = new List<AlternateOption>();
                foreach (var alt in job.AlternateFiles.Where(x => x.UIIsSelected))
                {
                    M3Log.Information($@"Adding alternate file to install package {job.Header} {alt.FriendlyName}", Settings.LogModInstallation);
                    optionsMap[job.Header].Add(alt);
                }
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    // Custom DLC: add alternate dlc option.
                    foreach (var alt in job.AlternateDLCs.Where(x => x.UIIsSelected))
                    {
                        M3Log.Information($@"Adding alternate dlc to install package {job.Header} {alt.FriendlyName}", Settings.LogModInstallation);
                        optionsMap[job.Header].Add(alt);
                    }
                }
            }

            ModInstallOptionsPackage moip = new ModInstallOptionsPackage()
            {
                SkipPrerequesitesCheck = AllOptionsAreAutomatic,
                CompressInstalledPackages = CompressInstalledPackages,
                InstallTarget = SelectedGameTarget,
                ModBeingInstalled = ModBeingInstalled,
                SelectedOptions = optionsMap,
                BatchMode = BatchMod != null,
                IsFirstBatchMod = BatchMod?.IsFirstBatchMod ?? false,
                SetME1ReadOnlyConfigFiles = AlternateGroups.SelectMany(x => x.AlternateOptions).OfType<ReadOnlyOption>().Any(x => x.UIIsSelected) // ME1 Read only option
            };

            // Save batch options to the object in the event the user wants to save the options.
            if (BatchMod != null)
            {
                // Record them to the batch mod
                BatchMod.UserChosenOptions = InOrderRecordedOptions;
                BatchMod.AllChosenOptionsForValidation = optionsMap.SelectMany(x => x.Value).Select(x => x.OptionKey).ToList();
                BatchMod.ConfigurationTime = DateTime.Now;
                BatchMod.HasChosenOptions = true;
                BatchMod.ChosenOptionsDesync = false;
            }

            OnClosing(new DataEventArgs(moip));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            GC.Collect(); //this should help with the oddities of missing radio button's somehow still in the visual tree from busyhost
            InitializeComponent();
            InstallationTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == ModBeingInstalled.Game));
            SelectedGameTarget = mainwindow.SelectedGameTarget != null && mainwindow.SelectedGameTarget.Game == ModBeingInstalled.Game ? mainwindow.SelectedGameTarget : InstallationTargets.FirstOrDefault();
            if (SelectedGameTarget != null)
            {
                SetupOptions(true);
                if (BatchMod != null && BatchMod.UseSavedOptions && BatchMod.HasChosenOptions && !BatchMod.ChosenOptionsDesync)
                {
                    // Install our option choices
                    InstallBatchChosenOptions();
                }
            }
        }

        /// <summary>
        /// Installs batch mod options to the mod. If options are valid, the mod will immediately install
        /// </summary>
        /// <param name="chosenDefaultOptions">List of chosen options to set. If null we will make a list of the original selections (for backup and revert)</param>
        private void InstallBatchChosenOptions(List<PlusMinusKey> chosenDefaultOptions = null)
        {
            // 1. Store the original options
            bool isReverting = chosenDefaultOptions != null; // If incoming options are already selected then this is a reversion due to below code

            if (chosenDefaultOptions == null)
            {
                chosenDefaultOptions ??= new List<PlusMinusKey>();
                foreach (var alt in AlternateGroups.SelectMany(x => x.AlternateOptions).Where(x => x.UIIsSelected))
                {
                    chosenDefaultOptions.Add(new PlusMinusKey(true, alt.OptionKey));
                }
            }

            bool hadSelectionFailure = false;
            if (!isReverting)
            {
                // Install our options

                // Enumerate every chosen option and select them in order
                foreach (var option in BatchMod.UserChosenOptions)
                {
                    // Find group and key
                    foreach (var group in AlternateGroups) // For every group...
                    {
                        var matching = group.AlternateOptions.FirstOrDefault(x => x.OptionKey == option.Key);
                        if (matching != null)
                        {
                            hadSelectionFailure |= !group.TrySelectOption(matching, option.IsPlus);
                            if (hadSelectionFailure)
                            {
                                M3Log.Error($@"Failed to select option {matching.OptionKey}, it was not selectable, this batch mod's configuration is not valid. We will revert to defaults.");
                                break;
                            }
                        }
                    }
                    if (hadSelectionFailure)
                    {
                        break;
                    }
                }
            }

            if (hadSelectionFailure)
            {
                M3Log.Error(@"Reverting to default mod options");
                InstallBatchChosenOptions(chosenDefaultOptions);
                return;
            }

            // 4. Validate that the selected options match the ones we know about in the batchmod object
            if (!isReverting)
            {
                bool valid = true;
                var allChosenOptionsInPanel = ModBeingInstalled.GetAllAlternates().Where(x => x.UIIsSelected).Select(x => x.OptionKey).ToList();
                var difference = allChosenOptionsInPanel.Except(BatchMod.AllChosenOptionsForValidation).ToList();
                if (difference.Any())
                {
                    M3Log.Error($@"The list of chosen options does not match the configured list - the underlying game state has changed. This mod requires reconfiguration.");
                    M3Log.Error($@"Keys that are configured for selection: {string.Join(',', BatchMod.AllChosenOptionsForValidation)}");
                    M3Log.Error($@"Keys that were selected in this install just now: {string.Join(',', allChosenOptionsInPanel)}");
                    valid = false;
                }

                if (!valid)
                {
                    // restore the originals
                    M3Log.Error(@"Batch mod options are not valid, reverting");
                    InstallBatchChosenOptions(chosenDefaultOptions); // REVERT
                }
                else
                {
                    M3Log.Information(@"Batch mod options are valid, beginning install");
                    AllOptionsAreAutomatic = true;
                    BeginInstallingMod();
                }
            }
        }

        protected override void OnClosing(DataEventArgs e)
        {
            base.OnClosing(e);
            foreach (var ao in AlternateGroups)
            {
                ao.ReleaseAssets();
                ao.RemoveIsSelectedChangeHandler(OnAlternateSelectionChanged);
            }
            AlternateGroups.ClearEx();
        }
        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        private void DebugEnumerateOptions_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            foreach (var v in AlternateGroups)
            {
                Debug.WriteLine($@"{v.AlternateOptions.Count} options in group {v.GroupName}:");
            }
#endif
        }

        private void FrameworkElement_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
