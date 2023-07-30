using System.Collections.ObjectModel;
using System.Diagnostics;
using LinuxDebugger.ProjectSystem.Serialization;
using LinuxDebugger.ProjectSystem.ViewModels;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    public interface IPathMappingEncoding
    {
        MappingMode Mode { get; }
        string Format(IEnumerable<FileUploadViewModel> mappings);
        void Parse(string str, IList<FileUploadViewModel> list);
    }

    public abstract class FileMappingEncoding : IPathMappingEncoding
    {
        private readonly Dictionary<string, string> cache  = new();
        public abstract MappingMode Mode { get; }
        public string Format(IEnumerable<FileUploadViewModel> mappings)
        {
            lock (this.cache)
            {
                Debug.Assert(this.cache.Count == 0);
                try
                {

                    foreach (var mapping in mappings)
                    {
                        if (mapping.LocalPath.IsMissing())
                            continue;
                        cache[mapping.LocalPath!.Trim()] = Format(mapping);
                    }
                    var items = LaunchProfileEnvironmentVariableEncoding.Format(cache);
                    return items;
                }
                finally
                {
                    this.cache.Clear();
                }
            }
        }

        protected abstract string Format(FileUploadViewModel mapping);

        public void Parse(string str, IList<FileUploadViewModel> list)
        {
            if (str.IsMissing()) return;
            lock (cache)
            {
                try
                {
                    LaunchProfileEnvironmentVariableEncoding.ParseIntoDictionary(str, cache);

                    foreach (var item in cache.Select(x =>
                    {
                        var ret = new FileUploadViewModel
                        {
                            LocalPath = x.Key,
                            Mode = this.Mode
                        };
                        Init(ret, x.Value);
                        return ret;
                    }))
                    {
                        list.Add(item);
                    }
                }
                finally
                {
                    cache.Clear();
                }
            }
        }

        protected abstract void Init(FileUploadViewModel ret, string value);
    }
    public sealed class UploadFileMappingEncoding : FileMappingEncoding
    {
        public static readonly UploadFileMappingEncoding Instance = new();
        public override MappingMode Mode => MappingMode.Upload;

        protected override string Format(FileUploadViewModel mapping)
            => mapping.RemotePath ?? string.Empty;

        protected override void Init(FileUploadViewModel ret, string value)
        {
            ret.RemotePath = value;
        }
    }
    public sealed class DownloadFileMappingEncoding : FileMappingEncoding
    {
        public static readonly DownloadFileMappingEncoding Instance = new();
        public override MappingMode Mode => MappingMode.Download;

        protected override string Format(FileUploadViewModel mapping)
        {
            var str = LaunchProfileEnvironmentVariableEncoding
                .FormatDownload(mapping.RemotePath,
                                mapping.Required,
                                mapping.OpenInEditor);
            return str;
        }

        protected override void Init(FileUploadViewModel ret, string value)
        {
            var val = DownloadMetadata.Parse(value);
            ret.Inititialize(val);
        }
    }
}
