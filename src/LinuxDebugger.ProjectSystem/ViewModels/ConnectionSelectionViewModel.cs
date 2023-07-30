using System.Collections.ObjectModel;

namespace LinuxDebugger.ProjectSystem.ViewModels
{
    public class ConnectionViewModel : ViewModel
    {
        public SshConnectionInfo? ConnectionInfo { get; }


        #region ConnectionId property

        private string m_ConnectionId;
        /// <summary>
        /// ConnectionId property
        /// <summary>
        public string ConnectionId
        {
            get => m_ConnectionId;
            set => this.SetProperty(ref m_ConnectionId, value);
        }

        #endregion ConnectionId property

        #region Hostname property

        private string m_Hostname;
        /// <summary>
        /// Hostname property
        /// <summary>
        public string Hostname
        {
            get => m_Hostname;
            set => this.SetProperty(ref m_Hostname, value);
        }

        #endregion Hostname property

        #region IsChecked property

        private bool m_IsChecked;
        public event EventHandler? IsCheckedChanged;
        /// <summary>
        /// IsChecked property
        /// <summary>
        public bool IsChecked
        {
            get => m_IsChecked;
            set
            {
                if (this.SetProperty(ref m_IsChecked, value))
                    this.IsCheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion IsChecked property

        public ConnectionViewModel(SshConnectionInfo sshConnectionInfo)
            : this(sshConnectionInfo.Id, sshConnectionInfo.Hostname)
        {
            this.ConnectionInfo = sshConnectionInfo;
        }
        public ConnectionViewModel(string id, string hostname)
        {
            this.m_ConnectionId = id;
            this.m_Hostname = hostname;
        }

    }
    public class ConnectionSelectionViewModel : ViewModel
    {
        public ObservableCollection<ConnectionViewModel> Connections { get; }


        #region CanClose property

        private bool m_CanClose;
        /// <summary>
        /// CanClose property
        /// <summary>
        public bool CanClose
        {
            get => m_CanClose;
            set => this.SetProperty(ref m_CanClose, value);
        }

        #endregion CanClose property

        public ConnectionSelectionViewModel(IEnumerable<SshConnectionInfo> infos)
        {
            this.Connections = new(infos.Select(x =>
            {
                var vm = new ConnectionViewModel(x);
                vm.IsCheckedChanged += (o, e) =>
                {
                    this.CanClose = this.Connections.Any(x => x.IsChecked);
                };
                return vm;
            }));
        }
    }
}
