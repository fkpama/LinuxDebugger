
namespace LinuxDebugger.VisualStudio
{
    public sealed class ConnectEventArgs : EventArgs
    {
        public string? Id { get; internal set; }
        public string? Hostname { get; internal set; }
        public AuthenticationMethod AuthenticationMethod { get; internal set; }
        public ConnectionChangedOperation Operation { get; }

        internal ConnectEventArgs(int id, ConnectionChangedOperation operation)
        {
            this.Id = id > 0 ? id.ToString() : null;
            this.Operation = operation;
        }
    }
}