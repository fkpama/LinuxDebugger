using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using LinuxDebugger.ProjectSystem.Serialization;
using Microsoft.Build.Framework;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem;
internal static class Extensions
{
    internal static OrderPrecedenceImportCollection<T> ToImportCollection<T>(this IEnumerable<Lazy<T, IOrderPrecedenceMetadataView>> items, ConfiguredProject configuredProject)
    {
        OrderPrecedenceImportCollection<T> col = new(ImportOrderPrecedenceComparer.PreferenceOrder.PreferredComesFirst, configuredProject);
        foreach (var item in items)
            col.Add(item);
        return col;
    }

    static IEnumerable<PathMapping> getPathMappings(this ILaunchProfile profile, string key)
    {
        if (profile.OtherSettings is null
            || !profile.OtherSettings.TryGetValue(key, out var value))
            return Enumerable.Empty<PathMapping>();

        var lst = new List<PathMapping>();
        if (value is IReadOnlyDictionary<string, object> dict)
        {
            return dict
            .Select(x => new PathMapping(x.Key, x.Value is null
            ? new()
            : DownloadMetadata.FromObject(x.Value)));
        }
        else if (value is IReadOnlyDictionary<string, string> dict2)
        {
            return dict2
            .Select(x => new PathMapping(x.Key, new DownloadMetadata { Path = x.Value }));
        }
        else
        {
            throw new NotImplementedException();
        }
        
    }
    internal static IEnumerable<PathMapping> GetUploadPathMappings(this ILaunchProfile profile)
        => profile.getPathMappings(Constants.ProfileParams.AdditionalDeploymentFiles);
    internal static IEnumerable<PathMapping> GetDownloadPathMappings(this ILaunchProfile profile)
        => profile.getPathMappings(Constants.ProfileParams.PostExecDownloadFile);

    internal static bool GetRedirectionsDisabled(this ILaunchProfile profile)
    {
        bool disabled = false;

        if(profile.OtherSettings?
            .TryGetValue(Constants.ProfileParams.DisableRedirections, out var str) == true)
        {
            if (str is bool b)
                disabled = b;
            else if (str is not null)
            {
                disabled = Convert.ToBoolean(str);
            }
        }
        return disabled;
    }
    internal static string? GetDeployDirectory(this ILaunchProfile profile)
    {
        if (profile.OtherSettings
            ?.TryGetValue(Constants.ProfileParams.DeploymentDir, out var deploymentDirO) == true)
        {
            return (string)deploymentDirO;
        }
        else
        {
            return null;
        }
    }
    internal static void SetConnectionId(this IWritableLaunchProfile profile, string id)
    {
        Assumes.Present(profile.OtherSettings);
        profile.OtherSettings[Constants.ProfileParams.ConnectionId] = id;
    }

    internal static bool IsRemoteLinuxProfile(this ILaunchProfile? profile)
        => string.Equals(profile?.CommandName, Constants.CommandName, StringComparison.OrdinalIgnoreCase);

    internal static int? GetConnectionId(this ILaunchProfile profile)
    {
        if (profile.OtherSettings is null
            || !profile.OtherSettings.TryGetValue(Constants.ProfileParams.ConnectionId, out var id))
            return null;
        return (int)Convert.ChangeType(id, typeof(int));
    }

    internal static bool LaunchBrowser(this ILaunchProfile profile)
    {
        if (profile.OtherSettings is null
            || !profile.OtherSettings.TryGetValue(Constants.ProfileParams.LaunchBrowser, out var id))
            return false;
        return Convert.ToBoolean(id);
    }

    internal static bool IsPresent(this string? str)
        => !string.IsNullOrWhiteSpace(str);
    internal static bool IsMissing(this string? str)
        => string.IsNullOrWhiteSpace(str);

    internal static bool IsCriticalException(this Exception e)
    {
      switch (e)
      {
        case StackOverflowException _:
        case OutOfMemoryException _:
        case ThreadAbortException _:
        case AccessViolationException _:
          return true;
        default:
          return false;
      }
    }

    [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Exception Rethrow(this Exception ex)
    {
        Requires.NotNull(ex, nameof(ex));
        ExceptionDispatchInfo.Capture(ex).Throw();
        throw Assumes.NotReachable();
    }

    [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RethrowIfCritical(this Exception ex)
    {
        if (!ex.IsCriticalException())
            return;
        ex.Rethrow();
    }

    internal static IEnumerable<T> GetVisualChildren<T>(this DependencyObject @object)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(@object);
        for(var i = 0; i <  count; i++)
        {
            var child = VisualTreeHelper.GetChild(@object, i);
            if (child is T t)
                yield return t;
        }
    }

    internal static bool IsAddConnectionChoice(this IEnumValue value)
        => string.Equals(value?.Name,
                         Constants.ProfileParams.AddConnectionValue,
                         StringComparison.Ordinal);

    internal static async Task<bool> HasRemoteLinuxCapabilityAsync(this ConfiguredProject project, CancellationToken cancellationToken)
    {
        var provider = project
                .Services
                .ProjectPropertiesProvider;
        if (provider is null)
            return false;
        var helper = await provider
                .GetCommonProperties()
                .GetEvaluatedPropertyValueAsync(Constants
                .ProjectProperties
                .TargetFrameworkMoniker);
        return helper.IsPresent()
            && helper.StartsWith(Constants.NetCoreAppTfm, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<T> GetBrokeredServiceAsync<T>(this IAsyncServiceProvider sp, ServiceRpcDescriptor descriptor, CancellationToken cancellationToken = default)
        where T : class
    {
        if (cancellationToken == default)
            cancellationToken = VsShellUtilities.ShutdownToken;
        var svc = await sp
            .GetServiceAsync<IBrokeredServiceContainer, IBrokeredServiceContainer>()
            .ConfigureAwait(false);

        var broker = svc.GetFullAccessServiceBroker();
#pragma warning disable ISB001 // Dispose of proxies
        var ret = await broker.GetProxyAsync<T>(descriptor, cancellationToken).ConfigureAwait(false);
#pragma warning restore ISB001 // Dispose of proxies
        if (ret is null)
        {
            throw new ServiceUnavailableException(typeof(T));
        }
        return ret;
    }
}

internal static class MSBuildExtensions
{
    internal static string? GetTargetPath(this ITaskItem item)
        => item.GetMetadata("TargetPath");
    internal static string? GetFullPath(this ITaskItem item)
        => item.GetMetadata("FullPath");
    internal static string? GetRelativeDir(this ITaskItem item)
        => item.GetMetadata("RelativeDir");
    internal static string? GetFilename(this ITaskItem item)
        => $"{item.GetMetadata("Filename")}{item.GetExtension()}";
    internal static string? GetExtension(this ITaskItem item)
        => item.GetMetadata("Extension");
    internal static string? GetIdentity(this ITaskItem item)
        => item.GetMetadata("Identity");
    internal static DateTimeOffset? GetModifiedTime(this ITaskItem item)
    {
        var str = item.GetMetadata("ModifiedTime");
        return DateTimeOffset.Parse(str);
    }
}
