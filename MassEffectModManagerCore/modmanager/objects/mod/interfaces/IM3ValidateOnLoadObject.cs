﻿using ME3TweaksModManager.modmanager.localizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.interfaces
{
    /// <summary>
    /// Indicates this object is serialized on mod load and can be validated at that point. Failure to validate should cause the mod to fail to load
    /// </summary>
    public class M3ValidateOnLoadObject
    {
        [JsonIgnore]
        public string ValidationFailedReason { get; set; }

        public bool ValidateParameters(string structName, Dictionary<string, string> parms, params string[] requiredValues)
        {
            foreach (var parm in requiredValues)
            {
                if (!parms.ContainsKey(parm))
                {
                    M3Log.Error($@"{structName} is missing required required parameter '{parm}'.");
                    ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_missingRequiredParameter, structName, parm);
                    return false;
                }
            }

            // Ensure it is set to null
            ValidationFailedReason = null;
            return true;
        }

        /// <summary>
        /// Validates a file reference exists in the mod and is referenced in a valid manner.
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="structName"></param>
        /// <param name="parms"></param>
        /// <param name="fileParamName"></param>
        /// <param name="fileFolderName"></param>
        /// <param name="required"></param>
        /// <returns>True if validation succeeded, false otherwise</returns>
        public bool ValidateFileParameter(Mod mod, string structName, Dictionary<string, string> parms, string fileParamName, string fileFolderName, bool required = true)
        {
            if (!parms.ContainsKey(fileParamName) || string.IsNullOrWhiteSpace(parms[fileParamName]))
            {
                if (required)
                {
                    M3Log.Error($@"{structName} is missing required file parameter '{fileParamName}'.");
                    ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_missingRequiredFileParameter, structName, fileParamName);
                    return false;
                }
                else
                {
                    return true; // Nothing set, nothing to validate.
                }
            }

            var fileAsset = parms[fileParamName];

            // Security pass
            if (fileAsset.StartsWith(@"/") || fileAsset.StartsWith(@"\\") || fileAsset.Contains(@".."))
            {
                M3Log.Error($@"{structName} references file {fileAsset}, which contains invalid patterns. File references cannot contain .. or start with / or \.");
                ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_invalidCharacterPatterns, structName, fileAsset);
                return false;
            }

            // Verify asset exists.
            var fullPath = FilesystemInterposer.PathCombine(mod.Archive != null, mod.ModPath, fileFolderName, fileAsset);
            if (!FilesystemInterposer.FileExists(fullPath, mod.Archive))
            {
                M3Log.Error($@"{structName} references file asset {fileAsset}, but this file does not exist under the {fileFolderName} directory.");
                ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_nonExistentFileReference, structName, fileAsset, fileFolderName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies an image asset exists and loads it if this mod is in an archive, if designated. Does not load the asset if improperly referenced.
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="structName"></param>
        /// <param name="parms"></param>
        /// <param name="imageParmName"></param>
        /// <param name="required"></param>
        /// <param name="canBeAccessedViaArchiveMod"></param>
        /// <param name="additionalRequiredParam"></param>
        /// <returns>True if validation succeeded, false otherwise</returns>
        public bool ValidateImageParameter(Mod mod, string structName, Dictionary<string, string> parms,
            string imageParmName, bool required = true, bool canBeAccessedViaArchiveMod = true,
            string additionalRequiredParam = null)
        {
            if (!parms.ContainsKey(imageParmName) || string.IsNullOrWhiteSpace(parms[imageParmName]))
            {
                if (required)
                {
                    M3Log.Error($@"{structName} is missing required image parameter '{imageParmName}'.");
                    ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_missingRequiredImageParameter, structName, imageParmName);
                    return false;
                }
                else
                {
                    return true; // Nothing set, nothing to validate.
                }
            }

            if (additionalRequiredParam != null && (!parms.ContainsKey(additionalRequiredParam) || string.IsNullOrWhiteSpace(parms[additionalRequiredParam])))
            {
                M3Log.Error($@"{structName} is missing required image parameter '{additionalRequiredParam}'.");
                ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_missingRequiredImageParameter, structName, additionalRequiredParam);
                return false;
            }

            // Verify asset exists.
            var imageAsset = parms[imageParmName];
            var fullPath = FilesystemInterposer.PathCombine(mod.Archive != null, mod.ModPath, Mod.M3IMAGES_FOLDER_NAME, imageAsset);
            if (!FilesystemInterposer.FileExists(fullPath, mod.Archive))
            {
                M3Log.Error($@"{structName} references imageasset {imageAsset}, but this file does not exist under the M3Images directory.");
                ValidationFailedReason = M3L.GetString(M3L.string_interp_im3v_nonExistentImageReference, structName, imageAsset);
                return false;
            }

            // File exists
            if (canBeAccessedViaArchiveMod && mod.Archive != null)
            {
                if (!mod.CheckNonSolidArchiveFile(fullPath))
                    return false; // Archive is solid for this file, it's not dynamically accessible

                // If we are loading from archive we must load it here while the archive stream is still available
                mod.LoadModImageAsset(fullPath);
            }

            return true;
        }
    }
}
