using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LinuxDebugger.ProjectSystem.ViewModels;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    public static class Behaviors
    {
        public static readonly DependencyProperty LastMappingInputBehaviorProperty = DependencyProperty
            .RegisterAttached("LastMappingInputBehavior", typeof(bool), typeof(Behaviors),
            new()
            {
                PropertyChangedCallback = (o, e) => SetLastMappingInputBehavior(o, (bool)e.NewValue)
            });

        public static readonly DependencyProperty PathMappingBehaviorProperty = DependencyProperty
            .RegisterAttached("PathMappingBehavior", typeof(bool), typeof(Behaviors),
            new()
            {
                PropertyChangedCallback = (o, e) => SetPathMappingBehavior(o, (bool)e.NewValue)
            });

        public static void SetLastMappingInputBehavior(DependencyObject d, bool value)
        {
            if (d is UIElement uie)
            {
                if (value)
                {
                    uie.PreviewKeyDown += onLastInputKeyUp;
                    uie.SetValue(LastMappingInputBehaviorProperty, Boxes.BooleanTrue);
                }
                else
                {
                    uie.PreviewKeyDown -= onLastInputKeyUp;
                    uie.SetValue(LastMappingInputBehaviorProperty, Boxes.BooleanFalse);
                }
            }
        }

        private static void onLastInputKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Source != e.OriginalSource)
                return;
            if (e.Key == Key.Tab && e.OriginalSource is TextBox tb)
            {
                if (tb.DataContext is FileUploadViewModel vm
                    && vm.IsPlaceHolder)
                {
                    var mappingControl = tb.FindAncestor<PathMappingControl>();
                    if (mappingControl is not null)
                    {
                        e.Handled = mappingControl.HandleTab(vm);
                    }
                }
            }
        }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static bool GetPathMappingBehavior(DependencyObject d)
        {
            var val = d.GetValue(PathMappingBehaviorProperty);
            return val is bool b ? b : false;
        }
        public static void SetPathMappingBehavior(DependencyObject d, bool value)
        {
            if (d is FrameworkElement uie)
            {
                if (value)
                {
                    uie.Initialized += onPathMappingControlInitialized;
                    uie.LostFocus += onPathMappingTextBoxLostFocus;
                    uie.SetValue(PathMappingBehaviorProperty, Boxes.BooleanTrue);
                }
                else
                {
                    uie.Initialized -= onPathMappingControlInitialized;
                    uie.LostFocus -= onPathMappingTextBoxLostFocus;
                    uie.SetValue(PathMappingBehaviorProperty, Boxes.BooleanFalse);
                }
            }
        }

        private static void onPathMappingControlInitialized(object sender, EventArgs e)
        {
            if (sender is FrameworkElement fe
                && fe.DataContext is FileUploadViewModel vm)
            {
                var pathMapping = fe.FindAncestor<PathMappingControl>();
                if (pathMapping is not null)
                {
                    pathMapping.OnMappingControlInitialized(fe, vm);
                }
            }
        }

        private static void onPathMappingTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource as TextBox;
            if (e.OriginalSource is TextBox tb)
            {
                var dt = tb.DataContext as FileUploadViewModel;
                if (dt is not null)
                {
                    var grid = tb.FindAncestor<Grid>();
                    if (!grid.IsKeyboardFocusWithin)
                    {
                        var ctrl = src.FindAncestor<PathMappingControl>();
                        //ctrl?.Save(dt);
                    }
                }
            }
        }

        static readonly DependencyProperty DropdownAnimationProperty = DependencyProperty
            .RegisterAttached("DropdownHandler",
                              typeof(DropdownAnimationHandler),
                              typeof(Behaviors),
                              new(null));

        [AttachedPropertyBrowsableForType(typeof(Border))]
        public static void SetDropdownDuration(DependencyObject @object, TimeSpan value)
        {
            var fe = @object as FrameworkElement;
            if (fe is not null)
            {
                var animation = (DropdownAnimationHandler)@object.GetValue(DropdownAnimationProperty);
                if (animation is null)
                {
                    animation = new(fe);
                    @object.SetValue(DropdownAnimationProperty, animation);
                }
                animation.Duration = value;
            }
        }

        public static readonly DependencyProperty DropdownEnabledProperty = DependencyProperty
            .RegisterAttached("DropdownEnabled",
            typeof(bool),
            typeof(Behaviors),
            new()
            {
                DefaultValue = Boxes.BooleanTrue,
                PropertyChangedCallback = (o, e) => SetDropdownEnabled(o, (bool)e.NewValue)
            });

        public static readonly DependencyProperty DropdownOpenProperty = DependencyProperty
            .RegisterAttached("DropdownOpen",
            typeof(bool),
            typeof(Behaviors),
            new()
            {
                PropertyChangedCallback = (o, e) => SetDropdownOpen(o, (bool)e.NewValue)
            });

        public static bool GetDropdownEnabled(DependencyObject @object)
            => (bool)@object.GetValue(DropdownEnabledProperty);
        public static void SetDropdownEnabled(DependencyObject @object, bool newValue)
        {
            var fe = @object as FrameworkElement;
            if (fe is not null)
            {
                var animation = (DropdownAnimationHandler?)@object.GetValue(DropdownAnimationProperty);
                if (animation is null)
                {
                    animation = getOrCreateDropdownHandler(fe);
                    Assumes.NotNull(animation);
                    animation.Enabled = newValue;
                }
                else
                {
                    animation.Enabled = newValue;
                }
            }
        }

        public static bool GetDropdownOpen(DependencyObject @object)
            => (bool)@object.GetValue(DropdownOpenProperty);
        [AttachedPropertyBrowsableForType(typeof(Border))]
        public static void SetDropdownOpen(DependencyObject @object, bool value)
        {
            var animation = getOrCreateDropdownHandler(@object);
            animation?.SetValue(value);
        }

        private static DropdownAnimationHandler? getOrCreateDropdownHandler(DependencyObject @object)
        {
            var fe = @object as FrameworkElement;
            if (fe is not null)
            {
                var animation = (DropdownAnimationHandler?)@object.GetValue(DropdownAnimationProperty);
                if (animation is null)
                {
                    animation = new(fe)
                    {
                        Enabled = GetDropdownEnabled(@object)
                    };
                    @object.SetValue(DropdownAnimationProperty, animation);
                }
                return animation;
            }
            return null;
        }

        sealed class DropdownAnimationHandler
        {
            private DoubleAnimation? animation;
            private TimeSpan duration = TimeSpan.FromMilliseconds(500);
            private bool currentValue;

            internal double MaxHeight => double.MaxValue;
            internal double MinHeight => double.MaxValue;

            public DoubleAnimation Animation
            {
                get
                {
                    if (this.animation is null)
                    {
                        this.animation = new()
                        {
                            Duration = this.duration
                        };
                        Storyboard.SetTargetProperty(this.Element, new(FrameworkElement.HeightProperty));
                    }
                    return this.animation;
                }
            }
            public TimeSpan Duration
            {
                get => this.duration;
                internal set
                {
                    this.duration = value;
                    this.Animation.Duration = duration;
                }
            }
            public FrameworkElement Element { get; }

            public Dispatcher Dispatcher => this.Element.Dispatcher;

            public bool Enabled { get; internal set; } = true;

            internal DropdownAnimationHandler(FrameworkElement fe)
            {
                this.Element = fe;
            }

            internal void SetValue(bool value)
            {
                if (value == this.currentValue)
                {
                    return;
                }
                if (!this.Enabled)
                    return;
                this.CheckAccess();
                if (!this.Element.IsVisible)
                {
                    this.currentValue = value;
                    return;
                }
                if (value)
                {
                    var heigth = this.getMeasureElement();
                    if (heigth == 0)
                    {
                        this.currentValue = value;
                        return;
                    }
                    this.Animation.From = 0d;
                    this.Animation.To = heigth;
                }
                else
                {
                    this.Animation.From = this.Element.ActualHeight;
                    this.Animation.To = 0;
                }
                void onCompleted(object o, EventArgs e)
                {
                    this.Animation.Completed -= onCompleted;
                    this.currentValue = value;
                }
                this.Animation.Completed += onCompleted;
                this.Element
                    .BeginAnimation(FrameworkElement.MaxHeightProperty, this.Animation);
            }

            private double getMeasureElement()
            {
                Size measureSize = new(double.MaxValue, this.MaxHeight);
                double maxHeight = 0;
                foreach(var descendant in this.Element.GetVisualChildren<UIElement>())
                {
                    descendant.Measure(measureSize);
                    maxHeight = Math.Max(maxHeight, descendant.DesiredSize.Height);
                }
                return maxHeight;
            }

            private void CheckAccess()
            {
                this.Dispatcher.CheckAccess();
            }
        }
    }
}
