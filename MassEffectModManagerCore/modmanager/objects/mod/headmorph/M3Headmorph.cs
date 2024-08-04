﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using Newtonsoft.Json;
using Pathoschild.FluentNexus.Models;

namespace ME3TweaksModManager.modmanager.objects.mod.headmorph
{
    /// <summary>
    /// A ModDesc-mod headmorph reference - pinned to a .me3headmorph and .ron file
    /// </summary>
    public class M3Headmorph : M3ValidateOnLoadObject, IM3ImageEnabled
    {
        /// <summary>
        /// Blank constructor
        /// </summary>
        public M3Headmorph()
        {
        }

        /// <summary>
        /// The filename of the headmorph
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The title of the headmorph when being shown to the user
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The description of the headmorph when being shown to the user
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The image asset to show in the the user interface when this headmorph is to be presented to the user, as part of a preview
        /// </summary>
        public string ImageAssetName { get; set; }

        public List<DLCRequirement> RequiredDLC { get; } = new(0); // Init so serializer doesn't fail

        /// <summary>
        /// The height of the image to display when shown in a tooltip
        /// </summary>
        [JsonIgnore]
        public int ImageHeight { get; set; }

        #region PARAMETERS

        private const string FILENAME_PARM = @"Filename";
        private const string TITLE_PARM = @"Title";
        private const string DESCRIPTION_PARM = @"Description";
        private const string IMAGE_PARM = @"ImageAsset";
        private const string IMAGE_HEIGHT_PARM = @"ImageHeight";
        private const string REQUIRED_DLC_PARM = @"RequiredDLC";
        #endregion

        /// <summary>
        /// Initializes a headmorph object from a mod and the ini struct of the headmorph
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="headmorphStruct"></param>
        public M3Headmorph(Mod mod, string headmorphStruct)
        {
            var parms = StringStructParser.GetCommaSplitValues(headmorphStruct, canBeCaseInsensitive: true);
            if (!ValidateParameters(nameof(M3Headmorph), parms, FILENAME_PARM, TITLE_PARM))
                return;

            if (ValidateFileParameter(mod, nameof(M3Headmorph), parms, FILENAME_PARM, Mod.HEADMORPHS_FOLDER_NAME,
                    required: true))
            {
                FileName = parms[FILENAME_PARM];
            }
            else
            {
                return; // Validation failed, the parameter will be set and logged already.
            }

            // Title already validated
            Title = parms[TITLE_PARM];

            Description = parms.ContainsKey(DESCRIPTION_PARM) ? parms[DESCRIPTION_PARM] : null;
            if (parms.ContainsKey(IMAGE_PARM) && ValidateImageParameter(mod, nameof(M3Headmorph), parms, IMAGE_PARM, false,
                    false,
                    additionalRequiredParam:
                    IMAGE_HEIGHT_PARM)) // Should headmorphs be installable directly from archive...?
            {
                ImageAssetName = parms[IMAGE_PARM];
                if (int.TryParse(parms[IMAGE_HEIGHT_PARM], out var imageHeight))
                {
                    if (imageHeight < 1)
                    {
                        M3Log.Error($@"{IMAGE_HEIGHT_PARM} value must be an integer greater than 0.");
                        ValidationFailedReason = M3L.GetString(M3L.string_interp_valueMustBeIntegerGreaterThanZero, IMAGE_HEIGHT_PARM);
                        return;
                    }

                    ImageHeight = imageHeight;
                }
            }

            // Parse required DLC (if any)
            if (parms.TryGetValue(REQUIRED_DLC_PARM, out var requiredDLCText) && !string.IsNullOrWhiteSpace(requiredDLCText))
            {
                var requiredDlcsSplit = requiredDLCText.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var reqDLC in requiredDlcsSplit)
                {
                    if (MEDirectories.OfficialDLC(mod.Game).Contains(reqDLC, StringComparer.InvariantCultureIgnoreCase))
                    {
                        M3Log.Error($@"Headmorphs cannot mark themselves as dependent on DLC included with the game. Invalid value: {reqDLC}");
                        ValidationFailedReason = M3L.GetString(M3L.string_interp_headmorphsCannotDependOnIncludedDLC, reqDLC);
                        return;
                    }

                    if (!reqDLC.StartsWith(@"DLC_"))
                    {
                        M3Log.Error($@"Required DLC does not start with DLC_: {reqDLC}");
                        ValidationFailedReason = $@"Headmorph {Title} has a DLC requirement that does not start with DLC_: {reqDLC}";
                        return;
                    }

                    M3Log.Information($@"Adding DLC requirement to {nameof(M3Headmorph)}: {reqDLC}", Settings.LogModStartup);
                    RequiredDLC.Add(DLCRequirement.ParseRequirement(reqDLC, true, false));
                }
            }

            ModdescMod = mod;
        }

        /// <summary>
        /// Gets the list of referenced files, relative to the root of the mod.
        /// </summary>
        /// <param name="mod">The mod we are gathering relative references for</param>
        /// <returns>A list of strings that represent relative files</returns>
        public IEnumerable<string> GetRelativeReferences(Mod mod)
        {
            List<string> references = new List<string>();
            var fullPath =
                FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, Mod.HEADMORPHS_FOLDER_NAME, FileName);
            references.Add((mod.IsInArchive && mod.ModPath.Length == 0)
                ? fullPath
                : fullPath.Substring(mod.ModPath.Length + 1)); // Add the headmorph file

            if (ImageAssetName != null)
            {
                var imageAssetPath =
                    FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModImageAssetsPath, ImageAssetName);
                references.Add((mod.IsInArchive && mod.ModPath.Length == 0)
                    ? imageAssetPath
                    : imageAssetPath.Substring(mod.ModPath.Length + 1)); // Add the image asset file
            }

            return references;
        }

        /// <summary>
        /// Parameter map, used for the moddesc.ini editor Contains a list of values in the alternate mapped to their string value
        /// </summary>
        [JsonIgnore]
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } =
            new ObservableCollectionExtended<MDParameter>();

        /// <summary>
        /// List of all keys in the M3MEMMod struct that are publicly available for editing
        /// </summary>
        /// <param name="mod"></param>
        public void BuildParameterMap(Mod mod)
        {
            var parameterDictionary = new Dictionary<string, object>()
            {
                // List of available parameters for this object
                { FILENAME_PARM, new MDParameter(@"string", FILENAME_PARM, FileName, mod.PopulateHeadmorphFileOptions(), "") { AllowedValuesPopulationFunc = mod.PopulateHeadmorphFileOptions}}, // Uses population function
                { TITLE_PARM, Title },
                { DESCRIPTION_PARM, Description },
                { IMAGE_PARM, new MDParameter(@"string", IMAGE_PARM, ImageAssetName, mod.PopulateImageFileOptions(), ""){ AllowedValuesPopulationFunc = mod.PopulateImageFileOptions } }, // Uses image population function
                { IMAGE_HEIGHT_PARM, ImageHeight },
                { REQUIRED_DLC_PARM, string.Join(';',RequiredDLC.Select(x=>x.Serialize(false)))},

            };

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }

        // IM3ImageEnabled interface
        public Mod ModdescMod { get; set; }
        public BitmapSource ImageBitmap { get; set; }
    }
}
