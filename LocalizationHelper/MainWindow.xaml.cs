﻿
#if DEBUG
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using ME3TweaksModManager.modmanager.objects;
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Localizer for ME3Tweaks Mod Manager
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// If we are operating on M3 (true) or M3Core (false)
        /// </summary>
        private bool LocalizingM3 = true;

        public ObservableCollectionExtended<string> SourceFiles { get; } = new ObservableCollectionExtended<string>();
        public string SelectedFile { get; set; }
        public MainWindow()
        {
            DataContext = this;
            ReloadData();
            InitializeComponent();
        }

        private void ReloadData()
        {
            List<string> files = new List<string>();
            if (LocalizingM3)
            {
                // ME3Tweaks Mod Manager
                var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
                var modmanagerroot = Path.Combine(solutionroot, "MassEffectModManagerCore");
                var rootLen = modmanagerroot.Length + 1;
                //localizable folders
                var ui = Path.Combine(modmanagerroot, "ui");
                var mmBase = Path.Combine(modmanagerroot, "modmanager");
                var deployment = Path.Combine(modmanagerroot, "modmanager", "deployment");
                var diagnostics = Path.Combine(modmanagerroot, "modmanager", "diagnostics");
                var exceptions = Path.Combine(modmanagerroot, "modmanager", "exceptions");
                var importer = Path.Combine(modmanagerroot, "modmanager", "importer");
                var loaders = Path.Combine(modmanagerroot, "modmanager", "loaders");
                var meim = Path.Combine(modmanagerroot, "modmanager", "meim");
                var starterkit = Path.Combine(modmanagerroot, "modmanager", "starterkit");
                var usercontrols = Path.Combine(modmanagerroot, "modmanager", "usercontrols");
                var converters = Path.Combine(modmanagerroot, "modmanager", "converters");
                var windows = Path.Combine(modmanagerroot, "modmanager", "windows");
                var me3tweaks = Path.Combine(modmanagerroot, "modmanager", "me3tweaks");
                var nexus = Path.Combine(modmanagerroot, "modmanager", "nexusmodsintegration");
                var objects = Path.Combine(modmanagerroot, "modmanager", "objects");
                var gameini = Path.Combine(modmanagerroot, "modmanager", "gameini");
                var helpers = Path.Combine(modmanagerroot, "modmanager", "helpers");
                var pmu = Path.Combine(modmanagerroot, "modmanager", "plotmanager");
                var merge = Path.Combine(modmanagerroot, "modmanager", "merge");
                var save = Path.Combine(modmanagerroot, "modmanager", "save");
                var headmorph = Path.Combine(modmanagerroot, "modmanager", "headmorph");

                // Top folder only
                files.AddRange(Directory.EnumerateFiles(mmBase, "*.cs", SearchOption.TopDirectoryOnly).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(ui, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(converters, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                // Contains some non .xaml.cs files
                files.AddRange(Directory.EnumerateFiles(usercontrols, "*.xaml", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(usercontrols, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(windows, "*.xaml*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(me3tweaks, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(nexus, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(objects, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(gameini, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(helpers, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(pmu, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));

                files.AddRange(Directory.EnumerateFiles(diagnostics, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(exceptions, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(importer, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(loaders, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(meim, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(deployment, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(starterkit, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(headmorph, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(merge, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                
                // Top folder only
                files.AddRange(Directory.EnumerateFiles(save, "*.cs", SearchOption.TopDirectoryOnly).Select(x => x.Substring(rootLen)));



                //these files are not localized
                files.Remove(Path.Combine(modmanagerroot, "modmanager", "me3tweaks", "JPatch.cs").Substring(rootLen));
                files.Remove(Path.Combine(modmanagerroot, "modmanager", "me3tweaks", "DynamicHelp.cs").Substring(rootLen));
                files.Remove(Path.Combine(modmanagerroot, "modmanager", "usercontrols", "AboutPanel.xaml").Substring(rootLen));
                // The .cs file is localized

                //Special files
                files.Add("MainWindow.xaml");
                files.Add("MainWindow.xaml.cs");
                files.Add(Path.Combine(modmanagerroot, "modmanager", "TLKTranspiler.cs").Substring(rootLen));
                files.Add(Path.Combine(modmanagerroot, "modmanager", "squadmates", "SQMOutfitMerge.cs").Substring(rootLen));
                //files.Add(Path.Combine(modmanagerroot, "gamefileformats","unreal","Texture2D.cs").Substring(rootLen));

                if (true)
                {
                    var allFiles = Directory.GetFiles(modmanagerroot, @"*.cs", SearchOption.AllDirectories)
                        .Select(x => x.Substring(rootLen)).ToList();
                    var notLocalizedFiles = allFiles.Except(files);
                    foreach (var f in notLocalizedFiles)
                    {
                        if (f.StartsWith(@"obj\")) continue;
                        if (f.StartsWith(@"modmanager\save\game2")) continue;
                        if (f.StartsWith(@"modmanager\save\game3")) continue;
                        Debug.WriteLine(f);
                    }
                }
            }
            else
            {
                // ME3Tweaks Core
                // ME3Tweaks Mod Manager
                var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
                var coreRoot = Path.Combine(solutionroot, "submodules", "ME3TweaksCore");
                var rootLen = coreRoot.Length + 1;

                // ME3TweaksCore
                var m3coreRoot = Path.Combine(coreRoot, "ME3TweaksCore");
                var m3coreWpfRoot = Path.Combine(coreRoot, "ME3TweaksCoreWPF");


                files.AddRange(Directory.EnumerateFiles(m3coreRoot, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
                files.AddRange(Directory.EnumerateFiles(m3coreWpfRoot, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));

                // Skip localizing these files
                // Submodules (LEX, etc)
                files = files.Except(Directory.EnumerateFiles(Path.Combine(m3coreRoot, "submodules"), "*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen))).ToList();
                // .NET generated stuff
                files = files.Except(Directory.EnumerateFiles(Path.Combine(m3coreRoot, "obj"), "*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen))).ToList();
                files = files.Except(Directory.EnumerateFiles(Path.Combine(m3coreWpfRoot, "obj"), "*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen))).ToList();

                //these files are not localized
                //files.Remove(Path.Combine(coreRoot, "modmanager", "me3tweaks", "JPatch.cs").Substring(rootLen));
                //files.Remove(Path.Combine(coreRoot, "modmanager", "me3tweaks", "DynamicHelp.cs").Substring(rootLen));
                //files.Remove(Path.Combine(coreRoot, "modmanager", "usercontrols", "AboutPanel.xaml").Substring(rootLen));
                // The .cs file is localized

                //Special files
                //files.Add("MainWindow.xaml");
                //files.Add("MainWindow.xaml.cs");
                //files.Add(Path.Combine(coreRoot, "modmanager", "TLKTranspiler.cs").Substring(rootLen));
                //files.Add(Path.Combine(coreRoot, "modmanager", "squadmates", "SQMOutfitMerge.cs").Substring(rootLen));
                //files.Add(Path.Combine(modmanagerroot, "gamefileformats","unreal","Texture2D.cs").Substring(rootLen));
            }

            files.Sort();
            SourceFiles.ReplaceAll(files);
        }

        public bool SelectedCS { get; set; }
        public bool SelectedXAML { get; set; }

        public void OnSelectedFileChanged()
        {
            SelectedCS = false;
            SelectedXAML = false;
            if (SelectedFile == null) return;
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;

            var pathRoot = LocalizingM3 ? Path.Combine(solutionroot, "MassEffectModManagerCore") : Path.Combine(solutionroot, "submodules", "ME3TweaksCore");

            var selectedFilePath = Path.Combine(pathRoot, SelectedFile);
            if (File.Exists(selectedFilePath))
            {
                ResultTextBox.Text = "";
                StringsTextBox.Text = "";
                Debug.WriteLine("Loading " + selectedFilePath);
                if (selectedFilePath.EndsWith(".cs"))
                {
                    SelectedCS = true;
                    PullStringsFromCS(selectedFilePath, null);
                }

                if (selectedFilePath.EndsWith(".xaml"))
                {
                    SelectedXAML = true;
                    PullStringsFromXaml(selectedFilePath, null);
                }
            }
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        private void PullStringsFromXaml(object sender, RoutedEventArgs e)
        {
            try
            {
                XDocument doc = XDocument.Parse(File.ReadAllText(sender as string));
                var menuitems = doc.Descendants().ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in menuitems)
                {
                    string title = (string)item.Attribute("Title");
                    string header = (string)item.Attribute("Header");
                    string tooltip = (string)item.Attribute("ToolTip");
                    string content = (string)item.Attribute("Content");
                    string text = (string)item.Attribute("Text");
                    string watermark = (string)item.Attribute("Watermark");
                    string directionstext = (string)item.Attribute("DirectionsText");

                    if (title != null && !title.StartsWith("{") && isNotLangWord(title) && isNotGameName(title) && isNotJobheader(title) && isNotLocalizableWord(title))
                    {
                        localizations[title] = $"string_{toCamelCase(title)}";
                        //item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                    }

                    if (header != null && !header.StartsWith("{") && isNotLangWord(header) && isNotGameName(header) && isNotJobheader(header) && isNotLocalizableWord(header))
                    {
                        localizations[header] = $"string_{toCamelCase(header)}";
                        //item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                    }

                    if (tooltip != null && !tooltip.StartsWith("{") && isNotLangWord(tooltip) && isNotGameName(tooltip) && isNotJobheader(tooltip) && isNotLocalizableWord(tooltip))
                    {
                        localizations[tooltip] = $"string_tooltip_{toCamelCase(tooltip)}";
                        //item.Attribute("ToolTip").Value = $"{{DynamicResource {localizations[tooltip]}}}";
                    }

                    if (content != null && !content.StartsWith("{") && content.Length > 1 && !content.StartsWith("/images/") && isNotLangWord(content) && isNotLocalizableWord(content) && isNotGameName(content) && isNotJobheader(content))
                    {
                        localizations[content] = $"string_{toCamelCase(content)}";
                        //item.Attribute("Content").Value = $"{{DynamicResource {localizations[content]}}}";
                    }

                    if (watermark != null && !watermark.StartsWith("{") && watermark.Length > 1 && !long.TryParse(watermark, out var _) && isNotLangWord(watermark) && isNotLocalizableWord(watermark)
                        && isNotJobheader(watermark) && isNotGameName(watermark)
                        && !watermark.StartsWith("http"))
                    {
                        localizations[watermark] = $"string_{toCamelCase(watermark)}";
                        //item.Attribute("Watermark").Value = $"{{DynamicResource {localizations[watermark]}}}";
                    }

                    if (directionstext != null && !directionstext.StartsWith("{") && directionstext.Length > 1 && !long.TryParse(directionstext, out var _) && isNotLangWord(directionstext) && isNotLocalizableWord(directionstext)
                        && isNotJobheader(directionstext) && isNotGameName(directionstext)
                        && !directionstext.StartsWith("http"))
                    {
                        localizations[directionstext] = $"string_{toCamelCase(directionstext)}";
                        //item.Attribute("directionstext").Value = $"{{DynamicResource {localizations[directionstext]}}}";
                    }


                    if (text != null && !text.StartsWith("{")
                                     && text.Length > 1
                                     && isNotLangWord(text)
                                     && isNotGameName(text)
                                     && isNotJobheader(text)
                                     && isNotLocalizableWord(text)
                                     && text != "BioGame"
                                     && text != "BioParty"
                                     && text != "BioEngine" && text != "DLC_MOD_")
                    {
                        localizations[text] = $"string_{toCamelCase(text)}";
                        //item.Attribute("Text").Value = $"{{DynamicResource {localizations[text]}}}";
                    }
                }

                //ResultTextBox.Text = doc.ToString();
                StringBuilder sb = new StringBuilder();
                foreach (var v in localizations)
                {
                    var newlines = v.Key.Contains("\n");
                    var text = v.Key.Replace("\r\n", "&#10;").Replace("\n", "&#10;");
                    sb.AppendLine("\t<system:String" + (newlines ? " xml:space=\"preserve\" " : " ") + "x:Key=\"" + v.Value.Substring(0, "string_".Length) + v.Value.Substring("string_".Length, 1).ToLower() + v.Value.Substring("string_".Length + 1) + "\">" + text + "</system:String>");
                }

                StringsTextBox.Text = sb.ToString();
                if (string.IsNullOrEmpty(sb.ToString()))
                {
                    StringsTextBox.Text = "No strings needing localized in " + SelectedFile;
                }
            }
            catch (Exception)
            {

            }
        }

        private void Synchronize_Clicked(object sender, RoutedEventArgs e)
        {
            //get out of project in debug mode
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
            var localizationsFolder = LocalizingM3 ? Path.Combine(solutionroot, "MassEffectModManagerCore", "modmanager", "localizations") : Path.Combine(solutionroot, "submodules", "ME3TweaksCore", "ME3TweaksCore", "Localization");
            var keyStringsCSFile = LocalizingM3 ? Path.Combine(localizationsFolder, "M3L.cs") : Path.Combine(localizationsFolder, "LocalizationStringKeys.cs");
            var templateFile = LocalizingM3 ? Path.Combine(localizationsFolder, "M3L_Template.txt") : Path.Combine(localizationsFolder, "LC_Template.txt");
            ;
            var intfile = LocalizingM3 ? Path.Combine(localizationsFolder, "int.xaml") : Path.Combine(localizationsFolder, "Dictionaries", "int.xaml");

            var m3llines = File.ReadAllLines(templateFile).ToList();

            var doc = XDocument.Load(intfile);
            XNamespace xnamespace = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var keys = doc.Descendants(xnamespace + "String");
            Debug.WriteLine(keys.Count());
            foreach (var key in keys)
            {
                var keyStr = key.Attribute(x + "Key").Value;
                m3llines.Add($"\t\tpublic const string {keyStr} = \"{keyStr}\";");
                //Debug.WriteLine(keyStr);
            }

            //Write end of .cs file lines
            m3llines.Add("\t}");
            m3llines.Add("}");

            File.WriteAllLines(keyStringsCSFile, m3llines); //write back updated file

            //return;
            //Update all of the other xaml files
            return; //skip other langauges as it's now handled by localizer tool

        }

        private (bool preserveWhitespace, string key) extractInfo(string line)
        {
            var closingTagIndex = line.IndexOf(">");
            var strInfo = line.Substring(0, closingTagIndex).Trim();
            bool preserveWhitespace = strInfo.Contains("xml:space=\"preserve\"");
            int keyPos = strInfo.IndexOf("x:Key=\"");
            string keyVal = strInfo.Substring(keyPos + "x:Key=\"".Length);
            keyVal = keyVal.Substring(0, keyVal.IndexOf("\""));
            return (preserveWhitespace, keyVal);
        }

        /// <summary>
        /// Pulls localizable strings from a .cs file and puts them into the interface
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PullStringsFromCS(object sender, RoutedEventArgs e)
        {
            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            var filelines = File.ReadAllLines(sender as string);
            HashSet<string> s = new HashSet<string>();
            HashSet<string> origStrForSubsOnly = new HashSet<string>();
            bool sectionIsLocalizable = true;
            for (int x = 0; x < filelines.Length; x++)
            {
                if (x == 132)
                    Debug.WriteLine("ok");
                var line = filelines[x];
                if (line.Contains("do not localize", StringComparison.InvariantCultureIgnoreCase)) continue; //ignore this line.
                if (line.Contains("Localizable(true)", StringComparison.InvariantCultureIgnoreCase))
                {
                    sectionIsLocalizable = true;
                    continue; //ignore this line.
                }

                if (line.Contains("Localizable(false)", StringComparison.InvariantCultureIgnoreCase))
                {
                    sectionIsLocalizable = false;
                    continue; //ignore this line.
                }

                if (!sectionIsLocalizable && !line.Contains("//force localize"))
                {
                    continue;
                }

                if (line.Contains("[DebuggerDisplay(")) continue; //skip these lines
                var commentIndex = line.IndexOf("//");
                var protocolIndex = line.IndexOf(@"://");
                if (line.IndexOf(@" M3Log.") > 0 || line.IndexOf(@" MLog.") > 0)
                {
                    Debug.WriteLine($@"Skipping log line at {x}");
                    continue;
                }
                var matches = r.Matches(line);
                foreach (var match in matches)
                {
                    bool xmlPreserve = false;
                    var matchIndex = line.IndexOf(match.ToString());
                    if (commentIndex >= 0 && matchIndex > commentIndex)
                    {
                        // Check it's not http:// in same line
                        if ((protocolIndex >= 0 && protocolIndex != commentIndex - 1) || (commentIndex >= 0 && protocolIndex == -1))
                        {
                            continue; //this is a comment
                        }


                        // Otherwise, this is something like http:// as the :// index is // index - 1
                    }

                    var str = match.ToString();
                    if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                    var strname = "string_";
                    if (str.StartsWith("$")) strname = "string_interp_";
                    var newStr = match.ToString().TrimStart('$').Trim('"');
                    if (newStr.Length > 1)
                    {
                        if (newStr.Contains("\\n")) xmlPreserve = true;

                        strname += toCamelCase(newStr);

                        //Substitutions
                        int pos = 0;
                        int openbracepos = -1;
                        List<string> substitutions = new List<string>();
                        while (pos < newStr.Length)
                        {
                            if (openbracepos == -1)
                            {
                                if (newStr[pos] == '{')
                                {
                                    openbracepos = pos;
                                    continue;
                                }
                            }
                            else if (newStr[pos] == '}')
                            {
                                //closing!
                                substitutions.Add(newStr.Substring(openbracepos, pos - openbracepos + 1));
                                openbracepos = -1;
                            }

                            //Debug.Write(newStr[pos]);
                            //Debug.Flush();
                            pos++;
                        }

                        int num = 0;
                        string comment = "";
                        string subbedStr = newStr;
                        foreach (var substitution in substitutions)
                        {
                            subbedStr = subbedStr.Replace(substitution, "{" + num.ToString() + "}"); //replacing a {str} with {#}
                            comment += " " + num + "=" + substitution;
                            num++;
                        }

                        string commentStr = "";
                        if (comment.Length > 0) commentStr = "<!--" + comment + " -->";
                        Debug.WriteLine((x + 1) + "\t\t" + subbedStr);
                        s.Add($"    <system:String{(xmlPreserve ? " xml:space=\"preserve\"" : "")} x:Key=\"{strname}\">{subbedStr}</system:String> " + commentStr);
                        if (substitutions.Count > 0)
                        {
                            origStrForSubsOnly.Add($"    <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                        }
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (var str in s)
            {
                sb.AppendLine(str);
            }

            if (origStrForSubsOnly.Count > 0)
            {
                sb.AppendLine("<!-- The follow items are only for letting this localizer replace the correct strings! Remove them when done and make sure keys are identical to the stripped versions-->");
            }

            foreach (var str in origStrForSubsOnly)
            {
                //interps
                sb.AppendLine(str);
            }

            StringsTextBox.Text = sb.ToString();
            if (string.IsNullOrEmpty(sb.ToString()))
            {
                StringsTextBox.Text = "No strings needing localized in " + SelectedFile;
            }
            //Debug.WriteLine("<!-- Subs only -->");


        }

        private string toCamelCase(string str)
        {
            var words = str.Split();
            var res = "";
            bool first = true;
            foreach (var word in words)
            {
                var cleanedWord = word.Replace(".", "");
                cleanedWord = cleanedWord.Replace("?", "Question");
                cleanedWord = cleanedWord.Replace("(", "");
                cleanedWord = cleanedWord.Replace(")", "");
                cleanedWord = cleanedWord.Replace(":", "");
                cleanedWord = cleanedWord.Replace("/", "");
                cleanedWord = cleanedWord.Replace("\\", "");
                cleanedWord = cleanedWord.Replace("{", "");
                cleanedWord = cleanedWord.Replace("}", "");
                cleanedWord = cleanedWord.Replace("-", "");
                cleanedWord = cleanedWord.Replace("'", "");
                cleanedWord = cleanedWord.Replace(",", "");
                if (first)
                {
                    res += caseFirst(cleanedWord, false);
                    first = false;
                }
                else
                {
                    res += caseFirst(cleanedWord, true);
                }
            }

            return res;
        }

        static string caseFirst(string s, bool upper)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            // Return char and concat substring.
            return (upper ? char.ToUpper(s[0]) : char.ToLower(s[0])) + s.Substring(1);
        }

        private void PushCSStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var text = StringsTextBox.Text;
            text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + text + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(text);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String").ToList();
            foreach (var str in lstrings)
            {
                Debug.WriteLine(str.Value);
            }

            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
            var rootFolder = LocalizingM3 ? Path.Combine(solutionroot, "MassEffectModManagerCore") : Path.Combine(solutionroot, "submodules", "ME3TweaksCore");


            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            StringBuilder sb = new StringBuilder();

            var lines = File.ReadAllLines(Path.Combine(rootFolder, SelectedFile));
            foreach (var line in lines)
            {
                var newline = line;
                var matches = r.Matches(line);
                var strings = new List<string>();
                foreach (var match in matches)
                {
                    var str = match.ToString();
                    if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                    var strippedStr = str.Trim('$', '"');

                    var localizedMatch = lstrings.FirstOrDefault(x => x.Value == strippedStr);
                    if (localizedMatch != null)
                    {

                        var m3lcodestr = LocalizingM3 ? "M3L.GetString(M3L." + localizedMatch.Attribute(x + "Key").Value : "LC.GetString(LC." + localizedMatch.Attribute(x + "Key").Value;

                        int pos = 0;
                        int openbracepos = -1;
                        List<string> substitutions = new List<string>();
                        while (pos < str.Length)
                        {
                            if (openbracepos == -1)
                            {
                                if (str[pos] == '{')
                                {
                                    openbracepos = pos;
                                    continue;
                                }
                            }
                            else if (str[pos] == '}')
                            {
                                //closing!
                                substitutions.Add(str.Substring(openbracepos + 1, pos - (openbracepos + 1)));
                                openbracepos = -1;
                            }

                            pos++;
                        }

                        foreach (var subst in substitutions)
                        {
                            m3lcodestr += ", " + subst;
                        }

                        m3lcodestr += ")";
                        newline = newline.Replace(str, m3lcodestr);
                    }
                }

                sb.AppendLine(newline);
            }

            ResultTextBox.Text = sb.ToString();
        }

        private void PushXamlStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var sourceStringsXaml = StringsTextBox.Text;
            sourceStringsXaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + sourceStringsXaml + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(sourceStringsXaml);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String").ToList();
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var file = Path.Combine(M3folder, SelectedFile);
            string[] attributes = { "Title", "Header", "ToolTip", "Content", "Text", "Watermark", "DirectionsText" };
            try
            {
                XDocument doc = XDocument.Load(file);
                var xamlItems = doc.Descendants().ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in xamlItems)
                {
                    foreach (var attribute in attributes)
                    {
                        string attributeText = (string)item.Attribute(attribute);

                        if (!string.IsNullOrWhiteSpace(attributeText) && !attributeText.StartsWith("{"))
                        {
                            var matchingStr = lstrings.FirstOrDefault(x => x.Value == attributeText);
                            if (matchingStr != null)
                            {
                                item.Attribute(attribute).Value = "{DynamicResource " + matchingStr.Attribute(x + "Key").Value + "}";
                            }
                        }
                    }
                }

                var xml = doc.ToString();
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.PreserveWhitespace = true;
                xmldoc.XmlResolver = null;
                xmldoc.LoadXml(doc.ToString());
                foreach (XmlNode node in xmldoc)
                {
                    if (node.NodeType == XmlNodeType.XmlDeclaration)
                    {
                        xmldoc.RemoveChild(node);
                    }
                }

                var xmlS = Beautify(xmldoc);

                if (xmlS.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>"))
                    xmlS = xmlS.Substring("<?xml version=\"1.0\" encoding=\"utf-8\"?>".Length);
                xmlS = xmlS.Trim();
                ResultTextBox.Text = xmlS;
            }
            catch (Exception)
            {

            }

        }

        public static string Beautify(System.Xml.XmlDocument doc)
        {
            string strRetValue = null;
            System.Text.Encoding enc = System.Text.Encoding.UTF8;
            // enc = new System.Text.UTF8Encoding(false);

            System.Xml.XmlWriterSettings xmlWriterSettings = new System.Xml.XmlWriterSettings();
            xmlWriterSettings.Encoding = enc;
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = "    ";
            xmlWriterSettings.NewLineChars = "\r\n";
            xmlWriterSettings.NewLineOnAttributes = true;
            xmlWriterSettings.NewLineHandling = System.Xml.NewLineHandling.Replace;
            //xmlWriterSettings.OmitXmlDeclaration = true;
            xmlWriterSettings.ConformanceLevel = System.Xml.ConformanceLevel.Document;


            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(ms, xmlWriterSettings))
                {
                    doc.Save(writer);
                    writer.Flush();
                    ms.Flush();

                    writer.Close();
                } // End Using writer

                ms.Position = 0;
                using (System.IO.StreamReader sr = new System.IO.StreamReader(ms, enc))
                {
                    // Extract the text from the StreamReader.
                    strRetValue = sr.ReadToEnd();

                    sr.Close();
                } // End Using sr

                ms.Close();
            } // End Using ms


            /*
            System.Text.StringBuilder sb = new System.Text.StringBuilder(); // Always yields UTF-16, no matter the set encoding
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
                writer.Close();
            } // End Using writer
            strRetValue = sb.ToString();
            sb.Length = 0;
            sb = null;
            */

            xmlWriterSettings = null;
            return strRetValue;
        } // End Function Beautify

        private void Check_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var coreRoot = Path.Combine(solutionroot, "submodules", "ME3TweaksCore");
            var rootLen = coreRoot.Length + 1;

            // ME3TweaksCore
            var m3coreRoot = Path.Combine(coreRoot, "ME3TweaksCore");
            var m3coreWpfRoot = Path.Combine(coreRoot, "ME3TweaksCoreWPF");

            string[] m3Dirs =
            {
                Path.Combine(M3folder, "modmanager", "usercontrols"),
                Path.Combine(M3folder, "modmanager", "objects"),
                Path.Combine(M3folder, "modmanager", "windows"),
                Path.Combine(M3folder, "modmanager", "helpers"),
            };

            string[] m3cDirs =
            {
                m3coreRoot,
                m3coreWpfRoot,
            };

            string[] dirs = LocalizingM3 ? m3Dirs : m3cDirs;

            int i = 0;
            foreach (var dir in dirs)
            {
                var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).ToList();
                if (i == 0 && LocalizingM3)
                {
                    csFiles.Add(Path.Combine(M3folder, "MainWindow.xaml.cs"));
                }



                i++;
                foreach (var csFile in csFiles)
                {
                    if (!LocalizingM3)
                    {
                        if (csFile.Contains("ME3TweaksCore\\submodules", StringComparison.InvariantCultureIgnoreCase))
                            continue; // this is not localized.

                        var fname = Path.GetFileName(csFile);
                        if (fname.Contains("AssemblyInfo") || fname.Contains("AssemblyAttributes") || fname.Contains("LocalizationStringKeys") || fname.Contains("LocalizationCore"))
                            continue;
                    }
                    Debug.WriteLine($" --------- FILE: {Path.GetFileName(csFile)} --------");
                    var regex = "([$@]*(\".+?\"))";
                    Regex r = new Regex(regex);
                    var filelines = File.ReadAllLines(csFile);
                    HashSet<string> s = new HashSet<string>();
                    int lineIndex = -1;
                    foreach (var line in filelines)
                    {
                        lineIndex++;
                        var commentIndex = line.IndexOf("//");
                        var matches = r.Matches(line);
                        if (line.Contains("do not localize", StringComparison.InvariantCultureIgnoreCase)) continue; //ignore this line.
                        foreach (var match in matches)
                        {
                            var matchIndex = line.IndexOf(match.ToString());
                            if (commentIndex >= 0 && matchIndex > commentIndex) continue; //this is a comment
                            var str = match.ToString();
                            if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                            var strname = "string_";
                            if (str.StartsWith("$")) strname = "string_interp_";
                            var newStr = match.ToString().TrimStart('$').Trim('"');
                            if (newStr.Length > 1)
                            {
                                strname += toCamelCase(newStr);

                                //LN is line number
                                s.Add($"  LN:{lineIndex}  <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                                //s.Add($"    <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                            }
                        }
                    }

                    foreach (var str in s)
                    {
                        Debug.WriteLine(str);
                    }
                }
            }
        }

        private void CheckXamls_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            string[] dirs =
            {
                Path.Combine(M3folder, "modmanager", "usercontrols"),
                Path.Combine(M3folder, "modmanager", "windows")
            };

            int i = 0;
            foreach (var dir in dirs)
            {
                var xamlFiles = Directory.GetFiles(dir, "*.xaml", SearchOption.AllDirectories).ToList();
                if (i == 0)
                {
                    xamlFiles.Add(Path.Combine(M3folder, "MainWindow.xaml"));
                }

                i++;
                foreach (var xamlFile in xamlFiles)
                {
                    Debug.WriteLine($" --------- FILE: {Path.GetFileName(xamlFile)} --------");
                    if (Path.GetFileName(xamlFile) == "AboutPanel.xaml") continue; //skip this file as it has a lot of non-localizable strings
                    try
                    {
                        XDocument doc = XDocument.Parse(File.ReadAllText(xamlFile));
                        var xamlItems = doc.Descendants().ToList();
                        Dictionary<string, string> localizations = new Dictionary<string, string>();

                        foreach (var item in xamlItems)
                        {
                            string header = (string)item.Attribute("Header");
                            string tooltip = (string)item.Attribute("ToolTip");
                            string content = (string)item.Attribute("Content");
                            string text = (string)item.Attribute("Text");
                            string watermark = (string)item.Attribute("Watermark");

                            if (header != null && !header.StartsWith("{")
                                               && header != "+"
                                               && isNotLangWord(header)
                                               && isNotGameName(header)
                                               && header != "Reload selected mod" //debug only
                            )
                            {
                                localizations[header] = $"string_{toCamelCase(header)}";
                                item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                            }

                            if (tooltip != null && !tooltip.StartsWith("{"))
                            {
                                localizations[tooltip] = $"string_tooltip_{toCamelCase(tooltip)}";
                                item.Attribute("ToolTip").Value = $"{{DynamicResource {localizations[tooltip]}}}";
                            }

                            if (content != null && !content.StartsWith("{")
                                                && !content.StartsWith("/images")
                                                && content.Length > 1
                                                && isNotLangWord(content)
                                                && isNotGameName(content)
                            )
                            {
                                localizations[content] = $"string_{toCamelCase(content)}";
                                item.Attribute("Content").Value = $"{{DynamicResource {localizations[content]}}}";
                            }

                            if (watermark != null && !watermark.StartsWith("{")
                                                  && watermark.Length > 1
                            )
                            {
                                localizations[watermark] = $"string_{toCamelCase(watermark)}";
                                item.Attribute("Watermark").Value = $"{{DynamicResource {localizations[watermark]}}}";
                            }

                            if (text != null && !text.StartsWith("{")
                                             && text.Length > 1
                                             && isNotLangWord(text)
                                             && isNotGameName(text)
                                             && text != "DLC_MOD_"
                                             && text != "BioGame"
                                             && text != "BioParty"
                                             && text != "BioEngine")
                            {
                                localizations[text] = $"string_{toCamelCase(text)}";
                                item.Attribute("Text").Value = $"{{DynamicResource {localizations[text]}}}";
                            }
                        }

                        //ResultTextBox.Text = doc.ToString();
                        StringBuilder sb = new StringBuilder();
                        foreach (var v in localizations)
                        {
                            Debug.WriteLine("\t<system:String x:Key=\"" + v.Value.Substring(0, "string_".Length) + v.Value.Substring("string_".Length, 1).ToLower() + v.Value.Substring("string_".Length + 1) + "\">" + v.Key + "</system:String>");
                        }
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("EXCEPTION!");
                    }
                }
            }
        }

        private bool isNotGameName(string str)
        {
            if (str.Equals("Mass Effect", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Mass Effect 2", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Mass Effect 3", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Mass Effect LE", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("ME1", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("ME2", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("ME3", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("LE1", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("LE2", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("LE3", StringComparison.InvariantCultureIgnoreCase)) return false;
            return true;
        }

        private bool isNotLocalizableWord(string str)
        {
            if (str.Equals("ME3Tweaks Mod Manager", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Mass Effect Ini Modder", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Multilist", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("moddir", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("=>", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("->", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Faster Legs", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("GatorZ", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("OneGreatMod", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Faster Legs DLC Module", StringComparison.InvariantCultureIgnoreCase)) return false;
            return true;
        }

        private bool isNotJobheader(string str)
        {
            str = str.TrimStart('[');
            str = str.TrimEnd(']');
            if (Enum.TryParse<ModJob.JobHeader>(str, out var parsed))
            {
                // it's a job header
                return false;
            }

            return true;
        }

        private bool isNotLangWord(string str)
        {
            if (str.Equals("Deutsch", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("English", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Español", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Français", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Polski", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Pусский", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Português", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("한국어", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (str.Equals("Italiano", StringComparison.InvariantCultureIgnoreCase)) return false;
            return true;
        }

        private void CheckXmlSpacePreserve_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var localizationsFolder = Path.Combine(solutionroot, "MassEffectModManagerCore", "modmanager", "localizations");
            var m3lFile = Path.Combine(localizationsFolder, "M3L.cs");
            var m3lTemplateFile = Path.Combine(localizationsFolder, "M3L_Template.txt");
            var intfile = Path.Combine(localizationsFolder, "int.xaml");

            var m3llines = File.ReadAllLines(m3lTemplateFile).ToList();

            var doc = XDocument.Load(intfile);
            XNamespace xnamespace = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace xml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var keys = doc.Descendants(xnamespace + "String");
            foreach (var key in keys)
            {
                if (key.Value.Contains(@"\n"))
                {
                    //check for preserve space
                    var preserveSpace = key.Attribute(XNamespace.Xml + "space");
                    if (preserveSpace == null || preserveSpace.Value != "preserve")
                    {
                        Debug.WriteLine(key.Value);
                    }
                }
            }
        }

        private void OpenLoalizationUI_Clicked(object sender, RoutedEventArgs e)
        {
            new LocalizationTablesUI().Show();
        }

        private void PerformINTDiff_Clicked(object sender, RoutedEventArgs e)
        {
            string oldfile = null, newfile = null;
            OpenFileDialog oldFileDialog = new OpenFileDialog()
            {
                Title = "Select OLD localization file",
                Filter = "Xaml files|*.xaml"
            };
            if (oldFileDialog.ShowDialog() == true)
            {
                oldfile = oldFileDialog.FileName;
            }

            if (oldfile == null) return;
            OpenFileDialog newFileDialog = new OpenFileDialog()
            {
                Title = "Select NEW localization file",
                Filter = "Xaml files|*.xaml"
            };
            if (newFileDialog.ShowDialog() == true)
            {
                newfile = newFileDialog.FileName;
            }

            if (newfile == null) return;
            var result = LocalizationFileDiff.generateDiff(oldfile, newfile);

            Debug.WriteLine(result);

        }

        private void SwitchProjects_Clicked(object sender, RoutedEventArgs e)
        {
            LocalizingM3 = !LocalizingM3;
            ReloadData();
        }
    }
}
#endif