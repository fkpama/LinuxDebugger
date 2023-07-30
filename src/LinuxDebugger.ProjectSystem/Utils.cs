using System.Diagnostics;
using LinuxDebugger.VisualStudio;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem
{
    internal static class Utils
    {
        internal static void ComputeDownloads(PathMapping[] downloads, DebugSessionInfo session)
        {
            for (var i = 0; i < downloads.Length; i++)
            {
                var mapping = downloads[i];
                string path;
                if (mapping.Target.IsPresent()
                    && Path.IsPathRooted(mapping.Target))
                {
                    continue;
                }
                else if (mapping.Target.IsMissing())
                {
                    var fname = LinuxPath.GetFilename(mapping.Source);
                    path = Path.Combine(session.LocalOutDir, fname);
                }
                else if (!Path.IsPathRooted(mapping.Target))
                {
                    path = Path.Combine(session.LocalOutDir, mapping.Target);
                }
                else
                {
                    throw new NotImplementedException();
                }

                downloads[i] = mapping.WithPath(path);
            }
        }

        internal static void ComputeUploads(PathMapping[] uploads, IVsSshClient client, DebugSessionInfo session)
        {
            for (var i = 0; i < uploads.Length; i++)
            {
                var mapping = uploads[i];
                string path;
                if (mapping.Target.IsPresent()
                    && LinuxPath.IsRooted(mapping.Target!))
                {
                    continue;
                }
                else
                {
                    var target = mapping.Target;
                    if (target.IsMissing())
                    {
                        target = Path.GetFileName(mapping.Source);
                    }
                    else
                    {
                        target = client.Expand(target!);
                    }
                    path = LinuxPath.IsRooted(target)
                        ? target
                        : LinuxPath.Combine(session.WorkingDir, target);
                    uploads[i] = mapping.WithPath(path);
                }
            }
        }

        internal static string GetRuleFilePath(string v)
            => GetVsixFilePath($"Rules\\{v.Trim()}");
        internal static string GetVsixFilePath(string v)
        {
            var path = typeof(LaunchProvider).Assembly.Location;
            path = Path.GetDirectoryName(path);
            var ret = Path.Combine(path, v);
            Debug.Assert(File.Exists(ret));
            return ret;
        }

        internal static async Task UpdateProfilesAsync(this ILaunchSettingsProvider3 provider, CancellationToken cancellationToken)
        {
            List<Task> tasks = new();
            foreach(var profile in (IEnumerable<ILaunchProfile?>?)provider.CurrentSnapshot?.Profiles
                 ?? new[] { provider.ActiveProfile })
            {
                if (profile?.Name is not null && profile.IsRemoteLinuxProfile())
                {
                    tasks.Add(provider.AddOrUpdateProfileAsync(profile, false)
                        .WithCancellation(cancellationToken));
                    
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

    }
}