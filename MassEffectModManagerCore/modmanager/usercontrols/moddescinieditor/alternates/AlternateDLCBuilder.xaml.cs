﻿using System.ComponentModel;
using System.Windows;
using IniParser.Model;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateDLCBuilder.xaml
    /// </summary>
    public partial class AlternateDLCBuilder : AlternateBuilderBaseControl, INotifyPropertyChanged
    {
        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (HasLoaded) return;
            AttachedJob = EditingMod?.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (AttachedJob != null)
            {
                Alternates.ReplaceAll(AttachedJob.AlternateDLCs);
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
                ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_ALTDLC] = outStr;
            }
        }

        public AlternateDLCBuilder()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddAlternateDLCCommand = new GenericCommand(AddAlternateDLC, CanAddAlternateDLC);
        }

        private bool CanAddAlternateDLC() => EditingMod != null && EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC) != null;

        private void AddAlternateDLC()
        {
            Alternates.Add(new AlternateDLC(EditingMod, $@"Alternate DLC {Alternates.Count + 1}", AlternateDLC.AltDLCCondition.COND_MANUAL, AlternateDLC.AltDLCOperation.OP_NOTHING)); // As this is noun in mod manager terminology it shouldn't be localized, i think
        }

        public GenericCommand AddAlternateDLCCommand { get; set; }
    }
}
