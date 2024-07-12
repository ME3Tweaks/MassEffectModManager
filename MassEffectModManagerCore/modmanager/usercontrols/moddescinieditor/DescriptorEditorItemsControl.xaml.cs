﻿using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for AlternatesItemsControl.xaml
    /// </summary>
    [DebuggerDisplay(@"DescriptorEditorItemsControl Header={HeaderText} ItemCount={GetItemCount()}")]
    public partial class DescriptorEditorItemsControl : UserControl, INotifyPropertyChanged
    {
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(@"HeaderText", typeof(string), typeof(DescriptorEditorItemsControl));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(@"Description", typeof(string), typeof(DescriptorEditorItemsControl));

        public ICollection ItemsSource
        {
            get => (ICollection)GetValue(ItemsSourceProperty);
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(@"ItemsSource", typeof(ICollection), typeof(DescriptorEditorItemsControl), new PropertyMetadata(new PropertyChangedCallback(OnItemsSourcePropertyChanged)));

        [SuppressPropertyChangedWarnings]
        private static void OnItemsSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = sender as DescriptorEditorItemsControl;
            control?.OnItemsSourceChanged((ICollection)e.OldValue, (ICollection)e.NewValue);
        }

        /// <summary>
        /// Invoked when the value of a property changes, after loading.
        /// </summary>
        public event EventHandler OnMDValueChanged;

        [SuppressPropertyChangedWarnings]
        private void OnItemsSourceChanged(ICollection oldValue, ICollection newValue)
        {
            // Remove handler for oldValue.CollectionChanged
            var oldValueINotifyCollectionChanged = oldValue as INotifyCollectionChanged;

            if (null != oldValueINotifyCollectionChanged)
            {
                oldValueINotifyCollectionChanged.CollectionChanged -= new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }
            // Add handler for newValue.CollectionChanged (if possible)
            var newValueINotifyCollectionChanged = newValue as INotifyCollectionChanged;
            if (null != newValueINotifyCollectionChanged)
            {
                newValueINotifyCollectionChanged.CollectionChanged += new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }
        }

        void newValueINotifyCollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
        }

#if DEBUG
        public string GetItemCount()
        {
            return ItemsSource != null ? $@"{ItemsSource.Count} items" : @"ItemsSource is null";
        }
#endif

        public DescriptorEditorItemsControl()
        {
            InitializeComponent();
        }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        private void AllowedValuesDropdown_Opened(object sender, EventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is MDParameter md && md.AllowedValuesPopulationFunc != null)
            {
                // Repopulate
                md.AllowedValues.ReplaceAll(md.AllowedValuesPopulationFunc.Invoke());
            }
        }

        /// <summary>
        /// Routes a property changing event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnValueChanged(object sender, TextChangedEventArgs e)
        {
            // Caller can access it on the sender.
            OnMDValueChanged?.Invoke(sender, EventArgs.Empty);
        }
    }
}
