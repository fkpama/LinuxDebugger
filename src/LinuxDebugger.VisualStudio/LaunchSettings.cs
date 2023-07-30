namespace LinuxDebugger.VisualStudio
{
    public class LaunchSettings
    {
        public string RemoteDotnetPath { get; set; } = LinuxConstants.DefaultDotNetPath;
        public string RemoteVsDbgBasePath { get; set; } = LinuxConstants.DefaultVsdbgBasePath;
        //public string RemoteVsDbgFullPath { get; set; }
    }
}
