using LinuxDebugger.ProjectSystem.PropertyPages.Editors;
using LinuxDebugger.ProjectSystem.Serialization;
using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.ViewModels
{
    public sealed class FileUploadViewModel : ViewModel
    {

        #region Mode property

        private MappingMode m_Mode;
        /// <summary>
        /// Mode property
        /// <summary>
        public MappingMode Mode
        {
            get => m_Mode;
            set => this.SetProperty(ref m_Mode, value);
        }

        #endregion Mode property

        #region OpenInEditor property

        private bool m_OpenInEditor;
        /// <summary>
        /// OpenInEditor property
        /// <summary>
        public bool OpenInEditor
        {
            get => m_OpenInEditor;
            set => this.SetProperty(ref m_OpenInEditor, value);
        }

        #endregion OpenInEditor property

        #region Required property

        private bool m_Required;
        /// <summary>
        /// Required property
        /// <summary>
        public bool Required
        {
            get => m_Required;
            set => this.SetProperty(ref m_Required, value);
        }

        #endregion Required property

        #region LocalPath property

        private string? m_LocalPath;
        /// <summary>
        /// LocalPath property
        /// <summary>
        public string? LocalPath
        {
            get => m_LocalPath;
            set => this.SetProperty(ref m_LocalPath, value);
        }

        #endregion LocalPath property

        #region RemotePath property

        private string? m_RemotePath;
        /// <summary>
        /// RemotePath property
        /// <summary>
        public string? RemotePath
        {
            get => m_RemotePath;
            set => this.SetProperty(ref m_RemotePath, value);
        }
        #endregion RemotePath property

        #region IsPlaceHolder property

        private bool m_IsPlaceHolder;
        /// <summary>
        /// CanRemove property
        /// <summary>
        public bool IsPlaceHolder
        {
            get => m_IsPlaceHolder;
            set
            {
                if(this.SetProperty(ref m_IsPlaceHolder, value))
                {
                    this.NotifyPropertyChanged(nameof(IsMapping));
                }
            }
        }

        #endregion IsPlaceHolder property

        #region IsOpen property

        private bool m_IsOpen;
        /// <summary>
        /// IsOpen property
        /// <summary>
        public bool IsOpen
        {
            get => m_IsOpen;
            set => this.SetProperty(ref m_IsOpen, value);
        }

        #endregion IsOpen property

        public bool IsValid => this.LocalPath.IsPresent();

        public bool IsMapping => !IsPlaceHolder;

        internal FileUploadViewModel()
        {
            this.IsPlaceHolder = true;
        }

        internal void Inititialize(DownloadMetadata val)
        {
            this.OpenInEditor = val.OpenInEditor;
            this.Required = val.Required;
            this.RemotePath = val.Path;
        }
    }
}
