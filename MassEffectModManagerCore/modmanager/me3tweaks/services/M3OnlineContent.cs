﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using ME3TweaksCore.Misc;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{

    partial class M3OnlineContent
    {
        /// <summary>
        /// URL where the documentation for moddesc lives
        /// </summary>
        public const string MODDESC_DOCUMENTATION_LINK = @"https://github.com/ME3Tweaks/ME3TweaksModManager/tree/staticfiles/documentation#readme";


        #region FALLBACKS
        /// <summary>
        /// Endpoint (base URL) for downloading static assets
        /// </summary>
        internal static FallbackLink StaticFileBaseEndpoints { get; } = new()
        {
            // v1 endpoint
            MainURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/staticfiles/liveservices/staticfiles/v1/",
            FallbackURL = @"https://me3tweaks.com/modmanager/tools/staticfiles/"
        };

        #endregion

        private const string ThirdPartyModDescURL = @"https://me3tweaks.com/mods/dlc_mods/importingmoddesc/";
        private const string ExeTransformBaseURL = @"https://me3tweaks.com/mods/dlc_mods/importingexetransforms/";
        private const string ModInfoRelayEndpoint = @"https://me3tweaks.com/modmanager/services/relayservice";
        private const string TipsServiceURL = @"https://me3tweaks.com/modmanager/services/tipsservice";
        private const string ModMakerTopModsEndpoint = @"https://me3tweaks.com/modmaker/api/topmods";
        private const string LocalizationEndpoint = @"https://me3tweaks.com/modmanager/services/livelocalizationservice";
        public static readonly string ModmakerModsEndpoint = @"https://me3tweaks.com/modmaker/download.php?id=";


        /// <summary>
        /// Checks if we can perform an online content fetch. This value is updated when manually checking for content updates, and on automatic 1-day intervals (if no previous manual check has occurred)
        /// </summary>
        /// <returns></returns>
        internal static bool CanFetchContentThrottleCheck()
        {
            var lastContentCheck = Settings.LastContentCheck;
            var timeNow = DateTime.Now;
            return (timeNow - lastContentCheck).TotalDays > 1;
        }

        public static (MemoryStream download, string errorMessage) DownloadStaticAsset(string assetName, Action<long, long> progressCallback = null)
        {
            (MemoryStream, string) result = (null, @"Could not download file: No attempt was made, or errors occurred!");
            foreach (var staticurl in StaticFileBaseEndpoints.GetAllLinks())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    {
                        var fullURL = staticurl + assetName;
                        result = DownloadToMemory(fullURL, logDownload: true, progressCallback: progressCallback);
                        if (result.Item2 == null) return result;
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Downloads a static asset that is mirrored onto the ME3Tweaks Assets repo. This is not the same as the github version of staticfiles.
        /// </summary>
        /// <param name="assetName">The asset filename. Do not include any path information.</param>
        /// <returns></returns>
        public static (MemoryStream download, string errorMessage) DownloadME3TweaksStaticAsset(string assetName)
        {
            (MemoryStream, string) result = (null, @"Could not download file: No attempt was made, or errors occurred!");
            foreach (var staticurl in StaticFileBaseEndpoints.GetAllLinks())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    {
                        var fullURL = staticurl + Path.GetFileNameWithoutExtension(assetName) + "/" + assetName;
                        result = DownloadToMemory(fullURL, logDownload: true);
                        if (result.Item2 == null) return result;
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
                }
            }

            return result;
        }


        public static string FetchRemoteString(string url, string authorizationToken = null)
        {
            try
            {
                using var wc = new ShortTimeoutWebClient();
                if (authorizationToken != null)
                {
                    wc.Headers.Add(@"Authorization", authorizationToken);
                }
                return WebClientExtensions.DownloadStringAwareOfEncoding(wc, url);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error downloading string: " + e.Message);
                return null;
            }
        }

        public static string FetchThirdPartyModdesc(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            M3Log.Information($@"Fetching online moddesc: {name}");
            string moddesc = WebClientExtensions.DownloadStringAwareOfEncoding(wc, ThirdPartyModDescURL + name);
            return moddesc;
        }

        public static List<ServerModMakerModInfo> FetchTopModMakerMods()
        {
            var topModsJson = FetchRemoteString(ModMakerTopModsEndpoint);
            if (topModsJson != null)
            {
                try
                {
                    return JsonConvert.DeserializeObject<List<ServerModMakerModInfo>>(topModsJson);
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Error converting top mods response to json: " + e.Message);
                }
            }

            return new List<ServerModMakerModInfo>();
        }

        public static string FetchExeTransform(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            string moddesc = WebClientExtensions.DownloadStringAwareOfEncoding(wc, ExeTransformBaseURL + name);
            return moddesc;
        }

        public static (MemoryStream result, string errorMessage) FetchString(string url)
        {
            using var wc = new ShortTimeoutWebClient();
            string downloadError = null;
            MemoryStream responseStream = null;
            wc.DownloadDataCompleted += (a, args) =>
            {
                downloadError = args.Error?.Message;
                if (downloadError == null)
                {
                    responseStream = new MemoryStream(args.Result);
                }
                lock (args.UserState)
                {
                    //releases blocked thread
                    Monitor.Pulse(args.UserState);
                }
            };
            var syncObject = new Object();
            lock (syncObject)
            {
                Debug.WriteLine(@"Download file to memory: " + url);
                wc.DownloadDataAsync(new Uri(url), syncObject);
                //This will block the thread until download completes
                Monitor.Wait(syncObject);
            }

            return (responseStream, downloadError);
        }

        /// <summary>
        /// Queries the ME3Tweaks Mod Relay for information about a file with the specified md5 and size.
        /// </summary>
        /// <param name="md5"></param>
        /// <param name="size"></param>
        /// <returns>Dictionary of information about the file, if any.</returns>
        public static Dictionary<string, string> QueryModRelay(string md5, long size)
        {
            //Todo: Finish implementing relay service
            string finalRelayURL = $@"{ModInfoRelayEndpoint}?modmanagerversion={App.BuildNumber}&md5={md5.ToLowerInvariant()}&size={size}";
            try
            {
                using (var wc = new ShortTimeoutWebClient())
                {
                    Debug.WriteLine(finalRelayURL);
                    string json = WebClientExtensions.DownloadStringAwareOfEncoding(wc, finalRelayURL);
                    //todo: Implement response format serverside
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error querying relay service from ME3Tweaks: " + App.FlattenException(e));
            }

            return null;
        }

        /// <summary>
        /// Downloads from a URL to memory. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="progressCallback">Progress information clalback</param>
        /// <param name="hash">Hash check value (md5). Leave null if no hash check</param>
        /// <returns></returns>
        public static (MemoryStream result, string errorMessage) DownloadToMemory(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null, CancellationToken cancellationToken = default)
        {
            var resultV = DownloadToStreamInternal(url, progressCallback, hash, logDownload, cancellationToken: cancellationToken);
            return (resultV.result as MemoryStream, resultV.errorMessage);
        }

        /// <summary>
        /// Downloads a URL to the specified stream. If not stream is specifed, the stream returned is a MemoryStream. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="progressCallback"></param>
        /// <param name="hash"></param>
        /// <param name="logDownload"></param>
        /// <param name="destStreamOverride"></param>
        /// <returns></returns>
        public static (Stream result, string errorMessage) DownloadToStream(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null, CancellationToken cancellationToken = default)
        {
            return DownloadToStreamInternal(url, progressCallback, hash, logDownload, destStreamOverride, cancellationToken);
        }

        private static (Stream result, string errorMessage) DownloadToStreamInternal(string url,
            Action<long, long> progressCallback = null,
            string hash = null,
            bool logDownload = false,
            Stream destStreamOverride = null,
            CancellationToken cancellationToken = default)
        {
            using var wc = new ShortTimeoutWebClient();
            string downloadError = null;
            string destType = destStreamOverride?.GetType().ToString() ?? @"memory";
            Stream responseStream = destStreamOverride ?? new MemoryStream();

            var syncObject = new Object();
            lock (syncObject)
            {
                if (logDownload)
                {
                    M3Log.Information($@"Downloading to {destType}: " + url);
                }
                else
                {
                    Debug.WriteLine($@"Downloading to {destType}: " + url);
                }

                try
                {
                    using var remoteStream = wc.OpenRead(new Uri(url));
                    long.TryParse(wc.ResponseHeaders[@"Content-Length"], out var totalSize);
                    var buffer = new byte[4096];
                    int bytesReceived;
                    while ((bytesReceived = remoteStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            downloadError = M3L.GetString(M3L.string_theDownloadWasCanceled);
                            return (responseStream, downloadError);
                        }
                        responseStream.Write(buffer, 0, bytesReceived);
                        progressCallback?.Invoke(responseStream.Position, totalSize); // Progress
                    }

                    // Check hash
                    if (hash != null)
                    {
                        responseStream.Position = 0;
                        var md5 = MD5.Create().ComputeHashAsync(responseStream, cancellationToken, x => progressCallback?.Invoke(x, 100)).Result;
                        responseStream.Position = 0;
                        if (md5 != hash)
                        {
                            responseStream = null;
                            downloadError = M3L.GetString(M3L.string_interp_onlineContentHashWrong, url, hash, md5); //needs localized
                        }
                    }
                }
                catch (Exception e)
                {
                    downloadError = e.InnerException?.Message ?? e.Message;
                }
            }

            return (responseStream, downloadError);
        }

        [Localizable(true)]
        public class ServerModMakerModInfo
        {

            public string mod_id { get; set; }
            public string mod_name { get; set; }
            public string mod_desc { get; set; }
            public string revision { get; set; }
            public string username { get; set; }

            public string UIRevisionString => M3L.GetString(M3L.string_interp_revisionX, revision);
            public string UICodeString => M3L.GetString(M3L.string_interp_codeX, mod_id);
        }
    }
}
