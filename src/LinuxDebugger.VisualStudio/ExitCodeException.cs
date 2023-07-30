using System.Runtime.Serialization;

namespace LinuxDebugger.VisualStudio
{
    [Serializable]
    public sealed class ExitCodeException : Exception
    {
        public int ExitCode { get; } = -1;
        public ExitCodeException() { }
        public ExitCodeException(int exitCode)
        {
            this.ExitCode = exitCode;
        }

        public ExitCodeException(string message) : base(message)
        {
        }

        public ExitCodeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ExitCodeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}