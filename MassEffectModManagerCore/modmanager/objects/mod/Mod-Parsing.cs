﻿using System.Diagnostics;
using System.Globalization;
using System.Text;
using IniParser.Parser;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.ModManager.Interfaces;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod.headmorph;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.modmanager.objects.mod.moddesc;
using SevenZip;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    [DebuggerDisplay("Mod - {ModName}")] //do not localize
    [AddINotifyPropertyChangedInterface]
    public partial class Mod : IImportableMod, IM3Mod
    {

        private static readonly string[] DirectorySeparatorChars = new[] { @"\", @"/" };
        /// <summary>
        /// The default website value, to indicate one was not set. This value must be set to a valid url or navigation request in UI binding may not work.
        /// </summary>
        public const string DefaultWebsite = @"http://example.com"; //this is required to prevent exceptions when binding the navigateuri

        /// <summary>
        /// MergeMods folder. Do not change
        /// </summary>
        public const string MergeModFolderName = @"MergeMods";  // DO NOT CHANGE THIS VALUE

        /// <summary>
        /// The folder that contains the TLK files for the GAME1_EMBEDDED_TLK feature. DO NOT CHANGE.
        /// </summary>
        public const string Game1EmbeddedTlkFolderName = @"GAME1_EMBEDDED_TLK";  // DO NOT CHANGE THIS VALUE

        /// <summary>
        /// The filename of the combined compressed TLK info for moddesc > 8 GAME1_EMBEDDED_TLK
        /// </summary>
        public const string Game1EmbeddedTlkCompressedFilename = @"CombinedTLKMergeData.m3za";  // DO NOT CHANGE THIS VALUE

        /// <summary>
        /// The numerical ID for a mod on the respective game's NexusMods page. This is automatically parsed from the ModWebsite if this is not explicitly set and the ModWebsite attribute is a nexusmods url.
        /// </summary>
        public int NexusModID { get; set; }

        /// <summary>
        /// The moddesc.ini value that was set for nexuscode. If one was not set, this value is blank.
        /// </summary>
        public string NexusCodeRaw { get; set; }

        /// <summary>
        /// Indicates if this is a valid mod or not.
        /// </summary>
        public bool ValidMod { get; private set; }
        /// <summary>
        /// List of installation jobs (aka Task Headers)
        /// </summary>
        public List<ModJob> InstallationJobs = new List<ModJob>();
        /// <summary>
        /// Mapping of Custom DLC names to their human-readable versions.
        /// </summary>
        public Dictionary<string, string> HumanReadableCustomDLCNames = new Dictionary<string, string>(0);
        /// <summary>
        /// What game this mod can install to.
        /// </summary>
        public MEGame Game { get; set; }
        /// <summary>
        /// If this mod actually targets the LE launcher
        /// </summary>
        public bool TargetsLELauncher { get; set; }
        /// <summary>
        /// Flag if the moddesc.ini is stored properly in the archive file when the archive was loaded. Can be used to detect if mod was actually
        /// properly deployed, or if developer decided to skip M3 deployment, which shouldn't be done. Only used when loading mod from archive.
        /// </summary>
        public bool DeployedWithM3 { get; set; }
        /// <summary>
        /// If this mod should check if it was deploying using M3 or not.
        /// </summary>
        public bool CheckDeployedWithM3 { get; private set; }
        /// <summary>
        /// The mod's name.
        /// </summary>
        public string ModName { get; set; }
        /// <summary>
        /// Developer of the mod
        /// </summary>
        public string ModDeveloper { get; set; }
        /// <summary>
        /// The description for the mod, as written in moddesc.ini
        /// </summary>
        public string ModDescription { get; set; }
        /// <summary>
        /// The ID for updating on ModMaker. This value will be 0 if none is set.
        /// </summary>
        public int ModModMakerID { get; set; }
        /// <summary>
        /// Indicates this is an 'unofficial' mod that was imported from the game's DLC directory.
        /// </summary>
        public bool IsUnofficial { get; set; }
        /// <summary>
        /// This variable is only set if IsInArchive is true
        /// </summary>
        public long SizeRequiredtoExtract { get; set; }
        /// <summary>
        /// Used with DLC imported from the game directory, this is used to track what version of MM imported the DLC, which may be used in the future check for things like texture tags.
        /// </summary>
        public int ImportedByBuild { get; set; }
        /// <summary>
        /// If a mod prefers compressed packages. This auto sets the 'Compress packages' flag in the importer window
        /// </summary>
        public bool PreferCompressed { get; set; }
        /// <summary>
        /// If the mod requires an AMD processor to install. This is only used for ME1 lighting fix.
        /// </summary>
        public bool RequiresAMD { get; set; }

        /// <summary>
        /// List of files that will always be deleted locally when servicing an update on a client. This has mostly been deprecated for new mods.
        /// </summary>
        public ObservableCollectionExtended<string> UpdaterServiceBlacklistedFiles { get; } = new ObservableCollectionExtended<string>();

        /// <summary>
        /// If this mod can attempt to check for updates via Nexus. This being true doesn't mean it will - it requires whitelisting.
        /// This essentially is only used to disable nexus update checks.
        /// </summary>
        public bool NexusUpdateCheck { get; set; } = true;
        /// <summary>
        /// The server folder that this mod will be published to when using the ME3Tweaks Updater Service
        /// </summary>
        public string UpdaterServiceServerFolder { get; set; }
        public string UpdaterServiceServerFolderShortname
        {
            get
            {
                if (UpdaterServiceServerFolder == null) return null;
                if (UpdaterServiceServerFolder.Contains('/'))
                {
                    return UpdaterServiceServerFolder.Substring(UpdaterServiceServerFolder.LastIndexOf('/') + 1);
                }
                return UpdaterServiceServerFolder;
            }
        }

        /// <summary>
        /// If alternate options should be sorted on initial setup to put NotApplicable items at the bottom
        /// </summary>
        public bool SortAlternateOptions { get; set; } = true;

        /// <summary>
        /// Indicates if this mod has the relevant information attached to it for updates. That is, classic update code, modmaker id, or nexusmods ID
        /// </summary>
        public bool IsUpdatable
        {
            get
            {
                if (ModClassicUpdateCode > 0) return true;
                if (ModModMakerID > 0) return true;
                if (NexusModID > 0 && NexusUpdateCheck && NexusUpdaterService.IsNexusCodeWhitelisted(Game, NexusModID)) return true;
                return false;
            }
        }

        /// <summary>
        /// Indicates if this mod has the relevant information attached to it for updates from ME3Tweaks. That is, classic update code or modmaker id
        /// </summary>
        public bool IsME3TweaksUpdatable
        {
            get
            {
                if (ModClassicUpdateCode > 0) return true;
                if (ModModMakerID > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The actual description string shown on the right hand panel of Mod Manager's main window
        /// </summary>
        public string DisplayedModDescription
        {
            get
            {
                if (LoadFailedReason != null) return LoadFailedReason;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ModDescription);
                sb.AppendLine(@"=============================");
                //Todo: Automatic configuration

                //Todo: Optional manuals

                sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modVersion, ModVersionString ?? @"1.0"));
                sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modDeveloper, ModDeveloper));
                if (ModModMakerID > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modMakerCode, ModModMakerID.ToString()));
                }
                if (ModClassicUpdateCode > 0 && Settings.DeveloperMode)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_updateCode, ModClassicUpdateCode.ToString()));
                }
                //else if (NexusModID > 0 && Settings.DeveloperMode)
                //{
                //    sb.AppendLine($"NexusMods ID: {NexusModID}");
                //}

                sb.AppendLine(M3L.GetString(M3L.string_modparsing_installationInformationSplitter));
                if (Settings.DeveloperMode)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_targetsModDesc, ModDescTargetVersion.ToString(CultureInfo.InvariantCulture)));
                }
                var modifiesList = InstallationJobs.Where(x => x.Header != ModJob.JobHeader.CUSTOMDLC
                                                               && x.Header != ModJob.JobHeader.LOCALIZATION
                                                               && x.Header != ModJob.JobHeader.TEXTUREMODS
                                                               && x.Header != ModJob.JobHeader.HEADMORPHS).Select(x => x.Header == ModJob.JobHeader.ME2_RCWMOD ? @"ME2 Coalesced.ini" : x.Header.ToString()).ToList();
                if (modifiesList.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modifies, string.Join(@", ", modifiesList)));
                }

                var customDLCJob = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC);
                if (customDLCJob != null)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_addCustomDLCs, string.Join(@", ", customDLCJob.CustomDLCFolderMapping.Values)));
                }

                var localizationJob = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.LOCALIZATION);
                if (localizationJob != null)
                {
                    var dlcReq = RequiredDLC.First().DLCFolderName; //Localization jobs, if valid, will always have something here.
                    var tpmi = TPMIService.GetThirdPartyModInfo(dlcReq.Key, Game);
                    if (tpmi != null) dlcReq += $@" ({tpmi.modname})";
                    sb.AppendLine(M3L.GetString(M3L.string_interp_addsTheFollowingLocalizationsToX, dlcReq));
                    foreach (var l in localizationJob.FilesToInstall)
                    {
                        var langCode = l.Key.Substring(l.Key.Length - 7, 3);
                        sb.AppendLine(@" - " + langCode);
                    }
                }

                if (ASIModsToInstall.Any())
                {
                    sb.AppendLine(M3L.GetString(M3L.string_installsTheFollowingASIMods));
                    foreach (var asi in ASIModsToInstall)
                    {
                        var realasi = ASIManager.GetASIModVersion(Game, asi.ASIGroupID, asi.Version);
                        if (realasi == null)
                        {
                            var str = $@" - {asi.ASIGroupID}";
                            if (asi.Version != null)
                            {
                                str += $@" v{asi.Version}";
                            }

                            str += @" (" + M3L.GetString(M3L.string_invalid) + @")";
                            sb.AppendLine(str);
                        }
                        else
                        {
                            var str = $@" - {realasi.Name}";
                            if (asi.Version != null)
                            {
                                str += $@" v{asi.Version}";
                            }

                            sb.AppendLine(str);
                        }
                    }
                }


                SortedSet<string> autoConfigs = GetAutoConfigs();
                if (autoConfigs.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_modparsing_configCanChangeIfOtherDLCFound));
                    foreach (var autoConfigDLC in autoConfigs)
                    {
                        string name = TPMIService.GetThirdPartyModInfo(autoConfigDLC, Game)?.modname ?? autoConfigDLC;
                        sb.AppendLine($@" - {name}");
                    }
                }


                if (RequiredDLC.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_modparsing_requiresTheFollowingDLCToInstall));
                    foreach (var reqDLC in RequiredDLC)
                    {
                        var info = TPMIService.GetThirdPartyModInfo(reqDLC.DLCFolderName.Key, Game);
                        sb.AppendLine($@" - {reqDLC.ToUIString(info, false)}");
                    }
                }

                if (OptionalSingleRequiredDLC.Any())
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_singleRequiredDLC));
                    foreach (var reqDLC in OptionalSingleRequiredDLC)
                    {
                        string name = TPMIService.GetThirdPartyModInfo(reqDLC.DLCFolderName.Key, Game)?.modname ?? reqDLC.DLCFolderName.Key;
                        sb.AppendLine($@" - {name}");
                    }
                }

                var texJob = GetJob(ModJob.JobHeader.TEXTUREMODS);
                if (texJob != null && texJob.TextureModReferences.Any())
                {
                    sb.AppendLine(M3L.GetString(M3L.string_mod_modReferencesTextureFiles));
                    foreach (var reference in texJob.TextureModReferences)
                    {
                        string name = reference.Title;
                        sb.AppendLine($@" - {name}");
                    }
                }

                var headmorphJob = GetJob(ModJob.JobHeader.HEADMORPHS);
                if (headmorphJob != null && headmorphJob.HeadMorphFiles.Any())
                {
                    sb.AppendLine(M3L.GetString(M3L.string_mod_modReferencesHeadmorphFiles));
                    foreach (var reference in headmorphJob.HeadMorphFiles)
                    {
                        string name = reference.Title;
                        sb.AppendLine($@" - {name}");
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets the installation job associated with the header, or null if that job is not defined for this mod.
        /// </summary>
        /// <param name="header">Header to find job for</param>
        /// <returns>Associated job with this header, null otherwise</returns>
        public ModJob GetJob(ModJob.JobHeader header) => InstallationJobs.FirstOrDefault(x => x.Header == header);

        /// <summary>
        /// The raw string the mod sets as the version. Check <see cref="ParsedModVersion"/> for the parsed version.
        /// </summary>
        public string ModVersionString { get; set; }
        /// <summary>
        /// The properly versioned mod version. Can be null if the developer does it wrong.
        /// </summary>
        public Version ParsedModVersion { get; set; }

        /// <summary>
        /// The website the mod lists
        /// </summary>
        public string ModWebsite { get; set; } = ""; //not null default I guess.

        /// <summary>
        /// The moddesc parser version 
        /// </summary>
        public double ModDescTargetVersion { get; set; }

        /// <summary>
        /// List of DLC foldernames that will be offered for removal if found upon successful mod install
        /// </summary>

        public List<string> OutdatedCustomDLC = new List<string>();

        /// <summary>
        /// List of DLC foldernames that will block install of this mod
        /// </summary>
        public List<string> IncompatibleDLC = new List<string>();

        /// <summary>
        /// The updater service code for this mod
        /// </summary>
        public int ModClassicUpdateCode { get; set; }

        /// <summary>
        /// The reason the mod failed to load
        /// </summary>
        public string LoadFailedReason { get; set; }
#if AZURE && DEBUG
        public void OnLoadFailedReasonChanged()
        {
            Debug.WriteLine(@"Breakpoint me");
        }
#endif

        /// <summary>
        /// If this mod makes use of the new bink encoder - this flag is used to help flag that bink is not installed when troubleshooting
        /// </summary>
        public bool RequiresEnhancedBink { get; set; }

        /// <summary>
        /// If this mod should use the highest mount priority instead of the lowest when sorting in batch installer
        /// </summary>
        public bool BatchInstallUseReverseMountSort { get; set; }

        /// <summary>
        /// List of DLC requirements for this mod to be able to install
        /// </summary>
        public List<DLCRequirement> RequiredDLC { get; set; } = new List<DLCRequirement>();

        /// <summary>
        /// List of DLC, of which at least one must be installed
        /// </summary>
        public List<DLCRequirement> OptionalSingleRequiredDLC = new List<DLCRequirement>();

        /// <summary>
        /// List of additional folders to include in mod deployment
        /// </summary>
        private List<string> AdditionalDeploymentFolders = new List<string>();

        /// <summary>
        /// List of additional files to include in mod deployment
        /// </summary>
        private List<string> AdditionalDeploymentFiles = new List<string>();

        /// <summary>
        /// List of ASI mods to install based on their group id in the ASI manifest
        /// </summary>
        public List<M3ASIVersion> ASIModsToInstall = new List<M3ASIVersion>();

        /// <summary>
        /// The path on disk to the root of the mod folder
        /// </summary>
        public string ModPath { get; private set; }

        /// <summary>
        /// The archive this mod was loaded from, if loaded from archive
        /// </summary>
        public SevenZipExtractor Archive;
        /// <summary>
        /// The full path to the moddesc.ini file
        /// </summary>
        public string ModDescPath => FilesystemInterposer.PathCombine(IsInArchive, ModPath, @"moddesc.ini");

        /// <summary>
        /// If this mod was was loaded from archive or from disk
        /// </summary>
        public bool IsInArchive { get; init; }
        /// <summary>
        /// The minimum build number that this mod is allowed to load on
        /// </summary>
        public int MinimumSupportedBuild { get; set; }
        /// <summary>
        /// If this mod was loaded using a moddesc.ini from ME3Tweaks
        /// </summary>
        public bool IsVirtualized { get; private set; }

        /// <summary>
        /// What tool to launch after mod install
        /// </summary>
        public string PostInstallToolLaunch { get; private set; }

        /// <summary>
        /// The virtualize ini text, if this mod was loaded from virtual
        /// </summary>
        private readonly string VirtualizedIniText;

        /// <summary>
        /// The path to the archive file, if this mod was initialized from archive on disk
        /// </summary>
        private readonly string ArchivePath;

        public Mod(RCWMod rcw)
        {
            M3Log.Information(@"Converting an RCW mod to an M3 mod.");
            Game = MEGame.ME2;
            ModDescTargetVersion = 6.0;
            ModDeveloper = rcw.Author;
            ModName = rcw.ModName;
            ModDescription = M3L.GetString(M3L.string_modparsing_defaultRCWDescription);
            ModJob rcwJob = new ModJob(ModJob.JobHeader.ME2_RCWMOD);
            rcwJob.RCW = rcw;
            InstallationJobs.Add(rcwJob);
            ValidMod = true;
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (RCW) - " + ModName, this);
        }

        /// <summary>
        /// Loads a moddesc from a stream. Used when reading data from an archive. 
        /// </summary>
        /// <param name="moddescArchiveEntry">File entry in archive for this moddesc.ini</param>
        /// <param name="archive">Archive to inspect for</param>
        public Mod(ArchiveFileInfo moddescArchiveEntry, SevenZipExtractor archive)
        {
            M3Log.Information($@"Loading moddesc.ini from archive: {Path.GetFileName(archive.FileName)} => {moddescArchiveEntry.FileName}");
            MemoryStream ms = new MemoryStream();
            try
            {
                M3Log.Information($@"moddesc.ini file is stored with compression {moddescArchiveEntry.Method}");
                archive.ExtractFile(moddescArchiveEntry.FileName, ms);
                CheckDeployedWithM3 = true;
                DeployedWithM3 = Path.GetExtension(archive.FileName) == @".7z" && moddescArchiveEntry.Method == @"Copy";
            }
            catch (Exception e)
            {
                ModName = M3L.GetString(M3L.string_loadFailed);
                M3Log.Error(@"Error loading moddesc.ini from archive! Error: " + e.Message);
                LoadFailedReason = M3L.GetString(M3L.string_interp_errorReadingArchiveModdesc, e.Message);
                return;
            }

            // 06/14/2022 - ME3Tweaks Moddesc Updates 
            // This is for mods that might break when used on Mod Manager 8.0 and above due to alternate logic change
            ModDescHash = MUtilities.CalculateHash(ms); //resets pos to 0
            ModDescSize = ms.Length;

            string updatedIni = null;
            if (ModDescUpdaterService.HasHash(ModDescHash))
            {
                updatedIni = ModDescUpdaterService.FetchUpdatedModdesc(ModDescHash, out var localHash);
                if (updatedIni != null)
                {
                    M3Log.Information(@"This moddesc is being updated by ME3Tweaks ModDesc Updater Service");
                    ModDescHash = localHash;
                    VirtualizedIniText = updatedIni;
                    IsVirtualized = true; // Mark virtualized so on extraction it works properly
                }
            }

            string iniText = updatedIni ?? new StreamReader(ms).ReadToEnd();

            ModPath = Path.GetDirectoryName(moddescArchiveEntry.FileName);
            Archive = archive;
            ArchivePath = archive.FileName;
            IsInArchive = true;
            try
            {
                loadMod(iniText, MEGame.Unknown);
                SizeRequiredtoExtract = GetRequiredSpaceForExtraction();
            }
            catch (Exception e)
            {
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingArchiveModdescini, moddescArchiveEntry.FileName, e.Message);
            }
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Archive) - " + ModName, this);

            //Retain reference to archive as we might need this.
            //Archive = null; //dipose of the mod
        }

        /// <summary>
        /// Hash of the moddesc file.
        /// </summary>
        public string ModDescHash { get; set; }

        /// <summary>
        /// Size of the moddesc file. Only populated whne loading from archive
        /// </summary>
        public long ModDescSize { get; set; }

        /// <summary>
        /// Initializes a mod from a moddesc.ini file
        /// </summary>
        /// <param name="filePath">moddesc.ini path</param>
        /// <param name="expectedGame">Game that the mod is expected to resolve to. Set to unknown if it is not known.</param>
        /// <param name="blankLoad">If this mod is supposed to be 'blank'. Suppresses output about this mod being invalid, essentially this flag indicates the caller knows what they are doing.</param>
        public Mod(string filePath, MEGame expectedGame, bool blankLoad = false)
        {
            ModPath = Path.GetDirectoryName(filePath);
            M3Log.Information(@"Loading moddesc: " + filePath);
            try
            {
                loadMod(File.ReadAllText(filePath), expectedGame, blankLoad: blankLoad);
                ModDescHash = MUtilities.CalculateHash(filePath);
                ModDescSize = new FileInfo(filePath).Length;
            }
            catch (Exception e)
            {
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingModdescini, filePath, e.Message);
            }
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Disk) - " + ModName, this);

        }

        /// <summary>
        /// Loads a mod from a virtual moddesc.ini file, forcing the ini path. If archive is specified, the archive will be used, otherwise it will load as if on disk.
        /// </summary>
        /// <param name="iniText">Virtual Ini text</param>
        /// <param name="forcedModPath">Directory where this moddesc.ini would reside be if it existed in the archive or on disk</param>
        /// <param name="archive">Archive file to parse against. If null, this mod will be parsed as if on-disk</param>
        public Mod(string iniText, string forcedModPath, SevenZipExtractor archive)
        {
            ModPath = forcedModPath;
            if (archive != null)
            {
                Archive = archive;
                ArchivePath = archive.FileName;
                IsInArchive = true;
                IsVirtualized = true;
            }

            VirtualizedIniText = iniText;
            M3Log.Information(@"Loading virtualized moddesc.ini");
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                ValidMod = false;
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingVirtualizedModdescini, e.Message);
            }

            if (archive != null && ValidMod)
            {
                SizeRequiredtoExtract = GetRequiredSpaceForExtraction();
                M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Virtualized) - " + ModName, this);
            }

        }

        private readonly string[] GameFileExtensions = { @".u", @".upk", @".sfm", @".pcc", @".bin", @".tlk", @".cnd", @".ini", @".afc", @".tfc", @".dlc", @".sfar", @".txt", @".bik", @".bmp", @".usf", @".isb" };

        /// <summary>
        /// Main moddesc.ini parser
        /// </summary>
        /// <param name="iniText"></param>
        /// <param name="expectedGame"></param>
        private void loadMod(string iniText, MEGame expectedGame, bool blankLoad = false)
        {
            Game = expectedGame; //we will assign this later. This is for startup errors only
            var parser = new IniDataParser();
            var iniData = parser.Parse(iniText);

            if (int.TryParse(iniData[MODDESC_HEADERKEY_MODMANAGER][MODDESC_DESCRIPTOR_MODMANAGER_MINBUILD], out int minBuild))
            {
                MinimumSupportedBuild = minBuild;
                if (App.BuildNumber < minBuild)
                {
                    ModName = (ModPath == "" && IsInArchive)
                        ? Path.GetFileNameWithoutExtension(Archive.FileName)
                        : Path.GetFileName(ModPath);
                    M3Log.Error(
                        $@"This mod specifies it can only load on M3 builds {minBuild} or higher. The current build number is {App.BuildNumber}.");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_cannotLoadBuildTooOld,
                        minBuild, App.BuildNumber);
                    return; //Won't set valid
                }
            }

            int.TryParse(iniData[MODDESC_HEADERKEY_MODMANAGER][MODDESC_DESCRIPTOR_MODMANAGER_IMPORTEDBY], out int importedByBuild);
            ImportedByBuild = importedByBuild;

            if (double.TryParse(iniData[MODDESC_HEADERKEY_MODMANAGER][MODDESC_DESCRIPTOR_MODMANAGER_CMMVER], out double parsedModCmmVer))
            {
                ModDescTargetVersion = parsedModCmmVer;
            }
            else
            {
                //Run in legacy mode (ME3CMM 1.0)
                ModDescTargetVersion = 1.0;
            }


            if (parsedModCmmVer < 6.0)
            {
                CheckDeployedWithM3 = false;
                DeployedWithM3 = false;
            }

            ModName = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME];
            if (string.IsNullOrEmpty(ModName))
            {
                ModName = (ModPath == "" && IsInArchive)
                    ? Path.GetFileNameWithoutExtension(Archive.FileName)
                    : Path.GetFileName(ModPath);
                M3Log.Error($@"moddesc.ini in {ModPath} does not set the modname descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomodname, ModPath);
                return; //Won't set valid
            }

            // This is loaded early so the website value can be parsed if something else fails. 
            // This is used in failedmodspanel
            ModWebsite = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_SITE] ?? DefaultWebsite;
            if (string.IsNullOrWhiteSpace(ModWebsite)) ModWebsite = DefaultWebsite;

            //test url scheme
            Uri uriResult;
            if (!(Uri.TryCreate(ModWebsite, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)))
            {
                M3Log.Error($@"Invalid url for mod {ModName}: URL must be of type http:// or https:// and be a valid formed url. Invalid value: {ModWebsite}");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_invalidUrlScheme, ModName, ModWebsite);
                ModWebsite = DefaultWebsite; //Reset so we don't try to open invalid url
                return; //Won't set valid
            }

            // After name and site are loaded, we now check if we can parse it, as these attributes have been always supported.
            // LE1DP shipped with 8.1 as the value before 8.1 was ever even submitted for beta testing; this needs removed eventually //04/01/2023
            // LE1DP workaround removed 05/02/2023
            if (parsedModCmmVer > App.HighestSupportedModDesc)
            {
                M3Log.Error(@"The cmmver specified by this mod is higher than the version supported by this build of ME3Tweaks Mod Manager. You may need to update Mod Manager for this mod to load.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_unsupportedModdescVersion, parsedModCmmVer, App.HighestSupportedModDesc);
                return;
            }

            ModDescription = M3Utilities.ConvertBrToNewline(iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION]);
            if (string.IsNullOrWhiteSpace(ModDescription))
            {
                M3Log.Error($@"moddesc.ini in {ModPath} does not set the moddesc descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomoddesc, ModPath);
                return; //Won't set valid
            }

            ModDeveloper = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER];
            if (string.IsNullOrWhiteSpace(ModDeveloper))
            {
                M3Log.Error($@"moddesc.ini in {ModPath} does not set the moddev descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomoddev, ModPath);
                return; //Won't set valid
            }

            ModVersionString = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION];
            //Check for integer value only
            if (int.TryParse(ModVersionString, out var intVersion))
            {
                ModVersionString += @".0";
            }

            Version.TryParse(ModVersionString, out var parsedValue);
            ParsedModVersion = parsedValue;

            //updates
            int.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_MODMAKERID], out int modmakerId);
            ModModMakerID = modmakerId;

            int.TryParse(iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_MODINFO_UPDATECODE], out int modupdatecode);
            ModClassicUpdateCode = modupdatecode;

            if (bool.TryParse(iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_UPDATES_NEXUSUPDATECHECK], out var nexusupdatecheck))
            {
                // Enables/disables the mod from being able to check in with Nexus
                NexusUpdateCheck = nexusupdatecheck;
            }

            if (ModClassicUpdateCode == 0)
            {
                //try in old location
                int.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_UPDATECODE], out int modupdatecode2);
                ModClassicUpdateCode = modupdatecode2;
            }

            int.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NEXUSMODSDOMAINID], out int nexuscode);
            NexusModID = nexuscode;
            NexusCodeRaw = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NEXUSMODSDOMAINID];

            // NexusMods code from URL is parsed after Game is read since it changes how domain parser works

            M3Log.Information($@"Read modmaker update code (or used default): {ModClassicUpdateCode}",
                Settings.LogModStartup);
            if (ModClassicUpdateCode > 0 && ModModMakerID > 0)
            {
                M3Log.Error(
                    $@"{ModName} has both an updater service update code and a modmaker code assigned. This is not allowed.");
                LoadFailedReason =
                    M3L.GetString(M3L.string_validation_modparsing_loadfailed_cantSetBothUpdaterAndModMaker);
                return; //Won't set valid
            }

            var unofficialStr = iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_UNOFFICIAL];
            if (!string.IsNullOrWhiteSpace(unofficialStr))
            {
                IsUnofficial = true;
                M3Log.Information($@"Found unofficial descriptor. Marking mod as unofficial. This will block deployment of the mod until it is removed.",
                    Settings.LogModStartup);

            }

            if (bool.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_COMPRESSPACKAGESBYDEFAULT], out var pCompressed))
            {
                M3Log.Information($@"Found prefercompressed descriptor. The mod will default the compress packages flag to {pCompressed} in the mod import panel.",
                    Settings.LogModStartup);
                PreferCompressed = pCompressed;
            }

            if (Game == MEGame.ME1 && bool.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_REQUIRESAMD], out var bRequiresAMD))
            {
                // Only used for ME1 AMD Lighting Fix
                RequiresAMD = bRequiresAMD;
            }

            // ModDesc 8.0: Allow disable alternate sorting
            if (ModDescTargetVersion >= 8.0 && bool.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_SORTALTERNATES], out var bSortAlternates))
            {
                SortAlternateOptions = bSortAlternates;
            }

            string game = iniData[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME];
            if (parsedModCmmVer >= 6 && game == null && importedByBuild > 0)
            {
                //Not allowed. You MUST specify game on cmmver 6 or higher
                M3Log.Error($@"{ModName} does not set the ModInfo 'game' descriptor, which is required for all mods targeting ModDesc 6 or higher.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_missingModInfoGameDescriptor, ModName);
                return;
            }
            switch (game)
            {
                //case null: //will have to find a way to deal with the null case, in the event it's an ME3 mod manager mod from < 6.0.
                case @"ME3":
                    Game = MEGame.ME3;
                    break;
                case @"ME2":
                    Game = MEGame.ME2;
                    break;
                case @"ME1":
                    Game = MEGame.ME1;
                    break;
                // LEGENDARY
                case @"LE1":
                    Game = MEGame.LE1;
                    break;
                case @"LE2":
                    Game = MEGame.LE2;
                    break;
                case @"LE3":
                    Game = MEGame.LE3;
                    break;
                case @"LELAUNCHER":
                    Game = MEGame.LELauncher;
                    TargetsLELauncher = true;
                    break;
                default:
                    //Check if this is in ME3 game directory. If it's null, it might be a legacy mod
                    if (game == null)
                    {
#if !AZURE 
                        // Don't log on azure since there's a ton of mods that will generate this and just clog things up
                        M3Log.Warning(@"Game indicator is null. This may be mod from pre-Mod Manager 6, or developer did not specify the game. Defaulting to ME3", Settings.LogModStartup);
#endif
                        Game = MEGame.ME3;
                    }
                    else
                    {
                        M3Log.Error($@"{ModName} has unknown game ID set for ModInfo descriptor 'game'. Valid values are ME1, ME2, ME3, LE1, LE2, LE3, and LELAUNCHER. Value provided: {game}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_unknownGameId, game);
                        return;
                    }

                    break;
            }

            if (ModDescTargetVersion < 6 && Game.IsOTGame() && Game != MEGame.ME3)
            {
                M3Log.Error($@"{ModName} is designed for {game}. ModDesc versions (cmmver descriptor under ModManager section) under 6.0 do not support ME1 or ME2.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_cmm6RequiredForME12, game, ModDescTargetVersion.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (ModDescTargetVersion < 7 && (Game.IsLEGame() || TargetsLELauncher))
            {
                M3Log.Error($@"{ModName} is designed for {game}. ModDesc versions (cmmver descriptor under ModManager section) under 7.0 cannot target Legendary Edition games.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_leGamesRequireCmm7, ModName, Game.ToGameName());
                return;
            }

            // Parsed here as it depends on the game
            #region NexusMods ID from URL
            if (NexusModID == 0 && ModModMakerID == 0 /*&& ModClassicUpdateCode == 0 */ &&
                !string.IsNullOrWhiteSpace(ModWebsite) && ModWebsite.Contains(@"nexusmods.com/masseffect"))
            {
                var nfi = NexusFileInfo.FromModSite(Game, ModWebsite);
                if (nfi != null && nfi.ModId != 0)
                {
                    NexusModID = nfi.ModId;
                }
            }
            #endregion

            if (ModDescTargetVersion < 2) //Mod Manager 1 (2012)
            {
                //Ancient legacy mod that only supports ME3 basegame coalesced
                ModDescTargetVersion = 1;
                if (CheckAndCreateLegacyCoalescedJob())
                {
                    ValidMod = true;
                }

                M3Log.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
                return;
            }

            // Older versions of mod manager rounded the version numbers, this corrects them
            if (ModDescTargetVersion >= 2.0 && ModDescTargetVersion < 3) //Mod Manager 2 (2013)
            {
                ModDescTargetVersion = 2.0;
            }

            if (ModDescTargetVersion >= 3 && ModDescTargetVersion < 3.1) //Mod Manager 3 (2014)
            {
                ModDescTargetVersion = 3.0;
            }

            //A few mods shipped as 3.2 moddesc, however the features they targeted are officially supported in 3.1
            if (ModDescTargetVersion >= 3.1 && ModDescTargetVersion < 4.0) //Mod Manager 3.1 (2014)
            {
                ModDescTargetVersion = 3.1;
            }

            //This was in Java version - I believe this was to ensure only tenth version of precision would be used. E.g no moddesc 4.52
            ModDescTargetVersion = Math.Round(ModDescTargetVersion * 10) / 10;
            M3Log.Information(@"Parsing mod using moddesc version: " + ModDescTargetVersion, Settings.LogModStartup);

            // End of version rounding

            #region Banner Image
            if (ModDescTargetVersion >= 6.2)
            {
                // Requires 6.2. Mods not deployed using M3 will NOT support this as it has special
                // archive requirements.
                BannerImageName = iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_BANNERIMAGENAME];

                if (!string.IsNullOrWhiteSpace(BannerImageName))
                {
                    var fullPath = FilesystemInterposer.PathCombine(Archive != null, ModPath, Mod.M3IMAGES_FOLDER_NAME, BannerImageName);
                    if (!FilesystemInterposer.FileExists(fullPath, Archive))
                    {
                        M3Log.Error($@"Mod has banner image name of {BannerImageName}, but this file does not exist under the M3Images directory.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_bannerImageAssetNotFound, BannerImageName);
                        return;
                    }

                    // File exists
                    if (Archive != null)
                    {
                        if (!CheckNonSolidArchiveFile(fullPath))
                            return;

                        // If we are loading from archive we must load it here while the archive stream is still available
                        LoadBannerImage();
                    }
                }
            }
            #endregion

            #region Header Loops

            #region BASEGAME and OFFICIAL HEADERS

            var supportedOfficialHeaders = ModJob.GetSupportedNonCustomDLCHeaders(Game);

            //We must check against official headers
            //ME2 doesn't support anything but basegame.
            foreach (var header in supportedOfficialHeaders)
            {
                //if (Game != MEGame.ME3 && header != ModJob.JobHeader.BASEGAME) continue; //Skip any non-basegame offical headers for ME1/ME2
                var headerAsString = header.ToString();
                var jobSubdirectory = iniData[headerAsString][Mod.MODDESC_DESCRIPTOR_JOB_DIR];
                if (jobSubdirectory != null)
                {
                    jobSubdirectory = jobSubdirectory.Replace('/', '\\').TrimStart('\\');
                    M3Log.Information(@"Found INI header with moddir specified: " + headerAsString, Settings.LogModStartup);
                    M3Log.Information(@"Subdirectory (moddir): " + jobSubdirectory, Settings.LogModStartup);
                    //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                    if (TargetsLELauncher)
                    {
                        // Special load
                        LoadLauncherMod(jobSubdirectory);
                        return;
                    }

                    bool directoryMatchesGameStructure = false;
                    if (ModDescTargetVersion >= 6.0) bool.TryParse(iniData[headerAsString][MODDESC_DESCRIPTOR_JOB_GAMEDIRECTORYSTRUCTURE], out directoryMatchesGameStructure);

                    //Replace files (ModDesc 2.0)
                    string replaceFilesSourceList = iniData[headerAsString][Mod.MODDESC_DESCRIPTOR_JOB_NEWFILES]; //Present in MM2. So this will always be read
                    string replaceFilesTargetList = iniData[headerAsString][MODDESC_DESCRIPTOR_JOB_REPLACEFILES]; //Present in MM2. So this will always be read

                    //Add files (ModDesc 4.1)
                    string addFilesSourceList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString][MODDESC_DESCRIPTOR_JOB_ADDFILES] : null;
                    string addFilesTargetList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString][MODDESC_DESCRIPTOR_JOB_ADDFILESTARGETS] : null;

                    //Add files Read-Only (ModDesc 4.3)
                    // Never did anything since Mod Manager 6, removed commented out code in Mod Manager 8
                    //string addFilesTargetReadOnlyList = ModDescTargetVersion >= 4.3 ? iniData[headerAsString][@"addfilesreadonlytargets"] : null;

                    //Remove files (ModDesc 4.1) - REMOVED IN MOD MANAGER 6

                    //MergeMods: Mod Manager 7.0 (parsed below so it passes the task does something check)
                    string mergeModsList = (ModDescTargetVersion >= 7.0 && header == ModJob.JobHeader.BASEGAME) ? iniData[headerAsString][MODDESC_DESCRIPTOR_BASEGAME_MERGEMODS] : null;

                    // AltFiles: Mod Manager 4.2
                    string altfilesStr = (ModDescTargetVersion >= 4.2 && header != ModJob.JobHeader.BALANCE_CHANGES) ? iniData[headerAsString][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_ALTFILES] : null;

                    //Check that the lists here are at least populated in one category. If none are populated then this job will do effectively nothing.
                    bool taskDoesSomething = (replaceFilesSourceList != null && replaceFilesTargetList != null) || (addFilesSourceList != null && addFilesTargetList != null) || !string.IsNullOrWhiteSpace(mergeModsList) || !string.IsNullOrWhiteSpace(altfilesStr);

                    if (!taskDoesSomething)
                    {
                        M3Log.Error($@"Mod has job header ({headerAsString}) with no tasks in add, replace, or remove lists. This header does effectively nothing. Marking mod as invalid");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerDoesNothing, headerAsString);
                        return;
                    }

                    List<string> replaceFilesSourceSplit = null;
                    List<string> replaceFilesTargetSplit = null;
                    if (replaceFilesSourceList != null && replaceFilesTargetList != null)
                    {
                        //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                        replaceFilesSourceSplit = replaceFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        replaceFilesTargetSplit = replaceFilesTargetList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        if (replaceFilesSourceSplit.Count != replaceFilesTargetSplit.Count)
                        {
                            //Mismatched source and target lists
                            M3Log.Error($@"Mod has job header ({headerAsString}) that has mismatched newfiles and replacefiles descriptor lists. newfiles has {replaceFilesSourceSplit.Count} items, replacefiles has {replaceFilesTargetSplit.Count} items. The number of items in each list must match.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerHasMismatchedNewFilesReplaceFiles, headerAsString, replaceFilesSourceSplit.Count.ToString(), replaceFilesTargetSplit.Count.ToString());
                            return;
                        }

                        M3Log.Information($@"Parsing replacefiles/newfiles on {headerAsString}. Found {replaceFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                    }

                    //Don't support add files on anything except ME3 (due to legacy implementation), unless basegame.
                    List<string> addFilesSourceSplit = null;
                    List<string> addFilesTargetSplit = null;
                    if (Game == MEGame.ME3 || header == ModJob.JobHeader.BASEGAME)
                    {

                        if (addFilesSourceList != null && addFilesTargetList != null)
                        {
                            //Parse the addfiles and addfilestargets list and ensure they have the same number of elements in them.
                            addFilesSourceSplit = addFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            addFilesTargetSplit = addFilesTargetList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            if (addFilesSourceSplit.Count != addFilesTargetSplit.Count)
                            {
                                //Mismatched source and target lists
                                M3Log.Error($@"Mod has job header ({headerAsString}) that has mismatched addfiles and addfilestargets descriptor lists. addfiles has {addFilesSourceSplit.Count} items, addfilestargets has {addFilesTargetSplit.Count} items. The number of items in each list must match.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerHasMismatchedAddFilesAddFilesTargets, headerAsString, addFilesSourceSplit.Count.ToString(), addFilesTargetSplit.Count.ToString());
                                return;
                            }

                            M3Log.Information($@"Parsing addfiles/addfilestargets on {headerAsString}. Found {addFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                        }

                        // Mod Manager 8: Removed code for 'addfilesreadonlytargets'. It was never implemented in Mod Manager 6
                        // and thus hasn't worked for years. It was only used by Expanded Galaxy Mod in Original Trilogy and doesn't
                        // have a functional impact on mod installs

                        //Ensure TESTPATCH is supported by making sure we are at least on ModDesc 3 if using TESTPATCH header.
                        //ME3 only
                        if (ModDescTargetVersion < 3 && header == ModJob.JobHeader.TESTPATCH)
                        {
                            M3Log.Error($@"Mod has job header ({headerAsString}) specified, but this header is only supported when targeting ModDesc 3 or higher.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerUnsupportedOnLTModdesc3, headerAsString);
                            return;
                        }

                    }

                    //This was introduced in Mod Manager 4.1 but is considered applicable to all moddesc versions as it doesn't impact installation and is only for user convenience
                    //In Java Mod Manager, this required 4.1 moddesc
                    string jobRequirement = iniData[headerAsString][MODDESC_DESCRIPTOR_JOB_JOBDESCRIPTION];
                    M3Log.Information($@"Read job requirement text: {jobRequirement}", Settings.LogModStartup && jobRequirement != null);

                    ModJob headerJob = new ModJob(header, this);
                    // Editor only stuff
                    headerJob.NewFilesRaw = replaceFilesSourceList;
                    headerJob.ReplaceFilesRaw = replaceFilesTargetList;
                    headerJob.AddFilesRaw = addFilesSourceList;
                    headerJob.AddFilesTargetsRaw = addFilesTargetList;
                    headerJob.GameDirectoryStructureRaw = directoryMatchesGameStructure;

                    // End editor only stuff
                    headerJob.JobDirectory = jobSubdirectory.Replace('/', '\\');
                    headerJob.RequirementText = jobRequirement;

                    //Build replacements 
                    int jobDirLength = jobSubdirectory == @"." ? 0 : jobSubdirectory.Length;
                    if (replaceFilesSourceSplit != null)
                    {
                        for (int i = 0; i < replaceFilesSourceSplit.Count; i++)
                        {
                            if (directoryMatchesGameStructure)
                            {
                                var sourceDirectory = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory, replaceFilesSourceSplit[i]).Replace('/', '\\');
                                var destGameDirectory = replaceFilesTargetSplit[i].TrimEnd('/', '\\');
                                if (FilesystemInterposer.DirectoryExists(sourceDirectory, Archive))
                                {
                                    #region Pathing corrections
                                    if (replaceFilesSourceSplit.Count == 1 && replaceFilesSourceSplit[0] == @".")
                                    {
                                        // The replacement directory is the 'root' of moddir. This is effectively going to be length zero. We have to correct the string so substrings work properly on the length
                                        replaceFilesSourceSplit[0] = "";
                                    }
                                    #endregion
                                    var files = FilesystemInterposer.DirectoryGetFiles(sourceDirectory, @"*.*", SearchOption.AllDirectories, Archive).Select(x => x.Substring((ModPath.Length > 0 ? (ModPath.Length + 1) : 0) + jobDirLength).TrimStart('\\')).ToList();
                                    foreach (var file in files)
                                    {
                                        if (GameFileExtensions.Contains(Path.GetExtension(file), StringComparer.InvariantCultureIgnoreCase))
                                        {
                                            // We use trimstart here as the split might be an empty string.
                                            var destFile = destGameDirectory + Path.DirectorySeparatorChar + file.Substring(replaceFilesSourceSplit[i].Length).TrimStart('\\', '/');
                                            M3Log.Information($@"Adding file to job replace files list: {file} => {destFile}", Settings.LogModStartup);
                                            string failurereason = headerJob.AddPreparsedFileToInstall(destFile, file, this);
                                            if (failurereason != null)
                                            {
                                                M3Log.Error($@"Error occurred while automapping the replace files lists for {headerAsString}: {failurereason}. This is likely a bug in M3, please report it to Mgamerz");
                                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_errorAutomappingPleaseReport, headerAsString, failurereason);
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            M3Log.Warning(@"File type/extension not supported by gamedirectorystructure scan: " + file);
                                        }
                                    }
                                }
                                else
                                {
                                    M3Log.Error($@"Error occurred while parsing the replace files lists for {headerAsString}: source directory {sourceDirectory} was not found and the gamedirectorystructure flag was used on this job.");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_sourceDirectoryForJobNotFound, headerAsString, sourceDirectory);
                                    return;
                                }
                                if (!headerJob.FilesToInstall.Any())
                                {
                                    M3Log.Error($@"Error using gamedirectorystructure option: No files were found to install in the specified path for job: {headerAsString}, Path: {sourceDirectory}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_gamedirectoryStructureFoundNoFiles, headerAsString, sourceDirectory);
                                    return;
                                }
                            }
                            else
                            {
                                string destFile = replaceFilesTargetSplit[i];
                                M3Log.Information($@"Adding file to job installation queue: {replaceFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                                string failurereason = headerJob.AddFileToInstall(destFile, replaceFilesSourceSplit[i], this);
                                if (failurereason != null)
                                {
                                    M3Log.Error($@"Error occurred while parsing the replace files lists for {headerAsString}: {failurereason}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailureParsingLists, headerAsString, failurereason);
                                    return;
                                }
                            }
                        }


                    }

                    //Build additions (vars will be null if these aren't supported by target version)
                    if (addFilesSourceSplit != null && !directoryMatchesGameStructure)
                    {
                        for (int i = 0; i < addFilesSourceSplit.Count; i++)
                        {
                            string destFile = addFilesTargetSplit[i];
                            M3Log.Information($@"Adding file to installation queue (addition): {addFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                            string failurereason = headerJob.AddAdditionalFileToInstall(destFile, addFilesSourceSplit[i], this); //add files are layered on top
                            if (failurereason != null)
                            {
                                M3Log.Error($@"Error occurred while parsing the add files lists for {headerAsString}: {failurereason}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailedToParseAddFilesLists, headerAsString, failurereason);
                                return;
                            }
                        }
                    }

                    //MultiLists: Mod Manager 6.0
                    //Must be parsed before AltFile as AltFiles can access these lists.
                    if (ModDescTargetVersion >= 6.0)
                    {
                        int i = 1;
                        while (true)
                        {
                            var multilist = iniData[headerAsString][MODDESC_DESCRIPTOR_ALTERNATE_MULTILIST + i];
                            if (multilist == null) break; //no more to parse
                            headerJob.MultiLists[i] = multilist.Split(';');
                            i++;
                        }
                    }

                    // Mod Manager 7: Merge Mods
                    if (!string.IsNullOrWhiteSpace(mergeModsList))
                    {
                        var mergeSplit = mergeModsList.Split(';');
                        foreach (var mergeItem in mergeSplit)
                        {
                            // Security checks
                            var mergeItemSanitized = mergeItem.TrimStart('/', '\\');
                            if (mergeItemSanitized.Contains(@".."))
                            {
                                // This entry may be malicious, do not load
                                M3Log.Error($@"{ModName} has a merge mod listed under header {headerAsString} which contains a '..' in the name. '..' is not allowed in names.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_securityIssueMergeModDotDot, ModName, headerAsString);
                                return;
                            }


                            var fullPath = FilesystemInterposer.PathCombine(Archive != null, ModPath, Mod.MergeModFolderName, mergeItem);
                            if (!FilesystemInterposer.FileExists(fullPath, Archive))
                            {
                                M3Log.Error($@"{ModName} has a merge mod listed under header {headerAsString} which does not exist in the {Mod.MergeModFolderName} folder: {mergeItemSanitized}.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_mergeModNotFound, ModName, headerAsString, Mod.MergeModFolderName, mergeItemSanitized);
                                return;
                            }

                            var mergeMod = LoadMergeMod(fullPath);
                            if (mergeMod == null)
                                return; // Load failed, handled in LoadMergeMod()
                            M3Log.Information($@"Loaded merge mod {fullPath}", Settings.LogMixinStartup);
                            headerJob.MergeMods.Add(mergeMod);
                        }
                    }

                    //Altfiles: Mod Manager 4.2
                    if (!string.IsNullOrEmpty(altfilesStr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                        if (splits.Count == 0)
                        {
                            M3Log.Error($@"Alternate files list was unable to be parsed for header {headerAsString}, no items were returned from parenthesis parser.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_failedToParseAltfiles, headerAsString);
                            return;
                        }
                        foreach (var split in splits)
                        {
                            AlternateFile af = new AlternateFile(split, headerJob, this);
                            if (af.ValidAlternate)
                            {
                                headerJob.AlternateFiles.Add(af);
                            }
                            else
                            {
                                //Error is logged in constructor of AlternateFile
                                LoadFailedReason = af.LoadFailedReason;
                                return;
                            }
                        }
                    }

                    if (!headerJob.ValidateAlternates(this, out string failureReason))
                    {
                        LoadFailedReason = failureReason;
                        return;
                    }

                    M3Log.Information($@"Successfully made mod job for {headerAsString}", Settings.LogModStartup);
                    InstallationJobs.Add(headerJob);
                }

                // 04/18/2023: Add check for 'mergemods' set in basegame job without a job header, make mod fail to load if 
                // jobdir is not specified, as a way to cue user into needing a value for it
                // This is
                if (ModDescTargetVersion >= 8.1 && header == ModJob.JobHeader.BASEGAME && jobSubdirectory == null &&
                    !string.IsNullOrWhiteSpace(iniData[headerAsString][MODDESC_DESCRIPTOR_BASEGAME_MERGEMODS]))
                {
                    M3Log.Error(@"Mod specifies basegame mergemods descriptor but does not set basegame moddir, setting mod as invalid to prevent misleading behavior");
                    LoadFailedReason = M3L.GetString(M3L.string_mod_validation_basegameMergeModsWithoutModDir);
                    return;
                }
            }

            #endregion

            #region CUSTOMDLC

            if (ModDescTargetVersion >= 3.1)
            {
                var customDLCSourceDirsStr = iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS];
                var customDLCDestDirsStr = iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS];
                //ALT DLC: Mod Manager 4.4
                //This behavior changed in Mod Manager 6 to allow no sourcedirs/destdirs if a custom dlc will only be added on a condition
                string altdlcstr = (ModDescTargetVersion >= 4.4) ? iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_ALTDLC] : null;


                if ((customDLCSourceDirsStr != null && customDLCDestDirsStr != null) || !string.IsNullOrEmpty(altdlcstr))
                {
                    M3Log.Information(@"Found CUSTOMDLC header", Settings.LogModStartup);
                    ModJob customDLCjob = new ModJob(ModJob.JobHeader.CUSTOMDLC, this);

                    if (customDLCSourceDirsStr != null && customDLCDestDirsStr != null)
                    {
                        var customDLCSourceSplit = customDLCSourceDirsStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        var customDLCDestSplit = customDLCDestDirsStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                        //Verify lists are the same length
                        if (customDLCSourceSplit.Count != customDLCDestSplit.Count)
                        {
                            //Mismatched source and target lists
                            M3Log.Error($@"Mod has job header (CUSTOMDLC) that has mismatched sourcedirs and destdirs descriptor lists. sourcedirs has {customDLCSourceSplit.Count} items, destdirs has {customDLCDestSplit.Count} items. The number of items in each list must match.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDLCMismatchedSourceDirsDestDirs, customDLCSourceSplit.Count.ToString(), customDLCDestSplit.Count.ToString());
                            return;
                        }

                        //Security check for ..
                        if (customDLCSourceSplit.Any(x => x.Contains(@"..")) || customDLCDestSplit.Any(x => x.Contains(@".")))
                        {
                            //Security violation: Cannot use .. in filepath
                            M3Log.Error(@"CUSTOMDLC header sourcedirs or destdirs includes item that contains a '..' (sourcedirs) or '.' (destdirs), which is not permitted.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_customDLCItemHasIllegalCharacters);
                            return;
                        }

                        // Security check for directory separators
                        if (customDLCDestSplit.Any(x => x.ContainsAny(DirectorySeparatorChars, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            //Security violation: Cannot use \ in filepath
                            M3Log.Error(@"CUSTOMDLC header destdirs contains a value that contains a directory separator character, which is not allowed.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_customdlcDotDotFound);
                            return;
                        }

                        //Verify folders exists
                        foreach (var f in customDLCSourceSplit)
                        {
                            if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, f), Archive))
                            {
                                M3Log.Error($@"Mod has job header (CUSTOMDLC) sourcedirs descriptor specifies installation of a Custom DLC folder that does not exist in the mod folder: {f}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDLCSourceDirMissing, f);
                                return;
                            }
                        }

                        //Security check: Protected folders
                        foreach (var f in customDLCDestSplit)
                        {
                            if (M3Utilities.IsProtectedDLCFolder(f, Game))
                            {
                                M3Log.Error($@"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder to a protected target: {f}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_destDirCannotBeMetadataOrOfficialDLC, f);
                                return;
                            }

                            if (!f.StartsWith(@"DLC_"))
                            {
                                M3Log.Error($@"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder that would install a disabled DLC: {f}. DLC folders must start with DLC_.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_destDirFoldernamesMustStartWithDLC, f);
                                return;
                            }
                        }
                        for (int i = 0; i < customDLCSourceSplit.Count; i++)
                        {
                            // COMMIT REVIEW: Make sure this didn't change in string replacement of ]] 12/23/2023
                            customDLCjob.CustomDLCFolderMapping[customDLCSourceSplit[i]] = customDLCDestSplit[i];
                        }
                    }

                    //MultiLists: Mod Manager 6.0
                    //Must be parsed before Alternates as they can access these lists
                    if (ModDescTargetVersion >= 6.0)
                    {
                        int i = 1;
                        while (true)
                        {
                            var multilist = iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][MODDESC_DESCRIPTOR_ALTERNATE_MULTILIST + i];
                            if (multilist == null) break; //no more to parse
                            customDLCjob.MultiLists[i] = multilist.Split(';');
                            i++;
                        }
                    }

                    //Altfiles: Mod Manager 4.2
                    string altfilesStr = (ModDescTargetVersion >= 4.2) ? iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_ALTFILES] : null;
                    if (!string.IsNullOrEmpty(altfilesStr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                        if (splits.Count == 0)
                        {
                            M3Log.Error(@"Alternate files list was unable to be parsed, no items were returned from parenthesis parser.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_altFilesListFailedToParse);
                            return;
                        }
                        foreach (var split in splits)
                        {
                            AlternateFile af = new AlternateFile(split, customDLCjob, this);
                            if (af.ValidAlternate)
                            {
                                customDLCjob.AlternateFiles.Add(af);
                            }
                            else
                            {
                                //Error is logged in constructor of AlternateFile
                                LoadFailedReason = af.LoadFailedReason;
                                return;
                            }
                        }
                    }


                    //AltDLC: Mod Manager 4.4
                    if (!string.IsNullOrEmpty(altdlcstr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altdlcstr);
                        foreach (var split in splits)
                        {
                            AlternateDLC af = new AlternateDLC(split, this, customDLCjob);
                            if (af.ValidAlternate)
                            {
                                customDLCjob.AlternateDLCs.Add(af);
                            }
                            else
                            {
                                //Error is logged in constructor of AlternateDLC
                                LoadFailedReason = af.LoadFailedReason;
                                return;
                            }
                        }
                    }

                    //Custom DLC names: Mod Manager 6 (but can be part of any spec as it's only cosmetic)
                    HumanReadableCustomDLCNames = iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC].Where(x => x.KeyName.StartsWith(@"DLC_")).ToDictionary(mc => mc.KeyName, mc => mc.Value);

                    if (!customDLCjob.ValidateAlternates(this, out string failureReason))
                    {
                        LoadFailedReason = failureReason;
                        return;
                    }

                    M3Log.Information($@"Successfully made mod job for CUSTOMDLC", Settings.LogModStartup);
                    InstallationJobs.Add(customDLCjob);
                }
                else if ((customDLCSourceDirsStr != null) != (customDLCDestDirsStr != null))
                {
                    M3Log.Error($@"{ModName} specifies only one of the two required lists for the CUSTOMDLC header. Both sourcedirs and destdirs descriptors must be set for CUSTOMDLC.");
                    LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_mustHaveBothSourceAndDestDirs);
                    return;
                }
            }

            #endregion

            #region BALANCE_CHANGES (ME3 ONLY)

            var balanceChangesDirectory = (Game == MEGame.ME3 && ModDescTargetVersion >= 4.3) ? iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()][Mod.MODDESC_DESCRIPTOR_JOB_DIR] : null;
            if (balanceChangesDirectory != null)
            {
                M3Log.Information(@"Found BALANCE_CHANGES header", Settings.LogModStartup);
                M3Log.Information(@"Subdirectory (moddir): " + balanceChangesDirectory, Settings.LogModStartup);
                //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                //In MM5.1 or lower you would have to specify the target. In MM6 you can only specify a single source and it must be a .bin file.
                string replaceFilesSourceList = iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()][Mod.MODDESC_DESCRIPTOR_JOB_NEWFILES];
                if (replaceFilesSourceList != null)
                {
                    //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                    var replaceFilesSourceSplit = replaceFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (replaceFilesSourceSplit.Count == 1)
                    {
                        //Only 1 file is allowed here.
                        string balanceFile = replaceFilesSourceSplit[0];
                        if (!balanceFile.EndsWith(@".bin"))
                        {
                            //Invalid file
                            M3Log.Error(@"Balance changes file must be a .bin file.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_balanceChangesFileNotBinFile);
                            return;
                        }
                        ModJob balanceJob = new ModJob(ModJob.JobHeader.BALANCE_CHANGES);
                        balanceJob.JobDirectory = balanceChangesDirectory;
                        M3Log.Information($@"Adding file to job installation queue: {balanceFile} => Binaries\win32\asi\ServerCoalesced.bin", Settings.LogModStartup);

                        string failurereason = balanceJob.AddFileToInstall(@"Binaries\win32\asi\ServerCoalesced.bin", balanceFile, this);
                        if (failurereason != null)
                        {
                            M3Log.Error($@"Error occurred while creating BALANCE_CHANGE job: {failurereason}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericErrorCreatingBalanceChangeJob, failurereason);
                            return;
                        }
                        M3Log.Information($@"Successfully made mod job for {balanceJob.Header}", Settings.LogModStartup);
                        InstallationJobs.Add(balanceJob);
                        balanceJob.BalanceChangesFileRaw = balanceFile;
                    }
                    else
                    {
                        M3Log.Error($@"Balance changes newfile descriptor only allows 1 entry in the list, but {replaceFilesSourceSplit.Count} were parsed.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_canOnlyHaveOneBalanceChangesFile, replaceFilesSourceSplit.Count.ToString());
                        return;
                    }
                }
            }


            #endregion

            #region CONFIG FILES FOR ME1 AND ME2

            if (ModDescTargetVersion >= 6 && Game < MEGame.ME3)
            {

                if (Game == MEGame.ME1)
                {
                    var jobSubdirectory = iniData[ModJob.JobHeader.ME1_CONFIG.ToString()][Mod.MODDESC_DESCRIPTOR_JOB_DIR];
                    if (!string.IsNullOrWhiteSpace(jobSubdirectory))
                    {
                        // 12/23/2023 - Change from hardcoded string to jobheader tostring
                        var configfilesStr = iniData[ModJob.JobHeader.ME1_CONFIG.ToString()][MODDESC_DESCRIPTOR_ME1CONFIG_CONFIGFILES];
                        if (string.IsNullOrWhiteSpace(configfilesStr))
                        {
                            M3Log.Error(@"ME1_CONFIG job was specified but configfiles descriptor is empty or missing. Remove this header if you are not using this task.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cannotHaveEmptyME1ConfigJob);
                            return;
                        }
                        var configFilesSplit = configfilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        ModJob me1ConfigJob = new ModJob(ModJob.JobHeader.ME1_CONFIG, this);
                        me1ConfigJob.JobDirectory = jobSubdirectory;
                        foreach (var configFilename in configFilesSplit)
                        {
                            if (allowedConfigFilesME1.Contains(configFilename, StringComparer.InvariantCultureIgnoreCase))
                            {
                                var failurereason = me1ConfigJob.AddFileToInstall(configFilename, configFilename, this);
                                if (failurereason != null)
                                {
                                    M3Log.Error($@"Error occurred while creating ME1_CONFIG job: {failurereason}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericErrorReadingME1ConfigJob, failurereason);
                                    return;
                                }
                            }
                            else
                            {
                                M3Log.Error(@"ME1_CONFIG job's configfiles descriptor contains an unsupported config file: " + configFilename);
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_unsupportedME1ConfigFileSpecified, configFilename);
                                return;
                            }
                        }
                        M3Log.Information($@"Successfully made mod job for {ModJob.JobHeader.ME1_CONFIG}", Settings.LogModStartup);
                        InstallationJobs.Add(me1ConfigJob);
                        me1ConfigJob.ConfigFilesRaw = configfilesStr;
                    }
                }

                if (Game == MEGame.ME2)
                {
                    var rcwfile = iniData[ModJob.JobHeader.ME2_RCWMOD.ToString()][MODDESC_DESCRIPTOR_ME2RCW_MODFILE];
                    if (!string.IsNullOrWhiteSpace(rcwfile))
                    {
                        var path = FilesystemInterposer.PathCombine(IsInArchive, ModPath, rcwfile);
                        if (!FilesystemInterposer.FileExists(path, Archive))
                        {
                            M3Log.Error(@"ME2_RCWMOD job was specified, but the specified file doesn't exist: " + rcwfile);
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_specifiedRCWFileDoesntExist, rcwfile);
                            return;
                        }

                        if (IsInArchive)
                        {
                            M3Log.Error(@"Cannot load compressed RCW through main mod loader.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cannotLoadCompressedRCWModThroughMainLoaderLikelyBug);
                            return;
                        }
                        var rcwMods = RCWMod.LoadRCWMods(path);
                        if (rcwMods.Count != 1)
                        {
                            M3Log.Error(@"M3-mod based RCW mods may only contain 1 RCW mod each. Importing should split these into multiple single mods.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_rcwModsMayOnlyContainOneRCWMod);
                            return;
                        }

                        ModJob rcwJob = new ModJob(ModJob.JobHeader.ME2_RCWMOD);
                        rcwJob.RCW = rcwMods[0];
                        InstallationJobs.Add(rcwJob);
                        M3Log.Information(@"Successfully made RCW mod job for " + rcwJob.RCW.ModName, Settings.LogModStartup);
                    }
                }
            }


            #endregion

            #region LOCALIZATION MODS (6.1+) (ME2/3 ONLY)

            // 06/11/2022 - Change from >= ME2 to IsGame2() and IsGame3()
            var localizationFilesStr = ((Game.IsGame2() || Game.IsGame3()) && ModDescTargetVersion >= 6.1) ? iniData[ModJob.JobHeader.LOCALIZATION.ToString()][Mod.MODDESC_DESCRIPTOR_LOCALIZATION_FILES] : null;
            if (localizationFilesStr != null)
            {
                if (InstallationJobs.Any())
                {
                    M3Log.Error(@"Cannot have LOCALIZATION task with other tasks. LOCALIZATION jobs must be on their own.");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_cannotCombineLocalizationTask);
                    return;
                }
                var destDlc = iniData[ModJob.JobHeader.LOCALIZATION.ToString()][Mod.MODDESC_DESCRIPTOR_LOCALIZATION_TARGETDLC];
                if (string.IsNullOrWhiteSpace(destDlc))
                {
                    M3Log.Error(@"LOCALIZATION header requires 'dlcname' descriptor that the localization file will target.");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_missingDlcNameDesriptor);
                    return;
                }

                if (!destDlc.StartsWith(@"DLC_") || MEDirectories.OfficialDLC(Game).Any(x => x.Equals(destDlc, StringComparison.InvariantCultureIgnoreCase)))
                {
                    M3Log.Error($@"The destdlc descriptor under LOCALIZATION must start with DLC_ and not be an official DLC for the game. Invalid value: {destDlc}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_invalidDlcNameLocalization, destDlc);
                    return;
                }

                // Files check
                var locFiles = StringStructParser.GetSemicolonSplitList(localizationFilesStr);
                ModJob localizationJob = new ModJob(ModJob.JobHeader.LOCALIZATION);
                foreach (var f in locFiles)
                {
                    // Review on 12/23/2023 - is this right? Does LOCALIZATION folder not have a jobdir?
                    var filePath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, f);
                    if (!FilesystemInterposer.FileExists(filePath, Archive))
                    {
                        M3Log.Error($@"A referenced localization file could not be found: {f}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validaiton_modparsing_referencedLocalizationFileCouldNotBeFound, f);
                        return;
                    }

                    var fname = Path.GetFileName(f);
                    if (!fname.EndsWith(@".tlk"))
                    {
                        M3Log.Error($@"Referenced localization file is not a .tlk: {f}. LOCALIZATION tasks only allow installation of .tlk files.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_invalidLocalizationFileType, f);
                        return;
                    }

                    if (Game == MEGame.ME3)
                    {
                        if (!fname.StartsWith(destDlc + @"_"))
                        {
                            M3Log.Error($@"Referenced localization file has incorrect name: {f}. Localization filenames must begin with the name of the DLC, followed by an underscore and then the three letter language code.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_invalidLocalizationFilename, f);
                            return;
                        }
                    }
                    else if (Game == MEGame.ME2)
                    {
                        // Read bioengine before install maybe?
                        // We need to know the module number
                        // TODO: FIX THIS FOR ME2
                    }


                    var failurereason = localizationJob.AddFileToInstall($@"BIOGame/DLC/{destDlc}/{MEDirectories.CookedName(Game)}/{fname}", f, this);
                    if (failurereason != null)
                    {
                        M3Log.Error($@"Error occurred while adding file for LOCALIZATION job: {failurereason}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_errorOccuredWhileAddingFileForLOCALIZATIONJobX, failurereason);
                        return;
                    }
                }

                localizationJob.LocalizationFilesStrRaw = localizationFilesStr;
                InstallationJobs.Add(localizationJob);
                RequiredDLC.Add(new DLCRequirement() { DLCFolderName = destDlc }); //Add DLC requirement.
            }
            #endregion

            #region GAME1_TLK_UPDATES
            // 12/23/2023 - actually check value of the bool being true
            if (Game is MEGame.LE1 && ModDescTargetVersion >= 7.0 && bool.TryParse(iniData[ModJob.JobHeader.GAME1_EMBEDDED_TLK.ToString()][MODDESC_DESCRIPTOR_GAME1TLK_USESFEATURE], out var usesGame1TlkFeature) && usesGame1TlkFeature)
            {
                if (!ParseGame1TLKMerges())
                {
                    return; // Stop parsing.
                }
            }

            #endregion

            #region Additional Alternates Validation (requires jobs to have loaded)
            // Validate and resolve names of all alternate dependencies now that the mod has loaded all jobs and alternates (Mod Manager 8.0)
            if (ModDescTargetVersion >= 8.0)
            {
                var allAlternates = InstallationJobs.SelectMany(x => x.GetAllAlternates()).ToList();

                // Validate the depends on
                foreach (var alternate in allAlternates)
                {
                    if (!alternate.SetupAndValidateDependsOnText(this, allAlternates))
                    {
                        // Validation message is set in the validation method, so we only return here.
                        return;
                    }
                }

                // Validate the sort orders being unique.
                // OptionGroups must all have same sort index (or be undefined)
                var indexedAlternates = allAlternates.Where(x => x.SortIndex > 0);

                // 
                foreach (var indexedAlternate in indexedAlternates)
                {
                    var collision = indexedAlternates.FirstOrDefault(x => x.SortIndex == indexedAlternate.SortIndex && x != indexedAlternate && (x.GroupName == null || x.GroupName != indexedAlternate.GroupName));
                    if (collision != null)
                    {
                        M3Log.Error($@"Alternate {indexedAlternate.FriendlyName} specifies a non-unique sortindex value '{indexedAlternate.SortIndex}'. {collision.FriendlyName} also uses this sortindex value, these values must be unique.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_nonUniqueSortIndex, indexedAlternate.FriendlyName, indexedAlternate.SortIndex, collision.FriendlyName);
                        return;
                    }
                }

                var groupedIndexed = indexedAlternates.Where(x => x.GroupName != null).GroupBy(x => x.GroupName);
                foreach (var groupPair in groupedIndexed.Where(x => x.Count() > 1)) // Only groups with multiple sortindices are counted, since if there is only one it'll always be valid.
                {
                    // Ensure all indices are the same or zero
                    int setIndex = 0;
                    foreach (var option in groupPair)
                    {
                        if (setIndex == 0)
                        {
                            setIndex = option.SortIndex;
                        }
                        else
                        {
                            if (setIndex != option.SortIndex)
                            {
                                // They mismatch
                                M3Log.Error($@"Alternate {option.FriendlyName} specifies a 'sortindex' value that differs from another one in group '{option.GroupName}'. Alternate specifies sortindex {option.SortIndex}, but another specifies {setIndex}. 'sortindex' values in a group must all be the same, or only one defined.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_sortIndexGroupUnique, option.FriendlyName, option.GroupName, option.SortIndex, setIndex);
                                return;
                            }
                        }
                    }
                }
            }

            // For performance reasons we do not validate TLK merge keys in m3za files
            // except at install time as we would have to extract m3za and read its header.
            // During development mods will use loose xml files which we validate here


            if (ModDescTargetVersion >= 9.0 && Game == MEGame.LE1)
            {
                // Validate TLK option keys
                var tlkJob = GetJob(ModJob.JobHeader.GAME1_EMBEDDED_TLK);
                var custDlcJob = GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (tlkJob != null && custDlcJob != null)
                {
                    var tlkJobPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName);
                    var m3zaPath = FilesystemInterposer.PathCombine(IsInArchive, tlkJobPath, Mod.Game1EmbeddedTlkCompressedFilename);
                    if (!FilesystemInterposer.FileExists(m3zaPath, Archive))
                    {
                        foreach (var alt in custDlcJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ENABLE_TLKMERGE_OPTIONKEY))
                        {
                            var keyPath = FilesystemInterposer.PathCombine(IsInArchive, tlkJobPath, alt.LE1TLKOptionKey);
                            if (!FilesystemInterposer.DirectoryExists(keyPath))
                            {
                                M3Log.Error($@"Alternate {alt.FriendlyName} specifies a '{AlternateKeys.ALTDLC_LE1TLK_OPTIONKEY}' value that references folder in {Mod.Game1EmbeddedTlkFolderName} that does not exist");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altSpecifiesValueFolderDoesntExist, alt.FriendlyName, AlternateKeys.ALTDLC_LE1TLK_OPTIONKEY, Mod.Game1EmbeddedTlkFolderName);
                                return;
                            }
                        }
                    }
                }
            }

            #endregion

            #region Texture Mod References

            if (Game.IsLEGame() && ModDescTargetVersion >= 8.1)
            {
                // 12/23/2023 - Change from hardcoded 'TEXTUREMODS' string to job header enum
                var textureModsStruct = iniData[ModJob.JobHeader.TEXTUREMODS.ToString()][MODDESC_DESCRIPTOR_TEXTURESMODS_FILES];
                if (!string.IsNullOrWhiteSpace(textureModsStruct))
                {
                    M3Log.Information(@"Found [TEXTUREMODS] job header", Settings.LogModStartup);
                    ModJob texJob = new ModJob(ModJob.JobHeader.TEXTUREMODS, this);
                    var tmSplit = StringStructParser.GetParenthesisSplitValues(textureModsStruct);
                    foreach (var tm in tmSplit)
                    {
                        M3Log.Information($@"TextureMods: Instantiating M3MEMMod object from struct {tm}", Settings.LogModStartup);
                        var mm = new M3MEMMod(this, tm);
                        if (mm.ValidationFailedReason != null)
                        {
                            // Mod fails to load due to validation failure
                            LoadFailedReason = mm.ValidationFailedReason;
                            return;
                        }

                        M3Log.Information($@"TextureMods: Added texture mod reference for {mm.Title}", Settings.LogModStartup);
                        texJob.TextureModReferences.Add(mm);
                    }
                    InstallationJobs.Add(texJob);
                }
            }

            #endregion

            #region Headmorphs
            // This is LE only cause save files are a pain in the arse for OT
            if (Game.IsLEGame() && ModDescTargetVersion >= 8.1)
            {
                var headmorphReferenceStruct = iniData[ModJob.JobHeader.HEADMORPHS.ToString()][MODDESC_DESCRIPTOR_HEADMORPH_FILES];
                if (!string.IsNullOrWhiteSpace(headmorphReferenceStruct))
                {
                    ModJob headmorphJob = new ModJob(ModJob.JobHeader.HEADMORPHS, this);

                    var tmSplit = StringStructParser.GetParenthesisSplitValues(headmorphReferenceStruct);
                    foreach (var tm in tmSplit)
                    {
                        var mm = new M3Headmorph(this, tm);
                        if (mm.ValidationFailedReason != null)
                        {
                            // Mod fails to load due to validation failure
                            LoadFailedReason = mm.ValidationFailedReason;
                            return;
                        }

                        headmorphJob.HeadMorphFiles.Add(mm);
                    }

                    InstallationJobs.Add(headmorphJob);
                }
            }

            #endregion

            #endregion

            #region Additional Mod Items

            // Required DLC (Mod Manager 5.0)
            var requiredDLCText = ModDescTargetVersion >= 5.0 ? iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_REQUIREDDLC] : null;
            if (!string.IsNullOrWhiteSpace(requiredDLCText))
            {
                var requiredDlcsSplit = requiredDLCText.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var reqDLC in requiredDlcsSplit)
                {
                    var reqDLCss = reqDLC;
                    var list = RequiredDLC;
                    if (ModDescTargetVersion >= 6.2) // This feature requires M3 6.2
                    {
                        if (reqDLCss.StartsWith('?'))
                        {
                            reqDLCss = reqDLCss.Substring(1); //? means the DLC is optional, but one item prefixed with ? must be installed.
                            list = OptionalSingleRequiredDLC;
                        }
                    }

                    switch (Game)
                    {
                        case MEGame.ME1:
                            if (Enum.TryParse(reqDLCss, out ModJob.JobHeader header1) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME1).TryGetValue(header1, out var foldername1))
                            {
                                list.Add(new DLCRequirement() { DLCFolderName = foldername1 });
                                M3Log.Information(@"Adding DLC requirement to mod: " + foldername1, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME2:
                            if (Enum.TryParse(reqDLCss, out ModJob.JobHeader header2) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME2).TryGetValue(header2, out var foldername2))
                            {
                                list.Add(new DLCRequirement() { DLCFolderName = foldername2 });
                                M3Log.Information(@"Adding DLC requirement to mod: " + foldername2, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME3:
                            if (Enum.TryParse(reqDLCss, out ModJob.JobHeader header3) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME3).TryGetValue(header3, out var foldername3))
                            {
                                list.Add(new DLCRequirement() { DLCFolderName = foldername3 });
                                M3Log.Information(@"Adding DLC requirement to mod: " + foldername3, Settings.LogModStartup);
                                continue;
                            }
                            break;

                            // Headers for official jobs are not supported in LE games
                    }



                    if (!reqDLCss.StartsWith(@"DLC_"))
                    {
                        M3Log.Error(@"Required DLC does not match officially supported header or start with DLC_.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_invalidRequiredDLCSpecified, reqDLC);
                        return;
                    }

                    // ModDesc 8.0: LE games cannot depend on vanilla DLC being installed.
                    if (Game.IsLEGame() && ModDescTargetVersion >= 8.0)
                    {
                        if (MEDirectories.OfficialDLC(Game).Contains(reqDLCss, StringComparer.InvariantCultureIgnoreCase))
                        {
                            M3Log.Error($@"Legendary Edition mods targeting moddesc 8.0 or higher cannot mark official vanilla DLC as a dependency, as Mod Manager does not support these DLC being removed. Remove vanilla DLC items from 'requireddlc'. Invalid value: {reqDLCss}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_cannotDependOnOfficialLEDLC, reqDLCss);
                            return;
                        }
                    }
                    M3Log.Information(@"Adding DLC requirement to mod: " + reqDLCss, Settings.LogModStartup);
                    if (ModDescTargetVersion >= 9.0)
                    {
                        list.Add(DLCRequirement.ParseRequirementKeyed(reqDLCss, ModDescTargetVersion));
                    }
                    else
                    {
                        // Mod Manager 8.2 and below
                        list.Add(DLCRequirement.ParseRequirement(reqDLCss, ModDescTargetVersion >= 8.0, false));
                    }
                }
            }

            // Outdated DLC (Mod Manager 4.4)
            var outdatedDLCText = ModDescTargetVersion >= 4.4 ? iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][MODDESC_DESCRIPTOR_CUSTOMDLC_OUTDATEDDLC] : null;
            if (!string.IsNullOrEmpty(outdatedDLCText))
            {
                var outdatedCustomDLCDLCSplits = outdatedDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var outdated in outdatedCustomDLCDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(outdated, StringComparer.InvariantCultureIgnoreCase))
                    {
                        M3Log.Error($@"Outdated Custom DLC cannot contain an official DLC: " + outdated);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDlcCAnnotListOfficialDLCAsOutdated, outdated);
                        return;
                    }
                }
                OutdatedCustomDLC.ReplaceAll(outdatedCustomDLCDLCSplits);

            }

            // Incompatible DLC (Mod Manager 6)
            var incompatibleDLCText = ModDescTargetVersion >= 6.0 ? iniData[Mod.MODDESC_HEADERKEY_CUSTOMDLC][MODDESC_DESCRIPTOR_CUSTOMDLC_INCOMPATIBLEDLC] : null;
            if (!string.IsNullOrEmpty(incompatibleDLCText))
            {
                var incompatibleDLCSplits = incompatibleDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var incompat in incompatibleDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                    {
                        M3Log.Error($@"Incompatible Custom DLC cannot contain an official DLC: " + incompat);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDlcCannotListOfficialDLCAsIncompatible, incompat);
                        return;
                    }
                }
                IncompatibleDLC.ReplaceAll(incompatibleDLCSplits);
            }


            //Additional Deployment Folders (Mod Manager 5.1)
            var additonaldeploymentfoldersStr = ModDescTargetVersion >= 5.1 ? iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FOLDERS] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfoldersStr))
            {
                var addlFolderSplit = additonaldeploymentfoldersStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var addlFolder in addlFolderSplit)
                {
                    //Todo: Check to make sure this isn't contained by one of the jobs or alt files
                    if (addlFolder.Contains(@"..") || addlFolder.Contains(@"/") || addlFolder.Contains(@"\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        M3Log.Error($@"UPDATES header additionaldeploymentfolders includes directory ({addlFolder}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_illegalAdditionalDeploymentFoldersValue, addlFolder);
                        return;
                    }

                    //Check folder exists
                    if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFolder), Archive))
                    {
                        M3Log.Error($@"UPDATES header additionaldeploymentfolders includes directory that does not exist in the mod directory: {addlFolder}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_updatesSpecifiesMissingAdditionalDeploymentFolder, addlFolder);
                        return;
                    }

                    AdditionalDeploymentFolders = addlFolderSplit;
                }
            }

            //Additional Root Deployment Files (Mod Manager 6.0)
            //Todo: Update documentation
            var additonaldeploymentfilesStr = ModDescTargetVersion >= 6.0 ? iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FILES] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfilesStr))
            {
                var addlFileSplit = additonaldeploymentfilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var addlFile in addlFileSplit)
                {
                    if (addlFile.Contains(@"..") || addlFile.Contains(@"/") || addlFile.Contains(@"\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        M3Log.Error($@"UPDATES header additionaldeploymentfiles includes file ({addlFile}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_illegalAdditionalDeploymentFilesValue, addlFile);
                        return;
                    }

                    //Check file exists
                    if (!FilesystemInterposer.FileExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFile), Archive))
                    {
                        M3Log.Error($@"UPDATES header additionaldeploymentfiles includes file that does not exist in the mod directory: {addlFile}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_updatesSpecifiesMissingAdditionalDeploymentFiles, addlFile);
                        return;
                    }

                    AdditionalDeploymentFiles = addlFileSplit;
                }
            }

            #endregion

            #region Backwards Compatibilty

            // Mod Manager 2.0 supported "modcoal" flag that would replicate Mod Manager 1.0 functionality of coalesced swap since basegame jobs at the time
            // were not yet supported
            // When basegame jobs were introduced in 3.0 this flag would just convert to those instead.

            string modCoalFlag = ModDescTargetVersion == 2 ? iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_LEGACY_COALESCED] : null;
            //This check could be rewritten to simply check for non zero string. However, for backwards compatibility sake, we will keep the original
            //method of checking in place.
            if (modCoalFlag != null && Int32.TryParse(modCoalFlag, out int modCoalInt) && modCoalInt != 0)
            {
                M3Log.Information(@"Mod targets ModDesc 2.0, found modcoal flag", Settings.LogModStartup);
                if (!CheckAndCreateLegacyCoalescedJob())
                {
                    M3Log.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
                    return;
                }
            }
            #endregion

            #region Updater Service (Devs only)
            UpdaterServiceServerFolder = iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_UPDATES_SERVERFOLDER];
            var blacklistedFilesStr = iniData[MODDESC_HEADERKEY_UPDATES][MODDESC_DESCRIPTOR_UPDATES_BLACKLISTEDFILES];
            if (blacklistedFilesStr != null)
            {
                var blacklistedFiles = blacklistedFilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var blf in blacklistedFiles)
                {
                    var fullpath = Path.Combine(ModPath, blf);
                    if (File.Exists(fullpath))
                    {

                        M3Log.Error(@"Mod folder contains file that moddesc.ini blacklists: " + fullpath);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_blacklistedfileFound, fullpath);
                        return;
                    }
                }
                UpdaterServiceBlacklistedFiles.ReplaceAll(blacklistedFiles);
            }
            #endregion

            #region ASI features
            // ModDesc 9: Allow requesting ASI installation based on key/var mappings.
            if (ModDescTargetVersion >= 9.0 && Game.IsLEGame())
            {
                var asiModsList = iniData[MODDESC_HEADERKEY_ASIMODS][MODDESC_DESCRIPTOR_ASI_ASIMODSTOINSTALL];
                if (asiModsList != null)
                {
                    var asiList = StringStructParser.GetParenthesisSplitValues(asiModsList);
                    foreach (var asiStruct in asiList)
                    {
                        M3ASIVersion.Parse(this, asiStruct);
                        if (LoadFailedReason != null)
                        {
                            // ASI struct failed to parse
                            return;
                        }
                    }
                }
            }

            #endregion

            //What tool to launch post-install
            PostInstallToolLaunch = iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_POSTINSTALLTOOL];

            // Enhanced bink support
            if (ModDescTargetVersion >= 8.1 && (Game.IsLEGame() || Game == MEGame.LELauncher))
            {
                if (bool.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_REQUIRESENHANCEDBINK], out var usesEnhancedBink))
                {
                    // Marks mod are requiring the enhanced bink
                    RequiresEnhancedBink = usesEnhancedBink;
                    M3Log.Information(@"This mod requires the enhanced bink2w64 dll", Settings.LogModStartup && usesEnhancedBink);
                }
            }

            if (ModDescTargetVersion >= 9.0)
            {
                if (bool.TryParse(iniData[Mod.MODDESC_HEADERKEY_MODINFO][MODDESC_DESCRIPTOR_MODINFO_BATCHINSTALL_REVERSESORT], out var useReverseSort))
                {
                    BatchInstallUseReverseMountSort = useReverseSort;
                    M3Log.Information(@"This mod will use the highest mount priority instead of the lowest in batch mod sorting", Settings.LogModStartup && useReverseSort);
                }
            }

            // SECURITY CHECK
            #region TASK SILOING CHECK
            // Lordy this is gonna be messy
            foreach (var job in InstallationJobs)
            {
                if (ModJob.IsJobScoped(job))
                {
                    var siloScopes = ModJob.GetScopedSilos(job, Game);

                    // Ensure there is at least one silo. If there isn't, then the mod will never validate
                    if (!siloScopes.AllowedSilos.Any())
                    {
                        TelemetryInterposer.TrackEvent(@"Mod has no scoped silos", new Dictionary<string, string>(){
                            {@"Mod name", ModName},
                            {@"Mod version", ModVersionString},
                        });
                        M3Log.Fatal($@"{ModName}'s job {job.Header} does not have any allowed silos! This is a bug, all jobs that are scoped should have at least one silo. Please report this to Mgamerz.");
                        LoadFailedReason = $@"{ModName}'s job {job.Header} does not have any allowed silos! This is a bug, all jobs that are scoped should have at least one silo. Please report this to Mgamerz.";
                        return;
                    }

                    // Custom DLC sourcedirs/destdirs are not checked as they are automatically scoped. However,
                    // their alternates are scoped.
                    List<string> allPossibleTargets = new List<string>();
                    allPossibleTargets.AddRange(job.FilesToInstall.Keys);
                    if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                    {
                        allPossibleTargets.AddRange(job.AlternateFiles.Where(x =>
                            x.Operation == AlternateFile.AltFileOperation.OP_INSTALL ||
                            x.Operation == AlternateFile.AltFileOperation.OP_SUBSTITUTE).Select(x => Path.Combine(MEDirectories.GetDLCPath(Game, ""), x.ModFile)));
                        allPossibleTargets.AddRange(job.AlternateFiles.Where(x =>
                            x.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES).Select(x => Path.Combine(MEDirectories.GetDLCPath(Game, ""), x.MultiListTargetPath)));
                        allPossibleTargets.AddRange(job.AlternateDLCs.Where(x =>
                            x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC ||
                            x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC).Select(x => Path.Combine(MEDirectories.GetDLCPath(Game, ""), x.DestinationDLCFolder) + Path.DirectorySeparatorChar));
                    }
                    else
                    {
                        allPossibleTargets.AddRange(job.AlternateFiles.Where(x =>
                            x.Operation == AlternateFile.AltFileOperation.OP_INSTALL ||
                            x.Operation == AlternateFile.AltFileOperation.OP_SUBSTITUTE).Select(x => x.ModFile));
                        allPossibleTargets.AddRange(job.AlternateFiles.Where(x =>
                            x.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES).Select(x => x.MultiListTargetPath));
                        allPossibleTargets.AddRange(job.AlternateDLCs.Where(x =>
                            x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC ||
                            x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC).Select(x => Path.Combine(MEDirectories.GetDLCPath(Game, ""), x.DestinationDLCFolder) + Path.DirectorySeparatorChar));

                    }

                    allPossibleTargets = allPossibleTargets.Distinct().ToList();
                    // Check all files are within allowed silos.
                    foreach (var f in allPossibleTargets)
                    {
                        if (!siloScopes.AllowedSilos.Any(silo => f.StartsWith(silo, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            TelemetryInterposer.TrackEvent(@"Mod attempts to install outside of header silos", new Dictionary<string, string>()
                                {
                                    {@"Mod name", ModName},
                                    {@"Header", job.Header.ToString()},
                                    {@"Game", Game.ToString()},
                                });
                            // The target of this file is outside the silo
                            M3Log.Error($@"{ModName}'s job {job.Header} file target {f} is outside of allow locations for this header. This is a security risk, mods must only install files to their specified task header directories.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_securityCheckOutsideAllowSilo, ModName, job.Header, f);
                            return;
                        }
                    }

                    // See if any files are in disallowed directory silos.
                    if (siloScopes.DisallowedSilos.Any())
                    {
                        // We need to check it's silos
                        foreach (var f in allPossibleTargets)
                        {
                            if (siloScopes.DisallowedSilos.Any(silo => f.StartsWith(silo, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                TelemetryInterposer.TrackEvent(@"Mod attempts to install outside of header silos", new Dictionary<string, string>()
                                {
                                    {@"Mod name", ModName},
                                    {@"Header", job.Header.ToString()},
                                    {@"Game", Game.ToString()},
                                });
                                // The target of this file is outside the silo
                                M3Log.Error($@"{ModName}'s job {job.Header} file target {f} installs a file into a disallowed silo scope. This is a security risk, mods must only install files to their specified task header directories, and not into protected directories, such as DLC when using BASEGAME tasks.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_securityCheckInsideDisallowedSilo, ModName, job.Header, f);
                                return;
                            }
                        }
                    }

                    // See if any files try to install to a disallowed file silo
                    if (siloScopes.DisallowedFileSilos.Any())
                    {
                        // We need to check it's silos
                        foreach (var f in allPossibleTargets)
                        {
                            if (siloScopes.DisallowedFileSilos.Any(silo => silo.Equals(f, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                TelemetryInterposer.TrackEvent(@"Mod attempts to install disallowed file", new Dictionary<string, string>()
                                {
                                    {@"Mod name", ModName},
                                    {@"Header", job.Header.ToString()},
                                    {@"Game", Game.ToString()},
                                    {@"File", f}
                                });

                                // The target of this file is outside the silo
                                M3Log.Error($@"{ModName}'s job {job.Header} file target {f} installs a file that cannot be installed by Mod Manager as it has been specifically blacklisted. Installing this file poses a security risk or is known to likely harm a user's installation.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_modInstallsBlacklistedDestFile, ModName, job.Header, f);
                                return;
                            }
                        }
                    }
                }
            }

            #endregion

            if (InstallationJobs.Count > 0)
            {
                M3Log.Information($@"Finalizing: {InstallationJobs.Count} installation job(s) were found.", Settings.LogModStartup);
                ValidMod = true;
            }
            else if (!blankLoad)
            {
                M3Log.Error(@"No installation jobs were specified. This mod does nothing.");
                LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_modDoesNothing);
                return;
            }

            M3Log.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
        }

        /// <summary>
        /// Prepares the LELAUNCHER mod job and finalizes loading of the mod.
        /// </summary>
        /// <param name="jobSubdirectory"></param>
        private void LoadLauncherMod(string jobSubDir)
        {
            int jobDirLength = jobSubDir == @"." ? 0 : jobSubDir.Length;
            ModJob headerJob = new ModJob(ModJob.JobHeader.LELAUNCHER, this);
            headerJob.JobDirectory = jobSubDir.Replace('/', '\\');
            var sourceDirectory = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubDir).Replace('/', '\\');
            if (FilesystemInterposer.DirectoryExists(sourceDirectory, Archive))
            {
                var files = FilesystemInterposer.DirectoryGetFiles(sourceDirectory, @"*.*", SearchOption.AllDirectories, Archive).Select(x => x.Substring((ModPath.Length > 0 ? (ModPath.Length + 1) : 0) + jobDirLength).TrimStart('\\')).ToList();
                foreach (var file in files)
                {
                    if (IsLauncherFiletypeAllowed(file))
                    {
                        headerJob.AddPreparsedFileToInstall($@"Content\{file}", file, this);
                    }
                    else
                    {
                        M3Log.Error($@"The LELAUNCHER header only supports the following file extensions: {string.Join(@", ", AllowedLauncherFileTypes)} An unsupported filetype was found: {file}"); // do not localize
                        LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_foundDisallowedLauncherFileType, string.Join(@", ", AllowedLauncherFileTypes), file);
                        ValidMod = false;
                        return;
                    }
                }
            }
            else
            {
                M3Log.Error($@"{ModName}'s LELAUNCHER header specifies a job subdirectory (moddir) that does not exist: {jobSubDir}");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_leLauncherDirNotFound, ModName, jobSubDir);
                ValidMod = false;
                return;
            }

            InstallationJobs.Add(headerJob);
            ValidMod = true;
        }

        /// <summary>
        /// Prepares the Game1TLKMerge mod job
        /// </summary>
        /// <param name="jobSubdirectory"></param>
        private bool ParseGame1TLKMerges()
        {
            int jobDirLength = Mod.Game1EmbeddedTlkFolderName.Length;
            ModJob headerJob = new ModJob(ModJob.JobHeader.GAME1_EMBEDDED_TLK, this);
            headerJob.JobDirectory = Mod.Game1EmbeddedTlkFolderName;
            var sourceDirectory = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName).Replace('/', '\\');
            if (FilesystemInterposer.DirectoryExists(sourceDirectory, Archive))
            {
                if (ModDescTargetVersion >= 8.0)
                {
                    // Check for compressed data
                    var m3zaf = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName, Mod.Game1EmbeddedTlkCompressedFilename);
                    if (FilesystemInterposer.FileExists(m3zaf, Archive))
                    {
                        if (IsInArchive && !CheckNonSolidArchiveFile(m3zaf))
                            return false; // Error handled in guard

                        // We make a new list but we don't populate it as this is used in things like file referencers which would be wrong for the combined file
                        // This variable being populated though will indicate there ARE game1tlk xml files in the mod
                        headerJob.Game1TLKXmls ??= new List<string>();
                    }
                }

                if (headerJob.Game1TLKXmls == null)
                {
                    var searchType = ModDescTargetVersion >= 9.0 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var files = FilesystemInterposer.DirectoryGetFiles(sourceDirectory, @"*.xml", searchType, Archive).Select(x => x.Substring((ModPath.Length > 0 ? (ModPath.Length + 1) : 0) + jobDirLength).TrimStart('\\')).ToList();
                    if (!files.Any())
                    {
                        M3Log.Error($@"Mod specifies {ModJob.JobHeader.GAME1_EMBEDDED_TLK} task header, but no xmls file were found in the {Mod.Game1EmbeddedTlkFolderName} directory. Remove this task header if you are not using it, or add valid xml files to the {Mod.Game1EmbeddedTlkFolderName} directory.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_tlkMergeNoTlkXmlFound, ModJob.JobHeader.GAME1_EMBEDDED_TLK, Mod.Game1EmbeddedTlkFolderName, Mod.Game1EmbeddedTlkFolderName);
                        ValidMod = false;
                        return false;
                    }

                    foreach (var file in files)
                    {
                        if (file.Count(x => x == '.') < 2)
                        {
                            M3Log.Error($@"The {ModJob.JobHeader.GAME1_EMBEDDED_TLK} header only supports the files in the {Mod.Game1EmbeddedTlkFolderName} directory that contain at least 2 '.' characters; one for the extension, and at least one to split the package name from the export path. If the export is nested under packages, more '.' may be needed. Invalid value: {file}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_tlkMergeInvalidTlkXmlFilenames, ModJob.JobHeader.GAME1_EMBEDDED_TLK, Mod.Game1EmbeddedTlkFolderName, file);
                            ValidMod = false;
                            return false;
                        }

                        // Assign in loop to populate variable in the event any of them exist
                        headerJob.Game1TLKXmls ??= new List<string>(files.Count);
                        headerJob.Game1TLKXmls.Add(file);

                        // Load option key value if we find one.
                        if (ModDescTargetVersion >= 9.0)
                        {
                            if (file.Contains('/') || file.Contains('\\'))
                            {
                                var parent = FilesystemInterposer.DirectoryGetParent(file, IsInArchive);
                                if (parent != sourceDirectory)
                                {
                                    LE1TLKMergeAllOptionKeys ??= new List<string>();
                                    var optionName = Path.GetFileName(parent);
                                    if (!LE1TLKMergeAllOptionKeys.Contains(optionName))
                                    {
                                        LE1TLKMergeAllOptionKeys.Add(optionName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                M3Log.Error($@"Mod specifies {ModJob.JobHeader.GAME1_EMBEDDED_TLK} task header, but the {Mod.Game1EmbeddedTlkFolderName} directory was not found. Remove this task header if you are not using it.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_hardcodedDirNotFound, ModJob.JobHeader.GAME1_EMBEDDED_TLK, Mod.Game1EmbeddedTlkFolderName);
                ValidMod = false;
                return false;
            }
            InstallationJobs.Add(headerJob);
            return true;
        }

        internal bool CheckNonSolidArchiveFile(string path)
        {
            if (!IsInArchive)
                throw new Exception(@"Guarded check: Called CheckNonSolidArchiveFile() when there is no backing archive!");

            var storageType = Archive.GetStorageTypeOfFile(path);
            if (storageType != @"Copy")
            {
                M3Log.Error($@"Mod has file is improperly stored in the archive: {path}. Mods must be deployed from Mod Manager to properly work.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_improperPackedFile, path);
                return false;
            }

            return true;
        }

        private static string[] AllowedLauncherFileTypes = new[]
        {
            @".bik", @".cfg", @".chn", @".cht", @".deu", @".esn", @".fra", @".int", @".ita", @".jpn", @".kor", @".pol", @".rus", @".swd", @".swf", @".tmp", @".wav",
        };

        private bool IsLauncherFiletypeAllowed(string filename)
        {
            var extension = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(extension)) return false;
            return AllowedLauncherFileTypes.Contains(extension);
        }

        private static readonly string[] allowedConfigFilesME1 = { @"BIOCredits.ini", @"BioEditor.ini", @"BIOEngine.ini", @"BIOGame.ini", @"BIOGuiResources.ini", @"BIOInput.ini", @"BIOParty.in", @"BIOQA.ini" };

        private bool CheckAndCreateLegacyCoalescedJob()
        {
            var legacyCoalFile = FilesystemInterposer.PathCombine(IsInArchive, ModPath, @"Coalesced.bin");
            if (!FilesystemInterposer.FileExists(legacyCoalFile, Archive))
            {
                if (ModDescTargetVersion == 1.0)
                {
                    //Mod Manager 1/1.1
                    M3Log.Error($@"{ModName} is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.");
                    LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cmm1CoalFileMissing);
                }
                else
                {
                    //Mod Manager 2
                    M3Log.Error($@"{ModName} specifies modcoal descriptor for cmmver 2.0, but the local Coalesced file doesn't exist: {legacyCoalFile}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_cmm2CoalFileMissing, legacyCoalFile);
                }

                return false;
            }

            ModJob basegameJob = new ModJob(ModJob.JobHeader.BASEGAME);
            string failurereason = basegameJob.AddFileToInstall(@"BIOGame\CookedPCConsole\Coalesced.bin", @"Coalesced.bin", this);
            if (failurereason != null)
            {
                M3Log.Error($@"Error occurred while creating basegame job for legacy 1.0 mod: {failurereason}");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_errorCreatingLegacyMod, failurereason);
                return false;
            }
            InstallationJobs.Add(basegameJob);
            return true;
        }

        /// <summary>
        /// Returns a list of DLC foldernames that the mod can depend on being present (or not present!) for it's alternates
        /// </summary>
        /// <returns></returns>
        public SortedSet<string> GetAutoConfigs()
        {
            var autoConfigs = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var InstallationJob in InstallationJobs)
            {
                foreach (var altdlc in InstallationJob.AlternateDLCs)
                {
                    if (altdlc.Condition != AlternateDLC.AltDLCCondition.COND_MANUAL)
                    {
                        foreach (var conditionaldlc in altdlc.ConditionalDLC) // Conditional DLC are not available for COND_MANUAL
                        {
                            autoConfigs.Add(conditionaldlc.DLCFolderName.Key);
                        }
                    }
                    else if (altdlc.Condition == AlternateDLC.AltDLCCondition.COND_MANUAL && altdlc.DLCRequirementsForManual != null && altdlc.DLCRequirementsForManual.Any())
                    {
                        foreach (var manualTrigger in altdlc.DLCRequirementsForManual)
                        {
                            autoConfigs.Add(manualTrigger.DLCFolderName.Key);
                        }
                    }
                }
                foreach (var altfile in InstallationJob.AlternateFiles)
                {
                    foreach (var conditionaldlc in altfile.ConditionalDLC)
                    {
                        autoConfigs.Add(conditionaldlc.DLCFolderName.Key);
                    }
                }
            }

            return autoConfigs;
        }
    }
}
