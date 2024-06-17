﻿using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.ui;
using Pathoschild.FluentNexus.Models;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.memoryanalyzer;

namespace ME3TweaksModManager.modmanager.objects
{
    public enum EModDownloadState
    {
        UNKNOWN,
        /// <summary>
        /// Download failed
        /// </summary>
        FAILED,
        /// <summary>
        /// Download is gathering necessary information before it is ready to download
        /// </summary>
        INITIALIZING,
        /// <summary>
        /// Download is currently waiting to start
        /// </summary>
        QUEUED,
        /// <summary>
        /// Mod is currently downloading
        /// </summary>
        DOWNLOADING,
        /// <summary>
        /// Download has finished
        /// </summary>
        DOWNLOADCOMPLETE,
        /// <summary>
        /// Mod is queued for automatic import
        /// </summary>
        WAITINGFORIMPORT,
        /// <summary>
        /// The mod is currently importing
        /// </summary>
        IMPORTING,
        /// <summary>
        /// Everything has completed
        /// </summary>
        FINISHED
    }

    /// <summary>
    /// Class for information about a mod that is being downloaded, and optionally imported
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public abstract class ModDownload
    {
        /// <summary>
        /// The maximum size of an archive that can be downloaded and loaded from memory.
        /// </summary>
        private protected static readonly long DOWNLOAD_TO_MEMORY_SIZE_CAP = 100 * FileSize.MebiByte;



        #region Useful UI stuff
        /// <summary>
        /// The current state of the download
        /// </summary>
        public EModDownloadState DownloadState { get; set; }
        private void OnDownloadStateChanged()
        {
            DownloadStateChanged?.Invoke(this, null);
        }
        public event EventHandler<EventArgs> DownloadStateChanged;


        public long ProgressValue { get; protected internal set; }
        public long ProgressMaximum { get; protected internal set; }
        public bool ProgressIndeterminate { get; protected internal set; } = true;

        /// <summary>
        /// UI string to display. Should be short.
        /// </summary>
        public string Status { get; protected internal set; }

        public void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, DataEventArgs.Empty);
        }

        #endregion

        /// <summary>
        /// The downloaded stream data
        /// </summary>
        public Stream DownloadedStream { get; private protected set; }

        /// <summary>
        /// If import should be automatically attempted
        /// </summary>
        public bool AutoImport { get; set; }

        /// <summary>
        /// Associated mod importing flow for this download
        /// </summary>
        public ModArchiveImport ImportFlow { get; set; }

        /// <summary>
        /// Invoked when the mod has initialized
        /// </summary>
        public event EventHandler<EventArgs> OnInitialized;
        /// <summary>
        /// Invoked when a mod download has completed
        /// </summary>
        public event EventHandler<DataEventArgs> OnModDownloaded;
        /// <summary>
        /// Invoked when a mod download has an error
        /// </summary>
        public event EventHandler<string> OnModDownloadError;

        protected ModDownload(string downloadLink){ }

        protected ModDownload()
        {
            DownloadState = EModDownloadState.INITIALIZING;
        }

        private protected void OnDownloadProgress(long done, long total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            ProgressIndeterminate = false;
            Status = $@"{FileSize.FormatSize(ProgressValue)}/{FileSize.FormatSize(ProgressMaximum)}";
        }

        /// <summary>
        /// When the UIStatus string has changed.
        /// </summary>
        public event EventHandler StatusChanged;

        public abstract void StartDownload(CancellationToken cancellationToken, bool forceDownloadToDisk = false);
        
        protected void InternalOnModDownloadError(string str)
        {
            OnModDownloadError?.Invoke(this, str);
        }

        private protected void InternalOnModDownloaded(DataEventArgs args)
        {
            OnModDownloaded?.Invoke(this, args);
        }

        private protected void InternalOnInitialized()
        {
            OnInitialized?.Invoke(this, null);
        }

        public abstract string CreateDownloadKey();
    }

    /// <summary>
    /// Class for downloading a mod from NexusMods
    /// </summary>
    public class NexusModDownload : ModDownload
    {

        private static readonly int[] WhitelistedME1FileIDs = new[]
        {
            116, // skip intro movies
            117, // skip to main menu
            120, // controller skip intro movies
            121, // controller skip to main menu
            245, // ME1 Controller 1.2.2
            326, // MAKO MOD
            327, // Mako Mod v2
            328, // Mako mod v3
            569, // mass effect ultrawide
        };

        private static readonly int[] WhitelistedME2FileIDs = new[]
        {
            3, // cheat console
            44, // faster load screens animated
            338, // Controller Mod 1.7.2
            365, // no minigames 2.0.2
        };

        private static readonly int[] WhitelistedME3FileIDs = new[]
        {
            0,
        };


        public string NXMLink { get; set; }
        public ModFile ModFile { get; private set; }
        public NexusProtocolLink ProtocolLink { get; private set; }
        public List<ModFileDownloadLink> DownloadLinks { get; } = new List<ModFileDownloadLink>();

        public NexusModDownload(string nxmLink) : base()
        {
            NXMLink = nxmLink;
        }


        private bool IsDownloadWhitelisted(string domain, ModFile modFile)
        {
            switch (domain)
            {
                case @"masseffect":
                    return WhitelistedME1FileIDs.Contains(modFile.FileID);
                case @"massseffect2":
                    return WhitelistedME2FileIDs.Contains(modFile.FileID);
                case @"masseffect3":
                    return WhitelistedME3FileIDs.Contains(modFile.FileID);
            }

            return false;
        }

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin.
        /// THIS IS A BLOCKING CALL DO NOT RUN ON THE UI
        /// </summary>
        public void Initialize()
        {
            M3Log.Information($@"Initializing {NXMLink}");
            Task.Run(() =>
            {
                try
                {
                    DownloadLinks.Clear();

                    ProtocolLink = NexusProtocolLink.Parse(NXMLink);
                    if (ProtocolLink == null) return; // Parse failed.

                    if (!NexusModsUtilities.AllSupportedNexusDomains.Contains(ProtocolLink?.Domain))
                    {
                        M3Log.Error($@"Cannot download file from unsupported domain: {ProtocolLink?.Domain}. Open your preferred mod manager from that game first");
                        DownloadState = EModDownloadState.FAILED;
                        ProgressIndeterminate = false;
                        InternalOnModDownloadError(M3L.GetString(M3L.string_interp_dialog_modNotForThisModManager, ProtocolLink.Domain));
                        return;
                    }

                    // Mod Manager 8: Blacklisting files
                    if (BlacklistingService.IsNXMBlacklisted(ProtocolLink))
                    {
                        M3Log.Error($@"File is blacklisted by ME3Tweaks: {ProtocolLink?.Domain} file {ProtocolLink.FileId}");
                        DownloadState = EModDownloadState.FAILED;
                        ProgressIndeterminate = false;
                        InternalOnModDownloadError(M3L.GetString(M3L.string_description_blacklistedMod));
                        return;
                    }

                    ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(ProtocolLink.Domain, ProtocolLink.ModId, ProtocolLink.FileId).Result;
                    if (ModFile != null)
                    {
                        if (ModFile.SizeInBytes != null && ModFile.SizeInBytes.Value > DOWNLOAD_TO_MEMORY_SIZE_CAP && M3Utilities.GetDiskFreeSpaceEx(M3Filesystem.GetModDownloadCacheDirectory(), out var free, out var total, out var totalFree))
                        {
                            // Check free disk space.
                            var spaceRequiredWithBuffer = ModFile.SizeInBytes * 1.2;// 20% buffer.
                            if (totalFree < spaceRequiredWithBuffer)
                            //if (totalFree < ModFile.SizeInBytes * 100000.0) // Debug code
                            {
                                M3Log.Error($@"There is not enough free space on {Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory())} to download {ModFile.FileName}. We need {FileSize.FormatSize((long)spaceRequiredWithBuffer)} but only {FileSize.FormatSize(totalFree)} is available.");
                                DownloadState = EModDownloadState.FAILED;
                                ProgressIndeterminate = false;
                                InternalOnModDownloadError(M3L.GetString(M3L.string_interp_notEnoughFreeSpaceForDownload, Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory()), ModFile.FileName, FileSize.FormatSize((long)spaceRequiredWithBuffer), FileSize.FormatSize(totalFree)));
                                return;
                            }
                        }


                        if (ModFile.Category != FileCategory.Deleted)
                        {
                            if (ProtocolLink.Key != null)
                            {
                                // Website click


                                if (ProtocolLink.Domain is @"masseffect" or @"masseffect2" && !IsDownloadWhitelisted(ProtocolLink.Domain, ModFile))
                                {
                                    // Check to see file has moddesc.ini the listing
                                    var fileListing = NexusModsUtilities.GetFileListing(ModFile);

                                    // 02/27/2022: We check for TPISService SizeInBytes. It's not 100% accurate since we don't have an MD5 to check against. But it's pretty likely it's supported.
                                    if (fileListing == null || !HasModdescIni(fileListing) && ModFile.SizeInBytes != null && TPIService.GetImportingInfosBySize(ModFile.SizeInBytes.Value).Count == 0)
                                    {
                                        M3Log.Error($@"This file is not whitelisted for download and does not contain a moddesc.ini file, this is not a mod manager mod: {ModFile.FileName}");
                                        ProgressIndeterminate = false;
                                        DownloadState = EModDownloadState.FAILED;
                                        InternalOnModDownloadError(M3L.GetString(M3L.string_interp_nexusModNotCompatible, ModFile.Name));
                                        return;
                                    }
                                }


                                // download with manager was clicked.

                                // Check if parameters are correct!
                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink).Result);
                            }
                            else
                            {
                                // premium? no parameters were supplied...
                                if (!NexusModsUtilities.UserInfo.IsPremium)
                                {
                                    M3Log.Error(
                                        $@"Cannot download {ModFile.FileName}: User is not premium, but this link is not generated from NexusMods");
                                    DownloadState = EModDownloadState.FAILED;
                                    ProgressIndeterminate = false;
                                    InternalOnModDownloadError(M3L.GetString(M3L.string_dialog_mustBePremiumUserToDownload));
                                    return;
                                }

                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink)
                                    ?.Result);
                            }

                            ProgressMaximum = ModFile.SizeInBytes ?? ModFile.SizeInKilobytes * 1024L; // SizeKb is the original version. They added SizeInBytes at my request
                            DownloadState = EModDownloadState.QUEUED;
                            M3Log.Information($@"ModDownload has initialized: {ModFile.FileName}");
                            InternalOnInitialized();
                        }
                        else
                        {
                            M3Log.Error($@"Cannot download {ModFile.FileName}: File deleted from NexusMods");
                            DownloadState = EModDownloadState.FAILED;
                            ProgressIndeterminate = false;
                            InternalOnModDownloadError(M3L.GetString(M3L.string_dialog_cannotDownloadDeletedFile));
                        }
                    }
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, $@"Error downloading {ModFile?.FileName}:");
                    DownloadState = EModDownloadState.FAILED;
                    ProgressIndeterminate = false;
                    InternalOnModDownloadError(M3L.GetString(M3L.string_interp_errorDownloadingModX, e.Message));
                }
            });
        }

        /// <summary>
        /// Begins downloading of the mod to disk or memory.
        /// </summary>
        /// <param name="cancellationToken">Token to indicate the download has been canceled.</param>
        public override void StartDownload(CancellationToken cancellationToken, bool forceDownloadToDisk = false)
        {
            Task.Run(() =>
            {
                if (!forceDownloadToDisk && ProgressMaximum < DOWNLOAD_TO_MEMORY_SIZE_CAP && Settings.ModDownloadCacheFolder == null) // Mod Manager 8.0.1: If cache is set, always download to disk    
                {
                    DownloadedStream = new MemoryStream();
                    M3MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download MemoryStream", DownloadedStream);
                }
                else
                {
                    DownloadedStream = new FileStream(Path.Combine(M3Filesystem.GetModDownloadCacheDirectory(), ModFile.FileName), FileMode.Create);
                    M3MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download FileStream", DownloadedStream);
                }

                var downloadUri = DownloadLinks[0].Uri;
                DownloadState = EModDownloadState.DOWNLOADING;
                var downloadResult = M3OnlineContent.DownloadToStream(downloadUri.ToString(), OnDownloadProgress, null, true, DownloadedStream, cancellationToken);
                if (downloadResult.errorMessage != null)
                {
                    DownloadedStream?.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Aborted download.
                    }
                    else
                    {
                        M3Log.Error($@"Download failed: {downloadResult.errorMessage}");
                        InternalOnModDownloadError(downloadResult.errorMessage);
                    }
                    // Download didn't work!
                    TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", ProtocolLink?.Domain},
                        {@"File", ModFile?.Name},
                        {@"Result", $@"Failed, {downloadResult.errorMessage}"},
                    });
                }
                else
                {
                    // Verify
                    ProgressIndeterminate = true;
                    Status = M3L.GetString(M3L.string_verifyingDownload);
                    try
                    {
                        // Todo: If verification fails, should we let user try to continue anyways?

                        var hash = MUtilities.CalculateHash(DownloadedStream);
                        var matchingHashedFiles = NexusModsUtilities.MD5Search(ProtocolLink.Domain, hash);
                        if (matchingHashedFiles.All(x => x.Mod.ModID != ProtocolLink.ModId))
                        {
                            // Our hash does not match
                            M3Log.Error(@"Download failed: File does not appear to match file on NexusMods");
                            InternalOnModDownloadError(M3L.GetString(M3L.string_fileDidNotVerifyDownloadMayBeCorrupt));
                            TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                            {
                                {@"Domain", ProtocolLink?.Domain},
                                {@"File", ModFile?.Name},
                                {@"Result", @"Corrupt download"},
                            });
                            DownloadState = EModDownloadState.FAILED;
                            return;
                        }
                        M3Log.Information(@"File verified OK, nexus MD5 search returned correct result (NexusMods: Please make MD5 available as part of download API! This is a ridiculous way of verifying files)");
                    }
                    catch (Exception ex)
                    {
                        M3Log.Warning($@"An error occurred while attempting to verify the file: {ex.Message}. Skipping verification for this download.");
                    }

                    TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", ProtocolLink?.Domain},
                        {@"File", ModFile?.Name},
                        {@"Result", @"Success"},
                    });
                }

                ProgressIndeterminate = false;
                ProgressValue = 1;
                ProgressMaximum = 1;
                Status = "Download complete";
                DownloadState = EModDownloadState.DOWNLOADCOMPLETE;
                InternalOnModDownloaded(new DataEventArgs(DownloadedStream));
            });
        }

        public override string CreateDownloadKey()
        {
            return $@"{ProtocolLink.Domain}-{ProtocolLink.FileId}";
        }

        /// <summary>
        /// Determines if the file listing has any moddesc.ini files in it.
        /// </summary>
        /// <param name="fileListing"></param>
        /// <returns></returns>
        private bool HasModdescIni(ContentPreview fileListing)
        {
            foreach (var e in fileListing.Children)
            {
                if (HasModdescIniRecursive(e))
                    return true;
            }

            return false;
        }

        private bool HasModdescIniRecursive(ContentPreviewEntry entry)
        {
            // Directory
            if (entry.Type == ContentPreviewEntryType.Directory)
            {
                foreach (var e in entry.Children)
                {
                    if (HasModdescIniRecursive(e))
                        return true;
                }

                return false;
            }

            // File
            return entry.Name == @"moddesc.ini";
        }
    }
}
