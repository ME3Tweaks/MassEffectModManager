﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using Microsoft.AppCenter.Analytics;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        private bool checkedEndorsementStatus;

        // This is technically not nexus but it's close enough
        public bool IsCheckingForUpdates { get; set; }
        public bool IsEndorsed { get; set; }
        public bool IsOwnMod { get; set; }
        public bool CanEndorse { get; set; }
        //public string EndorsementStatus { get; set; } = "Endorse mod";

        public async Task<bool?> GetEndorsementStatus()
        {
            if (!NexusModsUtilities.HasAPIKey || NexusModsUtilities.UserInfo == null) return false;
            if (checkedEndorsementStatus) return IsEndorsed;
            try
            {
                var client = NexusModsUtilities.GetClient();
                string gamename = @"masseffect";
                if (Game == MEGame.ME2) gamename += @"2";
                if (Game == MEGame.ME3) gamename += @"3";
                if (Game.IsLEGame()) gamename += @"legendaryedition";
                var modinfo = await client.Mods.GetMod(gamename, NexusModID);
                if (modinfo.User.MemberID == NexusModsUtilities.UserInfo.UserID)
                {
                    IsEndorsed = false;
                    CanEndorse = false;
                    IsOwnMod = true;
                    checkedEndorsementStatus = true;
                    return null; //cannot endorse your own mods
                }
                var endorsementstatus = modinfo.Endorsement;
                if (endorsementstatus != null)
                {
                    if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Undecided || endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Abstained)
                    {
                        IsEndorsed = false;
                    }
                    else if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Endorsed)
                    {
                        IsEndorsed = true;
                    }

                    CanEndorse = true;
                }
                else
                {
                    IsEndorsed = false;
                    CanEndorse = false;
                }
                checkedEndorsementStatus = true;
                return IsEndorsed;
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error getting endorsement status: " + e.Message);
                return false; //null would mean own mod. so just say its not endorsed atm.
            }
        }

        /// <summary>
        /// Attempts to endorse/unendorse this mod on NexusMods.
        /// </summary>
        /// <param name="newEndorsementStatus"></param>
        /// <param name="endorse"></param>
        /// <param name="currentuserid"></param>
        public void EndorseMod(Action<Mod, bool, string> endorsementResultCallback, bool endorse)
        {
            if (NexusModsUtilities.UserInfo == null || !CanEndorse) return;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModSpecificEndorsement");
            nbw.DoWork += (a, b) =>
            {
                var client = NexusModsUtilities.GetClient();
                string gamename = @"masseffect";
                if (Game == MEGame.ME2) gamename += @"2";
                if (Game == MEGame.ME3) gamename += @"3";
                if (Game.IsLEGame() || Game == MEGame.LELauncher) gamename += @"legendaryedition";
                string telemetryOverride = null;
                string endorsementFailedReason = null;
                try
                {
                    if (endorse)
                    {
                        client.Mods.Endorse(gamename, NexusModID, @"1.0").Wait();
                    }
                    else
                    {
                        client.Mods.Unendorse(gamename, NexusModID, @"1.0").Wait();
                    }
                }
                catch (Exception e)
                {
                    if (e.InnerException != null)
                    {
                        if (e.InnerException.Message == @"NOT_DOWNLOADED_MOD")
                        {
                            // User did not download this mod from NexusMods
                            endorsementFailedReason = M3L.GetString(M3L.string_dialog_cannotEndorseNonDownloadedMod);
                        }
                        else if (e.InnerException.Message == @"TOO_SOON_AFTER_DOWNLOAD")
                        {
                            endorsementFailedReason = M3L.GetString(M3L.string_dialog_cannotEndorseUntil15min);
                        }
                    }
                    else
                    {
                        telemetryOverride = e.ToString();
                    }
                    M3Log.Error(@"Error endorsing/unendorsing: " + e.ToString());
                }

                checkedEndorsementStatus = false;
                IsEndorsed = GetEndorsementStatus().Result ?? false;
                TelemetryInterposer.TrackEvent(@"Set endorsement for mod", new Dictionary<string, string>
                {
                    {@"Endorsed", endorse.ToString() },
                    {@"Succeeded", telemetryOverride ?? (endorse == IsEndorsed).ToString() }
                });
                b.Result = endorsementFailedReason;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    // Log handled internally by nbw
                }
                else if (b.Result is string endorsementFailedReason)
                {
                    endorsementResultCallback.Invoke(this, IsEndorsed, endorsementFailedReason);
                }
                else
                {
                    endorsementResultCallback.Invoke(this, IsEndorsed, null);

                }
            };
            nbw.RunWorkerAsync();
        }
    }
}