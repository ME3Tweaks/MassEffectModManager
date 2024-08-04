﻿using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.localizations;
using System.Diagnostics;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services.ThirdPartyModIdentification;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class TextureChecks
    {
        /// <summary>
        /// Adds texture checks to the encompassing mod deployment checks.
        /// </summary>
        /// <param name="check"></param>
        public static void AddTextureChecks(EncompassingModDeploymentCheck check)
        {
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_texturesCheck),
                ModToValidateAgainst = check.ModBeingDeployed,
                DialogMessage = M3L.GetString(M3L.string_texturesCheckDetectedErrors),
                DialogTitle = M3L.GetString(M3L.string_textureErrorsInMod),
                ValidationFunction = CheckTextures,
                internalValidationTarget = check.internalValidationTarget,
            });
        }

        /// <summary>
        /// Checks texture references and known bad texture setups
        /// </summary>
        /// <param name="item"></param>
        private static void CheckTextures(DeploymentChecklistItem item)
        {
            // if (item.ModToValidateAgainst.Game >= MEGame.ME2)
            //{

            // LE1: TFC in basegame
            if (item.ModToValidateAgainst.Game == MEGame.LE1)
            {
                var installableFiles = item.ModToValidateAgainst.GetAllInstallableFiles();
                var basegameTFCs = installableFiles.Where(x => x.Replace('/', '\\').TrimStart('\\').StartsWith(@"BioGame\CookedPCConsole\", StringComparison.InvariantCultureIgnoreCase) && x.EndsWith(@".tfc")).ToList();
                foreach (var basegameTFC in basegameTFCs)
                {
                    M3Log.Error($@"Found basegame TFC being deployed for LE1: {basegameTFC}");
                    item.AddBlockingError(M3L.GetString(M3L.string_interp_cannotInstallTFCToBasegameLE1, Path.GetFileName(basegameTFC)));
                }
            }

            // CHECK REFERENCES
            item.ItemText = M3L.GetString(M3L.string_checkingTexturesInMod);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences().Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            var allTFCs = referencedFiles.Where(x => Path.GetExtension(x) == @".tfc").ToList();
            int numChecked = 0;
            foreach (var f in referencedFiles)
            {
                if (item.CheckDone) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingTexturesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    var relativePath = f.Substring(item.ModToValidateAgainst.ModPath.Length + 1);
                    M3Log.Information(@"Checking file for broken textures: " + f);
                    using var package = MEPackageHandler.UnsafePartialLoad(f, x => x.IsTexture() && !x.IsDefaultObject); // 06/12/2022 - Use unsafe partial load to increase performance
                    if (package.Game != item.ModToValidateAgainst.Game)
                        continue; // Don't bother checking this
                    if (package.Game.IsLEGame() && package.LECLTagData.WasSavedWithMEM)
                    {
                        // Cannot ship texture touched files.
                        M3Log.Error($@"Found package that was part of a texture modded game install: {relativePath}. Cannot ship this package.");
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_foundTextureModdedPackageCannotShip, relativePath));
                        continue; // No further checks on this file.
                    }
                    else if (package.Game.IsOTGame())
                    {
                        using var fs = File.OpenRead(f);
                        fs.Seek(-MEPackage.MEMPackageTagLength, SeekOrigin.End);
                        if (MEPackage.MEMPackageTag.SequenceEqual(fs.ReadToBuffer(MEPackage.MEMPackageTagLength)))
                        {
                            M3Log.Error($@"Found package that was part of a texture modded game install: {relativePath}. Cannot ship this package.");
                            item.AddBlockingError(M3L.GetString(M3L.string_interp_foundTextureModdedPackageCannotShip, relativePath));
                        }

                        continue; // No further checks on this file.
                    }

                    var textures = package.Exports.Where(x => x.IsTexture() && !x.IsDefaultObject).ToList();
                    foreach (var texture in textures)
                    {
                        if (item.CheckDone) return;

                        if (package.Game > MEGame.ME1)
                        {
                            // 05/29/2022 - Does this affect LE?

                            // 06/19/2024 - Disable this for LE as it references texture LODs which we don't touch.
                            Texture2D tex = new Texture2D(texture);

                            if (package.Game.IsOTGame())
                            {
                                // CHECK NEVERSTREAM
                                // 1. Has more than six mips.
                                // 2. Has no external mips.

                                var topMip = tex.GetTopMip();
                                if (topMip.storageType == StorageTypes.pccUnc)
                                {
                                    // It's an internally stored texture
                                    if (!tex.NeverStream && tex.Mips.Count(x => x.storageType != StorageTypes.empty) > 6)
                                    {
                                        // NEVERSTREAM SHOULD HAVE BEEN SET.
                                        M3Log.Error(@"Found texture missing 'NeverStream' attribute " + texture.InstancedFullPath);
                                        item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalMissingNeverstreamFlag, relativePath, texture.UIndex, texture.InstancedFullPath));
                                    }
                                }

                                if (package.Game == MEGame.ME3) // ME3 only. does not affect LE
                                {
                                    // CHECK FOR 4K NORM
                                    var compressionSettings = texture.GetProperty<EnumProperty>(@"CompressionSettings");
                                    if (compressionSettings != null && compressionSettings.Value == @"TC_NormalMapUncompressed")
                                    {
                                        var mipTailBaseIdx = texture.GetProperty<IntProperty>(@"MipTailBaseIdx");
                                        if (mipTailBaseIdx != null && mipTailBaseIdx == 12)
                                        {
                                            // It's 4K (2^12)
                                            M3Log.Error(@"Found 4K Norm. These are not used by game (they use up to 1 mip below the diff) and waste large amounts of memory. Drop the top mip to correct this issue. " + texture.InstancedFullPath);
                                            item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalFound4KNorm, relativePath, texture.UIndex, texture.InstancedFullPath));
                                        }
                                    }
                                }
                            }


                            var cache = texture.GetProperty<NameProperty>(@"TextureFileCacheName");
                            if (cache != null)
                            {
                                if (!VanillaDatabaseService.IsBasegameTFCName(cache.Value, item.ModToValidateAgainst.Game))
                                {
                                    //var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    try
                                    {
                                        tex.GetImageBytesForMip(tex.GetTopMip(), item.internalValidationTarget.Game, false, out _, item.internalValidationTarget.TargetPath, allTFCs); //use active target
                                    }
                                    catch (Exception e)
                                    {
                                        M3Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                    }
                                }

                                if (cache.Value.Name.Contains(@"CustTextures"))
                                {
                                    // ME3Explorer 3.0 or below Texplorer
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_foundCustTexturesTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
                                }
                                else if (cache.Value.Name.Contains(@"TexturesMEM"))
                                {
                                    // Textures replaced by MEM. This is not allowed in mods as it'll immediately be broken
                                    item.AddBlockingError(M3L.GetString(M3L.string_interp_error_foundTexturesMEMTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
                                }
                            }
                        }
                        else
                        {
                            Texture2D tex = new Texture2D(texture);
                            var cachename = tex.GetTopMip().TextureCacheName;
                            if (cachename != null)
                            {
                                foreach (var mip in tex.Mips)
                                {
                                    try
                                    {
                                        tex.GetImageBytesForMip(mip, item.internalValidationTarget.Game, false, out _, item.internalValidationTarget.TargetPath);
                                    }
                                    catch (Exception e)
                                    {
                                        M3Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Mod Manager 8.2: Check if shipping another mods TFC
            var dlcFolderDests = item.ModToValidateAgainst.GetAllPossibleCustomDLCFolders();
            foreach (var tfc in allTFCs)
            {
                var tfcName = Path.GetFileName(tfc);
                string strippedTFCName = Path.GetFileNameWithoutExtension(tfcName);
                if (strippedTFCName.Contains(@"DLC_MOD"))
                {
                    strippedTFCName = strippedTFCName.Substring(strippedTFCName.IndexOf(@"DLC_MOD"));
                    if (dlcFolderDests.Contains(strippedTFCName))
                        continue; // DLC belongs to this mod

                    var tpmi = TPMIService.GetThirdPartyModInfo(strippedTFCName, item.ModToValidateAgainst.Game);
                    if (tpmi != null)
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_detectedTFCFromAnotherModTPMI, tfcName, tpmi.modname));
                    }
                    else
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_detectedTFCFromAnotherModUnknown, tfcName));
                    }
                }

                // Future warning for moddesc 9.1 (maybe 10), unsure. Include at least 
                if (!tfcName.ContainsAny(dlcFolderDests, StringComparison.InvariantCultureIgnoreCase))
                {
                    item.AddInfoWarning(M3L.GetString(M3L.string_interp_futureModdescRequirementTFCName, tfcName));
                }

                Debug.WriteLine(tfc);
            }

            // Mod Manager 9: Do not allow multiple same-named TFCs
            // Warn on older cmmver.
            // Check for multiple same-tfcs
            var duplicates = allTFCs
                .GroupBy(i => i)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key).ToList();

            if (duplicates.Any())
            {
                if (item.ModToValidateAgainst.ModDescTargetVersion >= 9.0)
                {
                    item.AddBlockingError(
                        M3L.GetString(M3L.string_interp_cannotShipMultipleSameTFC, string.Join(',', duplicates)));
                }
                else
                {
                    item.AddSignificantIssue(
                        M3L.GetString(M3L.string_interp_modsShouldNotShipMultipleSameTFCMD8, string.Join(',', duplicates)));
                }
            }

            if (!item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_noBrokenTexturesWereFound);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_textureIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
        }
    }
}
