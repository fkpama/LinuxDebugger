namespace LinuxDebugger.VisualStudio
{
    public static class LinuxConstants
    {
        public static readonly Guid MIDebugEngineGuid = new("{ea6637c6-17df-45b5-a183-0951c54243bc}");
        public static readonly Guid DAP_MIDebugEngineGuid = new("{F328E937-B412-44AD-905D-90B913C3D098}");
        public static readonly Guid DebugAdapterHostEngineGuid = new("{541B8A8A-6081-4506-9F0A-1CE771DEBC04}");
        public static readonly Guid CrossPlatformOutputWindowPaneGuid = new("{33BE791C-7549-4979-8407-A2B1C79C2B62}");
        internal const string CrossPlatformOutputWindowPaneLabel = "Cross Platform Logging";
        /// <summary>Filename of Visual Studio Debugger.</summary>
        public const string AppVSDbg = "vsdbg";

        public const string DefaultDotNetPath = "dotnet";
        public const string VS2022 = "vs2022";
        public const string DefaultVsdbgBasePath = "~/.vs-debugger";
        public const string LaunchJson = "launch.json";

        internal const string DebugAdapterHost = "DebugAdapterHost.Launch";
        internal const string DebugAdapterHostLogging = "DebugAdapterHost.Logging";
        internal const string DebugAdapterHostLoggingOnOutputWindow = "/On /OutputWindow";
        internal const string DebugAdapterLaunchJson = "/LaunchJson:";

        internal const string PipeLaunchOptionsNamespace = "http://schemas.microsoft.com/vstudio/MDDDebuggerOptions/2014";
        private static string? s_plinkExePath;
        internal static string PlinkExePath
            => s_plinkExePath ??= Path.Combine(
                Path.GetDirectoryName(typeof(LinuxConstants).Assembly.Location),
                "bin",
                "plink.exe");

    }
}
