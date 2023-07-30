using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using LinuxDebugger.ProjectSystem.ViewModels;
using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.Controls.Converters
{
    internal sealed class FileUploadPairConverter : IValueConverter
    {
        public static readonly FileUploadPairConverter Instance = new FileUploadPairConverter();
        public object Convert(object value,
                              Type targetType,
                              object parameter,
                              CultureInfo culture)
        {
            var collection = new ObservableCollection<FileUploadViewModel>();
            if (value is not IPropertyValueViewModel vm)
                return collection;

            if (vm.EvaluatedValue is not string str
                || str.IsMissing())
                return collection;


            var dict = LaunchProfileEnvironmentVariableEncoding
                .ParseIntoDictionary(str!);

            foreach(var item in dict)
            {
                collection.Add(new()
                {
                    LocalPath = item.Key,
                    RemotePath = item.Value,
                });
            }

            return collection;
        }

        public object? ConvertBack(object value,
                                  Type targetType,
                                  object parameter,
                                  CultureInfo culture)
        {
            if (value is not IReadOnlyCollection<FileUploadViewModel> vals
                || vals.Count == 0)
                return null;

            var sb = new Dictionary<string, string>();
            foreach(var item in vals.Where(x => x.LocalPath.IsPresent()))
            {
                sb[item.LocalPath!.Trim()] = item.RemotePath ?? string.Empty;
            }
            return LaunchProfileEnvironmentVariableEncoding.Format(sb);
        }
    }
}
