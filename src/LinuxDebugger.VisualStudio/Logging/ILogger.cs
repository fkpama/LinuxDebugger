namespace LinuxDebugger.VisualStudio.Logging
{
    public enum LogLevel
    {
        LogAlways   = 0,
        Verbose     = 5,
        Information = 10,
        Warning     = 15,
        Error       = 20,
    }
    public interface ILogger
    {
        void LogVerbose(string message);
        void LogInformation(string message);
        void LogWarning(string v);
        void LogError(string msg);
        void LogError(Exception exception, string msg);
    }
}
