namespace LinuxDebugger.ProjectSystem;

internal static class Constants
{
    internal const string CommandName = "RemoteUnix";
    internal const string NetCoreAppTfm = ".NETCoreApp";

    public const string ConnectionManagerPackageIdString = "5669A1E2-09B4-405D-AE52-18437C23D7EA";
    public const string ConnectionManagerServiceIdString = ConnectionManagerPackageIdString;
    public static Guid ConnectionManagerServiceId = new(ConnectionManagerServiceIdString);
    public static Guid ConnectionManagerPackageId = new(ConnectionManagerPackageIdString);

    public const string ConnectionManagerOptionPageGuid = "{6039ABF8-7F89-4A66-AF35-14AE0E81F5B4}";

    internal static class MSBuild
    {
        internal const string GetCopyToOutputDirectoryItemsTargetName = "GetCopyToOutputDirectoryItems";
        public static class ProjectProperties
        {
            internal static string MSBuildProjectDirectory = nameof(MSBuildProjectDirectory);
        }

        public static class OutputGroups
        {
            internal const string ReferenceCopyLocalPaths = "ReferenceCopyLocalPathsOutputGroup";
        }
    }

    public static class Capabilities
    {
        internal const string DotNetCoreWeb = nameof(DotNetCoreWeb);
        internal const string RemoteLinuxCapability = "RemoteLinux";
        internal const string DotNetCoreRazor = nameof(DotNetCoreRazor);
        internal const string AspNetCore = nameof(AspNetCore);
        internal const string LaunchProfileCapability = $"{RemoteLinuxCapability} & {ProjectCapabilities.LaunchProfiles}";
        internal const string RemoteWebProject = $"{RemoteLinuxCapability} & {DotNetCoreWeb}";
        internal const string RemoteRazorWebProject = $"{RemoteLinuxCapability} & {DotNetCoreRazor}";
    }

    internal static class EditorConstants
    {
        internal const string ModeMetadataKey = "Mode";
        internal const string DownloadMode = "Download";
    }

    internal static class ProjectProperties
    {
        internal const string TargetFrameworkMoniker = nameof(TargetFrameworkMoniker);
    }
    internal static class ProfileParams
    {
        internal const string CommandPre = "preCommand",
            CommandPost                  = "postCommand",
            PostExecDownloadFile         = "postExecDownloadFiles",
            AdditionalDeploymentFiles    = "additionalDeploymentFiles",
            EnvironementVariables        = "environmentVariables",
            CommandLineArguments         = "commandLineArgs",
            ExePath                      = "executablePath",
            PrivateKeyFile               = "privateKeyFile",
            DeploymentDir                = "deploymentDir",
            DisableRedirections          = "disableRedirections",
            DotnetPath                   = "dotnetPath",
            WorkingDirectory             = "workingDirectory",
            ConnectionId                 = "connectionId",
            LaunchBrowser                = "launchBrowser",
            BrowserUrl                   = "applicationUrl";

        internal const string DefaultConnectionName = "<default>";
        internal const string AddConnectionValue = "-99";
    }
}
