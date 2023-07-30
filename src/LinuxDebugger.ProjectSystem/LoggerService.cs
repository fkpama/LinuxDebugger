using LinuxDebugger.ProjectSystem.Commands;
using LinuxDebugger.VisualStudio;
using LinuxDebugger.VisualStudio.Logging;

namespace LinuxDebugger.ProjectSystem
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class LoggerService
    {
        private readonly IProjectThreadingService threadingService;
        private ILogger? log;

        [ImportingConstructor]
        public LoggerService(IProjectThreadingService threadingService)
        {
            this.threadingService = threadingService;
        }

        public ILogger GetLogger<T>(CancellationToken  cancellation = default)
        {

            return this.threadingService
                .ExecuteSynchronously(() => this.GetLoggerAsync<T>(cancellation).AsTask());
        }

        internal ValueTask<ILogger> GetLoggerAsync<T>(CancellationToken cancellationToken)
        {
            if (this.log is not null)
                return new(this.log);
            return new(Task.Run(async ()=>
            {
                await this.threadingService.SwitchToUIThread();
                this.log = Logger
                        .OutputWindow(LinuxConstants.CrossPlatformOutputWindowPaneGuid);
                return this.log;
            }, cancellationToken));
        }
    }
}
