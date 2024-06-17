﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.ui
{
    public static class ClipperHelper
    {
        /// <summary>
        /// Hides or shows horizontal content, with the assumption the content is closed by default.
        /// </summary>
        /// <param name="clippedPanel"></param>
        /// <param name="show"></param>
        /// <param name="completionDelegate"></param>
        /// <param name="isInitial"></param>
        /// <param name="animTime"></param>
        public static void ShowHideHorizontalContent(FrameworkElement clippedPanel, bool show, bool isInitial = false, double animTime = 0.15, Action completionDelegate = null)
        {
            if (isInitial && !show) return; // Don't do any animation since it's already closed
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    // Show the panel but set the height to zero
                    //clippedPanel.Height = 0;
                    clippedPanel.Visibility = Visibility.Visible;
                }

                var from = show ? 0.0 : 1.0;
                var to = show ? 1.0 : 0.0;
                DoubleAnimation animation = new DoubleAnimation(from, to, new Duration(TimeSpan.FromSeconds(animTime)));

                // We put this here in the event multiple animations play at once
                // (Such as failed mods panel) as it might make it collapsed due to timing.
                animation.Completed += (sender, args) =>
                {
                    clippedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed; // Collapse so renderer doesn't try to do anything with it
                    completionDelegate?.Invoke();
                };

                clippedPanel.BeginAnimation(Clipper.WidthFractionProperty, animation);
            });
        }


        /// <summary>
        /// Hides or shows vertical content, with the assumption the content is closed by default.
        /// </summary>
        /// <param name="clippedPanel"></param>
        /// <param name="show"></param>
        /// <param name="completed"></param>
        /// <param name="isInitial"></param>
        /// <param name="animTime"></param>
        public static void ShowHideVerticalContent(FrameworkElement clippedPanel, bool show, bool isInitial = false, double animTime = 0.15, Action completionDelegate = null)
        {
            if (isInitial && !show) return; // Don't do any animation since it's already closed
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    // Show the panel but set the height to zero
                    //clippedPanel.Height = 0;
                    clippedPanel.Visibility = Visibility.Visible;
                }

                var from = show ? 0.0 : 1.0;
                var to = show ? 1.0 : 0.0;
                DoubleAnimation animation = new DoubleAnimation(from, to, new Duration(TimeSpan.FromSeconds(animTime)));

                // We put this here in the event multiple animations play at once
                // (Such as failed mods panel) as it might make it collapsed due to timing.
                animation.Completed += (sender, args) =>
                {
                    clippedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed; // Collapse so renderer doesn't try to do anything with it
                    completionDelegate?.Invoke();
                };

                clippedPanel.BeginAnimation(Clipper.HeightFractionProperty, animation);
            });
        }
    }

    // From https://stackoverflow.com/a/59376318/800318

    // * Note the Constraint property: it determines what the child control considers "Auto" dimensions.
    // * For example, if your control is static (has Height and Width set explicitly), you should set
    // * Constraint to Nothing to clip the fraction of the entire element. If your control is WrapPanel
    // * with Orientation set to Horizontal, Constraint should be set to Width, etc. If you are getting
    // * wrong clipping, try out out different constraints. Note also that Clipper respects you control's
    // * alignment, which can potentially be exploited in an animation (for example, while animating
    // * HeightFraction from 0 to 1, VerticalAlignment.Bottom will mean that the control "slides down",
    // * VerticalAlignment.Center - "opens up").
    // *
    // * Mgamerz Note: You seem to have to set the alignments for this to animate properly.
    // *

    public sealed class Clipper : Decorator
    {
        public static readonly DependencyProperty WidthFractionProperty = DependencyProperty.RegisterAttached(@"WidthFraction", typeof(double), typeof(Clipper), new PropertyMetadata(1d, OnClippingInvalidated), IsFraction);
        public static readonly DependencyProperty HeightFractionProperty = DependencyProperty.RegisterAttached(@"HeightFraction", typeof(double), typeof(Clipper), new PropertyMetadata(1d, OnClippingInvalidated), IsFraction);
        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(@"Background", typeof(Brush), typeof(Clipper), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ConstraintProperty = DependencyProperty.Register(@"Constraint", typeof(ConstraintSource), typeof(Clipper), new PropertyMetadata(ConstraintSource.WidthAndHeight, OnClippingInvalidated), IsValidConstraintSource);
        
        // ME3TWEAKS EXTENSION - Property you can bind to slide open and closed with the default ME3Tweaks ClipperHelper animation.
        public static readonly DependencyProperty VisibilityValueProperty = DependencyProperty.Register(nameof(VisibilityValue), typeof(bool), typeof(Clipper), new PropertyMetadata(false));



        private Size _childSize;
        private DependencyPropertySubscriber _childVerticalAlignmentSubcriber;
        private DependencyPropertySubscriber _childHorizontalAlignmentSubscriber;

        public Clipper()
        {
            ClipToBounds = true;
        }

        public Brush Background
        {
            get { return (Brush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        public ConstraintSource Constraint
        {
            get { return (ConstraintSource)GetValue(ConstraintProperty); }
            set { SetValue(ConstraintProperty, value); }
        }

        public bool VisibilityValue
        {
            get => (bool)GetValue(VisibilityValueProperty);
            set
            {
                SetValue(VisibilityValueProperty, value);
                ClipperHelper.ShowHideVerticalContent(this, value);
            }
        }

        [AttachedPropertyBrowsableForChildren]
        public static double GetWidthFraction(DependencyObject obj)
        {
            return (double)obj.GetValue(WidthFractionProperty);
        }

        public static void SetWidthFraction(DependencyObject obj, double value)
        {
            obj.SetValue(WidthFractionProperty, value);
        }

        [AttachedPropertyBrowsableForChildren]
        public static double GetHeightFraction(DependencyObject obj)
        {
            return (double)obj.GetValue(HeightFractionProperty);
        }

        public static void SetHeightFraction(DependencyObject obj, double value)
        {
            obj.SetValue(HeightFractionProperty, value);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (Child is null)
            {
                return Size.Empty;
            }

            switch (Constraint)
            {
                case ConstraintSource.WidthAndHeight:
                    Child.Measure(constraint);
                    break;

                case ConstraintSource.Width:
                    Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
                    break;

                case ConstraintSource.Height:
                    Child.Measure(new Size(double.PositiveInfinity, constraint.Height));
                    break;

                case ConstraintSource.Nothing:
                    Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    break;
            }

            var finalSize = Child.DesiredSize;
            if (Child is FrameworkElement childElement)
            {
                if (childElement.HorizontalAlignment == HorizontalAlignment.Stretch && constraint.Width > finalSize.Width && !double.IsInfinity(constraint.Width))
                {
                    finalSize.Width = constraint.Width;
                }

                if (childElement.VerticalAlignment == VerticalAlignment.Stretch && constraint.Height > finalSize.Height && !double.IsInfinity(constraint.Height))
                {
                    finalSize.Height = constraint.Height;
                }
            }

            _childSize = finalSize;

            finalSize.Width *= GetWidthFraction(Child);
            finalSize.Height *= GetHeightFraction(Child);

            return finalSize;
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (Child is null)
            {
                return Size.Empty;
            }

            var childSize = _childSize;
            var clipperSize = new Size(Math.Min(arrangeSize.Width, childSize.Width * GetWidthFraction(Child)),
                                       Math.Min(arrangeSize.Height, childSize.Height * GetHeightFraction(Child)));
            var offsetX = 0d;
            var offsetY = 0d;

            if (Child is FrameworkElement childElement)
            {
                if (childSize.Width > clipperSize.Width)
                {
                    switch (childElement.HorizontalAlignment)
                    {
                        case HorizontalAlignment.Right:
                            offsetX = -(childSize.Width - clipperSize.Width);
                            break;

                        case HorizontalAlignment.Center:
                            offsetX = -(childSize.Width - clipperSize.Width) / 2;
                            break;
                    }
                }

                if (childSize.Height > clipperSize.Height)
                {
                    switch (childElement.VerticalAlignment)
                    {
                        case VerticalAlignment.Bottom:
                            offsetY = -(childSize.Height - clipperSize.Height);
                            break;

                        case VerticalAlignment.Center:
                            offsetY = -(childSize.Height - clipperSize.Height) / 2;
                            break;
                    }
                }
            }

            Child.Arrange(new Rect(new Point(offsetX, offsetY), childSize));

            return clipperSize;
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            void UpdateLayout(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                if (e.NewValue.Equals(HorizontalAlignment.Stretch) || e.NewValue.Equals(VerticalAlignment.Stretch))
                {
                    InvalidateMeasure();
                }
                else
                {
                    InvalidateArrange();
                }
            }

            _childHorizontalAlignmentSubscriber?.Unsubscribe();
            _childVerticalAlignmentSubcriber?.Unsubscribe();

            if (visualAdded is FrameworkElement childElement)
            {
                _childHorizontalAlignmentSubscriber = new DependencyPropertySubscriber(childElement, HorizontalAlignmentProperty, UpdateLayout);
                _childVerticalAlignmentSubcriber = new DependencyPropertySubscriber(childElement, VerticalAlignmentProperty, UpdateLayout);
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
        }

        private static bool IsFraction(object value)
        {
            var numericValue = (double)value;
            return numericValue >= 0d && numericValue <= 1d;
        }

        private static void OnClippingInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && VisualTreeHelper.GetParent(element) is Clipper translator)
            {
                translator.InvalidateMeasure();
            }
        }

        private static bool IsValidConstraintSource(object value)
        {
            return Enum.IsDefined(typeof(ConstraintSource), value);
        }
    }

    public enum ConstraintSource
    {
        WidthAndHeight,
        Width,
        Height,
        Nothing
    }

    public class DependencyPropertySubscriber : DependencyObject
    {
        private static readonly DependencyProperty ValueProperty = DependencyProperty.Register(@"Value", typeof(object), typeof(DependencyPropertySubscriber), new PropertyMetadata(null, ValueChanged));

        private readonly PropertyChangedCallback _handler;

        public DependencyPropertySubscriber(DependencyObject dependencyObject, DependencyProperty dependencyProperty, PropertyChangedCallback handler)
        {
            if (dependencyObject is null)
            {
                throw new ArgumentNullException(nameof(dependencyObject));
            }

            if (dependencyProperty is null)
            {
                throw new ArgumentNullException(nameof(dependencyProperty));
            }

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));

            var binding = new Binding() { Path = new PropertyPath(dependencyProperty), Source = dependencyObject, Mode = BindingMode.OneWay };
            BindingOperations.SetBinding(this, ValueProperty, binding);
        }

        public void Unsubscribe()
        {
            BindingOperations.ClearBinding(this, ValueProperty);
        }

        private static void ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DependencyPropertySubscriber)d)._handler(d, e);
        }
    }
}
