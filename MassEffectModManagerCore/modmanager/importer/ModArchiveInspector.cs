﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser.Model;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols;
using SevenZip;

namespace ME3TweaksModManager.modmanager.importer
{
    /// <summary>
    /// Class for inspecting mod archives to find mod content
    /// </summary>
    internal class ModArchiveInspector
    {
        /// <summary>
        /// Inspects and loads compressed mods from an archive.
        /// </summary>
        /// <param name="filepath">Path of the archive</param>
        /// <param name="addCompressedModCallback">Callback indicating that the mod should be added to the collection of found mods</param>
        /// <param name="currentOperationTextCallback">Callback to tell caller what's going on'</param>
        /// <param name="failedToLoadModCallback">Callback that returns a mod that failed to load</param>
        public static string FindModsInArchive(string filepath,
            Action<Mod> addCompressedModCallback = null,
            Action<Mod> failedToLoadModCallback = null,
            Action<MEMMod> addTextureMod = null,
            Action<BatchLibraryInstallQueue> addBiq = null,
            Action<string> currentOperationTextCallback = null,
            string forcedMD5 = null,
            long forcedSize = -1,
            Stream archiveStream = null)
        {
            string relayVersionResponse = @"-1";
            List<Mod> internalModList = new List<Mod>(); //internal mod list is for this function only so we don't need a callback to get our list since results are returned immediately
            var isExe = filepath.EndsWith(@".exe");
            SevenZipExtractor archiveFile = null;

            bool closeStreamOnComplete = true;
            try
            {
                if (archiveStream != null)
                {
                    closeStreamOnComplete = false;
                    archiveStream.Position = 0;
                    archiveFile = isExe
                        ? new SevenZipExtractor(archiveStream, InArchiveFormat.Nsis)
                        : new SevenZipExtractor(archiveStream);
                    archiveFile.SetFilename(filepath);
                }
                else
                {
                    archiveFile = isExe
                        ? new SevenZipExtractor(filepath, InArchiveFormat.Nsis)
                        : new SevenZipExtractor(filepath);
                }
            }
            catch (IOException ioException)
            {
                // There was an error opening the archive
                // Like a PUP (tested with eicar.7z)
                M3Log.Error($@"Error opening archive: {ioException.Message}");
                return ioException.Message;
            }
#if DEBUG
            //foreach (var v in archiveFile.ArchiveFileData)
            //{
            //    Debug.WriteLine($@"{v.FileName} | Index {v.Index} | Size {v.Size} | Method {v.Method} | IsDirectory {v.IsDirectory} | Last Modified {v.LastWriteTime}");
            //}
#endif
            var moddesciniEntries = new List<ArchiveFileInfo>();
            var sfarEntries = new List<ArchiveFileInfo>(); //ME3 DLC
            var me2mods = new List<ArchiveFileInfo>(); //ME2 RCW Mods
            var textureModEntries = new List<ArchiveFileInfo>(); //TPF MEM MOD files
            var batchQueueEntries = new List<ArchiveFileInfo>(); //BIQ2 files (old biq are not supported)
            bool isAlotFile = false;
            try
            {
                foreach (var entry in archiveFile.ArchiveFileData)
                {
                    if (!entry.IsDirectory)
                    {
                        string fname = Path.GetFileName(entry.FileName);
                        var extension = Path.GetExtension(fname);
                        if (fname.Equals(@"ALOTInstaller.exe", StringComparison.InvariantCultureIgnoreCase))
                        {
                            isAlotFile = true;
                        }
                        else if (fname.Equals(@"moddesc.ini", StringComparison.InvariantCultureIgnoreCase))
                        {
                            moddesciniEntries.Add(entry);
                        }
                        else if (fname.Equals(@"Default.sfar", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //for unofficial lookups
                            sfarEntries.Add(entry);
                        }
                        else if (extension == @".me2mod")
                        {
                            me2mods.Add(entry);
                        }
                        else if (extension is @".mem" or @".tpf" or @".mod")
                        {
                            textureModEntries.Add(entry);
                        }
                        else if (extension is BatchLibraryInstallQueue.QUEUE_VERSION_BIQ2_EXTENSION)
                        {
                            batchQueueEntries.Add(entry);
                        }
                    }
                }
            }
            catch (SevenZipArchiveException svae)
            {
                //error reading archive!
                Mod failed = new Mod(false);
                failed.ModName = M3L.GetString(M3L.string_archiveError);
                failed.LoadFailedReason = M3L.GetString(M3L.string_couldNotInspectArchive7zException);
                M3Log.Error($@"Unable to inspect archive {filepath}: SevenZipException occurred! It may be corrupt. The specific error was: {svae.Message}");
                failedToLoadModCallback?.Invoke(failed);
                addCompressedModCallback?.Invoke(failed);
                if (closeStreamOnComplete)
                {
                    archiveFile?.Dispose();
                }
                else
                {
                    archiveFile?.DisposeObjectOnly();
                }
                return null;
            }

            if (batchQueueEntries.Any() && !filepath.EndsWith(@".7z"))
            {
                M3Log.Error(@"Batch queues cannot be imported if they are not deployed by mod manager!");
                Mod failed = new Mod(false);
                failed.ModName = M3L.GetString(M3L.string_archiveError);
                failed.LoadFailedReason = M3L.GetString(M3L.string_description_invalidBatchQueueArchive);
                failedToLoadModCallback?.Invoke(failed);
                addCompressedModCallback?.Invoke(failed);
                if (closeStreamOnComplete)
                {
                    archiveFile?.Dispose();
                }
                else
                {
                    archiveFile?.DisposeObjectOnly();
                }

                return null;
            }

            // This updates the found files list to
            // scrub out files that may be also referenced
            // e.g. .mem files as part of a moddesc mod
            // should not also show up as importable standalone
            void removeAllSubentries(string rootPath)
            {
                sfarEntries.RemoveAll(x => x.FileName.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase));
                textureModEntries.RemoveAll(x => x.FileName.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase));
                me2mods.RemoveAll(x => x.FileName.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase));
                batchQueueEntries.RemoveAll(x => x.FileName.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase));
            }


            // Used for TPIS information lookup
            bool useTPIS = true;
            long archiveSize = forcedSize > 0 ? forcedSize : archiveStream != null ? archiveStream.Length : new FileInfo(filepath).Length;

            if (moddesciniEntries.Count > 0)
            {
                foreach (var entry in moddesciniEntries)
                {
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_interp_readingX, entry.FileName));
                    Mod m = new Mod(entry, archiveFile);
                    removeAllSubentries(m.ModPath);
                    if (!m.ValidMod)
                    {
                        failedToLoadModCallback?.Invoke(m);
                        m.SelectedForImport = false;
                    }

                    addCompressedModCallback?.Invoke(m);
                    internalModList.Add(m);
                    useTPIS = false;
                }
            }
            else if (me2mods.Count > 0)
            {
                //found some .me2mod files.
                foreach (var entry in me2mods)
                {
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_interp_readingX, entry.FileName));
                    MemoryStream ms = new MemoryStream();
                    archiveFile.ExtractFile(entry.Index, ms);
                    ms.Position = 0;
                    StreamReader reader = new StreamReader(ms);
                    string text = reader.ReadToEnd();
                    var rcwModsForFile = RCWMod.ParseRCWMods(Path.GetFileNameWithoutExtension(entry.FileName), text);
                    foreach (var rcw in rcwModsForFile)
                    {
                        Mod m = new Mod(rcw);
                        addCompressedModCallback?.Invoke(m);
                        internalModList.Add(m);
                        useTPIS = false;
                    }
                }
            }
            else if (textureModEntries.Any())
            {
                //found some .mem files
                foreach (var entry in textureModEntries.Where(x => Path.GetExtension(x.FileName) == @".mem"))
                {
                    MEMMod memFile = new MEMMod(entry.FileName) { SizeRequiredtoExtract = (long)entry.Size, SelectedForImport = true, IsInArchive = true };
                    addTextureMod(memFile);
                    useTPIS = false;
                }
            }
            else if (batchQueueEntries.Any())
            {
                foreach (var entry in batchQueueEntries)
                {
                    MemoryStream ms = new MemoryStream();
                    archiveFile.ExtractFile(entry.Index, ms);
                    ms.Position = 0;
                    StreamReader reader = new StreamReader(ms);
                    string text = reader.ReadToEnd();
                    var biq = BatchLibraryInstallQueue.ParseModernQueue(Path.GetFileName(entry.FileName), text, cacheText: true);
                    addBiq(biq);
                    biq.SelectedForImport = biq.ValidMod;
                    useTPIS = false;
                }
            }

            // If nothing was loaded, use TPIS to attempt a virtual load
            if (useTPIS)
            {
                M3Log.Information(@"Querying third party importing service for information about this file: " + filepath);
                currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_queryingThirdPartyImportingService));
                var md5 = forcedMD5 ?? (archiveStream != null ? MUtilities.CalculateHash(archiveStream) : MUtilities.CalculateHash(filepath));
                var potentialImportinInfos = TPIService.GetImportingInfosBySize(archiveSize);
                var importingInfo = potentialImportinInfos.FirstOrDefault(x => x.md5 == md5);

                if (importingInfo == null && isExe)
                {
                    M3Log.Error(@"EXE-based mods must be validated by ME3Tweaks before they can be imported into M3. This is to prevent breaking third party mods.");
                    return null; // We don't want to tell user that we don't support it cause they'll just ask us to, which I don't want
                }

                ModArchiveImport.ExeTransform transform = null;
                if (importingInfo?.exetransform != null)
                {
                    M3Log.Information(@"TPIS lists exe transform for this mod: " + importingInfo.exetransform);
                    transform = new ModArchiveImport.ExeTransform(M3OnlineContent.FetchExeTransform(importingInfo.exetransform));
                }

                string custommoddesc = null;
                if (importingInfo?.servermoddescname != null)
                {
                    //Partially supported unofficial third party mod
                    //Mod has a custom written moddesc.ini stored on ME3Tweaks
                    M3Log.Information(@"Fetching premade moddesc.ini from ME3Tweaks for this mod archive");
                    string loadFailedReason = null;
                    try
                    {
                        custommoddesc = M3OnlineContent.FetchThirdPartyModdesc(importingInfo.servermoddescname ?? transform.PostTransformModdesc);
                    }
                    catch (Exception e)
                    {
                        loadFailedReason = e.Message;
                        M3Log.Error(@"Error fetching moddesc from server: " + e.Message);
                    }

                    //if (!isExe)
                    //{
                    Mod virutalCustomMod = new Mod(custommoddesc, "", archiveFile); //Load virtual mod
                    if (virutalCustomMod.ValidMod)
                    {
                        M3Log.Information(@"Mod loaded from server moddesc.");
                        addCompressedModCallback?.Invoke(virutalCustomMod);
                        internalModList.Add(virutalCustomMod);
                        return null; //Don't do further parsing as this is custom written
                    }
                    else
                    {
                        if (loadFailedReason != null)
                        {
                            virutalCustomMod.LoadFailedReason = M3L.GetString(
                                M3L.string_interp_failedToFetchModdesciniFileFromServerReasonLoadFailedReason,
                                loadFailedReason);
                        }
                        else
                        {
                            M3Log.Error(@"Server moddesc was not valid for this mod. This shouldn't occur. Please report to Mgamerz.");
                            TelemetryInterposer.TrackEvent(@"Invalid servermoddesc detected", new Dictionary<string, string>()
                                {
                                    {@"moddesc.ini name", importingInfo.servermoddescname ?? transform.PostTransformModdesc}
                                });
                        }
                        if (closeStreamOnComplete)
                        {
                            archiveFile?.Dispose();
                        }
                        else
                        {
                            archiveFile?.DisposeObjectOnly();
                        }
                        return M3L.GetString(M3L.string_invalidServerModdesc);
                    }
                    //} else
                    //{
                    //    M3Log.Information(@"Fetched premade moddesc.ini from server. We will fake the mod for the user");
                    //}
                }



                //Fully unofficial third party mod.
                //ME3 ONLY 
                foreach (var sfarEntry in sfarEntries)
                {
                    var vMod = AttemptLoadVirtualMod(sfarEntry, archiveFile, md5);
                    if (vMod != null)
                    {
                        addCompressedModCallback?.Invoke(vMod);
                        internalModList.Add(vMod);
                        vMod.ExeExtractionTransform = transform;
                    }
                }


                if (importingInfo?.version != null)
                {
                    foreach (Mod compressedMod in internalModList)
                    {
                        compressedMod.ModVersionString = importingInfo.version;
                        Version.TryParse(importingInfo.version, out var parsedValue);
                        compressedMod.ParsedModVersion = parsedValue;
                    }
                }
                else if (relayVersionResponse == @"-1")
                {
                    //If no version information, check ME3Tweaks to see if it's been added recently
                    //see if server has information on version number
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_gettingAdditionalInformationAboutFileFromME3Tweaks));
                    M3Log.Information(@"Querying ME3Tweaks for additional information for this file...");
                    var modInfo = M3OnlineContent.QueryModRelay(md5, archiveSize);
                    //todo: make this work offline.
                    if (modInfo != null && modInfo.TryGetValue(@"version", out string value))
                    {
                        M3Log.Information(@"ME3Tweaks reports version number for this file is: " + value);
                        foreach (Mod compressedMod in internalModList)
                        {
                            compressedMod.ModVersionString = value;
                            Version.TryParse(value, out var parsedValue);
                            compressedMod.ParsedModVersion = parsedValue;
                        }
                        relayVersionResponse = value;
                    }
                    else
                    {
                        M3Log.Information(@"ME3Tweaks does not have additional version information for this file.");
                        TelemetryInterposer.TrackEvent(@"Non Mod Manager Mod Dropped", new Dictionary<string, string>()
                            {
                                {@"Filename", Path.GetFileName(filepath)},
                                {@"MD5", md5}
                            });
                        foreach (Mod compressedMod in internalModList)
                        {
                            compressedMod.ModVersionString = M3L.GetString(M3L.string_unknown);
                        }
                    }
                }

                else
                {
                    //Try straight up TPMI import?
                    M3Log.Warning($@"No importing information is available for file with hash {md5}. No mods could be found.");
                    TelemetryInterposer.TrackEvent(@"Non Mod Manager Mod Dropped", new Dictionary<string, string>()
                        {
                            {@"Filename", Path.GetFileName(filepath)},
                            {@"MD5", md5}
                        });
                }
            }

            if (closeStreamOnComplete)
            {
                archiveFile?.Dispose();
            }
            else
            {
                archiveFile?.DisposeObjectOnly();
            }

            return null;
        }

        /// <summary>
        /// Loads a virtual moddesc.ini entry based on the given sfar path (ME3 only)
        /// </summary>
        /// <param name="sfarEntry"></param>
        /// <param name="archive"></param>
        /// <param name="game"></param>
        /// <param name="md5"></param>
        /// <returns></returns>
        private static Mod AttemptLoadVirtualMod(ArchiveFileInfo sfarEntry, SevenZipExtractor archive, string md5)
        {
            var sfarPath = sfarEntry.FileName;
            var cookedPath = FilesystemInterposer.DirectoryGetParent(sfarPath, true);
            //Todo: Check if value is CookedPC/CookedPCConsole as further validation
            if (!string.IsNullOrEmpty(FilesystemInterposer.DirectoryGetParent(cookedPath, true)))
            {
                var dlcDir = FilesystemInterposer.DirectoryGetParent(cookedPath, true);
                var dlcFolderName = Path.GetFileName(dlcDir);
                if (!string.IsNullOrEmpty(dlcFolderName))
                {
                    var thirdPartyInfo = TPMIService.GetThirdPartyModInfo(dlcFolderName, MEGame.ME3);
                    if (thirdPartyInfo != null)
                    {
                        if (thirdPartyInfo.PreventImport == false)
                        {
                            M3Log.Information($@"Third party mod found: {thirdPartyInfo.modname}, preparing virtual moddesc.ini");
                            //We will have to load a virtual moddesc. Since Mod constructor requires reading an ini, we will build and feed it a virtual one.
                            IniData virtualModDesc = new IniData();
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER] = App.HighestSupportedModDesc.ToString();
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_IMPORTEDBY] = App.BuildNumber.ToString();
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME] = @"ME3";
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME] = thirdPartyInfo.modname;
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER] = thirdPartyInfo.moddev;
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_SITE] = thirdPartyInfo.modsite;
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION] = thirdPartyInfo.moddesc;
                            virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_UNOFFICIAL] = Mod.MODDESC_VALUE_TRUE;
                            if (int.TryParse(thirdPartyInfo.updatecode, out var updatecode) && updatecode > 0)
                            {
                                virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_UPDATECODE] = updatecode.ToString();
                                virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION] = 0.001.ToString(CultureInfo.InvariantCulture); //This will force mod to check for update after reload
                            }
                            else
                            {
                                virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION] = 0.0.ToString(CultureInfo.InvariantCulture); //Will attempt to look up later after mods have parsed.
                            }

                            virtualModDesc[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS] = dlcFolderName;
                            virtualModDesc[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS] = dlcFolderName;

                            var archiveSize = archive.ArchiveSize;
                            var importingInfos = TPIService.GetImportingInfosBySize(archiveSize);
                            if (importingInfos.Count == 1 && importingInfos[0].GetParsedRequiredDLC().Count > 0)
                            {
                                M3OnlineContent.QueryModRelay(importingInfos[0].md5, archiveSize); //Tell telemetry relay we are accessing the TPIS for an existing item so it can update latest for tracking
                                virtualModDesc[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_REQUIREDDLC] = importingInfos[0].requireddlc;
                            }

                            return new Mod(virtualModDesc.ToString(), FilesystemInterposer.DirectoryGetParent(dlcDir, true), archive);
                        }

                        //Mod is marked for preventing import
                        return new Mod(false)
                        {
                            ModName = thirdPartyInfo.modname,
                            ModDeveloper = thirdPartyInfo.moddev,
                            LoadFailedReason = M3L.GetString(M3L.string_modCannotBeImportedDueToOneOfTheFollowingReasons)
                        };
                    }
                    else
                    {
                        M3Log.Information($@"No third party mod information for importing {dlcFolderName}. Should this be supported for import? Contact Mgamerz on the ME3Tweaks Discord if it should.");
                    }
                }
            }
            return null;
        }

    }
}
