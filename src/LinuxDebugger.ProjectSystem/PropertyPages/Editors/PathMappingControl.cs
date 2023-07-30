using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LinuxDebugger.ProjectSystem.ViewModels;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    public enum MappingMode
    {
        Upload = 1,
        Download = 2
    }

    [TemplatePart(Name = PART_ItemsControl, Type = typeof(ItemsControl))]
    internal sealed class PathMappingControl : Control
    {

        const string PART_ItemsControl = nameof(PART_ItemsControl);
        const string c_LocalInputName = "LocalPathInput";
        const string c_RemoteInputName = "RemotePathInput";
        const string c_DetailsButtonName = "DetailsButton";
        const StringComparison c_NameComparison = StringComparison.Ordinal;

        public static readonly DependencyProperty EditorMetadataProperty;

        private FileUploadViewModel? currentOpenedItem;
        private bool isUpdating;
        private readonly ObservableCollection<FileUploadViewModel> mappings;

        public IPathMappingEncoding Encoding { get; private set; }




        public MappingMode MappingMode
        {
            get { return (MappingMode)GetValue(MappingModeProperty); }
            set { SetValue(MappingModeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MappingMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MappingModeProperty
            = DependencyProperty.Register(nameof(MappingMode),
                typeof(MappingMode),
                typeof(PathMappingControl),
                new PropertyMetadata(MappingMode.Upload)
                {
                    PropertyChangedCallback = (o, e)=>
                    {
                        var mode = (MappingMode)e.NewValue;
                        var ctrl = (PathMappingControl)o;
                        ctrl.Encoding = EditorTemplates.ModeToEcoding(mode);
                        ctrl.reloadMappings();
                    }
                });



        public IReadOnlyDictionary<string,string> EditorMetadata
        {
            get { return (IReadOnlyDictionary<string, string>)GetValue(EditorMetadataProperty); }
            private set { SetValue(EditorMetadataProperty, value); }
        }

        public string StringListProperty
        {
            get { return (string)GetValue(StringListPropertyProperty); }
            set { SetValue(StringListPropertyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StringListProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StringListPropertyProperty
            = DependencyProperty.Register(nameof(StringListProperty),
                typeof(string),
                typeof(PathMappingControl),
                new()
                {
                    PropertyChangedCallback = (o,e)=> ((PathMappingControl)o).reloadMappings()
                });


        public ICommand RemoveCommand { get; }


        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(PathMappingControl));

        void reloadMappings()
        {
            if (this.isUpdating)
                return;
            this.isUpdating = true;
            try
            {
                this.mappings.Clear();
                if (this.Encoding is not null && this.StringListProperty.IsPresent())
                {
                    this.Encoding.Parse(this.StringListProperty, this.mappings);
                    foreach (var nvals in this.mappings)
                        nvals.IsPlaceHolder = false;
                }
                this.mappings.Add(createPlaceHolder());
                this.NotifyMappingsChanged();
            }
            finally
            {
                this.isUpdating = false;
            }
        }

        public ReadOnlyObservableCollection<FileUploadViewModel> Mappings
        {
            get { return (ReadOnlyObservableCollection<FileUploadViewModel>)GetValue(MappingsProperty); }
            private set { SetValue(MappingsPropertyKey, value); }
        }

        public bool HasDetailsOpened
        {
            get => this.currentOpenedItem?.IsOpen == true;
        }

        // Using a DependencyProperty as the backing store for MappingCollection.  This enables animation, styling, binding, etc...
        static readonly DependencyPropertyKey MappingsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Mappings),
                typeof(ReadOnlyObservableCollection<FileUploadViewModel>),
                typeof(PathMappingControl),
                new(){});
        public static readonly DependencyProperty MappingsProperty = MappingsPropertyKey.DependencyProperty;

        static PathMappingControl()
        {
            EditorMetadataProperty = EditorTemplates
                .EditorMetadataProperty
                .AddOwner(typeof(PathMappingControl),
                new FrameworkPropertyMetadata()
                {
                    PropertyChangedCallback = (d, e) =>
                    {
                        var dict = (IReadOnlyDictionary<string, string>)e.NewValue;
                        if (dict?.TryGetValue(Constants.EditorConstants.ModeMetadataKey, out var mode) == true)
                        {
                            if (string.Equals(mode, Constants.EditorConstants.DownloadMode, StringComparison.Ordinal))
                            {
                                ((PathMappingControl)d).MappingMode = MappingMode.Download;
                            }
                        }
                    }
                });
            FocusableProperty.OverrideMetadata(typeof(PathMappingControl), new FrameworkPropertyMetadata()
            {
                DefaultValue = false
            });
        }

        public PathMappingControl()
        {
            this.mappings = new() { createPlaceHolder() };
            this.Encoding = UploadFileMappingEncoding.Instance;
            this.Mappings = new(this.mappings);

            AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(onToggleButtonChecked));
            this.GotFocus += onTextBoxFocus;

            this.RemoveCommand = new DelegateCommand<FileUploadViewModel>(x =>
            {
                Assumes.True(this.mappings.Remove(x));
                this.NotifyMappingsChanged();
            },
            canExecute: vm => !IsPlaceHolder(vm),
            jtf: ThreadHelper.JoinableTaskFactory);
        }

        private void onTextBoxFocus(object sender,  RoutedEventArgs e)
        {
            if (IsPathInput(e.OriginalSource as DependencyObject))
            {
                var el = ((FrameworkElement)e.OriginalSource).DataContext as FileUploadViewModel;
                if (el != this.currentOpenedItem)
                    this.setCurrentDetail(null);
            }

        }

        private void setCurrentDetail(FileUploadViewModel? vm)
        {
            if (this.currentOpenedItem is not null
                && this.currentOpenedItem != vm)
            {
                this.currentOpenedItem.IsOpen = false;
            }
            this.currentOpenedItem = vm;
        }

        private void onToggleButtonChecked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement uie
                && string.Equals(uie.Name, c_DetailsButtonName, c_NameComparison))
            {
                var vm = uie.DataContext as FileUploadViewModel;
                this.setCurrentDetail(vm);
            }
        }

        private void NotifyMappingsChanged()
        {
            if (this.Encoding is null || this.isUpdating)
                return;
            var count = this.Mappings.Count;
            this.StringListProperty = this.Encoding.Format(this.Mappings.Take(count - 1));
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            if (!this.IsKeyboardFocusWithin)
                this.NotifyMappingsChanged();
        }

        internal void Save(FileUploadViewModel dt)
        {
            if (dt.IsPlaceHolder && dt.IsValid)
            {
                dt.IsPlaceHolder = false;
                this.mappings.Add(createPlaceHolder());
            }
            //this.NotifyMappingsChanged();
        }

        internal bool HandleTab(FileUploadViewModel vm)
        {
            if (vm == this.mappings[this.mappings.Count - 1] && vm.IsValid)
            {
                this.mappings.Add(createPlaceHolder());
                vm.IsPlaceHolder = false;
                CommandManager.InvalidateRequerySuggested();
                return false;
            }
            return false;
        }

        private FileUploadViewModel createPlaceHolder()
            => new()
            {
                IsPlaceHolder = true,
                Mode = Encoding?.Mode ?? MappingMode.Upload
            };

        internal void OnMappingControlInitialized(FrameworkElement fe,
            FileUploadViewModel vm)
        {
            if (this.IsPlaceHolder(vm))
            {
                var input = fe.FindName(c_LocalInputName) as TextBox;
                input?.Focus();
            }
        }

        private bool IsPathInput(DependencyObject? vm)
            => vm is FrameworkElement el
            && (string.Equals(el.Name, c_RemoteInputName, c_NameComparison)
            || string.Equals(el.Name, c_LocalInputName, c_NameComparison));
        private bool IsRemoteInput(FrameworkElement vm)
            => vm is TextBoxBase && string.Equals(vm.Name,
                c_RemoteInputName,
                StringComparison.Ordinal);
        private bool IsLocalInput(FrameworkElement vm)
            => vm is TextBoxBase && string.Equals(vm.Name, c_LocalInputName, c_NameComparison);
        private bool IsPlaceHolder(FileUploadViewModel vm)
            => vm is not null
            && this.mappings.Count > 0
            && vm == this.mappings[this.mappings.Count - 1];
    }
}
