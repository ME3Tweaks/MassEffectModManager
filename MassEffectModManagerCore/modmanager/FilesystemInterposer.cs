﻿using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using SevenZip;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    /// <summary>
    /// Contains methods useful for handling file paths both in and out of mod archives
    /// </summary>
    public class FilesystemInterposer
    {
        /// <summary>
        /// Combines paths and will attempt to keep the separator style
        /// </summary>
        /// <param name="archiveFilesystem">If this is an archive filesystem or not</param>
        /// <param name="paths">Paths to combine</param>
        /// <returns>Combined path</returns>
        public static string PathCombine(bool archiveFilesystem, string pathBase, params string[] paths)
        {
            char separator = '\\'; 
            if (paths == null || !paths.Any())
                return pathBase;

            if (archiveFilesystem)
            {
                pathBase = pathBase.TrimStart('\\', '/'); //archive paths don't start with a / or \
            }


            #region Remove path end slash

            var slash = new[] { '/', '\\' };
            void removeLastSlash(StringBuilder sb)
            {
                if (sb.Length == 0) return;
                if (!slash.Contains(sb[^1])) return;
                sb.Remove(sb.Length - 1, 1);
                removeLastSlash(sb);
            };

            #endregion Remove path end slash

            #region Combine

            var pathSb = new StringBuilder();
            bool skipFirst = pathBase == ""; //If the base path is "", don't apply a separator.
            bool skippedFirst = false;
            pathSb.Append(pathBase);
            removeLastSlash(pathSb);
            foreach (var path in paths)
            {
                if (path == ".")
                {
                    continue; //A . path is the same as nothing so don't parse it. This can be used to have hack workarounds where we need a folder but we don't have one.
                }
                if (!skipFirst || skippedFirst)
                {
                    pathSb.Append(separator);
                }
                else
                {
                    skippedFirst = true;
                }

                pathSb.Append(path);
                removeLastSlash(pathSb);
            }

            #endregion Combine

            #region Append slash if last path contains
            if (paths.Count() > 2)
            {
                if (slash.Contains(paths.Last().Last()))
                    pathSb.Append(separator);
            }
            #endregion Append slash if last path contains

            return pathSb.ToString();
        }

        internal static List<string> DirectoryGetFiles(string directoryPath, SevenZipExtractor archive = null)
        {
            return DirectoryGetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly, archive);
        }

        internal static List<string> DirectoryGetFiles(string directoryPath, string searchPattern, SearchOption directorySearchOption, SevenZipExtractor archive = null)
        {
            if (archive == null) return Directory.GetFiles(directoryPath, searchPattern, directorySearchOption).ToList();
            var fileList = new List<string>();
            string internalSearchPattern = directoryPath.TrimEnd('\'').Replace('/', '\\') + '\\'; //ensures we are looking in directory itself
            int numSlashesInBasepath = internalSearchPattern.Count(f => f == '\\'); //used for same directory search
            var compiledPattern = FindFilesPatternToRegex.Convert(searchPattern);

            foreach (var entry in archive.ArchiveFileData)
            {
                if (entry.IsDirectory) continue; //not a file
                string fname = entry.FileName;
                if (!fname.StartsWith(internalSearchPattern)) continue; // use internal search pattern to avoid another same-level dir that has same path base
                                                                        // //not in this directory.
                                                                        // this bug was found with ME1 Same Gender Romances due to folders named:
                                                                        // Options\NPCs Flirt Regardless of Gender
                                                                        // Options\NPCs Flirt Regardless of Gender - ME1 Recalibrated
                if (directorySearchOption == SearchOption.TopDirectoryOnly && fname.Count(x => x == '\\') != numSlashesInBasepath) continue; //Skip if we are in a different subdirectory
                string nameOnly = Path.GetFileName(fname);
                if (compiledPattern.IsMatch(nameOnly))
                {
                    fileList.Add(entry.FileName);
                }
            }

            return fileList;
        }

        /// <summary>
        /// Checks if a directory exists. This method will look an archive file if one is specified.
        /// </summary>
        /// <param name="path">Path to determine if it's a directory</param>
        /// <param name="archive">Archive to look in. Not specifying this will look on the local filesystem instead</param>
        /// <returns></returns>
        public static bool DirectoryExists(string path, SevenZipExtractor archive = null)
        {
            if (archive != null)
            {
                path = path.TrimStart('\\', '/'); //archive paths don't start with a / \ but path combining will append one of these.
                var entry = archive.ArchiveFileData.FirstOrDefault(x => x.FileName.Equals(path, StringComparison.InvariantCultureIgnoreCase));
                if (!string.IsNullOrEmpty(entry.FileName) && entry.IsDirectory) return true;//must check filename is populated as this is a struct
                //if this is zip archive it might not have entry for folder specifically. We should look for a subfile that will create this folder.
                if (archive.Format == InArchiveFormat.Zip || archive.Format == InArchiveFormat.Nsis)
                {
                    return archive.ArchiveFileData.Any(x => x.FileName.StartsWith(path + "\\"));
                }
                return false;
            }
            else
            {
                return Directory.Exists(path);
            }
        }

        /// <summary>
        /// Returns the parent path of the given path. This method will use archive-style filepaths if isInArchive is specified.
        /// </summary>
        /// <param name="path">Path to find parent for</param>
        /// <param name="isInArchive">Specifies if this is an archive filesystem</param>
        /// <returns></returns>
        public static string DirectoryGetParent(string path, bool isInArchive = false)
        {
            if (isInArchive)
            {
                path = path.TrimStart('\\', '/'); //archive paths don't start with a / \ but path combining will append one of these.
                if (path.Contains('\\'))
                {
                    return path.Substring(0, path.LastIndexOf('\\'));
                }

                return "";
            }
            else
            {
                return Directory.GetParent(path).FullName;
            }
        }

        /// <summary>
        /// Checks if a file exists. This method will look in an archive file if one is specified
        /// </summary>
        /// <param name="path">Path of file to find</param>
        /// <param name="archive">Archive to look in. Not specifying this will look on the local filesystem instead</param>
        /// <returns></returns>
        public static bool FileExists(string path, SevenZipExtractor archive = null)
        {
            if (archive != null)
            {
                path = path.TrimStart('\\', '/').Replace('/', '\\'); //archive paths don't start with a / \ but path combining will append one of these.
                var entry = archive.ArchiveFileData.FirstOrDefault(x => x.FileName.Equals(path, StringComparison.InvariantCultureIgnoreCase));
                return !string.IsNullOrEmpty(entry.FileName) && !entry.IsDirectory; //must check filename is populated as this is a struct
            }
            else
            {
                return File.Exists(path);
            }
        }



        //From https://stackoverflow.com/questions/652037/how-do-i-check-if-a-filename-matches-a-wildcard-pattern
        private static class FindFilesPatternToRegex
        {
            private static Regex HasQuestionMarkRegEx = new Regex(@"\?", RegexOptions.Compiled);
            private static Regex IllegalCharactersRegex = new Regex("[" + @"\/:<>|" + "\"]", RegexOptions.Compiled);
            private static Regex CatchExtentionRegex = new Regex(@"^\s*.+\.([^\.]+)\s*$", RegexOptions.Compiled);
            private static string NonDotCharacters = @"[^.]*";
            public static Regex Convert(string pattern)
            {
                if (pattern == null)
                {
                    throw new ArgumentNullException();
                }
                pattern = pattern.Trim();
                if (pattern.Length == 0)
                {
                    throw new ArgumentException("Pattern is empty.");
                }
                if (IllegalCharactersRegex.IsMatch(pattern))
                {
                    throw new ArgumentException("Pattern contains illegal characters.");
                }
                bool hasExtension = CatchExtentionRegex.IsMatch(pattern);
                bool matchExact = false;
                if (HasQuestionMarkRegEx.IsMatch(pattern))
                {
                    matchExact = true;
                }
                else if (hasExtension)
                {
                    matchExact = CatchExtentionRegex.Match(pattern).Groups[1].Length != 3;
                }
                string regexString = Regex.Escape(pattern);
                regexString = "^" + Regex.Replace(regexString, @"\\\*", ".*");
                regexString = Regex.Replace(regexString, @"\\\?", ".");
                if (!matchExact && hasExtension)
                {
                    regexString += NonDotCharacters;
                }
                regexString += "$";
                Regex regex = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                return regex;
            }
        }
    }
}