using liblinux.IO;
using LinuxDebugger.VisualStudio.Logging;
using LinuxDebugger.VisualStudio.Serialization;
using LinuxDebugger.VisualStudio.Settings;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LinuxDebugger.VisualStudio
{
    public readonly struct DebugTargetInfo
    {
        public DebugLaunchSettings Settings { get; }
        public IVsSshClient SshClient { get; }
        public DebugTargetInfo(DebugLaunchSettings settings,
                               IVsSshClient sshClient)
        {
            this.Settings = settings;
            this.SshClient = sshClient;
        }
    }

    public readonly struct DebugInfoOwner : IDisposable
    {
        public VsDebugTargetInfo4 TargetInfo { get; }

        public DebugInfoOwner(VsDebugTargetInfo4 TargetInfo)
        {
            this.TargetInfo = TargetInfo;
        }

        public void Dispose()
        {
        }
    }
    public class DebugLauncher
    {
        private readonly IVsSshClient client;
        private readonly ILogger log;
        private readonly IVsUIShell shell;
        private string? vsDbgBasePath;
        private static readonly JsonSerializerSettings JsonSerializerOptions = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        internal IVsUIShell Shell
        {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            get => this.shell
                ?? ServiceProvider
                .GlobalProvider
                .GetService<SVsUIShell, IVsUIShell>();
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
        }

        public static string GetVsDbgBasePath(LinuxDebuggerSettings settings)
        {
            var path = settings.VsDbgDirectory;
            path = string.IsNullOrWhiteSpace(path)
                ? LinuxConstants.DefaultVsdbgBasePath
                : path;
            Assumes.NotNull(path);
            var vsDbgBasePath = LinuxPath.Combine(path, LinuxConstants.VS2022);
            Assumes.NotNull(vsDbgBasePath);
            return vsDbgBasePath;
        }

        public string VsDbgBasePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.vsDbgBasePath))
                {
                    this.vsDbgBasePath = GetVsDbgBasePath(this.Settings);
                }
                Assumes.NotNull(this.vsDbgBasePath);
                return this.vsDbgBasePath;
            }
            set
            {
                Assumes.NotNull(value);
                this.vsDbgBasePath = value;
            }
        }

        public string VsDbgFullPath => LinuxPath.Combine(this.VsDbgBasePath, LinuxConstants.AppVSDbg);
        public string RemoteDotnetPath => this.Settings.RemoteDotnetPath;
        public LinuxDebuggerSettings Settings { get; }

        public DebugLauncher(IVsSshClient client,
                             LinuxDebuggerSettings settings,
                             ILogger? logger = null)
        {
            this.log = logger ?? Logger.None;
            this.shell = null!;
            this.Settings = settings;
            this.client = client;
        }
        public DebugLauncher(IVsSshClient client,
                             LinuxDebuggerSettings settings,
                             IVsUIShell shell,
                             string targetPath,
                             string workingDir,
                             ILogger? logger = null)
            : this(client, settings, logger)
        {
            this.shell = shell;
        }

        public ValueTask<DebugTargetInfo> QueryDebugTargetsAsync(
            DebugLaunchOptions options,
            string targetPath,
            string? commandLineArgs,
            string? workingDirectory,
            string? tty,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken)
        {
            string[]? args;
            if (string.IsNullOrWhiteSpace(commandLineArgs))
            {
                args = null;
            }
            else
            {
                Assumes.NotNull(commandLineArgs);
                args = LinuxPath.ParseCommandLine(commandLineArgs);
            }

            var json = this.GenerateLaunchJson(targetPath,
                                               args,
                                               workingDirectory,
                                               environment: environment,
                                               tty: tty,
                                               stopAtEntry: options.HasFlag(DebugLaunchOptions.StopAtEntryPoint));
            var ret = new DebugLaunchSettings(options)
            {
                LaunchOperation = DebugLaunchOperation.CreateProcess,
                Executable = this.RemoteDotnetPath,
                Arguments = commandLineArgs,
                CurrentDirectory = workingDirectory,
                LaunchOptions = options,
                Options = json,
                //StandardOutputHandle = readPipe.DangerousGetHandle(),
                //StandardInputHandle = readHandle.DangerousGetHandle(),
                LaunchDebugEngineGuid = LinuxConstants.DebugAdapterHostEngineGuid,
            };
            if (environment is not null)
            {
                foreach (var kvp in environment)
                    ret.Environment.Add(kvp.Key, kvp.Value);
            }

            return new(new DebugTargetInfo(ret, this.client));
        }

        /// <summary>
        /// <see href="https://code.visualstudio.com/docs/cpp/launch-json-reference">Doc</see>
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="commandLineArgs"></param>
        /// <param name="remteworkingDir"></param>
        /// <returns></returns>
        internal string GenerateLaunchJson(string targetPath,
            string[]? commandLineArgs = null,
            string? remteworkingDir = null,
            IReadOnlyDictionary<string, string>? environment = null,
            string? tty = null,
            bool stopAtEntry = false)
        {
            if (!string.IsNullOrWhiteSpace(remteworkingDir))
            {
                Assumes.NotNull(remteworkingDir);
                remteworkingDir = this.client.Expand(remteworkingDir);
            }

            //var str = BaseLaunchOptions.Serialize(options);
            //return str;
            var lst = new List<string>
            {
                targetPath
            };
            if (commandLineArgs is not null)
                lst.AddRange(commandLineArgs);

            if (!string.IsNullOrWhiteSpace(tty))
            {
                lst.AddRange(new[] { ">", tty!, "<", tty!, "2>&1" });
            }

            var adapterPath = this.getAdapterPath(tty, out var args);
            var model = new Configuration(adapterPath, args)
            {
                Program = this.RemoteDotnetPath,
                Adapter = adapterPath,
                Cwd = remteworkingDir,
                Request = "launch",
                Env = environment,
                Args = lst,
                StopAtEntry = stopAtEntry,
                Type = "coreclr",
                Name = ".NET Core Launch",
                AdapterArgs = args
            };

            var json = JsonConvert.SerializeObject(model, Formatting.Indented, JsonSerializerOptions);
            this.log.LogVerbose($"Genrated launch.json:\n{json}");

            return json;
        }

        private string getAdapterPath(string? tty, out string args)
        {
            var adapter = "ssh.exe";

            var logPath = this.client.Expand("~/vsdbg.log");
            var connInfos = $"{this.client.Username}@{this.client.Hostname}";
            var fullPath = this.client.Expand(this.VsDbgFullPath);
            var remotePath = $"\"{PathUtils.EscapeStringForUnixShell(fullPath)}\"";
            if (!string.IsNullOrWhiteSpace(tty))
            {
                remotePath += $" --tty={PathUtils.EscapeStringForUnixShell(tty)}";
            }
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                remotePath += $" --engineLogging={PathUtils.EscapeStringForUnixShell(logPath)}";
            }

            args = $"{connInfos} {remotePath}";
            return adapter;
        }
    }
}
