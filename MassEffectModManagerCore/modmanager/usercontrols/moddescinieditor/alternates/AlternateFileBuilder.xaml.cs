﻿using System.ComponentModel;
using System.Linq;
using System.Windows;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateDLCBuilder.xaml
    /// </summary>
    public partial class AlternateFileBuilder : AlternateBuilderBaseControl, INotifyPropertyChanged
    {
        public string DirectionsText
        {
            get => (string)GetValue(DirectionsTextProperty);
            set => SetValue(DirectionsTextProperty, value);
        }

        public static readonly DependencyProperty DirectionsTextProperty = DependencyProperty.Register(@"DirectionsText", typeof(string), typeof(AlternateFileBuilder));

        public ModJob.JobHeader? TaskHeader
        {
            get => (ModJob.JobHeader?)GetValue(TaskHeaderProperty);
            set => SetValue(TaskHeaderProperty, value);
        }

        public static readonly DependencyProperty TaskHeaderProperty = DependencyProperty.Register(@"TaskHeader", typeof(ModJob.JobHeader?), typeof(AlternateFileBuilder));

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                AttachedJob = TaskHeader != null ? EditingMod.GetJob(TaskHeader.Value) : null;

                if (AttachedJob != null)
                {
                    Alternates.ReplaceAll(AttachedJob.AlternateFiles);
                    foreach (var a in Alternates)
                    {
                        a.BuildParameterMap(EditingMod);
                    }
                }
                else
                {
                    Alternates.ClearEx();
                }

                HasLoaded = true;
            }
        }

        public override void Serialize(IniData ini)
        {
            if (AttachedJob != null && Alternates.Any())
            {
                string outStr = @"(";
                bool isFirst = true;
                foreach (var adlc in Alternates)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        outStr += @",";
                    }
                    outStr += StringStructParser.BuildCommaSeparatedSplitValueList(adlc.ParameterMap.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value));
                }

                outStr += @")";
                ini[TaskHeader.ToString()][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_ALTFILES] = outStr;
            }
        }

        public AlternateFileBuilder()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddAlternateFileCommand = new GenericCommand(AddAlternateFile, CanAddAlternateFile);
        }

        private bool CanAddAlternateFile() => TaskHeader != null && EditingMod?.GetJob(TaskHeader.Value) != null;

        private void AddAlternateFile()
        {
            Alternates.Add(new AlternateFile(EditingMod, M3L.GetString(M3L.string_interp_alternateFileX, Alternates.Count + 1), AlternateFile.AltFileCondition.COND_MANUAL, AlternateFile.AltFileOperation.OP_NOTHING));
        }


        public GenericCommand AddAlternateFileCommand { get; set; }

        //public ObservableCollectionExtended<AlternateDLC> AlternateDLCs { get; } = new ObservableCollectionExtended<AlternateDLC>();
    }
}
