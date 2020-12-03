﻿using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using ME3ExplorerCore.Misc;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for MetadataEditorControl.xaml
    /// </summary>
    public partial class MetadataEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public MetadataEditorControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        public ObservableCollectionExtended<MDParameter> ModManagerParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();
        public ObservableCollectionExtended<MDParameter> ModInfoParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();
        public ObservableCollectionExtended<MDParameter> UPDATESParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        public override void OnEditingModChanged(Mod newMod)
        {
            base.OnEditingModChanged(newMod);
            newMod.BuildParameterMap();
            ModManagerParameterMap.ReplaceAll(newMod.ParameterMap.Where(x => x.Header == "ModManager"));
            ModInfoParameterMap.ReplaceAll(newMod.ParameterMap.Where(x => x.Header == "ModInfo"));
            UPDATESParameterMap.ReplaceAll(newMod.ParameterMap.Where(x => x.Header == "UPDATES"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void Serialize(IniData ini)
        {
            foreach (var v in EditingMod.ParameterMap) //references will still be same
            {
                if (!string.IsNullOrWhiteSpace(v.Value))
                {
                    ini[v.Header][v.Key] = v.Value;
                }
            }
        }
    }
}
