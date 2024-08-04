﻿using System.Text.RegularExpressions;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.launcher;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using ME3TweaksModManager.extensions;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for LaunchParametersDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class LaunchParametersDialog : Window
    {
        //public GameTargetWPF SelectedGameTarget { get; }

        /// <summary>
        /// The package that was used to initialize the window
        /// </summary>
        public LaunchOptionsPackage LaunchPackage { get; set; }

        /// <summary>
        /// List of languages that are supported by this game
        /// </summary>
        public ObservableCollectionExtended<LauncherLanguageOption> LanguageOptions { get; } = new();
        /// <summary>
        /// List of custom command line parameters
        /// </summary>
        public ObservableCollectionExtended<LauncherCustomParameter> CustomOptions { get; } = new();

        #region Commands
        public GenericCommand DeletePackageCommand { get; private set; }
        public GenericCommand SavePackageCommand { get; private set; }

        /// <summary>
        /// The parameter set name shown in the UI
        /// </summary>
        public string ParameterSetName { get; set; }

        /// <summary>
        /// The current language string
        /// </summary>
        public string ChosenLanguage { get; set; } = @"INT";

        /// <summary>
        /// The current subtitle size
        /// </summary>
        public int SubtitleSize { get; set; } = 20;

        /// <summary>
        /// The list of custom arguments to supply, if any
        /// </summary>
        public string CustomArguments { get; set; }

        /// <summary>
        /// Game this dialog is for editing
        /// </summary>
        public MEGame Game { get; set; }

        /// <summary>
        /// Internal GUID for identifying the package
        /// </summary>
        private Guid PackageGuid { get; set; }
        #endregion

        public LaunchParametersDialog(Window window, MEGame game, LaunchOptionsPackage package)
        {
            Owner = window;
            Game = game;

            LoadPackage(package);
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();
        }

        private void LoadPackage(LaunchOptionsPackage package)
        {
            LaunchPackage = package ?? new LaunchOptionsPackage() { Game = Game, ChosenLanguage = @"INT", PackageTitle = LaunchOptionsPackage.GetCustomTitle() };
            PackageGuid = LaunchPackage.PackageGuid == Guid.Empty ? Guid.NewGuid() : LaunchPackage.PackageGuid; // Keep existing guid if found
            ParameterSetName = LaunchPackage.PackageTitle;
            LoadLanguagesAndOptions();

            var chosenLang = LanguageOptions.FirstOrDefault(x => x.LanguageString == LaunchPackage.ChosenLanguage);
            if (chosenLang != null) chosenLang.UIIsSelected = true;
        }

        private void LoadLanguagesAndOptions()
        {
            CustomOptions.Clear();
            LanguageOptions.Clear();
            // Global options
            CustomOptions.Add(new LauncherCustomParameter() { DisplayString = M3L.GetString(M3L.string_automaticallyResumeLastSave), ToolTip = M3L.GetString(M3L.string_tooltip_autoResume), CommandLineText = @"-RESUME", SaveKey = LauncherCustomParameter.KEY_AUTORESUME, IsSelected = LaunchPackage.AutoResumeSave });
            CustomOptions.Add(new LauncherCustomParameter() { DisplayString = M3L.GetString(M3L.string_noForceFeedback), ToolTip = M3L.GetString(M3L.string_tooltip_disablesControllerVibration), CommandLineText = @"-NOFORCEFEEDBACK", SaveKey = LauncherCustomParameter.KEY_NOFORCEFEEDBACK, IsSelected = LaunchPackage.NoForceFeedback });
            CustomOptions.Add(new LauncherCustomParameter() { DisplayString = M3L.GetString(M3L.string_enableProcessMinidumps), ToolTip = M3L.GetString(M3L.string_tooltip_enableMinidumps), CommandLineText = @"-enableminidumps", SaveKey = LauncherCustomParameter.KEY_MINIDUMPS, IsSelected = LaunchPackage.EnableMinidumps });


            // Perhaps localize these?
            switch (LaunchPackage.Game)
            {
                case MEGame.LE1:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_english), LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_french), LanguageString = @"FR" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_frenchTextEnglishVoiceover), LanguageString = @"FE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_german), LanguageString = @"DE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_germanTextEnglishVoiceover), LanguageString = @"GE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italian), LanguageString = @"IT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italianTextEnglishVoiceover), LanguageString = @"IE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_spanishTextEnglishVoiceover), LanguageString = @"ES" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_japaneseTextEnglishVoiceover), LanguageString = @"JA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_polish), LanguageString = @"PLPC" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_polishTextEnglishVoiceover), LanguageString = @"PL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_russian), LanguageString = @"RA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_russianTextEnglishVoiceover), LanguageString = @"RU" });

                    // Unofficial ones in game files
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_czechTextPartialEnglishVoiceover), LanguageString = @"CS" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_hungarianTextPartialEnglishVoiceover), LanguageString = @"HU" });

                    break;
                case MEGame.LE2:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_english), LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_french), LanguageString = @"FRA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_frenchTextEnglishVoiceover), LanguageString = @"FRE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_german), LanguageString = @"DEU" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_germanTextEnglishVoiceover), LanguageString = @"DEE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italian), LanguageString = @"ITA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italianTextEnglishVoiceover), LanguageString = @"ITE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_spanishTextEnglishVoiceover), LanguageString = @"ESN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_japaneseTextEnglishVoiceover), LanguageString = @"JPN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_polish), LanguageString = @"POL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_polishTextEnglishVoiceover), LanguageString = @"POE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_russianTextEnglishVoiceover), LanguageString = @"RUS" });

                    // LE2 has no unofficial localizations.
                    break;
                case MEGame.LE3:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_english), LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_french), LanguageString = @"FRA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_frenchTextEnglishVoiceover), LanguageString = @"FRE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_german), LanguageString = @"DEU" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_germanTextEnglishVoiceover), LanguageString = @"DEE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italian), LanguageString = @"ITA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_italianTextEnglishVoiceover), LanguageString = @"ITE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_spanishTextEnglishVoiceover), LanguageString = @"ESN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_japaneseTextEnglishVoiceover), LanguageString = @"JPN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_polishTextEnglishVoiceover), LanguageString = @"POL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = M3L.GetString(M3L.string_russianTextEnglishVoiceover), LanguageString = @"RUS" });
                    break;

                    // LE3 has no unofficial localizations.
            }

            CustomArguments = LaunchPackage.CustomExtraArgs;
        }

        private void LoadCommands()
        {
            SavePackageCommand = new GenericCommand(SavePackage, CanSavePackage);
            DeletePackageCommand = new GenericCommand(DeletePackage, CanDeletePackage);
        }

        private void DeletePackage()
        {
            var itemsToDelete = M3LoadedMods.Instance.AllLaunchOptions.Where(x => x.PackageGuid == PackageGuid && !x.IsCustomOption).ToList(); // Must make copy
            foreach (var item in itemsToDelete)
            {
                M3Log.Information($@"Deleting LaunchOptionPackage {item}");
                File.Delete(item.FilePath);
                M3LoadedMods.Instance.AllLaunchOptions.Remove(item);
            }

            LaunchPackage = null;
            Close();
        }

        private bool CanDeletePackage()
        {
            return LaunchPackage != null && !LaunchPackage.IsCustomOption;
        }

        private bool CanSavePackage()
        {
            if (string.IsNullOrWhiteSpace(ParameterSetName)) return false;
            if (ParameterSetName.Length > 30) return false;
            return Regex.IsMatch(ParameterSetName,
                    @"\A(?!(?:COM[0-9]|CON|LPT[0-9]|NUL|PRN|AUX|com[0-9]|con|lpt[0-9]|nul|prn|aux)|\s|[\.]{2,})[^\\\/:*""?<>|]{1,254}(?<![\s\.])\z"); // do not localize
        }

        private void SavePackage()
        {
            var package = new LaunchOptionsPackage();
            package.SetLatestVersion();
            package.Game = Game;
            package.PackageTitle = ParameterSetName;

            // Language + Subtitle
            package.ChosenLanguage = ChosenLanguage;
            package.SubtitleSize = SubtitleSize;
            package.PackageGuid = PackageGuid;
            // Custom arguments
            foreach (var custOption in CustomOptions)
            {
                // Map the keys into the package for save
                package.SetOption(custOption.SaveKey, custOption.IsSelected);
            }

            package.CustomExtraArgs = CustomArguments;

            var outPath = Path.Combine(M3LoadedMods.GetLaunchOptionsDirectory(), $@"{Game}-{ParameterSetName}") + LaunchOptionsPackage.FILE_EXTENSION;
            var m3lText = JsonConvert.SerializeObject(package, Formatting.Indented);

            // Remove existing package with current guid, if any
            var itemsToDelete = M3LoadedMods.Instance.AllLaunchOptions.Where(x => x.PackageGuid == PackageGuid && x.FilePath != outPath);
            foreach (var item in itemsToDelete)
            {
                File.Delete(item.FilePath);
            }

            File.WriteAllText(outPath, m3lText);
            package.FilePath = outPath;
            LaunchPackage = package; // Set this so the calling window can access and assign it
            Close();
        }

        private void SubtitleSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == SubSizeUp)
            {
                SubtitleSize++;
            }
            if (sender == SubSizeDown && SubtitleSize > 1)
            {
                SubtitleSize--;
            }
        }

        private void LanguageButtonSelected(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.DataContext is LauncherLanguageOption llo)
            {
                ChosenLanguage = llo.LanguageString;
            }
        }
    }
}
