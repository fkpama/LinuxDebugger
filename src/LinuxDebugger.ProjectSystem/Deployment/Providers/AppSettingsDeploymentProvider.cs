namespace LinuxDebugger.ProjectSystem.Deployment.Providers
{
    //[Export(typeof(IRemoteDeploymentProvider))]
    //[AppliesTo(Constants.Capabilities.RemoteWebProject)]
    //[ExportMetadata("Name", "AppSettings")]
    //sealed class AppSettingsDeploymentProvider : RemoteDeploymentBaseProvider
    //{
    //    public override async ValueTask<SimplePathMapping[]> GetAdditionalFileAsync(IDeploymentSession session, CancellationToken cancellationToken)
    //    {
    //        var items = await session
    //            .GetCopyToOutputDirectoryItemsAsync(cancellationToken)
    //            .ConfigureAwait(false);

    //        var settings = items
    //            .Select(x => x.GetFullPath())
    //            .Where(x => x!.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase))
    //            .Select(x => new SimplePathMapping(x!))
    //            .ToArray();
    //        return settings;
    //    }
    //}
}
