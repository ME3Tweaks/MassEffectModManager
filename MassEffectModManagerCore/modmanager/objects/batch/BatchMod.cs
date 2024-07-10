﻿using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.FileSource;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    [AddINotifyPropertyChangedInterface]
    public class BatchMod : IBatchQueueMod
    {
        public BatchMod() { }

        /// <summary>
        /// Creates a batch mod wrapper based on this mod with no chosen options yet
        /// </summary>
        /// <param name="m"></param>
        public BatchMod(Mod m)
        {
            Mod = m;
            ModDescPath = m.ModDescPath.Substring(M3LoadedMods.GetCurrentModLibraryDirectory().Length + 1); // Make relative to root of library (global)
        }


        /// <summary>
        /// The matching mod in the library
        /// </summary>
        [JsonIgnore]
        public Mod Mod { get; set; }

        /// <summary>
        /// The path on disk for the associated mod
        /// </summary>
        [JsonProperty(@"moddescpath")]
        public string ModDescPath { get; set; }

        /// <summary>
        /// When this batch mod was last configured
        /// </summary>
        [JsonProperty(@"configurationtime")]
        public DateTime ConfigurationTime { get; set; }

        /// <summary>
        /// The hash of the moddesc.ini file - if the one on disk does not match this, options must (should?) be rechosen
        /// </summary>
        [JsonProperty(@"moddeschash")]
        public string Hash { get; set; }

        /// <summary>
        /// The original size of the moddesc
        /// </summary>
        [JsonProperty(@"moddescsize")]
        public long Size { get; set; }

        /// <summary>
        /// If the moddesc hash does not match the one on disk
        /// </summary>
        [JsonIgnore]
        public bool ChosenOptionsDesync { get; set; }

        /// <summary>
        /// If the mod is not available for install, e.g. it was removed
        /// </summary>
        [JsonIgnore]
        public bool ModMissing => Mod == null;

        /// <summary>
        /// Link to where the asset that satisfies this mod can be found
        /// </summary>
        [JsonProperty(@"downloadlink")]
        public string DownloadLink { get; set; }

        /// <summary>
        /// List of user selections, in order
        /// </summary>
        [JsonProperty(@"userchosenoptions")]
        public List<PlusMinusKey> UserChosenOptions { get; set; }

        /// <summary>
        /// List of all chosen options before mod should install. If this list doesn't match it means the underlying game state differs to a degree that a different option was automatically chosen
        /// </summary>
        [JsonProperty(@"allchosenoptions")]
        public List<string> AllChosenOptionsForValidation { get; set; }

        /// <summary>
        /// If options have been chosen for this mod - even if there are none.
        /// </summary>
        [JsonProperty(@"haschosenoptions")]
        public bool HasChosenOptions { get; set; }

        /// <summary>
        /// The UI bound string to show if options were chosen or not
        /// </summary>
        [JsonIgnore]
        public string OptionsRecordedString
        {
            get
            {
                if (IsStandalone) return M3L.GetString(M3L.string_batchModHasNoConfigOptionsUIText);
                if (!HasChosenOptions) return M3L.GetString(M3L.string_notConfigured);
                if (ChosenOptionsDesync) return M3L.GetString(M3L.string_reconfigurationRequired);
                return M3L.GetString(M3L.string_interp_configuredTimestamp, ConfigurationTime.ToString(@"d"));
            }
        }

        /// <summary>
        /// If a content mod has no configurable options
        /// </summary>
        public bool IsStandalone => Mod != null && Mod.InstallationJobs.Sum(x => x.GetAllAlternates().Count) == 0;

        /// <summary>
        /// The UI bound tooltip string to show if options were chosen or not
        /// </summary>
        [JsonIgnore]
        public string OptionsRecordedTooltipString
        {
            get
            {
                // Standalone
                if (Mod != null && Mod.InstallationJobs.Sum(x => x.GetAllAlternates().Count) == 0) return M3L.GetString(M3L.string_tooltip_standaloneBQ);
                if (!HasChosenOptions) return M3L.GetString(M3L.string_tooltip_noSavedOptionsBQ);
                if (ChosenOptionsDesync) return M3L.GetString(M3L.string_tooltip_reconfigurationBQ);
                return M3L.GetString(M3L.string_tooltip_timestampBQ);
            }
        }

        /// <summary>
        /// Indicates this is the first mod being installed
        /// </summary>
        [JsonIgnore]
        public bool IsFirstBatchMod { get; set; }

        /// <summary>
        /// If the saved options for this batch mod should be used by the installer
        /// </summary>
        [JsonIgnore]
        public bool UseSavedOptions { get; set; }

        /// <summary>
        /// Name to show when mod is not available. For serialization.
        /// </summary>
        [JsonProperty(@"modname")]
        public string ModName { get; set; }


        /// <summary>
        /// Initializes and associates a mod with this object
        /// </summary>
        public void Init(bool logMissing)
        {
            var libraryRoot = M3LoadedMods.GetCurrentModLibraryDirectory(); // biq2 stores relative to library root. biq stores to library root FOR GAME

            var fullModdescPath = Path.Combine(libraryRoot, ModDescPath);
            if (File.Exists(fullModdescPath))
            {
                Mod m = M3LoadedMods.Instance.AllLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(fullModdescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    Mod = m;
                    var localHash = MUtilities.CalculateHash(Mod.ModDescPath);
                    ChosenOptionsDesync = Hash != null && localHash != Hash && !IsStandalone;
                    //if (ChosenOptionsDesync)
                    //{
                    //    Debugger.Break();
                    //}
                }
                else
                {
                    M3Log.Warning($@"Batch queue mod has moddesc but is not a loaded mod in library: {fullModdescPath}");
                }
            }
            else
            {
                M3Log.Warning($@"Batch queue mod not available in library: {fullModdescPath}", logMissing);
            }
        }

        /// <summary>
        /// Populates variables that must be available when serializing to disk
        /// </summary>
        public void PrepareForSave()
        {
            if (Mod != null)
            {
                Size = new FileInfo(Mod.ModDescPath).Length;
                Hash = MUtilities.CalculateHash(Mod.ModDescPath);
                if (FileSourceService.TryGetSource(new FileInfo(Mod.ModDescPath).Length, Hash, out var sourceLink))
                {
                    DownloadLink = sourceLink;
                }

                ModName = Mod.ModName;
            }
        }

        public bool IsAvailableForInstall()
        {
            return Mod != null;
        }
    }
}
