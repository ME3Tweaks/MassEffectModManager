﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using System.Xml.Linq;
using Dark.Net;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.meim;
using Microsoft.AppCenter.Analytics;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ME1IniModder.xaml
    /// </summary>
    public partial class ME1IniModder : Window, INotifyPropertyChanged, IClosableWindow
    {
        private bool doNotOpen;

        public ObservableCollectionExtended<IniPropertyMaster> BioEngineEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();
        public ObservableCollectionExtended<IniPropertyMaster> BioGameEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();
        public ObservableCollectionExtended<IniPropertyMaster> BioPartyEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();

        public ME1IniModder()
        {
            TelemetryInterposer.TrackEvent(@"Launched MEIM");
            DataContext = this;
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            string configFileFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config";
            if (Directory.Exists(configFileFolder))
            {
                Dictionary<string, ObservableCollectionExtended<IniPropertyMaster>> loadingMap = new Dictionary<string, ObservableCollectionExtended<IniPropertyMaster>>();
                loadingMap[@"BioEngine.xml"] = BioEngineEntries;
                loadingMap[@"BioGame.xml"] = BioGameEntries;
                loadingMap[@"BioParty.xml"] = BioPartyEntries;


                foreach (var kp in loadingMap)
                {
                    XElement rootElement = XElement.Parse(GetPropertyMap(kp.Key));

                    var linqlist = (from e in rootElement.Elements(@"Section")
                                    select new IniSection
                                    {
                                        SectionName = (string)e.Attribute(@"name"),
                                        BoolProperties = e.Elements(@"boolproperty").Select(f => new IniPropertyBool
                                        {
                                            CanAutoReset = f.Attribute(@"canautoreset") != null ? (bool)f.Attribute(@"canautoreset") : true,
                                            PropertyName = (string)f.Attribute(@"propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute(@"friendlyname"),
                                            Notes = (string)f.Attribute(@"notes"),
                                            OriginalValue = f.Value

                                        }).ToList(),
                                        IntProperties = e.Elements(@"intproperty").Select(f => new IniPropertyInt
                                        {
                                            CanAutoReset = f.Attribute(@"canautoreset") != null ? (bool)f.Attribute(@"canautoreset") : true,
                                            PropertyName = (string)f.Attribute(@"propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute(@"friendlyname"),
                                            Notes = (string)f.Attribute(@"notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        FloatProperties = e.Elements(@"floatproperty").Select(f => new IniPropertyFloat
                                        {
                                            CanAutoReset = f.Attribute(@"canautoreset") != null ? (bool)f.Attribute(@"canautoreset") : true,
                                            PropertyName = (string)f.Attribute(@"propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute(@"friendlyname"),
                                            Notes = (string)f.Attribute(@"notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        EnumProperties = e.Elements(@"enumproperty").Select(f => new IniPropertyEnum
                                        {
                                            CanAutoReset = f.Attribute(@"canautoreset") != null ? (bool)f.Attribute(@"canautoreset") : true,
                                            PropertyName = (string)f.Attribute(@"propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute(@"friendlyname"),
                                            Notes = (string)f.Attribute(@"notes"),
                                            Choices = f.Elements(@"enumvalue").Select(g => new IniPropertyEnumValue
                                            {
                                                FriendlyName = (string)g.Attribute(@"friendlyname"),
                                                Notes = (string)g.Attribute(@"notes"),
                                                IniValue = g.Value
                                            }).ToList()
                                        }).ToList(),
                                        NameProperties = e.Elements(@"nameproperty").Select(f => new IniPropertyName
                                        {
                                            CanAutoReset = f.Attribute(@"canautoreset") != null ? (bool)f.Attribute(@"canautoreset") : true,
                                            PropertyName = (string)f.Attribute(@"propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute(@"friendlyname"),
                                            Notes = (string)f.Attribute(@"notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),

                                    }).ToList();

                    List<IniPropertyMaster> items = new List<IniPropertyMaster>();
                    foreach (IniSection sec in linqlist)
                    {
                        sec.PropogateOwnership();
                        items.AddRange(sec.GetAllProperties());
                    }

                    string inifilepath = Path.Combine(configFileFolder, Path.GetFileNameWithoutExtension(kp.Key) + @".ini");
                    if (File.Exists(inifilepath))
                    {
                        DuplicatingIni configIni = DuplicatingIni.LoadIni(inifilepath);
                        foreach (IniPropertyMaster prop in items)
                        {
                            prop.LoadCurrentValue(configIni);
                        }
                    }
                    else
                    {
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_missingConfigFileForMEIM, Path.GetFileNameWithoutExtension(kp.Key), inifilepath));
                        doNotOpen = true;
                    }


                    kp.Value.ReplaceAll(items);
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(kp.Value);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription(@"SectionFriendlyName");
                    view.GroupDescriptions.Add(groupDescription);
                }
            }
            else
            {
                doNotOpen = true;
                M3L.ShowDialog(null, M3L.GetString(M3L.string_interp_dialogConfigDirectoryMissing, configFileFolder), M3L.GetString(M3L.string_cannotRunMassEffectIniModder), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public string GetPropertyMap(string filename)
        {
            string result = string.Empty;

            using Stream stream = this.GetType().Assembly.
                GetManifestResourceStream($@"ME3TweaksModManager.modmanager.meim.propertymaps.{filename}");
            using StreamReader sr = new StreamReader(stream);
            result = sr.ReadToEnd();
            return result;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var items = new[] {BioEngineEntries.ToList(), BioGameEntries.ToList(), BioPartyEntries.ToList()};

            foreach (var list in items)
            {
                foreach (IniPropertyMaster prop in list)
                {
                    if (prop.CanAutoReset)
                    {
                        prop.Reset();
                    }
                }
            }

            ShowMessage(M3L.GetString(M3L.string_resetIniItemsExceptBasic),7000);
        }

        /// <summary>
        /// Shows a message in the statusbar, which is cleared after a few seconds.
        /// </summary>
        /// <param name="v">String to display</param>
        private void ShowMessage(string v, long milliseconds = 4000)
        {
            TextBlock_Status.Text = v;
            if (milliseconds > 0)
            {
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
                timer.Tick += (s, a) =>
                {
                    TextBlock_Status.Text = "";
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            saveData();
        }

        private void saveData()
        {
            var saveMap = new Dictionary<string, List<IniPropertyMaster>>();
            saveMap[@"BioEngine.ini"] = BioEngineEntries.ToList();
            saveMap[@"BioGame.ini"] = BioGameEntries.ToList();
            saveMap[@"BioParty.ini"] = BioPartyEntries.ToList();
            string configFileFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BioWare\Mass Effect\Config";

            foreach (var kp in saveMap)
            {
                string configFileBeingUpdated = Path.Combine(configFileFolder, kp.Key);
                if (File.Exists(configFileBeingUpdated))
                {
                    M3Log.Information(@"MEIM: Saving ini file: " + configFileBeingUpdated);

                    //unset readonly
                    File.SetAttributes(configFileBeingUpdated, File.GetAttributes(configFileBeingUpdated) & ~FileAttributes.ReadOnly);

                    DuplicatingIni ini = DuplicatingIni.LoadIni(configFileBeingUpdated);
                    foreach (IniPropertyMaster prop in kp.Value)
                    {
                        string validation = prop.Validate(@"CurrentValue");
                        if (validation == null)
                        {
                            var itemToUpdate = ini.GetValue(prop.SectionName, prop.PropertyName);
                            if (itemToUpdate != null)
                            {
                                itemToUpdate.Value = prop.ValueToWrite;
                            }
                            else
                            {
                                M3Log.Error($@"Could not find property to update in ini! [{prop.SectionName}] {prop.PropertyName}");
                            }
                        }
                        else
                        {
                            M3Log.Error($@"Could not save property {prop.FriendlyPropertyName} because {validation}");
                            M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_propertyNotSaved, prop.FriendlyPropertyName, validation), M3L.GetString(M3L.string_errorSavingProperties), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    TelemetryInterposer.TrackEvent(@"Saved game config in MEIM");
                    File.WriteAllText(configFileBeingUpdated, ini.ToString());
                    ShowMessage(M3L.GetString(M3L.string_saved));
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (doNotOpen)
            {
                Close();
            }
        }

        public bool AskToClose()
        {
            if (doNotOpen)
                return true;

            if (M3L.ShowDialog(this, "Close without saving changes?", "Application closing", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                Close();
                return true;
            }

            // Denied closing.
            return false;
        }
    }
}
