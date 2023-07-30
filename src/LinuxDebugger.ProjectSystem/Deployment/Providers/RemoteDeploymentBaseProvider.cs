namespace LinuxDebugger.ProjectSystem.Deployment.Providers
{
    public abstract class RemoteDeploymentBaseProvider : IRemoteDeploymentProvider
    {
        public virtual ValueTask<SimplePathMapping[]> GetAdditionalFileAsync(IDeploymentSession session, CancellationToken cancellationToken)
        {
            return default;
        }

        public virtual ValueTask ProcessAsync(IDeploymentSession session, CancellationToken cancellationToken)
        {
            return default;
        }
    }
}
