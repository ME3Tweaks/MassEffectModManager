﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Helpers;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && parameter is string str)
            {
                if (str == "Reversed")
                {
                    return value != null ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

    [Localizable(false)]
    public class NullOrEmptyVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool reversed = false;
            if (parameter != null && parameter is string str)
            {
                reversed = str.CaseInsensitiveEquals("Reversed");
            }

            if (value is string vStr)
            {
                if (reversed)
                {
                    return string.IsNullOrWhiteSpace(vStr) ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    return !string.IsNullOrWhiteSpace(vStr) ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            if (reversed)
            {
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return value == null ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

    [Localizable(false)]
    public class NullHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && parameter is string str)
            {
                if (str == "Reversed")
                {
                    return value != null ? Visibility.Hidden : Visibility.Visible;
                }
            }
            return value == null ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
