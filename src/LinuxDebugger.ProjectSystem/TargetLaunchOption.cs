namespace LinuxDebugger.ProjectSystem
{
    [Flags]
    internal enum TargetLaunchOption
    {
        None             = 0,
        InstallVsDbgShim = 1 << 0,
        Deploy           = 1 << 1,
    }
}
