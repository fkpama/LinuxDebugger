using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using LinuxDebugger.ProjectSystem.ViewModels;
using LinuxDebugger.VisualStudio.Settings;
using Microsoft.VisualStudio.Utilities;
using LinuxDebugger.Controls;

namespace LinuxDebugger.ViewModels
{

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum AdapterMode
    {
        [Description("Ssh")]
        Ssh,
        [Description("PLink")]
        PLink
    }

    public class OptionsViewModel : ViewModel
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        public string AdapterGroupHeader => "Adapter";
        public string VsDbgGroupHeader => "VS Debugger";

        #region AdapterMode property
        public string AdapterModeLabel => "Mode";

        private AdapterMode m_AdapterMode;
        /// <summary>
        /// AdapterMode property
        /// <summary>
        public AdapterMode AdapterMode
        {
            get => m_AdapterMode;
            set => this.SetProperty(ref m_AdapterMode, value);
        }

        #endregion AdapterMode property

        #region VsDbgDirectory property

        public string VsDbgDirectoryLabel => "VsDbg directory";
        private string m_VsDbgDirectory;
        /// <summary>
        /// VsDbgDirectory property
        /// <summary>
        [Name(nameof(LinuxDebuggerSettings.VsDbgDirectory))]
        public string VsDbgDirectory
        {
            get => m_VsDbgDirectory;
            set => this.SetProperty(ref m_VsDbgDirectory, value);
        }

        #endregion VsDbgDirectory property

        #region AdapterPathLabel property

        private string m_AdapterPathLabel = "ssh.exe path";
        /// <summary>
        /// AdapterPathLabel property
        /// <summary>
        public string AdapterPathLabel
        {
            get => m_AdapterPathLabel;
            set => this.SetProperty(ref m_AdapterPathLabel, value);
        }

        #endregion AdapterPathLabel property

        #region AdapterPath property

        private string m_AdapterPath;
        /// <summary>
        /// AdapterPath property
        /// <summary>
        [Name(nameof(LinuxDebuggerSettings.AdapterExePath))]
        public string AdapterPath
        {
            get => m_AdapterPath;
            set => this.SetProperty(ref m_AdapterPath, value);
        }

        #endregion AdapterPath property

        #region AutoInstallVsDbg property
        public string AutoInstallVsDbgLabel => "Automatically install VsDbg";

        private bool m_AutoInstallVsDbg;
        /// <summary>
        /// AutoInstallVsDbg property
        /// <summary>
        [Name(nameof(LinuxDebuggerSettings.AutoInstallVsDbg))]
        public bool AutoInstallVsDbg
        {
            get => m_AutoInstallVsDbg;
            set => this.SetProperty(ref m_AutoInstallVsDbg, value);
        }

        #endregion AutoInstallVsDbg property

        #region ProjectDirectory property

        public string ProjectDirectoryLabel => "Projects' Directory";
        private string m_ProjectDirectory;
        /// <summary>
        /// ProjectDirectory property
        /// <summary>
        [Name(nameof(LinuxDebuggerSettings.RemoteProjectDirectory))]
        public string ProjectDirectory
        {
            get => m_ProjectDirectory;
            set => this.SetProperty(ref m_ProjectDirectory, value);
        }

        #endregion ProjectDirectory property

        #region DotnetExePath property

        private string m_DotnetExePath;
        /// <summary>
        /// DotnetExePath property
        /// <summary>
        [Name(nameof(LinuxDebuggerSettings.RemoteDotnetPath))]
        public string DotnetExePath
        {
            get => m_DotnetExePath;
            set => this.SetProperty(ref m_DotnetExePath, value);
        }

        #endregion DotnetExePath property

        internal void Update(LinuxDebuggerSettings settings)
        {
            settings.UseSsh = this.AdapterMode == AdapterMode.Ssh;
            var myType = this.GetType();
            foreach(var prop in myType.GetProperties(flags))
            {
                var attr = prop.GetCustomAttribute<NameAttribute>();
                string name = attr?.Name ?? prop.Name;
                var oprop = typeof(LinuxDebuggerSettings).GetProperty(name, flags);
                if (oprop is not null)
                {
                    var val = prop.GetValue(this, null);
                    oprop.SetValue(settings, val);
                }
            }
        }

        internal void Initialize(LinuxDebuggerSettings settings)
        {
            this.m_AdapterMode = settings.UseSsh ? AdapterMode.Ssh : AdapterMode.PLink;
            foreach(var prop in this.GetType().GetProperties(flags))
            {
                var attr = prop.GetCustomAttribute<NameAttribute>();
                if (attr is null) continue;

                var oprop = typeof(LinuxDebuggerSettings).GetProperty(attr.Name, flags);
                Debug.Assert(oprop is not null);
                if (oprop is not null)
                {
                    var val = oprop.GetValue(settings, null);
                    prop.SetValue(this, val);
                }
            }
        }
    }
}
