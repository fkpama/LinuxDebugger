namespace LinuxDebugger.VisualStudio
{
    public sealed class SshConnectionInfo
    {
        private readonly liblinux.Remoting.ConnectionInfo info;
        private ConnectionInfo? info2;

        internal SshConnectionInfo(liblinux.Remoting.ConnectionInfo info,
                                 ConnectionInfo? info2) : this(info)
        {
            this.info2 = info2;
        }

        internal SshConnectionInfo(liblinux.Remoting.ConnectionInfo info)
        {
            this.info = info;
        }

        internal int InternalId => this.info.Id;
        public string Id => this.info.Id.ToString();
        public string Hostname => this.info.Host;
        public AuthenticationMethod ConnectionMethod
            => (AuthenticationMethod)this.info.AuthenticationMode;

        public ConnectionInfo Infos
        {
            get => this.info2 ??= Utils.GetInfos(this.info.Id);
        }

        internal IRemoteSystem CreateRemoteSystem()
        {
            return new RemoteSystem(this.Infos);
        }
    }
}