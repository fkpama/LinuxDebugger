using liblinux.IO;
using liblinux.Shell.Commanding;

namespace LinuxDebugger.VisualStudio
{
    public interface IVsSshClient
    {
        string Hostname { get; }
        string Username { get; }
        bool IsConnected { get; }

        SshConnectionInfo ConnectionInfo { get; }

        Task UploadAsync(SimplePathMapping[] mappings,
                         CancellationToken cancellationToken);
        Task UploadAsync(string localPath,
                         string remotePath,
                         CancellationToken cancellationToken);
        ValueTask<bool> ExecutableExistsAsync(string v, CancellationToken cancellationToken);
        ValueTask<bool> DirectoryExistsAsync(string v, CancellationToken cancellationToken);
        ValueTask<bool> FileExistsAsync(string v, CancellationToken cancellationToken);
        string Expand(string remteworkingDir);
        ValueTask<string> ExpandAsync(string remteworkingDir, CancellationToken cancellationToken);
        ValueTask<int> RunCommandAsync(string cmd, TimeSpan? timeSpan, CancellationToken cancellationToken);
        ValueTask ConnectAsync(CancellationToken cancellationToken);
        Task CreateDirectoryAsync(string path, CancellationToken cancellationToken);
        Task<FileSystemInfo> DownloadAsync(string source, string target, CancellationToken cancellationToken);
    }
    internal sealed class VsSshClient : IVsSshClient
    {
        internal static TimeSpan COMMAND_TIMEOUT = TimeSpan.FromSeconds(10.0);
        private readonly SshConnectionInfo infos;
        private readonly IRemoteSystemFactory factory;
        private RemoteSystem? remoteSystem;
        private readonly AsyncLazy<string> _homeDirLazy;
        private readonly JoinableTaskFactory taskFactory;

        public string Username => this.infos.Infos.UserName;
        public bool IsConnected => this.remoteSystem?.IsConnected ?? false;
        public string Hostname => this.infos.Infos.HostName;

        public SshConnectionInfo ConnectionInfo
        {
            get => this.infos;
        }

        public string HomeDirectory
        {
            get => this._homeDirLazy.GetValue(VsShellUtilities.ShutdownToken);
        }

        internal VsSshClient(SshConnectionInfo infos, JoinableTaskFactory taskFactory)
        {
            this._homeDirLazy = new(async () =>
            {
                var cancellationToken  = VsShellUtilities.ShutdownToken;
                var fs = await this.GetRemoteFileSystemAsync(cancellationToken)
                .ConfigureAwait(false);
                return fs.GetFullPath("~");
            }, taskFactory);
            this.infos = infos;
            this.factory = SshRemoteSystemFactory.Create(infos.Infos);
            this.taskFactory = taskFactory;
            this.reset();
        }

        private void reset()
        {
            //await this.GetRemoteSystemAsync(cancellationToken).ConfigureAwait(false);
            //this.remoteSystemLazy = new(() =>
            //{
            //    try
            //    {
            //        this.remoteSystem = new RemoteSystem(infos);
            //        return Task.FromResult(this.remoteSystem);
            //    }
            //    catch (RemoteConnectionTimeoutException ex)
            //    {
            //        var msg = $"Timeout connecting to host {infos.HostName}";
            //        throw new TimeoutException(msg, ex);
            //    }
            //}, this.taskFactory);
            this.remoteSystem = null;
        }

        public Task CreateDirectoryAsync(string directory, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var fs = await this.GetRemoteFileSystemAsync(cancellationToken).ConfigureAwait(false);
                var result = fs.CreateDirectory(directory);
            }, cancellationToken);
        }
        public Task UploadFileAsync(string path,
                                          string remoteFileName,
                                          CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var fs = await this.GetRemoteFileSystemAsync(cancellationToken)
                .ConfigureAwait(false);
                var result = fs.UploadFile(path, remoteFileName);
            }, cancellationToken);
        }

        public async Task UploadAsync(SimplePathMapping[] mappings, CancellationToken cancellationToken)
        {
            const int bufSize = 8092;
            var fs = await this.GetRemoteFileSystemAsync(cancellationToken)
                    .ConfigureAwait(false);
            foreach (var mapping in mappings)
            {
                Debug.Assert(File.Exists(mapping.Source) || Directory.Exists(mapping.Source));
                var localPath = mapping.Source;
                var remotePath = this.expand(mapping.Target);
                if (Directory.Exists(localPath))
                {
                    _ = await fs.UploadDirectoryAsync(localPath, remotePath, bufSize)
                    .ConfigureAwait(false);
                }
                else if (File.Exists(localPath))
                {
                    fs.CreateDirectories(LinuxPath.GetDirectoryName(remotePath));
                    var rf = await fs
                        .UploadFileAsync(localPath, remotePath, bufSize)
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new System.IO.FileNotFoundException(localPath);
                }

            }
        }
        public async Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
        {
            remotePath = await this.ExpandAsync(remotePath, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await doUploadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            when ((ex is liblinux.IO.IOException || ex is System.IO.IOException)
            && string.Equals(ex.InnerException?.GetType().Name, "SshConnectionException"))
            {
                this.reset();
                await doUploadAsync(cancellationToken).ConfigureAwait(false);
            }

            async Task doUploadAsync(CancellationToken cancellationToken)
            {
                var fs = await this.GetRemoteFileSystemAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (Directory.Exists(localPath))
                {
                    _ = await fs.UploadDirectoryAsync(localPath, remotePath, 8092)
                    .ConfigureAwait(false);
                }
                else if (File.Exists(localPath))
                {
                    fs.CreateDirectories(LinuxPath.GetDirectoryName(remotePath));
                    var rf = await fs
                        .UploadFileAsync(localPath, remotePath, 8092)
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new System.IO.FileNotFoundException(localPath);
                }
            }
        }

        public ValueTask<string> ExpandAsync(string remotePath, CancellationToken cancellationToken)
        {
            if (this.IsConnected)
            {
                return new(this.expand(remotePath));
            }
            return new(Task.Run(async () =>
            {
                _ = await this.GetRemoteFileSystemAsync(cancellationToken)
                .ConfigureAwait(false);
                return this.expand(remotePath);
            }, cancellationToken));

        }
        string IVsSshClient.Expand(string remteworkingDir)
            => this.taskFactory.Run(async () =>
            {
                return await this
                .ExpandAsync(remteworkingDir, VsShellUtilities.ShutdownToken)
                .ConfigureAwait(false);
            });

        private string expand(string remotePath)
        {
            remotePath = remotePath.Trim();
            if (remotePath.StartsWith("~"))
            {
                remotePath = PathUtils
                    .CombineUnixPaths(this.HomeDirectory, remotePath.Substring(1));
            }
            return remotePath;
        }

        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken)
        {
            return new(Task.Run(async () =>
            {
                var remoteSystem = await this.GetRemoteSystemAsync(cancellationToken)
                .ConfigureAwait(false);
                var additionalUnescapedChars = new char[1] { '$' };
                path = this.expand(path);
                path = PathUtils.EscapeStringForUnixShell(path, additionalUnescapedChars);
                var cmd = remoteSystem.Shell.ExecuteCommand($"test -f {path}", COMMAND_TIMEOUT);
                return cmd.ExitCode == 0;
            }, cancellationToken));
        }

        public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken)
        {
            return new(Task.Run(async () =>
            {
                var remoteSystem = await this
                .GetRemoteSystemAsync(cancellationToken)
                .ConfigureAwait(false);
                var additionalUnescapedChars = new char[1] { '$' };
                path = PathUtils.EscapeStringForUnixShell(path, additionalUnescapedChars);
                var cmd = remoteSystem.Shell.ExecuteCommand($"test -d {path}", COMMAND_TIMEOUT);
                return cmd.ExitCode == 0;
            }, cancellationToken));
        }

        public ValueTask<bool> ExecutableExistsAsync(string v, CancellationToken cancellationToken)
        {
            return new(Task.Run(async () =>
            {
                var remoteSystem = await this.GetRemoteSystemAsync (cancellationToken)
                .ConfigureAwait(false);
                return remoteSystem.ExecutableExists(v);
            }, cancellationToken).WithTimeout(COMMAND_TIMEOUT));
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken)
        {
            if (this.IsConnected)
                return default;

            return new(Task.Run(async () =>
            {
                _ = await this.GetRemoteSystemAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken));
        }

        public ValueTask<int> RunCommandAsync(string cmdText, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            TimeSpan ts;
            if (!timeout.HasValue)
            {
                ts = COMMAND_TIMEOUT;
            }
            else if (timeout.Value != Timeout.InfiniteTimeSpan)
            {
                ts = timeout.Value;
            }
            else
            {
                ts = default;
            }
            var task = Task.Run(async () =>
            {
                await TaskScheduler.Default;
                var remoteSystem = await this.GetRemoteSystemAsync(cancellationToken).ConfigureAwait(false);
                var cmd = remoteSystem.Shell.ExecuteCommand(cmdText, timeout);
                if (cmd.ExitCode != ExitCodes.CommandSucceeded)
                    throw new ExitCodeException($"Command '{cmd.CommandText}' exited with code {cmd.ExitCode}");
                return cmd.ExitCode;
            }, cancellationToken);
            if (ts != default)
                task = task.WithTimeout(ts);
            return new(task.WithCancellation(cancellationToken));
        }

        internal async ValueTask<IRemoteFileSystem> GetRemoteFileSystemAsync(CancellationToken cancellationToken)
        {
            var system = await this.factory
                .CreateRemoteSystemAsync(cancellationToken)
                .ConfigureAwait(false);
            //var system = await this.GetRemoteSystemAsync(cancellationToken).ConfigureAwait(false);
            return system.FileSystem;
        }
        internal ValueTask<RemoteSystem> GetRemoteSystemAsync(CancellationToken cancellationToken)
        {
            if (this.IsConnected)
            {
                Assumes.NotNull(this.remoteSystem);
                return new(this.remoteSystem);
            }

            return new(Task.Run(async () =>
            {
                var system = this.remoteSystem;
                if (system is null)
                {
                    var isystem = await this.factory
                    .CreateRemoteSystemAsync(cancellationToken)
                    .ConfigureAwait(false);
                    this.remoteSystem = system = (RemoteSystem)isystem;
                }
                if (!system.IsConnected)
                    await system.ConnectAsync(this.infos.Infos, cancellationToken)
                    .ConfigureAwait(false);
                this.remoteSystem = system;
                return system;
            }, cancellationToken));
        }

        public async Task<FileSystemInfo> DownloadAsync(string source,
                                  string target,
                                  CancellationToken cancellationToken)
        {
            var fs = await this
                .GetRemoteFileSystemAsync(cancellationToken)
                .ConfigureAwait(false);
            var src = await this.ExpandAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return await fs.DownloadFileAsync(src, target, 256, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
